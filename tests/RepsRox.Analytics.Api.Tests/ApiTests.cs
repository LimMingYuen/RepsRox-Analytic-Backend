using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RepsRox.Analytics.Api.Analytics;
using RepsRox.Analytics.Api.Import;

namespace RepsRox.Analytics.Api.Tests;

public class ApiTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public ApiTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<List<ImportResult>> Upload(params (string Name, string Text)[] files)
    {
        using var form = new MultipartFormDataContent();
        foreach (var (name, text) in files)
            form.Add(new StringContent(text, Encoding.UTF8, "text/csv"), "files", name);
        var response = await _client.PostAsync("/api/imports", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ImportResult>>(Json))!;
    }

    private async Task<T> Get<T>(string url) => (await _client.GetFromJsonAsync<T>(url, Json))!;

    private Task<List<ImportResult>> UploadAugust() => Upload(
        ("repsrox-training-2026-08.csv", Samples.Training),
        ("repsrox-meals-2026-08.csv", Samples.Meals),
        ("repsrox-races-2026-08.csv", Samples.Races()),
        ("repsrox-weight-2026-08.csv", Samples.Weight));

    [Fact]
    public async Task A_month_imports_and_reads_back_as_an_overview()
    {
        var results = await UploadAugust();
        Assert.All(results, r => Assert.True(r.Imported, r.Error));

        var overview = await Get<OverviewDto>("/api/overview");
        Assert.Equal(2, overview.Training.Sessions);
        Assert.Equal(5, overview.Training.Sets);
        // 5×120 + 5×122.5 + 3×130; the sled's metres move nothing and the unloaded row nothing either.
        Assert.Equal(1602.5m, overview.Training.VolumeKg);
        Assert.Equal(1, overview.Nutrition.DaysLogged);
        Assert.Equal(820, overview.Nutrition.AvgKcal);
        Assert.Equal(2, overview.Races.Count);
        Assert.Equal(4320, overview.Races.BestSeconds);
        Assert.Equal(81.5m, overview.Weight.LatestKg);
        Assert.Equal(-0.5m, overview.Weight.ChangeKg);
        Assert.Equal(new DateOnly(2026, 8, 2), overview.FirstDate);
    }

    [Fact]
    public async Task Importing_a_month_again_replaces_it_rather_than_doubling_it()
    {
        await UploadAugust();
        var again = await Upload(("repsrox-weight-2026-08.csv", "Date,Weight kg\r\n2026-08-30,80.0\r\n"));

        Assert.True(again[0].Replaced);
        var weight = await Get<List<WeightPointDto>>("/api/weight");
        Assert.Single(weight);
        Assert.Equal(80.0m, weight[0].WeightKg);
        Assert.Equal(4, (await Get<List<ImportDto>>("/api/imports")).Count);
    }

    [Fact]
    public async Task A_bad_file_is_reported_without_holding_back_the_rest()
    {
        var results = await Upload(("notes.csv", "hello,world\r\n"), ("repsrox-weight-2026-08.csv", Samples.Weight));

        Assert.False(results[0].Imported);
        Assert.NotNull(results[0].Error);
        Assert.True(results[1].Imported);
    }

    [Fact]
    public async Task Deleting_an_import_takes_its_rows_with_it()
    {
        await UploadAugust();
        var training = (await Get<List<ImportDto>>("/api/imports")).Single(i => i.Kind == Data.SheetKind.Training);

        var response = await _client.DeleteAsync($"/api/imports/{training.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await Get<List<SessionDto>>("/api/training/sessions"));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/imports/{training.Id}")).StatusCode);
    }

    [Fact]
    public async Task Training_reads_by_session_week_and_exercise()
    {
        await UploadAugust();

        var sessions = await Get<List<SessionDto>>("/api/training/sessions");
        Assert.Equal(["Upper pull", "Lower push"], sessions.Select(s => s.Name));
        Assert.Equal(new TopSetDto("Back squat", 5, 122.5m), sessions[1].TopSet);

        var detail = await Get<SessionDetailDto>($"/api/training/sessions/{sessions[1].Id}");
        Assert.Equal(["Back squat", "Sled push"], detail.Exercises.Select(e => e.Name));

        // The 15th is a Saturday, the 18th the Tuesday after.
        var weeks = await Get<List<VolumePointDto>>("/api/training/volume?interval=Week");
        Assert.Equal([new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 17)], weeks.Select(w => w.Period));

        var squat = await Get<List<ExerciseProgressDto>>("/api/training/progress?exercise=Back%20squat");
        Assert.Equal(2, squat.Count);
        // Epley: 122.5 × (1 + 5/30), to a decimal place.
        Assert.Equal(142.9m, squat[0].Estimated1RmKg);

        var exercises = await Get<List<ExerciseSummaryDto>>("/api/training/exercises");
        Assert.Equal("Back squat", exercises[0].Name);
        Assert.Equal(130m, exercises[0].BestWeightKg);
    }

    [Fact]
    public async Task Ranges_cut_every_list()
    {
        await UploadAugust();

        var nutrition = await Get<List<NutritionDayDto>>("/api/nutrition/daily?from=2026-08-16&to=2026-08-31");
        var day = Assert.Single(nutrition);
        Assert.Equal(800, day.PlannedKcal);
        Assert.Equal(0, day.LoggedKcal);

        var races = await Get<List<RaceDto>>("/api/races?to=2026-08-20");
        var race = Assert.Single(races);
        Assert.Equal(275, race.Legs[0].TargetSeconds);

        var legs = await Get<List<LegStatsDto>>("/api/races/legs");
        Assert.Equal(16, legs.Count);
        Assert.Equal(2, legs[0].Races);
        Assert.Equal(270, legs[0].BestSeconds);
    }

    [Fact]
    public async Task The_weight_trend_looks_back_past_the_start_of_the_range()
    {
        await Upload(("repsrox-weight-2026-08.csv", "Date,Weight kg\r\n2026-08-01,82.0\r\n2026-08-03,81.0\r\n"));

        var weight = await Get<List<WeightPointDto>>("/api/weight?from=2026-08-02");

        var point = Assert.Single(weight);
        Assert.Equal(81.5m, point.TrendKg);
    }
}

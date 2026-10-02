using Microsoft.AspNetCore.Mvc;
using RepsRox.Analytics.Api.Analytics;

namespace RepsRox.Analytics.Api.Controllers;

/// <summary>
/// Read-only figures for the dashboard. Every list takes an optional inclusive
/// <c>from</c> / <c>to</c> date (yyyy-MM-dd); leave either off to run to the edge of the data.
/// </summary>
[ApiController]
[Route("api")]
public class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    [HttpGet("overview")]
    public Task<OverviewDto> Overview([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        analytics.OverviewAsync(new DateRange(from, to), ct);

    [HttpGet("training/sessions")]
    public Task<List<SessionDto>> Sessions([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        analytics.SessionsAsync(new DateRange(from, to), ct);

    [HttpGet("training/sessions/{id:int}")]
    public async Task<ActionResult<SessionDetailDto>> Session(int id, CancellationToken ct) =>
        await analytics.SessionAsync(id, ct) is { } session ? session : NotFound();

    [HttpGet("training/volume")]
    public Task<List<VolumePointDto>> Volume(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Interval interval = Interval.Week,
        CancellationToken ct = default) =>
        analytics.VolumeAsync(new DateRange(from, to), interval, ct);

    [HttpGet("training/exercises")]
    public Task<List<ExerciseSummaryDto>> Exercises([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        analytics.ExercisesAsync(new DateRange(from, to), ct);

    /// <summary>One exercise session by session. The name goes in the query: names carry spaces and slashes.</summary>
    [HttpGet("training/progress")]
    public Task<List<ExerciseProgressDto>> Progress(
        [FromQuery] string exercise,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        analytics.ExerciseProgressAsync(exercise, new DateRange(from, to), ct);

    [HttpGet("nutrition/daily")]
    public Task<List<NutritionDayDto>> Nutrition([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        analytics.NutritionAsync(new DateRange(from, to), ct);

    [HttpGet("races")]
    public Task<List<RaceDto>> Races([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        analytics.RacesAsync(new DateRange(from, to), ct);

    [HttpGet("races/legs")]
    public Task<List<LegStatsDto>> Legs([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        analytics.LegStatsAsync(new DateRange(from, to), ct);

    [HttpGet("weight")]
    public Task<List<WeightPointDto>> Weight([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        analytics.WeightAsync(new DateRange(from, to), ct);
}

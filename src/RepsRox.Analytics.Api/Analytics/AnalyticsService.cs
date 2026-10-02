using Microsoft.EntityFrameworkCore;
using RepsRox.Analytics.Api.Data;

namespace RepsRox.Analytics.Api.Analytics;

public enum Interval
{
    Week,
    Month,
}

/// <summary>
/// The figures the dashboard draws. Rows are cut to the range in SQL and summed here:
/// one person's training runs to a few thousand sets a year, and the week and
/// estimated-max sums read far plainer in C# than in any one SQL dialect.
/// </summary>
public class AnalyticsService(AnalyticsDbContext db)
{
    /// <summary>A rep-range past which an estimated max stops meaning much.</summary>
    private const int MaxRepsForEstimate = 12;

    private const int WeightTrendDays = 7;

    public async Task<OverviewDto> OverviewAsync(DateRange range, CancellationToken ct)
    {
        var sessions = await SessionsQuery(range).Include(s => s.Sets).ToListAsync(ct);
        var meals = await MealsQuery(range).ToListAsync(ct);
        var races = await RacesQuery(range).ToListAsync(ct);
        var weighIns = await WeighInsQuery(range).OrderBy(w => w.Date).ToListAsync(ct);

        var dates = sessions.Select(s => s.Date)
            .Concat(meals.Select(m => m.Date))
            .Concat(races.Select(r => r.Date))
            .Concat(weighIns.Select(w => w.Date))
            .ToList();

        var loggedDays = meals.Where(m => m.Logged).GroupBy(m => m.Date).ToList();
        int? Avg(Func<Meal, int> pick) => loggedDays.Count == 0
            ? null
            : (int)Math.Round(loggedDays.Average(d => d.Sum(pick)));

        var first = weighIns.FirstOrDefault();
        var last = weighIns.LastOrDefault();

        return new OverviewDto(
            dates.Count == 0 ? null : dates.Min(),
            dates.Count == 0 ? null : dates.Max(),
            new TrainingTotals(
                sessions.Count,
                sessions.Sum(s => s.Sets.Count),
                sessions.Sum(s => s.Sets.Sum(Volume)),
                (int)Math.Round(sessions.Sum(s => s.DurationSeconds) / 60.0)),
            new NutritionTotals(
                loggedDays.Count,
                meals.Count,
                meals.Count(m => m.Logged),
                Avg(m => m.Kcal),
                Avg(m => m.ProteinG),
                Avg(m => m.CarbsG)),
            new RaceTotals(
                races.Count,
                races.Count(r => r.Complete),
                races.Where(r => r.Complete).Select(r => (int?)r.TotalSeconds).Min()),
            new WeightTotals(
                last?.Date,
                last?.WeightKg,
                first is null || last is null ? null : last.WeightKg - first.WeightKg));
    }

    public async Task<List<SessionDto>> SessionsAsync(DateRange range, CancellationToken ct)
    {
        var sessions = await SessionsQuery(range)
            .Include(s => s.Sets)
            .OrderByDescending(s => s.Date).ThenByDescending(s => s.Id)
            .ToListAsync(ct);

        return sessions.Select(s =>
        {
            var top = s.Sets
                .Where(x => x.Unit == SetUnit.Reps && x.WeightKg is > 0)
                .MaxBy(x => x.WeightKg);
            return new SessionDto(
                s.Id,
                s.Date,
                s.Name,
                s.DurationSeconds,
                s.Sets.Select(x => x.Exercise).Distinct().Count(),
                s.Sets.Count,
                s.Sets.Sum(Volume),
                top is null ? null : new TopSetDto(top.Exercise, top.Amount, top.WeightKg!.Value));
        }).ToList();
    }

    public async Task<SessionDetailDto?> SessionAsync(int id, CancellationToken ct)
    {
        var session = await db.TrainingSessions.AsNoTracking()
            .Include(s => s.Sets)
            .SingleOrDefaultAsync(s => s.Id == id, ct);
        if (session is null) return null;

        // Exercises in the order they were first worked, sets in the order they were banked.
        var exercises = session.Sets
            .OrderBy(x => x.Position)
            .GroupBy(x => x.Exercise)
            .Select(g => new ExerciseDto(g.Key, g.Select(x => new SetDto(x.SetNumber, x.Amount, x.Unit, x.WeightKg)).ToList()))
            .ToList();
        return new SessionDetailDto(session.Id, session.Date, session.Name, session.DurationSeconds, exercises);
    }

    public async Task<List<VolumePointDto>> VolumeAsync(DateRange range, Interval interval, CancellationToken ct)
    {
        var sessions = await SessionsQuery(range).Include(s => s.Sets).ToListAsync(ct);
        return sessions
            .GroupBy(s => PeriodStart(s.Date, interval))
            .OrderBy(g => g.Key)
            .Select(g => new VolumePointDto(
                g.Key,
                g.Count(),
                g.Sum(s => s.Sets.Count),
                g.Sum(s => s.Sets.Sum(Volume)),
                (int)Math.Round(g.Sum(s => s.DurationSeconds) / 60.0)))
            .ToList();
    }

    public async Task<List<ExerciseSummaryDto>> ExercisesAsync(DateRange range, CancellationToken ct)
    {
        var sets = await SetsQuery(range).ToListAsync(ct);
        return sets
            .GroupBy(x => x.Exercise)
            .Select(g => new ExerciseSummaryDto(
                g.Key,
                // An exercise is logged one way; should it ever have been both, the commoner wins.
                g.GroupBy(x => x.Unit).MaxBy(u => u.Count())!.Key,
                g.Select(x => x.SessionId).Distinct().Count(),
                g.Count(),
                g.Sum(Volume),
                g.Max(x => x.WeightKg),
                g.Select(Estimated1Rm).Max(),
                g.Max(x => x.Session.Date)))
            .OrderByDescending(e => e.Sessions).ThenBy(e => e.Name)
            .ToList();
    }

    public async Task<List<ExerciseProgressDto>> ExerciseProgressAsync(string exercise, DateRange range, CancellationToken ct)
    {
        var sets = await SetsQuery(range).Where(x => x.Exercise == exercise).ToListAsync(ct);
        return sets
            .GroupBy(x => x.SessionId)
            .Select(g => new ExerciseProgressDto(
                g.First().Session.Date,
                g.Count(),
                g.Sum(x => x.Amount),
                g.Max(x => x.WeightKg),
                g.Sum(Volume),
                g.Select(Estimated1Rm).Max()))
            .OrderBy(p => p.Date)
            .ToList();
    }

    public async Task<List<NutritionDayDto>> NutritionAsync(DateRange range, CancellationToken ct)
    {
        var meals = await MealsQuery(range).ToListAsync(ct);
        return meals
            .GroupBy(m => m.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var eaten = g.Where(m => m.Logged).ToList();
                return new NutritionDayDto(
                    g.Key,
                    g.Count(),
                    eaten.Count,
                    g.Sum(m => m.Kcal),
                    g.Sum(m => m.ProteinG),
                    g.Sum(m => m.CarbsG),
                    eaten.Sum(m => m.Kcal),
                    eaten.Sum(m => m.ProteinG),
                    eaten.Sum(m => m.CarbsG));
            })
            .ToList();
    }

    public async Task<List<RaceDto>> RacesAsync(DateRange range, CancellationToken ct)
    {
        var races = await RacesQuery(range)
            .Include(r => r.Legs)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.Id)
            .ToListAsync(ct);
        return races.Select(r => new RaceDto(
            r.Id,
            r.Date,
            r.TotalSeconds,
            r.Complete,
            r.Legs.OrderBy(l => l.Position)
                .Select(l => new RaceLegDto(l.Position, l.Tag, l.Name, l.Seconds, RaceCourse.TargetSeconds(l.Tag)))
                .ToList()))
            .ToList();
    }

    public async Task<List<LegStatsDto>> LegStatsAsync(DateRange range, CancellationToken ct)
    {
        var legs = await db.RaceLegs.AsNoTracking()
            .Include(l => l.Race)
            .Where(l => (range.From == null || l.Race.Date >= range.From) && (range.To == null || l.Race.Date <= range.To))
            .ToListAsync(ct);
        return legs
            .GroupBy(l => l.Position)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var latest = g.MaxBy(l => (l.Race.Date, l.RaceId))!;
                return new LegStatsDto(
                    g.Key,
                    latest.Tag,
                    latest.Name,
                    RaceCourse.TargetSeconds(latest.Tag),
                    g.Count(),
                    (int)Math.Round(g.Average(l => l.Seconds)),
                    g.Min(l => l.Seconds),
                    latest.Seconds);
            })
            .ToList();
    }

    public async Task<List<WeightPointDto>> WeightAsync(DateRange range, CancellationToken ct)
    {
        // The trend looks back a week, so the first points in range still get a full window.
        var lookBack = range with { From = range.From?.AddDays(-(WeightTrendDays - 1)) };
        var weighIns = await WeighInsQuery(lookBack).OrderBy(w => w.Date).ToListAsync(ct);

        return weighIns
            .Where(w => range.Contains(w.Date))
            .Select(w =>
            {
                var window = weighIns.Where(x => x.Date <= w.Date && x.Date > w.Date.AddDays(-WeightTrendDays));
                return new WeightPointDto(w.Date, w.WeightKg, Math.Round(window.Average(x => x.WeightKg), 1));
            })
            .ToList();
    }

    public async Task<List<ImportDto>> ImportsAsync(CancellationToken ct) =>
        await db.Imports.AsNoTracking()
            .OrderByDescending(i => i.Month).ThenBy(i => i.Kind)
            .Select(i => new ImportDto(i.Id, i.Kind, i.Month, i.FileName, i.RowCount, i.ImportedAtUtc))
            .ToListAsync(ct);

    public async Task<bool> DeleteImportAsync(int id, CancellationToken ct) =>
        await db.Imports.Where(i => i.Id == id).ExecuteDeleteAsync(ct) > 0;

    /// <summary>Load moved, as the app counts it: metres are a distance and move nothing.</summary>
    public static decimal Volume(TrainingSet set) =>
        set.Unit == SetUnit.Reps && set.WeightKg is { } kg ? set.Amount * kg : 0m;

    /// <summary>Epley's estimate of a one-rep max, for a loaded set of a dozen reps or fewer.</summary>
    public static decimal? Estimated1Rm(TrainingSet set)
    {
        if (set.Unit != SetUnit.Reps || set.WeightKg is not > 0 || set.Amount is < 1 or > MaxRepsForEstimate) return null;
        var kg = set.WeightKg.Value;
        return set.Amount == 1 ? kg : Math.Round(kg * (1 + set.Amount / 30m), 1);
    }

    /// <summary>The Monday a week opens on, or the first of the month.</summary>
    public static DateOnly PeriodStart(DateOnly date, Interval interval) => interval switch
    {
        Interval.Month => new DateOnly(date.Year, date.Month, 1),
        _ => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
    };

    private IQueryable<TrainingSession> SessionsQuery(DateRange r) =>
        db.TrainingSessions.AsNoTracking().Where(s => (r.From == null || s.Date >= r.From) && (r.To == null || s.Date <= r.To));

    private IQueryable<TrainingSet> SetsQuery(DateRange r) =>
        db.TrainingSets.AsNoTracking().Include(x => x.Session)
            .Where(x => (r.From == null || x.Session.Date >= r.From) && (r.To == null || x.Session.Date <= r.To));

    private IQueryable<Meal> MealsQuery(DateRange r) =>
        db.Meals.AsNoTracking().Where(m => (r.From == null || m.Date >= r.From) && (r.To == null || m.Date <= r.To));

    private IQueryable<Race> RacesQuery(DateRange r) =>
        db.Races.AsNoTracking().Where(x => (r.From == null || x.Date >= r.From) && (r.To == null || x.Date <= r.To));

    private IQueryable<WeighIn> WeighInsQuery(DateRange r) =>
        db.WeighIns.AsNoTracking().Where(w => (r.From == null || w.Date >= r.From) && (r.To == null || w.Date <= r.To));
}

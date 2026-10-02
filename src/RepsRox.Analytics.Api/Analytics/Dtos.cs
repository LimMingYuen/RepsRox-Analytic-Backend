using RepsRox.Analytics.Api.Data;

namespace RepsRox.Analytics.Api.Analytics;

/// <summary>An inclusive date range. Either end left open runs to the edge of the data.</summary>
public record DateRange(DateOnly? From, DateOnly? To)
{
    public bool Contains(DateOnly date) => (From is null || date >= From) && (To is null || date <= To);
}

public record ImportDto(int Id, SheetKind Kind, DateOnly Month, string FileName, int RowCount, DateTime ImportedAtUtc);

public record OverviewDto(
    DateOnly? FirstDate,
    DateOnly? LastDate,
    TrainingTotals Training,
    NutritionTotals Nutrition,
    RaceTotals Races,
    WeightTotals Weight);

public record TrainingTotals(int Sessions, int Sets, decimal VolumeKg, int Minutes);

public record NutritionTotals(
    int DaysLogged,
    int MealsPlanned,
    int MealsLogged,
    int? AvgKcal,
    int? AvgProteinG,
    int? AvgCarbsG);

public record RaceTotals(int Count, int Completed, int? BestSeconds);

public record WeightTotals(DateOnly? LatestDate, decimal? LatestKg, decimal? ChangeKg);

public record SessionDto(
    int Id,
    DateOnly Date,
    string Name,
    int DurationSeconds,
    int Exercises,
    int Sets,
    decimal VolumeKg,
    TopSetDto? TopSet);

public record TopSetDto(string Exercise, int Reps, decimal WeightKg);

public record SessionDetailDto(int Id, DateOnly Date, string Name, int DurationSeconds, List<ExerciseDto> Exercises);

public record ExerciseDto(string Name, List<SetDto> Sets);

public record SetDto(int SetNumber, int Amount, SetUnit Unit, decimal? WeightKg);

public record VolumePointDto(DateOnly Period, int Sessions, int Sets, decimal VolumeKg, int Minutes);

public record ExerciseSummaryDto(
    string Name,
    SetUnit Unit,
    int Sessions,
    int Sets,
    decimal VolumeKg,
    decimal? BestWeightKg,
    decimal? BestEstimated1RmKg,
    DateOnly LastDate);

public record ExerciseProgressDto(
    DateOnly Date,
    int Sets,
    int TotalAmount,
    decimal? TopWeightKg,
    decimal VolumeKg,
    decimal? Estimated1RmKg);

public record NutritionDayDto(
    DateOnly Date,
    int MealsPlanned,
    int MealsLogged,
    int PlannedKcal,
    int PlannedProteinG,
    int PlannedCarbsG,
    int LoggedKcal,
    int LoggedProteinG,
    int LoggedCarbsG);

public record RaceDto(int Id, DateOnly Date, int TotalSeconds, bool Complete, List<RaceLegDto> Legs);

public record RaceLegDto(int Position, string Tag, string Name, int Seconds, int? TargetSeconds);

public record LegStatsDto(
    int Position,
    string Tag,
    string Name,
    int? TargetSeconds,
    int Races,
    int AvgSeconds,
    int BestSeconds,
    int LatestSeconds);

public record WeightPointDto(DateOnly Date, decimal WeightKg, decimal TrendKg);

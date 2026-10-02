using RepsRox.Analytics.Api.Data;

namespace RepsRox.Analytics.Api.Import;

/// <summary>
/// A sheet read out of its CSV and checked, but not yet stored. Exactly one of the
/// row lists is filled, the one matching <see cref="Kind"/>.
/// </summary>
public record ParsedSheet(SheetKind Kind, DateOnly Month, int RowCount)
{
    public List<TrainingSession> Sessions { get; init; } = [];
    public List<Meal> Meals { get; init; } = [];
    public List<Race> Races { get; init; } = [];
    public List<WeighIn> WeighIns { get; init; } = [];
}

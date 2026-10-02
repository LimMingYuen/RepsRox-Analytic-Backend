namespace RepsRox.Analytics.Api.Data;

/// <summary>The four sheets the app's "Export month" sends out, one CSV each.</summary>
public enum SheetKind
{
    Training,
    Meals,
    Races,
    Weight,
}

public enum SetUnit
{
    Reps,
    Metres,
}

/// <summary>
/// One CSV file as it was read in. Every row below hangs off the import that
/// brought it, so re-importing a month's sheet replaces it whole: there is at most
/// one import per kind and month.
/// </summary>
public class SheetImport
{
    public int Id { get; set; }
    public SheetKind Kind { get; set; }

    /// <summary>The first day of the month the sheet covers.</summary>
    public DateOnly Month { get; set; }

    public string FileName { get; set; } = "";
    public int RowCount { get; set; }
    public DateTime ImportedAtUtc { get; set; }
}

/// <summary>
/// A strength session. The training sheet carries a row a set and no session id,
/// so a session is the run of consecutive rows sharing a date, name and duration.
/// </summary>
public class TrainingSession
{
    public int Id { get; set; }
    public int ImportId { get; set; }
    public SheetImport Import { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
    public int DurationSeconds { get; set; }
    public List<TrainingSet> Sets { get; set; } = [];
}

public class TrainingSet
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public TrainingSession Session { get; set; } = null!;

    /// <summary>Where the set sat in the session, so it reads back in the order it was worked.</summary>
    public int Position { get; set; }

    public string Exercise { get; set; } = "";
    public int SetNumber { get; set; }

    /// <summary>Reps, or metres for a sled or a carry — see <see cref="Unit"/>.</summary>
    public int Amount { get; set; }

    public SetUnit Unit { get; set; }

    /// <summary>Null when the app logged no load, or one that is not a number.</summary>
    public decimal? WeightKg { get; set; }
}

public class Meal
{
    public int Id { get; set; }
    public int ImportId { get; set; }
    public SheetImport Import { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public int Kcal { get; set; }
    public int ProteinG { get; set; }
    public int CarbsG { get; set; }

    /// <summary>Whether the meal was checked in as eaten, rather than only planned.</summary>
    public bool Logged { get; set; }
}

/// <summary>A race sim. One ended early holds fewer legs than the course.</summary>
public class Race
{
    public int Id { get; set; }
    public int ImportId { get; set; }
    public SheetImport Import { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int TotalSeconds { get; set; }
    public bool Complete { get; set; }
    public List<RaceLeg> Legs { get; set; } = [];
}

public class RaceLeg
{
    public int Id { get; set; }
    public int RaceId { get; set; }
    public Race Race { get; set; } = null!;

    /// <summary>Course order, from zero.</summary>
    public int Position { get; set; }

    public string Tag { get; set; } = "";
    public string Name { get; set; } = "";
    public int Seconds { get; set; }
}

public class WeighIn
{
    public int Id { get; set; }
    public int ImportId { get; set; }
    public SheetImport Import { get; set; } = null!;
    public DateOnly Date { get; set; }
    public decimal WeightKg { get; set; }
}

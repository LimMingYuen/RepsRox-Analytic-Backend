using System.Globalization;
using System.Text.RegularExpressions;
using RepsRox.Analytics.Api.Data;

namespace RepsRox.Analytics.Api.Import;

/// <summary>
/// Turns one of the app's month export files back into rows. The sheet is told
/// apart by its header rather than its name, since a file can be renamed on the way;
/// the name still settles the month when the sheet holds no rows to date it by.
/// Mirrors <c>MonthExport.kt</c> in the Reps-Rox app.
/// </summary>
public static partial class SheetParser
{
    private static readonly string[] TrainingHeader = ["Date", "Session", "Duration", "Exercise", "Set", "Amount", "Unit", "Weight kg"];
    private static readonly string[] MealsHeader = ["Date", "Meal", "Detail", "Kcal", "Protein g", "Carbs g", "Logged"];
    private static readonly string[] RacesHeaderLead = ["Date", "Time", "Complete"];
    private static readonly string[] WeightHeader = ["Date", "Weight kg"];

    [GeneratedRegex(@"(\d{4})-(\d{2})", RegexOptions.RightToLeft)]
    private static partial Regex MonthInName();

    public static ParsedSheet Parse(string fileName, string text)
    {
        var records = Csv.Parse(text);
        if (records.Count == 0) throw new SheetFormatException("The file is empty.");

        var header = records[0].Select(h => h.Trim()).ToArray();
        var rows = records.Skip(1).Select((cells, index) => new Row(index + 2, cells)).ToList();
        var nameMonth = MonthFromName(fileName);

        if (header.SequenceEqual(TrainingHeader)) return Training(rows, nameMonth);
        if (header.SequenceEqual(MealsHeader)) return Meals(rows, nameMonth);
        if (header.SequenceEqual(WeightHeader)) return Weight(rows, nameMonth);
        if (header.Length > RacesHeaderLead.Length && header.Take(RacesHeaderLead.Length).SequenceEqual(RacesHeaderLead))
            return Races(header, rows, nameMonth);

        throw new SheetFormatException(
            "The header does not match any RepsRox export sheet (training, meals, races or weight).");
    }

    private static ParsedSheet Training(List<Row> rows, DateOnly? nameMonth)
    {
        var sessions = new List<TrainingSession>();
        TrainingSession? current = null;

        foreach (var row in rows)
        {
            row.Expect(TrainingHeader.Length);
            var date = row.Date(0);
            var name = row.Text(1);
            var duration = row.Clock(2);

            // The sheet runs session by session, so a change of date, name or clock opens the next one.
            if (current is null || current.Date != date || current.Name != name || current.DurationSeconds != duration)
            {
                current = new TrainingSession { Date = date, Name = name, DurationSeconds = duration };
                sessions.Add(current);
            }

            current.Sets.Add(new TrainingSet
            {
                Position = current.Sets.Count,
                Exercise = row.Text(3),
                SetNumber = row.Int(4),
                Amount = row.Int(5),
                Unit = row.Text(6) switch
                {
                    "reps" => SetUnit.Reps,
                    "m" => SetUnit.Metres,
                    var other => throw row.Error($"unit \"{other}\" is neither reps nor m"),
                },
                WeightKg = decimal.TryParse(row.Text(7), NumberStyles.Number, CultureInfo.InvariantCulture, out var kg) ? kg : null,
            });
        }

        var month = MonthOf(rows.Count, sessions.Select(s => s.Date), nameMonth);
        return new ParsedSheet(SheetKind.Training, month, rows.Count) { Sessions = sessions };
    }

    private static ParsedSheet Meals(List<Row> rows, DateOnly? nameMonth)
    {
        var meals = rows.Select(row =>
        {
            row.Expect(MealsHeader.Length);
            return new Meal
            {
                Date = row.Date(0),
                Name = row.Text(1),
                Detail = row.Text(2),
                Kcal = row.Int(3),
                ProteinG = row.Int(4),
                CarbsG = row.Int(5),
                Logged = row.Text(6) switch
                {
                    "yes" => true,
                    "no" => false,
                    var other => throw row.Error($"logged \"{other}\" is neither yes nor no"),
                },
            };
        }).ToList();

        var month = MonthOf(rows.Count, meals.Select(m => m.Date), nameMonth);
        return new ParsedSheet(SheetKind.Meals, month, rows.Count) { Meals = meals };
    }

    private static ParsedSheet Races(string[] header, List<Row> rows, DateOnly? nameMonth)
    {
        // A column a leg, headed "RUN 1 1 km run": the tag is its first two words.
        var legs = header.Skip(RacesHeaderLead.Length).Select(column =>
        {
            var words = column.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            return words.Length == 3
                ? (Tag: $"{words[0]} {words[1]}", Name: words[2])
                : throw new SheetFormatException($"Race leg column \"{column}\" is not \"TAG N Name\".");
        }).ToArray();

        var races = rows.Select(row =>
        {
            row.Expect(header.Length);
            var race = new Race
            {
                Date = row.Date(0),
                TotalSeconds = row.Clock(1),
                Complete = row.Text(2) == "yes",
            };
            for (var i = 0; i < legs.Length; i++)
            {
                // A sim ended early leaves its unraced legs blank.
                if (row.Text(3 + i).Length == 0) continue;
                race.Legs.Add(new RaceLeg { Position = i, Tag = legs[i].Tag, Name = legs[i].Name, Seconds = row.Clock(3 + i) });
            }
            return race;
        }).ToList();

        var month = MonthOf(rows.Count, races.Select(r => r.Date), nameMonth);
        return new ParsedSheet(SheetKind.Races, month, rows.Count) { Races = races };
    }

    private static ParsedSheet Weight(List<Row> rows, DateOnly? nameMonth)
    {
        var weighIns = rows.Select(row =>
        {
            row.Expect(WeightHeader.Length);
            return new WeighIn { Date = row.Date(0), WeightKg = row.Decimal(1) };
        }).ToList();

        var month = MonthOf(rows.Count, weighIns.Select(w => w.Date), nameMonth);
        return new ParsedSheet(SheetKind.Weight, month, rows.Count) { WeighIns = weighIns };
    }

    /// <summary>
    /// The month a sheet covers. Every row must fall in one month, and agree with the
    /// file name when it carries one — importing replaces that month, so a stray
    /// row would land somewhere it can never be replaced from.
    /// </summary>
    private static DateOnly MonthOf(int rowCount, IEnumerable<DateOnly> dates, DateOnly? nameMonth)
    {
        var months = dates.Select(d => new DateOnly(d.Year, d.Month, 1)).Distinct().ToList();
        if (months.Count > 1)
            throw new SheetFormatException($"The rows span {months.Count} months; an export sheet holds one.");
        if (months.Count == 1 && nameMonth is { } named && named != months[0])
            throw new SheetFormatException($"The file is named for {named:yyyy-MM} but its rows are dated {months[0]:yyyy-MM}.");
        if (rowCount == 0 && nameMonth is null)
            throw new SheetFormatException("The sheet has no rows, and its name carries no month to file it under.");
        return months.Count == 1 ? months[0] : nameMonth!.Value;
    }

    private static DateOnly? MonthFromName(string fileName)
    {
        var match = MonthInName().Match(Path.GetFileNameWithoutExtension(fileName));
        if (!match.Success) return null;
        var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        return month is >= 1 and <= 12 ? new DateOnly(year, month, 1) : null;
    }

    /// <summary>One data row, with its line in the file so an error can point at it.</summary>
    private sealed class Row(int line, string[] cells)
    {
        public void Expect(int count)
        {
            if (cells.Length != count) throw Error($"has {cells.Length} cells, expected {count}");
        }

        public string Text(int index) => Csv.Unprotect(cells[index].Trim());

        public DateOnly Date(int index) =>
            DateOnly.TryParseExact(Text(index), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : throw Error($"date \"{Text(index)}\" is not yyyy-MM-dd");

        public int Int(int index) =>
            int.TryParse(Text(index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw Error($"\"{Text(index)}\" is not a whole number");

        public decimal Decimal(int index) =>
            decimal.TryParse(Text(index), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw Error($"\"{Text(index)}\" is not a number");

        /// <summary>m:ss or h:mm:ss, as the app writes a duration, into seconds.</summary>
        public int Clock(int index)
        {
            var parts = Text(index).Split(':');
            var seconds = 0;
            foreach (var part in parts)
            {
                if (parts.Length is < 2 or > 3 || !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                    throw Error($"time \"{Text(index)}\" is not m:ss or h:mm:ss");
                seconds = seconds * 60 + n;
            }
            return seconds;
        }

        public SheetFormatException Error(string problem) => new($"Line {line}: {problem}.");
    }
}

namespace RepsRox.Analytics.Api.Tests;

/// <summary>Sheets as the app writes them (see MonthExportTest.kt): CRLF, quoted cells, apostrophe-guarded formulas.</summary>
public static class Samples
{
    public const string Training =
        "Date,Session,Duration,Exercise,Set,Amount,Unit,Weight kg\r\n" +
        "2026-08-15,Lower push,58:12,Back squat,1,5,reps,120\r\n" +
        "2026-08-15,Lower push,58:12,Back squat,2,5,reps,122.5\r\n" +
        "2026-08-15,Lower push,58:12,Sled push,1,25,m,150\r\n" +
        "2026-08-18,Upper pull,45:00,Back squat,1,3,reps,130\r\n" +
        "2026-08-18,Upper pull,45:00,\"Row, bent\",1,8,reps,\r\n";

    public const string Meals =
        "Date,Meal,Detail,Kcal,Protein g,Carbs g,Logged\r\n" +
        "2026-08-15,Breakfast,\"Oats, whey, banana\",620,40,70,yes\r\n" +
        "2026-08-15,'=SUM(A1),,200,10,5,yes\r\n" +
        "2026-08-16,Dinner,,800,0,0,no\r\n";

    public static string Races()
    {
        var legs = new[]
        {
            "RUN 1 1 km run", "STN 1 SkiErg", "RUN 2 1 km run", "STN 2 Sled push", "RUN 3 1 km run",
            "STN 3 Sled pull", "RUN 4 1 km run", "STN 4 Burpee broad jump", "RUN 5 1 km run", "STN 5 Rowing",
            "RUN 6 1 km run", "STN 6 Farmers carry", "RUN 7 1 km run", "STN 7 Sandbag lunges", "RUN 8 1 km run",
            "STN 8 Wall balls",
        };
        var full = string.Join(",", Enumerable.Repeat("4:30", 16));
        return "Date,Time,Complete," + string.Join(",", legs) + "\r\n" +
            "2026-08-20,0:10:00,no,4:30,4:25" + new string(',', 14) + "\r\n" +
            "2026-08-27,1:12:00,yes," + full + "\r\n";
    }

    public const string Weight = "Date,Weight kg\r\n2026-08-02,82.0\r\n2026-08-16,81.5\r\n";
}

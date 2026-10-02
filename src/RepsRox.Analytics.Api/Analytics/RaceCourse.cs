namespace RepsRox.Analytics.Api.Analytics;

/// <summary>
/// The target split for each leg, keyed by its tag. The sheet carries what was raced,
/// not what was aimed for, so these mirror <c>LEGS</c> in the app's <c>DemoData.kt</c>.
/// </summary>
public static class RaceCourse
{
    private static readonly Dictionary<string, int> Targets = new()
    {
        ["RUN 1"] = 275, ["STN 1"] = 270,
        ["RUN 2"] = 280, ["STN 2"] = 150,
        ["RUN 3"] = 285, ["STN 3"] = 210,
        ["RUN 4"] = 285, ["STN 4"] = 270,
        ["RUN 5"] = 290, ["STN 5"] = 270,
        ["RUN 6"] = 290, ["STN 6"] = 165,
        ["RUN 7"] = 295, ["STN 7"] = 240,
        ["RUN 8"] = 300, ["STN 8"] = 330,
    };

    public static int? TargetSeconds(string tag) => Targets.TryGetValue(tag, out var seconds) ? seconds : null;
}

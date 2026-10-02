namespace RepsRox.Analytics.Api.Import;

/// <summary>A file that is not one of the app's export sheets, or one that does not read cleanly.</summary>
public class SheetFormatException(string message) : Exception(message);

using Microsoft.EntityFrameworkCore;
using RepsRox.Analytics.Api.Data;

namespace RepsRox.Analytics.Api.Import;

public record ImportResult(
    string FileName,
    bool Imported,
    SheetKind? Kind,
    DateOnly? Month,
    int Rows,
    bool Replaced,
    string? Error);

/// <summary>
/// Stores export sheets. A sheet replaces whatever was imported before for its kind
/// and month, so sending a month in twice — or a corrected copy — never doubles it.
/// </summary>
public class ImportService(AnalyticsDbContext db, TimeProvider clock)
{
    public async Task<ImportResult> ImportAsync(string fileName, string text, CancellationToken ct = default)
    {
        ParsedSheet sheet;
        try
        {
            sheet = SheetParser.Parse(fileName, text);
        }
        catch (SheetFormatException e)
        {
            return new ImportResult(fileName, false, null, null, 0, false, e.Message);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // The rows hang off the import and go with it, by the database's cascade.
        var replaced = await db.Imports
            .Where(i => i.Kind == sheet.Kind && i.Month == sheet.Month)
            .ExecuteDeleteAsync(ct) > 0;

        var import = new SheetImport
        {
            Kind = sheet.Kind,
            Month = sheet.Month,
            FileName = fileName,
            RowCount = sheet.RowCount,
            ImportedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Imports.Add(import);
        foreach (var s in sheet.Sessions) s.Import = import;
        foreach (var m in sheet.Meals) m.Import = import;
        foreach (var r in sheet.Races) r.Import = import;
        foreach (var w in sheet.WeighIns) w.Import = import;
        db.TrainingSessions.AddRange(sheet.Sessions);
        db.Meals.AddRange(sheet.Meals);
        db.Races.AddRange(sheet.Races);
        db.WeighIns.AddRange(sheet.WeighIns);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();

        return new ImportResult(fileName, true, sheet.Kind, sheet.Month, sheet.RowCount, replaced, null);
    }
}

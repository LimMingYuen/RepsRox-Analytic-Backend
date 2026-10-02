using System.Text;
using Microsoft.AspNetCore.Mvc;
using RepsRox.Analytics.Api.Analytics;
using RepsRox.Analytics.Api.Import;

namespace RepsRox.Analytics.Api.Controllers;

[ApiController]
[Route("api/imports")]
public class ImportsController(ImportService imports, AnalyticsService analytics) : ControllerBase
{
    /// <summary>A month's sheet runs to kilobytes; anything near this is not one.</summary>
    private const long MaxFileBytes = 5 * 1024 * 1024;

    [HttpGet]
    public Task<List<ImportDto>> List(CancellationToken ct) => analytics.ImportsAsync(ct);

    /// <summary>
    /// Takes the CSV files the app's Export month shares — any number of them, in any
    /// order. Each is read on its own, so one bad file does not hold back the rest.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(4 * MaxFileBytes)]
    public async Task<ActionResult<List<ImportResult>>> Upload([FromForm] List<IFormFile> files, CancellationToken ct)
    {
        if (files.Count == 0) return BadRequest("Attach one or more export CSV files as \"files\".");

        var results = new List<ImportResult>();
        foreach (var file in files)
        {
            if (file.Length > MaxFileBytes)
            {
                results.Add(new ImportResult(file.FileName, false, null, null, 0, false, "The file is too large to be an export sheet."));
                continue;
            }
            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            var text = await reader.ReadToEndAsync(ct);
            results.Add(await imports.ImportAsync(Path.GetFileName(file.FileName), text, ct));
        }
        return results;
    }

    /// <summary>Drops a sheet and every row it brought in.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await analytics.DeleteImportAsync(id, ct) ? NoContent() : NotFound();
}

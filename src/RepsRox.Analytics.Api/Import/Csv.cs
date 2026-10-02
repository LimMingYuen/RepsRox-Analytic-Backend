using System.Text;

namespace RepsRox.Analytics.Api.Import;

/// <summary>
/// Reads the CSV the app writes: RFC 4180 quoting, CRLF line ends (LF is taken too,
/// in case a file went through an editor on the way).
/// </summary>
public static class Csv
{
    /// <summary>Lead characters the app holds off with an apostrophe so a spreadsheet opens them as text.</summary>
    private const string FormulaLeads = "=+-@";

    public static List<string[]> Parse(string text)
    {
        var records = new List<string[]>();
        var record = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var i = 0;

        // A BOM is what a spreadsheet adds when it saves a file back out.
        if (text.Length > 0 && text[0] == '﻿') i = 1;

        void EndCell()
        {
            record.Add(cell.ToString());
            cell.Clear();
        }

        void EndRecord()
        {
            EndCell();
            // A blank line is nothing, not a record holding one empty cell.
            if (record.Count > 1 || record[0].Length > 0) records.Add([.. record]);
            record.Clear();
        }

        for (; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c != '"') cell.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else quoted = false;
                continue;
            }

            switch (c)
            {
                case '"': quoted = true; break;
                case ',': EndCell(); break;
                case '\r': break;
                case '\n': EndRecord(); break;
                default: cell.Append(c); break;
            }
        }

        if (quoted) throw new SheetFormatException("The file ends inside a quoted cell.");
        if (cell.Length > 0 || record.Count > 0) EndRecord();
        return records;
    }

    /// <summary>Undoes the apostrophe the app puts in front of a cell that opens like a formula.</summary>
    public static string Unprotect(string value) =>
        value.Length > 1 && value[0] == '\'' && FormulaLeads.Contains(value[1]) ? value[1..] : value;
}

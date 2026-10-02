using RepsRox.Analytics.Api.Data;
using RepsRox.Analytics.Api.Import;

namespace RepsRox.Analytics.Api.Tests;

public class SheetParserTests
{
    [Fact]
    public void Training_groups_consecutive_rows_into_sessions()
    {
        var sheet = SheetParser.Parse("repsrox-training-2026-08.csv", Samples.Training);

        Assert.Equal(SheetKind.Training, sheet.Kind);
        Assert.Equal(new DateOnly(2026, 8, 1), sheet.Month);
        Assert.Equal(5, sheet.RowCount);
        Assert.Equal(2, sheet.Sessions.Count);

        var lower = sheet.Sessions[0];
        Assert.Equal("Lower push", lower.Name);
        Assert.Equal(58 * 60 + 12, lower.DurationSeconds);
        Assert.Equal(3, lower.Sets.Count);
        Assert.Equal(122.5m, lower.Sets[1].WeightKg);
        Assert.Equal(SetUnit.Metres, lower.Sets[2].Unit);

        var row = sheet.Sessions[1].Sets[1];
        Assert.Equal("Row, bent", row.Exercise);
        Assert.Null(row.WeightKg);
    }

    [Fact]
    public void Meals_undo_the_formula_guard_and_keep_quoted_commas()
    {
        var sheet = SheetParser.Parse("repsrox-meals-2026-08.csv", Samples.Meals);

        Assert.Equal(SheetKind.Meals, sheet.Kind);
        Assert.Equal("Oats, whey, banana", sheet.Meals[0].Detail);
        Assert.Equal("=SUM(A1)", sheet.Meals[1].Name);
        Assert.False(sheet.Meals[2].Logged);
    }

    [Fact]
    public void A_race_ended_early_keeps_only_the_legs_it_ran()
    {
        var sheet = SheetParser.Parse("repsrox-races-2026-08.csv", Samples.Races());

        Assert.Equal(2, sheet.Races.Count);
        var early = sheet.Races[0];
        Assert.False(early.Complete);
        Assert.Equal(600, early.TotalSeconds);
        Assert.Equal([270, 265], early.Legs.Select(l => l.Seconds));
        Assert.Equal("STN 1", early.Legs[1].Tag);
        Assert.Equal("SkiErg", early.Legs[1].Name);
        Assert.Equal(16, sheet.Races[1].Legs.Count);
        Assert.Equal(3600 + 12 * 60, sheet.Races[1].TotalSeconds);
    }

    [Fact]
    public void The_header_decides_the_kind_whatever_the_file_is_called()
    {
        var sheet = SheetParser.Parse("download (3).csv", Samples.Weight);

        Assert.Equal(SheetKind.Weight, sheet.Kind);
        Assert.Equal(new DateOnly(2026, 8, 1), sheet.Month);
        Assert.Equal(81.5m, sheet.WeighIns[1].WeightKg);
    }

    [Fact]
    public void Line_feeds_and_a_byte_order_mark_are_taken()
    {
        var text = "﻿" + Samples.Weight.Replace("\r\n", "\n");
        Assert.Equal(2, SheetParser.Parse("w.csv", text).RowCount);
    }

    [Fact]
    public void An_empty_sheet_takes_its_month_from_the_name()
    {
        var sheet = SheetParser.Parse("repsrox-weight-2026-07.csv", "Date,Weight kg\r\n");
        Assert.Equal(new DateOnly(2026, 7, 1), sheet.Month);
        Assert.Equal(0, sheet.RowCount);
    }

    [Theory]
    [InlineData("w.csv", "Date,Weight kg\r\n2026-08-31,80\r\n2026-09-01,80\r\n", "span 2 months")]
    [InlineData("repsrox-weight-2026-07.csv", "Date,Weight kg\r\n2026-08-31,80\r\n", "named for 2026-07")]
    [InlineData("w.csv", "Date,Weight kg\r\n", "no month")]
    [InlineData("w.csv", "Date,Weight kg\r\n31/08/2026,80\r\n", "Line 2: date")]
    [InlineData("w.csv", "Name,Age\r\nA,1\r\n", "does not match")]
    [InlineData("t.csv", "Date,Session,Duration,Exercise,Set,Amount,Unit,Weight kg\r\n2026-08-15,A,1:00,B,1,5,lbs,1\r\n", "unit \"lbs\"")]
    public void A_sheet_that_does_not_read_cleanly_says_why(string name, string text, string expected)
    {
        var error = Assert.Throws<SheetFormatException>(() => SheetParser.Parse(name, text));
        Assert.Contains(expected, error.Message);
    }
}

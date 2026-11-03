using System.Collections.Immutable;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StudyReportEvaluator.App.Tests.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Workbooks.Writing;

// FR-113 AC-075 / AC-076（結合）: `採点結果` は Quantification_Results を参照する数式で、空欄は空欄、0は0。
public sealed class ScoreSummarySheetWriterTests
{
    [Fact]
    public void FR113_AC075_summary_cells_are_formulas_referencing_results_cells_not_literals()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        (string summaryName, string resultsName) = WriteAll(input.Path, existingSheetName: null);

        using SpreadsheetDocument doc = SpreadsheetDocument.Open(input.Path, false);
        Worksheet summary = Get(doc, summaryName);
        Worksheet results = Get(doc, resultsName);
        string[] headers = summary.Descendants<Row>().Single(r => r.RowIndex!.Value == 1)
            .Elements<Cell>().Select(c => c.InnerText).ToArray();
        Assert.Equal(["ID", "質問1の評価", "質問1のPromptの評価", "合計"], headers);

        string quoted = "'" + resultsName + "'!";
        foreach (int row in new[] { 2, 3, 4, 5 })
        {
            Assert.Equal(
                quoted + ColumnOf(results, "SourceRow") + row,
                Formula(summary, "ID", row));
            Assert.Contains(quoted + ColumnOf(results, "Q1.Question_Normalized") + row, Formula(summary, "質問1の評価", row), StringComparison.Ordinal);
            Assert.Contains(quoted + ColumnOf(results, "Q1.E1.Evaluator_Score") + row, Formula(summary, "質問1のPromptの評価", row), StringComparison.Ordinal);
            Assert.Contains(quoted + ColumnOf(results, "Final_Score") + row, Formula(summary, "合計", row), StringComparison.Ordinal);
        }

        // 固定値ではない: 4列すべて数式セルである。
        Assert.Equal(4 * 4, summary.Descendants<CellFormula>().Count());
    }

    [Fact]
    public void FR113_AC076_blank_stays_blank_zero_stays_zero_and_renamed_results_sheet_is_referenced()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        (string summaryName, string resultsName) = WriteAll(input.Path, existingSheetName: "Quantification_Results");
        Assert.Equal("Quantification_Results (2)", resultsName);

        using SpreadsheetDocument doc = SpreadsheetDocument.Open(input.Path, false);
        Worksheet summary = Get(doc, summaryName);
        Assert.Contains("'Quantification_Results (2)'!", Formula(summary, "合計", 2), StringComparison.Ordinal);

        // 行2: 採点不可 → 質問の評価は空欄（0にしない）。Final_Score は評価対象外でも基礎点が出る。
        Assert.Equal(string.Empty, CachedText(summary, "質問1の評価", 2));
        Assert.Equal(string.Empty, CachedText(summary, "質問1のPromptの評価", 2));
        // 行3: 取消 → 合計も空欄。
        Assert.Equal(string.Empty, CachedText(summary, "質問1の評価", 3));
        Assert.Equal(string.Empty, CachedText(summary, "合計", 3));
        // 行4: 上書き5 → 50 と 99.5。
        Assert.Equal("50", CachedText(summary, "質問1の評価", 4));
        Assert.Equal("99.5", CachedText(summary, "合計", 4));
        // 行5: 上書き0 → 0 として表示される（空欄にしない）。
        Assert.Equal("0", CachedText(summary, "質問1の評価", 5));
        Assert.Equal("0", CachedText(summary, "質問1のPromptの評価", 5));
        // 数値でない参照元は "" にする式であること（0にしない）。
        Assert.Contains("ISNUMBER", Formula(summary, "合計", 3), StringComparison.Ordinal);
    }

    private static (string Summary, string Results) WriteAll(string path, string? existingSheetName)
    {
        if (existingSheetName is not null)
        {
            using SpreadsheetDocument pre = SpreadsheetDocument.Open(path, true);
            WorkbookPart wp = pre.WorkbookPart!;
            WorksheetPart part = wp.AddNewPart<WorksheetPart>();
            part.Worksheet = new Worksheet(new SheetData());
            part.Worksheet.Save();
            Sheets sheets = wp.Workbook!.GetFirstChild<Sheets>()!;
            sheets.Append(new Sheet
            {
                Id = wp.GetIdOfPart(part),
                SheetId = sheets.Elements<Sheet>().Max(s => s.SheetId!.Value) + 1,
                Name = existingSheetName,
            });
            wp.Workbook.Save();
        }

        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(Definition());
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, true);
        AppOwnedSheetNameResolver resolver = new();
        AppOwnedSheetNames names = resolver.Resolve(document);
        string[] originals = document.WorkbookPart!.Workbook!.Descendants<Sheet>().Select(s => s.Name!.Value!).ToArray();
        names = names.WithScoreSummarySheetName(ScoreSummarySheetWriter.ResolveName(resolver, originals, names));
        ConfigCellAddressMap config = new ConfigSheetWriter().Write(document, snapshot, names);
        ResultsSheetWriteResult results = new ResultsSheetWriter().Write(
            document,
            snapshot,
            names,
            config,
            [Row(2, scorable: false, ResultsStatusCodes.Empty), Row(3, true, ResultsStatusCodes.Cancelled), Row(4, true, ResultsStatusCodes.Success, "5"), Row(5, true, ResultsStatusCodes.Success, "0")]);
        new ScoreSummarySheetWriter().Write(document, names, results);
        return (names.ScoreSummarySheetName!, names.ResultsSheetName);
    }

    private static QuantificationDefinition Definition() =>
        new()
        {
            Id = "DEF-FR113",
            Name = "FR-113 definition",
            Revision = "1",
            SourceSheet = "Original",
            HeaderRow = 1,
            FirstDataRow = 2,
            LastDataRow = 5,
            BasePoints = 99m,
            RoundingDigits = 1,
            Questions =
            [
                new QuestionDefinition
                {
                    Id = "Q1",
                    DisplayName = "Question",
                    QuestionText = "Synthetic question",
                    PrimarySourceColumn = "G",
                    Points = 1m,
                    Evaluators =
                    [
                        new EvaluatorDefinition
                        {
                            Id = "E1",
                            DisplayName = "Evaluator",
                            Type = EvaluatorType.PromptAnalysis,
                            Weight = 1m,
                            Range = new ScoreRange(0m, 10m),
                            BuiltInTemplateVersion = StudyReportEvaluator.Core.Prompting.BuiltInPromptTemplates.PromptAnalysisTemplateVersion,
                            Criteria =
                            [
                                new CriterionDefinition { Id = "C1", DisplayName = "Criterion", Description = "Synthetic criterion", Weight = 1m },
                            ],
                        },
                    ],
                },
            ],
        };

    private static ResultsSheetRowInput Row(int sourceRow, bool scorable, string status, string? overrideValue = null) =>
        new()
        {
            SourceRowNumber = sourceRow,
            Questions =
            [
                new QuestionResultInput
                {
                    QuestionId = "Q1",
                    Scorable = scorable,
                    Evaluators =
                    [
                        new EvaluatorResultInput
                        {
                            EvaluatorId = "E1",
                            Status = status,
                            Overrides = overrideValue is null
                                ? []
                                : [new CriterionOverrideInput { CriterionId = "C1", Value = overrideValue }],
                        },
                    ],
                    Similarity = new SimilarityResultInput
                    {
                        AiRaw = 0m,
                        Reason = string.Empty,
                        Status = scorable ? ResultsStatusCodes.Success : ResultsStatusCodes.Empty,
                    },
                },
            ],
        };

    private static Worksheet Get(SpreadsheetDocument doc, string name)
    {
        Sheet sheet = doc.WorkbookPart!.Workbook!.Descendants<Sheet>().Single(s => s.Name!.Value == name);
        return ((WorksheetPart)doc.WorkbookPart.GetPartById(sheet.Id!.Value!)).Worksheet!;
    }

    private static string ColumnOf(Worksheet sheet, string header)
    {
        Cell cell = sheet.Descendants<Row>().Single(r => r.RowIndex!.Value == 1)
            .Elements<Cell>().Single(c => c.InnerText == header);
        return new string(cell.CellReference!.Value!.TakeWhile(char.IsAsciiLetter).ToArray());
    }

    private static Cell CellAt(Worksheet sheet, string header, int row) =>
        sheet.Descendants<Cell>().Single(c => c.CellReference!.Value == ColumnOf(sheet, header) + row);

    private static string Formula(Worksheet sheet, string header, int row) =>
        CellAt(sheet, header, row).CellFormula!.Text;

    private static string CachedText(Worksheet sheet, string header, int row) =>
        CellAt(sheet, header, row).CellValue?.Text ?? string.Empty;
}

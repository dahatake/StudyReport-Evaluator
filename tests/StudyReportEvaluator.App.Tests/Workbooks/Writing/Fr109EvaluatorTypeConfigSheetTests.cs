using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StudyReportEvaluator.App.Tests.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Serialization;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Workbooks.Writing;

// FR-109 / AC-064: 設定シートのEvaluatorType列は3値を書き、同じ種類へ往復できる。既存のCUSTOM_PROMPTはCustomのまま読める。
public sealed class Fr109EvaluatorTypeConfigSheetTests
{
    private static QuantificationDefinition CreateDefinition()
    {
        static EvaluatorDefinition Evaluator(string id, EvaluatorType type, string? version, string? custom) => new()
        {
            Id = id,
            DisplayName = "Synthetic " + id,
            Type = type,
            Weight = 1m,
            Range = new ScoreRange(0m, 10m),
            BuiltInTemplateVersion = version,
            CustomPromptTemplate = custom,
            Enabled = true,
            Criteria =
            [
                new CriterionDefinition
                {
                    Id = "C-" + id,
                    DisplayName = "Criterion " + id,
                    Description = "Synthetic criterion description",
                    Weight = 1m,
                    Enabled = true,
                },
            ],
        };

        return new QuantificationDefinition
        {
            Id = "DEF-FR109",
            Name = "Synthetic FR-109 definition",
            Revision = "1",
            SourceSheet = "Original",
            HeaderRow = 1,
            FirstDataRow = 2,
            LastDataRow = 3,
            BasePoints = 70m,
            SpecialPoints = 0m,
            SimilarityPenaltyWeight = 0.1m,
            RoundingDigits = 2,
            Questions =
            [
                new QuestionDefinition
                {
                    Id = "Q1",
                    DisplayName = "Synthetic question",
                    QuestionText = "Synthetic question text",
                    PrimarySourceColumn = "G",
                    SupportingSourceColumns = [],
                    Points = 30m,
                    Enabled = true,
                    Evaluators =
                    [
                        Evaluator("E1", EvaluatorType.KnowledgeCoverage, "knowledge-v1", null),
                        Evaluator("E2", EvaluatorType.PromptAnalysis, "prompt-analysis-v1", null),
                        Evaluator("E3", EvaluatorType.CustomPrompt, null, "Evaluate {回答} by {評価項目}"),
                    ],
                },
            ],
        };
    }

    // FR-109 / AC-064
    [Fact]
    public void FR109_AC064_Config_sheet_writes_three_evaluator_types_and_round_trips_them()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(CreateDefinition());
        string targetDirectory = Path.Combine(input.Directory, "target");
        Directory.CreateDirectory(targetDirectory);
        using WorkingPackage package = WorkingPackage.Create(input.Path, Path.Combine(targetDirectory, "result.xlsx"));
        string configName;
        using (SpreadsheetDocument document = package.OpenForEditing())
        {
            AppOwnedSheetNames names = new AppOwnedSheetNameResolver().Resolve(document);
            configName = names.ConfigSheetName;
            _ = new ConfigSheetWriter().Write(document, snapshot, names);
        }

        using SpreadsheetDocument reopened = SpreadsheetDocument.Open(package.TemporaryPath, false);
        WorkbookPart workbookPart = reopened.WorkbookPart!;
        Sheet sheet = workbookPart.Workbook!.Descendants<Sheet>().Single(s => s.Name?.Value == configName);
        Worksheet worksheet = ((WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!)).Worksheet!;
        Row[] rows = worksheet.Descendants<Row>().ToArray();

        // 設定シートのEvaluatorType列(J)に書かれる値
        Dictionary<string, string> written = rows
            .Where(row => Text(row, "A") == "EVALUATOR")
            .ToDictionary(row => Text(row, "C"), row => Text(row, "J"), StringComparer.Ordinal);
        Assert.Equal("KNOWLEDGE_COVERAGE", written["E1"]);
        Assert.Equal("PROMPT_ANALYSIS", written["E2"]);
        Assert.Equal("CUSTOM_PROMPT", written["E3"]);

        // 設定シートに保存された定義(CANONICAL_JSON)を本番の ConfigSheetReader(FR-110)で読み、同じ種類に往復する
        CanonicalDefinitionReadResult readBack = new ConfigSheetReader().Read(workbookPart, configName);
        Assert.True(readBack.IsSuccess);
        Dictionary<string, EvaluatorType> types = readBack.Definition!.Questions[0].Evaluators
            .ToDictionary(e => e.Id, e => e.Type, StringComparer.Ordinal);
        Assert.Equal(EvaluatorType.KnowledgeCoverage, types["E1"]);
        Assert.Equal(EvaluatorType.PromptAnalysis, types["E2"]);
        Assert.Equal(EvaluatorType.CustomPrompt, types["E3"]);
    }
    private static string Text(Row row, string column)
    {
        Cell? cell = row.Elements<Cell>().SingleOrDefault(c =>
            string.Equals(new string((c.CellReference?.Value ?? "").TakeWhile(char.IsLetter).ToArray()), column, StringComparison.Ordinal));
        return cell?.InnerText ?? string.Empty;
    }
}

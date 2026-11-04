using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StudyReportEvaluator.App.Tests.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Serialization;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Workbooks.Reading;

// FR-110 / AC-065, AC-066, AC-067: 設定シートからの採点定義の読み戻し（ConfigSheetReader）。
public sealed class Fr110ConfigSheetReaderTests
{
    private static EvaluatorDefinition Evaluator(string id, EvaluatorType type, string? version, string? custom) => new()
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
                Description = "Synthetic criterion",
                Weight = 1m,
                Enabled = true,
            },
        ],
    };

    private static QuantificationDefinition Definition(params EvaluatorDefinition[] evaluators) => new()
    {
        Id = "DEF-FR110",
        Name = "Synthetic FR-110 definition",
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
                Evaluators = [.. evaluators],
            },
        ],
    };

    private static readonly EvaluatorDefinition[] AllThree =
    [
        Evaluator("E1", EvaluatorType.KnowledgeCoverage, "knowledge-v1", null),
        Evaluator("E2", EvaluatorType.PromptAnalysis, "prompt-analysis-v1", null),
        Evaluator("E3", EvaluatorType.CustomPrompt, null, "Evaluate {回答} by {評価項目}"),
    ];

    // 設定シートを書いてから、mutate で壊し、ConfigSheetReader で読む。
    private static CanonicalDefinitionReadResult WriteMutateRead(
        QuantificationSnapshot snapshot,
        Action<SheetData>? mutate = null)
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        string targetDirectory = Path.Combine(input.Directory, "target");
        Directory.CreateDirectory(targetDirectory);
        using WorkingPackage package = WorkingPackage.Create(input.Path, Path.Combine(targetDirectory, "result.xlsx"));
        using SpreadsheetDocument document = package.OpenForEditing();
        AppOwnedSheetNames names = new AppOwnedSheetNameResolver().Resolve(document);
        _ = new ConfigSheetWriter().Write(document, snapshot, names);
        if (mutate is not null)
        {
            WorkbookPart workbookPart = document.WorkbookPart!;
            Sheet sheet = workbookPart.Workbook!.Descendants<Sheet>().Single(s => s.Name?.Value == names.ConfigSheetName);
            mutate(((WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!)).Worksheet!.GetFirstChild<SheetData>()!);
        }

        return new ConfigSheetReader().Read(document, names);
    }

    private static string Text(Row row, string column) => Cell(row, column)?.InnerText ?? string.Empty;

    private static Cell? Cell(Row row, string column) => row.Elements<Cell>().SingleOrDefault(c =>
        string.Equals(new string((c.CellReference?.Value ?? "").TakeWhile(char.IsLetter).ToArray()), column, StringComparison.Ordinal));

    private static IEnumerable<Row> RowsOf(SheetData data, string recordType) =>
        data.Elements<Row>().Where(r => Text(r, "A") == recordType).ToArray();

    private static void SetText(Row row, string column, string text)
    {
        Cell cell = Cell(row, column)!;
        cell.RemoveAllChildren();
        cell.DataType = CellValues.InlineString;
        cell.Append(new InlineString(new Text(text)));
    }

    // FR-110 / AC-065
    [Fact]
    public void FR110_AC065_Round_trips_definition_and_three_evaluator_types()
    {
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(Definition(AllThree));

        CanonicalDefinitionReadResult result = WriteMutateRead(snapshot);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Equal(snapshot.CanonicalJson, new CanonicalDefinitionSerializer().Serialize(result.Definition!));
        EvaluatorDefinition[] evaluators = [.. result.Definition!.Questions[0].Evaluators];
        Assert.Equal(
            [EvaluatorType.KnowledgeCoverage, EvaluatorType.PromptAnalysis, EvaluatorType.CustomPrompt],
            evaluators.Select(e => e.Type));
    }

    // FR-110 / AC-065: 既存の CUSTOM_PROMPT だけの設定シートは Custom として読める
    [Fact]
    public void FR110_AC065_Custom_only_sheet_reads_as_custom()
    {
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(
            Definition(Evaluator("E3", EvaluatorType.CustomPrompt, null, "Evaluate {回答} by {評価項目}")));

        CanonicalDefinitionReadResult result = WriteMutateRead(snapshot);

        Assert.True(result.IsSuccess);
        Assert.Equal(EvaluatorType.CustomPrompt, result.Definition!.Questions[0].Evaluators.Single().Type);
        Assert.Equal("Evaluate {回答} by {評価項目}", result.Definition.Questions[0].Evaluators.Single().CustomPromptTemplate);
    }

    // FR-110 / AC-066
    [Theory]
    [InlineData("missing", DefinitionReadErrorKind.MissingCanonicalJson)]
    [InlineData("empty", DefinitionReadErrorKind.EmptyCanonicalJson)]
    [InlineData("broken", DefinitionReadErrorKind.MalformedJson)]
    [InlineData("noField", DefinitionReadErrorKind.MissingRequiredField)]
    public void FR110_AC066_Broken_or_missing_canonical_json_returns_validation_error(string scenario, DefinitionReadErrorKind expected)
    {
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(Definition(AllThree));

        CanonicalDefinitionReadResult result = WriteMutateRead(snapshot, data =>
        {
            Row[] jsonRows = [.. RowsOf(data, "CANONICAL_JSON")];
            switch (scenario)
            {
                case "missing":
                    foreach (Row row in jsonRows)
                    {
                        row.Remove();
                    }

                    break;
                case "empty":
                    SetText(jsonRows[0], "Y", string.Empty);
                    foreach (Row row in jsonRows.Skip(1))
                    {
                        row.Remove();
                    }

                    break;
                case "broken":
                    SetText(jsonRows[0], "Y", "{\"id\": ");
                    foreach (Row row in jsonRows.Skip(1))
                    {
                        row.Remove();
                    }

                    break;
                default:
                    string all = string.Concat(jsonRows.Select(r => Text(r, "Y")));
                    SetText(jsonRows[0], "Y", all.Replace("\"questions\"", "\"other\"", StringComparison.Ordinal));
                    foreach (Row row in jsonRows.Skip(1))
                    {
                        row.Remove();
                    }

                    break;
            }
        });

        Assert.False(result.IsSuccess);
        Assert.Null(result.Definition);
        Assert.Contains(result.Errors, e => e.Kind == expected);
    }

    // FR-110 / AC-067: 列の未知の種類名
    [Fact]
    public void FR110_AC067_Unknown_type_name_in_column_is_rejected_not_replaced()
    {
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(Definition(AllThree));

        CanonicalDefinitionReadResult result = WriteMutateRead(snapshot, data =>
            SetText(RowsOf(data, "EVALUATOR").First(), "J", "MYSTERY_TYPE"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Definition);
        DefinitionReadError error = Assert.Single(result.Errors);
        Assert.Equal(DefinitionReadErrorKind.UnknownEvaluatorType, error.Kind);
        Assert.Contains("MYSTERY_TYPE", error.Message, StringComparison.Ordinal);
    }

    // FR-110 / AC-067: JSON 内の未知の種類名
    [Fact]
    public void FR110_AC067_Unknown_type_name_in_json_is_rejected()
    {
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(Definition(AllThree));

        CanonicalDefinitionReadResult result = WriteMutateRead(snapshot, data =>
        {
            Row[] jsonRows = [.. RowsOf(data, "CANONICAL_JSON")];
            string all = string.Concat(jsonRows.Select(r => Text(r, "Y")));
            SetText(jsonRows[0], "Y", all.Replace("\"KNOWLEDGE_COVERAGE\"", "\"NOPE_TYPE\"", StringComparison.Ordinal));
            foreach (Row row in jsonRows.Skip(1))
            {
                row.Remove();
            }
        });

        Assert.False(result.IsSuccess);
        Assert.Null(result.Definition);
        Assert.Contains(result.Errors, e => e.Kind == DefinitionReadErrorKind.UnknownEvaluatorType
            && e.Message.Contains("NOPE_TYPE", StringComparison.Ordinal));
    }

    // FR-110 / AC-067: JSON と列の食い違い
    [Fact]
    public void FR110_AC067_Mismatch_between_json_and_column_is_rejected()
    {
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(Definition(AllThree));

        CanonicalDefinitionReadResult result = WriteMutateRead(snapshot, data =>
            SetText(RowsOf(data, "EVALUATOR").First(), "J", "PROMPT_ANALYSIS"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Definition);
        Assert.Contains(result.Errors, e => e.Kind == DefinitionReadErrorKind.EvaluatorTypeMismatch);
    }

    // FR-114 / AC-004: 明示した係数 0 は設定シートから 0 のまま読み戻り、補完の通知は出さない。
    [Theory]
    [InlineData("0")]
    [InlineData("0.1")]
    [InlineData("0.25")]
    public void FR114_AC004_Explicit_similarity_weight_round_trips_through_config_without_notice(string weight)
    {
        decimal expected = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(
            Definition(AllThree) with { BasePoints = 70m, SimilarityPenaltyWeight = expected });

        CanonicalDefinitionReadResult result = WriteMutateRead(snapshot);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Definition!.SimilarityPenaltyWeight);
        Assert.Empty(result.Notices);
        Assert.Equal(snapshot.CanonicalJson, new CanonicalDefinitionSerializer().Serialize(result.Definition));
    }

    // FR-114 / AC-004: 係数を持たない旧形式の設定シートは旧既定値 0.1 を補い、そのことを通知する。
    [Fact]
    public void FR114_AC004_Legacy_config_without_similarity_weight_supplies_0_1_with_notice()
    {
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(Definition(AllThree));

        CanonicalDefinitionReadResult result = WriteMutateRead(snapshot, data =>
        {
            Row[] chunks = RowsOf(data, "CANONICAL_JSON").OrderBy(r => int.Parse(Text(r, "X"), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            string json = string.Concat(chunks.Select(r => Text(r, "Y")));
            string legacy = System.Text.RegularExpressions.Regex.Replace(json, "\"similarityPenaltyWeight\":[^,]*,", string.Empty);
            Assert.NotEqual(json, legacy);
            SetText(chunks[0], "Y", legacy);
            foreach (Row other in chunks.Skip(1))
            {
                SetText(other, "Y", string.Empty);
            }
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(QuantificationDefinition.LegacyDefaultSimilarityPenaltyWeight, result.Definition!.SimilarityPenaltyWeight);
        Assert.Equal([CanonicalDefinitionDeserializer.LegacySimilarityPenaltyWeightNotice], result.Notices);
    }
}

using System.Collections.Immutable;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StudyReportEvaluator.App.Workbooks.Checkpoint;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.SystemTests.Integration;

/// <summary>
/// FR-113 AC-074: 期待値は受入基準だけから決める。実装は、合成 run（fake AI・合成 workbook）の起動と、
/// 完成 workbook を Open XML で開いて読むためにだけ使う。
/// </summary>
public sealed partial class ScoreSummarySheetTests
{
    private const string SheetName = "採点結果";
    private const string ResultsSheet = "Quantification_Results";
    private static readonly int[] StudentRows = [2, 3, 4];

    [GeneratedRegex(@"^\$?([A-Z]+)\$?(\d+)$")]
    private static partial Regex CellRef();

    // FR-113 AC-074: Knowledge・Prompt を持つ設問が複数ある run の完成 workbook を開くと、
    // `採点結果` の見出しが ID・設問順の質問Nの評価（Prompt が有効な設問は直後に質問NのPromptの評価）・合計。
    // 無効な設問・有効な PROMPT_ANALYSIS のない設問の Prompt 列はない。行数・ID は Quantification_Results と一致。
    // `.partial` には無く、元シートに同名があれば既存規則で名前が変わる（異常系）。
    [Fact]
    public async Task FR113_AC074_score_summary_sheet_has_headers_rows_and_ids_and_avoids_name_collision()
    {
        using TempDir dir = new();
        string input = Path.Combine(dir.Path, "input.xlsx");
        string output = Path.Combine(dir.Path, "out");
        Directory.CreateDirectory(output);
        CreateInput(input, "Original");

        string finalPath = await RunAsync(input, output);

        string[] expected =
        [
            "ID", "質問1の評価", "質問1のPromptの評価", "質問2の評価", "質問3の評価", "合計",
        ];

        using (SpreadsheetDocument doc = SpreadsheetDocument.Open(finalPath, false))
        {
            WorksheetPart summary = Sheet(doc, SheetName);
            Dictionary<string, string> headerCells = RowCells(doc, summary, 1);
            string[] headers = headerCells.OrderBy(p => ColumnIndex(p.Key)).Select(p => p.Value).ToArray();
            Assert.Equal(expected, headers);
            Assert.DoesNotContain(headers, h => h.StartsWith("質問2のPrompt", StringComparison.Ordinal));
            Assert.DoesNotContain(headers, h => h.StartsWith("質問3のPrompt", StringComparison.Ordinal));
            Assert.DoesNotContain(headers, h => h.StartsWith("質問4", StringComparison.Ordinal));

            int summaryDataRows = DataRowIndexes(summary).Count(r => r > 1);
            Assert.Equal(StudentRows.Length, summaryDataRows);

            // ID は Quantification_Results の SourceRow と一致（元シートの行番号）。
            WorksheetPart results = Sheet(doc, ResultsSheet);
            Dictionary<string, string> resultHeader = RowCells(doc, results, 1);
            string sourceRowColumn = resultHeader.Single(p => p.Value == "SourceRow").Key;
            List<string> resultIds = StudentRows
                .Select(r => RowCells(doc, results, r).GetValueOrDefault(sourceRowColumn) ?? string.Empty)
                .ToList();
            Assert.Equal(StudentRows.Select(r => r.ToString()).ToList(), resultIds);

            string idColumn = headerCells.Single(p => p.Value == "ID").Key;
            for (int i = 0; i < StudentRows.Length; i++)
            {
                Cell cell = FindCell(summary, idColumn + StudentRows[i]);
                string? cached = CellText(doc, cell);
                if (!string.IsNullOrEmpty(cached))
                {
                    Assert.Equal(StudentRows[i].ToString(), cached);
                }
                else
                {
                    // 値が未計算の場合は、同じ学生行の SourceRow セルを参照する数式であること。
                    string formula = cell.CellFormula?.Text ?? string.Empty;
                    Assert.Contains(ResultsSheet, formula, StringComparison.Ordinal);
                    Match m = Regex.Match(formula, @"\$?([A-Z]+)\$?(\d+)\s*$");
                    Assert.True(m.Success, formula);
                    Assert.Equal(sourceRowColumn, m.Groups[1].Value);
                    Assert.Equal(StudentRows[i].ToString(), RowCells(doc, results, int.Parse(m.Groups[2].Value)).GetValueOrDefault(sourceRowColumn));
                }
            }
        }

        // `.partial` には入れない。
        foreach (string partial in Directory.GetFiles(output, "*.partial*"))
        {
            using SpreadsheetDocument p = SpreadsheetDocument.Open(partial, false);
            Assert.DoesNotContain(SheetNames(p), n => n.StartsWith(SheetName, StringComparison.Ordinal));
        }

        // 異常系: 元シートに同名`採点結果`があれば、元シートは変わらず、本シートは別名になる。
        using TempDir dir2 = new();
        string input2 = Path.Combine(dir2.Path, "input.xlsx");
        string output2 = Path.Combine(dir2.Path, "out");
        Directory.CreateDirectory(output2);
        CreateInput(input2, SheetName);
        string final2 = await RunAsync(input2, output2, sourceSheet: SheetName);
        using SpreadsheetDocument doc2 = SpreadsheetDocument.Open(final2, false);
        string[] names = SheetNames(doc2).ToArray();
        Assert.Single(names, n => n == SheetName);
        string renamed = Assert.Single(names, n => n != SheetName && n.StartsWith(SheetName, StringComparison.Ordinal));
        Assert.Equal("ID", RowCells(doc2, Sheet(doc2, renamed), 1)["A"]);
        Assert.Equal("Primary A", RowCells(doc2, Sheet(doc2, SheetName), 1)["A"]);
    }

    private static async Task<string> RunAsync(string input, string output, string sourceSheet = "Original")
    {
        WorkbookMetadata metadata = new WorkbookMetadataReader().Read(input);
        DurableQuantificationOrchestrator orchestrator = new(
            new OpenXmlEvaluationRowSource(input),
            new Fakes.NormalRunner(),
            new Fakes.ReferenceRunner(),
            new Fakes.SpecialRunner());
        RunSummary summary = await orchestrator.RunAsync(
            new DurableQuantificationRunRequest
            {
                Run = new QuantificationRunRequest
                {
                    DraftDefinition = Definition(sourceSheet),
                    WorkbookMetadata = metadata,
                    InputPath = input,
                    ModelId = "model-test",
                    MaximumPromptTokens = 64_000,
                    MaximumContextWindowTokens = 128_000,
                    MaxConcurrency = 1,
                    UseDurableWorkflow = true,
                },
                Runtime = new CheckpointRuntimeIdentity
                {
                    ApplicationIdentity = "StudyReportEvaluator.App/4.0.0",
                    CliVersion = "1.0.79",
                    CliSha256 = new string('A', 64),
                    SdkInformationalVersion = "1.0.11",
                },
                OutputDirectory = output,
            },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(summary.IsDurable);
        string finalPath = Assert.IsType<string>(summary.FinalPath);
        Assert.True(File.Exists(finalPath));
        return finalPath;
    }

    // Q1: Knowledge + Prompt（有効）, Q2: Knowledge + Prompt（無効）, Q3: Knowledge のみ, Q4: 設問ごと無効。
    private static QuantificationDefinition Definition(string sourceSheet) =>
        new()
        {
            Id = "DEF-ST113",
            Name = "FR-113 system-test definition",
            Revision = "1",
            SourceSheet = sourceSheet,
            HeaderRow = 1,
            FirstDataRow = 2,
            LastDataRow = 4,
            BasePoints = 90m,
            RoundingDigits = 1,
            Questions =
            [
                Question("Q1", "A", 4m, true, Knowledge("K1", true), Prompt("P1", true)),
                Question("Q2", "B", 3m, true, Knowledge("K2", true), Prompt("P2", false)),
                Question("Q3", "C", 3m, true, Knowledge("K3", true)),
                Question("Q4", "D", 0m, false, Knowledge("K4", true), Prompt("P4", true)),
            ],
        };

    private static QuestionDefinition Question(string id, string column, decimal points, bool enabled, params EvaluatorDefinition[] evaluators) =>
        new()
        {
            Id = id,
            DisplayName = "Question " + id,
            QuestionText = "text " + id,
            PrimarySourceColumn = column,
            SupportingSourceColumns = [],
            Points = points,
            Enabled = enabled,
            Evaluators = [.. evaluators],
        };

    private static EvaluatorDefinition Knowledge(string id, bool enabled) =>
        Evaluator(id, EvaluatorType.KnowledgeCoverage, BuiltInPromptTemplates.KnowledgeTemplateVersion, enabled);

    private static EvaluatorDefinition Prompt(string id, bool enabled) =>
        Evaluator(id, EvaluatorType.PromptAnalysis, BuiltInPromptTemplates.PromptAnalysisTemplateVersion, enabled);

    private static EvaluatorDefinition Evaluator(string id, EvaluatorType type, string templateVersion, bool enabled) =>
        new()
        {
            Id = id,
            DisplayName = "Evaluator " + id,
            Type = type,
            Weight = 1m,
            Range = new ScoreRange(0m, 10m),
            BuiltInTemplateVersion = templateVersion,
            Enabled = enabled,
            Criteria =
            [
                new CriterionDefinition
                {
                    Id = id + "-C1",
                    DisplayName = "Criterion " + id,
                    Description = "Description " + id,
                    Weight = 1m,
                    Enabled = true,
                },
            ],
        };

    private static void CreateInput(string path, string sheetName)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
        WorksheetPart part = workbookPart.AddNewPart<WorksheetPart>();
        string[] heads = ["Primary A", "Primary B", "Primary C", "Primary D"];
        Row Make(int index, string[] values) =>
            new(values.Select((v, i) => Inline(((char)('A' + i)).ToString() + index, v))) { RowIndex = (uint)index };
        part.Worksheet = new Worksheet(
            new SheetData(
                Make(1, heads),
                Make(2, ["answer a1", "answer b1", "answer c1", "answer d1"]),
                Make(3, ["answer a2", "answer b2", "answer c2", "answer d2"]),
                Make(4, ["answer a3", "answer b3", "answer c3", "answer d3"])));
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(part), SheetId = 1, Name = sheetName });
        workbookPart.Workbook.Save();
    }

    private static Cell Inline(string reference, string value) =>
        new()
        {
            CellReference = reference,
            DataType = DocumentFormat.OpenXml.Spreadsheet.CellValues.InlineString,
            InlineString = new InlineString(new Text(value)),
        };

    private static IEnumerable<string> SheetNames(SpreadsheetDocument doc) =>
        doc.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Select(s => s.Name!.Value!);

    private static WorksheetPart Sheet(SpreadsheetDocument doc, string name)
    {
        Sheet sheet = doc.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Single(s => s.Name == name);
        return (WorksheetPart)doc.WorkbookPart.GetPartById(sheet.Id!.Value!);
    }

    private static IEnumerable<int> DataRowIndexes(WorksheetPart part) =>
        part.Worksheet!.Descendants<Row>()
            .Where(r => r.Elements<Cell>().Any(c => c.CellValue is not null || c.CellFormula is not null || c.InlineString is not null))
            .Select(r => (int)r.RowIndex!.Value);

    private static Cell FindCell(WorksheetPart part, string reference) =>
        part.Worksheet!.Descendants<Cell>().Single(c => c.CellReference == reference);

    private static Dictionary<string, string> RowCells(SpreadsheetDocument doc, WorksheetPart part, int row)
    {
        Dictionary<string, string> result = [];
        foreach (Cell cell in part.Worksheet!.Descendants<Cell>())
        {
            Match m = CellRef().Match(cell.CellReference!.Value!);
            if (m.Success && int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) == row)
            {
                string? text = CellText(doc, cell);
                if (!string.IsNullOrEmpty(text))
                {
                    result[m.Groups[1].Value] = text;
                }
            }
        }

        return result;
    }

    private static string? CellText(SpreadsheetDocument doc, Cell cell)
    {
        if (cell.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.InlineString)
        {
            return cell.InlineString?.Text?.Text;
        }

        string? value = cell.CellValue?.Text;
        if (value is not null && cell.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString)
        {
            SharedStringTablePart? table = doc.WorkbookPart!.SharedStringTablePart;
            return table?.SharedStringTable!.Elements<SharedStringItem>().ElementAt(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).InnerText;
        }

        return value;
    }

    private static int ColumnIndex(string column) =>
        column.Aggregate(0, (acc, ch) => (acc * 26) + (ch - 'A' + 1));

    private sealed class TempDir : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SRE-ST113-" + Guid.NewGuid().ToString("N"));

        internal TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static class Fakes
    {
        internal sealed class NormalRunner : IEvaluationRunner
        {
            public Task<EvaluationRunnerResult> EvaluateAsync(SafeEvaluationPayload payload, string modelId, CancellationToken cancellationToken) =>
                Task.FromResult(EvaluationRunnerResult.Succeeded(
                    new QuantificationResult
                    {
                        EvaluatorId = payload.EvaluatorId,
                        Criteria = payload.ExpectedCriteria
                            .Select(criterion => new CriterionQuantificationResult
                            {
                                CriterionId = criterion.CriterionId,
                                RawScore = 7m,
                                Reason = "reason",
                                Evidence = "answer",
                                EvidenceSource = EvidenceSourceKind.PrimaryAnswer,
                                EvidenceSourceColumnId = "A",
                            })
                            .ToImmutableArray(),
                    }));
        }

        internal sealed class ReferenceRunner : IReferenceAnswerOperationRunner
        {
            public Task<AuxiliaryOperationResult<ReferenceAnswerResult>> EvaluateAsync(SafeReferenceAnswerPayload payload, CancellationToken cancellationToken) =>
                Task.FromResult(AuxiliaryOperationResult<ReferenceAnswerResult>.Succeeded(
                    new ReferenceAnswerResult { QuestionId = payload.QuestionId, Answer = "reference answer" }));
        }

        internal sealed class SpecialRunner : ISpecialEvaluationOperationRunner
        {
            public Task<AuxiliaryOperationResult<SpecialQuantificationResult>> EvaluateAsync(SafeSpecialEvaluationPayload payload, string modelId, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("no special evaluation");
        }
    }
}

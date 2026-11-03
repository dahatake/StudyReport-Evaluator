using System.Collections.Immutable;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StudyReportEvaluator.App.Workbooks.Checkpoint;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.SystemTests.Integration;

/// <summary>
/// FR-112 AC-072 (system). 期待値は受入基準だけから決める:
/// 選んだ行だけを再実行すると、fake AI の呼出しは選んだ行の分だけ、選ばれなかった行の結果・採点値は不変、
/// 選んだ行の結果だけが新しい結果（成功または再度の技術エラー）に置き換わる。
/// </summary>
public sealed class SelectedRowRerunTests
{
    private const int FirstRow = 42;
    private const int LastRow = 51;
    private static readonly int[] FailingRows = [43, 47];

    // FR-112 AC-072: 行42〜51のうち43・47が技術エラー。43は再実行で成功、47は再度の技術エラー。
    [Fact]
    public async Task FR112_AC072_rerun_calls_ai_only_for_selected_rows_and_replaces_only_their_results()
    {
        using RerunWorkbook workbook = new();
        CountingRunner runner = new();
        runner.FailRows.UnionWith(FailingRows);

        // 初回 run: 全 10 行が AI を 1 回ずつ呼ぶ。
        RunSummary first = await workbook.RunAsync(runner, targetRows: null, TestContext.Current.CancellationToken);
        Assert.Equal(LastRow - FirstRow + 1, runner.TotalCalls);
        Dictionary<int, string> before = workbook.RowOutcomes(first);
        Assert.Equal(LastRow - FirstRow + 1, before.Count);
        foreach (int row in FailingRows)
        {
            Assert.Equal("technical-error", before[row]);
        }

        // 再実行: 43 は成功へ、47 は再度の技術エラー。選んだ行以外は呼ばれない。
        runner.ResetCalls();
        runner.FailRows.Remove(43);
        RunSummary second = await workbook.RerunAsync(runner, first, [43, 47], TestContext.Current.CancellationToken);

        Assert.Equal(2, runner.TotalCalls);
        Assert.Equal(new[] { 43, 47 }, runner.CalledRows.OrderBy(r => r).ToArray());

        Dictionary<int, string> after = workbook.RowOutcomes(second);
        Assert.Equal(before.Count, after.Count);
        foreach (int row in Enumerable.Range(FirstRow, LastRow - FirstRow + 1).Except([43, 47]))
        {
            Assert.Equal(before.GetValueOrDefault(row, "<none>"), after.GetValueOrDefault(row, "<none>")); // 選ばれなかった行の結果・採点値は不変
        }

        Assert.NotEqual(before.GetValueOrDefault(43, "<none>"), after.GetValueOrDefault(43, "<none>")); // 技術エラー → 成功
        Assert.Equal(before.GetValueOrDefault(47, "<none>"), after.GetValueOrDefault(47, "<none>")); // 再度の技術エラー（同じ失敗の結果）

        // 異常系: 選ばれなかった（成功）行を含めず、元の出力 workbook は上書きされない。
        Assert.True(File.Exists(first.FinalPath));
    }

    private sealed class CountingRunner : IEvaluationRunner
    {
        private readonly List<int> calledRows = [];
        private readonly object gate = new();

        internal HashSet<int> FailRows { get; } = [];

        internal int TotalCalls { get { lock (gate) { return calledRows.Count; } } }

        internal IReadOnlyList<int> CalledRows { get { lock (gate) { return calledRows.ToArray(); } } }

        internal void ResetCalls() { lock (gate) { calledRows.Clear(); } }

        public Task<EvaluationRunnerResult> EvaluateAsync(SafeEvaluationPayload payload, string modelId, CancellationToken cancellationToken)
        {
            int row = int.Parse(payload.PrimarySource.Value.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
            lock (gate) { calledRows.Add(row); }
            if (FailRows.Contains(row))
            {
                return Task.FromResult(EvaluationRunnerResult.Failed(ResultsStatusCodes.AiRuntimeFailed));
            }

            return Task.FromResult(EvaluationRunnerResult.Succeeded(new QuantificationResult
            {
                EvaluatorId = payload.EvaluatorId,
                Criteria = payload.ExpectedCriteria.Select(c => new CriterionQuantificationResult
                {
                    CriterionId = c.CriterionId,
                    RawScore = 5m + (row % 5),
                    Reason = "synthetic",
                    Evidence = "synthetic",
                    EvidenceSource = EvidenceSourceKind.PrimaryAnswer,
                    EvidenceSourceColumnId = "A",
                }).ToImmutableArray(),
            }));
        }
    }

    /// <summary>合成 workbook（行42〜51）と run/再実行の足場。再実行の入口は <see cref="RerunAsync"/> の 1 か所に集約する。</summary>
    private sealed class RerunWorkbook : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "SRE-ST-FR112-" + Guid.NewGuid().ToString("N"));

        internal RerunWorkbook()
        {
            Directory.CreateDirectory(directory);
            InputPath = Path.Combine(directory, "input.xlsx");
            OutputDirectory = Path.Combine(directory, "out");
            Directory.CreateDirectory(OutputDirectory);
            Create(InputPath);
        }

        internal string InputPath { get; }

        internal string OutputDirectory { get; }

        internal Task<RunSummary> RunAsync(IEvaluationRunner runner, IReadOnlyList<int>? targetRows, CancellationToken ct)
        {
            _ = targetRows;
            QuantificationDefinition definition = SyntheticRun.Definition() with { };
            return RunCoreAsync(runner, definition, ct);
        }

        /// <summary>
        /// 選んだ行（元の行番号）だけを、元の run と同じ入力・採点定義・model で再実行する（FR-112）。
        /// 再実行 API は実装後に確定するため、実装役は本メソッドの 1 か所だけを実 API に合わせる（期待値は変えない）。
        /// </summary>
        internal Task<RunSummary> RerunAsync(IEvaluationRunner runner, RunSummary original, IReadOnlyList<int> rows, CancellationToken ct) =>
            throw new NotImplementedException("FR-112: 選択行の再実行の入口が未実装（AC-072 の実行入口を実装後に接続する）");

        internal Dictionary<int, string> RowOutcomes(RunSummary summary) =>
            summary.CompletedRows.ToDictionary(
                r => r.SourceRowNumber,
                r => System.Text.Json.JsonSerializer.Serialize(r));

        public void Dispose()
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private async Task<RunSummary> RunCoreAsync(IEvaluationRunner runner, QuantificationDefinition definition, CancellationToken ct)
        {
            WorkbookMetadata metadata = new WorkbookMetadataReader().Read(InputPath);
            DurableQuantificationOrchestrator orchestrator = new(
                new OpenXmlEvaluationRowSource(InputPath), runner, new NoReference(), new NoSpecial());
            return await orchestrator.RunAsync(
                new DurableQuantificationRunRequest
                {
                    Run = new QuantificationRunRequest
                    {
                        DraftDefinition = definition with { FirstDataRow = FirstRow, LastDataRow = LastRow },
                        WorkbookMetadata = metadata,
                        InputPath = InputPath,
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
                    OutputDirectory = OutputDirectory,
                },
                cancellationToken: ct);
        }

        private static void Create(string path)
        {
            using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
            WorkbookPart workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
            WorksheetPart part = workbookPart.AddNewPart<WorksheetPart>();
            SheetData data = new(new Row(Cell("A1", "Primary A"), Cell("B1", "Support B")) { RowIndex = 1 });
            for (int row = FirstRow; row <= LastRow; row++)
            {
                data.Append(new Row(Cell($"A{row}", $"answer {row}"), Cell($"B{row}", $"support {row}")) { RowIndex = (uint)row });
            }

            part.Worksheet = new Worksheet(new SheetDimension { Reference = $"A1:B{LastRow}" }, data);
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(part), SheetId = 1, Name = "Original" });
            workbookPart.Workbook.Save();
        }

        private static Cell Cell(string reference, string value) =>
            new() { CellReference = reference, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(value)) };
    }

    private sealed class NoReference : IReferenceAnswerOperationRunner
    {
        public Task<AuxiliaryOperationResult<ReferenceAnswerResult>> EvaluateAsync(SafeReferenceAnswerPayload payload, CancellationToken cancellationToken) =>
            Task.FromResult(AuxiliaryOperationResult<ReferenceAnswerResult>.Succeeded(
                new ReferenceAnswerResult { QuestionId = payload.QuestionId, Answer = "reference" }));
    }

    private sealed class NoSpecial : ISpecialEvaluationOperationRunner
    {
        public Task<AuxiliaryOperationResult<SpecialQuantificationResult>> EvaluateAsync(SafeSpecialEvaluationPayload payload, string modelId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No special evaluation.");
    }
}


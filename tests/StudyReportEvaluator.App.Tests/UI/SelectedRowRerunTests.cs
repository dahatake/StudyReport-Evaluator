using System.Collections.Concurrent;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.Tests.Workflow;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.App.Workbooks.Checkpoint;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

/// <summary>
/// FR-112 結果画面で選んだ行だけの再実行。行42〜51の合成 run（43・47 が技術エラー）を使う。
/// </summary>
public sealed class SelectedRowRerunTests
{
    private static readonly int[] FailingRows = [43, 47];

    // FR-112 AC-070: 「技術エラーの行を選ぶ」で技術エラーの 2 行だけにチェックが付き、任意の行の付け外しと選択件数、0 件での無効化。
    [Fact]
    public async Task FR112_AC070_select_technical_error_rows_checks_only_those_rows_and_counts_the_selection()
    {
        RerunFixture fixture = new();
        fixture.Runner.FailRows.UnionWith(FailingRows);
        ExecutionRunContext context = await fixture.RunAsync();
        using ResultsOutputViewModel viewModel = new(new RecordingOutputBoundary(), context);

        Assert.Equal(0, viewModel.SelectedRerunCount);
        Assert.False(viewModel.CanRerunSelectedRows);
        Assert.False(viewModel.RerunSelectedRowsCommand.CanExecute(null));
        Assert.True(viewModel.SelectTechnicalErrorRowsCommand.CanExecute(null));

        viewModel.SelectTechnicalErrorRowsCommand.Execute(null);

        Assert.Equal(FailingRows, viewModel.RowScores.Where(row => row.IsSelectedForRerun).Select(row => row.SourceRowNumber).ToArray());
        Assert.All(viewModel.RowScores.Where(row => row.Status == ResultsRowStatus.Success), row => Assert.False(row.IsSelectedForRerun));
        Assert.Equal(2, viewModel.SelectedRerunCount);
        Assert.Contains("2", viewModel.SelectedRerunCountText);
        Assert.True(viewModel.RerunSelectedRowsCommand.CanExecute(null));

        // 任意の行のチェックの付け外し。
        ResultsRowScoreViewModel extra = viewModel.RowScores.Single(row => row.SourceRowNumber == 45);
        extra.IsSelectedForRerun = true;
        Assert.Equal(3, viewModel.SelectedRerunCount);
        extra.IsSelectedForRerun = false;
        Assert.Equal(2, viewModel.SelectedRerunCount);

        // 選択が 0 件のとき再実行操作は無効。
        foreach (ResultsRowScoreViewModel row in viewModel.RowScores.Where(row => row.IsSelectedForRerun).ToArray())
        {
            row.IsSelectedForRerun = false;
        }

        Assert.Equal(0, viewModel.SelectedRerunCount);
        Assert.False(viewModel.CanRerunSelectedRows);
        Assert.False(viewModel.RerunSelectedRowsCommand.CanExecute(null));
    }

    // 結果画面のエラー絞り込み: エラーの行だけを一覧に出し、詳細の要約を持ち、その行だけ選んで再実行できる。
    [Fact]
    public async Task ShowOnlyErrors_filters_the_list_to_technical_error_rows_with_a_summary()
    {
        RerunFixture fixture = new();
        fixture.Runner.FailRows.UnionWith(FailingRows);
        using ResultsOutputViewModel viewModel = new(new RecordingOutputBoundary(), await fixture.RunAsync());

        Assert.True(viewModel.HasErrorRows);
        Assert.Equal(2, viewModel.ErrorRowCount);
        Assert.True(viewModel.VisibleRowScores.Count > 2);

        viewModel.ShowOnlyErrors = true;

        Assert.Equal(FailingRows, viewModel.VisibleRowScores.Select(row => row.SourceRowNumber).ToArray());
        Assert.All(viewModel.VisibleRowScores, row => Assert.False(string.IsNullOrWhiteSpace(row.ErrorSummaryText)));
        Assert.Contains("エラーのみ", viewModel.PageSummary);

        viewModel.SelectTechnicalErrorRowsCommand.Execute(null);
        Assert.Equal(2, viewModel.SelectedRerunCount);

        viewModel.ShowOnlyErrors = false;
        Assert.True(viewModel.VisibleRowScores.Count > 2);
    }

    // FR-112 AC-070（異常系）: 技術エラーの行がなければ一括選択は無効。override の再計算で行を作り直してもチェックは保たれる。
    [Fact]
    public async Task FR112_AC070_selection_survives_override_recalculation_and_bulk_select_is_disabled_without_errors()
    {
        RerunFixture clean = new();
        using ResultsOutputViewModel cleanViewModel = new(new RecordingOutputBoundary(), await clean.RunAsync());
        Assert.False(cleanViewModel.SelectTechnicalErrorRowsCommand.CanExecute(null));

        RerunFixture failing = new();
        failing.Runner.FailRows.UnionWith(FailingRows);
        using ResultsOutputViewModel viewModel = new(new RecordingOutputBoundary(), await failing.RunAsync());
        viewModel.SelectTechnicalErrorRowsCommand.Execute(null);
        viewModel.Results.First(item => item.SourceRowNumber == 44).OverrideText = "1";

        Assert.Equal(FailingRows, viewModel.RowScores.Where(row => row.IsSelectedForRerun).Select(row => row.SourceRowNumber).ToArray());
        Assert.Equal(2, viewModel.SelectedRerunCount);
    }

    // FR-112 AC-071: 結果画面で再実行すると実行画面に切り替わり、件数と元の行番号が表示される。AI 処理は始まらない。
    [Fact]
    public async Task FR112_AC071_rerun_switches_to_execution_with_target_rows_and_does_not_start_ai()
    {
        RerunFixture fixture = new();
        fixture.Runner.FailRows.UnionWith(FailingRows);
        ExecutionRunContext context = await fixture.RunAsync();
        int callsBefore = fixture.Runner.TotalCalls;
        RecordingRunBoundary boundary = new((request, _, _) => Task.FromResult(context.Summary));
        using ExecutionViewModel execution = fixture.CreateExecution(boundary);
        using ResultsOutputViewModel results = new(new RecordingOutputBoundary(), context);
        WorkflowNavigator navigator = new();
        navigator.MoveNext();
        navigator.MoveNext();
        navigator.MoveNext(); // 結果画面（実際の流れでは run 完了後にここにいる）
        using MainWindowViewModel shell = new(
            navigator, new InputViewModel(),
            new QuantificationDesignViewModel(new InputViewModel().DefinitionDraft, []),
            execution, results);
        results.SelectTechnicalErrorRowsCommand.Execute(null);

        results.RerunSelectedRowsCommand.Execute(null);

        Assert.Equal(WorkflowStep.Execution, shell.CurrentStep);
        Assert.True(execution.HasRerunTarget);
        Assert.Equal(2, execution.RerunTargetCount);
        Assert.Equal(FailingRows, execution.RerunTargetRows.ToArray());
        Assert.Contains("2 行", execution.RerunTargetText);
        Assert.Contains("43", execution.RerunTargetText);
        Assert.Contains("47", execution.RerunTargetText);
        Assert.Equal(0, boundary.CallCount); // 自動開始しない
        Assert.Equal(callsBefore, fixture.Runner.TotalCalls);

        // 利用者が明示的に開始して初めて、対象行つきの request が実行境界へ渡る。
        using ExecutionViewModel startable = fixture.CreateExecution(boundary);
        startable.SetRerunTarget(context, execution.RerunTargetRows);
        await startable.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, boundary.CallCount);
        await startable.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, boundary.CallCount);
        Assert.Equal(FailingRows, boundary.LastRequest!.RerunRows.ToArray());
        Assert.Same(context.Summary, boundary.LastRequest.RerunBase);
        Assert.False(startable.HasRerunMismatch);
    }

    // FR-112 AC-072（integration 相当）: 選んだ行の分だけ fake AI を呼び、選ばれない行は不変、出力は新しい名前。
    [Fact]
    public async Task FR112_AC072_rerun_calls_ai_only_for_selected_rows_and_replaces_only_their_results()
    {
        RerunFixture fixture = new();
        fixture.Runner.FailRows.UnionWith(FailingRows);
        ExecutionRunContext first = await fixture.RunAsync();
        Dictionary<int, string> before = RerunFixture.Outcomes(first.Summary);
        Assert.Equal(10, fixture.Runner.TotalCalls);

        fixture.Runner.ResetCalls();
        fixture.Runner.FailRows.Remove(43);
        RunSummary second = await fixture.RerunAsync(first, [43, 47]);

        Assert.Equal(QuantificationRunStatusCodes.Success, second.StatusCode);
        Assert.Equal(new[] { 43, 47 }, fixture.Runner.CalledRows.OrderBy(row => row).ToArray());
        Dictionary<int, string> after = RerunFixture.Outcomes(second);
        Assert.Equal(before.Count, after.Count);
        foreach (int row in before.Keys.Except(FailingRows))
        {
            Assert.Equal(before[row], after[row]);
        }

        Assert.NotEqual(before[43], after[43]); // 技術エラー → 成功
        Assert.Equal(before[47], after[47]); // 再度の技術エラー
        Assert.NotNull(second.FinalPath);
        Assert.NotEqual(first.Summary.FinalPath, second.FinalPath);
        Assert.Equal(10, first.Summary.CompletedRows.Length); // 元の run の結果は変わらない
    }

    // FR-112 AC-072（異常系）: 完了していない行・空の選択・前提が一致しない再実行は AI を呼ばずに拒否される。
    [Fact]
    public async Task FR112_AC072_rerun_rejects_unknown_rows_and_mismatched_model_without_ai_calls()
    {
        RerunFixture fixture = new();
        ExecutionRunContext first = await fixture.RunAsync();
        fixture.Runner.ResetCalls();

        QuantificationRunException unknown = await Assert.ThrowsAsync<QuantificationRunException>(
            () => fixture.RerunAsync(first, [99]));
        Assert.Equal("RERUN_ROWS_INVALID", unknown.Code);
        QuantificationRunException empty = await Assert.ThrowsAsync<QuantificationRunException>(
            () => fixture.RerunAsync(first, []));
        Assert.Equal("RERUN_ROWS_INVALID", empty.Code);
        QuantificationRunException model = await Assert.ThrowsAsync<QuantificationRunException>(
            () => fixture.RerunAsync(first, [43], originalModelId: "other-model"));
        Assert.Equal(CheckpointAdmissionStatusCodes.ModelMismatch, model.Code);
        Assert.Equal(0, fixture.Runner.TotalCalls);
    }

    // FR-112 AC-073: 入力 workbook または採点定義が元の run と違うと、安全な文言で示して開始せず、既存の結果は変わらない。
    [Theory]
    [InlineData("input", "入力ワークブック")]
    [InlineData("definition", "採点定義")]
    public async Task FR112_AC073_mismatched_prerequisites_are_shown_safely_and_nothing_starts(string changed, string expectedItem)
    {
        RerunFixture fixture = new();
        fixture.Runner.FailRows.UnionWith(FailingRows);
        ExecutionRunContext context = await fixture.RunAsync();
        RecordingRunBoundary boundary = new((request, _, _) => Task.FromResult(context.Summary));
        InputSnapshot current = changed == "input"
            ? new InputSnapshot(new string('C', 64), 999, new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero))
            : fixture.InputSnapshots.Snapshot;
        QuantificationDefinition definition = changed == "definition"
            ? fixture.Definition with { Revision = "2-changed" }
            : fixture.Definition;
        using ExecutionViewModel execution = fixture.CreateExecution(boundary, definition, current);
        await execution.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
        execution.SetRerunTarget(context, FailingRows);
        int callsBefore = fixture.Runner.TotalCalls;

        await execution.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, boundary.CallCount);
        Assert.Equal(callsBefore, fixture.Runner.TotalCalls);
        Assert.True(execution.HasRerunMismatch);
        Assert.Contains(expectedItem, execution.RerunMismatchText);
        Assert.DoesNotContain(context.InputPath, execution.RerunMismatchText);
        Assert.DoesNotContain("answer ", execution.RerunMismatchText);
        Assert.Equal(FailingRows, execution.RerunTargetRows.ToArray()); // 既存の結果・対象はそのまま
        Assert.Equal(10, context.Summary.CompletedRows.Length);
    }

    // FR-112 AC-073（オーケストレータ）: 入力・採点定義が違うと再実行は AI を呼ぶ前に拒否される。
    [Fact]
    public async Task FR112_AC073_orchestrator_rejects_changed_input_or_definition_before_any_ai_call()
    {
        RerunFixture fixture = new();
        fixture.Runner.FailRows.UnionWith(FailingRows);
        ExecutionRunContext first = await fixture.RunAsync();
        fixture.Runner.ResetCalls();

        fixture.InputSnapshots.Replace(new InputSnapshot(new string('D', 64), 1, new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero)));
        QuantificationRunException input = await Assert.ThrowsAsync<QuantificationRunException>(() => fixture.RerunAsync(first, [43]));
        Assert.Equal(CheckpointAdmissionStatusCodes.InputMismatch, input.Code);
        fixture.InputSnapshots.Replace(first.Summary.InputSnapshot);

        QuantificationRunException definition = await Assert.ThrowsAsync<QuantificationRunException>(
            () => fixture.RerunAsync(first, [43], definitionOverride: fixture.Definition with { Revision = "changed" }));
        Assert.Equal(CheckpointAdmissionStatusCodes.DefinitionMismatch, definition.Code);
        Assert.Equal(0, fixture.Runner.TotalCalls);
    }

    private sealed class RerunFixture : IEvaluationRowSource, IReferenceAnswerOperationRunner,
        ISpecialEvaluationOperationRunner, ICheckpointStore, IOutputPathPlanner, IDurableRunFinalizer, IPartialCheckpointCleaner
    {
        private CheckpointEnvelope? checkpoint;
        private int reservations;

        internal CountingRunner Runner { get; } = new();

        internal MutableInputSnapshots InputSnapshots { get; } = new(U01TestSupport.InputSnapshot());

        internal QuantificationDefinition Definition { get; } = U04TestSupport.Definition(42, 51);

        internal string InputPath { get; } = Path.Combine(Path.GetTempPath(), "synthetic-FR112-input.xlsx");

        internal async Task<ExecutionRunContext> RunAsync()
        {
            RunSummary summary = await Orchestrator().RunAsync(
                new DurableQuantificationRunRequest { Run = Request(Definition), Runtime = Runtime() },
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(QuantificationRunStatusCodes.Success, summary.StatusCode);
            return U04TestSupport.Context(summary, InputPath);
        }

        internal Task<RunSummary> RerunAsync(
            ExecutionRunContext original,
            IReadOnlyList<int> rows,
            string originalModelId = "model-test",
            QuantificationDefinition? definitionOverride = null) =>
            Orchestrator().RerunRowsAsync(
                new DurableQuantificationRunRequest { Run = Request(definitionOverride ?? Definition), Runtime = Runtime() },
                original.Summary,
                originalModelId,
                rows,
                cancellationToken: TestContext.Current.CancellationToken);

        internal ExecutionViewModel CreateExecution(
            IQuantificationRunBoundary boundary,
            QuantificationDefinition? definition = null,
            InputSnapshot? currentInput = null)
        {
            ExecutionViewModel viewModel = new(
                new RecordingAuthenticationBoundary(new ExecutionAuthenticationSnapshot(
                    ExecutionAuthenticationState.Available,
                    [U04TestSupport.Model("model-test"), U04TestSupport.Model("auto")],
                    U04TestSupport.RuntimeIdentity())),
                boundary,
                loginService: null,
                resumeInspectionBoundary: new FixedInspection(currentInput ?? InputSnapshots.Snapshot));
            QuantificationDefinition configured = definition ?? Definition;
            viewModel.Configure(configured, U01TestSupport.ValidateMapping(configured).Metadata, InputPath);
            return viewModel;
        }

        internal static Dictionary<int, string> Outcomes(RunSummary summary) =>
            summary.CompletedRows.ToDictionary(
                row => row.SourceRowNumber,
                row => System.Text.Json.JsonSerializer.Serialize(row));

        private DurableQuantificationOrchestrator Orchestrator() => new(
            this, Runner, this, this, InputSnapshots, this, this, this, this, new FixedClock());

        private QuantificationRunRequest Request(QuantificationDefinition definition) => new()
        {
            DraftDefinition = definition,
            WorkbookMetadata = U01TestSupport.ValidateMapping(definition).Metadata,
            InputPath = InputPath,
            ModelId = "model-test",
            MaximumPromptTokens = 64_000,
            MaximumContextWindowTokens = 128_000,
            MaxConcurrency = 1,
        };

        private static CheckpointRuntimeIdentity Runtime() => new()
        {
            ApplicationIdentity = "StudyReportEvaluator.App/4.0.0",
            CliVersion = "1.0.82",
            CliSha256 = new string('B', 64),
            SdkInformationalVersion = "1.0.11",
        };

        public Task<EvaluationRowData> ReadAsync(EvaluationRowRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new EvaluationRowData(request.SourceRowNumber, new Dictionary<string, string?>
            {
                ["A"] = $"answer {request.SourceRowNumber}",
                ["B"] = $"support {request.SourceRowNumber}",
            }));

        public Task<AuxiliaryOperationResult<ReferenceAnswerResult>> EvaluateAsync(SafeReferenceAnswerPayload payload, CancellationToken cancellationToken) =>
            Task.FromResult(AuxiliaryOperationResult<ReferenceAnswerResult>.Succeeded(
                new ReferenceAnswerResult { QuestionId = payload.QuestionId, Answer = "reference" }));

        public Task<AuxiliaryOperationResult<SpecialQuantificationResult>> EvaluateAsync(SafeSpecialEvaluationPayload payload, string modelId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No special evaluation.");

        public CheckpointSaveResult Create(CheckpointEnvelope envelope, CancellationToken cancellationToken = default)
        {
            checkpoint = envelope;
            return CheckpointSaveResult.Succeeded(createdNew: true);
        }

        public CheckpointSaveResult Update(CheckpointEnvelope envelope, CancellationToken cancellationToken = default)
        {
            checkpoint = envelope;
            return CheckpointSaveResult.Succeeded(createdNew: false);
        }

        public CheckpointLoadResult Load(string partialPath, CancellationToken cancellationToken = default) =>
            checkpoint is null ? CheckpointLoadResult.Failed(CheckpointStatusCodes.Invalid) : CheckpointLoadResult.Succeeded(checkpoint);

        public OutputPathReservation Reserve(string inputPath, DateTimeOffset localTime, string? outputDirectory = null)
        {
            string directory = outputDirectory ?? Path.GetTempPath();
            int number = Interlocked.Increment(ref reservations);
            return OutputPathReservation.Create(
                directory,
                Path.Combine(directory, $"synthetic-FR112-final-{number}.xlsx"),
                Path.Combine(directory, $"synthetic-FR112-final-{number}.partial.xlsx"));
        }

        public DurableFinalizationResult Finalize(RunSummary summary, CheckpointEnvelope envelope, CancellationToken cancellationToken) =>
            DurableFinalizationResult.Succeeded(envelope.FinalPath);

        public bool TryDelete(string partialPath) => true;
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
            return Task.FromResult(FailRows.Contains(row)
                ? EvaluationRunnerResult.Failed(ResultsStatusCodes.AiTimeout)
                : EvaluationRunnerResult.Succeeded(U01TestSupport.ValidResult(payload, _ => 5m + (row % 5))));
        }
    }

    private sealed class MutableInputSnapshots(InputSnapshot snapshot) : IInputSnapshotBoundary
    {
        internal InputSnapshot Snapshot { get; private set; } = snapshot;

        internal void Replace(InputSnapshot value) => Snapshot = value;

        public InputSnapshot Capture(string inputPath) => Snapshot;

        public bool IsUnchanged(string inputPath, InputSnapshot expected) => Snapshot.Equals(expected);
    }

    private sealed class FixedInspection(InputSnapshot snapshot) : IResumeInspectionBoundary
    {
        public Task<CheckpointLoadResult> LoadAsync(string partialPath, CancellationToken cancellationToken) =>
            Task.FromResult(CheckpointLoadResult.Failed(CheckpointStatusCodes.Invalid));

        public Task<InputSnapshot> CaptureInputAsync(string inputPath, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class FixedClock : TimeProvider
    {
        private long ticks = new DateTimeOffset(2026, 11, 3, 1, 2, 3, TimeSpan.Zero).UtcTicks;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => new(Interlocked.Add(ref ticks, TimeSpan.TicksPerSecond), TimeSpan.Zero);
    }
}

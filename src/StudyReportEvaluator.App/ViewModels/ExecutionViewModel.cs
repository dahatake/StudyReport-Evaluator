using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Input;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.App.Workbooks.Mapping;
using StudyReportEvaluator.App.Workbooks.Checkpoint;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Validation;

namespace StudyReportEvaluator.App.ViewModels;

public enum ExecutionAuthenticationState
{
    NotChecked,
    Checking,
    Available,
    AuthRequired,
    CliUnavailable,
    RuntimeFailed,
    Cancelled,
}

public sealed class ExecutionAuthenticationSnapshot
{
    public ExecutionAuthenticationSnapshot(
        ExecutionAuthenticationState state,
        IEnumerable<string>? modelIds,
        CopilotRuntimeIdentity? runtimeIdentity = null)
        : this(
            state,
            modelIds?.Select(id => new CopilotModelAvailability(id, null, null)),
            runtimeIdentity)
    {
    }

    public ExecutionAuthenticationSnapshot(
        ExecutionAuthenticationState state,
        IEnumerable<CopilotModelAvailability>? models = null,
        CopilotRuntimeIdentity? runtimeIdentity = null)
    {
        if (state is ExecutionAuthenticationState.NotChecked or ExecutionAuthenticationState.Checking)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        List<CopilotModelAvailability> normalized = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (CopilotModelAvailability? model in models ?? [])
        {
            if (model is null || !seen.Add(model.Id))
            {
                continue;
            }

            normalized.Add(model);
        }

        if (state == ExecutionAuthenticationState.Available && runtimeIdentity is null)
        {
            throw new ArgumentException(
                "An available authentication snapshot requires a runtime identity.",
                nameof(runtimeIdentity));
        }

        State = state;
        Models = normalized.AsReadOnly();
        ModelIds = Array.AsReadOnly(normalized.Select(model => model.Id).ToArray());
        RuntimeIdentity = runtimeIdentity;
    }

    public ExecutionAuthenticationState State { get; }

    public IReadOnlyList<string> ModelIds { get; }

    public IReadOnlyList<CopilotModelAvailability> Models { get; }

    public CopilotRuntimeIdentity? RuntimeIdentity { get; }

    public override string ToString() =>
        $"{nameof(ExecutionAuthenticationSnapshot)} {{ State = {State}, ModelCount = {ModelIds.Count.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";
}

public interface IExecutionAuthenticationBoundary
{
    Task<ExecutionAuthenticationSnapshot> CheckAsync(CancellationToken cancellationToken);
}

public sealed class CopilotExecutionAuthenticationBoundary : IExecutionAuthenticationBoundary
{
    private readonly CopilotAuthenticationService service;

    public CopilotExecutionAuthenticationBoundary()
        : this(new CopilotAuthenticationService())
    {
    }

    public CopilotExecutionAuthenticationBoundary(CopilotAuthenticationService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public async Task<ExecutionAuthenticationSnapshot> CheckAsync(
        CancellationToken cancellationToken)
    {
        CopilotAuthenticationResult result = await service
            .CheckAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ExecutionAuthenticationSnapshot(
            result.Status switch
            {
                CopilotAuthenticationStatus.Available => ExecutionAuthenticationState.Available,
                CopilotAuthenticationStatus.AuthRequired => ExecutionAuthenticationState.AuthRequired,
                CopilotAuthenticationStatus.CliUnavailable => ExecutionAuthenticationState.CliUnavailable,
                CopilotAuthenticationStatus.RuntimeFailed => ExecutionAuthenticationState.RuntimeFailed,
                CopilotAuthenticationStatus.Cancelled => ExecutionAuthenticationState.Cancelled,
                _ => ExecutionAuthenticationState.RuntimeFailed,
            },
            result.AvailableModels,
            result.Identity);
    }

    public override string ToString() =>
        $"{nameof(CopilotExecutionAuthenticationBoundary)} {{ Content = <redacted> }}";
}

public interface IQuantificationRunBoundary
{
    Task<RunSummary> RunAsync(
        QuantificationRunRequest request,
        Action<EvaluationProgress>? progress,
        CancellationToken cancellationToken);
}

public sealed class QuantificationRunBoundary : IQuantificationRunBoundary
{
    public Task<RunSummary> RunAsync(
        QuantificationRunRequest request,
        Action<EvaluationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        OpenXmlEvaluationRowSource rowSource = new(request.InputPath);
        EphemeralEvaluationRunnerOptions runnerOptions = new(
            maxConcurrency: request.MaxConcurrency);
        EphemeralEvaluationRunner runner = new(
            new SdkEphemeralCopilotTransportFactory(new CopilotClientFactory()),
            runnerOptions);
        EphemeralEvaluationRunnerAdapter normalRunner = new(runner);
        if (!request.UseDurableWorkflow)
        {
            QuantificationOrchestrator orchestrator = new(rowSource, normalRunner);
            return orchestrator.RunAsync(request, progress, cancellationToken);
        }

        CopilotRuntimeIdentity identity = request.RuntimeIdentity
            ?? throw new QuantificationRunException("RUNTIME_IDENTITY_REQUIRED");
        SdkEphemeralCopilotTransportFactory transportFactory =
            new(new CopilotClientFactory());
        DurableQuantificationOrchestrator durable = new(
            rowSource,
            normalRunner,
            new ReferenceAnswerOperationRunnerAdapter(new ReferenceAnswerEvaluationRunner(
                transportFactory,
                runnerOptions)),
            new SpecialEvaluationOperationRunnerAdapter(new SpecialEvaluationRunner(
                transportFactory,
                runnerOptions)),
            new SimilarityEvaluationOperationRunnerAdapter(new SimilarityEvaluationRunner(
                transportFactory,
                runnerOptions)));
        return durable.RunAsync(
            new DurableQuantificationRunRequest
            {
                Run = request,
                Runtime = new CheckpointRuntimeIdentity
                {
                    ApplicationIdentity = ApplicationIdentity(),
                    CliVersion = identity.CliVersion,
                    CliSha256 = identity.CliSha256,
                    SdkInformationalVersion = identity.SdkInformationalVersion,
                },
                OutputDirectory = request.OutputDirectory,
                ResumePartialPath = request.ResumePartialPath,
            },
            value => progress?.Invoke(ToLegacyProgress(value)),
            cancellationToken);
    }

    public override string ToString() =>
        $"{nameof(QuantificationRunBoundary)} {{ Content = <redacted> }}";

    private static EvaluationProgress ToLegacyProgress(DurableEvaluationProgress value) =>
        new(
            value.OperationTotal,
            value.OperationCompleted,
            value.InFlight,
            value.Stage switch
            {
                DurableEvaluationStage.Cancelling => EvaluationProgressStatus.Cancelling,
                DurableEvaluationStage.Completed when string.Equals(
                    value.StatusCode,
                    QuantificationRunStatusCodes.Success,
                    StringComparison.Ordinal) => EvaluationProgressStatus.Completed,
                DurableEvaluationStage.Completed => EvaluationProgressStatus.Cancelled,
                _ => EvaluationProgressStatus.Running,
            },
            value.Stage,
            value.ReferenceCompleted,
            value.ReferenceTotal,
            value.RowCompleted,
            value.RowTotal,
            value.StatusCode,
            value.FinalPath,
            value.PartialPath);

    private static string ApplicationIdentity()
    {
        Assembly assembly = typeof(QuantificationRunBoundary).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        string version = informationalVersion?.Split('+', 2)[0]
            ?? assembly.GetName().Version?.ToString(3)
            ?? "unknown";
        return (assembly.GetName().Name ?? "StudyReportEvaluator.App") + "/" + version;
    }
}

public sealed class ExecutionTechnicalError
{
    public ExecutionTechnicalError(string code, string field, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Field = field;
        Message = message;
    }

    public string Code { get; }

    public string Field { get; }

    public string Message { get; }

    public string AccessibleText => $"{Field}。{Message}";

    public override string ToString() =>
        $"{nameof(ExecutionTechnicalError)} {{ Code = {Code}, Field = {Field}, Content = <redacted> }}";
}

public sealed class ExecutionRunContext
{
    public ExecutionRunContext(
        RunSummary summary,
        string inputPath,
        string modelId,
        CopilotRuntimeIdentity runtimeIdentity)
    {
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        RuntimeIdentity = runtimeIdentity
            ?? throw new ArgumentNullException(nameof(runtimeIdentity));
        if (modelId.Length > 256
            || !string.Equals(modelId, modelId.Trim(), StringComparison.Ordinal)
            || modelId.Any(char.IsControl))
        {
            throw new ArgumentException("The model identity is invalid.", nameof(modelId));
        }

        InputPath = Path.GetFullPath(inputPath);
        ModelId = modelId;
    }

    public RunSummary Summary { get; }

    public string InputPath { get; }

    public string ModelId { get; }

    public CopilotRuntimeIdentity RuntimeIdentity { get; }

    public override string ToString() =>
        $"{nameof(ExecutionRunContext)} {{ StatusCode = {Summary.StatusCode}, Content = <redacted> }}";
}

public sealed class ExecutionRunCompletedEventArgs : EventArgs
{
    public ExecutionRunCompletedEventArgs(ExecutionRunContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public ExecutionRunContext Context { get; }
}

public sealed class ExecutionViewModel : UiObservableObject, IDisposable
{
    private static readonly IReadOnlyList<int> ClosedConcurrencyOptions =
        Array.AsReadOnly([1, 2, 3]);

    private readonly IExecutionAuthenticationBoundary authenticationBoundary;
    private readonly IQuantificationRunBoundary runBoundary;
    private readonly QuantificationDefinitionValidator definitionValidator = new();
    private readonly ColumnMappingValidator mappingValidator = new();
    private readonly WorkbookExecutionPreflight workbookPreflight = new();
    private readonly ObservableCollection<string> modelItems = [];
    private readonly Dictionary<string, CopilotModelAvailability> modelsById = new(StringComparer.Ordinal);
    private readonly ObservableCollection<ExecutionTechnicalError> technicalErrorItems = [];
    private readonly ViewModelCommand checkAuthenticationCommand;
    private readonly ViewModelCommand startCommand;
    private readonly ViewModelCommand cancelCommand;
    private QuantificationDefinition? definition;
    private WorkbookMetadata? workbookMetadata;
    private string inputPath = string.Empty;
    private string? selectedModelId;
    private CopilotRuntimeIdentity? runtimeIdentity;
    private ExecutionAuthenticationState authenticationState = ExecutionAuthenticationState.NotChecked;
    private string? runtimeErrorCode;
    private ImmutableArray<ExecutionTechnicalError> runPreflightErrors = [];
    private int maxConcurrency = EvaluationSchedulerOptions.DefaultMaxConcurrency;
    private bool isCheckingAuthentication;
    private bool isRunning;
    private bool isCancelling;
    private int progressTotal;
    private int progressCompleted;
    private int progressInFlight;
    private EvaluationProgressStatus? progressStatus;
    private DurableEvaluationStage? progressStage;
    private int referenceCompleted;
    private int referenceTotal;
    private int rowCompleted;
    private int rowTotal;
    private string reservedFinalPath = string.Empty;
    private string partialPath = string.Empty;
    private string outputDirectory = string.Empty;
    private string resumePartialPath = string.Empty;
    private bool isResumeMode;
    private ExecutionRunContext? lastRunContext;
    private CancellationTokenSource? authenticationCancellation;
    private CancellationTokenSource? runCancellation;
    private long authenticationSequence;
    private bool disposed;

    public ExecutionViewModel()
        : this(
            new CopilotExecutionAuthenticationBoundary(),
            new QuantificationRunBoundary())
    {
    }

    public ExecutionViewModel(
        IExecutionAuthenticationBoundary authenticationBoundary,
        IQuantificationRunBoundary runBoundary)
    {
        this.authenticationBoundary = authenticationBoundary
            ?? throw new ArgumentNullException(nameof(authenticationBoundary));
        this.runBoundary = runBoundary
            ?? throw new ArgumentNullException(nameof(runBoundary));
        AvailableModelIds = new ReadOnlyObservableCollection<string>(modelItems);
        TechnicalErrors = new ReadOnlyObservableCollection<ExecutionTechnicalError>(technicalErrorItems);
        checkAuthenticationCommand = new ViewModelCommand(
            _ => _ = CheckAuthenticationAsync(),
            _ => CanCheckAuthentication);
        startCommand = new ViewModelCommand(
            _ => _ = StartAsync(),
            _ => CanStart);
        cancelCommand = new ViewModelCommand(
            _ => Cancel(),
            _ => CanCancel);
        Revalidate();
    }

    public event EventHandler<ExecutionRunCompletedEventArgs>? RunCompleted;

    public ReadOnlyObservableCollection<string> AvailableModelIds { get; }

    public ReadOnlyObservableCollection<ExecutionTechnicalError> TechnicalErrors { get; }

    public IReadOnlyList<int> ConcurrencyOptions => ClosedConcurrencyOptions;

    public bool IsConfigured => definition is not null
        && workbookMetadata is not null
        && !string.IsNullOrWhiteSpace(inputPath);

    public ExecutionAuthenticationState AuthenticationState
    {
        get => authenticationState;
        private set
        {
            if (SetProperty(ref authenticationState, value))
            {
                OnPropertiesChanged(
                    nameof(AuthenticationStatusText),
                    nameof(IsAuthenticationAvailable));
            }
        }
    }

    public bool IsAuthenticationAvailable =>
        AuthenticationState == ExecutionAuthenticationState.Available;

    public string AuthenticationStatusText => AuthenticationState switch
    {
        ExecutionAuthenticationState.NotChecked => "Copilot CLI の login 状態は未確認です。",
        ExecutionAuthenticationState.Checking => "Copilot CLI と利用可能 model を確認しています…",
        ExecutionAuthenticationState.Available => "既存の Copilot CLI login を利用できます。",
        ExecutionAuthenticationState.AuthRequired => "Copilot CLI で login してから再確認してください。",
        ExecutionAuthenticationState.CliUnavailable => "Copilot CLI を確認できません。PATH と導入状態を確認してください。",
        ExecutionAuthenticationState.RuntimeFailed => "Copilot runtime の確認に失敗しました。",
        ExecutionAuthenticationState.Cancelled => "Copilot 状態の確認を取り消しました。",
        _ => "Copilot runtime の状態を確認できません。",
    };

    public string RuntimeIdentityText => runtimeIdentity is null
        ? "runtime identity はまだありません。"
        : $"CLI {runtimeIdentity.CliVersion} · SHA-256 {runtimeIdentity.CliSha256[..12]}… · SDK {runtimeIdentity.SdkInformationalVersion}";

    public string? SelectedModelId
    {
        get => selectedModelId;
        set
        {
            string? next = value;
            if (SetProperty(ref selectedModelId, next))
            {
                runtimeErrorCode = null;
                runPreflightErrors = [];
                Revalidate();
            }
        }
    }

    public int MaxConcurrency
    {
        get => maxConcurrency;
        set
        {
            if (SetProperty(ref maxConcurrency, value))
            {
                runtimeErrorCode = null;
                runPreflightErrors = [];
                Revalidate();
                OnPropertyChanged(nameof(ConcurrencyText));
            }
        }
    }

    public string ConcurrencyText =>
        $"最大 {MaxConcurrency.ToString(CultureInfo.InvariantCulture)} 件を並列実行（許可範囲 1～3）";

    public string OutputDirectory
    {
        get => outputDirectory;
        set
        {
            if (SetProperty(ref outputDirectory, value ?? string.Empty))
            {
                runtimeErrorCode = null;
                Revalidate();
            }
        }
    }

    public string ResumePartialPath
    {
        get => resumePartialPath;
        set
        {
            if (SetProperty(ref resumePartialPath, value ?? string.Empty))
            {
                runtimeErrorCode = null;
                Revalidate();
            }
        }
    }

    public bool IsResumeMode
    {
        get => isResumeMode;
        set
        {
            if (SetProperty(ref isResumeMode, value))
            {
                runtimeErrorCode = null;
                OnPropertiesChanged(nameof(OutputModeText), nameof(CanStart));
                Revalidate();
            }
        }
    }

    public string OutputModeText => IsResumeMode
        ? "既存の .partial.xlsx をread-only検証して再開します。"
        : "新規runとしてfinal/partial名を同時予約します。";

    public long PlannedEvaluationCount
    {
        get
        {
            if (definition is null)
            {
                return 0;
            }

            long selectedRows = Math.Max(
                0L,
                ((long)definition.LastDataRow - definition.FirstDataRow) + 1L);
            long referenceOperations = definition.Questions.Count(question => question.Enabled);
            long normalOperations = definition.Questions
                .Where(question => question.Enabled)
                .Sum(question => (long)question.Evaluators.Count(evaluator => evaluator.Enabled));
            long specialOperations = definition.Questions
                .Where(question => question.Enabled)
                .Sum(question => (long)question.SpecialEvaluations.Count(special => special.Enabled));
            try
            {
                return checked(referenceOperations
                    + selectedRows * (normalOperations + specialOperations + referenceOperations));
            }
            catch (OverflowException)
            {
                return long.MaxValue;
            }
        }
    }

    public string PlanSummary => IsConfigured
        ? $"{PlannedEvaluationCount.ToString("N0", CultureInfo.InvariantCulture)} evaluation units · retry込み最大 {WorstCaseAttemptCount.ToString("N0", CultureInfo.InvariantCulture)} attempts · snapshot は実行開始時に固定"
        : "入力と定量化設計を完了してください。";

    public long WorstCaseAttemptCount
    {
        get
        {
            try
            {
                return checked(PlannedEvaluationCount * RetryAndCleanupCoordinator.MaximumTransientAttempts);
            }
            catch (OverflowException)
            {
                return long.MaxValue;
            }
        }
    }

    public bool IsCheckingAuthentication
    {
        get => isCheckingAuthentication;
        private set
        {
            if (SetProperty(ref isCheckingAuthentication, value))
            {
                OnPropertiesChanged(
                    nameof(CanCheckAuthentication),
                    nameof(CanStart));
                RaiseCommandStates();
            }
        }
    }

    public bool IsRunning
    {
        get => isRunning;
        private set
        {
            if (SetProperty(ref isRunning, value))
            {
                OnPropertiesChanged(
                    nameof(CanCheckAuthentication),
                    nameof(CanStart),
                    nameof(CanCancel),
                    nameof(RunStatusText));
                RaiseCommandStates();
            }
        }
    }

    public bool IsCancelling
    {
        get => isCancelling;
        private set
        {
            if (SetProperty(ref isCancelling, value))
            {
                OnPropertiesChanged(nameof(CanCancel), nameof(RunStatusText));
                RaiseCommandStates();
            }
        }
    }

    public int ProgressTotal => progressTotal;

    public int ProgressCompleted => progressCompleted;

    public int ProgressInFlight => progressInFlight;

    public DurableEvaluationStage? ProgressStage => progressStage;

    public int ReferenceCompleted => referenceCompleted;

    public int ReferenceTotal => referenceTotal;

    public int RowCompleted => rowCompleted;

    public int RowTotal => rowTotal;

    public string ReservedFinalPath => reservedFinalPath;

    public string PartialPath => partialPath;

    public string StageText => progressStage switch
    {
        DurableEvaluationStage.Preparing => "準備中",
        DurableEvaluationStage.GeneratingReferences => "参照回答を生成中",
        DurableEvaluationStage.EvaluatingRows => "学生行を評価中",
        DurableEvaluationStage.SavingCheckpoint => "checkpointを保存中",
        DurableEvaluationStage.FinalizingWorkbook => "final workbookを検証中",
        DurableEvaluationStage.Completed => "完了",
        DurableEvaluationStage.Cancelling => "cancel処理中",
        _ => "未開始",
    };

    public string DurableProgressText =>
        $"参照 {ReferenceCompleted.ToString(CultureInfo.InvariantCulture)} / {ReferenceTotal.ToString(CultureInfo.InvariantCulture)} · 行 {RowCompleted.ToString(CultureInfo.InvariantCulture)} / {RowTotal.ToString(CultureInfo.InvariantCulture)}";

    public string OutputIdentityText => string.IsNullOrWhiteSpace(PartialPath)
        ? "run開始時にfinal/partial pathを予約します。"
        : $"final: {(string.IsNullOrWhiteSpace(ReservedFinalPath) ? "—" : ReservedFinalPath)}{Environment.NewLine}partial: {PartialPath}";

    public double ProgressPercent => progressTotal == 0
        ? 0d
        : (double)progressCompleted / progressTotal * 100d;

    public string ProgressText => progressTotal == 0
        ? "まだ実行していません。"
        : $"{progressCompleted.ToString("N0", CultureInfo.InvariantCulture)} / {progressTotal.ToString("N0", CultureInfo.InvariantCulture)} 完了 · {progressInFlight.ToString(CultureInfo.InvariantCulture)} 実行中 · {ProgressPercent.ToString("0.0", CultureInfo.InvariantCulture)}%";

    public string RunStatusText
    {
        get
        {
            if (IsCancelling)
            {
                return "cancel を受け付けました。実行中 unit の安全な終了を待っています。";
            }

            if (IsRunning)
            {
                return "同一 immutable snapshot で評価しています。";
            }

            if (lastRunContext is null)
            {
                return "実行準備中です。";
            }

            RunSummary summary = lastRunContext.Summary;
            return summary.StatusCode switch
            {
                QuantificationRunStatusCodes.Success when summary.PartialCleanupFailed =>
                    "final workbookは有効ですが、partial cleanupに失敗しました。",
                QuantificationRunStatusCodes.Success => "検証済みfinal workbookを自動作成しました。",
                QuantificationRunStatusCodes.Cancelled => "cancel 済みの部分結果をpartial checkpointへ保持しました。",
                QuantificationRunStatusCodes.InputChanged => "入力変更を検出したため、出力は停止されています。",
                QuantificationRunStatusCodes.CheckpointFailed => "checkpointを安全に保存できなかったため停止しました。",
                QuantificationRunStatusCodes.OutputInvalid => "final検証に失敗したためpartialを保持しました。",
                _ => "実行状態を確認してください。",
            };
        }
    }

    public ExecutionRunContext? LastRunContext
    {
        get => lastRunContext;
        private set => SetProperty(ref lastRunContext, value);
    }

    public bool HasTechnicalErrors => technicalErrorItems.Count > 0;

    public bool IsTechnicallyValid => !HasTechnicalErrors;

    public string ValidationSummary => IsTechnicallyValid
        ? "技術検証を通過しました。run を開始できます。"
        : $"実行前に解消する技術的な問題が {technicalErrorItems.Count.ToString(CultureInfo.InvariantCulture)} 件あります。";

    public bool CanCheckAuthentication => !disposed
        && !IsCheckingAuthentication
        && !IsRunning;

    public bool CanStart => !disposed
        && IsConfigured
        && !IsCheckingAuthentication
        && !IsRunning
        && AuthenticationState == ExecutionAuthenticationState.Available
        && runtimeIdentity is not null
        && SelectedModelId is not null
        && modelItems.Contains(SelectedModelId)
        && MaxConcurrency is >= EvaluationSchedulerOptions.DefaultMaxConcurrency
            and <= EvaluationSchedulerOptions.MaximumMaxConcurrency
        && !HasTechnicalErrors;

    public bool CanCancel => IsRunning && !IsCancelling;

    public ICommand CheckAuthenticationCommand => checkAuthenticationCommand;

    public ICommand StartCommand => startCommand;

    public ICommand CancelCommand => cancelCommand;

    public void Configure(
        QuantificationDefinition draftDefinition,
        WorkbookMetadata metadata,
        string configuredInputPath)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(draftDefinition);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredInputPath);
        if (IsRunning)
        {
            throw new InvalidOperationException("An active run cannot be reconfigured.");
        }

        definition = InputViewModel.CloneDefinition(draftDefinition);
        workbookMetadata = metadata;
        inputPath = configuredInputPath;
        outputDirectory = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(configuredInputPath)) ?? string.Empty,
            "result");
        resumePartialPath = string.Empty;
        isResumeMode = false;
        runtimeErrorCode = null;
        runPreflightErrors = [];
        LastRunContext = null;
        ResetProgress();
        OnPropertiesChanged(
            nameof(IsConfigured),
            nameof(OutputDirectory),
            nameof(ResumePartialPath),
            nameof(IsResumeMode),
            nameof(OutputModeText),
            nameof(PlannedEvaluationCount),
            nameof(WorstCaseAttemptCount),
            nameof(PlanSummary),
            nameof(RunStatusText));
        Revalidate();
    }

    internal void ClearConfiguration()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsRunning)
        {
            throw new InvalidOperationException("An active run configuration cannot be cleared.");
        }

        definition = null;
        workbookMetadata = null;
        inputPath = string.Empty;
        outputDirectory = string.Empty;
        resumePartialPath = string.Empty;
        isResumeMode = false;
        runtimeErrorCode = null;
        runPreflightErrors = [];
        LastRunContext = null;
        ResetProgress();
        OnPropertiesChanged(
            nameof(IsConfigured),
            nameof(OutputDirectory),
            nameof(ResumePartialPath),
            nameof(IsResumeMode),
            nameof(OutputModeText),
            nameof(PlannedEvaluationCount),
            nameof(WorstCaseAttemptCount),
            nameof(PlanSummary),
            nameof(RunStatusText));
        Revalidate();
    }

    public async Task CheckAuthenticationAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!CanCheckAuthentication)
        {
            return;
        }

        long sequence = Interlocked.Increment(ref authenticationSequence);
        authenticationCancellation?.Cancel();
        authenticationCancellation?.Dispose();
        authenticationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken token = authenticationCancellation.Token;
        IsCheckingAuthentication = true;
        AuthenticationState = ExecutionAuthenticationState.Checking;
        runtimeIdentity = null;
        modelItems.Clear();
        selectedModelId = null;
        runtimeErrorCode = null;
        runPreflightErrors = [];
        OnPropertiesChanged(
            nameof(AvailableModelIds),
            nameof(SelectedModelId),
            nameof(RuntimeIdentityText));
        Revalidate();

        try
        {
            ExecutionAuthenticationSnapshot result = await authenticationBoundary
                .CheckAsync(token);
            if (sequence != Volatile.Read(ref authenticationSequence))
            {
                return;
            }

            ApplyAuthentication(result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (sequence == Volatile.Read(ref authenticationSequence))
            {
                AuthenticationState = ExecutionAuthenticationState.Cancelled;
            }
        }
        catch
        {
            if (sequence == Volatile.Read(ref authenticationSequence))
            {
                AuthenticationState = ExecutionAuthenticationState.RuntimeFailed;
            }
        }
        finally
        {
            if (sequence == Volatile.Read(ref authenticationSequence))
            {
                IsCheckingAuthentication = false;
                Revalidate();
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Revalidate();
        if (!CanStart
            || definition is null
            || workbookMetadata is null
            || selectedModelId is null
            || runtimeIdentity is null)
        {
            return;
        }

        QuantificationDefinition runDefinition = InputViewModel.CloneDefinition(definition);
        string runInputPath = inputPath;
        string runModelId = selectedModelId;
        CopilotModelAvailability runModel = modelsById[runModelId];
        CopilotRuntimeIdentity runRuntimeIdentity = runtimeIdentity;
        QuantificationRunRequest request = new()
        {
            DraftDefinition = runDefinition,
            WorkbookMetadata = workbookMetadata,
            InputPath = runInputPath,
            ModelId = runModelId,
            MaximumPromptTokens = runModel.EffectivePromptTokenLimit!.Value,
            MaximumContextWindowTokens = runModel.MaximumContextWindowTokens
                ?? runModel.EffectivePromptTokenLimit.Value,
            MaxConcurrency = MaxConcurrency,
            RuntimeIdentity = runRuntimeIdentity,
            OutputDirectory = IsResumeMode ? null : OutputDirectory,
            ResumePartialPath = IsResumeMode ? ResumePartialPath : null,
            UseDurableWorkflow = true,
        };

        runCancellation?.Cancel();
        runCancellation?.Dispose();
        runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken token = runCancellation.Token;
        SynchronizationContext? observerContext = SynchronizationContext.Current;
        runtimeErrorCode = null;
        runPreflightErrors = [];
        LastRunContext = null;
        ResetProgress();
        IsCancelling = false;
        IsRunning = true;
        Revalidate();

        try
        {
            RunSummary summary = await runBoundary.RunAsync(
                request,
                progress => ReportProgress(progress, observerContext),
                token);
            ApplyCompletedSummary(summary);
            ExecutionRunContext context = new(
                summary,
                runInputPath,
                runModelId,
                runRuntimeIdentity);
            LastRunContext = context;
            RaiseRunCompleted(context);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            runtimeErrorCode = "RUN_CANCELLED_WITHOUT_SUMMARY";
            progressStatus = EvaluationProgressStatus.Cancelled;
        }
        catch (QuantificationMappingValidationException)
        {
            runtimeErrorCode = "MAPPING_INVALID";
        }
        catch (QuantificationDefinitionValidationException)
        {
            runtimeErrorCode = "DEFINITION_INVALID";
        }
        catch (QuantificationRunPreflightException exception)
        {
            runtimeErrorCode = null;
            runPreflightErrors = exception.Errors
                .Select(CapacityTechnicalError)
                .ToImmutableArray();
        }
        catch (QuantificationRunException exception)
        {
            runtimeErrorCode = exception.Code;
        }
        catch
        {
            runtimeErrorCode = "RUN_FAILED";
        }
        finally
        {
            IsRunning = false;
            IsCancelling = false;
            OnPropertiesChanged(
                nameof(ProgressText),
                nameof(ProgressPercent),
                nameof(RunStatusText));
            Revalidate();
        }
    }

    public void Cancel()
    {
        if (!CanCancel)
        {
            return;
        }

        IsCancelling = true;
        progressStatus = EvaluationProgressStatus.Cancelling;
        runCancellation?.Cancel();
        OnPropertiesChanged(nameof(ProgressText), nameof(RunStatusText));
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Interlocked.Increment(ref authenticationSequence);
        authenticationCancellation?.Cancel();
        authenticationCancellation?.Dispose();
        runCancellation?.Cancel();
        runCancellation?.Dispose();
        RaiseCommandStates();
    }

    public override string ToString() =>
        $"{nameof(ExecutionViewModel)} {{ AuthenticationState = {AuthenticationState}, IsRunning = {IsRunning}, PlannedEvaluationCount = {PlannedEvaluationCount.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";

    private void ApplyAuthentication(ExecutionAuthenticationSnapshot result)
    {
        AuthenticationState = result.State;
        runtimeIdentity = result.RuntimeIdentity;
        modelItems.Clear();
        modelsById.Clear();
        foreach (CopilotModelAvailability model in result.Models)
        {
            modelItems.Add(model.Id);
            modelsById.Add(model.Id, model);
        }

        selectedModelId = result.State == ExecutionAuthenticationState.Available
            ? modelItems.FirstOrDefault()
            : null;
        OnPropertiesChanged(
            nameof(AvailableModelIds),
            nameof(SelectedModelId),
            nameof(RuntimeIdentityText));
    }

    private void RaiseRunCompleted(ExecutionRunContext context)
    {
        EventHandler<ExecutionRunCompletedEventArgs>? handlers = RunCompleted;
        if (handlers is null)
        {
            return;
        }

        ExecutionRunCompletedEventArgs eventArgs = new(context);
        foreach (EventHandler<ExecutionRunCompletedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch
            {
                // Navigation and presentation observers cannot corrupt completed run data.
            }
        }
    }

    private void ReportProgress(
        EvaluationProgress progress,
        SynchronizationContext? observerContext)
    {
        if (observerContext is null || ReferenceEquals(observerContext, SynchronizationContext.Current))
        {
            ApplyProgress(progress);
            return;
        }

        observerContext.Post(_ => ApplyProgress(progress), null);
    }

    private void ApplyProgress(EvaluationProgress progress)
    {
        progressTotal = progress.Total;
        progressCompleted = progress.Completed;
        progressInFlight = progress.InFlight;
        progressStatus = progress.Status;
        progressStage = progress.Stage;
        referenceCompleted = progress.ReferenceCompleted;
        referenceTotal = progress.ReferenceTotal;
        rowCompleted = progress.RowCompleted;
        rowTotal = progress.RowTotal;
        reservedFinalPath = progress.FinalPath ?? reservedFinalPath;
        partialPath = progress.PartialPath ?? partialPath;
        OnPropertiesChanged(
            nameof(ProgressTotal),
            nameof(ProgressCompleted),
            nameof(ProgressInFlight),
            nameof(ProgressPercent),
            nameof(ProgressText),
            nameof(ProgressStage),
            nameof(ReferenceCompleted),
            nameof(ReferenceTotal),
            nameof(RowCompleted),
            nameof(RowTotal),
            nameof(ReservedFinalPath),
            nameof(PartialPath),
            nameof(StageText),
            nameof(DurableProgressText),
            nameof(OutputIdentityText),
            nameof(RunStatusText));
    }

    private void ApplyCompletedSummary(RunSummary summary)
    {
        progressTotal = summary.PlannedOperationCount;
        progressCompleted = summary.CompletedOperationCount;
        progressInFlight = 0;
        progressStage = DurableEvaluationStage.Completed;
        if (summary.IsDurable)
        {
            referenceTotal = summary.Snapshot.Definition.Questions.Count(question => question.Enabled);
            referenceCompleted = summary.References.Length;
            rowTotal = summary.Plan.Mapping.SelectedRowCount;
            rowCompleted = summary.CompletedRows.Length;
        }

        reservedFinalPath = summary.FinalPath ?? reservedFinalPath;
        partialPath = summary.PartialPath ?? partialPath;
        progressStatus = summary.IsPartial
            ? EvaluationProgressStatus.Cancelled
            : EvaluationProgressStatus.Completed;
        OnPropertiesChanged(
            nameof(ProgressTotal),
            nameof(ProgressCompleted),
            nameof(ProgressInFlight),
            nameof(ProgressPercent),
            nameof(ProgressText),
            nameof(ProgressStage),
            nameof(ReferenceCompleted),
            nameof(ReferenceTotal),
            nameof(RowCompleted),
            nameof(RowTotal),
            nameof(ReservedFinalPath),
            nameof(PartialPath),
            nameof(StageText),
            nameof(DurableProgressText),
            nameof(OutputIdentityText));
    }

    private void ResetProgress()
    {
        progressTotal = 0;
        progressCompleted = 0;
        progressInFlight = 0;
        progressStatus = null;
        progressStage = null;
        referenceCompleted = 0;
        referenceTotal = 0;
        rowCompleted = 0;
        rowTotal = 0;
        reservedFinalPath = string.Empty;
        partialPath = string.Empty;
        OnPropertiesChanged(
            nameof(ProgressTotal),
            nameof(ProgressCompleted),
            nameof(ProgressInFlight),
            nameof(ProgressPercent),
            nameof(ProgressText),
            nameof(ProgressStage),
            nameof(ReferenceCompleted),
            nameof(ReferenceTotal),
            nameof(RowCompleted),
            nameof(RowTotal),
            nameof(ReservedFinalPath),
            nameof(PartialPath),
            nameof(StageText),
            nameof(DurableProgressText),
            nameof(OutputIdentityText));
    }

    private void Revalidate()
    {
        Dictionary<string, ExecutionTechnicalError> errors = new(StringComparer.Ordinal);
        if (definition is null || workbookMetadata is null || string.IsNullOrWhiteSpace(inputPath))
        {
            AddError(errors, new ExecutionTechnicalError(
                "EXECUTION_CONFIGURATION_REQUIRED",
                "Input / Design",
                "入力 workbook と有効な定量化設計が必要です。"));
        }
        else
        {
            try
            {
                _ = Path.GetFullPath(inputPath);
            }
            catch (Exception exception) when (exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
                AddError(errors, new ExecutionTechnicalError(
                    "INPUT_PATH_INVALID",
                    "Input",
                    "入力 workbook path を確認してください。"));
            }

            DefinitionValidationResult definitionResult = definitionValidator.Validate(definition);
            foreach (DefinitionValidationError error in definitionResult.Errors)
            {
                AddError(errors, new ExecutionTechnicalError(
                    error.Code,
                    error.Field,
                    "定量化設計の技術的な設定を修正してください。"));
            }

            ColumnMappingValidationResult mappingResult = mappingValidator.Validate(
                workbookMetadata,
                definition);
            foreach (ColumnMappingValidationError error in mappingResult.Errors)
            {
                AddError(errors, new ExecutionTechnicalError(
                    error.Code,
                    error.Field,
                    "入力 workbook と列 mapping を一致させてください。"));
            }

            if (definitionResult.IsValid && mappingResult.IsValid)
            {
                QuantificationSnapshot snapshot = QuantificationSnapshot.Create(definition);
                WorkbookExecutionPreflightResult capacity = workbookPreflight.Validate(
                    snapshot,
                    workbookMetadata);
                foreach (ExecutionCapacityError error in capacity.Errors)
                {
                    AddError(errors, CapacityTechnicalError(error));
                }
            }
        }

        if (PlannedEvaluationCount > int.MaxValue)
        {
            AddError(errors, new ExecutionTechnicalError(
                "EVALUATION_PLAN_TOO_LARGE",
                "Evaluation plan",
                "評価単位数が実行可能な上限を超えています。"));
        }

            if (WorstCaseAttemptCount > EvaluationRequestCapacityValidator.MaximumWorstCaseAttemptsPerRun)
            {
                AddError(errors, new ExecutionTechnicalError(
                "ATTEMPT_BUDGET_TOO_LARGE",
                "Evaluation plan",
                $"retry込み最大attempt数 {WorstCaseAttemptCount.ToString(CultureInfo.InvariantCulture)} がrun上限 {EvaluationRequestCapacityValidator.MaximumWorstCaseAttemptsPerRun.ToString(CultureInfo.InvariantCulture)} を超えています。"));
            }

        if (MaxConcurrency is < EvaluationSchedulerOptions.DefaultMaxConcurrency
            or > EvaluationSchedulerOptions.MaximumMaxConcurrency)
        {
            AddError(errors, new ExecutionTechnicalError(
                "CONCURRENCY_OUT_OF_RANGE",
                "Concurrency",
                "並列度は 1～3 にしてください。"));
        }

        if (IsResumeMode)
        {
            try
            {
                string resumePath = Path.GetFullPath(ResumePartialPath);
                if (!resumePath.EndsWith(".partial.xlsx", StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(resumePath))
                {
                    AddError(errors, new ExecutionTechnicalError(
                        "RESUME_PARTIAL_REQUIRED",
                        "Resume",
                        "既存の .partial.xlsx checkpointを指定してください。"));
                }
            }
            catch (Exception exception) when (exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
                AddError(errors, new ExecutionTechnicalError(
                    "RESUME_PARTIAL_REQUIRED",
                    "Resume",
                    "既存の .partial.xlsx checkpointを指定してください。"));
            }
        }
        else
        {
            try
            {
                string targetDirectory = Path.GetFullPath(OutputDirectory);
                if (File.Exists(targetDirectory))
                {
                    AddError(errors, new ExecutionTechnicalError(
                        "OUTPUT_DIRECTORY_INVALID",
                        "Output directory",
                        "既存directoryまたは作成可能なdirectoryを指定してください。"));
                }
            }
            catch (Exception exception) when (exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
                AddError(errors, new ExecutionTechnicalError(
                    "OUTPUT_DIRECTORY_INVALID",
                    "Output directory",
                    "既存directoryまたは作成可能なdirectoryを指定してください。"));
            }
        }

        switch (AuthenticationState)
        {
            case ExecutionAuthenticationState.NotChecked:
                AddError(errors, new ExecutionTechnicalError(
                    "AUTH_CHECK_REQUIRED",
                    "Copilot",
                    "既存 Copilot CLI login の状態を確認してください。"));
                break;
            case ExecutionAuthenticationState.Available when runtimeIdentity is null:
                AddError(errors, new ExecutionTechnicalError(
                    "RUNTIME_IDENTITY_REQUIRED",
                    "Copilot",
                    "安全な CLI / SDK identity を確認できません。"));
                break;
            case ExecutionAuthenticationState.Available when modelItems.Count == 0:
                AddError(errors, new ExecutionTechnicalError(
                    "MODEL_REQUIRED",
                    "Model",
                    "利用可能な model がありません。"));
                break;
            case ExecutionAuthenticationState.Available when SelectedModelId is null
                || !modelItems.Contains(SelectedModelId):
                AddError(errors, new ExecutionTechnicalError(
                    "MODEL_SELECTION_REQUIRED",
                    "Model",
                    "利用可能な model を選択してください。"));
                break;
            case ExecutionAuthenticationState.Available when !modelsById.TryGetValue(
                    SelectedModelId,
                    out CopilotModelAvailability? selectedModel)
                || selectedModel.EffectivePromptTokenLimit is null:
                AddError(errors, new ExecutionTechnicalError(
                    "MODEL_PROMPT_LIMIT_UNAVAILABLE",
                    "Model",
                    "SDKからmodelのprompt上限を取得できないため、安全なrequest preflightを実行できません。"));
                break;
            case ExecutionAuthenticationState.AuthRequired:
                AddError(errors, new ExecutionTechnicalError(
                    ResultsStatusCodes.AuthRequired,
                    "Copilot",
                    "Copilot CLI で login してから再確認してください。"));
                break;
            case ExecutionAuthenticationState.CliUnavailable:
                AddError(errors, new ExecutionTechnicalError(
                    "COPILOT_CLI_UNAVAILABLE",
                    "Copilot",
                    "Copilot CLI の導入状態と PATH を確認してください。"));
                break;
            case ExecutionAuthenticationState.RuntimeFailed:
                AddError(errors, new ExecutionTechnicalError(
                    "COPILOT_RUNTIME_FAILED",
                    "Copilot",
                    "Copilot runtime を安全に確認できませんでした。"));
                break;
        }

        if (runtimeErrorCode is not null)
        {
            AddError(errors, new ExecutionTechnicalError(
                runtimeErrorCode,
                "Run",
                RunFailureMessage(runtimeErrorCode)));
        }

            foreach (ExecutionTechnicalError error in runPreflightErrors)
            {
                AddError(errors, error);
            }

        technicalErrorItems.Clear();
        foreach (ExecutionTechnicalError error in errors.Values)
        {
            technicalErrorItems.Add(error);
        }

        OnPropertiesChanged(
            nameof(HasTechnicalErrors),
            nameof(IsTechnicallyValid),
            nameof(ValidationSummary),
            nameof(CanStart));
        RaiseCommandStates();
    }

    private static void AddError(
        IDictionary<string, ExecutionTechnicalError> errors,
        ExecutionTechnicalError error) =>
        errors.TryAdd($"{error.Code}|{error.Field}", error);

    private static ExecutionTechnicalError CapacityTechnicalError(
        ExecutionCapacityError error) =>
        new(
            error.Code,
            error.Field,
            $"実測値 {error.ActualDimension} は上限 {error.Limit} を満たしません。定量化設計を縮小してください。");

    private static string RunFailureMessage(string code) => code switch
    {
        "INPUT_SNAPSHOT_FAILED" => "入力 workbook の immutable identity を取得できませんでした。",
        "MAPPING_INVALID" => "入力 workbook と列 mapping が一致しません。",
        "DEFINITION_INVALID" => "定量化設計に技術的な問題があります。",
        CheckpointAdmissionStatusCodes.InputMismatch => "checkpointと現在の入力identityが一致しません。",
        CheckpointAdmissionStatusCodes.DefinitionMismatch => "checkpointと現在の定量化設計が一致しません。",
        CheckpointAdmissionStatusCodes.ModelMismatch => "checkpointと選択modelが一致しません。",
        CheckpointAdmissionStatusCodes.RuntimeMismatch => "checkpointと現在のCLI/SDK runtime identityが一致しません。",
        CheckpointStatusCodes.Invalid or CheckpointStatusCodes.HashMismatch or CheckpointStatusCodes.SchemaUnsupported =>
            "checkpointをclosed validationできないため再開しませんでした。",
        CheckpointStatusCodes.SaveFailed => "checkpointを安全に保存できませんでした。",
        "RUN_CANCELLED_WITHOUT_SUMMARY" => "部分結果を確定する前に実行が中断されました。再実行してください。",
        _ => "実行を安全に完了できませんでした。状態を確認して再実行してください。",
    };

    private void RaiseCommandStates()
    {
        checkAuthenticationCommand.RaiseCanExecuteChanged();
        startCommand.RaiseCanExecuteChanged();
        cancelCommand.RaiseCanExecuteChanged();
    }
}
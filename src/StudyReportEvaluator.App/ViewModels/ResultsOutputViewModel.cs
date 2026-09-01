using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Input;
using DocumentFormat.OpenXml.Packaging;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.App.Workbooks.Validation;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Scoring;

namespace StudyReportEvaluator.App.ViewModels;

public static class ResultsOutputStatusCodes
{
    public const string Ready = "READY";
    public const string Success = AtomicOutputStatusCodes.Success;
    public const string InputChanged = AtomicOutputStatusCodes.InputChanged;
    public const string OutputInvalid = AtomicOutputStatusCodes.OutputInvalid;
    public const string TargetExists = AtomicOutputStatusCodes.TargetExists;
    public const string CommitFailed = AtomicOutputStatusCodes.CommitFailed;
    public const string Cancelled = AtomicOutputStatusCodes.Cancelled;
    public const string CleanupFailed = AtomicOutputStatusCodes.CleanupFailed;
    public const string OverrideInvalid = "OVERRIDE_INVALID";
    public const string OutputPathInvalid = "OUTPUT_PATH_INVALID";
    public const string ExportFailed = "EXPORT_FAILED";
}

public sealed class ResultsOutputPathAssessment
{
    public ResultsOutputPathAssessment(string code, bool isValid, bool targetExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (isValid && targetExists)
        {
            throw new ArgumentException("An existing target is not a valid output destination.");
        }

        Code = code;
        IsValid = isValid;
        TargetExists = targetExists;
    }

    public string Code { get; }

    public bool IsValid { get; }

    public bool TargetExists { get; }

    public static ResultsOutputPathAssessment Valid { get; } =
        new(ResultsOutputStatusCodes.Ready, isValid: true, targetExists: false);

    public override string ToString() =>
        $"{nameof(ResultsOutputPathAssessment)} {{ Code = {Code}, IsValid = {IsValid}, TargetExists = {TargetExists}, Content = <redacted> }}";
}

public sealed class ResultsOutputRequest
{
    public ResultsOutputRequest(
        ExecutionRunContext context,
        string outputPath,
        IEnumerable<RunCriterionOverride>? overrides = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        OutputPath = outputPath;
        Overrides = (overrides ?? []).ToImmutableArray();
        if (Overrides.Any(item => item is null))
        {
            throw new ArgumentException("Overrides cannot contain null values.", nameof(overrides));
        }
    }

    public ExecutionRunContext Context { get; }

    public string OutputPath { get; }

    public ImmutableArray<RunCriterionOverride> Overrides { get; }

    public override string ToString() =>
        $"{nameof(ResultsOutputRequest)} {{ OverrideCount = {Overrides.Length.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";
}

public sealed class ResultsOutputResult
{
    public ResultsOutputResult(string code, string? finalPath = null, string? causeCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        FinalPath = finalPath;
        CauseCode = causeCode;
    }

    public string Code { get; }

    public string? FinalPath { get; }

    public string? CauseCode { get; }

    public bool IsSuccess => string.Equals(
        Code,
        ResultsOutputStatusCodes.Success,
        StringComparison.Ordinal);

    public override string ToString() =>
        $"{nameof(ResultsOutputResult)} {{ Code = {Code}, CauseCode = {CauseCode ?? "<none>"}, Content = <redacted> }}";
}

public interface IResultsOutputBoundary
{
    ResultsOutputPathAssessment AssessPath(string inputPath, string outputPath);

    Task<ResultsOutputResult> ExportAsync(
        ResultsOutputRequest request,
        CancellationToken cancellationToken);
}

public sealed class ResultsOutputBoundary : IResultsOutputBoundary
{
    private readonly AtomicOutputCommitter committer;

    public ResultsOutputBoundary()
        : this(new AtomicOutputCommitter())
    {
    }

    public ResultsOutputBoundary(AtomicOutputCommitter committer)
    {
        this.committer = committer ?? throw new ArgumentNullException(nameof(committer));
    }

    public ResultsOutputPathAssessment AssessPath(string inputPath, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            return InvalidPath();
        }

        try
        {
            string canonicalInput = Path.GetFullPath(inputPath);
            string canonicalOutput = Path.GetFullPath(outputPath);
            if (!string.Equals(Path.GetExtension(canonicalOutput), ".xlsx", StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    canonicalInput,
                    canonicalOutput,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal))
            {
                return InvalidPath();
            }

            string? targetDirectory = Path.GetDirectoryName(canonicalOutput);
            if (targetDirectory is null || !Directory.Exists(targetDirectory))
            {
                return InvalidPath();
            }

            if (File.Exists(canonicalOutput) || Directory.Exists(canonicalOutput))
            {
                return new ResultsOutputPathAssessment(
                    ResultsOutputStatusCodes.TargetExists,
                    isValid: false,
                    targetExists: true);
            }

            return ResultsOutputPathAssessment.Valid;
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException
            or IOException
            or UnauthorizedAccessException)
        {
            return InvalidPath();
        }
    }

    public Task<ResultsOutputResult> ExportAsync(
        ResultsOutputRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(
            () => ExportCore(request, cancellationToken),
            CancellationToken.None);
    }

    public override string ToString() =>
        $"{nameof(ResultsOutputBoundary)} {{ Content = <redacted> }}";

    private ResultsOutputResult ExportCore(
        ResultsOutputRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunSummary summary = request.Context.Summary;
            RunOutputPreparation preparation = summary.PrepareOutput(request.Overrides);
            if (!preparation.InputUnchanged)
            {
                return new ResultsOutputResult(ResultsOutputStatusCodes.InputChanged);
            }

            if (!preparation.OverrideErrors.IsEmpty)
            {
                return new ResultsOutputResult(ResultsOutputStatusCodes.OverrideInvalid);
            }

            ResultsOutputPathAssessment path = AssessPath(
                request.Context.InputPath,
                request.OutputPath);
            if (!path.IsValid)
            {
                return new ResultsOutputResult(path.Code);
            }

            using WorkingPackage package = WorkingPackage.Create(
                request.Context.InputPath,
                request.OutputPath);
            cancellationToken.ThrowIfCancellationRequested();
            AppOwnedSheetNames sheetNames;
            ResultsSheetWriteResult results;
            using (SpreadsheetDocument document = package.OpenForEditing())
            {
                sheetNames = new AppOwnedSheetNameResolver().Resolve(document);
                ConfigCellAddressMap config = new ConfigSheetWriter().Write(
                    document,
                    preparation.Snapshot,
                    sheetNames);
                results = new ResultsSheetWriter().Write(
                    document,
                    preparation.Snapshot,
                    sheetNames,
                    config,
                    preparation.Rows);
                new RunSheetWriter().Write(
                    document,
                    CreateRunMetadata(request.Context, sheetNames));
                new CalculationPropertiesWriter().Write(document);
            }

            cancellationToken.ThrowIfCancellationRequested();
            ImmutableArray<ExpectedFormulaCell> expectedFormulas = results.FormulaCells
                .Select(item => new ExpectedFormulaCell(item.Definition, item.CachedValue))
                .ToImmutableArray();
            OutputPackageValidationPlan validationPlan = OutputPackageValidationPlan.Capture(
                request.Context.InputPath,
                sheetNames,
                expectedFormulas);
            AtomicOutputCommitResult commit = committer.Commit(
                package,
                request.OutputPath,
                request.Context.InputPath,
                preparation.InputSnapshot,
                validationPlan,
                cancellationToken);
            return new ResultsOutputResult(
                commit.Code,
                commit.FinalPath,
                commit.CauseCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ResultsOutputResult(ResultsOutputStatusCodes.Cancelled);
        }
        catch
        {
            return new ResultsOutputResult(ResultsOutputStatusCodes.ExportFailed);
        }
    }

    private static RunSheetMetadata CreateRunMetadata(
        ExecutionRunContext context,
        AppOwnedSheetNames sheetNames)
    {
        RunSummary summary = context.Summary;
        return new RunSheetMetadata
        {
            InputIdentity = summary.InputSnapshot,
            DefinitionSha256 = summary.DefinitionSha256,
            ApplicationIdentity = ApplicationIdentity(),
            CopilotSdkIdentity = "GitHub.Copilot.SDK/" + context.RuntimeIdentity.SdkInformationalVersion,
            CopilotCliIdentity = "copilot/" + context.RuntimeIdentity.CliVersion
                + ";sha256=" + context.RuntimeIdentity.CliSha256,
            ModelIdentity = context.ModelId,
            StartedAtUtc = summary.StartedAtUtc,
            EndedAtUtc = summary.EndedAtUtc,
            PlannedEvaluationCount = summary.PlannedEvaluationCount,
            CompletedEvaluationCount = summary.CompletedEvaluationCount,
            ErrorCount = summary.FailureCount,
            SheetNames = sheetNames,
        };
    }

    private static string ApplicationIdentity()
    {
        Assembly assembly = typeof(ResultsOutputBoundary).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        string version = informationalVersion?.Split('+', 2)[0]
            ?? assembly.GetName().Version?.ToString(3)
            ?? "unknown";
        return (assembly.GetName().Name ?? "StudyReportEvaluator.App") + "/" + version;
    }

    private static ResultsOutputPathAssessment InvalidPath() =>
        new(
            ResultsOutputStatusCodes.OutputPathInvalid,
            isValid: false,
            targetExists: false);
}

public sealed class ResultsCriterionViewModel : UiObservableObject
{
    private readonly Action<ResultsCriterionViewModel> overrideChanged;
    private string overrideText = string.Empty;
    private string? overrideError;
    private decimal? effectiveRaw;
    private decimal? normalizedScore;
    private decimal? evaluatorScore;
    private decimal? questionScore;
    private decimal? overallScore;

    internal ResultsCriterionViewModel(
        int sourceRowNumber,
        QuestionDefinition question,
        EvaluatorDefinition evaluator,
        CriterionDefinition criterion,
        bool canOverride,
        decimal? aiRawScore,
        string statusCode,
        Action<ResultsCriterionViewModel> overrideChanged)
    {
        if (sourceRowNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRowNumber));
        }

        Question = question ?? throw new ArgumentNullException(nameof(question));
        Evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        Criterion = criterion ?? throw new ArgumentNullException(nameof(criterion));
        ArgumentException.ThrowIfNullOrWhiteSpace(statusCode);
        this.overrideChanged = overrideChanged
            ?? throw new ArgumentNullException(nameof(overrideChanged));
        SourceRowNumber = sourceRowNumber;
        CanOverride = canOverride;
        AiRawScore = aiRawScore;
        StatusCode = statusCode;
        Range = criterion.Range ?? evaluator.Range;
    }

    internal QuestionDefinition Question { get; }

    internal EvaluatorDefinition Evaluator { get; }

    internal CriterionDefinition Criterion { get; }

    public int SourceRowNumber { get; }

    public string QuestionId => Question.Id;

    public string EvaluatorId => Evaluator.Id;

    public string CriterionId => Criterion.Id;

    public string QuestionName => Question.DisplayName;

    public string EvaluatorName => Evaluator.DisplayName;

    public string CriterionName => Criterion.DisplayName;

    public bool CanOverride { get; }

    public decimal? AiRawScore { get; }

    public string AiRawText => FormatScore(AiRawScore);

    public string StatusCode { get; }

    public ScoreRange Range { get; }

    public string RangeText =>
        $"{Range.Minimum.ToString(CultureInfo.InvariantCulture)} ～ {Range.Maximum.ToString(CultureInfo.InvariantCulture)}";

    public string OverrideText
    {
        get => overrideText;
        set
        {
            string next = value ?? string.Empty;
            if (!CanOverride && next.Length > 0)
            {
                return;
            }

            if (SetProperty(ref overrideText, next))
            {
                overrideChanged(this);
            }
        }
    }

    public string? OverrideError
    {
        get => overrideError;
        internal set
        {
            if (SetProperty(ref overrideError, value))
            {
                OnPropertyChanged(nameof(HasOverrideError));
            }
        }
    }

    public bool HasOverrideError => OverrideError is not null;

    public decimal? EffectiveRaw => effectiveRaw;

    public string EffectiveRawText => FormatScore(EffectiveRaw);

    public decimal? NormalizedScore => normalizedScore;

    public string NormalizedScoreText => FormatScore(NormalizedScore);

    public decimal? EvaluatorScore => evaluatorScore;

    public string EvaluatorScoreText => FormatScore(EvaluatorScore);

    public decimal? QuestionScore => questionScore;

    public string QuestionScoreText => FormatScore(QuestionScore);

    public decimal? OverallScore => overallScore;

    public string OverallScoreText => FormatScore(OverallScore);

    public string AutomationId =>
        $"Result-{SourceRowNumber.ToString(CultureInfo.InvariantCulture)}-{QuestionId}-{EvaluatorId}-{CriterionId}";

    public string AccessibleSummary =>
        $"row {SourceRowNumber.ToString(CultureInfo.InvariantCulture)}、{QuestionName}、{EvaluatorName}、{CriterionName}、status {StatusCode}";

    internal void SetPreview(
        decimal? nextEffectiveRaw,
        decimal? nextNormalizedScore,
        decimal? nextEvaluatorScore,
        decimal? nextQuestionScore,
        decimal? nextOverallScore)
    {
        SetPreviewProperty(ref effectiveRaw, nextEffectiveRaw, nameof(EffectiveRaw), nameof(EffectiveRawText));
        SetPreviewProperty(ref normalizedScore, nextNormalizedScore, nameof(NormalizedScore), nameof(NormalizedScoreText));
        SetPreviewProperty(ref evaluatorScore, nextEvaluatorScore, nameof(EvaluatorScore), nameof(EvaluatorScoreText));
        SetPreviewProperty(ref questionScore, nextQuestionScore, nameof(QuestionScore), nameof(QuestionScoreText));
        SetPreviewProperty(ref overallScore, nextOverallScore, nameof(OverallScore), nameof(OverallScoreText));
    }

    public override string ToString() =>
        $"{nameof(ResultsCriterionViewModel)} {{ SourceRowNumber = {SourceRowNumber.ToString(CultureInfo.InvariantCulture)}, StatusCode = {StatusCode}, Content = <redacted> }}";

    private void SetPreviewProperty(
        ref decimal? field,
        decimal? value,
        string propertyName,
        string textPropertyName)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            OnPropertyChanged(textPropertyName);
        }
    }

    private static string FormatScore(decimal? value) => value is decimal score
        ? score.ToString(CultureInfo.InvariantCulture)
        : "—";
}

public sealed class ResultsOutputViewModel : UiObservableObject, IDisposable
{
    private const NumberStyles OverrideNumberStyles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    private readonly IResultsOutputBoundary outputBoundary;
    private readonly WeightedScoreCalculator scoreCalculator = new();
    private readonly ObservableCollection<ResultsCriterionViewModel> resultItems = [];
    private readonly ViewModelCommand exportCommand;
    private readonly ViewModelCommand cancelExportCommand;
    private ExecutionRunContext? context;
    private string outputPath = string.Empty;
    private ResultsOutputPathAssessment pathAssessment = InvalidInitialPath();
    private bool hasOverrideErrors;
    private bool isExporting;
    private bool isExportCancelling;
    private string lastExportCode = ResultsOutputStatusCodes.Ready;
    private CancellationTokenSource? exportCancellation;
    private long exportSequence;
    private bool disposed;

    public ResultsOutputViewModel()
        : this(new ResultsOutputBoundary())
    {
    }

    public ResultsOutputViewModel(
        IResultsOutputBoundary outputBoundary,
        ExecutionRunContext? context = null)
    {
        this.outputBoundary = outputBoundary
            ?? throw new ArgumentNullException(nameof(outputBoundary));
        Results = new ReadOnlyObservableCollection<ResultsCriterionViewModel>(resultItems);
        exportCommand = new ViewModelCommand(
            _ => _ = ExportAsync(),
            _ => CanExport);
        cancelExportCommand = new ViewModelCommand(
            _ => CancelExport(),
            _ => CanCancelExport);
        if (context is not null)
        {
            Load(context);
        }
    }

    public ReadOnlyObservableCollection<ResultsCriterionViewModel> Results { get; }

    public bool IsLoaded => context is not null;

    public bool IsPartial => context?.Summary.IsPartial == true;

    public bool IsInputUnchanged => context?.Summary.IsExportReady == true;

    public int PlannedEvaluationCount => context?.Summary.PlannedEvaluationCount ?? 0;

    public int CompletedEvaluationCount => context?.Summary.CompletedEvaluationCount ?? 0;

    public int FailureCount => context?.Summary.FailureCount ?? 0;

    public int CancelledCount => context?.Summary.CancelledCount ?? 0;

    public string RunSummaryText => context is null
        ? "完了または cancel 済みの run はありません。"
        : $"{CompletedEvaluationCount.ToString("N0", CultureInfo.InvariantCulture)} / {PlannedEvaluationCount.ToString("N0", CultureInfo.InvariantCulture)} 完了 · error {FailureCount.ToString("N0", CultureInfo.InvariantCulture)} · cancelled {CancelledCount.ToString("N0", CultureInfo.InvariantCulture)}";

    public string PartialResultText => IsPartial
        ? "部分結果です。完了済み unit だけを保持し、未完了値は空欄のまま出力できます。"
        : "全評価 unit の実行が終了しています。";

    public string InputStateText => IsInputUnchanged
        ? "run 終了時の exact hash / size / mtime は一致しています。final commit 直前にも再確認します。"
        : "run 終了時に入力変更を検出しました。出力は停止されています。";

    public string OutputPath
    {
        get => outputPath;
        set
        {
            string next = value ?? string.Empty;
            if (SetProperty(ref outputPath, next))
            {
                LastExportCode = ResultsOutputStatusCodes.Ready;
                RefreshPathAssessment();
            }
        }
    }

    public bool HasOverrideErrors
    {
        get => hasOverrideErrors;
        private set
        {
            if (SetProperty(ref hasOverrideErrors, value))
            {
                OnPropertiesChanged(
                    nameof(OverrideValidationText),
                    nameof(CanExport));
                RaiseCommandStates();
            }
        }
    }

    public string OverrideValidationText => HasOverrideErrors
        ? "override の技術検証エラーを修正してください。出力は停止されています。"
        : "override は snapshot の scorable flag と effective range に適合しています。";

    public bool IsOutputPathValid => pathAssessment.IsValid;

    public bool OutputTargetExists => pathAssessment.TargetExists;

    public string OutputPathStatusText => pathAssessment.Code switch
    {
        ResultsOutputStatusCodes.Ready => "新規 .xlsx として atomic commit できます。",
        ResultsOutputStatusCodes.TargetExists => "完成名が既に存在します。上書きせず、別の名前を指定してください。",
        _ => "既存 directory 内の、入力とは異なる新規 .xlsx path を指定してください。",
    };

    public bool IsExporting
    {
        get => isExporting;
        private set
        {
            if (SetProperty(ref isExporting, value))
            {
                OnPropertiesChanged(
                    nameof(CanExport),
                    nameof(CanCancelExport),
                    nameof(ExportStatusText));
                RaiseCommandStates();
            }
        }
    }

    public bool IsExportCancelling
    {
        get => isExportCancelling;
        private set
        {
            if (SetProperty(ref isExportCancelling, value))
            {
                OnPropertiesChanged(nameof(CanCancelExport), nameof(ExportStatusText));
                RaiseCommandStates();
            }
        }
    }

    public string LastExportCode
    {
        get => lastExportCode;
        private set
        {
            if (SetProperty(ref lastExportCode, value))
            {
                OnPropertyChanged(nameof(ExportStatusText));
            }
        }
    }

    public string ExportStatusText
    {
        get
        {
            if (IsExportCancelling)
            {
                return "cancel を受け付けました。atomic rename の安全点を待っています。";
            }

            if (IsExporting)
            {
                return "working copy を write・flush・close・reopen・validate しています…";
            }

            return LastExportCode switch
            {
                ResultsOutputStatusCodes.Ready => "出力準備を確認してください。",
                ResultsOutputStatusCodes.Success => "検証済み workbook を完成名へ atomic commit しました。",
                ResultsOutputStatusCodes.InputChanged => "入力変更を検出したため、完成名は作成していません。",
                ResultsOutputStatusCodes.OutputInvalid => "再オープン検証に失敗したため、完成名は作成していません。",
                ResultsOutputStatusCodes.TargetExists => "完成名が既に存在するため、上書きしていません。",
                ResultsOutputStatusCodes.OverrideInvalid => "override が無効なため、出力していません。",
                ResultsOutputStatusCodes.Cancelled => "final commit 前に取り消しました。完成名は作成していません。",
                ResultsOutputStatusCodes.CleanupFailed => "一時 workbook の cleanup に失敗しました。状態を確認してください。",
                _ => "workbook を安全に出力できませんでした。状態を確認して再実行してください。",
            };
        }
    }

    public bool CanExport => !disposed
        && context is not null
        && context.Summary.PrepareOutput(CreateOverrides()).IsExportReady
        && !HasOverrideErrors
        && pathAssessment.IsValid
        && !IsExporting;

    public bool CanCancelExport => IsExporting && !IsExportCancelling;

    public ICommand ExportCommand => exportCommand;

    public ICommand CancelExportCommand => cancelExportCommand;

    public void Load(ExecutionRunContext runContext)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(runContext);
        Interlocked.Increment(ref exportSequence);
        context = runContext;
        exportCancellation?.Cancel();
        exportCancellation?.Dispose();
        exportCancellation = null;
        IsExporting = false;
        IsExportCancelling = false;
        LastExportCode = ResultsOutputStatusCodes.Ready;
        resultItems.Clear();
        BuildResultItems(runContext.Summary);
        outputPath = CreateDefaultOutputPath(runContext);
        OnPropertiesChanged(
            nameof(IsLoaded),
            nameof(IsPartial),
            nameof(IsInputUnchanged),
            nameof(PlannedEvaluationCount),
            nameof(CompletedEvaluationCount),
            nameof(FailureCount),
            nameof(CancelledCount),
            nameof(RunSummaryText),
            nameof(PartialResultText),
            nameof(InputStateText),
            nameof(OutputPath));
        RevalidateOverridesAndPreview();
        RefreshPathAssessment();
    }

    public async Task ExportAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RevalidateOverridesAndPreview();
        RefreshPathAssessment();
        if (!CanExport || context is null)
        {
            return;
        }

        long sequence = Interlocked.Increment(ref exportSequence);
        ExecutionRunContext exportContext = context;
        string exportPath = OutputPath;
        ImmutableArray<RunCriterionOverride> exportOverrides = CreateOverrides();
        exportCancellation?.Cancel();
        exportCancellation?.Dispose();
        CancellationTokenSource currentCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        exportCancellation = currentCancellation;
        IsExportCancelling = false;
        IsExporting = true;
        LastExportCode = ResultsOutputStatusCodes.Ready;
        try
        {
            ResultsOutputResult result = await outputBoundary.ExportAsync(
                new ResultsOutputRequest(exportContext, exportPath, exportOverrides),
                currentCancellation.Token);
            if (sequence == Volatile.Read(ref exportSequence) && !disposed)
            {
                LastExportCode = result.Code;
            }
        }
        catch (OperationCanceledException) when (currentCancellation.IsCancellationRequested)
        {
            if (sequence == Volatile.Read(ref exportSequence) && !disposed)
            {
                LastExportCode = ResultsOutputStatusCodes.Cancelled;
            }
        }
        catch
        {
            if (sequence == Volatile.Read(ref exportSequence) && !disposed)
            {
                LastExportCode = ResultsOutputStatusCodes.ExportFailed;
            }
        }
        finally
        {
            currentCancellation.Dispose();
            if (sequence == Volatile.Read(ref exportSequence) && !disposed)
            {
                exportCancellation = null;
                IsExporting = false;
                IsExportCancelling = false;
                RefreshPathAssessment();
            }
        }
    }

    public void CancelExport()
    {
        if (!CanCancelExport)
        {
            return;
        }

        IsExportCancelling = true;
        exportCancellation?.Cancel();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Interlocked.Increment(ref exportSequence);
        exportCancellation?.Cancel();
        exportCancellation?.Dispose();
        RaiseCommandStates();
    }

    public override string ToString() =>
        $"{nameof(ResultsOutputViewModel)} {{ IsLoaded = {IsLoaded}, ResultCount = {Results.Count.ToString(CultureInfo.InvariantCulture)}, LastExportCode = {LastExportCode}, Content = <redacted> }}";

    private void BuildResultItems(RunSummary summary)
    {
        foreach (EvaluationUnitResult unit in summary.Units.OrderBy(item => item.Item.SequenceNumber))
        {
            QuestionDefinition question = summary.Snapshot.Definition.Questions.Single(item =>
                item.Enabled
                && string.Equals(item.Id, unit.Item.QuestionId, StringComparison.Ordinal));
            EvaluatorDefinition evaluator = question.Evaluators.Single(item =>
                item.Enabled
                && string.Equals(item.Id, unit.Item.EvaluatorId, StringComparison.Ordinal));
            bool canOverride = QuestionIsScorable(
                summary,
                unit.Item.SourceRowNumber,
                question.Id);
            foreach (CriterionDefinition criterion in evaluator.Criteria.Where(item => item.Enabled))
            {
                CriterionQuantificationResult? criterionResult = unit.AcceptedResult?.Criteria.SingleOrDefault(
                    item => string.Equals(item.CriterionId, criterion.Id, StringComparison.Ordinal));
                resultItems.Add(new ResultsCriterionViewModel(
                    unit.Item.SourceRowNumber,
                    question,
                    evaluator,
                    criterion,
                    canOverride,
                    criterionResult?.RawScore,
                    unit.StatusCode,
                    _ => RevalidateOverridesAndPreview()));
            }
        }
    }

    private void RevalidateOverridesAndPreview()
    {
        if (context is null)
        {
            HasOverrideErrors = false;
            return;
        }

        ImmutableArray<RunCriterionOverride> overrides = CreateOverrides();
        RunOverrideValidationResult validation = context.Summary.ValidateOverrides(overrides);
        Dictionary<ResultKey, RunOverrideValidationError> errors = validation.Errors
            .Where(error => error.SourceRowNumber is not null)
            .GroupBy(error => new ResultKey(
                error.SourceRowNumber!.Value,
                error.QuestionId,
                error.EvaluatorId,
                error.CriterionId))
            .ToDictionary(group => group.Key, group => group.First());
        foreach (ResultsCriterionViewModel item in resultItems)
        {
            ResultKey key = Key(item);
            item.OverrideError = errors.TryGetValue(key, out RunOverrideValidationError? error)
                ? OverrideErrorMessage(error.Code)
                : null;
        }

        HasOverrideErrors = !validation.IsValid;
        RecomputePreview();
        OnPropertyChanged(nameof(CanExport));
        RaiseCommandStates();
    }

    private void RecomputePreview()
    {
        if (context is null)
        {
            return;
        }

        QuantificationDefinition definition = context.Summary.Snapshot.Definition;
        foreach (IGrouping<int, ResultsCriterionViewModel> sourceRow in resultItems.GroupBy(item => item.SourceRowNumber))
        {
            Dictionary<ResultKey, ResultsCriterionViewModel> rowItems = sourceRow.ToDictionary(Key);
            Dictionary<string, decimal?> evaluatorScores = new(StringComparer.Ordinal);
            Dictionary<string, decimal?> questionScores = new(StringComparer.Ordinal);
            foreach (QuestionDefinition question in definition.Questions.Where(item => item.Enabled))
            {
                foreach (EvaluatorDefinition evaluator in question.Evaluators.Where(item => item.Enabled))
                {
                    List<WeightedScoreInput> criterionInputs = [];
                    foreach (CriterionDefinition criterion in evaluator.Criteria.Where(item => item.Enabled))
                    {
                        ResultKey key = new(sourceRow.Key, question.Id, evaluator.Id, criterion.Id);
                        ResultsCriterionViewModel item = rowItems[key];
                        decimal? parsedOverride = ParseValidOverride(item);
                        EffectiveRawSelection effective = item.OverrideText.Length > 0
                            && item.HasOverrideError
                            ? new EffectiveRawSelection(null, EffectiveRawStatus.InvalidOverride)
                            : scoreCalculator.SelectEffectiveRaw(
                                item.CanOverride,
                                item.AiRawScore,
                                parsedOverride,
                                item.Range);
                        decimal? normalized = scoreCalculator.Normalize(
                            effective.Value,
                            item.Range,
                            definition.RoundingDigits);
                        item.SetPreview(effective.Value, normalized, null, null, null);
                        criterionInputs.Add(new WeightedScoreInput(normalized, criterion.Weight));
                    }

                    evaluatorScores[evaluator.Id] = scoreCalculator.Aggregate(
                        criterionInputs,
                        definition.RoundingDigits);
                }

                questionScores[question.Id] = scoreCalculator.Aggregate(
                    question.Evaluators
                        .Where(item => item.Enabled)
                        .Select(evaluator => new WeightedScoreInput(
                            evaluatorScores[evaluator.Id],
                            evaluator.Weight)),
                    definition.RoundingDigits);
            }

            decimal? overall = scoreCalculator.Aggregate(
                definition.Questions
                    .Where(item => item.Enabled)
                    .Select(question => new WeightedScoreInput(
                        questionScores[question.Id],
                        question.Weight)),
                definition.RoundingDigits);
            foreach (ResultsCriterionViewModel item in sourceRow)
            {
                item.SetPreview(
                    item.EffectiveRaw,
                    item.NormalizedScore,
                    evaluatorScores[item.EvaluatorId],
                    questionScores[item.QuestionId],
                    overall);
            }
        }
    }

    private void RefreshPathAssessment()
    {
        pathAssessment = context is null
            ? InvalidInitialPath()
            : SafeAssessPath(context.InputPath, OutputPath);
        OnPropertiesChanged(
            nameof(IsOutputPathValid),
            nameof(OutputTargetExists),
            nameof(OutputPathStatusText),
            nameof(CanExport));
        RaiseCommandStates();
    }

    private ResultsOutputPathAssessment SafeAssessPath(string inputPath, string candidateOutputPath)
    {
        try
        {
            return outputBoundary.AssessPath(inputPath, candidateOutputPath)
                ?? InvalidInitialPath();
        }
        catch
        {
            return InvalidInitialPath();
        }
    }

    private ImmutableArray<RunCriterionOverride> CreateOverrides() => resultItems
        .Where(item => item.OverrideText.Length > 0)
        .Select(item => new RunCriterionOverride
        {
            SourceRowNumber = item.SourceRowNumber,
            QuestionId = item.QuestionId,
            EvaluatorId = item.EvaluatorId,
            CriterionId = item.CriterionId,
            Value = item.OverrideText,
        })
        .ToImmutableArray();

    private static bool QuestionIsScorable(
        RunSummary summary,
        int sourceRowNumber,
        string questionId)
    {
        bool? known = null;
        foreach (EvaluationUnitResult unit in summary.Units.Where(item =>
                     item.Item.SourceRowNumber == sourceRowNumber
                     && string.Equals(item.Item.QuestionId, questionId, StringComparison.Ordinal)
                     && item.ScorableKnown))
        {
            known ??= unit.Scorable;
            if (known.Value != unit.Scorable)
            {
                return false;
            }
        }

        return known ?? false;
    }

    private static decimal? ParseValidOverride(ResultsCriterionViewModel item)
    {
        if (item.OverrideText.Length == 0 || item.HasOverrideError)
        {
            return null;
        }

        return decimal.TryParse(
            item.OverrideText,
            OverrideNumberStyles,
            CultureInfo.InvariantCulture,
            out decimal value)
            ? value
            : null;
    }

    private static string OverrideErrorMessage(string code) => code switch
    {
        "OVERRIDE_NOT_NUMERIC" => "数値を入力するか空欄にしてください。",
        "OVERRIDE_OUT_OF_RANGE" => "snapshot の effective range 内で入力してください。",
        "OVERRIDE_NOT_ALLOWED" => "主回答が空欄または未完了のため override できません。",
        _ => "この override は技術検証を通過できません。",
    };

    private static string CreateDefaultOutputPath(ExecutionRunContext runContext)
    {
        string directory = Path.GetDirectoryName(runContext.InputPath) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(runContext.InputPath);
        string timestamp = runContext.Summary.EndedAtUtc.ToLocalTime()
            .ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(directory, $"{stem}_quantified_{timestamp}.xlsx");
    }

    private static ResultKey Key(ResultsCriterionViewModel item) =>
        new(
            item.SourceRowNumber,
            item.QuestionId,
            item.EvaluatorId,
            item.CriterionId);

    private static ResultsOutputPathAssessment InvalidInitialPath() =>
        new(
            ResultsOutputStatusCodes.OutputPathInvalid,
            isValid: false,
            targetExists: false);

    private void RaiseCommandStates()
    {
        exportCommand.RaiseCanExecuteChanged();
        cancelExportCommand.RaiseCanExecuteChanged();
    }

    private readonly record struct ResultKey(
        int SourceRowNumber,
        string QuestionId,
        string EvaluatorId,
        string CriterionId);
}
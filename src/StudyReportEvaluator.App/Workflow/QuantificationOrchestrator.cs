using System.Collections.Immutable;
using System.Globalization;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Mapping;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.Core.Domain;

namespace StudyReportEvaluator.App.Workflow;

public interface IInputSnapshotBoundary
{
    InputSnapshot Capture(string inputPath);

    bool IsUnchanged(string inputPath, InputSnapshot expected);
}

public sealed class PhysicalInputSnapshotBoundary : IInputSnapshotBoundary
{
    private readonly InputSnapshotService service = new();

    public InputSnapshot Capture(string inputPath) => service.Capture(inputPath);

    public bool IsUnchanged(string inputPath, InputSnapshot expected) =>
        service.Recheck(inputPath, expected).IsMatch;

    public override string ToString() =>
        $"{nameof(PhysicalInputSnapshotBoundary)} {{ Content = <redacted> }}";
}

public sealed class QuantificationMappingValidationException : Exception
{
    public QuantificationMappingValidationException(
        IEnumerable<ColumnMappingValidationError> errors)
        : this(errors?.ToImmutableArray()
            ?? throw new ArgumentNullException(nameof(errors)))
    {
    }

    private QuantificationMappingValidationException(
        ImmutableArray<ColumnMappingValidationError> errors)
        : base($"The quantification mapping has {errors.Length.ToString(CultureInfo.InvariantCulture)} validation error(s).")
    {
        Errors = errors;
    }

    public ImmutableArray<ColumnMappingValidationError> Errors { get; }

    public override string ToString() =>
        $"{nameof(QuantificationMappingValidationException)} {{ ErrorCount = {Errors.Length.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";
}

public sealed class QuantificationRunException : Exception
{
    public QuantificationRunException(string code)
        : base("The quantification run could not be started.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }

    public override string ToString() =>
        $"{nameof(QuantificationRunException)} {{ Code = {Code}, Content = <redacted> }}";
}

public sealed record QuantificationRunRequest
{
    public required QuantificationDefinition DraftDefinition { get; init; }

    public required WorkbookMetadata WorkbookMetadata { get; init; }

    public required string InputPath { get; init; }

    public required string ModelId { get; init; }

    public int MaxConcurrency { get; init; } = EvaluationSchedulerOptions.DefaultMaxConcurrency;

    public override string ToString() =>
        $"{nameof(QuantificationRunRequest)} {{ MaxConcurrency = {MaxConcurrency.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";
}

public sealed class QuantificationOrchestrator
{
    private readonly IInputSnapshotBoundary inputSnapshots;
    private readonly EvaluationPlanBuilder planBuilder;
    private readonly ColumnMappingValidator mappingValidator;
    private readonly EvaluationScheduler scheduler;
    private readonly TimeProvider timeProvider;

    public QuantificationOrchestrator(
        IEvaluationRowSource rowSource,
        IEvaluationRunner runner)
        : this(
            rowSource,
            runner,
            new PhysicalInputSnapshotBoundary(),
            TimeProvider.System)
    {
    }

    public QuantificationOrchestrator(
        IEvaluationRowSource rowSource,
        IEvaluationRunner runner,
        IInputSnapshotBoundary inputSnapshots,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(rowSource);
        ArgumentNullException.ThrowIfNull(runner);
        this.inputSnapshots = inputSnapshots
            ?? throw new ArgumentNullException(nameof(inputSnapshots));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        planBuilder = new EvaluationPlanBuilder();
        mappingValidator = new ColumnMappingValidator();
        scheduler = new EvaluationScheduler(rowSource, runner);
    }

    public async Task<RunSummary> RunAsync(
        QuantificationRunRequest request,
        Action<EvaluationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.DraftDefinition);
        ArgumentNullException.ThrowIfNull(request.WorkbookMetadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModelId);
        _ = new EvaluationSchedulerOptions(request.MaxConcurrency);

        DateTimeOffset startedAtUtc = timeProvider.GetUtcNow();

        // The draft is read exactly once. Every downstream contract receives this frozen instance.
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(request.DraftDefinition);
        ColumnMappingValidationResult mappingValidation = mappingValidator.Validate(
            request.WorkbookMetadata,
            snapshot.Definition);
        if (!mappingValidation.IsValid || mappingValidation.Mapping is null)
        {
            throw new QuantificationMappingValidationException(mappingValidation.Errors);
        }

        InputSnapshot inputSnapshot;
        try
        {
            inputSnapshot = inputSnapshots.Capture(request.InputPath)
                ?? throw new InvalidOperationException("The input snapshot boundary returned no snapshot.");
        }
        catch (QuantificationRunException)
        {
            throw;
        }
        catch
        {
            throw new QuantificationRunException("INPUT_SNAPSHOT_FAILED");
        }

        EvaluationPlan plan = planBuilder.Build(snapshot, mappingValidation.Mapping);
        EvaluationScheduleResult schedule = await scheduler
            .RunAsync(
                plan,
                request.ModelId,
                request.MaxConcurrency,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        bool inputUnchanged;
        try
        {
            inputUnchanged = inputSnapshots.IsUnchanged(
                request.InputPath,
                inputSnapshot);
        }
        catch
        {
            inputUnchanged = false;
        }

        string statusCode = !inputUnchanged
            ? QuantificationRunStatusCodes.InputChanged
            : schedule.IsCancelled
                ? QuantificationRunStatusCodes.Cancelled
                : QuantificationRunStatusCodes.Success;
        DateTimeOffset endedAtUtc = timeProvider.GetUtcNow();
        if (endedAtUtc < startedAtUtc)
        {
            endedAtUtc = startedAtUtc;
        }

        return new RunSummary(
            schedule,
            inputSnapshot,
            statusCode,
            startedAtUtc,
            endedAtUtc);
    }

    public override string ToString() =>
        $"{nameof(QuantificationOrchestrator)} {{ Content = <redacted> }}";
}

using System.Collections.Immutable;
using System.Globalization;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Mapping;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;

namespace StudyReportEvaluator.App.Workflow;

public static class QuantificationRunStatusCodes
{
    public const string Success = "SUCCESS";
    public const string Cancelled = "CANCELLED";
    public const string InputChanged = "INPUT_CHANGED";
}

public sealed record RunCriterionOverride
{
    public int SourceRowNumber { get; init; }

    public required string QuestionId { get; init; }

    public required string EvaluatorId { get; init; }

    public required string CriterionId { get; init; }

    public string? Value { get; init; }

    public override string ToString() =>
        $"{nameof(RunCriterionOverride)} {{ SourceRowNumber = {SourceRowNumber.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";
}

public sealed record RunOverrideValidationError(
    string Code,
    int? SourceRowNumber,
    string QuestionId,
    string EvaluatorId,
    string CriterionId,
    string Field,
    string SafeOffendingValue)
{
    public override string ToString() =>
        $"{nameof(RunOverrideValidationError)} {{ Code = {Code}, SourceRowNumber = {SourceRowNumber?.ToString(CultureInfo.InvariantCulture) ?? "<none>"}, Field = {Field}, Content = <redacted> }}";
}

public sealed class RunOverrideValidationResult
{
    internal RunOverrideValidationResult(
        ImmutableArray<RunOverrideValidationError> errors,
        ImmutableDictionary<RunOverrideKey, string> validatedValues)
    {
        Errors = errors;
        ValidatedValues = validatedValues;
    }

    public ImmutableArray<RunOverrideValidationError> Errors { get; }

    public bool IsValid => Errors.IsEmpty;

    internal ImmutableDictionary<RunOverrideKey, string> ValidatedValues { get; }

    public override string ToString() =>
        $"{nameof(RunOverrideValidationResult)} {{ IsValid = {IsValid}, ErrorCount = {Errors.Length.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";
}

public sealed class RunOutputPreparation
{
    internal RunOutputPreparation(
        QuantificationSnapshot snapshot,
        InputSnapshot inputSnapshot,
        string runStatusCode,
        bool isPartial,
        ImmutableArray<ResultsSheetRowInput> rows,
        ImmutableArray<RunOverrideValidationError> overrideErrors,
        bool inputUnchanged)
    {
        Snapshot = snapshot;
        InputSnapshot = inputSnapshot;
        RunStatusCode = runStatusCode;
        IsPartial = isPartial;
        Rows = rows;
        OverrideErrors = overrideErrors;
        InputUnchanged = inputUnchanged;
    }

    public QuantificationSnapshot Snapshot { get; }

    public string DefinitionSha256 => Snapshot.Sha256;

    public InputSnapshot InputSnapshot { get; }

    public string RunStatusCode { get; }

    public bool IsPartial { get; }

    public ImmutableArray<ResultsSheetRowInput> Rows { get; }

    public ImmutableArray<RunOverrideValidationError> OverrideErrors { get; }

    public bool InputUnchanged { get; }

    public bool IsExportReady => InputUnchanged && OverrideErrors.IsEmpty;

    public override string ToString() =>
        $"{nameof(RunOutputPreparation)} {{ IsExportReady = {IsExportReady}, IsPartial = {IsPartial}, RowCount = {Rows.Length.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";
}

public sealed class RunSummary
{
    private const NumberStyles OverrideNumberStyles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    internal RunSummary(
        EvaluationScheduleResult schedule,
        InputSnapshot inputSnapshot,
        string statusCode,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(inputSnapshot);
        if (statusCode is not QuantificationRunStatusCodes.Success
            and not QuantificationRunStatusCodes.Cancelled
            and not QuantificationRunStatusCodes.InputChanged)
        {
            throw new ArgumentException("The run status is invalid.", nameof(statusCode));
        }

        if (startedAtUtc.Offset != TimeSpan.Zero
            || endedAtUtc.Offset != TimeSpan.Zero
            || endedAtUtc < startedAtUtc)
        {
            throw new ArgumentException("Run timestamps must be ordered UTC values.");
        }

        Schedule = schedule;
        InputSnapshot = inputSnapshot;
        StatusCode = statusCode;
        StartedAtUtc = startedAtUtc;
        EndedAtUtc = endedAtUtc;
    }

    public EvaluationScheduleResult Schedule { get; }

    public EvaluationPlan Plan => Schedule.Plan;

    public QuantificationSnapshot Snapshot => Plan.Snapshot;

    public string DefinitionSha256 => Snapshot.Sha256;

    public InputSnapshot InputSnapshot { get; }

    public ValidatedColumnMapping Mapping => Plan.Mapping;

    public ImmutableArray<EvaluationUnitResult> Units => Schedule.Units;

    public string StatusCode { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset EndedAtUtc { get; }

    public int PlannedEvaluationCount => Units.Length;

    public int CompletedEvaluationCount => Units.Count(unit => !IsStatus(
        unit,
        ResultsStatusCodes.Cancelled));

    public int SucceededCount => Units.Count(unit => IsStatus(
        unit,
        ResultsStatusCodes.Success));

    public int EmptyCount => Units.Count(unit => IsStatus(
        unit,
        ResultsStatusCodes.Empty));

    public int CancelledCount => Units.Count(unit => IsStatus(
        unit,
        ResultsStatusCodes.Cancelled));

    public int FailureCount => Units.Length - SucceededCount - EmptyCount - CancelledCount;

    public bool IsPartial => CancelledCount > 0;

    public bool IsExportReady => !string.Equals(
        StatusCode,
        QuantificationRunStatusCodes.InputChanged,
        StringComparison.Ordinal);

    public RunOverrideValidationResult ValidateOverrides(
        IEnumerable<RunCriterionOverride>? overrides = null)
    {
        ImmutableArray<RunOverrideValidationError>.Builder errors =
            ImmutableArray.CreateBuilder<RunOverrideValidationError>();
        ImmutableDictionary<RunOverrideKey, string>.Builder validated =
            ImmutableDictionary.CreateBuilder<RunOverrideKey, string>();
        HashSet<RunOverrideKey> seen = [];
        Dictionary<RunUnitKey, EvaluationUnitResult> units = Units.ToDictionary(
            unit => new RunUnitKey(
                unit.Item.SourceRowNumber,
                unit.Item.QuestionId,
                unit.Item.EvaluatorId));

        foreach (RunCriterionOverride? candidate in overrides ?? [])
        {
            if (candidate is null)
            {
                AddOverrideError(
                    errors,
                    "NULL_OVERRIDE",
                    null,
                    "<blank>",
                    "<blank>",
                    "<blank>",
                    "Override",
                    "null");
                continue;
            }

            string questionId = candidate.QuestionId ?? string.Empty;
            string evaluatorId = candidate.EvaluatorId ?? string.Empty;
            string criterionId = candidate.CriterionId ?? string.Empty;
            RunOverrideKey key = new(
                candidate.SourceRowNumber,
                questionId,
                evaluatorId,
                criterionId);
            if (!seen.Add(key))
            {
                AddOverrideError(
                    errors,
                    "DUPLICATE_OVERRIDE",
                    candidate.SourceRowNumber,
                    SafeId(questionId),
                    SafeId(evaluatorId),
                    SafeId(criterionId),
                    "Override",
                    "duplicate");
                continue;
            }

            if (!TryResolveCriterion(
                    units,
                    key,
                    out EvaluationUnitResult? unit,
                    out CriterionDefinition? criterion,
                    out ScoreRange range))
            {
                AddOverrideError(
                    errors,
                    "OVERRIDE_CRITERION_NOT_ENABLED",
                    candidate.SourceRowNumber,
                    SafeId(questionId),
                    SafeId(evaluatorId),
                    SafeId(criterionId),
                    "Override",
                    "unexpected");
                continue;
            }

            string? value = candidate.Value;
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            bool questionScorable = DetermineQuestionScorable(
                candidate.SourceRowNumber,
                questionId);
            if (!questionScorable || (unit.ScorableKnown && !unit.Scorable))
            {
                AddOverrideError(
                    errors,
                    "OVERRIDE_NOT_ALLOWED",
                    candidate.SourceRowNumber,
                    questionId,
                    evaluatorId,
                    criterion.Id,
                    "Override",
                    "unscorable");
                continue;
            }

            if (value.Length > ConfigSheetWriter.MaximumCellCharacters)
            {
                AddOverrideError(
                    errors,
                    "OVERRIDE_TEXT_LIMIT_EXCEEDED",
                    candidate.SourceRowNumber,
                    questionId,
                    evaluatorId,
                    criterion.Id,
                    "Override",
                    value.Length.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            if (!decimal.TryParse(
                    value,
                    OverrideNumberStyles,
                    CultureInfo.InvariantCulture,
                    out decimal parsed))
            {
                AddOverrideError(
                    errors,
                    "OVERRIDE_NOT_NUMERIC",
                    candidate.SourceRowNumber,
                    questionId,
                    evaluatorId,
                    criterion.Id,
                    "Override",
                    "non-numeric");
                continue;
            }

            if (parsed < range.Minimum || parsed > range.Maximum)
            {
                AddOverrideError(
                    errors,
                    "OVERRIDE_OUT_OF_RANGE",
                    candidate.SourceRowNumber,
                    questionId,
                    evaluatorId,
                    criterion.Id,
                    "Override",
                    "out-of-range");
                continue;
            }

            validated.Add(key, value);
        }

        return new RunOverrideValidationResult(errors.ToImmutable(), validated.ToImmutable());
    }

    public RunOutputPreparation PrepareOutput(
        IEnumerable<RunCriterionOverride>? overrides = null)
    {
        RunOverrideValidationResult validation = ValidateOverrides(overrides);
        bool inputUnchanged = IsExportReady;
        ImmutableArray<ResultsSheetRowInput> rows = inputUnchanged && validation.IsValid
            ? BuildRows(validation.ValidatedValues)
            : [];
        return new RunOutputPreparation(
            Snapshot,
            InputSnapshot,
            StatusCode,
            IsPartial,
            rows,
            validation.Errors,
            inputUnchanged);
    }

    public override string ToString() =>
        $"{nameof(RunSummary)} {{ StatusCode = {StatusCode}, PlannedEvaluationCount = {PlannedEvaluationCount.ToString(CultureInfo.InvariantCulture)}, CompletedEvaluationCount = {CompletedEvaluationCount.ToString(CultureInfo.InvariantCulture)}, FailureCount = {FailureCount.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";

    private ImmutableArray<ResultsSheetRowInput> BuildRows(
        IReadOnlyDictionary<RunOverrideKey, string> overrides)
    {
        Dictionary<RunUnitKey, EvaluationUnitResult> units = Units.ToDictionary(
            unit => new RunUnitKey(
                unit.Item.SourceRowNumber,
                unit.Item.QuestionId,
                unit.Item.EvaluatorId));
        ImmutableArray<ResultsSheetRowInput>.Builder rows =
            ImmutableArray.CreateBuilder<ResultsSheetRowInput>();
        for (int sourceRow = Snapshot.Definition.FirstDataRow;
             sourceRow <= Snapshot.Definition.LastDataRow;
             sourceRow++)
        {
            ImmutableArray<QuestionResultInput>.Builder questions =
                ImmutableArray.CreateBuilder<QuestionResultInput>();
            foreach (QuestionDefinition question in Snapshot.Definition.Questions.Where(question => question.Enabled))
            {
                bool scorable = DetermineQuestionScorable(sourceRow, question.Id);
                ImmutableArray<EvaluatorResultInput>.Builder evaluators =
                    ImmutableArray.CreateBuilder<EvaluatorResultInput>();
                foreach (EvaluatorDefinition evaluator in question.Evaluators.Where(evaluator => evaluator.Enabled))
                {
                    EvaluationUnitResult unit = units[new RunUnitKey(
                        sourceRow,
                        question.Id,
                        evaluator.Id)];
                    ImmutableArray<CriterionOverrideInput>.Builder evaluatorOverrides =
                        ImmutableArray.CreateBuilder<CriterionOverrideInput>();
                    foreach (CriterionDefinition criterion in evaluator.Criteria.Where(criterion => criterion.Enabled))
                    {
                        RunOverrideKey key = new(
                            sourceRow,
                            question.Id,
                            evaluator.Id,
                            criterion.Id);
                        if (overrides.TryGetValue(key, out string? value))
                        {
                            evaluatorOverrides.Add(new CriterionOverrideInput
                            {
                                CriterionId = criterion.Id,
                                Value = value,
                            });
                        }
                    }

                    evaluators.Add(new EvaluatorResultInput
                    {
                        EvaluatorId = evaluator.Id,
                        AiResult = unit.AcceptedResult,
                        Status = unit.StatusCode,
                        Overrides = evaluatorOverrides.ToImmutable(),
                    });
                }

                questions.Add(new QuestionResultInput
                {
                    QuestionId = question.Id,
                    Scorable = scorable,
                    Evaluators = evaluators.ToImmutable(),
                });
            }

            rows.Add(new ResultsSheetRowInput
            {
                SourceRowNumber = sourceRow,
                Questions = questions.ToImmutable(),
            });
        }

        return rows.ToImmutable();
    }

    private bool DetermineQuestionScorable(int sourceRow, string questionId)
    {
        bool? known = null;
        foreach (EvaluationUnitResult unit in Units.Where(unit =>
                     unit.Item.SourceRowNumber == sourceRow
                     && string.Equals(unit.Item.QuestionId, questionId, StringComparison.Ordinal)
                     && unit.ScorableKnown))
        {
            known ??= unit.Scorable;
            if (known.Value != unit.Scorable)
            {
                return false;
            }
        }

        return known ?? false;
    }

    private bool TryResolveCriterion(
        IReadOnlyDictionary<RunUnitKey, EvaluationUnitResult> units,
        RunOverrideKey key,
        out EvaluationUnitResult unit,
        out CriterionDefinition criterion,
        out ScoreRange range)
    {
        unit = null!;
        criterion = null!;
        range = default;
        if (!units.TryGetValue(
                new RunUnitKey(key.SourceRowNumber, key.QuestionId, key.EvaluatorId),
                out EvaluationUnitResult? resolvedUnit))
        {
            return false;
        }

        QuestionDefinition? question = Snapshot.Definition.Questions.SingleOrDefault(candidate =>
            candidate.Enabled
            && string.Equals(candidate.Id, key.QuestionId, StringComparison.Ordinal));
        EvaluatorDefinition? evaluator = question?.Evaluators.SingleOrDefault(candidate =>
            candidate.Enabled
            && string.Equals(candidate.Id, key.EvaluatorId, StringComparison.Ordinal));
        CriterionDefinition? resolvedCriterion = evaluator?.Criteria.SingleOrDefault(candidate =>
            candidate.Enabled
            && string.Equals(candidate.Id, key.CriterionId, StringComparison.Ordinal));
        if (resolvedUnit is null || evaluator is null || resolvedCriterion is null)
        {
            return false;
        }

        unit = resolvedUnit;
        criterion = resolvedCriterion;
        range = resolvedCriterion.Range ?? evaluator.Range;
        return true;
    }

    private static bool IsStatus(EvaluationUnitResult unit, string statusCode) =>
        string.Equals(unit.StatusCode, statusCode, StringComparison.Ordinal);

    private static string SafeId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "<blank>" : value;

    private static void AddOverrideError(
        ImmutableArray<RunOverrideValidationError>.Builder errors,
        string code,
        int? sourceRow,
        string questionId,
        string evaluatorId,
        string criterionId,
        string field,
        string safeOffendingValue) =>
        errors.Add(new RunOverrideValidationError(
            code,
            sourceRow,
            questionId,
            evaluatorId,
            criterionId,
            field,
            safeOffendingValue));
}

internal readonly record struct RunUnitKey(
    int SourceRowNumber,
    string QuestionId,
    string EvaluatorId);

internal readonly record struct RunOverrideKey(
    int SourceRowNumber,
    string QuestionId,
    string EvaluatorId,
    string CriterionId);

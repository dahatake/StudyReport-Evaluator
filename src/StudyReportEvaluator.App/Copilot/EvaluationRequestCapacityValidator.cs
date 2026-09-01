using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using StudyReportEvaluator.Core.Prompting;

namespace StudyReportEvaluator.App.Copilot;

public sealed record EvaluationRequestCapacityError(
    string Code,
    string Field,
    long ActualDimension,
    int Limit)
{
    public override string ToString() =>
        $"{nameof(EvaluationRequestCapacityError)} {{ Code = {Code}, Field = {Field}, ActualDimension = {ActualDimension.ToString(CultureInfo.InvariantCulture)}, Limit = {Limit.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";
}

public sealed class EvaluationRequestCapacityResult
{
    internal EvaluationRequestCapacityResult(
        long promptUnicodeScalarCount,
        long promptUtf8ByteCount,
        long schemaUtf8ByteCount,
        long appOwnedRequestUtf8ByteCount,
        int modelContextBudget,
        ImmutableArray<EvaluationRequestCapacityError> errors)
    {
        PromptUnicodeScalarCount = promptUnicodeScalarCount;
        PromptUtf8ByteCount = promptUtf8ByteCount;
        SchemaUtf8ByteCount = schemaUtf8ByteCount;
        AppOwnedRequestUtf8ByteCount = appOwnedRequestUtf8ByteCount;
        ModelContextBudget = modelContextBudget;
        Errors = errors;
    }

    public long PromptUnicodeScalarCount { get; }

    public long PromptUtf8ByteCount { get; }

    public long SchemaUtf8ByteCount { get; }

    public long AppOwnedRequestUtf8ByteCount { get; }

    public int ModelContextBudget { get; }

    public ImmutableArray<EvaluationRequestCapacityError> Errors { get; }

    public bool IsValid => Errors.IsEmpty;

    public override string ToString() =>
        $"{nameof(EvaluationRequestCapacityResult)} {{ PromptUnicodeScalarCount = {PromptUnicodeScalarCount.ToString(CultureInfo.InvariantCulture)}, PromptUtf8ByteCount = {PromptUtf8ByteCount.ToString(CultureInfo.InvariantCulture)}, SchemaUtf8ByteCount = {SchemaUtf8ByteCount.ToString(CultureInfo.InvariantCulture)}, AppOwnedRequestUtf8ByteCount = {AppOwnedRequestUtf8ByteCount.ToString(CultureInfo.InvariantCulture)}, ModelContextBudget = {ModelContextBudget.ToString(CultureInfo.InvariantCulture)}, ErrorCount = {Errors.Length.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";
}

public sealed class EvaluationRequestCapacityValidator
{
    public const int MaximumRequestUnicodeScalars = 65_536;
    public const int MaximumWorstCaseAttemptsPerRun = 20_000;
    public const int ModelContextBudgetPercent = 80;

    private readonly EvaluationSchemaFactory schemaFactory = new();

    public EvaluationRequestCapacityResult Validate(
        SafeEvaluationPayload payload,
        int maximumPromptTokens,
        int maximumContextWindowTokens)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (maximumPromptTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPromptTokens));
        }

        if (maximumContextWindowTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumContextWindowTokens));
        }

        int effectiveLimit = Math.Min(maximumPromptTokens, maximumContextWindowTokens);
        string schema = schemaFactory.CreateSchema(payload).GetRawText();
        long promptScalars = CountUnicodeScalars(payload.RenderedPrompt);
        long promptBytes = Encoding.UTF8.GetByteCount(payload.RenderedPrompt);
        long schemaScalars = CountUnicodeScalars(schema);
        long schemaBytes = Encoding.UTF8.GetByteCount(schema);
        long toolContractScalars = checked(
            CountUnicodeScalars(EvaluationSchemaFactory.ToolName)
            + CountUnicodeScalars(EvaluationSchemaFactory.ToolDescription));
        long toolContractBytes = checked(
            Encoding.UTF8.GetByteCount(EvaluationSchemaFactory.ToolName)
            + Encoding.UTF8.GetByteCount(EvaluationSchemaFactory.ToolDescription));
        long appOwnedScalars = checked(promptScalars + schemaScalars + toolContractScalars);
        long appOwnedBytes = checked(promptBytes + schemaBytes + toolContractBytes);
        int modelContextBudget = checked((int)((long)effectiveLimit * ModelContextBudgetPercent / 100L));
        ImmutableArray<EvaluationRequestCapacityError>.Builder errors =
            ImmutableArray.CreateBuilder<EvaluationRequestCapacityError>();

        if (appOwnedScalars > MaximumRequestUnicodeScalars)
        {
            errors.Add(new EvaluationRequestCapacityError(
                "REQUEST_SCALAR_LIMIT_EXCEEDED",
                "AppOwnedRequestUnicodeScalars",
                appOwnedScalars,
                MaximumRequestUnicodeScalars));
        }

        // The SDK exposes model limits but not pre-send tokenization for an uncreated
        // ephemeral session. UTF-8 bytes are an explicit conservative upper bound for
        // app-owned tokenization and leave 20% of the model budget for runtime context.
        // This dimension is never reported as measured model tokens.
        if (appOwnedBytes > modelContextBudget)
        {
            errors.Add(new EvaluationRequestCapacityError(
                "REQUEST_CONTEXT_BUDGET_EXCEEDED",
                "AppOwnedRequestUtf8Bytes",
                appOwnedBytes,
                modelContextBudget));
        }

        return new EvaluationRequestCapacityResult(
            promptScalars,
            promptBytes,
            schemaBytes,
            appOwnedBytes,
            modelContextBudget,
            errors.ToImmutable());
    }

    private static long CountUnicodeScalars(string value)
    {
        long count = 0;
        foreach (Rune _ in value.EnumerateRunes())
        {
            count++;
        }

        return count;
    }
}

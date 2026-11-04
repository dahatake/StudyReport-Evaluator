using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Serialization;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Similarity;

namespace StudyReportEvaluator.App.Workbooks.Checkpoint;

public sealed record CheckpointRuntimeIdentity
{
    public required string ApplicationIdentity { get; init; }

    public required string CliVersion { get; init; }

    public required string CliSha256 { get; init; }

    public required string SdkInformationalVersion { get; init; }

    public override string ToString() =>
        $"{nameof(CheckpointRuntimeIdentity)} {{ Content = <redacted> }}";
}

public sealed record CheckpointTokenUsage
{
    public bool IsAvailable { get; init; }

    public long InputTokens { get; init; }

    public long OutputTokens { get; init; }

    public long ReasoningTokens { get; init; }

    public long CacheReadTokens { get; init; }

    public long CacheWriteTokens { get; init; }

    public override string ToString() =>
        $"{nameof(CheckpointTokenUsage)} {{ IsAvailable = {IsAvailable}, Content = <redacted> }}";
}

public sealed record CheckpointReference
{
    public required string QuestionId { get; init; }

    // FR-115 AC-009 / AC-083: 設問内の slot 番号（1 起点）。旧単独参照 checkpoint は 0（旧形式の単独集合）で、書かない。
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SlotIndex { get; init; }

    public string? Answer { get; init; }

    public required string StatusCode { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public int AttemptCount { get; init; }

    public CheckpointTokenUsage TokenUsage { get; init; } = new();

    public override string ToString() =>
        $"{nameof(CheckpointReference)} {{ StatusCode = {StatusCode}, Content = <redacted> }}";
}

public sealed record CheckpointNormalResult
{
    public required string QuestionId { get; init; }

    public required string EvaluatorId { get; init; }

    public required string StatusCode { get; init; }

    public int AttemptCount { get; init; }

    public bool Scorable { get; init; }

    public bool ScorableKnown { get; init; }

    public QuantificationResult? AcceptedResult { get; init; }

    public CheckpointTokenUsage TokenUsage { get; init; } = new();

    public override string ToString() =>
        $"{nameof(CheckpointNormalResult)} {{ StatusCode = {StatusCode}, Content = <redacted> }}";
}

public sealed record CheckpointSpecialResult
{
    public required string QuestionId { get; init; }

    public required string SpecialEvaluationId { get; init; }

    public required string StatusCode { get; init; }

    public int AttemptCount { get; init; }

    public SpecialQuantificationResult? AcceptedResult { get; init; }

    public CheckpointTokenUsage TokenUsage { get; init; } = new();

    public override string ToString() =>
        $"{nameof(CheckpointSpecialResult)} {{ StatusCode = {StatusCode}, Content = <redacted> }}";
}

public sealed record CheckpointSimilarityResult
{
    public required string QuestionId { get; init; }

    public required string StatusCode { get; init; }

    public int AttemptCount { get; init; }

    public SimilarityQuantificationResult? AcceptedResult { get; init; }

    public CheckpointTokenUsage TokenUsage { get; init; } = new();

    // FR-114 AC-010 / AC-077 / AC-079: 参照別の確認指標（未検証）の詳細。学生本文は持たない。
    // 旧形式の checkpoint にはない（null）。null のときは書かないため、旧 checkpoint の往復を保つ。
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<SimilarityReferenceDiagnostic>? ReferenceDiagnostics { get; init; }

    // FR-115 AC-081: 比較に使った参照集合の成功数／予定数と重複。旧形式にはない（null）。null のときは書かない。
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SimilarityReferenceSetInfo? ReferenceSet { get; init; }

    public override string ToString() =>
        $"{nameof(CheckpointSimilarityResult)} {{ StatusCode = {StatusCode}, Content = <redacted> }}";
}

public sealed record CheckpointCompletedRow
{
    public int SourceRowNumber { get; init; }

    public ImmutableArray<CheckpointNormalResult> NormalResults { get; init; } = [];

    public ImmutableArray<CheckpointSpecialResult> SpecialResults { get; init; } = [];

    public ImmutableArray<CheckpointSimilarityResult> SimilarityResults { get; init; } = [];

    public override string ToString() =>
        $"{nameof(CheckpointCompletedRow)} {{ SourceRowNumber = {SourceRowNumber.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";
}

public sealed record CheckpointEnvelope
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string InputPath { get; init; }

    public required InputSnapshot Input { get; init; }

    public required string DefinitionCanonicalJson { get; init; }

    public required string DefinitionSha256 { get; init; }

    public required string NormalModelId { get; init; }

    public string ReferenceModelId { get; init; } = "auto";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningEffort { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContextTier { get; init; }

    // FR-115 AC-009 / AC-080 / AC-083: 設問ごとの予定 slot 数（PARAM-003）と、集合に結び付けた比較条件の版。
    // 旧形式（0 / null）は旧単独参照の集合で、PARAM-003 へ水増しせず、条件は「未取得」として扱う。書かない。
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ReferenceSlotCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReferenceTemplateVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReferenceComparisonVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReferenceNormalizationVersion { get; init; }

    public required CheckpointRuntimeIdentity Runtime { get; init; }

    public required string FinalPath { get; init; }

    public required string PartialPath { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset SavedAtUtc { get; init; }

    public ImmutableArray<CheckpointReference> References { get; init; } = [];

    public ImmutableArray<CheckpointCompletedRow> CompletedRows { get; init; } = [];

    public override string ToString() =>
        $"{nameof(CheckpointEnvelope)} {{ SchemaVersion = {SchemaVersion.ToString(CultureInfo.InvariantCulture)}, ReferenceCount = {References.Length.ToString(CultureInfo.InvariantCulture)}, CompletedRowCount = {CompletedRows.Length.ToString(CultureInfo.InvariantCulture)}, Content = <redacted> }}";
}

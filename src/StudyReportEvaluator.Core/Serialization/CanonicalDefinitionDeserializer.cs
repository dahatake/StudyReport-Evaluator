using System.Collections.Immutable;
using System.Text.Json;
using StudyReportEvaluator.Core.Domain;

namespace StudyReportEvaluator.Core.Serialization;

// FR-110 / AC-066 / AC-067: CanonicalDefinitionSerializer が書いた JSON を、例外でなく結果として読み戻す。
public enum DefinitionReadErrorKind
{
    MissingConfigSheet,
    MissingCanonicalJson,
    EmptyCanonicalJson,
    MalformedJson,
    MissingRequiredField,
    UnknownEvaluatorType,
    EvaluatorTypeMismatch,
}

public sealed record DefinitionReadError(DefinitionReadErrorKind Kind, string Message);

public sealed class CanonicalDefinitionReadResult
{
    private CanonicalDefinitionReadResult(
        QuantificationDefinition? definition,
        ImmutableArray<DefinitionReadError> errors,
        ImmutableArray<string> notices)
    {
        Definition = definition;
        Errors = errors;
        Notices = notices;
    }

    public bool IsSuccess => Definition is not null && Errors.IsEmpty;

    public QuantificationDefinition? Definition { get; }

    public ImmutableArray<DefinitionReadError> Errors { get; }

    // FR-114 AC-004: 読込時に旧形式の欠損値を補ったなどの事実（エラーではない）。
    public ImmutableArray<string> Notices { get; }

    public static CanonicalDefinitionReadResult Success(QuantificationDefinition definition) =>
        new(definition, [], []);

    public static CanonicalDefinitionReadResult Success(QuantificationDefinition definition, IEnumerable<string> notices) =>
        new(definition, [], [.. notices]);

    public static CanonicalDefinitionReadResult Failure(IEnumerable<DefinitionReadError> errors) =>
        new(null, [.. errors], []);

    public static CanonicalDefinitionReadResult Failure(DefinitionReadErrorKind kind, string message) =>
        new(null, [new DefinitionReadError(kind, message)], []);
}

public sealed class CanonicalDefinitionDeserializer
{
    /// <summary>FR-114 AC-004: 旧形式で類似度減点係数が未記録のとき、旧既定値を補ったことの通知。</summary>
    public const string LegacySimilarityPenaltyWeightNotice =
        "旧形式の定義のため類似度減点係数（similarityPenaltyWeight）が未記録でした。旧既定値 0.1 を補いました。";

    public CanonicalDefinitionReadResult Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CanonicalDefinitionReadResult.Failure(
                DefinitionReadErrorKind.EmptyCanonicalJson,
                "The canonical definition JSON is empty.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return CanonicalDefinitionReadResult.Failure(
                DefinitionReadErrorKind.MalformedJson,
                "The canonical definition JSON is not valid JSON.");
        }

        using (document)
        {
            List<DefinitionReadError> errors = [];
            List<string> notices = [];
            QuantificationDefinition? definition = ReadDefinition(document.RootElement, errors, notices);
            return errors.Count == 0 && definition is not null
                ? CanonicalDefinitionReadResult.Success(definition, notices)
                : CanonicalDefinitionReadResult.Failure(errors);
        }
    }

    private static QuantificationDefinition? ReadDefinition(JsonElement root, List<DefinitionReadError> errors, List<string> notices)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            Missing(errors, "$", "object");
            return null;
        }

        string? id = Str(root, "id", "$", errors);
        string? name = Str(root, "name", "$", errors);
        string? revision = Str(root, "revision", "$", errors);
        string? sourceSheet = Str(root, "sourceSheet", "$", errors);
        _ = Str(root, "schemaVersion", "$", errors);
        int? headerRow = Int(root, "headerRow", "$", errors);
        int? firstDataRow = Int(root, "firstDataRow", "$", errors);
        int? lastDataRow = Int(root, "lastDataRow", "$", errors);
        decimal? basePoints = Dec(root, "basePoints", "$", errors);
        decimal? specialPoints = Dec(root, "specialPoints", "$", errors);
        // FR-114 AC-004: 欠損（プロパティなし）だけを旧形式として旧既定値で補う。壊れた値はエラーのまま。
        decimal? penalty;
        if (root.TryGetProperty("similarityPenaltyWeight", out _))
        {
            penalty = Dec(root, "similarityPenaltyWeight", "$", errors);
        }
        else
        {
            penalty = QuantificationDefinition.LegacyDefaultSimilarityPenaltyWeight;
            notices.Add(LegacySimilarityPenaltyWeightNotice);
        }
        int? rounding = Int(root, "roundingDigits", "$", errors);
        ImmutableArray<QuestionDefinition>.Builder questions = ImmutableArray.CreateBuilder<QuestionDefinition>();
        foreach ((JsonElement item, string path) in Items(root, "questions", "$", errors))
        {
            QuestionDefinition? question = ReadQuestion(item, path, errors);
            if (question is not null)
            {
                questions.Add(question);
            }
        }

        if (errors.Count > 0)
        {
            return null;
        }

        return new QuantificationDefinition
        {
            Id = id!,
            Name = name!,
            Revision = revision!,
            SourceSheet = sourceSheet!,
            HeaderRow = headerRow!.Value,
            FirstDataRow = firstDataRow!.Value,
            LastDataRow = lastDataRow!.Value,
            BasePoints = basePoints!.Value,
            SpecialPoints = specialPoints!.Value,
            SimilarityPenaltyWeight = penalty!.Value,
            RoundingDigits = rounding!.Value,
            Questions = questions.ToImmutable(),
        };
    }

    private static QuestionDefinition? ReadQuestion(JsonElement element, string path, List<DefinitionReadError> errors)
    {
        string? id = Str(element, "id", path, errors);
        string? displayName = Str(element, "displayName", path, errors);
        string? questionText = Str(element, "questionText", path, errors);
        string? primary = Str(element, "primarySourceColumn", path, errors);
        ImmutableArray<string> supporting = Strings(element, "supportingSourceColumns", path, errors);
        decimal? points = Dec(element, "points", path, errors);
        bool? enabled = Bool(element, "enabled", path, errors);
        ImmutableArray<EvaluatorDefinition>.Builder evaluators = ImmutableArray.CreateBuilder<EvaluatorDefinition>();
        foreach ((JsonElement item, string itemPath) in Items(element, "evaluators", path, errors))
        {
            EvaluatorDefinition? evaluator = ReadEvaluator(item, itemPath, errors);
            if (evaluator is not null)
            {
                evaluators.Add(evaluator);
            }
        }

        ImmutableArray<SpecialEvaluationDefinition>.Builder specials =
            ImmutableArray.CreateBuilder<SpecialEvaluationDefinition>();
        foreach ((JsonElement item, string itemPath) in Items(element, "specialEvaluations", path, errors))
        {
            string? sId = Str(item, "id", itemPath, errors);
            string? sName = Str(item, "displayName", itemPath, errors);
            string? sPrimary = Str(item, "primarySourceColumn", itemPath, errors);
            ImmutableArray<string> sSupporting = Strings(item, "supportingSourceColumns", itemPath, errors);
            string? sPrompt = Str(item, "promptTemplate", itemPath, errors);
            bool? sEnabled = Bool(item, "enabled", itemPath, errors);
            if (sId is not null && sName is not null && sPrimary is not null && sPrompt is not null && sEnabled is not null)
            {
                specials.Add(new SpecialEvaluationDefinition
                {
                    Id = sId,
                    DisplayName = sName,
                    PrimarySourceColumn = sPrimary,
                    SupportingSourceColumns = sSupporting,
                    PromptTemplate = sPrompt,
                    Enabled = sEnabled.Value,
                });
            }
        }

        if (id is null || displayName is null || questionText is null || primary is null || points is null || enabled is null)
        {
            return null;
        }

        return new QuestionDefinition
        {
            Id = id,
            DisplayName = displayName,
            QuestionText = questionText,
            PrimarySourceColumn = primary,
            SupportingSourceColumns = supporting,
            Points = points.Value,
            Enabled = enabled.Value,
            Evaluators = evaluators.ToImmutable(),
            SpecialEvaluations = specials.ToImmutable(),
        };
    }

    private static EvaluatorDefinition? ReadEvaluator(JsonElement element, string path, List<DefinitionReadError> errors)
    {
        string? id = Str(element, "id", path, errors);
        string? displayName = Str(element, "displayName", path, errors);
        string? typeName = Str(element, "type", path, errors);
        EvaluatorType? type = null;
        if (typeName is not null)
        {
            type = ParseType(typeName);
            if (type is null)
            {
                errors.Add(new DefinitionReadError(
                    DefinitionReadErrorKind.UnknownEvaluatorType,
                    $"Unknown evaluator type '{typeName}' at {path}.type."));
            }
        }

        decimal? weight = Dec(element, "weight", path, errors);
        ScoreRange? range = Range(element, "range", path, errors, required: true);
        string? version = OptionalStr(element, "builtInTemplateVersion", path, errors);
        string? custom = OptionalStr(element, "customPromptTemplate", path, errors);
        bool? enabled = Bool(element, "enabled", path, errors);
        ImmutableArray<CriterionDefinition>.Builder criteria = ImmutableArray.CreateBuilder<CriterionDefinition>();
        foreach ((JsonElement item, string itemPath) in Items(element, "criteria", path, errors))
        {
            string? cId = Str(item, "id", itemPath, errors);
            string? cName = Str(item, "displayName", itemPath, errors);
            string? cDescription = Str(item, "description", itemPath, errors);
            decimal? cWeight = Dec(item, "weight", itemPath, errors);
            ScoreRange? cRange = Range(item, "range", itemPath, errors, required: false);
            bool? cEnabled = Bool(item, "enabled", itemPath, errors);
            if (cId is not null && cName is not null && cDescription is not null && cWeight is not null && cEnabled is not null)
            {
                criteria.Add(new CriterionDefinition
                {
                    Id = cId,
                    DisplayName = cName,
                    Description = cDescription,
                    Weight = cWeight.Value,
                    Range = cRange,
                    Enabled = cEnabled.Value,
                });
            }
        }

        if (id is null || displayName is null || type is null || weight is null || range is null || enabled is null)
        {
            return null;
        }

        return new EvaluatorDefinition
        {
            Id = id,
            DisplayName = displayName,
            Type = type.Value,
            Weight = weight.Value,
            Range = range.Value,
            BuiltInTemplateVersion = version,
            CustomPromptTemplate = custom,
            Enabled = enabled.Value,
            Criteria = criteria.ToImmutable(),
        };
    }

    /// <summary>種類名（KNOWLEDGE_COVERAGE／PROMPT_ANALYSIS／CUSTOM_PROMPT）を種類へ。3値以外は null。</summary>
    public static EvaluatorType? ParseType(string? name) => name switch
    {
        "KNOWLEDGE_COVERAGE" => EvaluatorType.KnowledgeCoverage,
        "PROMPT_ANALYSIS" => EvaluatorType.PromptAnalysis,
        "CUSTOM_PROMPT" => EvaluatorType.CustomPrompt,
        _ => null,
    };

    private static void Missing(List<DefinitionReadError> errors, string path, string expected) =>
        errors.Add(new DefinitionReadError(
            DefinitionReadErrorKind.MissingRequiredField,
            $"Required field {path} is missing or is not {expected}."));

    private static bool TryGet(JsonElement parent, string name, JsonValueKind kind, string path, string expected,
        List<DefinitionReadError> errors, out JsonElement value)
    {
        value = default;
        if (parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out value)
            && value.ValueKind == kind)
        {
            return true;
        }

        Missing(errors, $"{path}.{name}", expected);
        return false;
    }

    private static string? Str(JsonElement parent, string name, string path, List<DefinitionReadError> errors) =>
        TryGet(parent, name, JsonValueKind.String, path, "a string", errors, out JsonElement v) ? v.GetString() : null;

    private static string? OptionalStr(JsonElement parent, string name, string path, List<DefinitionReadError> errors)
    {
        if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out JsonElement v))
        {
            if (v.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (v.ValueKind == JsonValueKind.String)
            {
                return v.GetString();
            }
        }

        Missing(errors, $"{path}.{name}", "a string or null");
        return null;
    }

    private static int? Int(JsonElement parent, string name, string path, List<DefinitionReadError> errors) =>
        TryGet(parent, name, JsonValueKind.Number, path, "an integer", errors, out JsonElement v)
            ? (v.TryGetInt32(out int i) ? i : FailNumber(errors, path, name, "an integer"))
            : null;

    private static decimal? Dec(JsonElement parent, string name, string path, List<DefinitionReadError> errors) =>
        TryGet(parent, name, JsonValueKind.Number, path, "a number", errors, out JsonElement v)
            ? (v.TryGetDecimal(out decimal d) ? d : FailNumber(errors, path, name, "a number"))
            : null;

    private static int? FailNumber(List<DefinitionReadError> errors, string path, string name, string expected)
    {
        Missing(errors, $"{path}.{name}", expected);
        return null;
    }

    private static bool? Bool(JsonElement parent, string name, string path, List<DefinitionReadError> errors)
    {
        if (parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement v)
            && v.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return v.GetBoolean();
        }

        Missing(errors, $"{path}.{name}", "a boolean");
        return null;
    }

    private static ImmutableArray<string> Strings(JsonElement parent, string name, string path, List<DefinitionReadError> errors)
    {
        ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
        foreach ((JsonElement item, string itemPath) in Items(parent, name, path, errors))
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                builder.Add(item.GetString()!);
            }
            else
            {
                Missing(errors, itemPath, "a string");
            }
        }

        return builder.ToImmutable();
    }

    private static IEnumerable<(JsonElement Item, string Path)> Items(
        JsonElement parent, string name, string path, List<DefinitionReadError> errors)
    {
        if (!TryGet(parent, name, JsonValueKind.Array, path, "an array", errors, out JsonElement array))
        {
            return [];
        }

        return array.EnumerateArray()
            .Select((item, index) => (item, $"{path}.{name}[{index}]"))
            .ToArray();
    }

    private static ScoreRange? Range(JsonElement parent, string name, string path, List<DefinitionReadError> errors, bool required)
    {
        if (!required && parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement nullable) && nullable.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (!TryGet(parent, name, JsonValueKind.Object, path, "an object", errors, out JsonElement range))
        {
            return null;
        }

        string rangePath = $"{path}.{name}";
        decimal? min = Dec(range, "minimum", rangePath, errors);
        decimal? max = Dec(range, "maximum", rangePath, errors);
        return min is null || max is null ? null : new ScoreRange(min.Value, max.Value);
    }
}

using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using StudyReportEvaluator.Core.Tests.Validation;
using Xunit;

namespace StudyReportEvaluator.Core.Tests.Prompting;

// FR-109 / AC-064: Prompt種類は組込みテンプレート prompt-analysis-v1 を使い、未知のversionは拒否する。
public sealed class Fr109PromptKindTemplateTests
{
    private readonly SafeEvaluationPayloadBuilder _builder = new();

    private static QuantificationSnapshot SnapshotWith(EvaluatorType type, string? version)
    {
        QuantificationDefinition source = C02TestDefinitions.CreateValid();
        EvaluatorDefinition evaluator = source.Questions[0].Evaluators[0] with
        {
            Type = type,
            BuiltInTemplateVersion = version,
            CustomPromptTemplate = null,
        };
        QuantificationDefinition definition = source with
        {
            Questions =
            [
                source.Questions[0] with { Evaluators = [evaluator, .. source.Questions[0].Evaluators.Skip(1)] },
                .. source.Questions.Skip(1),
            ],
        };
        return QuantificationSnapshot.Create(definition);
    }

    private static readonly Dictionary<string, string?> Row = new() { ["G"] = "answer", ["K"] = "", ["L"] = "" };

    // FR-109 / AC-064
    [Fact]
    public void FR109_AC064_Prompt_kind_builds_with_prompt_analysis_v1_and_keeps_the_structured_contract()
    {
        Assert.Equal("prompt-analysis-v1", BuiltInPromptTemplates.PromptAnalysisTemplateVersion);
        string evaluatorId = C02TestDefinitions.CreateValid().Questions[0].Evaluators[0].Id;

        SafeEvaluationPayload payload = _builder.Build(
            SnapshotWith(EvaluatorType.PromptAnalysis, BuiltInPromptTemplates.PromptAnalysisTemplateVersion),
            "Q1",
            evaluatorId,
            Row);

        Assert.Contains("answer", payload.RenderedPrompt, StringComparison.Ordinal);
        Assert.Contains("APP-OWNED STRUCTURED OUTPUT CONTRACT", payload.RenderedPrompt, StringComparison.Ordinal);
    }

    // FR-109 / AC-064
    [Theory]
    [InlineData("prompt-analysis-v2")]
    [InlineData("knowledge-v1")]
    [InlineData("")]
    [InlineData(null)]
    public void FR109_AC064_Prompt_kind_rejects_unknown_template_version(string? version)
    {
        // 組込みテンプレートの取得は未知versionを拒否する（payload構築が使う経路）
        PromptConfigurationException exception = Assert.Throws<PromptConfigurationException>(
            () => BuiltInPromptTemplates.GetPromptAnalysisTemplate(version));
        Assert.Equal("UNSUPPORTED_PROMPT_ANALYSIS_TEMPLATE_VERSION", exception.Code);

        // 定義の検証（スナップショット作成）も未知versionを拒否し、payloadは作られない
        Assert.Throws<StudyReportEvaluator.Core.Validation.QuantificationDefinitionValidationException>(
            () => SnapshotWith(EvaluatorType.PromptAnalysis, version));
    }
}

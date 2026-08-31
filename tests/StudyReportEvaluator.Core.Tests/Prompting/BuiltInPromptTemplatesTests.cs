using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.Core.Tests.Prompting;

public sealed class BuiltInPromptTemplatesTests
{
    [Fact]
    public void Knowledge_template_requires_semantic_explanation_relation_and_application_not_keyword_presence()
    {
        string template = BuiltInPromptTemplates.GetKnowledgeTemplate(BuiltInPromptTemplates.KnowledgeTemplateVersion);

        Assert.Contains("説明", template, StringComparison.Ordinal);
        Assert.Contains("関係", template, StringComparison.Ordinal);
        Assert.Contains("適用", template, StringComparison.Ordinal);
        Assert.Contains("単語が存在するだけで満点にせず", template, StringComparison.Ordinal);
        Assert.Contains("{回答}", template, StringComparison.Ordinal);
        Assert.Contains("{評価項目}", template, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_knowledge_template_version_is_rejected()
    {
        PromptConfigurationException exception = Assert.Throws<PromptConfigurationException>(
            () => BuiltInPromptTemplates.GetKnowledgeTemplate("unknown"));

        Assert.Equal("UNSUPPORTED_KNOWLEDGE_TEMPLATE_VERSION", exception.Code);
    }

    [Fact]
    public void App_owned_contract_forbids_aggregate_scores_and_requires_one_tool_submission()
    {
        string contract = BuiltInPromptTemplates.StructuredOutputInstruction;

        Assert.Contains("exactly once", contract, StringComparison.Ordinal);
        Assert.Contains("Never return an evaluator score, question score, overall score", contract, StringComparison.Ordinal);
        Assert.Contains("same-row", contract, StringComparison.Ordinal);
    }
}

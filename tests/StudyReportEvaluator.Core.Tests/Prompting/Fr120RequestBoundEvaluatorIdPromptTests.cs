using System.Text.RegularExpressions;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using StudyReportEvaluator.Core.Tests.Validation;
using Xunit;

namespace StudyReportEvaluator.Core.Tests.Prompting;

// FR-120 / AC-007: モデル向けの Prompt に evaluator ID の生成・返却指示を入れない（ID はアプリが送信時に保持する）。
// 通常評価のすべての種類（KNOWLEDGE_COVERAGE・PROMPT_ANALYSIS・CUSTOM_PROMPT）で同じ契約にする（監査 M-01）。
public sealed partial class Fr120RequestBoundEvaluatorIdPromptTests
{
    // 他の文字列と偶然一致しない evaluator ID。
    private const string DistinctEvaluatorId = "ZQ7EVX";

    private static readonly Dictionary<string, string?> Row = new() { ["G"] = "answer", ["K"] = "", ["L"] = "" };

    private readonly SafeEvaluationPayloadBuilder _builder = new();

    [GeneratedRegex(@"(?i)evaluator[\s_\-]*id")]
    private static partial Regex EvaluatorIdTerm();

    private static QuantificationSnapshot SnapshotWith(EvaluatorType type)
    {
        QuantificationDefinition source = C02TestDefinitions.CreateValid();
        EvaluatorDefinition original = source.Questions[0].Evaluators[0];
        EvaluatorDefinition evaluator = original with
        {
            Id = DistinctEvaluatorId,
            Type = type,
            BuiltInTemplateVersion = type switch
            {
                EvaluatorType.KnowledgeCoverage => BuiltInPromptTemplates.KnowledgeTemplateVersion,
                EvaluatorType.PromptAnalysis => BuiltInPromptTemplates.PromptAnalysisTemplateVersion,
                _ => null,
            },
            CustomPromptTemplate = type == EvaluatorType.CustomPrompt ? "{回答} {評価項目}" : null,
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

    // FR-120 AC-007
    [Theory]
    [InlineData(EvaluatorType.KnowledgeCoverage)]
    [InlineData(EvaluatorType.PromptAnalysis)]
    [InlineData(EvaluatorType.CustomPrompt)]
    public void FR120_AC007_prompt_has_no_evaluator_id_instruction_but_keeps_request_bound_identity(EvaluatorType type)
    {
        SafeEvaluationPayload payload = _builder.Build(SnapshotWith(type), "Q1", DistinctEvaluatorId, Row);

        // アプリは送信時の要求に ID を保持する。
        Assert.Equal(DistinctEvaluatorId, payload.EvaluatorId);
        Assert.Equal("Q1", payload.QuestionId);

        // モデル向け Prompt には ID の値も生成・返却指示もない。
        Assert.DoesNotContain(DistinctEvaluatorId, payload.RenderedPrompt, StringComparison.Ordinal);
        Assert.DoesNotMatch(EvaluatorIdTerm(), payload.RenderedPrompt);
        Assert.DoesNotContain("ExpectedEvaluatorId", payload.RenderedPrompt, StringComparison.Ordinal);

        // criterion ID・根拠列の契約は残す（採点・根拠の検証条件は変えない）。
        Assert.Contains("APP-OWNED STRUCTURED OUTPUT CONTRACT", payload.RenderedPrompt, StringComparison.Ordinal);
        Assert.Contains("PrimarySourceColumnId: G", payload.RenderedPrompt, StringComparison.Ordinal);
        Assert.Contains(
            "ExpectedCriterionIds: " + string.Join(",", payload.ExpectedCriteria.Select(c => c.CriterionId)),
            payload.RenderedPrompt,
            StringComparison.Ordinal);
    }

    // FR-120 AC-007
    [Fact]
    public void FR120_AC007_structured_output_instruction_does_not_ask_for_an_evaluator_id()
    {
        Assert.DoesNotMatch(EvaluatorIdTerm(), BuiltInPromptTemplates.StructuredOutputInstruction);
        Assert.Contains(
            "every expected criterion exactly once",
            BuiltInPromptTemplates.StructuredOutputInstruction,
            StringComparison.Ordinal);
    }
}

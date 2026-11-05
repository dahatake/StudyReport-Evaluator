using System.Text.Json;
using System.Text.Json.Serialization;
using StudyReportEvaluator.App.Tests.Workbooks.Mapping;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using StudyReportEvaluator.Core.Serialization;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class DefaultCriterionDescriptionTests
{
    private const string ExpectedKnowledge =
        "学生が作成したレポートを高度に分析・解析をしてそれぞれの{論点}について説明がされているかどうかの評価を行ってください。\n論点:\n- xxx\n- xxx\n- xxx";

    private const string ExpectedPromptAnalysis =
        "学生が作成したPromptを、Anthropic・OpenAI・Microsoftなどが公開するPrompt作成のベストプラクティスと照合して、高度に分析・評価を行ってください。\n観点:\n- 明確な目的が示されているか\n- 具体的で曖昧さのない指示になっているか\n- 必要な文脈・背景情報が与えられているか\n- 制約・禁止事項が明示されているか\n- 出力を検証できる基準が示されているか";

    private const string ExpectedCustom =
        "（評価したい内容と判断の基準を、利用者が具体的に書き換えてください。）\n評価の観点:\n- xxx\n- xxx\n- xxx";

    // FR-CD-01 / AC-052 ①
    [Fact]
    public void Description_text_matches_the_requested_wording_for_each_evaluator_type()
    {
        Assert.Equal(ExpectedKnowledge, DefaultCriterionDescriptions.For(EvaluatorType.KnowledgeCoverage));
        Assert.Equal(ExpectedPromptAnalysis, DefaultCriterionDescriptions.For(EvaluatorType.PromptAnalysis));
        Assert.Equal(ExpectedCustom, DefaultCriterionDescriptions.For(EvaluatorType.CustomPrompt));
        Assert.Throws<ArgumentOutOfRangeException>(() => DefaultCriterionDescriptions.For((EvaluatorType)99));
    }

    // FR-CD-01 / AC-052 ③
    [Fact]
    public void Type_choices_are_the_three_kinds_and_contract_names_round_trip()
    {
        Assert.Equal(
            ["Knowledge", "Prompt", "Custom"],
            QuantificationDesignViewModel.EvaluatorTypes.Select(choice => choice.DisplayName).ToArray());

        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        foreach ((EvaluatorType type, string name) in new[]
        {
            (EvaluatorType.KnowledgeCoverage, "KNOWLEDGE_COVERAGE"),
            (EvaluatorType.PromptAnalysis, "PROMPT_ANALYSIS"),
            (EvaluatorType.CustomPrompt, "CUSTOM_PROMPT"),
        })
        {
            Assert.Equal($"\"{name}\"", JsonSerializer.Serialize(type, options));
            Assert.Equal(type, JsonSerializer.Deserialize<EvaluatorType>($"\"{name}\"", options));
            Assert.Contains(QuantificationDesignViewModel.EvaluatorTypes, choice => choice.Type == type && choice.ContractName == name);
        }

        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel question = Assert.Single(viewModel.Questions);
        EvaluatorDesignItemViewModel prompt = viewModel.AddEvaluator(question.Id, EvaluatorType.PromptAnalysis);
        Assert.Equal("PROMPT_ANALYSIS", prompt.TypeContractName);
        Assert.Contains("\"PROMPT_ANALYSIS\"", new CanonicalDefinitionSerializer().Serialize(viewModel.Draft));
        Assert.True(viewModel.IsValid);
    }

    [Fact]
    public void New_design_has_knowledge_description_and_added_criteria_follow_their_evaluator_type()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel question = Assert.Single(viewModel.Questions);
        EvaluatorDesignItemViewModel knowledge = Assert.Single(question.Evaluators);
        Assert.Equal(ExpectedKnowledge, Assert.Single(knowledge.Criteria).Description);

        Assert.Equal(
            ExpectedKnowledge,
            viewModel.AddCriterion(question.Id, knowledge.Id).Description);

        EvaluatorDesignItemViewModel prompt = viewModel.AddEvaluator(question.Id, EvaluatorType.PromptAnalysis);
        Assert.Equal(ExpectedPromptAnalysis, Assert.Single(prompt.Criteria).Description);
        Assert.Equal(
            ExpectedPromptAnalysis,
            viewModel.AddCriterion(question.Id, prompt.Id).Description);

        EvaluatorDesignItemViewModel custom = viewModel.AddEvaluator(question.Id, EvaluatorType.CustomPrompt);
        Assert.Equal(ExpectedCustom, Assert.Single(custom.Criteria).Description);
        Assert.Equal(
            ExpectedCustom,
            viewModel.AddCriterion(question.Id, custom.Id).Description);

        EvaluatorDesignItemViewModel secondKnowledge = viewModel.AddEvaluator(question.Id, EvaluatorType.KnowledgeCoverage);
        Assert.Equal(ExpectedKnowledge, Assert.Single(secondKnowledge.Criteria).Description);
    }

    [Fact]
    public void Changing_evaluator_type_keeps_existing_descriptions_and_applies_new_type_to_later_criteria()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel question = Assert.Single(viewModel.Questions);
        EvaluatorDesignItemViewModel evaluator = Assert.Single(question.Evaluators);
        Assert.Equal(ExpectedKnowledge, Assert.Single(evaluator.Criteria).Description);

        viewModel.ChangeEvaluatorType(question.Id, evaluator.Id, EvaluatorType.PromptAnalysis);
        Assert.Equal(ExpectedKnowledge, evaluator.Criteria[0].Description);
        Assert.Equal(ExpectedPromptAnalysis, viewModel.AddCriterion(question.Id, evaluator.Id).Description);

        viewModel.ChangeEvaluatorType(question.Id, evaluator.Id, EvaluatorType.CustomPrompt);
        Assert.Equal(ExpectedKnowledge, evaluator.Criteria[0].Description);
        Assert.Equal(ExpectedPromptAnalysis, evaluator.Criteria[1].Description);
        Assert.Equal(ExpectedCustom, viewModel.AddCriterion(question.Id, evaluator.Id).Description);
    }

    [Fact]
    public void Edited_description_is_not_overwritten_when_another_criterion_is_added()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel question = Assert.Single(viewModel.Questions);
        EvaluatorDesignItemViewModel evaluator = Assert.Single(question.Evaluators);
        evaluator.Criteria[0].Description = "利用者が編集した説明";

        viewModel.AddCriterion(question.Id, evaluator.Id);

        Assert.Equal("利用者が編集した説明", evaluator.Criteria[0].Description);
    }

    [Fact]
    public async Task Suggested_questions_use_prompt_analysis_text_for_student_prompt_columns_and_knowledge_text_otherwise()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        InputViewModel viewModel = new();

        await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);

        Assert.All(viewModel.DefinitionDraft.Questions, question =>
        {
            EvaluatorDefinition evaluator = Assert.Single(question.Evaluators);
            string expected = evaluator.Type == EvaluatorType.PromptAnalysis
                ? ExpectedPromptAnalysis
                : ExpectedKnowledge;
            Assert.Equal(expected, Assert.Single(evaluator.Criteria).Description);
        });
        Assert.Contains(
            viewModel.DefinitionDraft.Questions,
            question => question.Evaluators[0].Type == EvaluatorType.PromptAnalysis);
        Assert.Contains(
            viewModel.DefinitionDraft.Questions,
            question => question.Evaluators[0].Type == EvaluatorType.KnowledgeCoverage);
        Assert.Empty(viewModel.ValidationErrors);
    }
}

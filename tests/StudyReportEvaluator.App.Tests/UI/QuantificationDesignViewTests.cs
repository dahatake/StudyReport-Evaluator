using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class QuantificationDesignViewTests
{
    [Fact]
    public void Dynamic_question_evaluator_and_criterion_operations_preserve_stable_ids_and_snapshot_isolation()
    {
        QuantificationDesignViewModel viewModel = new();
        Assert.True(viewModel.IsValid);
        QuestionDesignItemViewModel originalQuestion = Assert.Single(viewModel.Questions);
        EvaluatorDesignItemViewModel originalEvaluator = Assert.Single(originalQuestion.Evaluators);
        CriterionDesignItemViewModel originalCriterion = Assert.Single(originalEvaluator.Criteria);
        QuantificationSnapshot activeSnapshot = viewModel.BuildSnapshot();
        QuantificationDefinition beforeMutation = viewModel.Draft;

        QuestionDesignItemViewModel questionCopy = viewModel.DuplicateQuestion(originalQuestion.Id);
        Assert.NotSame(beforeMutation, viewModel.Draft);
        Assert.NotEqual(originalQuestion.Id, questionCopy.Id);
        Assert.NotEqual(originalEvaluator.Id, Assert.Single(questionCopy.Evaluators).Id);
        Assert.NotEqual(originalCriterion.Id, Assert.Single(Assert.Single(questionCopy.Evaluators).Criteria).Id);
        string copiedQuestionId = questionCopy.Id;
        viewModel.MoveQuestionUp(copiedQuestionId);
        Assert.Equal(copiedQuestionId, viewModel.Questions[0].Id);
        Assert.Equal(copiedQuestionId, viewModel.Draft.Questions[0].Id);
        questionCopy.Enabled = false;
        Assert.False(Assert.Single(viewModel.Draft.Questions, question => question.Id == copiedQuestionId).Enabled);
        Assert.Contains(viewModel.Questions, question => question.Id == copiedQuestionId);

        QuantificationSnapshot disabledQuestionSnapshot = viewModel.BuildSnapshot();
        Assert.False(Assert.Single(
            disabledQuestionSnapshot.Definition.Questions,
            question => question.Id == copiedQuestionId).Enabled);
        viewModel.DeleteQuestion(copiedQuestionId);
        Assert.DoesNotContain(viewModel.Questions, question => question.Id == copiedQuestionId);

        EvaluatorDesignItemViewModel evaluatorCopy = viewModel.DuplicateEvaluator(
            originalQuestion.Id,
            originalEvaluator.Id);
        Assert.NotEqual(originalEvaluator.Id, evaluatorCopy.Id);
        Assert.NotEqual(originalCriterion.Id, Assert.Single(evaluatorCopy.Criteria).Id);
        string copiedEvaluatorId = evaluatorCopy.Id;
        viewModel.MoveEvaluatorUp(originalQuestion.Id, copiedEvaluatorId);
        Assert.Equal(copiedEvaluatorId, originalQuestion.Evaluators[0].Id);
        evaluatorCopy.Enabled = false;
        Assert.False(Assert.Single(
            viewModel.Draft.Questions.Single(question => question.Id == originalQuestion.Id).Evaluators,
            evaluator => evaluator.Id == copiedEvaluatorId).Enabled);
        Assert.True(viewModel.BuildSnapshot().Definition.Questions
            .Single(question => question.Id == originalQuestion.Id).Evaluators
            .Any(evaluator => evaluator.Id == copiedEvaluatorId && !evaluator.Enabled));
        viewModel.DeleteEvaluator(originalQuestion.Id, copiedEvaluatorId);
        Assert.DoesNotContain(originalQuestion.Evaluators, evaluator => evaluator.Id == copiedEvaluatorId);

        EvaluatorDesignItemViewModel custom = viewModel.AddEvaluator(
            originalQuestion.Id,
            EvaluatorType.CustomPrompt);
        Assert.Equal(EvaluatorType.CustomPrompt, custom.Type);
        CriterionDesignItemViewModel criterionCopy = viewModel.DuplicateCriterion(
            originalQuestion.Id,
            custom.Id,
            Assert.Single(custom.Criteria).Id);
        string copiedCriterionId = criterionCopy.Id;
        viewModel.MoveCriterionUp(originalQuestion.Id, custom.Id, copiedCriterionId);
        Assert.Equal(copiedCriterionId, custom.Criteria[0].Id);
        criterionCopy.Enabled = false;
        Assert.False(Assert.Single(
            viewModel.Draft.Questions.Single(question => question.Id == originalQuestion.Id).Evaluators
                .Single(evaluator => evaluator.Id == custom.Id).Criteria,
            criterion => criterion.Id == copiedCriterionId).Enabled);
        Assert.True(viewModel.BuildSnapshot().Definition.Questions
            .Single(question => question.Id == originalQuestion.Id).Evaluators
            .Single(evaluator => evaluator.Id == custom.Id).Criteria
            .Any(criterion => criterion.Id == copiedCriterionId && !criterion.Enabled));
        viewModel.DeleteCriterion(originalQuestion.Id, custom.Id, copiedCriterionId);
        Assert.DoesNotContain(custom.Criteria, criterion => criterion.Id == copiedCriterionId);

        QuestionDesignItemViewModel addedQuestion = viewModel.AddQuestion();
        string addedQuestionId = addedQuestion.Id;
        viewModel.DeleteQuestion(addedQuestionId);
        Assert.DoesNotContain(viewModel.Questions, question => question.Id == addedQuestionId);
        Assert.Equal(originalQuestion.Id, activeSnapshot.Definition.Questions[0].Id);
        Assert.Single(activeSnapshot.Definition.Questions);
        Assert.Single(activeSnapshot.Definition.Questions[0].Evaluators);
        Assert.Single(activeSnapshot.Definition.Questions[0].Evaluators[0].Criteria);
        Assert.True(activeSnapshot.HasValidHash());
        Assert.True(viewModel.IsValid);
    }

    [Fact]
    public void Knowledge_prompt_is_immutable_semantic_coverage_and_evaluator_types_are_closed()
    {
        QuantificationDesignViewModel viewModel = new();
        EvaluatorDesignItemViewModel knowledge = Assert.Single(Assert.Single(viewModel.Questions).Evaluators);
        string originalPreview = knowledge.PromptPreview;

        Assert.Equal(EvaluatorType.KnowledgeCoverage, knowledge.Type);
        Assert.Equal("KNOWLEDGE_COVERAGE", knowledge.TypeContractName);
        Assert.True(knowledge.IsPromptReadOnly);
        Assert.Null(knowledge.CustomPromptTemplate);
        Assert.Contains("説明", knowledge.PromptPreview, StringComparison.Ordinal);
        Assert.Contains("関係", knowledge.PromptPreview, StringComparison.Ordinal);
        Assert.Contains("適用", knowledge.PromptPreview, StringComparison.Ordinal);
        Assert.Contains("単語が存在するだけで満点にせず", knowledge.PromptPreview, StringComparison.Ordinal);
        Assert.Contains("APP-OWNED STRUCTURED OUTPUT CONTRACT", knowledge.PromptPreview, StringComparison.Ordinal);

        knowledge.CustomPromptTemplate = "単語数だけを数える PRIVATE-CANARY";

        Assert.Equal(originalPreview, knowledge.PromptPreview);
        Assert.Null(Assert.Single(viewModel.Draft.Questions).Evaluators[0].CustomPromptTemplate);
        Assert.Equal(
            [EvaluatorType.KnowledgeCoverage, EvaluatorType.CustomPrompt],
            QuantificationDesignViewModel.EvaluatorTypes.Select(choice => choice.Type));
        Assert.Equal(
            ["KNOWLEDGE_COVERAGE", "CUSTOM_PROMPT"],
            QuantificationDesignViewModel.EvaluatorTypes.Select(choice => choice.ContractName));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => viewModel.AddEvaluator(Assert.Single(viewModel.Questions).Id, (EvaluatorType)99));
    }

    [Fact]
    public void Root_allocation_defaults_manual_edits_and_explicit_equalize_follow_the_v4_contract()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel first = Assert.Single(viewModel.Questions);

        Assert.Equal(60m, viewModel.BasePoints);
        Assert.Equal(0m, viewModel.SpecialPoints);
        Assert.Equal(0.1m, viewModel.SimilarityPenaltyWeight);
        Assert.Equal(40m, first.Points);
        Assert.Equal(100m, viewModel.AllocationTotal);
        Assert.Equal(0m, viewModel.AllocationRemaining);
        Assert.True(viewModel.IsAllocationValid);

        QuestionDesignItemViewModel second = viewModel.DuplicateQuestion(first.Id);
        Assert.Equal(40m, first.Points);
        Assert.Equal(40m, second.Points);
        Assert.Equal(140m, viewModel.AllocationTotal);
        Assert.False(viewModel.IsAllocationValid);

        viewModel.BasePoints = 50m;
        viewModel.SpecialPoints = 10m;
        viewModel.SimilarityPenaltyWeight = 0.25m;
        viewModel.AddSpecialEvaluation(first.Id);
        Assert.Equal(40m, first.Points);
        Assert.Equal(40m, second.Points);
        Assert.False(viewModel.IsAllocationValid);

        viewModel.EqualizeQuestionPoints();

        Assert.Equal(20m, first.Points);
        Assert.Equal(20m, second.Points);
        Assert.Equal(100m, viewModel.AllocationTotal);
        Assert.True(viewModel.IsAllocationValid);
        Assert.Contains("100", viewModel.AllocationSummary, StringComparison.Ordinal);
        Assert.True(viewModel.BuildSnapshot().HasValidHash());
    }

    [Fact]
    public void Special_items_support_crud_source_mapping_prompt_validation_and_snapshot_isolation()
    {
        QuantificationDesignViewModel viewModel = new(
            initialDefinition: null,
            availableColumnNames: ["A", "B", "C"]);
        QuestionDesignItemViewModel question = Assert.Single(viewModel.Questions);
        viewModel.BasePoints = 50m;
        viewModel.SpecialPoints = 10m;
        SpecialEvaluationDesignItemViewModel special = viewModel.AddSpecialEvaluation(question.Id);

        special.DisplayName = "学生Prompt";
        special.PrimarySourceColumn = "B";
        special.SupportingColumns.Single(item => item.ColumnName == "A").IsSelected = true;
        special.SupportingColumns.Single(item => item.ColumnName == "C").IsSelected = true;
        special.PromptTemplate = "観点に沿って評価してください。{回答} {補助情報}";

        Assert.Equal(["A", "C"], viewModel.Draft.Questions[0].SpecialEvaluations[0].SupportingSourceColumns);
        Assert.False(special.SupportingColumns.Single(item => item.ColumnName == "B").CanSelect);
        Assert.True(viewModel.IsValid);

        SpecialEvaluationDesignItemViewModel copy = viewModel.DuplicateSpecialEvaluation(question.Id, special.Id);
        Assert.NotEqual(special.Id, copy.Id);
        Assert.Equal(special.PromptTemplate, copy.PromptTemplate);
        viewModel.MoveSpecialEvaluationUp(question.Id, copy.Id);
        Assert.Equal(copy.Id, question.SpecialEvaluations[0].Id);
        copy.Enabled = false;
        Assert.False(viewModel.Draft.Questions[0].SpecialEvaluations[0].Enabled);
        viewModel.DeleteSpecialEvaluation(question.Id, copy.Id);
        Assert.Single(question.SpecialEvaluations);

        QuantificationSnapshot snapshot = viewModel.BuildSnapshot();
        special.PromptTemplate = "missing placeholder";
        Assert.False(viewModel.IsValid);
        Assert.Contains(viewModel.ValidationErrors, error =>
            error.NodeId == special.Id && error.Field == "PromptTemplate");
        Assert.Equal(
            "観点に沿って評価してください。{回答} {補助情報}",
            snapshot.Definition.Questions[0].SpecialEvaluations[0].PromptTemplate);

        special.PromptTemplate = "fixed {回答}";
        Assert.True(viewModel.IsValid);
    }

    [Fact]
    public void Custom_template_validation_handles_required_placeholders_unknown_and_escaped_braces()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel question = Assert.Single(viewModel.Questions);
        EvaluatorDesignItemViewModel custom = viewModel.AddEvaluator(question.Id, EvaluatorType.CustomPrompt);

        custom.CustomPromptTemplate = string.Empty;
        AssertPromptError(viewModel, custom.Id, "TEMPLATE_REQUIRED");
        Assert.False(viewModel.TryBuildSnapshot(out _));

        custom.CustomPromptTemplate = "{評価項目}";
        AssertPromptError(viewModel, custom.Id, "ANSWER_PLACEHOLDER_REQUIRED");

        custom.CustomPromptTemplate = "{回答}";
        AssertPromptError(viewModel, custom.Id, "CRITERIA_PLACEHOLDER_REQUIRED");

        custom.CustomPromptTemplate = "{回答} {評価項目} {未知}";
        AssertPromptError(viewModel, custom.Id, "UNKNOWN_PLACEHOLDER");

        custom.CustomPromptTemplate = "{回答} {評価項目";
        AssertPromptError(viewModel, custom.Id, "UNCLOSED_PLACEHOLDER");

        custom.CustomPromptTemplate = "literal {{brace}} / answer={回答} / criteria={評価項目}";

        Assert.DoesNotContain(
            viewModel.ValidationErrors,
            error => error.NodeId == custom.Id && error.Field == "CustomPromptTemplate");
        Assert.Contains("literal {brace}", custom.PromptPreview, StringComparison.Ordinal);
        Assert.Contains("【回答 preview】", custom.PromptPreview, StringComparison.Ordinal);
        Assert.Contains("【評価項目 preview】", custom.PromptPreview, StringComparison.Ordinal);
        Assert.True(viewModel.IsValid);
        Assert.True(viewModel.TryBuildSnapshot(out QuantificationSnapshot? snapshot));
        Assert.NotNull(snapshot);

        const string canary = "PRIVATE-PROMPT-CONTENT-CANARY";
        custom.CustomPromptTemplate = canary;
        DesignValidationError error = Assert.Single(
            viewModel.ValidationErrors,
            item => item.NodeId == custom.Id && item.Code == "ANSWER_PLACEHOLDER_REQUIRED");
        Assert.DoesNotContain(canary, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(canary, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Three_level_weights_and_optional_ranges_show_effective_values_and_block_invalid_snapshot()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel firstQuestion = Assert.Single(viewModel.Questions);
        QuestionDesignItemViewModel secondQuestion = viewModel.DuplicateQuestion(firstQuestion.Id);
        firstQuestion.Weight = 2m;
        secondQuestion.Weight = 1m;

        Assert.Equal(200m / 3m, firstQuestion.EffectiveWeightPercentage);
        Assert.Equal(100m / 3m, secondQuestion.EffectiveWeightPercentage);

        EvaluatorDesignItemViewModel firstEvaluator = Assert.Single(firstQuestion.Evaluators);
        EvaluatorDesignItemViewModel secondEvaluator = viewModel.DuplicateEvaluator(
            firstQuestion.Id,
            firstEvaluator.Id);
        firstEvaluator.Weight = 3m;
        secondEvaluator.Weight = 1m;
        Assert.Equal(75m, firstEvaluator.EffectiveWeightPercentage);
        Assert.Equal(25m, secondEvaluator.EffectiveWeightPercentage);
        secondEvaluator.Enabled = false;
        Assert.Equal(100m, firstEvaluator.EffectiveWeightPercentage);
        Assert.Equal(0m, secondEvaluator.EffectiveWeightPercentage);
        secondEvaluator.Enabled = true;

        CriterionDesignItemViewModel firstCriterion = Assert.Single(firstEvaluator.Criteria);
        CriterionDesignItemViewModel secondCriterion = viewModel.DuplicateCriterion(
            firstQuestion.Id,
            firstEvaluator.Id,
            firstCriterion.Id);
        firstCriterion.Weight = 1m;
        secondCriterion.Weight = 3m;
        Assert.Equal(25m, firstCriterion.EffectiveWeightPercentage);
        Assert.Equal(75m, secondCriterion.EffectiveWeightPercentage);
        Assert.False(firstCriterion.HasCustomRange);
        Assert.Equal(new ScoreRange(0m, 10m), firstCriterion.EffectiveRange);

        firstCriterion.HasCustomRange = true;
        firstCriterion.Minimum = 1m;
        firstCriterion.Maximum = 5m;
        Assert.True(firstCriterion.HasCustomRange);
        Assert.Equal(new ScoreRange(1m, 5m), firstCriterion.EffectiveRange);
        Assert.Equal(new ScoreRange(1m, 5m), viewModel.Draft.Questions
            .Single(question => question.Id == firstQuestion.Id).Evaluators
            .Single(evaluator => evaluator.Id == firstEvaluator.Id).Criteria
            .Single(criterion => criterion.Id == firstCriterion.Id).Range);

        firstCriterion.Maximum = 1m;
        Assert.False(viewModel.CanBuildSnapshot);
        Assert.Contains(
            viewModel.ValidationErrors,
            error => error.NodeId == firstCriterion.Id && error.Field == "Range");
        Assert.Throws<QuantificationDesignValidationException>(() => viewModel.BuildSnapshot());
        firstCriterion.Maximum = 5m;
        firstCriterion.HasCustomRange = false;
        Assert.Equal(new ScoreRange(0m, 10m), firstCriterion.EffectiveRange);

        firstEvaluator.Minimum = 10m;
        Assert.False(viewModel.CanBuildSnapshot);
        firstEvaluator.Minimum = 0m;
        firstQuestion.Weight = -1m;
        Assert.False(viewModel.CanBuildSnapshot);
        Assert.Contains(
            viewModel.ValidationErrors,
            error => error.NodeId == firstQuestion.Id && error.Field == "Points");
        firstQuestion.Weight = 2m;
        viewModel.EqualizeQuestionPoints();
        Assert.True(viewModel.CanBuildSnapshot);
        Assert.True(viewModel.BuildSnapshot().HasValidHash());
    }

    [Fact]
    public void Extreme_positive_weights_show_effective_percentages_without_decimal_overflow()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel firstQuestion = Assert.Single(viewModel.Questions);
        firstQuestion.Weight = decimal.MaxValue;
        QuestionDesignItemViewModel secondQuestion = viewModel.DuplicateQuestion(firstQuestion.Id);
        Assert.Equal(50m, firstQuestion.EffectiveWeightPercentage);
        Assert.Equal(50m, secondQuestion.EffectiveWeightPercentage);

        EvaluatorDesignItemViewModel firstEvaluator = Assert.Single(firstQuestion.Evaluators);
        firstEvaluator.Weight = decimal.MaxValue;
        EvaluatorDesignItemViewModel secondEvaluator = viewModel.DuplicateEvaluator(
            firstQuestion.Id,
            firstEvaluator.Id);
        Assert.Equal(50m, firstEvaluator.EffectiveWeightPercentage);
        Assert.Equal(50m, secondEvaluator.EffectiveWeightPercentage);

        CriterionDesignItemViewModel firstCriterion = Assert.Single(firstEvaluator.Criteria);
        firstCriterion.Weight = decimal.MaxValue;
        CriterionDesignItemViewModel secondCriterion = viewModel.DuplicateCriterion(
            firstQuestion.Id,
            firstEvaluator.Id,
            firstCriterion.Id);
        Assert.Equal(50m, firstCriterion.EffectiveWeightPercentage);
        Assert.Equal(50m, secondCriterion.EffectiveWeightPercentage);
        viewModel.EqualizeQuestionPoints();
        Assert.True(viewModel.CanBuildSnapshot);
    }

    [Fact]
    public void Removing_the_last_enabled_child_is_retained_as_an_invalid_draft_until_fixed()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel question = Assert.Single(viewModel.Questions);
        EvaluatorDesignItemViewModel evaluator = Assert.Single(question.Evaluators);
        CriterionDesignItemViewModel criterion = Assert.Single(evaluator.Criteria);

        viewModel.DeleteCriterion(question.Id, evaluator.Id, criterion.Id);

        Assert.Empty(evaluator.Criteria);
        Assert.Empty(viewModel.Draft.Questions[0].Evaluators[0].Criteria);
        Assert.False(viewModel.TryBuildSnapshot(out QuantificationSnapshot? snapshot));
        Assert.Null(snapshot);
        Assert.Contains(
            viewModel.ValidationErrors,
            error => error.NodeId == evaluator.Id && error.Code == "ENABLED_CRITERION_REQUIRED");

        viewModel.AddCriterion(question.Id, evaluator.Id);
        Assert.True(viewModel.CanBuildSnapshot);
    }

    [Fact]
    public void Design_contract_has_no_warning_acknowledgement_state()
    {
        string[] prohibitedTerms = ["Warning", "Ethics", "Acknowledge", "Consent", "Dismiss"];
        MemberInfo[] members = typeof(QuantificationDesignViewModel).GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(
            members,
            member => prohibitedTerms.Any(term => member.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    [AvaloniaFact]
    public void Large_valid_definition_realizes_question_summaries_and_virtualizes_nested_navigators()
    {
        QuantificationDesignViewModel viewModel = new(CreateLargeDefinition());
        QuantificationDesignView view = new(viewModel);
        Window window = new()
        {
            Width = 1260,
            Height = 900,
            Content = view,
        };

        try
        {
            window.Show();
            window.SetRenderScaling(2d);
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.IsValid);
            Assert.Equal(10, viewModel.Questions.Count);
            Assert.Equal(50, viewModel.Questions.Sum(question => question.Evaluators.Count));
            Assert.Equal(
                1_000,
                viewModel.Questions.Sum(question => question.Evaluators.Sum(evaluator => evaluator.Criteria.Count)));
            Assert.True(viewModel.BuildSnapshot().HasValidHash());
            Assert.Equal(
                10,
                view.GetVisualDescendants().OfType<Border>().Count(border =>
                    (AutomationProperties.GetAutomationId(border) ?? string.Empty).StartsWith(
                        "DesignQuestion-",
                        StringComparison.Ordinal)));
            Assert.True(view.GetVisualDescendants().OfType<VirtualizingStackPanel>().Count() >= 3);
            Assert.InRange(view.GetVisualDescendants().OfType<TextBox>().Count(), 1, 60);
            Assert.Equal(2d, window.RenderScaling);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Formula_guide_remains_fixed_and_reflects_current_values()
    {
        QuantificationDesignViewModel viewModel = new();
        QuantificationDesignView view = new(viewModel);
        Window window = new()
        {
            Width = 1260,
            Height = 900,
            Content = view,
        };

        try
        {
            window.Show();
            Render();

            Required<Expander>(view, "FormulaDetailsExpander").IsExpanded = true;
            Render();

            ScrollViewer scroll = Required<ScrollViewer>(view, "QuantificationDesignScrollViewer");
            Border formulaGuide = Required<Border>(view, "FormulaGuide");
            Border formulaDetails = Required<Border>(view, "FormulaDetails");
            TextBlock baseValue = RequiredByAutomationId<TextBlock>(view, "FormulaBaseValue");
            TextBlock specialValue = RequiredByAutomationId<TextBlock>(view, "FormulaSpecialValue");
            TextBlock similarityValue = RequiredByAutomationId<TextBlock>(view, "FormulaSimilarityValue");

            Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
            Assert.DoesNotContain(
                formulaGuide.GetVisualAncestors(),
                ancestor => ReferenceEquals(ancestor, scroll));
            Assert.Equal("60", baseValue.Text);
            Assert.Equal("0", specialValue.Text);
            Assert.Equal("0.1", similarityValue.Text);
            Assert.Contains("計算の各段階", ToolTip.GetTip(Required<TextBox>(view, "RoundingDigitsTextBox"))?.ToString() ?? string.Empty);
            Assert.Contains(
                formulaDetails.GetVisualDescendants().OfType<TextBlock>(),
                text => string.Equals(text.Text, "設問獲得点", StringComparison.Ordinal));
            Assert.Contains(
                formulaDetails.GetVisualDescendants().OfType<TextBlock>(),
                text => string.Equals(text.Text, "FinalRaw", StringComparison.Ordinal));
            Assert.Contains(
                formulaDetails.GetVisualDescendants().OfType<TextBlock>(),
                text => string.Equals(text.Text, "FinalScore", StringComparison.Ordinal));
            TextBlock earnedLabel = Assert.Single(
                formulaDetails.GetVisualDescendants().OfType<TextBlock>(),
                text => string.Equals(text.Text, "設問獲得点", StringComparison.Ordinal));
            TextBlock penaltyLabel = Assert.Single(
                formulaDetails.GetVisualDescendants().OfType<TextBlock>(),
                text => string.Equals(text.Text, "設問減点", StringComparison.Ordinal));
            Assert.Contains(
                "通常評価率 Rq",
                ToolTip.GetTip(earnedLabel)?.ToString() ?? string.Empty,
                StringComparison.Ordinal);
            Assert.Contains(
                "類似度 Lq",
                ToolTip.GetTip(penaltyLabel)?.ToString() ?? string.Empty,
                StringComparison.Ordinal);
            Assert.Contains(
                formulaGuide.GetVisualDescendants().OfType<TextBlock>(),
                text => (text.Text ?? string.Empty).Contains(
                    "FinalRaw がblankならblank",
                    StringComparison.Ordinal));
            Assert.Contains(
                formulaDetails.GetVisualDescendants().OfType<Border>(),
                border => (ToolTip.GetTip(border)?.ToString() ?? string.Empty).Contains(
                    "有効設問間でも等分平均",
                    StringComparison.Ordinal));

            Point before = formulaGuide.TranslatePoint(default, window)
                ?? throw new InvalidOperationException("Formula guide position is unavailable.");
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            scroll.Offset = new Vector(0d, Math.Min(300d, scroll.Extent.Height - scroll.Viewport.Height));
            Render();
            Point after = formulaGuide.TranslatePoint(default, window)
                ?? throw new InvalidOperationException("Formula guide position is unavailable after scrolling.");
            Assert.InRange(Math.Abs(after.Y - before.Y), 0d, 1d);

            viewModel.BasePoints = 55m;
            viewModel.SpecialPoints = 5m;
            viewModel.SimilarityPenaltyWeight = 0.25m;
            Render();

            Assert.Equal("55", baseValue.Text);
            Assert.Equal("5", specialValue.Text);
            Assert.Equal("0.25", similarityValue.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Many_long_question_names_leave_a_usable_editor_viewport()
    {
        QuantificationDefinition seed = new QuantificationDesignViewModel().Draft;
        QuantificationDefinition definition = seed with
        {
            Questions = [.. Enumerable.Range(1, 40).Select(index => seed.Questions[0] with
            {
                Id = $"question-{index}",
                DisplayName = new string('設', 256) + index,
                Points = 1m,
                Evaluators = [seed.Questions[0].Evaluators[0] with
                {
                    Id = $"evaluator-{index}",
                    Criteria = [seed.Questions[0].Evaluators[0].Criteria[0] with { Id = $"criterion-{index}" }],
                }],
            })],
        };
        QuantificationDesignViewModel viewModel = new(definition);
        QuantificationDesignView view = new(viewModel);
        Window window = new() { Width = 760, Height = 600, Content = view };
        try
        {
            window.Show();
            Render();
            ScrollViewer scroll = Required<ScrollViewer>(view, "QuantificationDesignScrollViewer");
            Assert.True(scroll.Viewport.Height >= 200d,
                $"Question names consumed the editor viewport: {scroll.Viewport.Height:F1} DIP.");

            Button validate = RequiredByAutomationId<Button>(view, "ValidateDesignDraft");
            validate.BringIntoView();
            Render();
            Point origin = validate.TranslatePoint(default, scroll)!.Value;
            Assert.InRange(origin.Y, 0d, scroll.Viewport.Height - validate.Bounds.Height + 1d);
            Assert.True(validate.Focus(NavigationMethod.Tab));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Multiple_questions_render_together_and_identify_the_prompt_target()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel first = Assert.Single(viewModel.Questions);
        first.DisplayName = "設問A";
        QuestionDesignItemViewModel second = viewModel.DuplicateQuestion(first.Id);
        second.DisplayName = "設問B";
        viewModel.EqualizeQuestionPoints();
        EvaluatorDesignItemViewModel custom = viewModel.AddEvaluator(first.Id, EvaluatorType.CustomPrompt);
        first.SelectedEvaluator = custom;
        viewModel.SelectedQuestion = first;
        QuantificationDesignView view = new(viewModel);
        Window window = new()
        {
            Width = 1260,
            Height = 900,
            Content = view,
        };

        try
        {
            window.Show();
            Render();

            WrapPanel questionPanel = Assert.Single(
                view.GetVisualDescendants().OfType<WrapPanel>(),
                panel => AutomationProperties.GetAutomationId(panel) == "QuestionCardPanel");
            TextBlock promptTarget = RequiredByAutomationId<TextBlock>(view, "SelectedPromptTargetSummary");

            Assert.Equal(Orientation.Horizontal, questionPanel.Orientation);
            Assert.Equal($"設問A → Custom: {custom.DisplayName}", promptTarget.Text);
            Assert.Equal(
                2,
                view.GetVisualDescendants().OfType<Border>().Count(border =>
                    (AutomationProperties.GetAutomationId(border) ?? string.Empty).StartsWith(
                        "DesignQuestion-",
                        StringComparison.Ordinal)));

            SpecialEvaluationDesignItemViewModel special = viewModel.AddSpecialEvaluation(first.Id);
            first.SelectedSpecialEvaluation = special;
            viewModel.SelectedPromptTarget = ImportedPromptTarget.SpecialEvaluation;
            Render();
            Assert.Equal($"設問A → 固有評価: {special.DisplayName}", promptTarget.Text);

            viewModel.SelectedPromptTarget = ImportedPromptTarget.CustomEvaluator;

            viewModel.SelectedQuestion = second;
            Render();

            Assert.Equal("設問B → Custom評価方法を選択してください", promptTarget.Text);
            ListBoxItem selectedCard = Assert.Single(
                view.GetVisualDescendants().OfType<ListBoxItem>(),
                item => ReferenceEquals(item.DataContext, second));
            Assert.True(selectedCard.IsSelected);
            Assert.Equal(new Thickness(3d), selectedCard.BorderThickness);

            viewModel.DeleteQuestion(second.Id);
            Render();

            Assert.Same(first, viewModel.SelectedQuestion);
            Assert.Equal($"設問A → Custom: {custom.DisplayName}", promptTarget.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Standalone_design_view_exposes_read_only_and_editable_prompt_surfaces_with_keyboard_and_two_hundred_percent_scroll()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel question = Assert.Single(viewModel.Questions);
        EvaluatorDesignItemViewModel knowledge = Assert.Single(question.Evaluators);
        EvaluatorDesignItemViewModel custom = viewModel.AddEvaluator(question.Id, EvaluatorType.CustomPrompt);
        QuantificationDesignView view = new(viewModel);
        Window window = new()
        {
            Width = 1260,
            Height = 900,
            Content = view,
        };

        try
        {
            window.Show();
            window.SetRenderScaling(2d);
            Dispatcher.UIThread.RunJobs();

            TextBox name = Required<TextBox>(view, "DefinitionNameTextBox");
            TextBox basePoints = Required<TextBox>(view, "BasePointsTextBox");
            TextBox specialPoints = Required<TextBox>(view, "SpecialPointsTextBox");
            TextBox similarityWeight = Required<TextBox>(view, "SimilarityPenaltyWeightTextBox");
            Button equalize = Required<Button>(view, "EqualizeQuestionPointsButton");
            ScrollViewer scroll = Required<ScrollViewer>(view, "QuantificationDesignScrollViewer");
            ListBox questions = Required<ListBox>(view, "QuestionEditorList");
            Border validation = Required<Border>(view, "DesignValidationSummary");
            TextBox knowledgePreview = Assert.Single(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => AutomationProperties.GetAutomationId(textBox) == knowledge.PromptPreviewAutomationId);

            Assert.Same(viewModel, view.DataContext);
            Assert.Equal(2d, window.RenderScaling);
            Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
            Assert.Equal("DesignQuestions", AutomationProperties.GetAutomationId(questions));
            Assert.Equal("DesignValidationSummary", AutomationProperties.GetAutomationId(validation));
            Assert.Equal("DesignBasePoints", AutomationProperties.GetAutomationId(basePoints));
            Assert.Equal("DesignSpecialPoints", AutomationProperties.GetAutomationId(specialPoints));
            Assert.Equal("DesignSimilarityPenaltyWeight", AutomationProperties.GetAutomationId(similarityWeight));
            Assert.Equal("EqualizeQuestionPoints", AutomationProperties.GetAutomationId(equalize));
            Assert.Same(name, window.FocusManager?.GetFocusedElement());
            Assert.True(name.MinHeight >= 44d);
            Assert.True(knowledgePreview.IsVisible);
            Assert.True(knowledgePreview.IsReadOnly);
            Assert.Contains("説明", knowledgePreview.Text ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains(view.GetVisualDescendants(), descendant => descendant is VirtualizingStackPanel);
            Assert.Null(view.FindControl<Border>("EthicsWarningBanner"));

            question.SelectedEvaluator = custom;
            Dispatcher.UIThread.RunJobs();
            TextBox customEditor = Assert.Single(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => AutomationProperties.GetAutomationId(textBox) == custom.PromptAutomationId);
            TextBox customPreview = Assert.Single(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => AutomationProperties.GetAutomationId(textBox) == custom.PromptPreviewAutomationId);
            Assert.False(customEditor.IsReadOnly);
            Assert.True(customEditor.IsVisible);
            Assert.True(customPreview.IsVisible);
            Assert.True(customPreview.IsReadOnly);

            Press(window, Key.Tab);
            Assert.NotSame(name, window.FocusManager?.GetFocusedElement());
            Assert.True(customEditor.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Assert.Same(customEditor, window.FocusManager?.GetFocusedElement());
            Assert.Equal(new Thickness(3d), customEditor.BorderThickness);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertPromptError(
        QuantificationDesignViewModel viewModel,
        string evaluatorId,
        string expectedCode) =>
        Assert.Contains(
            viewModel.ValidationErrors,
            error => error.NodeId == evaluatorId
                && error.Field == "CustomPromptTemplate"
                && error.Code == expectedCode);

    private static QuantificationDefinition CreateLargeDefinition() => new()
    {
        Id = "definition-large",
        Name = "Large virtualized design",
        Revision = "1",
        SourceSheet = "Original",
        HeaderRow = 1,
        FirstDataRow = 2,
        LastDataRow = 531,
        BasePoints = 45m,
        RoundingDigits = 1,
        Questions =
        [
            .. Enumerable.Range(1, 10).Select(question => new QuestionDefinition
            {
                Id = $"question-{question}",
                DisplayName = $"Question {question}",
                QuestionText = $"Question text {question}",
                PrimarySourceColumn = "A",
                SupportingSourceColumns = [],
                Points = question,
                Enabled = true,
                Evaluators =
                [
                    .. Enumerable.Range(1, 5).Select(evaluator => new EvaluatorDefinition
                    {
                        Id = $"evaluator-{question}-{evaluator}",
                        DisplayName = $"Knowledge {question}-{evaluator}",
                        Type = EvaluatorType.KnowledgeCoverage,
                        Weight = evaluator,
                        Range = new ScoreRange(0m, 10m),
                        BuiltInTemplateVersion = BuiltInPromptTemplates.KnowledgeTemplateVersion,
                        CustomPromptTemplate = null,
                        Enabled = true,
                        Criteria =
                        [
                            .. Enumerable.Range(1, 20).Select(criterion => new CriterionDefinition
                            {
                                Id = $"criterion-{question}-{evaluator}-{criterion}",
                                DisplayName = $"Criterion {question}-{evaluator}-{criterion}",
                                Description = "説明・関係・適用を確認する知識ポイント",
                                Weight = criterion,
                                Range = null,
                                Enabled = true,
                            }),
                        ],
                    }),
                ],
            }),
        ],
    };

    private static T Required<T>(Control root, string name)
        where T : Control =>
        Assert.IsType<T>(root.FindControl<T>(name));

    private static T RequiredByAutomationId<T>(Control root, string automationId)
        where T : Control =>
        Assert.Single(
            root.GetVisualDescendants().OfType<T>(),
            control => AutomationProperties.GetAutomationId(control) == automationId);

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(TopLevel window, Key key)
    {
        PhysicalKey physicalKey = key switch
        {
            Key.Tab => PhysicalKey.Tab,
            Key.Enter => PhysicalKey.Enter,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported test key."),
        };
        window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
        window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
        Dispatcher.UIThread.RunJobs();
    }
}
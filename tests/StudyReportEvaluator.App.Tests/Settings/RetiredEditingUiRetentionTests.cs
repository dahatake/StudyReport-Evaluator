using StudyReportEvaluator.App.Settings;
using StudyReportEvaluator.App.Tests.Workflow;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Scoring;
using StudyReportEvaluator.Core.Serialization;
using StudyReportEvaluator.Core.Validation;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Settings;

/// <summary>
/// FR-003 AC-058: 入力詳細・固有評価・読込Prompt の編集UIを廃止しても、固有評価項目・補助列を含む
/// 既存の採点定義は読込・保存・計算できる。
/// </summary>
public sealed class RetiredEditingUiRetentionTests
{
    private static QuantificationDefinition CreateDefinitionWithSpecialsAndSupportingColumns()
    {
        SpecialEvaluationDefinition special = new()
        {
            Id = "S-1",
            DisplayName = "固有 1",
            PrimarySourceColumn = "C",
            SupportingSourceColumns = ["D", "B"],
            PromptTemplate = "固有 {回答}",
        };
        QuestionDefinition question = U01TestSupport.Question(
            "Q-1", "B", ["D", "C"], true, U01TestSupport.Evaluator("E-1", "C-1")) with
        {
            Points = 20m,
            SpecialEvaluations = [special, special with { Id = "S-2", SupportingSourceColumns = [], Enabled = false }],
        };
        return U01TestSupport.Definition(3, 23, question) with
        {
            Id = "DEF-AC058",
            SpecialPoints = 10m,
            BasePoints = 70m,
        };
    }

    // FR-003 AC-058: 固有評価項目と補助列を含む定義を読み込んで保存し直しても、内容は失われない。
    [Fact]
    public async Task FR003_AC058_Definition_with_special_items_and_supporting_columns_survives_save_and_reload()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ac058-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "setting.txt");
        try
        {
            QuantificationDefinition definition = CreateDefinitionWithSpecialsAndSupportingColumns();
            Assert.True(new QuantificationDefinitionValidator().Validate(definition).IsValid);
            CanonicalDefinitionSerializer canonical = new();
            string hash = canonical.ComputeSha256(definition);
            SettingsFileStore store = new(path);

            SettingsSaveResult saved = await store.SaveAsync(
                new ApplicationSettings { Definition = definition }, TestContext.Current.CancellationToken);
            Assert.True(saved.IsSuccess);
            SettingsLoadResult loaded = await new SettingsFileStore(path).LoadAsync(TestContext.Current.CancellationToken);
            QuantificationDefinition restored = Assert.IsType<QuantificationDefinition>(loaded.Settings!.Definition);

            Assert.Equal(hash, canonical.ComputeSha256(restored));
            QuestionDefinition question = Assert.Single(restored.Questions);
            Assert.Equal(["D", "C"], question.SupportingSourceColumns);
            Assert.Equal(2, question.SpecialEvaluations.Length);
            Assert.Equal("C", question.SpecialEvaluations[0].PrimarySourceColumn);
            Assert.Equal(["D", "B"], question.SpecialEvaluations[0].SupportingSourceColumns);
            Assert.False(question.SpecialEvaluations[1].Enabled);
            Assert.Equal(10m, restored.SpecialPoints);

            // 保存し直しても同じ内容（往復で変化しない）。
            Assert.True((await new SettingsFileStore(path).SaveAsync(
                loaded.Settings, TestContext.Current.CancellationToken)).IsSuccess);
            SettingsLoadResult again = await new SettingsFileStore(path).LoadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(hash, canonical.ComputeSha256(again.Settings!.Definition!));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // FR-003 AC-058: 固有配点を含む獲得点・FinalRaw・FinalScore は、廃止の前後で同じ手計算の値になる（固有配点を0固定にしない）。
    [Fact]
    public void FR003_AC058_Special_points_still_contribute_to_earned_points_FinalRaw_and_FinalScore()
    {
        WeightedScoreCalculator calculator = new();
        QuantificationDefinition definition = CreateDefinitionWithSpecialsAndSupportingColumns();
        Assert.Equal(10m, definition.SpecialPoints);

        decimal? rate = calculator.QuestionRate(answerPresent: true, 50m);
        decimal? earned = calculator.QuestionEarned(rate, definition.Questions[0].Points, 2);
        decimal? specialRate = calculator.SpecialQuestionRate([0.8m, 0.6m], 2);
        decimal? specialEarned = calculator.SpecialEarned(definition.SpecialPoints, [specialRate], 2);
        decimal? finalRaw = calculator.FinalRaw(definition.BasePoints, [earned], specialEarned, [0m], 2);

        Assert.Equal(10m, earned);
        Assert.Equal(0.7m, specialRate);
        Assert.Equal(7m, specialEarned);
        Assert.Equal(87m, finalRaw);
        Assert.Equal(87m, QuantificationScoreFinal(finalRaw));
    }

    private static decimal? QuantificationScoreFinal(decimal? finalRaw) => WeightedScoreCalculator.FinalScore(finalRaw);
}

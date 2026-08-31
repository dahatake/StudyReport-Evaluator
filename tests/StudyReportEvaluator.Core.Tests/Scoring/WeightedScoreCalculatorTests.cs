using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Scoring;
using Xunit;

namespace StudyReportEvaluator.Core.Tests.Scoring;

public sealed class WeightedScoreCalculatorTests
{
    private readonly WeightedScoreCalculator _calculator = new();

    [Fact]
    public void Hand_calculated_criterion_and_evaluator_oracle_matches_rounded_child_contract()
    {
        decimal? criterionA = _calculator.Normalize(25m, new ScoreRange(0m, 30m), 1);
        decimal? criterionB = _calculator.Normalize(8m, new ScoreRange(1m, 10m), 1);
        decimal? evaluator = _calculator.Aggregate(
            [new WeightedScoreInput(criterionA, 2m), new WeightedScoreInput(criterionB, 1m)],
            1);

        Assert.Equal(83.3m, criterionA);
        Assert.Equal(77.8m, criterionB);
        Assert.Equal(81.5m, evaluator);
    }

    [Fact]
    public void Empty_primary_is_unscorable_and_override_cannot_create_a_score()
    {
        EffectiveRawSelection selection = _calculator.SelectEffectiveRaw(
            scorable: false,
            aiRaw: 7m,
            overrideValue: 5m,
            new ScoreRange(0m, 10m));

        Assert.Equal(EffectiveRawStatus.Unscorable, selection.Status);
        Assert.Null(selection.Value);
        Assert.Null(_calculator.Normalize(selection.Value, new ScoreRange(0m, 10m), 1));
    }

    [Fact]
    public void Missing_ai_and_override_remain_blank_never_zero()
    {
        EffectiveRawSelection selection = _calculator.SelectEffectiveRaw(true, null, null, new ScoreRange(0m, 10m));

        Assert.Equal(EffectiveRawStatus.Missing, selection.Status);
        Assert.Null(selection.Value);
        Assert.Null(_calculator.Aggregate([new WeightedScoreInput(null, 1m)], 1));
    }

    [Fact]
    public void Valid_override_wins_even_when_ai_is_invalid_or_missing()
    {
        EffectiveRawSelection invalidAi = _calculator.SelectEffectiveRaw(true, 11m, 5m, new ScoreRange(0m, 10m));
        EffectiveRawSelection missingAi = _calculator.SelectEffectiveRaw(true, null, 5m, new ScoreRange(0m, 10m));

        Assert.Equal(new EffectiveRawSelection(5m, EffectiveRawStatus.Override), invalidAi);
        Assert.Equal(new EffectiveRawSelection(5m, EffectiveRawStatus.Override), missingAi);
        Assert.Equal(50.0m, _calculator.Normalize(invalidAi.Value, new ScoreRange(0m, 10m), 1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Invalid_override_stays_blank_and_never_falls_back_to_ai(int overrideValue)
    {
        EffectiveRawSelection selection = _calculator.SelectEffectiveRaw(true, 5m, overrideValue, new ScoreRange(0m, 10m));

        Assert.Equal(EffectiveRawStatus.InvalidOverride, selection.Status);
        Assert.Null(selection.Value);
    }

    [Fact]
    public void Disabled_children_are_excluded_but_enabled_blank_propagates()
    {
        decimal? excludesDisabled = _calculator.Aggregate(
            [new WeightedScoreInput(80m, 1m), new WeightedScoreInput(null, 999m, Enabled: false)],
            1);
        decimal? propagatesEnabledBlank = _calculator.Aggregate(
            [new WeightedScoreInput(80m, 1m), new WeightedScoreInput(null, 1m)],
            1);

        Assert.Equal(80m, excludesDisabled);
        Assert.Null(propagatesEnabledBlank);
    }

    [Fact]
    public void Asymmetric_weights_are_normalized_without_requiring_a_total_of_one_hundred()
    {
        decimal? result = _calculator.Aggregate(
            [new WeightedScoreInput(20m, 2m), new WeightedScoreInput(80m, 8m)],
            1);

        Assert.Equal(68m, result);
    }

    [Theory]
    [InlineData(81.45, 81.5)]
    [InlineData(-81.45, -81.5)]
    public void Midpoints_round_away_from_zero(double input, double expected)
    {
        Assert.Equal((decimal)expected, _calculator.Round((decimal)input, 1));
    }

    [Fact]
    public void Very_large_weights_do_not_overflow_the_preview()
    {
        decimal? result = _calculator.Aggregate(
            [new WeightedScoreInput(50m, decimal.MaxValue), new WeightedScoreInput(100m, decimal.MaxValue)],
            1);

        Assert.Equal(75m, result);
    }

    [Fact]
    public void Extreme_but_valid_decimal_range_does_not_overflow_normalization()
    {
        decimal? result = _calculator.Normalize(
            0m,
            new ScoreRange(decimal.MinValue, decimal.MaxValue),
            1);

        Assert.Equal(50.0m, result);
    }
}

using System.Collections.Immutable;
using System.Numerics;
using StudyReportEvaluator.Core.Domain;

namespace StudyReportEvaluator.Core.Scoring;

public enum EffectiveRawStatus
{
    Unscorable,
    AiRaw,
    Override,
    Missing,
    InvalidAiRaw,
    InvalidOverride,
}

public readonly record struct EffectiveRawSelection(decimal? Value, EffectiveRawStatus Status);

public readonly record struct WeightedScoreInput(decimal? Score, decimal Weight, bool Enabled = true);

public sealed class WeightedScoreCalculator
{
    public EffectiveRawSelection SelectEffectiveRaw(
        bool scorable,
        decimal? aiRaw,
        decimal? overrideValue,
        ScoreRange range)
    {
        if (!scorable)
        {
            return new EffectiveRawSelection(null, EffectiveRawStatus.Unscorable);
        }

        if (overrideValue is decimal manual)
        {
            return IsInRange(manual, range)
                ? new EffectiveRawSelection(manual, EffectiveRawStatus.Override)
                : new EffectiveRawSelection(null, EffectiveRawStatus.InvalidOverride);
        }

        if (aiRaw is decimal ai)
        {
            return IsInRange(ai, range)
                ? new EffectiveRawSelection(ai, EffectiveRawStatus.AiRaw)
                : new EffectiveRawSelection(null, EffectiveRawStatus.InvalidAiRaw);
        }

        return new EffectiveRawSelection(null, EffectiveRawStatus.Missing);
    }

    public decimal? Normalize(decimal? effectiveRaw, ScoreRange range, int roundingDigits)
    {
        ValidateRoundingDigits(roundingDigits);
        if (effectiveRaw is not decimal raw || !IsInRange(raw, range) || range.Minimum >= range.Maximum)
        {
            return null;
        }

        return NormalizeExactly(raw, range, roundingDigits);
    }

    public decimal? Aggregate(IEnumerable<WeightedScoreInput> children, int roundingDigits)
    {
        ArgumentNullException.ThrowIfNull(children);
        ValidateRoundingDigits(roundingDigits);

        ImmutableArray<WeightedScoreInput> enabled = children.Where(child => child.Enabled).ToImmutableArray();
        if (enabled.IsEmpty || enabled.Any(child => child.Score is null))
        {
            return null;
        }

        foreach (WeightedScoreInput child in enabled)
        {
            if (child.Weight <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(children), child.Weight, "Enabled child weights must be positive.");
            }
        }

        decimal maximumWeight = enabled.Max(child => child.Weight);
        decimal scaledWeightTotal = 0m;
        decimal scaledWeightedScoreTotal = 0m;
        foreach (WeightedScoreInput child in enabled)
        {
            decimal scaledWeight = child.Weight / maximumWeight;
            scaledWeightTotal += scaledWeight;
            scaledWeightedScoreTotal += child.Score!.Value * scaledWeight;
        }

        return Round(scaledWeightedScoreTotal / scaledWeightTotal, roundingDigits);
    }

    public decimal Round(decimal value, int roundingDigits)
    {
        ValidateRoundingDigits(roundingDigits);
        return decimal.Round(value, roundingDigits, MidpointRounding.AwayFromZero);
    }

    private static bool IsInRange(decimal value, ScoreRange range) =>
        range.Minimum < range.Maximum && value >= range.Minimum && value <= range.Maximum;

    private static decimal NormalizeExactly(decimal raw, ScoreRange range, int roundingDigits)
    {
        (BigInteger rawValue, int rawScale) = ToIntegerAndScale(raw);
        (BigInteger minimumValue, int minimumScale) = ToIntegerAndScale(range.Minimum);
        (BigInteger maximumValue, int maximumScale) = ToIntegerAndScale(range.Maximum);
        int commonScale = Math.Max(rawScale, Math.Max(minimumScale, maximumScale));
        rawValue *= BigInteger.Pow(10, commonScale - rawScale);
        minimumValue *= BigInteger.Pow(10, commonScale - minimumScale);
        maximumValue *= BigInteger.Pow(10, commonScale - maximumScale);

        BigInteger numerator = rawValue - minimumValue;
        BigInteger denominator = maximumValue - minimumValue;
        BigInteger decimalPlaces = BigInteger.Pow(10, roundingDigits);
        BigInteger scaledNumerator = numerator * 100 * decimalPlaces;
        BigInteger quotient = BigInteger.DivRem(scaledNumerator, denominator, out BigInteger remainder);
        if ((remainder * 2) >= denominator)
        {
            quotient++;
        }

        return (decimal)quotient / (decimal)decimalPlaces;
    }

    private static (BigInteger Value, int Scale) ToIntegerAndScale(decimal value)
    {
        int[] bits = decimal.GetBits(value);
        BigInteger integer = (uint)bits[0]
            | ((BigInteger)(uint)bits[1] << 32)
            | ((BigInteger)(uint)bits[2] << 64);
        bool negative = (bits[3] & int.MinValue) != 0;
        int scale = (bits[3] >> 16) & 0x7F;
        return (negative ? -integer : integer, scale);
    }

    private static void ValidateRoundingDigits(int roundingDigits)
    {
        if (roundingDigits is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(roundingDigits), roundingDigits, "Rounding digits must be between 0 and 6.");
        }
    }
}

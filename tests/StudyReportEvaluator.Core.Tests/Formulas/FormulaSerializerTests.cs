using StudyReportEvaluator.Core.Formulas;
using Xunit;

namespace StudyReportEvaluator.Core.Tests.Formulas;

public sealed class FormulaSerializerTests
{
    private readonly FormulaSerializer _serializer = new();

    [Fact]
    public void Effective_raw_formula_is_blank_safe_range_safe_and_override_first()
    {
        FormulaExpression formula = FormulaExpressions.EffectiveRaw(
            Ref("Results", "A", 2),
            Ref("Results", "B", 2),
            Ref("Results", "C", 2),
            Ref("Config", "D", 2, absolute: true),
            Ref("Config", "E", 2, absolute: true));

        string serialized = _serializer.Serialize(formula);

        Assert.Equal(
            "=IF(('Results'!A2<>1),\"\",IF(('Results'!C2=\"\"),IF(ISNUMBER('Results'!B2),IF(('Results'!B2<'Config'!$D$2),\"\",IF(('Results'!B2>'Config'!$E$2),\"\",'Results'!B2)),\"\"),IF(ISNUMBER('Results'!C2),IF(('Results'!C2<'Config'!$D$2),\"\",IF(('Results'!C2>'Config'!$E$2),\"\",'Results'!C2)),\"\")))",
            serialized);
        Assert.DoesNotContain("5", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalized_formula_uses_only_config_refs_and_blank_propagation()
    {
        string serialized = _serializer.Serialize(FormulaExpressions.Normalized(
            Ref("Results", "D", 2),
            Ref("Config", "A", 2, absolute: true),
            Ref("Config", "B", 2, absolute: true),
            Ref("Config", "C", 1, absolute: true)));

        Assert.Equal(
            "=IF(ISNUMBER('Results'!D2),IFERROR(ROUND(((('Results'!D2-'Config'!$A$2)/('Config'!$B$2-'Config'!$A$2))*100),'Config'!$C$1),\"\"),\"\")",
            serialized);
    }

    [Fact]
    public void Aggregate_uses_pairwise_score_weight_terms_and_count_blank_guard()
    {
        string serialized = _serializer.Serialize(FormulaExpressions.Aggregate(
            [
                new WeightedFormulaChild(Ref("Results", "A", 2), Ref("Config", "B", 2, true)),
                new WeightedFormulaChild(Ref("Results", "C", 2), Ref("Config", "D", 2, true)),
            ],
            Ref("Config", "E", 1, true)));

        Assert.Equal(
            "=IF((COUNT('Results'!A2,'Results'!C2)=2),IFERROR(ROUND((SUM(('Results'!A2*'Config'!$B$2),('Results'!C2*'Config'!$D$2))/SUM('Config'!$B$2,'Config'!$D$2)),'Config'!$E$1),\"\"),\"\")",
            serialized);
    }

    [Fact]
    public void Aggregate_excludes_disabled_children_from_count_numerator_and_denominator()
    {
        string serialized = _serializer.Serialize(FormulaExpressions.Aggregate(
            [
                new WeightedFormulaChild(Ref("Results", "A", 2), Ref("Config", "B", 2, true)),
                new WeightedFormulaChild(Ref("Results", "C", 2), Ref("Config", "D", 2, true), Enabled: false),
            ],
            Ref("Config", "E", 1, true)));

        Assert.Contains("COUNT('Results'!A2)=1", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("'Results'!C2", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("'Config'!$D$2", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Sheet_apostrophes_are_escaped_as_Excel_sheet_references()
    {
        string serialized = _serializer.Serialize(new FormulaCell(Ref("Run's Data", "a", 1)));

        Assert.Equal("='Run''s Data'!A1", serialized);
    }

    private static FormulaCellReference Ref(
        string sheet,
        string column,
        int row,
        bool absolute = false) =>
        new(new FormulaCellAddress(sheet, column, row), absolute, absolute);
}

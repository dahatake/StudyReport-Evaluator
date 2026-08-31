using System.Collections.Immutable;

namespace StudyReportEvaluator.Core.Formulas;

public abstract record FormulaExpression;

public sealed record FormulaNumber(decimal Value) : FormulaExpression;

public sealed record FormulaBlank : FormulaExpression
{
    public static FormulaBlank Value { get; } = new();

    private FormulaBlank()
    {
    }
}

public sealed record FormulaCellAddress(string SheetName, string ColumnName, int RowNumber)
{
    public string NormalizedColumnName => ColumnName.ToUpperInvariant();
}

public sealed record FormulaRangeAddress(
    string SheetName,
    string StartColumnName,
    int StartRowNumber,
    string EndColumnName,
    int EndRowNumber);

public sealed record FormulaCellReference(
    FormulaCellAddress Address,
    bool AbsoluteColumn = false,
    bool AbsoluteRow = false);

public sealed record FormulaRangeReference(
    FormulaRangeAddress Address,
    bool AbsoluteColumns = false,
    bool AbsoluteRows = false);

public sealed record FormulaCell(FormulaCellReference Reference) : FormulaExpression;

public sealed record FormulaRange(FormulaRangeReference Reference) : FormulaExpression;

public enum FormulaFunctionName
{
    If,
    IfError,
    IsNumber,
    Count,
    Sum,
    SumProduct,
    Round,
}

public sealed record FormulaFunction : FormulaExpression
{
    public FormulaFunction(FormulaFunctionName name, IEnumerable<FormulaExpression> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        Name = name;
        Arguments = arguments.ToImmutableArray();
    }

    public FormulaFunctionName Name { get; }

    public ImmutableArray<FormulaExpression> Arguments { get; }
}

public enum FormulaBinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Equal,
    NotEqual,
    LessThan,
    GreaterThan,
    LessThanOrEqual,
    GreaterThanOrEqual,
}

public sealed record FormulaBinary(
    FormulaExpression Left,
    FormulaBinaryOperator Operator,
    FormulaExpression Right) : FormulaExpression;

public readonly record struct WeightedFormulaChild(
    FormulaCellReference Score,
    FormulaCellReference Weight,
    bool Enabled = true);

public static class FormulaExpressions
{
    public static FormulaExpression EffectiveRaw(
        FormulaCellReference scorable,
        FormulaCellReference aiRaw,
        FormulaCellReference overrideValue,
        FormulaCellReference minimum,
        FormulaCellReference maximum)
    {
        FormulaCell scorableCell = new(scorable);
        FormulaCell aiCell = new(aiRaw);
        FormulaCell overrideCell = new(overrideValue);
        FormulaCell minimumCell = new(minimum);
        FormulaCell maximumCell = new(maximum);
        FormulaBlank blank = FormulaBlank.Value;

        FormulaExpression validatedAi = If(
            IsNumber(aiCell),
            If(
                Binary(aiCell, FormulaBinaryOperator.LessThan, minimumCell),
                blank,
                If(Binary(aiCell, FormulaBinaryOperator.GreaterThan, maximumCell), blank, aiCell)),
            blank);
        FormulaExpression validatedOverride = If(
            IsNumber(overrideCell),
            If(
                Binary(overrideCell, FormulaBinaryOperator.LessThan, minimumCell),
                blank,
                If(Binary(overrideCell, FormulaBinaryOperator.GreaterThan, maximumCell), blank, overrideCell)),
            blank);

        return If(
            Binary(scorableCell, FormulaBinaryOperator.NotEqual, new FormulaNumber(1m)),
            blank,
            If(
                Binary(overrideCell, FormulaBinaryOperator.Equal, blank),
                validatedAi,
                validatedOverride));
    }

    public static FormulaExpression Normalized(
        FormulaCellReference effectiveRaw,
        FormulaCellReference minimum,
        FormulaCellReference maximum,
        FormulaCellReference roundingDigits)
    {
        FormulaCell effective = new(effectiveRaw);
        FormulaCell minimumCell = new(minimum);
        FormulaCell maximumCell = new(maximum);
        FormulaExpression difference = Binary(effective, FormulaBinaryOperator.Subtract, minimumCell);
        FormulaExpression range = Binary(maximumCell, FormulaBinaryOperator.Subtract, minimumCell);
        FormulaExpression percentage = Binary(
            Binary(difference, FormulaBinaryOperator.Divide, range),
            FormulaBinaryOperator.Multiply,
            new FormulaNumber(100m));
        FormulaExpression rounded = Function(FormulaFunctionName.Round, percentage, new FormulaCell(roundingDigits));

        return If(
            IsNumber(effective),
            IfError(rounded, FormulaBlank.Value),
            FormulaBlank.Value);
    }

    public static FormulaExpression Aggregate(
        IEnumerable<WeightedFormulaChild> children,
        FormulaCellReference roundingDigits)
    {
        ArgumentNullException.ThrowIfNull(children);
        ImmutableArray<WeightedFormulaChild> enabledChildren = children
            .Where(child => child.Enabled)
            .ToImmutableArray();
        if (enabledChildren.IsEmpty)
        {
            throw new ArgumentException("At least one enabled child is required.", nameof(children));
        }

        FormulaExpression[] scores = enabledChildren
            .Select(child => (FormulaExpression)new FormulaCell(child.Score))
            .ToArray();
        FormulaExpression[] weightedTerms = enabledChildren
            .Select(child => Binary(
                new FormulaCell(child.Score),
                FormulaBinaryOperator.Multiply,
                new FormulaCell(child.Weight)))
            .ToArray();
        FormulaExpression[] weights = enabledChildren
            .Select(child => (FormulaExpression)new FormulaCell(child.Weight))
            .ToArray();
        FormulaExpression allPresent = Binary(
            Function(FormulaFunctionName.Count, scores),
            FormulaBinaryOperator.Equal,
            new FormulaNumber(enabledChildren.Length));
        FormulaExpression weightedAverage = Binary(
            Function(FormulaFunctionName.Sum, weightedTerms),
            FormulaBinaryOperator.Divide,
            Function(FormulaFunctionName.Sum, weights));
        FormulaExpression rounded = Function(
            FormulaFunctionName.Round,
            weightedAverage,
            new FormulaCell(roundingDigits));

        return If(allPresent, IfError(rounded, FormulaBlank.Value), FormulaBlank.Value);
    }

    public static FormulaFunction Function(FormulaFunctionName name, params FormulaExpression[] arguments) =>
        new(name, arguments);

    public static FormulaFunction If(
        FormulaExpression condition,
        FormulaExpression whenTrue,
        FormulaExpression whenFalse) =>
        Function(FormulaFunctionName.If, condition, whenTrue, whenFalse);

    public static FormulaFunction IfError(FormulaExpression value, FormulaExpression fallback) =>
        Function(FormulaFunctionName.IfError, value, fallback);

    public static FormulaFunction IsNumber(FormulaExpression value) =>
        Function(FormulaFunctionName.IsNumber, value);

    public static FormulaBinary Binary(
        FormulaExpression left,
        FormulaBinaryOperator operation,
        FormulaExpression right) => new(left, operation, right);
}

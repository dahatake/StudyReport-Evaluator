using System.Collections.Immutable;

namespace StudyReportEvaluator.Core.Domain;

public sealed record QuestionDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string QuestionText { get; init; }

    public required string PrimarySourceColumn { get; init; }

    public ImmutableArray<string> SupportingSourceColumns { get; init; } = [];

    public decimal Weight { get; init; }

    public ImmutableArray<EvaluatorDefinition> Evaluators { get; init; } = [];

    public bool Enabled { get; init; } = true;

    public QuestionDefinition AddEvaluator(EvaluatorDefinition evaluator) =>
        this with { Evaluators = DefinitionCollectionOperations.Normalize(Evaluators).Add(evaluator) };

    public QuestionDefinition DuplicateEvaluator(
        int sourceIndex,
        string newId,
        Func<CriterionDefinition, string> criterionIdFactory)
    {
        ImmutableArray<EvaluatorDefinition> items = DefinitionCollectionOperations.Normalize(Evaluators);
        EvaluatorDefinition duplicate = items[DefinitionCollectionOperations.RequireIndex(items, sourceIndex)]
            .Duplicate(newId, criterionIdFactory);
        return this with { Evaluators = items.Insert(sourceIndex + 1, duplicate) };
    }

    public QuestionDefinition MoveEvaluator(int sourceIndex, int destinationIndex) =>
        this with { Evaluators = DefinitionCollectionOperations.Move(Evaluators, sourceIndex, destinationIndex) };

    public QuestionDefinition SetEvaluatorEnabled(int index, bool enabled)
    {
        ImmutableArray<EvaluatorDefinition> items = DefinitionCollectionOperations.Normalize(Evaluators);
        DefinitionCollectionOperations.RequireIndex(items, index);
        return this with { Evaluators = DefinitionCollectionOperations.Replace(items, index, items[index] with { Enabled = enabled }) };
    }

    public QuestionDefinition RemoveEvaluator(int index) =>
        this with { Evaluators = DefinitionCollectionOperations.RemoveAt(Evaluators, index) };

    public QuestionDefinition Duplicate(
        string newId,
        Func<EvaluatorDefinition, string> evaluatorIdFactory,
        Func<CriterionDefinition, string> criterionIdFactory)
    {
        ArgumentNullException.ThrowIfNull(evaluatorIdFactory);
        ArgumentNullException.ThrowIfNull(criterionIdFactory);
        return this with
        {
            Id = newId,
            Evaluators = DefinitionCollectionOperations.Normalize(Evaluators)
                .Select(evaluator => evaluator.Duplicate(evaluatorIdFactory(evaluator), criterionIdFactory))
                .ToImmutableArray(),
        };
    }
}

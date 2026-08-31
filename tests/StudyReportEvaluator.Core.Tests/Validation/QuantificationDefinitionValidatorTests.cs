using System.Collections.Immutable;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Validation;
using Xunit;

namespace StudyReportEvaluator.Core.Tests.Validation;

public sealed class QuantificationDefinitionValidatorTests
{
    private readonly QuantificationDefinitionValidator _validator = new();

    [Fact]
    public void Valid_dynamic_definition_is_accepted()
    {
        DefinitionValidationResult result = _validator.Validate(C02TestDefinitions.CreateValid());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Duplicate_and_blank_ids_are_rejected_with_exact_identity()
    {
        QuantificationDefinition source = C02TestDefinitions.CreateValid();
        QuantificationDefinition definition = source with
        {
            Questions =
            [
                source.Questions[0],
                source.Questions[1] with
                {
                    Id = " ",
                    Evaluators =
                    [
                        source.Questions[1].Evaluators[0] with { Id = "C1" },
                    ],
                },
            ],
        };

        DefinitionValidationResult result = _validator.Validate(definition);

        Assert.Contains(result.Errors, error => error.Code == "REQUIRED" && error.NodeKind == "Question" && error.Field == "Id");
        DefinitionValidationError duplicate = Assert.Single(result.Errors, error => error.Code == "DUPLICATE_ID");
        Assert.Equal("Evaluator", duplicate.NodeKind);
        Assert.Equal("C1", duplicate.NodeId);
        Assert.Equal("Id", duplicate.Field);
    }

    [Theory]
    [InlineData(2, 2, true)]
    [InlineData(2, 20001, true)]
    [InlineData(2, 20002, false)]
    [InlineData(3, 2, false)]
    public void Selected_row_boundaries_are_enforced(int firstRow, int lastRow, bool expectedValid)
    {
        QuantificationDefinition definition = C02TestDefinitions.CreateValid() with
        {
            HeaderRow = 1,
            FirstDataRow = firstRow,
            LastDataRow = lastRow,
        };

        DefinitionValidationResult result = _validator.Validate(definition);

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.Contains(result.Errors, error => error.Code is "SELECTED_ROW_LIMIT_EXCEEDED" or "DATA_ROW_RANGE_REVERSED");
        }
    }

    [Fact]
    public void Data_rows_must_follow_the_header_and_fit_Excel()
    {
        QuantificationDefinition definition = C02TestDefinitions.CreateValid() with
        {
            HeaderRow = 2,
            FirstDataRow = 2,
            LastDataRow = 3,
        };
        QuantificationDefinition rowOverflow = C02TestDefinitions.CreateValid() with
        {
            LastDataRow = QuantificationDefinitionValidator.MaximumExcelRow + 1,
        };

        DefinitionValidationResult result = _validator.Validate(definition);
        DefinitionValidationResult overflowResult = _validator.Validate(rowOverflow);

        Assert.Contains(result.Errors, error => error.Code == "DATA_ROW_NOT_AFTER_HEADER" && error.Field == "FirstDataRow");
        Assert.Contains(overflowResult.Errors, error => error.Code == "ROW_OUT_OF_RANGE" && error.Field == "LastDataRow");
    }

    [Fact]
    public void Columns_must_be_valid_unique_and_different_from_primary()
    {
        QuantificationDefinition source = C02TestDefinitions.CreateValid();
        QuantificationDefinition definition = source with
        {
            Questions =
            [
                source.Questions[0] with
                {
                    PrimarySourceColumn = "XFE",
                    SupportingSourceColumns = ["G", "g", "XFE", "1", ""],
                },
            ],
        };

        DefinitionValidationResult result = _validator.Validate(definition);

        Assert.Contains(result.Errors, error => error.Code == "INVALID_SOURCE_COLUMN" && error.Field == "PrimarySourceColumn");
        Assert.Contains(result.Errors, error => error.Code == "DUPLICATE_SUPPORTING_COLUMN");
        Assert.Contains(result.Errors, error => error.Code == "PRIMARY_COLUMN_REUSED");
        Assert.Contains(result.Errors, error => error.Code == "INVALID_SOURCE_COLUMN" && error.SafeOffendingValue == "1");
        Assert.Contains(result.Errors, error => error.Code == "SOURCE_COLUMN_REQUIRED");
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("z", true)]
    [InlineData("AA", true)]
    [InlineData("XFD", true)]
    [InlineData("XFE", false)]
    [InlineData("A1", false)]
    public void Primary_column_accepts_the_full_Excel_column_boundary(string column, bool expectedValid)
    {
        QuantificationDefinition source = C02TestDefinitions.CreateValid();
        QuantificationDefinition definition = source with
        {
            Questions = [source.Questions[0] with { PrimarySourceColumn = column, SupportingSourceColumns = [] }],
        };

        DefinitionValidationResult result = _validator.Validate(definition);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Equal_reversed_ranges_and_nonpositive_weights_are_rejected_at_each_identity()
    {
        QuantificationDefinition source = C02TestDefinitions.CreateValid();
        QuestionDefinition question = source.Questions[0];
        EvaluatorDefinition evaluator = question.Evaluators[0];
        QuantificationDefinition definition = source with
        {
            Questions =
            [
                question with
                {
                    Weight = 0m,
                    Evaluators =
                    [
                        evaluator with
                        {
                            Weight = -1m,
                            Range = new ScoreRange(10m, 10m),
                            Criteria =
                            [
                                evaluator.Criteria[0] with
                                {
                                    Weight = 0m,
                                    Range = new ScoreRange(5m, 1m),
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        DefinitionValidationResult result = _validator.Validate(definition);

        Assert.Equal(3, result.Errors.Count(error => error.Code == "WEIGHT_MUST_BE_POSITIVE"));
        Assert.Equal(2, result.Errors.Count(error => error.Code == "SCORE_RANGE_INVALID"));
        Assert.Contains(result.Errors, error => error.NodeId == "Q1" && error.Field == "Weight");
        Assert.Contains(result.Errors, error => error.NodeId == "E1" && error.Field == "Range");
        Assert.Contains(result.Errors, error => error.NodeId == "C1" && error.Field == "Range");
    }

    [Fact]
    public void Executable_snapshot_requires_an_enabled_child_at_every_level()
    {
        QuantificationDefinition source = C02TestDefinitions.CreateValid();
        QuestionDefinition question = source.Questions[0];
        EvaluatorDefinition evaluator = question.Evaluators[0];

        DefinitionValidationResult noQuestions = _validator.Validate(source with
        {
            Questions = source.Questions.Select(item => item with { Enabled = false }).ToImmutableArray(),
        });
        DefinitionValidationResult noEvaluators = _validator.Validate(source with
        {
            Questions = [question with { Evaluators = question.Evaluators.Select(item => item with { Enabled = false }).ToImmutableArray() }],
        });
        DefinitionValidationResult noCriteria = _validator.Validate(source with
        {
            Questions =
            [
                question with
                {
                    Evaluators = [evaluator with { Criteria = evaluator.Criteria.Select(item => item with { Enabled = false }).ToImmutableArray() }],
                },
            ],
        });

        Assert.Contains(noQuestions.Errors, error => error.Code == "ENABLED_QUESTION_REQUIRED");
        Assert.Contains(noEvaluators.Errors, error => error.Code == "ENABLED_EVALUATOR_REQUIRED" && error.NodeId == "Q1");
        Assert.Contains(noCriteria.Errors, error => error.Code == "ENABLED_CRITERION_REQUIRED" && error.NodeId == "E1");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void Rounding_digits_outside_zero_through_six_are_rejected(int roundingDigits)
    {
        DefinitionValidationResult result = _validator.Validate(C02TestDefinitions.CreateValid() with { RoundingDigits = roundingDigits });

        DefinitionValidationError error = Assert.Single(result.Errors, item => item.Code == "ROUNDING_OUT_OF_RANGE");
        Assert.Equal("RoundingDigits", error.Field);
        Assert.Equal(roundingDigits.ToString(System.Globalization.CultureInfo.InvariantCulture), error.SafeOffendingValue);
    }

    [Fact]
    public void Validation_error_never_echoes_question_or_prompt_bodies()
    {
        const string questionCanary = "QUESTION-BODY-MUST-NOT-APPEAR";
        const string promptCanary = "PROMPT-BODY-MUST-NOT-APPEAR";
        QuantificationDefinition source = C02TestDefinitions.CreateValid();
        QuantificationDefinition definition = source with
        {
            Questions =
            [
                source.Questions[0] with
                {
                    QuestionText = questionCanary,
                    Weight = 0m,
                    Evaluators =
                    [
                        source.Questions[0].Evaluators[1] with
                        {
                            CustomPromptTemplate = promptCanary,
                            Range = new ScoreRange(2m, 1m),
                        },
                    ],
                },
            ],
        };

        DefinitionValidationResult result = _validator.Validate(definition);
        string renderedErrors = string.Join("|", result.Errors.Select(error => error.ToString()));

        Assert.DoesNotContain(questionCanary, renderedErrors, StringComparison.Ordinal);
        Assert.DoesNotContain(promptCanary, renderedErrors, StringComparison.Ordinal);
    }
}

internal static class C02TestDefinitions
{
    internal static QuantificationDefinition CreateValid()
    {
        QuantificationDefinition source = Domain.TestDefinitions.Create();
        QuestionDefinition second = source.Questions[1] with
        {
            Evaluators = source.Questions[1].Evaluators
                .Select(evaluator => evaluator with
                {
                    Id = $"{evaluator.Id}-Q2",
                    Criteria = evaluator.Criteria
                        .Select(criterion => criterion with { Id = $"{criterion.Id}-Q2" })
                        .ToImmutableArray(),
                })
                .ToImmutableArray(),
        };

        return source with { Questions = [source.Questions[0], second] };
    }
}

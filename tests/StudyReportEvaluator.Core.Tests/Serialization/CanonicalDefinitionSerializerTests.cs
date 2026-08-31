using System.Globalization;
using System.Text.Json;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Serialization;
using StudyReportEvaluator.Core.Tests.Validation;
using Xunit;

namespace StudyReportEvaluator.Core.Tests.Serialization;

public sealed class CanonicalDefinitionSerializerTests
{
    private readonly CanonicalDefinitionSerializer _serializer = new();

    [Fact]
    public void Serialization_is_compact_parseable_ordered_and_uses_normative_type_names()
    {
        QuantificationDefinition definition = C02TestDefinitions.CreateValid();

        string json = _serializer.Serialize(definition);
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.DoesNotContain("\r", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", json, StringComparison.Ordinal);
        Assert.Equal("3.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(["Q1", "Q2"], document.RootElement.GetProperty("questions").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
        Assert.Equal("KNOWLEDGE_COVERAGE", document.RootElement.GetProperty("questions")[0].GetProperty("evaluators")[0].GetProperty("type").GetString());
        Assert.Equal("CUSTOM_PROMPT", document.RootElement.GetProperty("questions")[0].GetProperty("evaluators")[1].GetProperty("type").GetString());
    }

    [Fact]
    public void Equal_decimal_values_have_the_same_canonical_hash_regardless_of_scale_or_culture()
    {
        QuantificationDefinition first = C02TestDefinitions.CreateValid();
        QuantificationDefinition second = first with
        {
            Questions = [first.Questions[0] with { Weight = 3.000m }],
        };
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            string firstHash = _serializer.ComputeSha256(first with { Questions = [first.Questions[0]] });
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ja-JP");
            string secondHash = _serializer.ComputeSha256(second);

            Assert.Equal(firstHash, secondHash);
            Assert.Matches("^[0-9A-F]{64}$", firstHash);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void Ordered_collection_changes_change_the_hash()
    {
        QuantificationDefinition first = C02TestDefinitions.CreateValid();
        QuantificationDefinition reordered = first.MoveQuestion(1, 0);

        Assert.NotEqual(_serializer.ComputeSha256(first), _serializer.ComputeSha256(reordered));
    }

    [Fact]
    public void Every_execution_relevant_value_changes_the_hash()
    {
        QuantificationDefinition first = C02TestDefinitions.CreateValid();
        QuestionDefinition changedQuestion = first.Questions[0] with
        {
            PrimarySourceColumn = "H",
            Weight = 9m,
            Enabled = false,
        };
        QuantificationDefinition changed = first with { Questions = [changedQuestion, first.Questions[1]] };

        Assert.NotEqual(_serializer.ComputeSha256(first), _serializer.ComputeSha256(changed));
        Assert.Contains("\"primarySourceColumn\":\"H\"", _serializer.Serialize(changed), StringComparison.Ordinal);
        Assert.Contains("\"enabled\":false", _serializer.Serialize(changed), StringComparison.Ordinal);
    }
}

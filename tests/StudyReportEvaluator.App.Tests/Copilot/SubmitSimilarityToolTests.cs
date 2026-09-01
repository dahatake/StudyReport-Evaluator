using System.Text.Json;
using GitHub.Copilot;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.Core.Domain;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Copilot;

public sealed class SubmitSimilarityToolTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("0.5")]
    [InlineData("1")]
    public async Task One_closed_valid_invocation_accepts_zero_through_one(string similarityText)
    {
        SubmitSimilarityTool tool = new(AuxiliaryEvaluationSchemaFactoryTests.CreateSimilarityPayload());

        string acknowledgement = await tool.InvokeAsync(
            Invocation(ValidJson(similarityText)),
            TestContext.Current.CancellationToken);

        Assert.Equal("accepted", acknowledgement);
        Assert.True(tool.TryGetAcceptedResult(out SimilarityQuantificationResult? result));
        Assert.Equal(decimal.Parse(similarityText, System.Globalization.CultureInfo.InvariantCulture), result.Similarity);
        Assert.Equal(AuxiliarySubmitStatus.Accepted, tool.Status);
    }

    [Theory]
    [InlineData("-0.001", "SIMILARITY_OUT_OF_RANGE")]
    [InlineData("1.001", "SIMILARITY_OUT_OF_RANGE")]
    [InlineData("\"0.5\"", "NUMBER_INVALID")]
    public async Task Invalid_similarity_is_rejected_without_clamp(string similarityJson, string expectedCode)
    {
        SubmitSimilarityTool tool = new(AuxiliaryEvaluationSchemaFactoryTests.CreateSimilarityPayload());

        await tool.InvokeAsync(Invocation(ValidJson(similarityJson)), TestContext.Current.CancellationToken);

        Assert.Contains(expectedCode, tool.ErrorCodes);
        Assert.False(tool.TryGetAcceptedResult(out _));
    }

    [Theory]
    [InlineData("{\"QuestionId\":\"Q1\",\"Similarity\":0.5}", "ROOT_MISSING_PROPERTY")]
    [InlineData("{\"QuestionId\":\"Q1\",\"Similarity\":0.5,\"Reason\":\"r\",\"Penalty\":1}", "ROOT_UNKNOWN_PROPERTY")]
    [InlineData("{\"QuestionId\":\"q1\",\"Similarity\":0.5,\"Reason\":\"r\"}", "ID_MISMATCH")]
    [InlineData("{\"QuestionId\":\"Q1\",\"Similarity\":0.5,\"Reason\":\" \"}", "TEXT_REQUIRED")]
    public async Task Missing_unknown_wrong_id_and_blank_reason_are_rejected(string json, string expectedCode)
    {
        SubmitSimilarityTool tool = new(AuxiliaryEvaluationSchemaFactoryTests.CreateSimilarityPayload());

        await tool.InvokeAsync(Invocation(json), TestContext.Current.CancellationToken);

        Assert.Contains(expectedCode, tool.ErrorCodes);
        Assert.False(tool.TryGetAcceptedResult(out _));
    }

    [Fact]
    public async Task Duplicate_invocation_invalidates_an_accepted_similarity()
    {
        SubmitSimilarityTool tool = new(AuxiliaryEvaluationSchemaFactoryTests.CreateSimilarityPayload());
        ToolInvocation invocation = Invocation(ValidJson("0.5"));

        Assert.Equal("accepted", await tool.InvokeAsync(invocation, TestContext.Current.CancellationToken));
        Assert.Equal("rejected", await tool.InvokeAsync(invocation, TestContext.Current.CancellationToken));
        Assert.Equal(AuxiliarySubmitStatus.DuplicateInvocation, tool.Status);
        Assert.Equal(["TOOL_INVOCATION_COUNT_INVALID"], tool.ErrorCodes);
        Assert.False(tool.TryGetAcceptedResult(out _));
    }

    private static string ValidJson(string similarityJson) => $$"""
        {"QuestionId":"Q1","Similarity":{{similarityJson}},"Reason":"semantic overlap"}
        """;

    private static ToolInvocation Invocation(string json) => new()
    {
        SessionId = "similarity-test-session",
        ToolCallId = Guid.NewGuid().ToString("N"),
        ToolName = AuxiliaryEvaluationSchemaFactory.SimilarityToolName,
        Arguments = JsonDocument.Parse(json).RootElement.Clone(),
    };
}

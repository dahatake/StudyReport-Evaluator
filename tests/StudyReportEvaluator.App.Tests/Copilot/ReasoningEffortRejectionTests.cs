using System.Collections.Immutable;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.Core.Domain;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Copilot;

public sealed class ReasoningEffortRejectionTests
{
    [Theory]
    [InlineData("Session error: Execution failed: CAPIError: 400 \"thinking.type.enabled\" is not supported for this model.", true)]
    [InlineData("Use \"thinking.type.adaptive\" and \"output_config.effort\" to control thinking behavior.", true)]
    [InlineData("Session error: rate limited", false)]
    [InlineData(null, false)]
    public void Rejection_message_is_recognized(string? message, bool expected) =>
        Assert.Equal(expected, ReasoningEffortRejections.IsRejectionMessage(message));

    [Fact]
    public void Marked_model_is_reported_as_rejected_and_other_models_are_not()
    {
        string modelId = $"test-model-{Guid.NewGuid():N}";
        Assert.False(ReasoningEffortRejections.IsRejected(modelId));

        ReasoningEffortRejections.MarkRejected(modelId);

        Assert.True(ReasoningEffortRejections.IsRejected(modelId));
        Assert.False(ReasoningEffortRejections.IsRejected($"{modelId}-other"));
    }

    [Fact]
    public async Task Rejected_effort_is_retried_in_a_new_attempt_and_can_succeed()
    {
        RetryAndCleanupCoordinator coordinator = new();
        int calls = 0;

        EphemeralEvaluationResult result = await coordinator.ExecuteAsync(
            _ => new RejectThenSucceedAttempt(() => calls++),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            maxConcurrency: 1,
            TestContext.Current.CancellationToken);

        Assert.Equal(EphemeralEvaluationStatus.Succeeded, result.Status);
        Assert.Equal(2, result.AttemptCount);
        Assert.Equal(2, calls);
    }

    private sealed class RejectThenSucceedAttempt(Func<int> onExecute) : IEphemeralEvaluationAttempt
    {
        public string SessionId { get; } = $"a03-{Guid.NewGuid():N}";

        public EvaluationTokenUsage TokenUsage => EvaluationTokenUsage.Unavailable;

        public Task<QuantificationResult> ExecuteAsync(CancellationToken cancellationToken) =>
            onExecute() == 0
                ? Task.FromException<QuantificationResult>(new EvaluationReasoningEffortRejectedException())
                : Task.FromResult(new QuantificationResult
                {
                    EvaluatorId = "E1",
                    Criteria = ImmutableArray.Create(new CriterionQuantificationResult
                    {
                        CriterionId = "C1",
                        RawScore = 4m,
                        Reason = "reason",
                        Evidence = "evidence",
                        EvidenceSource = EvidenceSourceKind.PrimaryAnswer,
                        EvidenceSourceColumnId = "G",
                    }),
                });

        public Task AbortAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DisposeSessionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteSessionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

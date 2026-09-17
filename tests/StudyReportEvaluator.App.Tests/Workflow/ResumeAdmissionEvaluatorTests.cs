using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.App.Workbooks.Checkpoint;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Workflow;

public sealed class ResumeAdmissionEvaluatorTests
{
    [Fact]
    public void Matching_checkpoint_is_admitted()
    {
        QuantificationDefinition definition = Definition();
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(definition);
        EvaluationPlan plan = new EvaluationPlanBuilder().Build(
            snapshot,
            U01TestSupport.ValidateMapping(definition).Mapping!);
        InputSnapshot input = new(new string('A', 64), 123, DateTimeOffset.UnixEpoch);
        CheckpointRuntimeIdentity runtime = Runtime();
        CheckpointEnvelope checkpoint = Envelope(snapshot, input, runtime);

        ResumeAdmissionReport report = ResumeAdmissionEvaluator.Evaluate(
            checkpoint, checkpoint.PartialPath, snapshot, plan, input,
            checkpoint.InputPath, checkpoint.NormalModelId, runtime);

        Assert.True(report.CanResume);
        Assert.Null(report.BlockingStatusCode);
        Assert.All(report.Findings, finding => Assert.True(finding.IsSatisfied));
    }

    [Fact]
    public void Different_definition_is_rejected_without_reporting_content()
    {
        QuantificationDefinition original = Definition();
        QuantificationSnapshot originalSnapshot = QuantificationSnapshot.Create(original);
        InputSnapshot input = new(new string('A', 64), 123, DateTimeOffset.UnixEpoch);
        CheckpointEnvelope checkpoint = Envelope(originalSnapshot, input, Runtime());
        QuantificationDefinition changed = original with { Revision = "changed" };
        QuantificationSnapshot changedSnapshot = QuantificationSnapshot.Create(changed);
        EvaluationPlan changedPlan = new EvaluationPlanBuilder().Build(
            changedSnapshot,
            U01TestSupport.ValidateMapping(changed).Mapping!);

        ResumeAdmissionReport report = ResumeAdmissionEvaluator.Evaluate(
            checkpoint, checkpoint.PartialPath, changedSnapshot, changedPlan, input,
            checkpoint.InputPath, checkpoint.NormalModelId, Runtime());

        Assert.False(report.CanResume);
        Assert.Equal(CheckpointAdmissionStatusCodes.DefinitionMismatch, report.BlockingStatusCode);
        ResumeAdmissionFinding definition = Assert.Single(report.Findings,
            finding => finding.Item == ResumeAdmissionItem.Definition);
        Assert.False(definition.IsSatisfied);
        Assert.DoesNotContain(checkpoint.DefinitionCanonicalJson, definition.Description, StringComparison.Ordinal);
    }

    private static QuantificationDefinition Definition() => U01TestSupport.Definition(
        2,
        2,
        U01TestSupport.Question("Q1", "A", ["B"], true, U01TestSupport.Evaluator("E1", "C1")));

    private static CheckpointRuntimeIdentity Runtime() => new()
    {
        ApplicationIdentity = "StudyReportEvaluator.App/0.8.6",
        CliVersion = "1.0.79",
        CliSha256 = new string('A', 64),
        SdkInformationalVersion = "1.0.11",
    };

    private static CheckpointEnvelope Envelope(
        QuantificationSnapshot snapshot,
        InputSnapshot input,
        CheckpointRuntimeIdentity runtime) => new()
    {
        InputPath = "C:\\resume-input.xlsx",
        Input = input,
        DefinitionCanonicalJson = snapshot.CanonicalJson,
        DefinitionSha256 = snapshot.Sha256,
        NormalModelId = "model-test",
        Runtime = runtime,
        FinalPath = "C:\\result\\eval.xlsx",
        PartialPath = "C:\\result\\eval.partial.xlsx",
        StartedAtUtc = DateTimeOffset.UnixEpoch,
        SavedAtUtc = DateTimeOffset.UnixEpoch,
    };
}

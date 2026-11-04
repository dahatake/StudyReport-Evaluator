using System.Collections.Immutable;
using StudyReportEvaluator.App.Tests.Workbooks.Intake;
using StudyReportEvaluator.App.Tests.Workflow;
using StudyReportEvaluator.App.Workbooks.Checkpoint;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Similarity;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Workbooks.Checkpoint;

// FR-115: 参照集合（PARAM-003）の checkpoint 保存・検証と、参照別の類似度計算（単体テスト）。
public sealed class ReferenceSetCheckpointTests
{
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 11, 4, 1, 0, 0, TimeSpan.Zero);

    private static CheckpointReference Reference(int slot, string? answer, string status = ResultsStatusCodes.Success) =>
        new()
        {
            QuestionId = "Q1",
            SlotIndex = slot,
            Answer = answer,
            StatusCode = status,
            GeneratedAtUtc = StartedAtUtc.AddSeconds(5),
            AttemptCount = 1,
        };

    private static CheckpointEnvelope NewFormat(TemporaryWorkbook input, params CheckpointReference[] references) =>
        Envelope(input) with
        {
            ReferenceSlotCount = ReferenceSetPolicy.SlotCount,
            ReferenceTemplateVersion = ReferenceSetPolicy.SystemTemplateVersion,
            ReferenceComparisonVersion = ReferenceSetPolicy.ComparisonVersion,
            ReferenceNormalizationVersion = ReferenceSetPolicy.NormalizationVersion,
            References = [.. references],
        };

    // FR-115 AC-009 / AC-080 / AC-082: 5 slot の集合と条件の版を保存して往復し、再開時に保存済み slot をそのまま読める。
    [Fact]
    public void FR115_AC080_five_slot_set_and_conditions_round_trip()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        CheckpointEnvelope envelope = NewFormat(
            input,
            Reference(1, "a"),
            Reference(2, "b"),
            Reference(3, null, ResultsStatusCodes.AiRuntimeFailed));
        CheckpointStore store = new();

        Assert.True(store.Create(envelope, TestContext.Current.CancellationToken).IsSuccess);
        CheckpointLoadResult loaded = store.Load(envelope.PartialPath, TestContext.Current.CancellationToken);

        Assert.True(loaded.IsSuccess, loaded.Code);
        Assert.Equal(5, loaded.Envelope!.ReferenceSlotCount);
        Assert.Equal(ReferenceSetPolicy.SystemTemplateVersion, loaded.Envelope.ReferenceTemplateVersion);
        Assert.Equal([1, 2, 3], loaded.Envelope.References.Select(item => item.SlotIndex));
        Assert.Equal(ResultsStatusCodes.AiRuntimeFailed, loaded.Envelope.References[2].StatusCode);
    }

    // FR-115 AC-083: 旧単独参照（slot 0・予定数と条件の欄なし）は旧形式のまま保存・往復でき、新しい欄を書かない。
    [Fact]
    public void FR115_AC083_legacy_single_reference_round_trips_without_new_fields()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        CheckpointEnvelope legacy = Envelope(input) with { References = [Reference(0, "old")] };
        CheckpointStore store = new();

        Assert.True(store.Create(legacy, TestContext.Current.CancellationToken).IsSuccess);
        CheckpointLoadResult loaded = store.Load(legacy.PartialPath, TestContext.Current.CancellationToken);

        Assert.True(loaded.IsSuccess, loaded.Code);
        Assert.Equal(0, loaded.Envelope!.ReferenceSlotCount);
        Assert.Null(loaded.Envelope.ReferenceTemplateVersion);
        Assert.Equal(0, loaded.Envelope.References.Single().SlotIndex);
        Assert.Equal(1, ReferenceSetPolicy.PlannedSlots(loaded.Envelope.ReferenceSlotCount));
    }

    // FR-115 AC-083: 不正な新形式（範囲外・重複・条件欠落・旧形式への新条件の混在）は safe error で拒否し、何も書かない。
    [Fact]
    public void FR115_AC083_invalid_new_formats_are_rejected_without_writing()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        CheckpointEnvelope valid = NewFormat(input, Reference(1, "a"));
        CheckpointEnvelope[] invalid =
        [
            valid with { ReferenceSlotCount = ReferenceSetPolicy.SlotCount + 1 },
            valid with { ReferenceSlotCount = -1 },
            valid with { References = [Reference(0, "a")] },
            valid with { References = [Reference(6, "a")] },
            valid with { References = [Reference(1, "a"), Reference(1, "b")] },
            valid with { ReferenceTemplateVersion = null },
            valid with { ReferenceComparisonVersion = string.Empty },
            valid with { ReferenceSlotCount = 0 },
            Envelope(input) with { ReferenceTemplateVersion = ReferenceSetPolicy.SystemTemplateVersion },
        ];

        foreach (CheckpointEnvelope candidate in invalid)
        {
            CheckpointSaveResult result = new CheckpointStore().Create(candidate, TestContext.Current.CancellationToken);
            Assert.False(result.IsSuccess);
            Assert.Equal(CheckpointStatusCodes.Invalid, result.Code);
            Assert.False(File.Exists(candidate.PartialPath));
        }
    }

    // FR-115 AC-081: 完全な集合は参照別の値の最大値だけを採点値にし、成功数／予定数と重複を持つ。
    [Fact]
    public void FR115_AC081_complete_set_scores_with_the_maximum_and_marks_duplicates()
    {
        SurfaceTextSimilarityCalculator calculator = new();
        ImmutableArray<CheckpointReference> set =
        [
            Reference(1, "completely different text about cats"),
            Reference(2, "recursion calls itself"),
            Reference(3, "recursion calls itself"),
            Reference(4, "unrelated sorting remark"),
            Reference(5, "something else entirely"),
        ];

        CheckpointSimilarityResult result = DurableEvaluationScheduler.ComputeSimilarity(
            calculator, "Q1", "recursion calls itself", set);

        Assert.Equal(ResultsStatusCodes.Success, result.StatusCode);
        Assert.Equal(5, result.ReferenceDiagnostics!.Value.Length);
        Assert.Equal(1m, result.AcceptedResult!.Similarity);
        Assert.Equal(2, SimilarityDiagnostics.SelectMaximum(result.ReferenceDiagnostics.Value)!.ReferenceIndex);
        Assert.Equal(5, result.ReferenceSet!.Planned);
        Assert.Equal(5, result.ReferenceSet.Succeeded);
        SimilarityDuplicateReference duplicate = Assert.Single(result.ReferenceSet.Duplicates);
        Assert.Equal((3, 2), (duplicate.Slot, duplicate.DuplicateOfSlot));
    }

    // FR-115 AC-081 / AC-082: 一部失敗は採点値を持たず（blank）、成功参照に限った暫定の診断と 4/5 を持つ。全失敗は診断なし。
    [Fact]
    public void FR115_AC081_partial_and_total_failure_never_produce_a_scoring_value()
    {
        SurfaceTextSimilarityCalculator calculator = new();
        ImmutableArray<CheckpointReference> partial =
        [
            Reference(1, "alpha"),
            Reference(2, "beta"),
            Reference(3, "gamma"),
            Reference(4, "delta"),
            Reference(5, null, ResultsStatusCodes.AiTimeout),
        ];

        CheckpointSimilarityResult provisional = DurableEvaluationScheduler.ComputeSimilarity(calculator, "Q1", "alpha", partial);

        Assert.Equal(ResultsStatusCodes.AiTimeout, provisional.StatusCode);
        Assert.Null(provisional.AcceptedResult);
        Assert.Equal(4, provisional.ReferenceDiagnostics!.Value.Length);
        Assert.Equal((5, 4), (provisional.ReferenceSet!.Planned, provisional.ReferenceSet.Succeeded));

        ImmutableArray<CheckpointReference> none =
        [
            .. Enumerable.Range(1, 5).Select(slot => Reference(slot, null, ResultsStatusCodes.AiRuntimeFailed)),
        ];
        CheckpointSimilarityResult failed = DurableEvaluationScheduler.ComputeSimilarity(calculator, "Q1", "alpha", none);

        Assert.Equal(ResultsStatusCodes.AiRuntimeFailed, failed.StatusCode);
        Assert.Null(failed.AcceptedResult);
        Assert.Null(failed.ReferenceDiagnostics);
        Assert.Equal((5, 0), (failed.ReferenceSet!.Planned, failed.ReferenceSet.Succeeded));
    }

    // FR-115 AC-083: 旧単独参照（slot 0）は参照 1 の単独集合として比較し、予定数は 1。
    [Fact]
    public void FR115_AC083_legacy_single_reference_is_compared_as_a_one_member_set()
    {
        CheckpointSimilarityResult result = DurableEvaluationScheduler.ComputeSimilarity(
            new SurfaceTextSimilarityCalculator(), "Q1", "same words here", [Reference(0, "same words here")]);

        Assert.Equal(ResultsStatusCodes.Success, result.StatusCode);
        Assert.Equal(1, result.ReferenceSet!.Planned);
        Assert.Equal(1, Assert.Single(result.ReferenceDiagnostics!.Value).ReferenceIndex);
        Assert.Equal(1m, result.AcceptedResult!.Similarity);
    }

    private static CheckpointEnvelope Envelope(TemporaryWorkbook input)
    {
        QuantificationDefinition definition = U01TestSupport.Definition(
            2,
            2,
            U01TestSupport.Question("Q1", "A", ["B"], true, U01TestSupport.Evaluator("E1", "C1")));
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(definition);
        OutputPathReservation reservation = new OutputPathPlanner().Reserve(
            input.Path,
            StartedAtUtc.ToOffset(TimeSpan.FromHours(9)));
        return new CheckpointEnvelope
        {
            InputPath = Path.GetFullPath(input.Path),
            Input = new InputSnapshotService().Capture(input.Path),
            DefinitionCanonicalJson = snapshot.CanonicalJson,
            DefinitionSha256 = snapshot.Sha256,
            NormalModelId = "model-test",
            ReferenceModelId = "model-test",
            Runtime = new CheckpointRuntimeIdentity
            {
                ApplicationIdentity = "StudyReportEvaluator.App/4.0.0",
                CliVersion = "1.0.79",
                CliSha256 = new string('A', 64),
                SdkInformationalVersion = "1.0.11",
            },
            FinalPath = reservation.FinalPath,
            PartialPath = reservation.PartialPath,
            StartedAtUtc = StartedAtUtc,
            SavedAtUtc = StartedAtUtc.AddMinutes(1),
        };
    }
}

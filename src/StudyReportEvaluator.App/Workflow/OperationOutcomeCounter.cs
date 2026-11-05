using StudyReportEvaluator.App.Workbooks.Checkpoint;
using StudyReportEvaluator.App.Workbooks.Writing;

namespace StudyReportEvaluator.App.Workflow;

/// <summary>
/// FR-119 AC-097: checkpoint 済みの処理単位の結果の状態コードから成功数・失敗数を集計する（スレッドセーフ）。
/// 成功＝Success・Empty、失敗＝Success・Empty・NotRunZeroBudget・Cancelled 以外（取消は成功にも失敗にも数えない。<c>RunSummary.OperationFailureCount</c> と同じ定義）。
/// </summary>
internal sealed class OperationOutcomeCounter
{
    private readonly object gate = new();
    private int succeeded;
    private int failed;

    public static bool IsSuccess(string statusCode) =>
        statusCode is ResultsStatusCodes.Success or ResultsStatusCodes.Empty;

    public static bool IsFailure(string statusCode) =>
        statusCode is not ResultsStatusCodes.Success
            and not ResultsStatusCodes.Empty
            and not ResultsStatusCodes.NotRunZeroBudget
            and not ResultsStatusCodes.Cancelled;

    public void Add(CheckpointReference reference) => Apply(Count(reference), 1);

    public void Add(CheckpointCompletedRow row) => Apply(Count(row), 1);

    public void Remove(CheckpointReference reference) => Apply(Count(reference), -1);

    public void Remove(CheckpointCompletedRow row) => Apply(Count(row), -1);

    public (int Succeeded, int Failed) Snapshot()
    {
        lock (gate)
        {
            return (succeeded, failed);
        }
    }

    private void Apply((int Succeeded, int Failed) delta, int sign)
    {
        lock (gate)
        {
            succeeded += sign * delta.Succeeded;
            failed += sign * delta.Failed;
        }
    }

    private static (int Succeeded, int Failed) Count(CheckpointReference reference) =>
        Count([reference.StatusCode]);

    private static (int Succeeded, int Failed) Count(CheckpointCompletedRow row) =>
        Count(row.NormalResults.Select(result => result.StatusCode)
            .Concat(row.SpecialResults.Select(result => result.StatusCode))
            .Concat(row.SimilarityResults.Select(result => result.StatusCode)));

    private static (int Succeeded, int Failed) Count(IEnumerable<string> statuses)
    {
        int success = 0;
        int failure = 0;
        foreach (string status in statuses)
        {
            if (IsSuccess(status))
            {
                success++;
            }
            else if (IsFailure(status))
            {
                failure++;
            }
        }

        return (success, failure);
    }
}

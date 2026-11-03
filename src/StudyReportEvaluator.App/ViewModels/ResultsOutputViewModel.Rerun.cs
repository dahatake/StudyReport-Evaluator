using System.Collections.Immutable;
using System.Globalization;
using System.Windows.Input;
using StudyReportEvaluator.App.Workflow;

namespace StudyReportEvaluator.App.ViewModels;

public sealed class ResultsRerunRequestedEventArgs : EventArgs
{
    public ResultsRerunRequestedEventArgs(ExecutionRunContext context, IReadOnlyList<int> rows)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
    }

    public ExecutionRunContext Context { get; }

    /// <summary>再実行する元の行番号（昇順）。</summary>
    public IReadOnlyList<int> Rows { get; }
}

// FR-112: 結果画面の行チェック・「技術エラーの行を選ぶ」・選択件数・「選んだ行を再実行」。
public sealed partial class ResultsOutputViewModel
{
    private readonly HashSet<int> rerunSelection = [];
    private ViewModelCommand? selectTechnicalErrorRowsCommand;
    private ViewModelCommand? rerunSelectedRowsCommand;

    /// <summary>「選んだ行を再実行」が押され、対象行が確定したとき。実行画面への切り替えは購読側が行う。</summary>
    public event EventHandler<ResultsRerunRequestedEventArgs>? RerunRequested;

    public int SelectedRerunCount => rerunSelection.Count;

    public string SelectedRerunCountText =>
        $"選択: {rerunSelection.Count.ToString(CultureInfo.InvariantCulture)} 行";

    public bool CanRerunSelectedRows => !disposed
        && context is not null
        && !IsExporting
        && rerunSelection.Count > 0;

    public bool CanSelectTechnicalErrorRows => !disposed
        && rowScoreItems.Any(row => row.CanSelectForRerun && row.Status == ResultsRowStatus.TechnicalError);

    public ICommand SelectTechnicalErrorRowsCommand => selectTechnicalErrorRowsCommand ??= new ViewModelCommand(
        _ => SelectTechnicalErrorRows(),
        _ => CanSelectTechnicalErrorRows);

    public ICommand RerunSelectedRowsCommand => rerunSelectedRowsCommand ??= new ViewModelCommand(
        _ => RerunSelectedRows(),
        _ => CanRerunSelectedRows);

    /// <summary>状態が「技術エラー」の行だけにチェックを付ける（ほかの行のチェックは外す）。</summary>
    public void SelectTechnicalErrorRows()
    {
        if (disposed) return;
        foreach (ResultsRowScoreViewModel row in rowScoreItems)
        {
            bool target = row.CanSelectForRerun && row.Status == ResultsRowStatus.TechnicalError;
            if (target)
            {
                rerunSelection.Add(row.SourceRowNumber);
            }
            else
            {
                rerunSelection.Remove(row.SourceRowNumber);
            }

            row.SetRerunSelectionSilently(target);
        }

        RaiseRerunStates();
    }

    public void RerunSelectedRows()
    {
        if (!CanRerunSelectedRows || context is null) return;
        ImmutableArray<int> rows = rerunSelection.OrderBy(row => row).ToImmutableArray();
        RerunRequested?.Invoke(this, new ResultsRerunRequestedEventArgs(context, rows));
    }

    private void ResetRerunSelection()
    {
        rerunSelection.Clear();
        RaiseRerunStates();
    }

    private void AttachRerunSelection(ResultsRowScoreViewModel row, RunSummary summary, bool hasCompletedRow)
    {
        row.CanSelectForRerun = hasCompletedRow
            && summary.IsDurable
            && string.Equals(summary.StatusCode, QuantificationRunStatusCodes.Success, StringComparison.Ordinal)
            && summary.FinalPath is not null;
        row.RerunSelectionChanged = OnRerunSelectionChanged;
        if (row.CanSelectForRerun && rerunSelection.Contains(row.SourceRowNumber))
        {
            row.SetRerunSelectionSilently(true);
        }
        else
        {
            rerunSelection.Remove(row.SourceRowNumber);
        }

        RaiseRerunStates();
    }

    private void OnRerunSelectionChanged(ResultsRowScoreViewModel row, bool selected)
    {
        if (selected)
        {
            rerunSelection.Add(row.SourceRowNumber);
        }
        else
        {
            rerunSelection.Remove(row.SourceRowNumber);
        }

        RaiseRerunStates();
    }

    private void RaiseRerunStates()
    {
        OnPropertiesChanged(
            nameof(SelectedRerunCount),
            nameof(SelectedRerunCountText),
            nameof(CanRerunSelectedRows),
            nameof(CanSelectTechnicalErrorRows));
        selectTechnicalErrorRowsCommand?.RaiseCanExecuteChanged();
        rerunSelectedRowsCommand?.RaiseCanExecuteChanged();
    }
}

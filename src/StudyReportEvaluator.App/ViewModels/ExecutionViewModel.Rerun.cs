using System.Collections.Immutable;
using System.Globalization;
using System.Windows.Input;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.Core.Domain;

namespace StudyReportEvaluator.App.ViewModels;

// FR-112: 結果画面で選んだ行だけの再実行。対象行を受け取るだけで、利用者が「実行開始」を押すまで AI 処理は始めない。
public sealed partial class ExecutionViewModel
{
    private ExecutionRunContext? rerunBase;
    private ImmutableArray<int> rerunRows = [];
    private string rerunMismatchText = string.Empty;
    private bool isValidatingRerun;
    private ViewModelCommand? clearRerunTargetCommand;

    public bool HasRerunTarget => rerunBase is not null;

    public int RerunTargetCount => rerunRows.Length;

    public IReadOnlyList<int> RerunTargetRows => rerunRows;

    public string RerunTargetText => rerunBase is null
        ? string.Empty
        : $"再実行の対象: {rerunRows.Length.ToString(CultureInfo.InvariantCulture)} 行（元の行番号: "
          + string.Join("、", rerunRows.Select(row => row.ToString(CultureInfo.InvariantCulture)))
          + "）。「実行開始」を押すまで AI 処理は始まりません。";

    public string RerunMismatchText => rerunMismatchText;

    public bool HasRerunMismatch => rerunMismatchText.Length > 0;

    public ICommand ClearRerunTargetCommand => clearRerunTargetCommand ??= new ViewModelCommand(
        _ => ClearRerunTarget(),
        _ => HasRerunTarget && !IsRunning && !runStarting);

    /// <summary>結果画面から対象行（元の行番号）を受け取る。AI 処理は開始しない。</summary>
    public void SetRerunTarget(ExecutionRunContext context, IEnumerable<int> rows)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rows);
        if (IsRunning || runStarting) return;
        ImmutableArray<int> selected = rows.Distinct().OrderBy(row => row).ToImmutableArray();
        if (selected.IsEmpty) return;
        rerunBase = context;
        rerunRows = selected;
        rerunMismatchText = string.Empty;
        RaiseRerunStates();
    }

    public void ClearRerunTarget()
    {
        if (rerunBase is null && rerunMismatchText.Length == 0) return;
        rerunBase = null;
        rerunRows = [];
        rerunMismatchText = string.Empty;
        RaiseRerunStates();
    }

    private void RaiseRerunStates()
    {
        OnPropertiesChanged(
            nameof(HasRerunTarget),
            nameof(RerunTargetCount),
            nameof(RerunTargetRows),
            nameof(RerunTargetText),
            nameof(RerunMismatchText),
            nameof(HasRerunMismatch),
            nameof(CanStart));
        clearRerunTargetCommand?.RaiseCanExecuteChanged();
    }

    // §10.4 と同じ前提（入力 workbook・採点定義・model）を、AI 処理の前に検証する。文言は項目名だけで、実 path・回答本文を含めない。
    private async Task<bool> ValidateRerunAsync(ExecutionRunContext original, CancellationToken cancellationToken)
    {
        isValidatingRerun = true;
        OnPropertyChanged(nameof(CanStart));
        try
        {
            List<string> mismatches = [];
            InputSnapshot? current = null;
            try
            {
                current = await resumeInspectionBoundary.CaptureInputAsync(inputPath, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // 読めない入力は不一致として扱う。
            }

            if (current is null || !original.Summary.InputSnapshot.Equals(current)
                || !ResumeAdmissionEvaluator.PathsEqual(original.InputPath, inputPath))
            {
                mismatches.Add("入力ワークブック");
            }

            string? sha = null;
            try
            {
                if (definition is not null) sha = QuantificationSnapshot.Create(definition).Sha256;
            }
            catch
            {
                // 無効な定義は不一致として扱う。
            }

            if (!string.Equals(sha, original.Summary.DefinitionSha256, StringComparison.Ordinal))
            {
                mismatches.Add("採点定義");
            }

            if (!string.Equals(selectedModelId, original.ModelId, StringComparison.Ordinal)
                || !string.Equals(SelectedModelReasoningEffort, original.ReasoningEffort, StringComparison.Ordinal)
                || !string.Equals(SelectedContextTier, original.ContextTier, StringComparison.Ordinal))
            {
                mismatches.Add("モデル");
            }

            rerunMismatchText = mismatches.Count == 0
                ? string.Empty
                : "再実行の前提が元の実行と一致しません（" + string.Join("、", mismatches)
                  + "）。元の設定に戻してから開始してください。AI 処理は開始していません。";
            OnPropertiesChanged(nameof(RerunMismatchText), nameof(HasRerunMismatch));
            return mismatches.Count == 0;
        }
        finally
        {
            isValidatingRerun = false;
            OnPropertyChanged(nameof(CanStart));
        }
    }
}

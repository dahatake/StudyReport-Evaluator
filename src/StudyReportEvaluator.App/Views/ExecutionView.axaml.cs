using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StudyReportEvaluator.App.ViewModels;

namespace StudyReportEvaluator.App.Views;

public sealed partial class ExecutionView : UserControl
{
    private ExecutionViewModel? owner;
    private bool attached;

    public ExecutionView()
        : this(new ExecutionViewModel())
    {
    }

    public ExecutionView(ExecutionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        // Keep the pre-existing JobCost XAML intact; keep the primary Start/Interrupt
        // tab path ahead of the optional cost detail toggle.
        this.FindControl<CheckBox>("ExecutionCostToggle")!.TabIndex = 63;
        DataContextChanged += HandleDataContextChanged;
        DataContext = viewModel;
        Loaded += HandleLoaded;
    }

    /// <summary>Optional standalone host seam; the application uses its current window's VM.</summary>
    public event EventHandler? CommonSettingsRequested;

    public ExecutionViewModel ViewModel => DataContext as ExecutionViewModel
        ?? throw new InvalidOperationException("ExecutionView requires an ExecutionViewModel data context.");

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        attached = true;
        if (owner is not null)
        {
            owner.PropertyChanged += HandleExecutionPropertyChanged;
        }

        RefreshPresentation();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        attached = false;
        if (owner is not null)
        {
            owner.PropertyChanged -= HandleExecutionPropertyChanged;
        }

        // The host, not a temporary view detachment, owns login/run cancellation.
        base.OnDetachedFromVisualTree(e);
    }

    private void HandleDataContextChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(owner, DataContext))
        {
            return;
        }

        if (attached && owner is not null)
        {
            owner.PropertyChanged -= HandleExecutionPropertyChanged;
        }

        owner = DataContext as ExecutionViewModel;
        if (attached && owner is not null)
        {
            owner.PropertyChanged += HandleExecutionPropertyChanged;
        }

        RefreshPresentation();
    }

    private void HandleExecutionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!attached || !ReferenceEquals(sender, owner)
            || (!string.IsNullOrEmpty(e.PropertyName)
                && e.PropertyName is not (nameof(ExecutionViewModel.SelectedModelId)
                    or nameof(ExecutionViewModel.SelectedModelLimitText)
                    or nameof(ExecutionViewModel.SelectedModelReasoningEffortText)
                    or nameof(ExecutionViewModel.PreferredModelId)
                    or nameof(ExecutionViewModel.MaxConcurrency)
                    or nameof(ExecutionViewModel.HasCurrentRun)
                    or nameof(ExecutionViewModel.CurrentRunModelId)
                    or nameof(ExecutionViewModel.CurrentRunMaxConcurrency)
                    or nameof(ExecutionViewModel.CurrentRunReasoningEffortText)
                    or nameof(ExecutionViewModel.CurrentRunLimitText)
                    or nameof(ExecutionViewModel.IsRunning)
                    or nameof(ExecutionViewModel.OutputDirectoryOverride))))
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (attached && ReferenceEquals(sender, owner))
                {
                    RefreshPresentation();
                }
            });
            return;
        }

        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        string nextConditions = $"次回 {owner?.SelectedModelId ?? "未選択"} 並列{owner?.MaxConcurrency.ToString(CultureInfo.InvariantCulture) ?? "—"} · effort: {owner?.SelectedModelReasoningEffortText ?? "未確認"} · 希望: {owner?.PreferredModelId ?? "未指定"} · 上限: {owner?.SelectedModelLimitText ?? "未確認"}";
        this.FindControl<TextBox>("EffectiveModelTextBox")!.Text = owner is { HasCurrentRun: true } current
            ? $"{(current.IsRunning ? "実行中" : "前回run")} {current.CurrentRunModelId} 並列{current.CurrentRunMaxConcurrency?.ToString(CultureInfo.InvariantCulture)} · effort: {current.CurrentRunReasoningEffortText} · 上限: {current.CurrentRunLimitText} / {nextConditions}"
            : nextConditions;
        string reasoningEffort = owner?.SelectedModelId is null
            ? "未選択"
            : owner.SelectedModelReasoningEffort ?? "未指定";
        this.FindControl<TextBlock>("ReasoningEffortStatus")!.Text = $"effort\n{reasoningEffort}";
        this.FindControl<TextBlock>("OutputDirectorySource")!.Text = owner?.OutputDirectoryOverride is null
            ? "次回新規出力\n（入力隣接）" : "次回新規出力\n（明示指定）";
    }

    private void HandleOpenSettings(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window { DataContext: MainWindowViewModel main })
        {
            main.OpenSettings(SettingsCategory.Common);
        }
        else
        {
            CommonSettingsRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandleLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= HandleLoaded;
        if (TopLevel.GetTopLevel(this) is not Window window
            || !ReferenceEquals(window.Content, this))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (attached && ReferenceEquals(TopLevel.GetTopLevel(this), window)
                && ReferenceEquals(window.Content, this))
            {
                this.FindControl<Button>("ChangeExecutionSettingsButton")?.Focus(NavigationMethod.Tab, KeyModifiers.None);
            }
        });
    }
}
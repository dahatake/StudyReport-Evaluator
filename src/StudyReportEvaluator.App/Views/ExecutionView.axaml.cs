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
    private (string Code, string? NodeId, string? Path, string Field)? selectedErrorKey;
    private bool attached;

    public ExecutionView()
        : this(new ExecutionViewModel())
    {
    }

    public ExecutionView(ExecutionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
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
        selectedErrorKey = null;
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
                    or nameof(ExecutionViewModel.PreferredModelId)
                    or nameof(ExecutionViewModel.MaxConcurrency)
                    or nameof(ExecutionViewModel.HasCurrentRun)
                    or nameof(ExecutionViewModel.CurrentRunModelId)
                    or nameof(ExecutionViewModel.CurrentRunMaxConcurrency)
                    or nameof(ExecutionViewModel.IsRunning)
                    or nameof(ExecutionViewModel.IsAutoModelAvailable)
                    or nameof(ExecutionViewModel.OutputDirectoryOverride)
                    or nameof(ExecutionViewModel.HasTechnicalErrors))))
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
        string nextConditions = $"次回 {owner?.SelectedModelId ?? "未選択"} 並列{owner?.MaxConcurrency.ToString(CultureInfo.InvariantCulture) ?? "—"} · 希望: {owner?.PreferredModelId ?? "未指定"}";
        this.FindControl<TextBox>("EffectiveModelTextBox")!.Text = owner is { HasCurrentRun: true } current
            ? $"{(current.IsRunning ? "実行中" : "前回run")} {current.CurrentRunModelId} 並列{current.CurrentRunMaxConcurrency?.ToString(CultureInfo.InvariantCulture)} / {nextConditions}"
            : nextConditions;
        string autoAvailability = owner?.IsAutoModelAvailable == true
            ? "利用可能"
            : owner?.IsAuthenticationAvailable == true ? "利用不可" : "未確認";
        this.FindControl<TextBlock>("FixedAutoModelStatus")!.Text = $"固定 auto\n{autoAvailability}";
        this.FindControl<TextBlock>("OutputDirectorySource")!.Text = owner?.OutputDirectoryOverride is null
            ? "次回新規出力\n（入力隣接）" : "次回新規出力\n（明示指定）";

        ListBox errors = this.FindControl<ListBox>("TechnicalErrorsList")!;
        if (owner is null || owner.TechnicalErrors.Count == 0)
        {
            selectedErrorKey = null;
            errors.SelectedItem = null;
        }
        else
        {
            // Revalidation replaces the items. Retain the logical selection, not
            // a stale instance; collection-reset null feedback is not a user edit.
            errors.SelectedItem = owner.TechnicalErrors.FirstOrDefault(error =>
                selectedErrorKey == error.Key) ?? owner.TechnicalErrors[0];
        }

        RefreshSelectedErrorDetail();
    }

    private void HandleTechnicalErrorSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        RefreshSelectedErrorDetail();

    private void RefreshSelectedErrorDetail()
    {
        ExecutionTechnicalError? error = this.FindControl<ListBox>("TechnicalErrorsList")?.SelectedItem as ExecutionTechnicalError;
        if (error is not null)
        {
            selectedErrorKey = error.Key;
        }

        if (this.FindControl<TextBox>("SelectedTechnicalErrorDetail") is { } detail)
        {
            detail.Text = error is null ? string.Empty
                : $"{error.Code}{Environment.NewLine}{error.TargetText}{Environment.NewLine}{error.Message}";
        }
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
                this.FindControl<Button>("CheckAuthenticationButton")?.Focus(NavigationMethod.Tab, KeyModifiers.None);
            }
        });
    }
}
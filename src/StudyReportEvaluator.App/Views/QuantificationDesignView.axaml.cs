using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.ViewModels;

namespace StudyReportEvaluator.App.Views;

public sealed partial class QuantificationDesignView : UserControl
{
    private static readonly string[] NumericEditorNames =
    ["BasePointsTextBox", "SpecialPointsTextBox", "SimilarityPenaltyWeightTextBox", "SelectedQuestionPointsTextBox"];
    // Only this shared editor needs a per-question buffer. Confirmed points stay
    // in the VM; invalid text is never parsed, normalized, or written as a draft.
    private readonly Dictionary<string, string?> pendingQuestionPoints = new(StringComparer.Ordinal);
    private string? pointsQuestionId;
    private bool changingPointsQuestion;
    private Window? observedWindow;
    private DesignValidationError? selectedError;
    private bool attached;
    private bool capacityRefreshQueued;
    private bool validationRefreshQueued;
    private bool refreshingErrors;

    public QuantificationDesignView()
        : this(new QuantificationDesignViewModel())
    {
    }

    public QuantificationDesignView(QuantificationDesignViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        CompactEmbeddedEvaluatorEditor();
        DataContext = viewModel;
        Loaded += HandleLoaded;
        foreach (TextBox editor in NumericEditors())
        {
            editor.PropertyChanged += HandleNumericEditorChanged;
        }
    }

    /// <summary>
    /// 統合した通常評価の編集部を、有限の領域に収めるため 1 行ヘッダー・左タブにする（FR-001）。
    /// 選択・保存の束縛と Automation ID は変えない。
    /// </summary>
    private void CompactEmbeddedEvaluatorEditor()
    {
        if (this.FindControl<EvaluatorSettingsView>("EmbeddedEvaluatorEditor") is not { } view
            || view.Content is not Grid layout)
        {
            return;
        }

        layout.Margin = new Thickness(0);
        layout.RowSpacing = 4;
        if (layout.Children[0] is Grid selectors)
        {
            selectors.ColumnSpacing = 8;
            foreach (Grid field in selectors.Children.OfType<Grid>())
            {
                field.RowDefinitions = new RowDefinitions("44");
                field.ColumnDefinitions = new ColumnDefinitions(field.Children.OfType<Button>().Any()
                    ? "Auto,*,Auto" : "Auto,*");
                field.ColumnSpacing = 4;
                foreach (Control control in field.Children)
                {
                    Grid.SetRow(control, 0);
                    Grid.SetColumnSpan(control, 1);
                    Grid.SetColumn(control, control is TextBlock ? 0 : control is ComboBox ? 1 : 2);
                }
            }
        }

        foreach (Border pane in view.GetLogicalDescendants().OfType<Border>()
            .Where(border => border.Classes.Contains("settings-pane")))
        {
            pane.Padding = new Thickness(4);
        }

        foreach (TabControl tabs in view.GetLogicalDescendants().OfType<TabControl>())
        {
            tabs.TabStripPlacement = Dock.Left;
        }

        foreach (TabItem tab in view.GetLogicalDescendants().OfType<TabItem>())
        {
            if (tab.Content is Grid page)
            {
                page.Margin = new Thickness(0);
                page.ColumnSpacing = 8;
            }
        }
    }

    public QuantificationDesignViewModel ViewModel => DataContext as QuantificationDesignViewModel
        ?? throw new InvalidOperationException("QuantificationDesignView requires a QuantificationDesignViewModel data context.");

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        attached = true;
        observedWindow = TopLevel.GetTopLevel(this) as Window;
        if (observedWindow is not null)
        {
            observedWindow.DataContextChanged += HandleWindowDataContextChanged;
        }

        RefreshSettingsLinks();
        RefreshPresentation();
        QueuePageSizeUpdate();
        QueueValidationRefresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        attached = false;
        if (observedWindow is not null)
        {
            observedWindow.DataContextChanged -= HandleWindowDataContextChanged;
            observedWindow = null;
        }

        this.FindControl<Button>("FormulaDetailsButton")?.Flyout?.Hide();
        this.FindControl<Button>("ErrorDetailsButton")?.Flyout?.Hide();
        // T23 retains this view. Do not clear bindings, rebuild its controls, or
        // publish unfinished numeric text into a second draft on navigation.
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != DataContextProperty)
        {
            return;
        }

        if (change.OldValue is QuantificationDesignViewModel previous)
        {
            WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, QuantificationDesignView>(
                previous, nameof(previous.PropertyChanged), HandleDesignPropertyChanged);
        }

        // A different owner must not inherit another VM's unfinished text.
        pendingQuestionPoints.Clear();
        pointsQuestionId = null;
        changingPointsQuestion = true;
        if (change.NewValue is QuantificationDesignViewModel current)
        {
            WeakEventHandlerManager.Subscribe<QuantificationDesignViewModel, PropertyChangedEventArgs, QuantificationDesignView>(
                current, nameof(current.PropertyChanged), HandleDesignPropertyChanged);
        }

        selectedError = null;
        RefreshSettingsLinks();
        RefreshPresentation();
        QueueValidationRefresh();
        QueuePageSizeUpdate();
    }

    private void HandleDesignPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, DataContext))
        {
            return;
        }

        if (e.PropertyName == nameof(QuantificationDesignViewModel.SelectedQuestion)
            && this.FindControl<ListBox>("QuestionEditorList") is { } questions)
        {
            changingPointsQuestion |= pointsQuestionId != ViewModel.SelectedQuestion?.Id;
            QueueValidationRefresh();
            // Items have settled and the VM still guards collection-induced writebacks.
            // Compiled TwoWay bindings suppress an unchanged source reference; explicitly
            // reapply it without publishing a temporary null logical selection. This also
            // restores the highlight when a VisibleQuestions page contains the target again.
            BindingOperations.GetBindingExpressionBase(questions, ListBox.SelectedItemProperty)?.UpdateTarget();
        }

        if (e.PropertyName is null or "" or nameof(QuantificationDesignViewModel.SelectedQuestion)
            or nameof(QuantificationDesignViewModel.Draft) or nameof(QuantificationDesignViewModel.ValidationSummary))
        {
            RefreshPresentation();
        }
    }

    private void HandleBodySizeChanged(object? sender, SizeChangedEventArgs e)
    {
        Grid body = this.FindControl<Grid>("DesignBody")!;
        body.Width = Math.Max(720, e.NewSize.Width - 16);
        // Root rows/gaps 104 + 左（設問一覧＋選択設問・計算式）と右（通常評価の統合編集部）の領域 300 = 404。
        body.Height = Math.Max(404, e.NewSize.Height - 16);
    }

    private void HandleQuestionListSizeChanged(object? sender, SizeChangedEventArgs e) => QueuePageSizeUpdate();

    private void QueuePageSizeUpdate()
    {
        if (!attached || capacityRefreshQueued)
        {
            return;
        }

        capacityRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            capacityRefreshQueued = false;
            if (!attached || DataContext is not QuantificationDesignViewModel design
                || this.FindControl<ListBox>("QuestionEditorList") is not { } list)
            {
                return;
            }

            double available = list.Bounds.Height;
            double rowHeight = list.GetVisualDescendants().OfType<ListBoxItem>()
                .Select(item => item.Bounds.Height).FirstOrDefault(height => height >= 44d, 48d);
            if (double.IsFinite(available) && available >= rowHeight)
            {
                // Reuse the VM's original editors and its guarded selection update.
                // Page capacity grows as well as shrinks; four is not a fixed limit.
                design.PageSize = Math.Max(1, (int)Math.Floor(available / rowHeight));
            }
        }, DispatcherPriority.Loaded);
    }

    private MainWindowViewModel? Shell => TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel shell
        && ReferenceEquals(shell.DesignViewModel, DataContext) ? shell : null;

    private void HandleWindowDataContextChanged(object? sender, EventArgs e)
    {
        RefreshSettingsLinks();
        RefreshValidation();
    }

    private void RefreshSettingsLinks()
    {
        // FR-001/FR-002: 旧設定カテゴリ（入力詳細・通常評価・固有評価・読込Prompt）への入口は削除済み。
    }

    private void RefreshPresentation()
    {
        QuantificationDesignViewModel? design = DataContext as QuantificationDesignViewModel;
        // Format the VM's confirmed values only. All 28 fractional decimal places
        // fit without rounding or G29's scientific notation for tiny differences.
        string allocation = design is { AllocationTotal: decimal total, AllocationRemaining: decimal remaining }
            ? FormattableString.Invariant($"配点合計 {total:0.############################} / 100 · 残り {remaining:0.############################}")
            : design?.AllocationSummary ?? string.Empty;
        this.FindControl<TextBox>("AllocationSummaryText")?.SetCurrentValue(TextBox.TextProperty, allocation);
        // 通常評価の要約は統合した編集部（EvaluatorSettingsView）が直接表示する。
        RefreshValidation();
    }

    private void SetSummary(string name, string text)
    {
        if (this.FindControl<TextBlock>(name) is { } summary)
        {
            summary.Text = text;
            AutomationProperties.SetName(summary, text);
            ToolTip.SetTip(summary, text);
        }
    }

    private IEnumerable<TextBox> NumericEditors() => NumericEditorNames
        .Select(name => this.FindControl<TextBox>(name)).OfType<TextBox>();

    private void HandleNumericEditorChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!attached
            || (e.Property != TextBox.TextProperty && e.Property != DataValidationErrors.HasErrorsProperty))
        {
            return;
        }

        if (sender is TextBox { Name: "SelectedQuestionPointsTextBox" } editor)
        {
            // Binding notifications may precede our SelectedQuestion observer.
            // Once a switch starts, ignore target writes until bindings settle,
            // including a rapid A -> B -> A before the queued refresh runs.
            changingPointsQuestion |= pointsQuestionId != (DataContext as QuantificationDesignViewModel)?.SelectedQuestion?.Id;
            if (!changingPointsQuestion && pointsQuestionId is { } id)
            {
                if (DataValidationErrors.GetHasErrors(editor))
                {
                    pendingQuestionPoints[id] = editor.Text;
                }
                else
                {
                    pendingQuestionPoints.Remove(id);
                }
            }
        }

        QueueValidationRefresh();
    }

    private void QueueValidationRefresh()
    {
        if (!attached || validationRefreshQueued)
        {
            return;
        }

        // Let the existing binding publish its conversion error; never parse or
        // coerce text ourselves and never listen to a global input manager.
        validationRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (attached)
                {
                    RestorePendingQuestionPoints();
                    RefreshValidation();
                }
            }
            finally
            {
                validationRefreshQueued = false;
            }
        });
    }

    private void RestorePendingQuestionPoints()
    {
        string? id = (DataContext as QuantificationDesignViewModel)?.SelectedQuestion?.Id;
        if ((!changingPointsQuestion && pointsQuestionId == id)
            || this.FindControl<TextBox>("SelectedQuestionPointsTextBox") is not { } editor)
        {
            return;
        }

        changingPointsQuestion = true;
        try
        {
            pointsQuestionId = id;
            BindingOperations.GetBindingExpressionBase(editor, TextBox.TextProperty)?.UpdateTarget();
            if (id is not null && pendingQuestionPoints.TryGetValue(id, out string? text))
            {
                editor.SetCurrentValue(TextBox.TextProperty, text);
            }
        }
        finally
        {
            changingPointsQuestion = false;
        }
    }

    private void RefreshValidation()
    {
        if (this.FindControl<ComboBox>("ValidationErrorSelector") is not { } selector || refreshingErrors)
        {
            return;
        }

        refreshingErrors = true;
        try
        {
            QuantificationDesignViewModel? design = DataContext as QuantificationDesignViewModel;
            foreach (string id in pendingQuestionPoints.Keys.ToArray())
            {
                if (design is null || !design.Questions.Any(question => question.Id == id))
                {
                    pendingQuestionPoints.Remove(id);
                }
            }

            selectedError = design?.ValidationErrors.FirstOrDefault(error => selectedError is not null
                && error.NodeKind == selectedError.NodeKind && error.NodeId == selectedError.NodeId
                && error.Field == selectedError.Field && error.Code == selectedError.Code)
                ?? design?.ValidationErrors.FirstOrDefault();
            selector.SetCurrentValue(ComboBox.SelectedItemProperty, selectedError);
            TextBox[] invalidInputs = NumericEditors().Where(editor => editor.Name != "SelectedQuestionPointsTextBox"
                && DataValidationErrors.GetHasErrors(editor)).ToArray();
            QuestionDesignItemViewModel[] pendingQuestions = design?.Questions
                .Where(question => pendingQuestionPoints.ContainsKey(question.Id)).ToArray() ?? [];
            int pendingCount = invalidInputs.Length + pendingQuestions.Length;
            int count = design?.ValidationErrors.Count ?? 0;
            string inputStatus = pendingCount == 0 ? string.Empty
                : $"数値として未反映の入力 {pendingCount} 件: " + string.Join("、", invalidInputs.Select(AutomationProperties.GetName)
                    .Concat(pendingQuestions.Select(question => $"{question.DisplayName} ({question.Id}) の配点")));
            string status = design is null ? "採点設計がありません。"
                : pendingCount > 0 ? $"{inputStatus}（設定エラー {count} 件）"
                : count == 0 ? "設定エラーはありません"
                : $"設定エラー {count} 件（{design.ValidationErrors.IndexOf(selectedError!) + 1} / {count}）";
            SetSummary("DesignStatusText", status);
            if (this.FindControl<Button>("ErrorDetailsButton")?.Flyout is Flyout { Content: TextBox detail })
            {
                detail.Text = string.Join(Environment.NewLine,
                    new[] { inputStatus, selectedError?.AccessibleText }.Where(text => !string.IsNullOrEmpty(text)));
            }
            this.FindControl<Button>("ErrorDetailsButton")!.IsEnabled = selectedError is not null || pendingCount > 0;
            this.FindControl<Button>("GoToProblemButton")!.IsEnabled = pendingCount > 0
                || selectedError is not null && (Shell is not null || RootEditorName(selectedError) is not null
                    || selectedError.Field == "Points");
            this.FindControl<Button>("EqualizeQuestionPointsButton")!.IsEnabled = pendingCount == 0;
            Border validation = this.FindControl<Border>("DesignValidationSummary")!;
            validation.Classes.Set("ok", design is not null && count == 0 && pendingCount == 0);
            validation.Classes.Set("invalid", count > 0 || pendingCount > 0);
        }
        finally
        {
            refreshingErrors = false;
        }
    }

    private void HandleValidationSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!refreshingErrors && sender is ComboBox { SelectedItem: DesignValidationError error })
        {
            // A revalidation replaces error objects. Retain this exact problem by
            // node/field/code instead of snapping back to the first error each time.
            selectedError = error;
            RefreshValidation();
        }
    }

    private static string? RootEditorName(DesignValidationError error) => error.Field switch
    {
        "BasePoints" => "BasePointsTextBox",
        "SpecialPoints" => "SpecialPointsTextBox",
        "SimilarityPenaltyWeight" => "SimilarityPenaltyWeightTextBox",
        _ when error.Code == "ALLOCATION_TOTAL_INVALID" => "BasePointsTextBox",
        _ => null,
    };

    private static string? InputEditorName(DesignValidationError error) => error.Field switch
    {
        "SourceSheet" => "WorksheetComboBox",
        "HeaderRow" => "HeaderRowComboBox",
        "FirstDataRow" => "FirstDataRowTextBox",
        "LastDataRow" or "SelectedRowCount" => "LastDataRowTextBox",
        _ => null,
    };

    private void HandleGoToProblem(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not QuantificationDesignViewModel design)
        {
            return;
        }

        if (selectedError is not { } error)
        {
            if (NumericEditors().FirstOrDefault(candidate => candidate.IsEffectivelyEnabled
                && DataValidationErrors.GetHasErrors(candidate)) is { } editor)
            {
                editor.Focus(NavigationMethod.Tab);
            }
            else if (design.Questions.FirstOrDefault(question => pendingQuestionPoints.ContainsKey(question.Id)) is { } pending)
            {
                design.SelectedQuestion = pending;
                Dispatcher.UIThread.Post(() =>
                {
                    if (attached && ReferenceEquals(DataContext, design) && ReferenceEquals(design.SelectedQuestion, pending))
                    {
                        FocusEditor("SelectedQuestionPointsTextBox");
                    }
                }, DispatcherPriority.Loaded);
            }

            return;
        }

        if (RootEditorName(error) is { } field)
        {
            FocusEditor(field);
            return;
        }

        if (InputEditorName(error) is { } inputField)
        {
            OpenInputProblem(inputField);
            return;
        }

        foreach (QuestionDesignItemViewModel question in design.Questions)
        {
            if (question.Id == error.NodeId)
            {
                design.SelectedQuestion = question;
                if (error.Field == "Points")
                {
                    FocusEditor("SelectedQuestionPointsTextBox");
                }
                else if (error.Field is "QuestionText" or "PrimarySourceColumn")
                {
                    OpenInputProblem(error.Field == "QuestionText" ? "QuestionTextEditor" : "PrimaryColumnComboBox", question.Id);
                }
                else
                {
                    OpenProblemSettings(error.Field.StartsWith("Evaluators", StringComparison.Ordinal)
                        ? SettingsCategory.Evaluation : SettingsCategory.Mapping);
                }

                return;
            }

            foreach (EvaluatorDesignItemViewModel evaluator in question.Evaluators)
            {
                CriterionDesignItemViewModel? criterion = evaluator.Criteria.FirstOrDefault(item => item.Id == error.NodeId);
                if (evaluator.Id == error.NodeId || criterion is not null)
                {
                    design.SelectedQuestion = question;
                    question.SelectedEvaluator = evaluator;
                    if (criterion is not null)
                    {
                        evaluator.SelectedCriterion = criterion;
                    }

                    OpenProblemSettings(SettingsCategory.Evaluation);
                    return;
                }
            }

            if (question.SpecialEvaluations.FirstOrDefault(item => item.Id == error.NodeId) is { } special)
            {
                design.SelectedQuestion = question;
                question.SelectedSpecialEvaluation = special;
                OpenProblemSettings(SettingsCategory.Special);
                return;
            }
        }

        if (error.Code == "ENABLED_QUESTION_REQUIRED" && design.SelectedQuestion is not null)
        {
            FocusEditor("SelectedQuestionEnabled");
            return;
        }

        OpenProblemSettings(error.Code == "SPECIAL_ITEMS_REQUIRED" ? SettingsCategory.Special
            : error.Field.StartsWith("Questions", StringComparison.Ordinal)
                ? SettingsCategory.Mapping : SettingsCategory.Common);
    }

    private void OpenProblemSettings(SettingsCategory category)
    {
        // 通常評価は同じ画面の統合編集部へ移る。旧カテゴリは削除済みのため、その他は共通設定を指す。
        if (category == SettingsCategory.Evaluation)
        {
            FocusEditor("EvaluatorSettingsEvaluators");
            return;
        }

        Shell?.OpenSettingsCommand.Execute(SettingsCategory.Common);
    }

    private void OpenInputProblem(string editorName, string? questionId = null)
    {
        MainWindowViewModel? shell = Shell;
        TopLevel? owner = TopLevel.GetTopLevel(this);
        if (shell is null || owner is null || !shell.NavigateCommand.CanExecute(WorkflowStep.Input))
        {
            return;
        }

        shell.NavigateCommand.Execute(WorkflowStep.Input);
        if (questionId is not null)
        {
            if (shell.InputViewModel.Questions.FirstOrDefault(question => question.Id == questionId) is not { } target)
            {
                return;
            }

            shell.InputViewModel.SelectedQuestion = target;
        }

        Dispatcher.UIThread.Post(() =>
        {
            // Navigation can detach this cached Design view. Focus only the
            // current Input owned by the same shell, never a stale/foreign view.
            if (!ReferenceEquals(owner.DataContext, shell) || shell.IsSettingsOpen || shell.CurrentStep != WorkflowStep.Input)
            {
                return;
            }

            InputView? input = owner.GetVisualDescendants().OfType<InputView>().FirstOrDefault(view =>
                ReferenceEquals(view.DataContext, shell.InputViewModel) && view.IsEffectivelyVisible);
            if (input?.FindControl<Control>(editorName) is { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } editor)
            {
                editor.BringIntoView();
                editor.Focus(NavigationMethod.Tab);
            }
        }, DispatcherPriority.Loaded);
    }

    private void FocusEditor(string name)
    {
        if (this.FindControl<Control>(name) is { } control)
        {
            control.BringIntoView();
            control.Focus(NavigationMethod.Tab);
        }
    }

    private void HandleLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= HandleLoaded;
        RefreshSettingsLinks();
        if (TopLevel.GetTopLevel(this) is not Window window
            || !ReferenceEquals(window.Content, this))
        {
            return;
        }

        Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("BasePointsTextBox")?.Focus(
            NavigationMethod.Tab,
            KeyModifiers.None));
    }
}
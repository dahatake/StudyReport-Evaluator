using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.Composition;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.ViewModels;

namespace StudyReportEvaluator.App.Views;

public sealed partial class MainWindow : Window
{
    // One control per existing editor owner; no hidden sibling views or second draft.
    private readonly Dictionary<UiObservableObject, Control> editorViews = new(ReferenceEqualityComparer.Instance);
    private UiObservableObject? currentEditor;
    private UiObservableObject? settingsReturnEditor;
    private Control? settingsReturnTarget;
    private long focusVersion;
    private bool opened;
    private bool closed;
    private bool closePending;
    private bool finalCloseAllowed;

    public MainWindow()
        : this(new ServiceRegistration().CreateMainWindowViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += HandleViewModelPropertyChanged;
        Opened += HandleOpened;
        Closing += HandleClosing;
        Closed += HandleClosed;
        ShowCurrentEditor();
    }

    public MainWindowViewModel ViewModel { get; }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (null or "" or nameof(MainWindowViewModel.CurrentEditorViewModel)
            or nameof(MainWindowViewModel.IsSettingsOpen)))
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ShowCurrentEditor();
        }
        else
        {
            Dispatcher.UIThread.Post(ShowCurrentEditor);
        }
    }

    private void ShowCurrentEditor()
    {
        UiObservableObject? editor = ViewModel.CurrentEditorViewModel;
        if (closed || ReferenceEquals(currentEditor, editor))
        {
            return;
        }

        Control? returnTarget = null;
        if (editor is SettingsViewModel && currentEditor is not SettingsViewModel)
        {
            // Capture before detaching the invoking view, not from a control name
            // after it has been recreated. Category changes do not replace this target.
            settingsReturnEditor = currentEditor;
            settingsReturnTarget = FocusManager?.GetFocusedElement() as Control;
        }
        else if (currentEditor is SettingsViewModel)
        {
            if (ReferenceEquals(editor, settingsReturnEditor))
            {
                returnTarget = settingsReturnTarget;
            }

            settingsReturnEditor = null;
            settingsReturnTarget = null;
        }

        ContentControl content = this.FindControl<ContentControl>("CurrentStepContent")!;
        Control? view = null;
        if (editor is not null && !editorViews.TryGetValue(editor, out view))
        {
            IDataTemplate template = content.DataTemplates.First(candidate => candidate.Match(editor));
            view = template.Build(editor)
                ?? throw new InvalidOperationException("The current editor template must produce a control.");
            // Build does not set DataContext. A template-root binding would read
            // the host's inherited shell context when it is applied. Set this local
            // owner once; cache hits must not rebind or reset unfinished editors.
            view.DataContext = editor;
            editorViews.Add(editor, view);
        }

        content.Content = null;
        currentEditor = editor;
        content.Content = view;
        focusVersion++;
        this.FindControl<ScrollViewer>("ShellScrollViewer")!.Offset = default;
        // Settings owns its five categories and their initial focus. The shell
        // must not reset the cached Settings DataContext or initialize its store.
        if (opened && view is not null && editor is not SettingsViewModel)
        {
            FocusEditorAfterLayout(view, returnTarget);
        }
    }

    private void HandleBodySizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!closed && double.IsFinite(e.NewSize.Height) && e.NewSize.Height > 0d)
        {
            // Normal editors already fit a 450-DIP slot. Narrow Input reflows its
            // range fields into two rows; only that smaller viewport needs more
            // canvas. No header/footer is measured at infinity or scrolled away.
            double minimumHeight = e.NewSize.Width < 856d ? 520d : 450d;
            this.FindControl<Grid>("ShellBody")!.Height = Math.Max(minimumHeight, e.NewSize.Height);
        }
    }

    private void FocusEditorAfterLayout(Control view, Control? returnTarget = null)
    {
        long scheduledVersion = focusVersion;
        Dispatcher.UIThread.Post(() =>
        {
            if (closed || !opened || scheduledVersion != focusVersion
                || !ReferenceEquals(this.FindControl<ContentControl>("CurrentStepContent")!.Content, view))
            {
                return;
            }

            if (TryFocus(returnTarget))
            {
                return;
            }

            // GoToProblem posts a specific field focus at Loaded priority. Run
            // afterwards and keep that focus instead of stealing it for the default.
            if (FocusManager?.GetFocusedElement() is Control focused
                && focused.IsEffectivelyVisible && focused.IsEffectivelyEnabled
                && focused.GetVisualAncestors().Contains(view))
            {
                return;
            }

            Control? target = view switch
            {
                InputView input => input.FindControl<TextBox>("FilePathTextBox"),
                QuantificationDesignView design => design.FindControl<TextBox>("BasePointsTextBox"),
                ExecutionView execution => execution.FindControl<Button>("CheckAuthenticationButton"),
                ResultsOutputView results => ResultsFocusTarget(results),
                _ => null,
            };
            if (!TryFocus(target))
            {
                TryFocus(view.GetVisualDescendants().OfType<Control>().FirstOrDefault(control =>
                    control.Focusable && control.IsTabStop && control.IsEffectivelyVisible && control.IsEffectivelyEnabled));
            }
        }, DispatcherPriority.Background);
    }

    private static Control? ResultsFocusTarget(ResultsOutputView view)
    {
        ListBox? list = view.FindControl<ListBox>(view.ViewModel.IsDetailVisible ? "ResultsList" : "RowScoreList");
        return list is { SelectedIndex: >= 0 } ? list.ContainerFromIndex(list.SelectedIndex) ?? list : list;
    }

    private bool TryFocus(Control? target)
    {
        if (target is not { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true }
            || !ReferenceEquals(TopLevel.GetTopLevel(target), this))
        {
            return false;
        }

        target.BringIntoView();
        return target.Focus(NavigationMethod.Tab, KeyModifiers.None);
    }

    private void HandleOpened(object? sender, EventArgs e)
    {
        Opened -= HandleOpened;
        opened = true;
        if (!ViewModel.IsSettingsOpen && ViewModel.CurrentStep == WorkflowStep.Input)
        {
            TryFocus(this.FindControl<Button>("InputStepButton"));
        }
        else if (this.FindControl<ContentControl>("CurrentStepContent")!.Content is Control view
            && view is not SettingsView)
        {
            FocusEditorAfterLayout(view);
        }
    }

    private void HandleClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closed || finalCloseAllowed) return;

        if (e.CloseReason == WindowCloseReason.OSShutdown)
        {
            // OS termination cannot be delayed reliably. Request cooperative
            // interruption, but do not veto shutdown or kill any process.
            if (!closePending)
            {
                closePending = true;
                _ = DrainBeforeCloseAsync(closeAfterDrain: false);
            }
            return;
        }

        // Repeated user/programmatic requests must not skip the pending drain,
        // including the interval between IsRunning=false and task completion.
        if (closePending)
        {
            e.Cancel = true;
            return;
        }

        ExecutionViewModel execution = ViewModel.ExecutionViewModel;
        if (!execution.IsRunning && execution.LastRunTask is not { IsCompleted: false }) return;
        e.Cancel = true;
        closePending = true;
        _ = DrainBeforeCloseAsync(closeAfterDrain: true);
    }

    private async Task DrainBeforeCloseAsync(bool closeAfterDrain)
    {
        try
        {
            await ViewModel.ExecutionViewModel.StopAndDrainAsync(TimeSpan.FromSeconds(10));
        }
        catch
        {
            // Shutdown is finite and never depends on successful evaluation.
            // Do not expose exception details or interfere with external tools.
        }
        finally
        {
            if (closeAfterDrain)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (closed) return;
                    finalCloseAllowed = true; // Only this internal Close bypasses the guard.
                    Close();
                });
            }
        }
    }

    private void HandleClosed(object? sender, EventArgs e)
    {
        closed = true;
        opened = false;
        focusVersion++;
        Opened -= HandleOpened;
        Closing -= HandleClosing;
        Closed -= HandleClosed;
        ViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        this.FindControl<ContentControl>("CurrentStepContent")!.Content = null;
        currentEditor = null;
        settingsReturnEditor = null;
        settingsReturnTarget = null;
        editorViews.Clear();
        ViewModel.Dispose();
    }
}
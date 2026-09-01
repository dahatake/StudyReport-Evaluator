using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StudyReportEvaluator.App.ViewModels;

namespace StudyReportEvaluator.App.Views;

public sealed partial class ExecutionView : UserControl
{
    public ExecutionView()
        : this(new ExecutionViewModel())
    {
    }

    public ExecutionView(ExecutionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        Loaded += HandleLoaded;
    }

    public ExecutionViewModel ViewModel => DataContext as ExecutionViewModel
        ?? throw new InvalidOperationException("ExecutionView requires an ExecutionViewModel data context.");

    private void HandleLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= HandleLoaded;
        if (TopLevel.GetTopLevel(this) is not Window window
            || !ReferenceEquals(window.Content, this))
        {
            return;
        }

        Dispatcher.UIThread.Post(() => this.FindControl<Button>("CheckAuthenticationButton")?.Focus(
            NavigationMethod.Tab,
            KeyModifiers.None));
    }
}
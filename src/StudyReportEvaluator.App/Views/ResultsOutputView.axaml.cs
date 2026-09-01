using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StudyReportEvaluator.App.ViewModels;

namespace StudyReportEvaluator.App.Views;

public sealed partial class ResultsOutputView : UserControl
{
    public ResultsOutputView()
        : this(new ResultsOutputViewModel())
    {
    }

    public ResultsOutputView(ResultsOutputViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        Loaded += HandleLoaded;
    }

    public ResultsOutputViewModel ViewModel => DataContext as ResultsOutputViewModel
        ?? throw new InvalidOperationException("ResultsOutputView requires a ResultsOutputViewModel data context.");

    private void HandleLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= HandleLoaded;
        if (TopLevel.GetTopLevel(this) is not Window window
            || !ReferenceEquals(window.Content, this))
        {
            return;
        }

        Dispatcher.UIThread.Post(() => this.FindControl<ListBox>("ResultsList")?.Focus(
            NavigationMethod.Tab,
            KeyModifiers.None));
    }
}
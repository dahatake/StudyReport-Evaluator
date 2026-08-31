using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StudyReportEvaluator.App.ViewModels;

namespace StudyReportEvaluator.App.Views;

public sealed partial class InputView : UserControl
{
    public InputView()
        : this(new InputViewModel())
    {
    }

    public InputView(InputViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        Loaded += HandleLoaded;
    }

    public InputViewModel ViewModel => DataContext as InputViewModel
        ?? throw new InvalidOperationException("InputView requires an InputViewModel data context.");

    private void HandleLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= HandleLoaded;
        if (TopLevel.GetTopLevel(this) is not Window window
            || !ReferenceEquals(window.Content, this))
        {
            return;
        }

        Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("FilePathTextBox")?.Focus(
            NavigationMethod.Tab,
            KeyModifiers.None));
    }
}
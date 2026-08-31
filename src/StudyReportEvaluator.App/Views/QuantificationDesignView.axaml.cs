using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StudyReportEvaluator.App.ViewModels;

namespace StudyReportEvaluator.App.Views;

public sealed partial class QuantificationDesignView : UserControl
{
    public QuantificationDesignView()
        : this(new QuantificationDesignViewModel())
    {
    }

    public QuantificationDesignView(QuantificationDesignViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        Loaded += HandleLoaded;
    }

    public QuantificationDesignViewModel ViewModel => DataContext as QuantificationDesignViewModel
        ?? throw new InvalidOperationException("QuantificationDesignView requires a QuantificationDesignViewModel data context.");

    private void HandleLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= HandleLoaded;
        if (TopLevel.GetTopLevel(this) is not Window window
            || !ReferenceEquals(window.Content, this))
        {
            return;
        }

        Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("DefinitionNameTextBox")?.Focus(
            NavigationMethod.Tab,
            KeyModifiers.None));
    }
}
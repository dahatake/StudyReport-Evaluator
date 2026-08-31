using Avalonia.Controls;
using Avalonia.Input;
using StudyReportEvaluator.App.Composition;
using StudyReportEvaluator.App.ViewModels;

namespace StudyReportEvaluator.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
        : this(new ServiceRegistration().CreateMainWindowViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = ViewModel;
        Opened += HandleOpened;
        Closed += HandleClosed;
    }

    public MainWindowViewModel ViewModel { get; }

    private void HandleOpened(object? sender, EventArgs e)
    {
        Opened -= HandleOpened;
        this.FindControl<Button>("InputStepButton")?.Focus(NavigationMethod.Tab, KeyModifiers.None);
    }

    private void HandleClosed(object? sender, EventArgs e)
    {
        Closed -= HandleClosed;
        ViewModel.Dispose();
    }
}
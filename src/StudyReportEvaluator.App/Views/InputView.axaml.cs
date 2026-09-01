using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using StudyReportEvaluator.App.ViewModels;

namespace StudyReportEvaluator.App.Views;

public interface IInputWorkbookPicker
{
    Task<string?> PickAsync(TopLevel topLevel);
}

public sealed class InputWorkbookPickerPathUnavailableException : Exception
{
    public InputWorkbookPickerPathUnavailableException()
        : base("The selected workbook does not expose a local path.")
    {
    }
}

public sealed class NativeInputWorkbookPicker : IInputWorkbookPicker
{
    private static readonly FilePickerFileType StandardXlsx = new("標準 Excel workbook (.xlsx)")
    {
        Patterns = ["*.xlsx"],
        AppleUniformTypeIdentifiers = ["org.openxmlformats.spreadsheetml.sheet"],
        MimeTypes = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
    };

    public async Task<string?> PickAsync(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "評価する標準 .xlsx を選択",
                AllowMultiple = false,
                FileTypeFilter = [StandardXlsx],
                SuggestedFileType = StandardXlsx,
            });
        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath()
            ?? throw new InputWorkbookPickerPathUnavailableException();
    }

    public override string ToString() =>
        $"{nameof(NativeInputWorkbookPicker)} {{ Content = <redacted> }}";
}

public sealed partial class InputView : UserControl
{
    private readonly IInputWorkbookPicker picker;
    private bool isPicking;

    public InputView()
        : this(new InputViewModel(), new NativeInputWorkbookPicker())
    {
    }

    public InputView(InputViewModel viewModel)
        : this(viewModel, new NativeInputWorkbookPicker())
    {
    }

    public InputView(
        InputViewModel viewModel,
        IInputWorkbookPicker picker)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        this.picker = picker ?? throw new ArgumentNullException(nameof(picker));
        InitializeComponent();
        DataContext = viewModel;
        Loaded += HandleLoaded;
    }

    public InputViewModel ViewModel => DataContext as InputViewModel
        ?? throw new InvalidOperationException("InputView requires an InputViewModel data context.");

    public async Task PickFileAsync()
    {
        if (isPicking || TopLevel.GetTopLevel(this) is not TopLevel topLevel)
        {
            return;
        }

        isPicking = true;
        Button? button = this.FindControl<Button>("PickFileButton");
        if (button is not null)
        {
            button.IsEnabled = false;
        }

        try
        {
            string? selectedPath = await picker.PickAsync(topLevel);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                SetPickerStatus(string.Empty);
                await ViewModel.SetFilePathAsync(selectedPath);
            }
        }
        catch (InputWorkbookPickerPathUnavailableException)
        {
            SetPickerStatus("選択したファイルのlocal pathを取得できません。pathを直接入力してください。");
        }
        catch
        {
            SetPickerStatus("native pickerを利用できません。pathを直接入力してください。");
        }
        finally
        {
            isPicking = false;
            if (button is not null)
            {
                button.IsEnabled = true;
            }
        }
    }

    private async void HandlePickFileClick(object? sender, RoutedEventArgs e) =>
        await PickFileAsync();

    private void SetPickerStatus(string message)
    {
        TextBlock? status = this.FindControl<TextBlock>("PickerStatusMessage");
        if (status is not null)
        {
            status.Text = message;
            status.IsVisible = message.Length > 0;
        }
    }

    private async void HandleLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= HandleLoaded;
        await ViewModel.LoadLaunchInputIfRequestedAsync();
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
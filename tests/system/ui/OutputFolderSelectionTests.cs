using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.Settings;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Serialization;
using Control = Avalonia.Controls.Control;
using Xunit;

namespace StudyReportEvaluator.SystemTests.Ui;

/// <summary>
/// FR-002 AC-055 ①–⑤; oracle: AC-055 and requirements §9.1/11.6/11.8.
/// Replace only Avalonia's documented storage-provider factory, not the application's
/// picker/selection logic. Real routed mouse/keyboard events drive the real SettingsView.
/// The OS dialog's visual appearance/focus is outside a headless platform's capability.
/// </summary>
public sealed class OutputFolderSelectionTests
{
    // FR-002 AC-055 ⑤: UI changes while an injected run is pending affect ONLY next run.
    [AvaloniaFact]
    public async Task FR002_AC055_Picker_and_reset_during_run_preserve_snapshot_and_reserved_paths()
    {
        using TestFiles files = new();
        ScriptedStorage storage = new();
        using IDisposable scope = InstallStorageFactory(storage.Factory);
        PendingRun runner = new(files.Root);
        using Session session = await Session.Open(files, new AvailableAuthentication(), runner);
        await session.Load(files.InputA);
        ExecutionViewModel execution = session.Shell.ExecutionViewModel;
        QuantificationDefinition definition = new()
        {
            Id = "AC055-synthetic", Name = "Synthetic one answer", Revision = "1",
            SourceSheet = "Original", HeaderRow = 1, FirstDataRow = 2, LastDataRow = 2, BasePoints = 98,
            Questions = [new QuestionDefinition
            {
                Id = "Q1", DisplayName = "Question", QuestionText = "Synthetic question",
                PrimarySourceColumn = "A", Points = 2, Enabled = true,
                Evaluators = [new EvaluatorDefinition
                {
                    Id = "E1", DisplayName = "Evaluator", Type = EvaluatorType.CustomPrompt,
                    CustomPromptTemplate = "{回答} {評価項目}", Weight = 1, Range = new ScoreRange(0, 10), Enabled = true,
                    Criteria = [new CriterionDefinition
                    {
                        Id = "C1", DisplayName = "Criterion", Description = "Synthetic criterion", Weight = 1, Enabled = true,
                    }],
                }],
            }],
        };
        execution.Configure(definition, session.Shell.InputViewModel.Metadata!, files.InputA);
        await execution.CheckAuthenticationAsync();
        string original = Path.Combine(files.Root, "run-output");
        string next = Path.Combine(files.Root, "next-output");
        storage.Select(new Uri(original));
        Activate(session.Window, Picker(session), keyboard: false);
        await Until(() => Output(session).Text == original);
        Assert.True(execution.CanStart, execution.ValidationSummary);
        using CancellationTokenSource cancel = new();
        Task running = execution.StartAsync(cancel.Token);
        try
        {
            await Until(() => runner.Request is not null && execution.ReservedFinalPath == runner.Final);
            QuantificationRunRequest request = runner.Request!;
            Assert.Equal(Output(session).Text, request.OutputDirectory);
            Assert.Equal(runner.Final, execution.ReservedFinalPath);
            Assert.Equal(runner.Partial, execution.PartialPath);
            string canonical = new CanonicalDefinitionSerializer().Serialize(request.DraftDefinition);
            string currentOutput = execution.CurrentRunOutputSummary;
            string[] before = files.Inventory();
            byte[]? settingsBefore = File.Exists(files.Settings) ? await File.ReadAllBytesAsync(files.Settings) : null;

            storage.Select(new Uri(next));
            Activate(session.Window, Picker(session), keyboard: true);
            await Until(() => Output(session).Text == next);
            Assert.Equal(next, execution.OutputDirectoryOverride);
            storage.Cancel();
            Activate(session.Window, Picker(session), keyboard: false);
            await Until(() => storage.Calls == 3);
            Assert.Equal(next, Output(session).Text);
            Activate(session.Window, Reset(session), keyboard: true);
            await Until(() => execution.OutputDirectoryOverride is null);
            Assert.Equal(Path.Combine(Path.GetDirectoryName(files.InputA)!, "result"), Output(session).Text);
            Assert.Same(request, runner.Request);
            Assert.Equal(original, request.OutputDirectory);
            Assert.Equal(canonical, new CanonicalDefinitionSerializer().Serialize(request.DraftDefinition));
            Assert.Equal(currentOutput, execution.CurrentRunOutputSummary);
            Assert.Equal(runner.Final, execution.ReservedFinalPath);
            Assert.Equal(runner.Partial, execution.PartialPath);
            Assert.True(execution.IsRunning);
            Assert.Equal(before, files.Inventory());
            Assert.Equal(settingsBefore, File.Exists(files.Settings) ? await File.ReadAllBytesAsync(files.Settings) : null);
        }
        finally
        {
            cancel.Cancel();
            await running.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [AvaloniaFact]
    public async Task FR002_AC055_Native_selection_cancel_failure_save_restore_and_default_are_one_journey()
    {
        using TestFiles files = new();
        ScriptedStorage storage = new();
        using IDisposable scope = InstallStorageFactory(storage.Factory);
        using Session first = await Session.Open(files);
        Assert.Same(storage.Provider, first.Window.StorageProvider);
        await first.Load(files.InputA);
        TextBox display = Output(first);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(files.InputA)!, "result"), display.Text);
        Assert.Null(first.Shell.ExecutionViewModel.OutputDirectoryOverride);
        Assert.False(File.Exists(files.Settings));
        string chosen = Path.Combine(files.Root, "chosen", "日本語 folder", new string('x', 100));
        string[] beforeSelection = files.Inventory();

        // Native API contract: mouse invokes OpenFolderPickerAsync on this window's provider.
        storage.Select(new Uri(chosen));
        Activate(first.Window, Picker(first), keyboard: false);
        await Until(() => storage.Calls == 1 && Output(first).Text == chosen);
        Assert.NotNull(storage.LastOptions);
        Assert.False(storage.LastOptions!.AllowMultiple);
        Assert.Equal(chosen, first.Shell.ExecutionViewModel.OutputDirectoryOverride);
        Assert.Equal(Output(first).Text, first.Shell.ExecutionViewModel.OutputDirectory);
        Assert.Equal(beforeSelection, files.Inventory()); // selection is NOT mkdir or autosave
        await AssertCopyableButNotEditable(first, chosen);

        // Only explicit Save persists the override; no output directory is created.
        await Save(first);
        byte[] saved = await File.ReadAllBytesAsync(files.Settings);
        Assert.Equal(chosen, (await new SettingsFileStore(files.Settings).LoadAsync()).Settings!.OutputDirectoryOverride);
        Assert.False(Directory.Exists(chosen));

        await first.Load(files.InputB);
        Assert.Equal(chosen, Output(first).Text);
        Assert.Equal(chosen, first.Shell.ExecutionViewModel.OutputDirectoryOverride);
        Assert.Equal(saved, await File.ReadAllBytesAsync(files.Settings));
        string[] beforeCancel = files.Inventory();

        // Keyboard activation must reach the native provider too; cancel has no side effects.
        storage.Cancel();
        Activate(first.Window, Picker(first), keyboard: true);
        await Until(() => storage.Calls == 2);
        Assert.Equal(chosen, Output(first).Text);
        Assert.Equal(chosen, first.Shell.ExecutionViewModel.OutputDirectoryOverride);
        Assert.Equal(saved, await File.ReadAllBytesAsync(files.Settings));
        Assert.Equal(beforeCancel, files.Inventory());

        // Both open failure and a non-local result need a visible reason, never a text fallback.
        storage.Fail(new IOException("synthetic picker unavailable"));
        Activate(first.Window, Picker(first), keyboard: false);
        await Until(() => storage.Calls == 3 && HasReason(first));
        AssertFailedSelectionUnchanged(first, files, chosen, saved, beforeCancel);
        storage.Select(new Uri("https://example.invalid/non-local-folder"));
        Activate(first.Window, Picker(first), keyboard: true);
        await Until(() => storage.Calls == 4 && HasReason(first));
        AssertFailedSelectionUnchanged(first, files, chosen, saved, beforeCancel);

        first.Close();
        string[] beforeRestore = files.Inventory();
        using Session restored = await Session.Open(files);
        Assert.Equal(chosen, Output(restored).Text);
        Assert.Equal(chosen, restored.Shell.ExecutionViewModel.OutputDirectoryOverride);
        Assert.Equal(beforeRestore, files.Inventory()); // restore alone cannot mkdir/write output
        await restored.Load(files.InputB);
        Assert.Equal(chosen, Output(restored).Text);

        // Reset is a UI action. Saving null must supersede the previously saved override.
        Activate(restored.Window, Reset(restored), keyboard: true);
        await Until(() => restored.Shell.ExecutionViewModel.OutputDirectoryOverride is null);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(files.InputB)!, "result"), Output(restored).Text);
        Assert.Equal(saved, await File.ReadAllBytesAsync(files.Settings)); // still explicit-save only
        await Save(restored);
        Assert.Null((await new SettingsFileStore(files.Settings).LoadAsync()).Settings!.OutputDirectoryOverride);
        byte[] resetSaved = await File.ReadAllBytesAsync(files.Settings);
        await restored.Load(files.InputA);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(files.InputA)!, "result"), Output(restored).Text);
        Assert.Null(restored.Shell.ExecutionViewModel.OutputDirectoryOverride);
        Assert.Equal(resetSaved, await File.ReadAllBytesAsync(files.Settings));
        restored.Close();

        beforeRestore = files.Inventory();
        using Session defaults = await Session.Open(files);
        Assert.Null(defaults.Shell.ExecutionViewModel.OutputDirectoryOverride);
        TextBox empty = Output(defaults);
        Assert.True(empty.Text == "入力後に決定" ||
            (string.IsNullOrEmpty(empty.Text) && empty.PlaceholderText == "入力後に決定"));
        Assert.Equal(beforeRestore, files.Inventory());
        await defaults.Load(files.InputB);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(files.InputB)!, "result"), Output(defaults).Text);
        Assert.Null(defaults.Shell.ExecutionViewModel.OutputDirectoryOverride);
        Assert.Equal(resetSaved, await File.ReadAllBytesAsync(files.Settings));
        Assert.False(Directory.Exists(chosen));
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(files.InputA)!, "result")));
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(files.InputB)!, "result")));
    }

    private static void AssertFailedSelectionUnchanged(
        Session session, TestFiles files, string chosen, byte[] saved, string[] inventory)
    {
        Assert.True(HasReason(session));
        Assert.Equal(chosen, Output(session).Text);
        Assert.True(Output(session).IsReadOnly);
        Assert.Null(Find<Control>(session.View, "SettingsEffectiveOutputDirectory"));
        Assert.Equal(chosen, session.Shell.ExecutionViewModel.OutputDirectoryOverride);
        Assert.Equal(saved, File.ReadAllBytes(files.Settings));
        Assert.Equal(inventory, files.Inventory());
        Assert.DoesNotContain(session.View.GetVisualDescendants().OfType<TextBox>(), c =>
            !c.IsReadOnly && (AutomationProperties.GetAutomationId(c) ?? c.Name ?? "")
                .Contains("Output", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasReason(Session s) => s.View.GetVisualDescendants().OfType<TextBlock>()
        .Any(c =>
        {
            // Dedicated output-picker status anchor, not unrelated login/save status.
            // Wording is deliberately NOT an oracle: AC requires a reason, not fixed copy.
            string id = AutomationProperties.GetAutomationId(c) ?? c.Name ?? "";
            return c.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(c.Text) &&
                id.Contains("Output", StringComparison.OrdinalIgnoreCase) &&
                (id.Contains("Status", StringComparison.OrdinalIgnoreCase) ||
                 id.Contains("Message", StringComparison.OrdinalIgnoreCase));
        });

    private static async Task AssertCopyableButNotEditable(Session s, string chosen)
    {
        TextBox output = Output(s);
        Assert.True(output.IsReadOnly);
        Assert.True(output.Focus(NavigationMethod.Tab));
        Press(s.Window, Key.A, RawInputModifiers.Control);
        Assert.Equal(chosen, output.SelectedText);
        // Avalonia 12 clipboard access is provided by the top-level.
        var clipboard = s.Window.Clipboard!;
        Assert.NotNull(clipboard);
        Press(s.Window, Key.C, RawInputModifiers.Control);
        await Until(async () => await clipboard.TryGetTextAsync() == chosen);
        s.Window.KeyTextInput("must not replace");
        Press(s.Window, Key.Back);
        await clipboard.SetTextAsync("must not paste");
        Press(s.Window, Key.V, RawInputModifiers.Control);
        Render();
        Assert.Equal(chosen, output.Text);
        Assert.Equal(chosen, s.Shell.ExecutionViewModel.OutputDirectoryOverride);
    }

    private static async Task Save(Session s)
    {
        Activate(s.Window, Required<Button>(s.View, "SettingsSave"), keyboard: false);
        await Until(() => s.Shell.Settings.LastSaveTask is not null);
        await s.Shell.Settings.LastSaveTask!;
        Assert.Equal(SettingsSaveStatus.Saved, s.Shell.Settings.SaveStatus);
        Render();
    }

    private static T? Find<T>(Visual root, string id) where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(c =>
            AutomationProperties.GetAutomationId(c) == id || c.Name == id);
    private static T Required<T>(Visual root, string id) where T : Control =>
        Find<T>(root, id) ?? throw new Xunit.Sdk.XunitException($"Missing control {id}");
    private static TextBox Output(Session s) => Required<TextBox>(s.View, "ExecutionOutputDirectory");
    private static string Label(Button b) => $"{b.Content} {AutomationProperties.GetName(b)}";
    private static Button Picker(Session s) => Assert.Single(s.View.GetVisualDescendants().OfType<Button>(),
        b => Label(b).Contains("フォルダー", StringComparison.Ordinal) &&
            (Label(b).Contains("選択", StringComparison.Ordinal) || Label(b).Contains("変更", StringComparison.Ordinal)));
    private static Button Reset(Session s) => Assert.Single(s.View.GetVisualDescendants().OfType<Button>(),
        b => Label(b).Contains("既定に戻す", StringComparison.Ordinal) &&
            AutomationProperties.GetAutomationId(b) != "ExecutionResetModelOptions");

    private static IDisposable InstallStorageFactory(IStorageProviderFactory factory)
    {
        // Avalonia 12 hides locator registration from application code. Test-only reflection
        // replaces the platform boundary in a disposable scope; no application fields are read.
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        Type locator = typeof(AvaloniaLocator);
        IDisposable scope = (IDisposable)locator.GetMethod("EnterScope", flags)!.Invoke(null, null)!;
        try
        {
            object mutable = locator.GetProperty("CurrentMutable", flags)!.GetValue(null)!;
            object binding = locator.GetMethod("Bind", BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance)!.MakeGenericMethod(typeof(IStorageProviderFactory)).Invoke(mutable, null)!;
            MethodInfo constant = binding.GetType().GetMethod("ToConstant")!;
            if (constant.IsGenericMethodDefinition)
                constant = constant.MakeGenericMethod(typeof(IStorageProviderFactory));
            constant.Invoke(binding, [factory]);
            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    private static void Activate(Window w, Button b, bool keyboard)
    {
        Assert.True(b.IsEffectivelyEnabled);
        if (keyboard)
        {
            Assert.True(b.Focus(NavigationMethod.Tab));
            Press(w, Key.Enter);
        }
        else
        {
            Point p = b.TranslatePoint(new Point(b.Bounds.Width / 2, b.Bounds.Height / 2), w)!.Value;
            w.MouseDown(p, MouseButton.Left);
            w.MouseUp(p, MouseButton.Left);
            Render();
        }
    }

    private static void Press(Window w, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        w.KeyPress(key, modifiers, PhysicalKey.None, null);
        w.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Render();
    }
    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
    private static Task Until(Func<bool> condition) => Until(() => Task.FromResult(condition()));
    private static async Task Until(Func<Task<bool>> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        do
        {
            Render();
            if (await condition()) return;
            await Task.Delay(10);
        } while (DateTime.UtcNow < deadline);
        Assert.Fail("UI operation did not complete within 5 seconds.");
    }

    private sealed class Session : IDisposable
    {
        private bool closed;
        public MainWindowViewModel Shell { get; }
        public MainWindow Window { get; }
        public SettingsView View => Assert.Single(Window.GetVisualDescendants().OfType<SettingsView>());
        private Session(TestFiles files, IExecutionAuthenticationBoundary? authentication, IQuantificationRunBoundary? run)
        {
            InputViewModel input = new(new InputWorkbookLoader());
            Shell = new MainWindowViewModel(new WorkflowNavigator(), input,
                new QuantificationDesignViewModel(input.DefinitionDraft, input.AvailableColumnNames),
                new ExecutionViewModel(authentication ?? new OfflineAuthentication(), run ?? new DeniedRun(),
                    new StudyReportEvaluator.App.Copilot.BundledCopilotLoginService(new DeniedLogin(),
                        _ => throw new Xunit.Sdk.XunitException("Test must never launch a CLI."))),
                new ResultsOutputViewModel(), new SettingsFileStore(files.Settings));
            Window = new MainWindow(Shell) { Width = 1280, Height = 900 };
        }
        public static async Task<Session> Open(TestFiles files,
            IExecutionAuthenticationBoundary? authentication = null, IQuantificationRunBoundary? run = null)
        {
            Session s = new(files, authentication, run);
            await s.Shell.Settings.InitializeAsync();
            s.Window.Show();
            Render();
            s.Shell.OpenSettingsCommand.Execute(null);
            Render();
            return s;
        }
        public async Task Load(string path)
        {
            Shell.InputViewModel.FilePath = path;
            await Shell.InputViewModel.LoadAsync();
            Assert.NotNull(Shell.InputViewModel.Metadata);
            Shell.ExecutionViewModel.Configure(Shell.InputViewModel.DefinitionDraft,
                Shell.InputViewModel.Metadata!, path);
            Render();
        }
        public void Close()
        {
            if (closed) return;
            Window.Close();
            Shell.Dispose();
            closed = true;
        }
        public void Dispose() => Close();
    }

    private sealed class OfflineAuthentication : IExecutionAuthenticationBoundary
    {
        public Task<ExecutionAuthenticationSnapshot> CheckAsync(CancellationToken token) =>
            Task.FromResult(new ExecutionAuthenticationSnapshot(ExecutionAuthenticationState.AuthRequired));
    }
    private sealed class AvailableAuthentication : IExecutionAuthenticationBoundary
    {
        public Task<ExecutionAuthenticationSnapshot> CheckAsync(CancellationToken token) =>
            Task.FromResult(new ExecutionAuthenticationSnapshot(ExecutionAuthenticationState.Available,
                [new StudyReportEvaluator.App.Copilot.CopilotModelAvailability("model-test", 64000, 128000)],
                new StudyReportEvaluator.App.Copilot.CopilotRuntimeIdentity(
                    Path.Combine(Path.GetTempPath(), "synthetic-never-launch.exe"), "1.0.82", new string('B', 64), "1.0.11")));
    }
    private sealed class PendingRun(string root) : IQuantificationRunBoundary
    {
        public QuantificationRunRequest? Request { get; private set; }
        public string Final => Path.Combine(root, "run-output", "eval-20261103-2044.xlsx");
        public string Partial => Path.Combine(root, "run-output", "eval-20261103-2044.partial.xlsx");
        public async Task<RunSummary> RunAsync(QuantificationRunRequest request,
            Action<EvaluationProgress>? progress, CancellationToken token)
        {
            Request = request;
            progress?.Invoke(new EvaluationProgress(1, 0, 1, EvaluationProgressStatus.Running,
                DurableEvaluationStage.EvaluatingRows, finalPath: Final, partialPath: Partial));
            await Task.Delay(Timeout.Infinite, token);
            throw new Xunit.Sdk.XunitException("Pending run must be cancelled by the fixture.");
        }
    }
    private sealed class DeniedLogin : StudyReportEvaluator.App.Copilot.ICopilotCliPathResolver
    {
        public ValueTask<string?> ResolveAsync(CancellationToken token) => ValueTask.FromResult<string?>(null);
    }
    private sealed class DeniedRun : IQuantificationRunBoundary
    {
        public Task<StudyReportEvaluator.App.Workflow.RunSummary> RunAsync(
            QuantificationRunRequest request, Action<StudyReportEvaluator.App.Workflow.EvaluationProgress>? progress,
            CancellationToken token) => throw new Xunit.Sdk.XunitException("Selection must never start an AI run.");
    }

    // DispatchProxy keeps the fixture tied to the public Avalonia platform contract, not
    // to any new application-side DI API or a duplicate implementation of picker logic.
    private sealed class ScriptedStorage
    {
        private readonly Queue<Func<IReadOnlyList<IStorageFolder>>> responses = new();
        public int Calls { get; private set; }
        public FolderPickerOpenOptions? LastOptions { get; private set; }
        public IStorageProviderFactory Factory { get; }
        public IStorageProvider Provider { get; }
        public ScriptedStorage()
        {
            IStorageProvider provider = ContractProxy.Make<IStorageProvider>((method, args) =>
            {
                if (method.Name == "get_CanPickFolder") return true;
                if (method.Name == "OpenFolderPickerAsync")
                {
                    Calls++;
                    LastOptions = Assert.IsType<FolderPickerOpenOptions>(args![0]);
                    try { return Task.FromResult(responses.Dequeue()()); }
                    catch (Exception e) { return Task.FromException<IReadOnlyList<IStorageFolder>>(e); }
                }
                throw new Xunit.Sdk.XunitException($"Unexpected storage operation: {method.Name}");
            });
            Provider = provider;
            Factory = ContractProxy.Make<IStorageProviderFactory>((method, _) =>
                method.ReturnType == typeof(Task<IStorageProvider>) ? Task.FromResult(provider) : provider);
        }
        public void Select(Uri uri) => responses.Enqueue(() =>
            [ContractProxy.Make<IStorageFolder>((method, _) => method.Name switch
            {
                "get_Path" => uri,
                "get_Name" => "synthetic folder",
                "get_CanBookmark" => false,
                "Dispose" => null,
                _ => throw new Xunit.Sdk.XunitException($"Selection attempted storage mutation: {method.Name}"),
            })]);
        public void Cancel() => responses.Enqueue(() => []);
        public void Fail(Exception error) => responses.Enqueue(() => throw error);
    }

    public class ContractProxy : DispatchProxy
    {
        private Func<MethodInfo, object?[]?, object?> callback = null!;
        public static T Make<T>(Func<MethodInfo, object?[]?, object?> callback) where T : class
        {
            T proxy = Create<T, ContractProxy>();
            ((ContractProxy)(object)proxy).callback = callback;
            return proxy;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            callback(targetMethod!, args);
    }

    private sealed class TestFiles : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "FR002-AC055-" + Guid.NewGuid().ToString("N"));
        public string Settings => Path.Combine(Root, "settings", "setting.txt");
        public string InputA => Path.Combine(Root, "input-a", "input.xlsx");
        public string InputB => Path.Combine(Root, "input-b", "input.xlsx");
        public TestFiles()
        {
            Create(InputA);
            Create(InputB);
        }
        public string[] Inventory() => Directory.GetFileSystemEntries(Root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToArray();
        private static void Create(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
            WorkbookPart workbook = document.AddWorkbookPart();
            workbook.Workbook = new Workbook();
            WorksheetPart sheet = workbook.AddNewPart<WorksheetPart>();
            SheetData data = new(
                new Row(new Cell { CellReference = "A1", DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text("Answer")) }) { RowIndex = 1 },
                new Row(new Cell { CellReference = "A2", DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text("Synthetic answer")) }) { RowIndex = 2 });
            sheet.Worksheet = new Worksheet(new SheetDimension { Reference = "A1:A2" }, data);
            workbook.Workbook.AppendChild(new Sheets(new Sheet
                { Id = workbook.GetIdOfPart(sheet), SheetId = 1, Name = "Original" }));
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}

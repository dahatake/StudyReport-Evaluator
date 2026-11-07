using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using GitHub.Copilot;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using Xunit;

namespace StudyReportEvaluator.SystemTests.Ui;

/// <summary>
/// FR-121 AC-030 AC-033. Oracle: approved ACs and §3.2/11.10 only.
/// Real production resolver/factory/service/UI; fake runtime and owned processes.
/// This deliberately does NOT attest real Start/Ping, authentication or AI.
/// </summary>
public sealed class BundledRuntimeSystemTests
{
    public static bool Fr121Harness =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FR121_BUILD_PROFILE"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Tripwire => Environment.GetEnvironmentVariable("FR121_TRIPWIRE_CLI")
        ?? throw new InvalidOperationException("Run through the FR-121 pytest harness");

    // Reflection only locates existing composition seams; no production result
    // or implementation branch is used to calculate an expected result.
    private static ICopilotCliPathResolver LoginResolver(BundledCopilotLoginService service) =>
        (ICopilotCliPathResolver)(service.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(f => typeof(ICopilotCliPathResolver).IsAssignableFrom(f.FieldType))
            .GetValue(service) ?? throw new InvalidOperationException("Missing login resolver"));

    [Fact(Skip = "FR-121 requires the isolated pytest runtime harness", SkipUnless = nameof(Fr121Harness))]
    public async Task Default_composition_uses_distribution_bundle_but_preserves_source_PATH()
    {
        using Bundle bundle = new();
        string? before = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", Path.GetDirectoryName(Tripwire));
        try
        {
            bool distribution = Environment.GetEnvironmentVariable("FR121_BUILD_PROFILE") == "distribution";
            using BundledCopilotLoginService login = new();
            ICopilotCliPathResolver resolver = LoginResolver(login);
            string expected = distribution
                ? Path.Combine(AppContext.BaseDirectory, "runtimes", Rid, "native", "copilot.exe")
                : Tripwire;
            Assert.Equal(Path.GetFullPath(expected), await resolver.ResolveAsync(Token));
            CopilotClientCreationResult created = await new CopilotClientFactory().CreateAsync(Token);
            Assert.Equal(CopilotClientCreationStatus.Created, created.Status);
            Assert.Equal(Path.GetFullPath(expected), created.Identity?.CliPath);
            StdioRuntimeConnection connection = Assert.IsType<StdioRuntimeConnection>(created.Options?.Connection);
            Assert.Equal(Path.GetFullPath(expected), connection.Path);
            await created.Client!.DisposeAsync();
            // AC-033: observe existing default SDK composition for every AI
            // operation without sending prompts or executing the fake CLI.
            foreach (object consumer in new object[] {
                new CopilotAuthenticationService(), new EphemeralEvaluationRunner(),
                new ReferenceAnswerEvaluationRunner(), new SpecialEvaluationRunner() })
            {
                List<CopilotClientFactory> factories = FindClientFactories(consumer).ToList();
                Assert.NotEmpty(factories);
                foreach (CopilotClientFactory factory in factories)
                {
                    CopilotClientCreationResult result = await factory.CreateAsync(Token);
                    Assert.Equal(CopilotClientCreationStatus.Created, result.Status);
                    Assert.Equal(Path.GetFullPath(expected), result.Identity?.CliPath);
                    Assert.Equal(created.Identity?.CliSha256, result.Identity?.CliSha256);
                    await result.Client!.DisposeAsync();
                }
            }

            if (distribution)
            {
                // Removing a distribution manifest must not turn it into a dev build.
                string manifest = Path.Combine(AppContext.BaseDirectory, "copilot-runtime.json");
                byte[] original = await File.ReadAllBytesAsync(manifest, Token);
                try
                {
                    File.Delete(manifest);
                    CopilotClientCreationResult missing = await new CopilotClientFactory().CreateAsync(Token);
                    Assert.NotEqual(CopilotClientCreationStatus.Created, missing.Status);
                    Assert.Null(missing.Client);
                    Assert.Null(missing.Identity);
                    int launches = 0;
                    using BundledCopilotLoginService missingLogin = new(resolver, _ =>
                    {
                        launches++;
                        return new OwnedProcess { Complete = true };
                    });
                    Assert.NotEqual(CopilotLoginStatus.Completed, (await missingLogin.LoginAsync(Token)).Status);
                    Assert.Equal(0, launches);
                }
                finally { await File.WriteAllBytesAsync(manifest, original, Token); }
            }
        }
        finally { Environment.SetEnvironmentVariable("PATH", before); }
    }

    // FR-121 AC-030 AC-033: use both SDK and login boundaries for each corruption.
    [Fact(Skip = "FR-121 requires the isolated pytest runtime harness", SkipUnless = nameof(Fr121Harness))]
    public async Task Integrity_and_login_reject_corruption_without_PATH_or_process_fallback()
    {
        using Bundle bundle = new();
        BundledCopilotCliPathResolver resolver = new(bundle.Root);
        CopilotClientCreationResult valid = await new CopilotClientFactory(resolver).CreateAsync(Token);
        Assert.Equal(CopilotClientCreationStatus.Created, valid.Status);
        Assert.Equal(bundle.Cli, valid.Identity?.CliPath);
        Assert.Equal(bundle.Hash, valid.Identity?.CliSha256, ignoreCase: true);
        Assert.True(Path.IsPathFullyQualified(valid.Identity!.CliPath));
        await valid.Client!.DisposeAsync();
        OwnedProcess owned = new() { Complete = true };
        List<ProcessStartInfo> starts = [];
        using BundledCopilotLoginService login = new(resolver, info => { starts.Add(info); return owned; });
        Assert.Equal(CopilotLoginStatus.Completed, (await login.LoginAsync(Token)).Status);
        ProcessStartInfo start = Assert.Single(starts);
        Assert.Equal(bundle.Cli, start.FileName);
        Assert.False(start.UseShellExecute);
        Assert.False(start.RedirectStandardInput);
        Assert.False(start.RedirectStandardOutput);
        Assert.False(start.RedirectStandardError);
        Assert.Contains("login", start.ArgumentList);
        Assert.Contains("--no-auto-update", start.ArgumentList);
        Assert.Equal(bundle.Hash, Hash(bundle.Cli));

        foreach (string mutation in new[] {
            "manifest-missing", "manifest-corrupt", "cli-missing", "cli-corrupt",
            "rid", "sdk", "cli-version", "relative-path", "absolute-path", "escape-path", "hash" })
        {
            bundle.Restore();
            switch (mutation)
            {
                case "manifest-missing": File.Delete(bundle.Manifest); break;
                case "manifest-corrupt": File.WriteAllText(bundle.Manifest, "{"); break;
                case "cli-missing": File.Delete(bundle.Cli); break;
                case "cli-corrupt": File.AppendAllText(bundle.Cli, "different bytes"); break;
                case "rid": bundle.Change("runtimeIdentifier", "unsupported-rid"); break;
                case "sdk": bundle.Change("sdkVersion", "0.0.0"); break;
                case "cli-version": bundle.Change("cliVersion", "0.0.0"); break;
                case "relative-path": bundle.Change("cliRelativePath", "copilot.exe"); break;
                case "absolute-path": bundle.Change("cliRelativePath", bundle.Cli); break;
                case "escape-path": bundle.Change("cliRelativePath", $"runtimes/{Rid}/native/../../../copilot.exe"); break;
                case "hash": bundle.Change("cliSha256", new string('0', 64)); break;
            }
            CopilotClientCreationResult refused = await new CopilotClientFactory(resolver).CreateAsync(Token);
            Assert.NotEqual(CopilotClientCreationStatus.Created, refused.Status);
            Assert.Null(refused.Client);
            Assert.Null(refused.Identity);
            int previous = starts.Count;
            CopilotLoginResult denied = await login.LoginAsync(Token);
            Assert.NotEqual(CopilotLoginStatus.Completed, denied.Status);
            Assert.Equal(previous, starts.Count);
            Assert.DoesNotContain(bundle.Root, refused.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(bundle.Root, denied.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        bundle.Restore();
        // Login validates again, including after an earlier successful login.
        Assert.Equal(bundle.Cli, await resolver.ResolveAsync(Token));
        Assert.Equal(bundle.Hash, Hash(bundle.Cli));
        OwnedProcess pending = new();
        OwnedProcess unrelated = new();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        using BundledCopilotLoginService cancellable = new(resolver, _ => pending);
        Task<CopilotLoginResult> first = cancellable.LoginAsync(cancellation.Token);
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.Equal(CopilotLoginStatus.AlreadyRunning, (await cancellable.LoginAsync(Token)).Status);
        cancellation.Cancel();
        Assert.Equal(CopilotLoginStatus.Cancelled, (await first.WaitAsync(TimeSpan.FromSeconds(5), Token)).Status);
        Assert.Equal(1, pending.Kills);
        Assert.Equal(0, unrelated.Kills);
        Assert.Equal(bundle.Hash, Hash(bundle.Cli));
        OwnedProcess closingProcess = new();
        using BundledCopilotLoginService closing = new(resolver, _ => closingProcess);
        Task<CopilotLoginResult> closingTask = closing.LoginAsync(Token);
        await closingProcess.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        closing.Dispose();
        CopilotLoginResult afterClose = await closingTask.WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.NotEqual(CopilotLoginStatus.Completed, afterClose.Status);
        Assert.Equal(1, closingProcess.Kills);
        Assert.Equal(0, unrelated.Kills);
    }

    // FR-121 AC-033 AC-030: real auth mapping -> execution boundary -> startup UI.
    [AvaloniaFact(Skip = "FR-121 requires the isolated pytest runtime harness", SkipUnless = nameof(Fr121Harness))]
    public async Task Authentication_and_GUI_keep_runtime_errors_out_of_AuthRequired_and_never_start_AI()
    {
        using Bundle bundle = new();
        CopilotClientCreationResult created = await new CopilotClientFactory(
            new BundledCopilotCliPathResolver(bundle.Root)).CreateAsync(Token);
        Assert.Equal(CopilotClientCreationStatus.Created, created.Status);
        CopilotRuntimeIdentity identity = created.Identity!;
        await created.Client!.DisposeAsync();
        foreach (string scenario in new[] {
            "available", "unauthenticated", "suppressed", "start-failed", "ping-failed",
            "timeout", "incompatible", "invalid-auth", "cancelled" })
        {
            using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
            Runtime runtime = new(scenario) { Cancel = cancellation.Cancel };
            RuntimeFactory factory = new(runtime, identity);
            CopilotAuthenticationService service = new(factory, TimeSpan.FromMilliseconds(100));
            OwnedProcess process = new() { Complete = true };
            int loginCount = 0;
            using BundledCopilotLoginService login = new(new BundledCopilotCliPathResolver(bundle.Root),
                info => { Assert.Equal(identity.CliPath, info.FileName); loginCount++; return process; });
            NeverRun runner = new();
            using ExecutionViewModel execution = new(new CopilotExecutionAuthenticationBoundary(service), runner, login);
            InputViewModel input = new();
            using MainWindowViewModel shell = new(new WorkflowNavigator(), input,
                new QuantificationDesignViewModel(), execution, new ResultsOutputViewModel());
            MainWindow window = new(shell);
            try
            {
                // No Opened event here: exercise the one-shot startup operation
                // explicitly, so the real GUI cannot start ambient authentication.
                await execution.RunStartupAuthenticationAsync(scenario != "suppressed", cancellation.Token)
                    .WaitAsync(TimeSpan.FromSeconds(5), Token);
                ExecutionAuthenticationState expected = scenario switch
                {
                    "available" => ExecutionAuthenticationState.Available,
                    "unauthenticated" or "suppressed" => ExecutionAuthenticationState.AuthRequired,
                    "cancelled" => ExecutionAuthenticationState.Cancelled,
                    _ => ExecutionAuthenticationState.RuntimeFailed,
                };
                Assert.Equal(expected, execution.AuthenticationState);
                Assert.Equal(scenario == "unauthenticated" ? 1 : 0, loginCount);
                if (scenario == "unauthenticated")
                {
                    // Process exit alone did not authenticate: the successful
                    // completion rechecked the same runtime and remained unauthenticated.
                    Assert.True(factory.Calls >= 2);
                    runtime.Scenario = "available";
                    await execution.CheckAuthenticationAsync(Token);
                    Assert.Equal(ExecutionAuthenticationState.Available, execution.AuthenticationState);
                    Assert.Contains("synthetic-model", execution.AvailableModelIds);
                    Assert.Equal(1, loginCount);
                }
                if (scenario == "suppressed")
                {
                    Assert.True(execution.LoginCommand.CanExecute(null));
                    execution.LoginCommand.Execute(null);
                    await execution.LastLoginTask!.WaitAsync(TimeSpan.FromSeconds(5), Token);
                    Assert.Equal(1, loginCount);
                    Assert.Equal(ExecutionAuthenticationState.AuthRequired, execution.AuthenticationState);
                }
                int checks = factory.Calls;
                await execution.RunStartupAuthenticationAsync(true, Token);
                Assert.Equal(checks, factory.Calls);
                Assert.Equal(scenario is "unauthenticated" or "suppressed" ? 1 : 0, loginCount);
                Assert.Equal(0, runner.Calls);
                Assert.False(execution.IsRunning);
                Assert.True(execution.CanCheckAuthentication);
                Assert.DoesNotContain(bundle.Root, execution.AuthenticationStatusText, StringComparison.OrdinalIgnoreCase);
                if (scenario is "start-failed" or "incompatible")
                    Assert.DoesNotContain("auth", runtime.Operations);
                if (scenario == "ping-failed")
                    Assert.DoesNotContain("auth", runtime.Operations);
                if (expected == ExecutionAuthenticationState.AuthRequired)
                {
                    Assert.Contains("start", runtime.Operations);
                    Assert.Contains("ping", runtime.Operations);
                    Assert.Contains("auth", runtime.Operations);
                }
                // Offline navigation/design is independent of CLI/auth status.
                shell.Navigator.NavigateTo(WorkflowStep.Design);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(WorkflowStep.Design, shell.CurrentStep);
                Assert.True(execution.LoginCommand.CanExecute(null));
                if (scenario == "cancelled")
                {
                    // A single real headless window, real Excel loader and
                    // visible design view after runtime failure/cancellation.
                    string workbook = Path.Combine(bundle.Root, "offline.xlsx");
                    WriteWorkbook(workbook);
                    await input.SetFilePathAsync(workbook, Token);
                    Assert.True(input.HasLoadedWorkbook);
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    Assert.True(window.IsVisible);
                    Assert.Contains(window.GetVisualDescendants(), v => v is QuantificationDesignView);
                    Assert.Equal(0, runner.Calls);
                }
            }
            finally { window.Close(); }
        }
    }

    private static string Rid => "win-" + (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void WriteWorkbook(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbook = document.AddWorkbookPart();
        workbook.Workbook = new Workbook();
        WorksheetPart worksheet = workbook.AddNewPart<WorksheetPart>();
        static Cell TextCell(string address, string value) =>
            new() { CellReference = address, DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value)) };
        worksheet.Worksheet = new Worksheet(new SheetData(
            new Row(TextCell("A1", "Question")) { RowIndex = 1 },
            new Row(TextCell("A2", "Synthetic answer")) { RowIndex = 2 }));
        workbook.Workbook.AppendChild(new Sheets()).Append(new Sheet
        { Id = workbook.GetIdOfPart(worksheet), SheetId = 1, Name = "Responses" });
        worksheet.Worksheet.Save();
        workbook.Workbook.Save();
    }

    private static IEnumerable<CopilotClientFactory> FindClientFactories(object root)
    {
        Queue<(object Value, int Depth)> queue = new();
        HashSet<object> seen = new(ReferenceEqualityComparer.Instance);
        queue.Enqueue((root, 0));
        while (queue.TryDequeue(out var item))
        {
            if (!seen.Add(item.Value)) continue;
            if (item.Value is CopilotClientFactory factory) { yield return factory; continue; }
            if (item.Depth >= 6 || item.Value.GetType().Assembly != typeof(CopilotClientFactory).Assembly) continue;
            foreach (FieldInfo field in item.Value.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                if (field.GetValue(item.Value) is object child) queue.Enqueue((child, item.Depth + 1));
        }
    }

    private sealed class Bundle : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "FR121-" + Guid.NewGuid().ToString("N"));
        public string Cli => Path.Combine(Root, "runtimes", Rid, "native", "copilot.exe");
        public string Manifest => Path.Combine(Root, "copilot-runtime.json");
        public string Hash => BundledRuntimeSystemTests.Hash(Tripwire);
        private Dictionary<string, object> Fields => new()
        {
            ["schemaVersion"] = 1, ["runtimeIdentifier"] = Rid, ["cliVersion"] = "1.0.79",
            ["sdkVersion"] = typeof(CopilotClient).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0],
            ["cliSha256"] = Hash, ["cliRelativePath"] = $"runtimes/{Rid}/native/copilot.exe",
        };
        public Bundle() => Restore();
        public void Restore()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Cli)!);
            File.Copy(Tripwire, Cli, overwrite: true);
            // Wrong relative/traversal targets actually exist and match the
            // declared hash: refusal must be path validation, not missing bytes.
            File.Copy(Tripwire, Path.Combine(Root, "copilot.exe"), overwrite: true);
            File.WriteAllText(Manifest, JsonSerializer.Serialize(Fields));
        }
        public void Change(string key, string value)
        {
            Dictionary<string, object> fields = Fields;
            fields[key] = value;
            File.WriteAllText(Manifest, JsonSerializer.Serialize(fields));
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class OwnedProcess : ICopilotLoginProcess
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Kills { get; private set; }
        public bool Complete { get; set; }
        public bool HasExited => Complete;
        public int ExitCode => 0;
        public bool Start() { Started.TrySetResult(); return true; }
        public Task WaitForExitAsync(CancellationToken token) =>
            Complete ? Task.CompletedTask : Task.Delay(Timeout.Infinite, token);
        public void Kill(bool entireProcessTree) { Kills++; Complete = true; }
        public bool WaitForExit(int milliseconds) => Complete;
        public void Dispose() { }
    }

    private sealed class Runtime(string scenario) : ICopilotAuthenticationRuntime
    {
        public string Scenario { get; set; } = scenario;
        public Action? Cancel { get; init; }
        public List<string> Operations { get; } = [];
        public CopilotRuntimeState State { get; private set; } = CopilotRuntimeState.Created;
        public Task StartAsync(CancellationToken token)
        {
            Operations.Add("start");
            if (Scenario is "start-failed" or "incompatible") throw new InvalidOperationException("synthetic runtime");
            if (Scenario == "cancelled") { Cancel!(); token.ThrowIfCancellationRequested(); }
            State = CopilotRuntimeState.Ready;
            return Task.CompletedTask;
        }
        public Task PingAsync(CancellationToken token)
        {
            Operations.Add("ping");
            if (Scenario == "ping-failed") throw new InvalidOperationException("synthetic ping");
            return Scenario == "timeout" ? Task.Delay(Timeout.Infinite, token) : Task.CompletedTask;
        }
        public Task<CopilotRuntimeAuthenticationState> GetAuthenticationStatusAsync(CancellationToken token)
        {
            Operations.Add("auth");
            return Task.FromResult(Scenario switch {
                "available" => CopilotRuntimeAuthenticationState.Authenticated,
                "invalid-auth" => (CopilotRuntimeAuthenticationState)999,
                _ => CopilotRuntimeAuthenticationState.Unauthenticated,
            });
        }
        public Task<IReadOnlyList<CopilotModelAvailability>> ListModelsAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<CopilotModelAvailability>>([new("synthetic-model", 100, 200)]);
        public Task StopAsync(CancellationToken token) { State = CopilotRuntimeState.Stopped; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { State = CopilotRuntimeState.Disposed; return ValueTask.CompletedTask; }
    }
    private sealed class RuntimeFactory(Runtime runtime, CopilotRuntimeIdentity identity) : ICopilotAuthenticationRuntimeFactory
    {
        public int Calls { get; private set; }
        public ValueTask<CopilotAuthenticationRuntimeCreation> CreateAsync(CancellationToken token)
        {
            Calls++;
            return ValueTask.FromResult(CopilotAuthenticationRuntimeCreation.Created(runtime, identity));
        }
    }
    private sealed class NeverRun : IQuantificationRunBoundary
    {
        public int Calls { get; private set; }
        public Task<StudyReportEvaluator.App.Workflow.RunSummary> RunAsync(
            StudyReportEvaluator.App.Workflow.QuantificationRunRequest request,
            Action<            StudyReportEvaluator.App.Workflow.EvaluationProgress>? progress,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("FR-121 AC-033: startup/login must never start AI");
        }
    }
}

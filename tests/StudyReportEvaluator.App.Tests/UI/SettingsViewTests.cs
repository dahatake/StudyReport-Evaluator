using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.Launch;
using StudyReportEvaluator.App.Settings;
using StudyReportEvaluator.App.Tests.Workflow;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using StudyReportEvaluator.App.Workbooks.Mapping;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Serialization;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class SettingsViewTests
{
    private const string PrivateCanary = "PRIVATE-T17-CONTENT";
    private static readonly TimeSpan TestWait = TimeSpan.FromSeconds(10);
    private static readonly CanonicalDefinitionSerializer Canonical = new();

    [AvaloniaFact]
    public void Parameterless_view_inherits_Settings_owner_and_null_store_never_loads_or_saves()
    {
        using SettingsHarness harness = new(withStore: false);
        SettingsView view = new();
        Assert.Null(view.DataContext);
        Assert.Throws<InvalidOperationException>(() => view.ViewModel);
        Assert.Throws<ArgumentNullException>(() => new SettingsView(null!));
        harness.Window.Content = view;
        harness.Window.DataContext = harness.Settings;
        harness.Window.Show();
        Render();

        Assert.Same(harness.Settings, view.ViewModel);
        Assert.Same(harness.Settings, view.DataContext);
        Assert.Equal("SettingsView", AutomationProperties.GetAutomationId(view));
        Assert.IsType<TabControl>(ActiveContent(view));
        Assert.False(ById<Button>(view, "SettingsSave").IsEffectivelyEnabled);
        Assert.False(ById<Button>(view, "SettingsLoadRetry").IsEffectivelyEnabled);
        Assert.Contains("保存先が構成されていません", ById<TextBlock>(view, "SettingsStatus").Text!);
        Assert.Null(harness.Settings.LastLoadTask);
        Assert.Null(harness.Settings.LastSaveTask);
        Assert.False(Directory.Exists(harness.Root));
        AssertPassive(harness);

        view.DataContext = null;
        Render();
        Assert.Null(Required<ContentControl>(view, "CurrentSettingsContent").Content);
        Assert.False(ById<Button>(view, "SettingsRequestClose").IsEffectivelyEnabled);
        Assert.True(ById<TextBlock>(view, "SettingsDefinitionAvailability").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Settings_has_only_the_common_category_and_every_enum_value_reuses_the_same_common_editor_and_owner()
    {
        // FR-002 (AC-055): 設定のカテゴリは「共通」だけ。旧カテゴリの値が来ても共通を表示する。
        using SettingsHarness harness = new(withStore: false);
        await harness.LoadInputAsync();
        harness.Show();
        QuantificationDefinition before = harness.Design.Draft;

        Button current = ById<Button>(harness.View, "SettingsCategoryCommon");
        Assert.Same(current, Assert.Single(Required<Grid>(harness.View, "SettingsCategoryBar").Children.OfType<Button>()));
        foreach (string removed in new[] { "Mapping", "Evaluation", "Special", "ImportedPrompts" })
        {
            Assert.DoesNotContain(AllControls(harness.View), control =>
                AutomationProperties.GetAutomationId(control) == "SettingsCategory" + removed);
        }

        SelectCategory(harness, SettingsCategory.Common);
        Control first = ActiveContent(harness.View);
        Assert.Same(harness.Settings, first.DataContext);
        Assert.Same(harness.Settings.SelectCategoryCommand, current.Command);
        Assert.Equal(SettingsCategory.Common, Assert.IsType<SettingsCategory>(current.CommandParameter));
        Assert.Contains("selected", current.Classes);
        Assert.Contains("選択中", AutomationProperties.GetHelpText(current)!);

        foreach (SettingsCategory category in Enum.GetValues<SettingsCategory>().Reverse())
        {
            harness.Settings.SelectedCategory = category;
            Render();
            Assert.Same(first, ActiveContent(harness.View));
            AssertOnlyActiveEditor(harness.View);
            AssertUniqueIds(harness.View);
        }

        AssertCanonical(before, harness.Design.Draft);
        AssertCanonical(before, harness.Input.DefinitionDraft);
        Assert.Null(harness.Settings.LastLoadTask);
        Assert.Null(harness.Settings.LastSaveTask);
        Assert.False(Directory.Exists(harness.Root));
        AssertPassive(harness);
    }

    [AvaloniaFact]
    public async Task Repeated_common_edits_reach_the_latest_draft_before_explicit_save()
    {
        using SettingsHarness harness = new();
        await harness.LoadInputAsync();
        await harness.Settings.InitializeAsync(TestContext.Current.CancellationToken);
        harness.Show();
        Edit(harness.View, "DesignDefinitionName", "共通で編集した定義名");
        Edit(harness.View, "DesignRevision", "revision-T17");
        Edit(harness.View, "DesignRoundingDigits", "3");
        ById<ComboBox>(harness.View, "ExecutionConcurrency").SetCurrentValue(ComboBox.SelectedItemProperty, 3);
        Edit(harness.View, "ExecutionOutputDirectory", harness.OutputPath);

        // FR-001/FR-002: 設問・通常評価・固有評価・読込Prompt の編集は設定にない（採点設計画面で行う）。
        // 設定を共通のまま繰り返し開き直しても、選択と最新の下書きは変わらない。
        QuestionDesignItemViewModel question = harness.Design.SelectedQuestion!;
        EvaluatorDesignItemViewModel evaluator = question.SelectedEvaluator!;
        CriterionDesignItemViewModel criterion = evaluator.SelectedCriterion!;
        SpecialEvaluationDesignItemViewModel special = question.SelectedSpecialEvaluation!;
        foreach (SettingsCategory category in Enum.GetValues<SettingsCategory>())
        {
            harness.Settings.SelectedCategory = category;
            Render();
            AssertOnlyActiveEditor(harness.View);
        }

        SelectCategory(harness, SettingsCategory.Common);
        Assert.Equal("共通で編集した定義名", ById<TextBox>(harness.View, "DesignDefinitionName").Text);
        Assert.Equal("revision-T17", ById<TextBox>(harness.View, "DesignRevision").Text);
        Assert.Equal("3", ById<TextBox>(harness.View, "DesignRoundingDigits").Text);
        Assert.Same(question, harness.Design.SelectedQuestion);
        Assert.Same(evaluator, question.SelectedEvaluator);
        Assert.Same(criterion, evaluator.SelectedCriterion);
        Assert.Same(special, question.SelectedSpecialEvaluation);
        Assert.True(harness.Settings.HasUnsavedChanges);
        Assert.False(File.Exists(harness.SettingsPath));

        QuantificationDefinition latest = harness.Design.Draft;
        await Save(harness);
        ApplicationSettings saved = await harness.ReadSettingsAsync();
        AssertCanonical(latest, saved.Definition!);
        AssertCanonical(latest, harness.Input.DefinitionDraft);
        Assert.Equal(3, saved.MaxConcurrency);
        Assert.Equal(harness.OutputPath, saved.OutputDirectoryOverride);
        Assert.Equal(SettingsSaveStatus.Saved, harness.Settings.SaveStatus);
        Assert.False(harness.Settings.HasUnsavedChanges);
        Assert.Contains("保存済み", ById<TextBlock>(harness.View, "SettingsStatus").Text!);
        Assert.False(Directory.Exists(harness.OutputPath));
        AssertPassive(harness);
    }

    [AvaloniaFact]
    public async Task Cached_editors_retain_uncommitted_numeric_text_and_bindings_across_category_and_view_detach()
    {
        using SettingsHarness harness = new(withStore: false);
        await harness.LoadInputAsync();
        harness.Show();
        TextBox rounding = ById<TextBox>(harness.View, "DesignRoundingDigits");
        BindingExpressionBase? roundingBinding = BindingOperations.GetBindingExpressionBase(rounding, TextBox.TextProperty);
        int originalRounding = harness.Design.RoundingDigits;
        Edit(harness.View, "DesignRoundingDigits", "未確定");
        Assert.Equal(originalRounding, harness.Design.RoundingDigits);
        SelectCategory(harness, SettingsCategory.Common);
        ShowCommonTab(harness, 1);
        ShowCommonTab(harness, 0);
        Assert.Same(rounding, ById<TextBox>(harness.View, "DesignRoundingDigits"));
        Assert.Equal("未確定", rounding.Text);
        Assert.Same(roundingBinding, BindingOperations.GetBindingExpressionBase(rounding, TextBox.TextProperty));

        harness.Window.Content = null;
        Render();
        harness.Window.Content = harness.View;
        Render();
        Assert.Equal("未確定", rounding.Text);
        Assert.NotNull(roundingBinding);
        Assert.Same(roundingBinding, BindingOperations.GetBindingExpressionBase(rounding, TextBox.TextProperty));
        Assert.Equal(originalRounding, harness.Design.RoundingDigits);
        Assert.Null(harness.Settings.LastSaveTask);
        AssertPassive(harness);
    }

    [AvaloniaFact]
    public async Task Common_binds_effective_model_but_persists_nullable_override_and_excludes_read_only_runtime()
    {
        using SettingsHarness harness = new();
        await harness.SeedSettingsAsync(new ApplicationSettings { PreferredModelId = "not-available" });
        await harness.LoadInputAsync();
        await harness.Settings.InitializeAsync(TestContext.Current.CancellationToken);
        harness.Show();
        ComboBox model = ById<ComboBox>(harness.View, "ExecutionModel");
        Assert.Same(harness.Execution.AvailableModelIds, model.ItemsSource);
        Assert.Null(model.SelectedItem);
        Assert.Equal("not-available", harness.Execution.PreferredModelId);
        AssertPassive(harness);

        // Explicit fake confirmation is test setup, never a View-triggered operation.
        await harness.Execution.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
        Render();
        Assert.Null(model.SelectedItem);
        Assert.Equal("not-available", harness.Execution.PreferredModelId);
        ApplicationSettings automaticallySaved = await harness.ReadSettingsAsync();
        Assert.Equal("not-available", automaticallySaved.PreferredModelId);
        Assert.Equal(8, automaticallySaved.MaxConcurrency);
        Assert.Null(automaticallySaved.OutputDirectoryOverride);
        Assert.Null(automaticallySaved.Definition); // Loaded input draft is not automatically persisted.
        CachedCopilotModel[] expectedModels =
        [
            new("model-test", 64_000, 128_000),
            new("model-other", 64_000, 128_000),
            new("auto", null, null),
        ];
        Assert.NotNull(automaticallySaved.CachedModels);
        Assert.Equal(expectedModels, automaticallySaved.CachedModels.Value.ToArray());
        model.SetCurrentValue(ComboBox.SelectedItemProperty, "model-other");
        Render();
        Assert.Equal("model-other", harness.Execution.SelectedModelId);
        Assert.Equal("model-other", harness.Execution.PreferredModelId);
        Assert.Null(harness.Execution.OutputDirectoryOverride);
        TextBox output = ById<TextBox>(harness.View, "ExecutionOutputDirectory");
        TextBox effective = ById<TextBox>(harness.View, "SettingsEffectiveOutputDirectory");
        Assert.True(string.IsNullOrEmpty(output.Text));
        Assert.True(effective.IsReadOnly);
        Assert.Equal(Path.Combine(harness.Root, "result"), effective.Text);
        Edit(harness.View, "ExecutionOutputDirectory", harness.OutputPath);
        Assert.Equal(harness.OutputPath, effective.Text);
        Edit(harness.View, "ExecutionOutputDirectory", string.Empty);
        Assert.Null(harness.Execution.OutputDirectoryOverride);
        Assert.Equal(Path.Combine(harness.Root, "result"), effective.Text);

        ShowCommonTab(harness, 2);
        TextBox runtime = ById<TextBox>(harness.View, "SettingsRuntimeIdentity");
        TextBox authentication = ById<TextBox>(harness.View, "SettingsAuthenticationStatus");
        Assert.True(runtime.IsReadOnly);
        Assert.True(authentication.IsReadOnly);
        Assert.Equal(harness.Execution.RuntimeIdentityText, runtime.Text);
        Assert.Equal(harness.Execution.AuthenticationStatusText, authentication.Text);
        Assert.Equal("model-other", ById<TextBox>(harness.View, "SettingsPreferredModelId").Text);
        string? before = runtime.Text;
        Assert.True(runtime.Focus(NavigationMethod.Tab));
        harness.Window.KeyTextInput("診断は編集しない");
        Render();
        Assert.Equal(before, runtime.Text);
        await Save(harness);

        ApplicationSettings saved = await harness.ReadSettingsAsync();
        Assert.Equal("model-other", saved.PreferredModelId);
        Assert.Null(saved.OutputDirectoryOverride);
        Assert.NotNull(saved.CachedModels);
        Assert.Equal(expectedModels, saved.CachedModels.Value.ToArray());
        using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(harness.SettingsPath));
        Assert.Equal(new[] { "cachedModels", "definition", "maxConcurrency", "outputDirectoryOverride", "preferredModelId", "schemaVersion" },
            json.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        JsonElement[] cachedModels = json.RootElement.GetProperty("cachedModels").EnumerateArray().ToArray();
        Assert.Equal(expectedModels.Length, cachedModels.Length);
        for (int index = 0; index < expectedModels.Length; index++)
        {
            JsonElement cachedModel = cachedModels[index];
            // Closed metadata fields also exclude credential/account/authentication/runtime data.
            Assert.Equal(new[] { "id", "maximumContextWindowTokens", "maximumPromptTokens" },
                cachedModel.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
            Assert.Equal(expectedModels[index].Id, cachedModel.GetProperty("id").GetString());
            foreach ((string name, int? expected) in new[]
                     {
                         ("maximumPromptTokens", expectedModels[index].MaximumPromptTokens),
                         ("maximumContextWindowTokens", expectedModels[index].MaximumContextWindowTokens),
                     })
            {
                if (expected is { } limit)
                {
                    Assert.Equal(limit, cachedModel.GetProperty(name).GetInt32());
                }
                else
                {
                    Assert.Equal(JsonValueKind.Null, cachedModel.GetProperty(name).ValueKind);
                }
            }
        }

        Assert.False(string.IsNullOrWhiteSpace(runtime.Text));
        Assert.False(string.IsNullOrWhiteSpace(authentication.Text));
        Assert.DoesNotContain(runtime.Text!, json.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(authentication.Text!, json.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(harness.Root, "result")));
        Assert.False(Directory.Exists(harness.OutputPath));
        AssertPassive(harness, authenticationChecks: 1);
    }

    [AvaloniaFact]
    public async Task Missing_file_waits_for_explicit_read_and_only_Save_button_creates_it_not_navigation_or_Enter()
    {
        using SettingsHarness harness = new();
        harness.Show();
        Button save = ById<Button>(harness.View, "SettingsSave");
        Assert.Same(harness.Settings.SaveCommand, save.Command);
        Assert.False(save.IsDefault);
        Assert.False(save.IsEffectivelyEnabled);
        Assert.Single(save.GetVisualDescendants().OfType<PathIcon>());
        Assert.NotNull(save.GetVisualDescendants().OfType<PathIcon>().Single().Data);
        Assert.Null(harness.Settings.LastLoadTask);
        Assert.False(Directory.Exists(harness.Root));

        await Reload(harness);
        Assert.Equal(SettingsLoadStatus.Missing, harness.Settings.LoadStatus);
        Assert.True(save.IsEffectivelyEnabled);
        Assert.Contains("まだありません", ById<TextBlock>(harness.View, "SettingsStatus").Text!);
        Assert.False(Directory.Exists(harness.Root));
        Edit(harness.View, "ExecutionOutputDirectory", harness.OutputPath);
        Assert.True(ById<TextBox>(harness.View, "ExecutionOutputDirectory").Focus(NavigationMethod.Tab));
        Press(harness.Window, Key.Enter);
        Press(harness.Window, Key.Escape);
        foreach (SettingsCategory category in new[] { SettingsCategory.Common })
        {
            SelectCategory(harness, category);
            Assert.True(save.IsEffectivelyVisible);
            Assert.True(save.IsEffectivelyEnabled);
            Assert.Contains(save.GetVisualAncestors(), ancestor => ReferenceEquals(ancestor, Required<Grid>(harness.View, "SettingsFooter")));
            Assert.DoesNotContain(save.GetVisualAncestors(), ancestor => ancestor is ScrollViewer);
            Assert.Null(harness.Settings.LastSaveTask);
        }

        int closed = 0;
        harness.Settings.CloseRequested += (_, _) => closed++;
        Activate(ById<Button>(harness.View, "SettingsRequestClose"));
        Assert.Equal(1, closed);
        Assert.False(File.Exists(harness.SettingsPath));
        await Save(harness);
        Assert.True(File.Exists(harness.SettingsPath));
        Assert.Null((await harness.ReadSettingsAsync()).Definition);
        Assert.False(Directory.Exists(harness.OutputPath));
        AssertPassive(harness);
    }

    [AvaloniaFact]
    public async Task No_input_keeps_stored_definition_and_common_settings_but_disables_definition_editing()
    {
        using SettingsHarness harness = new();
        QuantificationDefinition stored = CreateDefinition() with { Name = "消してはいけない保存定義", Revision = "stored" };
        await harness.SeedSettingsAsync(new ApplicationSettings { Definition = stored });
        await harness.Settings.InitializeAsync(TestContext.Current.CancellationToken);
        byte[] original = File.ReadAllBytes(harness.SettingsPath);
        QuantificationDefinition input = harness.Input.DefinitionDraft;
        QuantificationDefinition design = harness.Design.Draft;
        harness.Show();
        Assert.False(harness.Settings.CanEditDefinition);
        Assert.False(ById<TextBox>(harness.View, "DesignDefinitionName").IsEffectivelyEnabled);
        Assert.False(ById<TextBox>(harness.View, "DesignRevision").IsEffectivelyEnabled);
        Assert.False(ById<TextBox>(harness.View, "DesignRoundingDigits").IsEffectivelyEnabled);
        Assert.Contains("Excel", ById<TextBlock>(harness.View, "SettingsCommonDefinitionAvailability").Text!);
        ShowCommonTab(harness, 1);
        Assert.Equal(harness.SettingsPath, ById<TextBox>(harness.View, "SettingsFilePath").Text);
        Assert.Equal(harness.Settings.StoredDefinitionSummary, ById<TextBox>(harness.View, "StoredDefinitionSummary").Text);
        Assert.False(ById<Button>(harness.View, "SettingsApplySavedDefinition").IsEffectivelyEnabled);
        Assert.Contains("Excel", ById<TextBlock>(harness.View, "SettingsApplyAvailability").Text!);

        // FR-002: 旧カテゴリは設定にない。入力がなくても共通だけが表示され、保存定義は消えない。
        foreach (SettingsCategory category in Enum.GetValues<SettingsCategory>())
        {
            harness.Settings.SelectedCategory = category;
            Render();
            AssertOnlyActiveEditor(harness.View);
        }

        SelectCategory(harness, SettingsCategory.Common);
        Assert.True(ActiveContent(harness.View).IsEffectivelyEnabled);
        Assert.Same(input, harness.Input.DefinitionDraft);
        Assert.Same(design, harness.Design.Draft);
        Assert.Equal(original, File.ReadAllBytes(harness.SettingsPath));

        ShowCommonTab(harness, 0);
        ById<ComboBox>(harness.View, "ExecutionConcurrency").SetCurrentValue(ComboBox.SelectedItemProperty, 2);
        Render();
        await Save(harness);
        ApplicationSettings saved = await harness.ReadSettingsAsync();
        Assert.Equal(2, saved.MaxConcurrency);
        AssertCanonical(stored, saved.Definition!);
        AssertCanonical(stored, harness.Settings.StoredDefinition!);
        Assert.Same(input, harness.Input.DefinitionDraft);
        Assert.Same(design, harness.Design.Draft);
        AssertPassive(harness);
    }

    [AvaloniaFact]
    public async Task Failed_save_preserves_bytes_current_edits_and_safe_status_then_explicit_save_can_retry()
    {
        using SettingsHarness harness = new();
        await harness.LoadInputAsync();
        await harness.Settings.InitializeAsync(TestContext.Current.CancellationToken);
        harness.Show();
        await Save(harness);
        byte[] original = File.ReadAllBytes(harness.SettingsPath);
        QuantificationDefinition? stored = harness.Settings.StoredDefinition;
        Edit(harness.View, "DesignDefinitionName", "まだ保存していない定義名");
        Edit(harness.View, "ExecutionOutputDirectory", "relative/" + PrivateCanary);
        QuantificationDefinition draft = harness.Design.Draft;
        await Save(harness);

        Assert.Equal(SettingsSaveStatus.InvalidSettings, harness.Settings.SaveStatus);
        Assert.True(harness.Settings.HasUnsavedChanges);
        AssertCanonical(draft, harness.Design.Draft);
        Assert.Same(stored, harness.Settings.StoredDefinition);
        Assert.Equal(original, File.ReadAllBytes(harness.SettingsPath));
        Assert.Contains("保存失敗", ById<TextBlock>(harness.View, "SettingsStatus").Text!);
        AssertSafeStatus(harness);
        Edit(harness.View, "ExecutionOutputDirectory", string.Empty);
        await Save(harness);
        Assert.Equal(SettingsSaveStatus.Saved, harness.Settings.SaveStatus);
        Assert.False(harness.Settings.HasUnsavedChanges);
        Assert.Equal("まだ保存していない定義名", (await harness.ReadSettingsAsync()).Definition?.Name);
        AssertPassive(harness);
    }

    [AvaloniaFact]
    public async Task IO_failure_does_not_repair_parent_file_and_Load_retry_recovers_with_refreshed_summary()
    {
        using SettingsHarness harness = new();
        Directory.CreateDirectory(harness.Root);
        byte[] sentinel = Encoding.UTF8.GetBytes(PrivateCanary);
        File.WriteAllBytes(harness.SettingsDirectory, sentinel); // Not a directory: deterministic IO failure.
        harness.Show();
        await Reload(harness);
        Assert.Equal(SettingsLoadStatus.ReadFailed, harness.Settings.LoadStatus);
        Assert.True(ById<Button>(harness.View, "SettingsSave").IsEffectivelyEnabled);
        await Save(harness);
        Assert.Equal(SettingsSaveStatus.WriteFailed, harness.Settings.SaveStatus);
        Assert.True(harness.Settings.HasUnsavedChanges);
        Assert.Equal(sentinel, File.ReadAllBytes(harness.SettingsDirectory));
        Assert.Equal(harness.SettingsDirectory, Assert.Single(Directory.GetFileSystemEntries(harness.Root)));
        AssertSafeStatus(harness);
        ShowCommonTab(harness, 1);
        Assert.Null(harness.Settings.StoredDefinition);

        File.Delete(harness.SettingsDirectory); // Only the test-owned sentinel.
        QuantificationDefinition incoming = CreateDefinition() with { SourceSheet = "再読込sheet" };
        await harness.SeedSettingsAsync(new ApplicationSettings { Definition = incoming, MaxConcurrency = 3 });
        await Reload(harness);
        Assert.Equal(SettingsLoadStatus.Loaded, harness.Settings.LoadStatus);
        Assert.Equal(3, harness.Execution.MaxConcurrency);
        Assert.Equal(harness.Settings.StoredDefinitionSummary, ById<TextBox>(harness.View, "StoredDefinitionSummary").Text);
        Assert.Contains("再読込sheet", ById<TextBox>(harness.View, "StoredDefinitionSummary").Text!);
        Assert.False(ById<Button>(harness.View, "SettingsApplySavedDefinition").IsEffectivelyEnabled);
        Assert.False(harness.Settings.HasUnsavedChanges);
        ShowCommonTab(harness, 0);
        Assert.Equal(3, ById<ComboBox>(harness.View, "ExecutionConcurrency").SelectedItem);
        AssertPassive(harness);
    }

    [AvaloniaFact]
    public async Task Cancelled_initial_load_remains_pending_until_the_explicit_retry_button()
    {
        using SettingsHarness harness = new();
        await harness.SeedSettingsAsync(new ApplicationSettings { Definition = CreateDefinition(), MaxConcurrency = 2 });
        byte[] original = File.ReadAllBytes(harness.SettingsPath);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await harness.Settings.InitializeAsync(cancellation.Token);
        Task? cancelled = harness.Settings.LastLoadTask;
        harness.Show();
        Assert.Same(cancelled, harness.Settings.LastLoadTask);
        Assert.True(harness.Settings.IsLoadPending);
        Assert.False(ById<Button>(harness.View, "SettingsSave").IsEffectivelyEnabled);
        Assert.True(ById<Button>(harness.View, "SettingsLoadRetry").IsEffectivelyEnabled);
        Assert.Contains("取り消しました", ById<TextBlock>(harness.View, "SettingsStatus").Text!);
        Assert.Equal(original, File.ReadAllBytes(harness.SettingsPath));

        await Reload(harness);
        Assert.NotSame(cancelled, harness.Settings.LastLoadTask);
        Assert.Equal(SettingsLoadStatus.Loaded, harness.Settings.LoadStatus);
        Assert.False(harness.Settings.IsLoadPending);
        Assert.True(ById<Button>(harness.View, "SettingsSave").IsEffectivelyEnabled);
        Assert.Equal(2, ById<ComboBox>(harness.View, "ExecutionConcurrency").SelectedItem);
        Assert.Equal(original, File.ReadAllBytes(harness.SettingsPath));
        AssertPassive(harness);
    }

    [AvaloniaFact]
    public async Task Saved_definition_is_applied_only_explicitly_and_refreshes_common_root_fields_after_success()
    {
        using SettingsHarness harness = new();
        QuantificationDefinition saved = CreateDefinition() with { Name = "保存定義の名前", Revision = "incoming", RoundingDigits = 3 };
        await harness.SeedSettingsAsync(new ApplicationSettings { Definition = saved });
        await harness.LoadInputAsync();
        await harness.Settings.InitializeAsync(TestContext.Current.CancellationToken);
        harness.Show();
        TextBox name = ById<TextBox>(harness.View, "DesignDefinitionName");
        QuantificationDefinition before = harness.Design.Draft;
        byte[] original = File.ReadAllBytes(harness.SettingsPath);
        Assert.NotEqual(saved.Name, name.Text);
        Assert.Same(before, harness.Design.Draft);
        ShowCommonTab(harness, 1);
        Button apply = ById<Button>(harness.View, "SettingsApplySavedDefinition");
        Assert.Same(harness.Settings.ApplySavedDefinitionCommand, apply.Command);
        Assert.True(apply.IsEffectivelyEnabled);
        int inputReads = harness.Loader.CallCount;
        Activate(apply);
        Assert.True(await Assert.IsAssignableFrom<Task<bool>>(harness.Settings.LastApplySavedDefinitionTask)
            .WaitAsync(TestWait, TestContext.Current.CancellationToken));
        Render();
        Assert.Equal(inputReads + 1, harness.Loader.CallCount);
        AssertCanonical(saved, harness.Input.DefinitionDraft);
        AssertCanonical(saved, harness.Design.Draft);
        Assert.Contains("適用しました", ById<TextBlock>(harness.View, "SettingsApplyStatus").Text!);
        ShowCommonTab(harness, 0);
        Assert.Same(name, ById<TextBox>(harness.View, "DesignDefinitionName"));
        Assert.Equal(saved.Name, name.Text);
        Assert.Equal(saved.Revision, ById<TextBox>(harness.View, "DesignRevision").Text);
        Assert.Equal("3", ById<TextBox>(harness.View, "DesignRoundingDigits").Text);
        Assert.Equal(original, File.ReadAllBytes(harness.SettingsPath));

        QuantificationDefinition incompatible = saved with { SourceSheet = "missing-sheet" };
        await harness.SeedSettingsAsync(new ApplicationSettings { Definition = incompatible });
        await Reload(harness);
        AssertCanonical(saved, harness.Design.Draft);
        ShowCommonTab(harness, 1);
        Assert.Contains("missing-sheet", ById<TextBox>(harness.View, "StoredDefinitionSummary").Text!);
        Activate(ById<Button>(harness.View, "SettingsApplySavedDefinition"));
        Assert.False(await Assert.IsAssignableFrom<Task<bool>>(harness.Settings.LastApplySavedDefinitionTask)
            .WaitAsync(TestWait, TestContext.Current.CancellationToken));
        Render();
        AssertCanonical(saved, harness.Design.Draft);
        AssertCanonical(saved, harness.Input.DefinitionDraft);
        Assert.Contains("適用できませんでした", ById<TextBlock>(harness.View, "SettingsApplyStatus").Text!);
        AssertPassive(harness);
    }

    [AvaloniaFact]
    public async Task SettingsRequestClose_invokes_parent_event_after_sync_without_saving_or_starting_AI()
    {
        using SettingsHarness harness = new();
        await harness.LoadInputAsync();
        await harness.Settings.InitializeAsync(TestContext.Current.CancellationToken);
        harness.Show();
        Edit(harness.View, "DesignDefinitionName", "戻る直前の最新編集");
        int events = 0;
        object? observedSender = null;
        string? synchronizedName = null;
        harness.Settings.CloseRequested += (sender, _) =>
        {
            events++;
            observedSender = sender;
            synchronizedName = harness.Input.DefinitionDraft.Name;
        };
        Button back = ById<Button>(harness.View, "SettingsRequestClose");
        Assert.Same(harness.Settings.RequestCloseCommand, back.Command);
        Assert.False(back.IsCancel);
        Press(harness.Window, Key.Escape);
        Assert.Equal(0, events);
        Activate(back);
        Assert.Equal(1, events);
        Assert.Same(harness.Settings, observedSender);
        Assert.Equal("戻る直前の最新編集", synchronizedName);
        Assert.Null(harness.Settings.LastSaveTask);
        Assert.False(File.Exists(harness.SettingsPath));
        AssertPassive(harness);
    }

    [AvaloniaFact]
    public async Task Replacement_and_reattachment_follow_only_the_current_Settings_owner_and_release_old_editors()
    {
        using SettingsHarness previous = new(withStore: false);
        using SettingsHarness current = new(withStore: false);
        await previous.LoadInputAsync();
        await current.LoadInputAsync();
        previous.Show();
        SelectCategory(previous, SettingsCategory.Common);
        Control oldEditor = ActiveContent(previous.View);
        previous.View.DataContext = current.Settings;
        Render();
        Assert.Same(current.Settings, previous.View.ViewModel);
        Assert.Same(current.Settings.SaveCommand, ById<Button>(previous.View, "SettingsSave").Command);
        Assert.Same(current.Settings.RequestCloseCommand, ById<Button>(previous.View, "SettingsRequestClose").Command);
        current.Design.DefinitionName = "新しい編集元";
        previous.Settings.SelectedCategory = SettingsCategory.Special;
        previous.Design.DefinitionName = "古い編集元";
        Render();
        Assert.IsType<TabControl>(ActiveContent(previous.View));
        Assert.Equal("新しい編集元", ById<TextBox>(previous.View, "DesignDefinitionName").Text);
        Assert.DoesNotContain(AllControls(previous.View), control => ReferenceEquals(control, oldEditor));

        previous.Window.Content = null;
        Render();
        current.Settings.SelectedCategory = SettingsCategory.Mapping;
        previous.Window.Content = previous.View;
        Render();
        Assert.Same(current.Settings, ActiveContent(previous.View).DataContext);
        Assert.NotSame(oldEditor, ActiveContent(previous.View));
        AssertOnlyActiveEditor(previous.View);
        AssertUniqueIds(previous.View);
        AssertPassive(previous);
        AssertPassive(current);
    }

    [AvaloniaFact]
    public async Task Known_950_by_450_slot_keeps_44_DIP_navigation_footer_and_at_least_340_DIP_finite_body()
    {
        using SettingsHarness harness = new(withStore: false);
        await harness.LoadInputAsync();
        harness.Show();
        Assert.Equal(new Size(950, 450), harness.Window.ClientSize);
        Assert.IsType<Grid>(harness.View.Content);
        Assert.Equal(44d, Required<Grid>(harness.View, "SettingsCategoryBar").Bounds.Height);
        Assert.Equal(44d, Required<Grid>(harness.View, "SettingsFooter").Bounds.Height);

        foreach (SettingsCategory category in new[] { SettingsCategory.Common })
        {
            SelectCategory(harness, category);
            ContentControl content = Required<ContentControl>(harness.View, "CurrentSettingsContent");
            ScrollViewer outer = Required<ScrollViewer>(harness.View, "SettingsBodyScroll");
            Assert.True(double.IsFinite(content.Bounds.Height));
            Assert.InRange(content.Bounds.Height, 340d, 346d);
            Assert.InRange(content.Bounds.Width, 949d, 950d);
            Assert.True(outer.Extent.Height <= outer.Viewport.Height + 1d, category.ToString());
            Assert.True(outer.Extent.Width <= outer.Viewport.Width + 1d, category.ToString());
            Assert.Equal(default, outer.Offset);
            AssertOnlyActiveEditor(harness.View);
            AssertUniqueIds(harness.View);
            AssertInteractiveBounds(harness.View);
            for (int index = 1; index < 3; index++)
            {
                ById<TabControl>(harness.View, "SettingsCommonTabs").SelectedIndex = index;
                Render();
                AssertInteractiveBounds(harness.View);
                AssertUniqueIds(harness.View);
                Assert.True(outer.Extent.Height <= outer.Viewport.Height + 1d, category.ToString());
            }
        }

        AssertPassive(harness);
    }

    [AvaloniaTheory]
    [InlineData(760d, 320d)]
    [InlineData(600d, 320d)]
    public async Task Short_or_narrow_body_scroll_is_explicit_and_never_covers_fixed_Save_and_return(double width, double height)
    {
        using SettingsHarness harness = new(withStore: false);
        await harness.LoadInputAsync();
        harness.Window.Width = width;
        harness.Window.Height = height;
        harness.Show();
        ScrollViewer scroll = Required<ScrollViewer>(harness.View, "SettingsBodyScroll");
        Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
        if (width >= 720)
        {
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1d);
        }

        scroll.ScrollToEnd();
        Render();
        Assert.True(scroll.Offset.Y > 0d);
        foreach (string id in new[] { "SettingsSave", "SettingsRequestClose", "SettingsLoadRetry" })
        {
            Button button = ById<Button>(harness.View, id);
            AssertFullyInside(button, harness.View);
            Assert.DoesNotContain(button.GetVisualAncestors(), ancestor => ReferenceEquals(ancestor, scroll));
            Assert.True(button.Bounds.Height >= 44d);
        }

        AssertPassive(harness);
    }

    [AvaloniaFact]
    public async Task Long_output_path_remains_complete_without_growing_the_outer_body()
    {
        using SettingsHarness harness = new(withStore: false);
        await harness.LoadInputAsync();
        harness.Show();
        string path = Path.Combine(harness.Root, new string('長', 220));
        Edit(harness.View, "ExecutionOutputDirectory", path);
        Assert.Equal(path, ById<TextBox>(harness.View, "ExecutionOutputDirectory").Text);
        Assert.Equal(path, ById<TextBox>(harness.View, "SettingsEffectiveOutputDirectory").Text);
        ScrollViewer outer = Required<ScrollViewer>(harness.View, "SettingsBodyScroll");
        Assert.True(outer.Extent.Height <= outer.Viewport.Height + 1d);
        Assert.True(outer.Extent.Width <= outer.Viewport.Width + 1d);
        Assert.Equal(default, outer.Offset);
        AssertFullyInside(ById<Button>(harness.View, "SettingsSave"), harness.View);
        AssertFullyInside(ById<Button>(harness.View, "SettingsRequestClose"), harness.View);
        Assert.False(Directory.Exists(harness.Root));
        AssertPassive(harness);

        AssertPassive(harness);
    }

    [AvaloniaFact]
    public async Task Category_focus_and_Tab_order_keep_navigation_before_content_then_fixed_actions()
    {
        using SettingsHarness harness = new();
        await harness.LoadInputAsync();
        await harness.Settings.InitializeAsync(TestContext.Current.CancellationToken);
        harness.Show();
        Button common = ById<Button>(harness.View, "SettingsCategoryCommon");
        Assert.True(common.Focus(NavigationMethod.Tab));
        Press(harness.Window, Key.Tab);
        // FR-002: カテゴリは共通だけなので、次の Tab はカテゴリ列を出て内容（共通のタブ列・入力欄）へ進む。
        Control afterCommon = Assert.IsAssignableFrom<Control>(harness.Window.FocusManager?.GetFocusedElement());
        Assert.NotSame(common, afterCommon);
        Assert.Contains(afterCommon.GetVisualAncestors().Prepend(afterCommon),
            ancestor => ReferenceEquals(ancestor, Required<ContentControl>(harness.View, "CurrentSettingsContent")));

        SelectCategory(harness, SettingsCategory.Common);
        WrapPanel tabStrip = Assert.Single(ById<TabControl>(harness.View, "SettingsCommonTabs")
            .GetVisualDescendants().OfType<WrapPanel>(), panel => panel.Children.All(child => child is TabItem));
        Assert.Equal(KeyboardNavigationMode.Once, KeyboardNavigation.GetTabNavigation(tabStrip));
        Assert.Equal(0, tabStrip.TabIndex);
        TextBox lastField = ById<TextBox>(harness.View, "DesignRoundingDigits");
        Assert.True(lastField.Focus(NavigationMethod.Tab));
        Press(harness.Window, Key.Tab);
        Assert.Equal("SettingsRequestClose", AutomationProperties.GetAutomationId(
            Assert.IsAssignableFrom<Control>(harness.Window.FocusManager?.GetFocusedElement())));
        Assert.Same(ById<Button>(harness.View, "SettingsRequestClose"), harness.Window.FocusManager?.GetFocusedElement());
        Press(harness.Window, Key.Tab, RawInputModifiers.Shift);
        Assert.Same(lastField, harness.Window.FocusManager?.GetFocusedElement());
        Press(harness.Window, Key.Tab);
        Assert.Same(ById<Button>(harness.View, "SettingsRequestClose"), harness.Window.FocusManager?.GetFocusedElement());
        Press(harness.Window, Key.Tab);
        Assert.Same(ById<Button>(harness.View, "SettingsLoadRetry"), harness.Window.FocusManager?.GetFocusedElement());
        Press(harness.Window, Key.Tab);
        Assert.Same(ById<Button>(harness.View, "SettingsSave"), harness.Window.FocusManager?.GetFocusedElement());
        Assert.Null(harness.Settings.LastSaveTask);
        AssertPassive(harness);
    }

    private static QuantificationDefinition CreateDefinition()
    {
        QuantificationDefinition seed = U04TestSupport.Definition(2, 3);
        QuestionDefinition question = seed.Questions[0];
        EvaluatorDefinition evaluator = question.Evaluators[0];
        return seed with
        {
            Name = "T17の合成採点定義",
            BasePoints = 60m,
            Questions = [.. Enumerable.Range(1, 2).Select(index => question with
            {
                Id = $"Q{index}",
                DisplayName = $"設問 {index}",
                Points = 20m,
                Evaluators = [evaluator with
                {
                    Id = $"E{index}",
                    DisplayName = $"評価方法 {index}",
                    Criteria = [evaluator.Criteria[0] with { Id = $"C{index}", DisplayName = $"観点 {index}" }],
                }],
                SpecialEvaluations = [new SpecialEvaluationDefinition
                {
                    Id = $"S{index}", DisplayName = $"固有項目 {index}", PrimarySourceColumn = "C",
                    SupportingSourceColumns = ["B"], PromptTemplate = "固有評価 {回答}", Enabled = true,
                }],
            })],
        };
    }

    [AvaloniaFact]
    public async Task Common_settings_hosts_copilot_login_bound_to_the_execution_commands()
    {
        using SettingsHarness harness = new(withStore: false);
        await harness.LoadInputAsync();
        harness.Show();
        ShowCommonTab(harness, 0);

        Assert.True(ById<StackPanel>(harness.View, "SettingsCopilotLoginPanel").IsEffectivelyVisible);
        Assert.Same(harness.Settings.Execution.CheckAuthenticationCommand, ById<Button>(harness.View, "SettingsCheckCopilotAuthentication").Command);
        Assert.Same(harness.Settings.Execution.LoginCommand, ById<Button>(harness.View, "SettingsStartCopilotLogin").Command);
        Assert.Same(harness.Settings.Execution.CancelLoginCommand, ById<Button>(harness.View, "SettingsCancelCopilotLogin").Command);
        Assert.Equal(harness.Settings.Execution.AuthenticationStatusText, ById<TextBlock>(harness.View, "SettingsCopilotAuthenticationStatus").Text);
        Assert.Equal(harness.Settings.Execution.LoginStatusText, ById<TextBlock>(harness.View, "SettingsCopilotLoginStatus").Text);
        AssertUniqueIds(harness.View);
        AssertPassive(harness);
    }
    private static void SelectCategory(SettingsHarness harness, SettingsCategory category)
    {
        Button button = ById<Button>(harness.View, "SettingsCategory" + category);
        Activate(button);
        Assert.Equal(category, harness.Settings.SelectedCategory);
    }

    private static void ShowCommonTab(SettingsHarness harness, int index)
    {
        SelectCategory(harness, SettingsCategory.Common);
        ById<TabControl>(harness.View, "SettingsCommonTabs").SelectedIndex = index;
        Render();
    }

    private static async Task Save(SettingsHarness harness)
    {
        Activate(ById<Button>(harness.View, "SettingsSave"));
        await Assert.IsAssignableFrom<Task>(harness.Settings.LastSaveTask).WaitAsync(TestWait, TestContext.Current.CancellationToken);
        Render();
    }

    private static async Task Reload(SettingsHarness harness)
    {
        Activate(ById<Button>(harness.View, "SettingsLoadRetry"));
        await Assert.IsAssignableFrom<Task>(harness.Settings.LastLoadTask).WaitAsync(TestWait, TestContext.Current.CancellationToken);
        Render();
    }

    private static void Edit(Control root, string id, string text)
    {
        TextBox control = ById<TextBox>(root, id);
        Assert.True(control.IsEffectivelyEnabled);
        Assert.NotNull(BindingOperations.GetBindingExpressionBase(control, TextBox.TextProperty));
        control.SetCurrentValue(TextBox.TextProperty, text);
        Render();
    }

    private static T Required<T>(Control root, string name) where T : Control =>
        Assert.IsType<T>(root.FindControl<T>(name));

    private static T ById<T>(Control root, string id) where T : Control =>
        Assert.Single(root.GetVisualDescendants().OfType<T>(), control => AutomationProperties.GetAutomationId(control) == id);

    private static Control ActiveContent(SettingsView view) =>
        Assert.IsAssignableFrom<Control>(Required<ContentControl>(view, "CurrentSettingsContent").Content);

    private static IEnumerable<Control> AllControls(Control root) => root.GetVisualDescendants().OfType<Control>()
        .Concat(root.GetLogicalDescendants().OfType<Control>()).Prepend(root).Distinct();

    private static void AssertOnlyActiveEditor(SettingsView view)
    {
        // FR-002: 設定に表示されるのは共通（TabControl）だけで、旧カテゴリの編集部は存在しない。
        Assert.Equal(typeof(TabControl), ActiveContent(view).GetType());
        Assert.DoesNotContain(AllControls(view), control => control is EvaluatorSettingsView);
    }

    private static void AssertUniqueIds(Control root)
    {
        string[] ids = AllControls(root).Select(AutomationProperties.GetAutomationId)
            .Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    private static void AssertInteractiveBounds(SettingsView view)
    {
        TemplatedControl[] controls = view.GetVisualDescendants().OfType<TemplatedControl>()
            .Where(control => control is TextBox or ComboBox or Button or CheckBox or TabItem or ListBox)
            .Where(control => control.IsEffectivelyVisible && !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(control)))
            .ToArray();
        Assert.NotEmpty(controls);
        ContentControl body = Required<ContentControl>(view, "CurrentSettingsContent");
        foreach (TemplatedControl control in controls)
        {
            string? id = AutomationProperties.GetAutomationId(control);
            Assert.True(control.Bounds.Height >= 44d, id);
            Assert.True(control.Bounds.Width >= 44d, id);
            Assert.Equal(14d, control.FontSize);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)), id);
            Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(control)?.ToString()), id);
            AssertFullyInside(control, control.GetVisualAncestors().Contains(body) ? body : view);
        }
    }

    private static void AssertFullyInside(Control control, Control container)
    {
        Point origin = control.TranslatePoint(default, container)
            ?? throw new InvalidOperationException("The test target must be attached to its container.");
        string? id = AutomationProperties.GetAutomationId(control);
        Assert.True(origin.X >= -1d && origin.Y >= -1d, id);
        Assert.True(origin.X + control.Bounds.Width <= container.Bounds.Width + 1d, id);
        Assert.True(origin.Y + control.Bounds.Height <= container.Bounds.Height + 1d, id);
    }

    private static void AssertCanonical(QuantificationDefinition expected, QuantificationDefinition actual) =>
        Assert.Equal(Canonical.Serialize(expected), Canonical.Serialize(actual));

    private static void AssertSafeStatus(SettingsHarness harness)
    {
        string? status = ById<TextBlock>(harness.View, "SettingsStatus").Text;
        Assert.Equal(harness.Settings.StatusText, status);
        Assert.DoesNotContain(PrivateCanary, status ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(harness.Root, status ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(IOException), status ?? string.Empty, StringComparison.Ordinal);
    }

    private static void AssertPassive(SettingsHarness harness, int authenticationChecks = 0)
    {
        Assert.Equal(authenticationChecks, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
        Assert.Null(harness.Execution.LastLoginTask);
        Assert.False(harness.Execution.IsLoggingIn);
        Assert.False(harness.Execution.IsRunning);
    }

    private static void Activate(Button button)
    {
        Assert.True(button.IsEffectivelyEnabled);
        Assert.True(button.Focus(NavigationMethod.Tab));
        Press(Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(button)), Key.Enter);
    }

    private static void Press(TopLevel window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        PhysicalKey physical = key switch
        {
            Key.Tab => PhysicalKey.Tab,
            Key.Enter => PhysicalKey.Enter,
            Key.Escape => PhysicalKey.Escape,
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
        Render();
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class SettingsHarness : IDisposable
    {
        public SettingsHarness(bool withStore = true)
        {
            Store = new SettingsFileStore(SettingsPath);
            Input = new InputViewModel(Loader);
            Design = new QuantificationDesignViewModel(CreateDefinition(), ["A", "B", "C"]);
            Execution = new ExecutionViewModel(Authentication, Runner);
            Settings = new SettingsViewModel(Input, Design, Execution, withStore ? Store : null);
            View = new SettingsView(Settings);
            Window = new Window { Width = 950, Height = 450, Content = View };
        }

        // No LocalApplicationData resolution or runtime/user home: all IO is injected and synthetic.
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "StudyReportEvaluator-T17-" + Guid.NewGuid().ToString("N"));
        public string SettingsDirectory => Path.Combine(Root, "settings");
        public string SettingsPath => Path.Combine(SettingsDirectory, "setting.txt");
        public string OutputPath => Path.Combine(Root, "未作成の出力先");
        public SyntheticInputLoader Loader { get; } = new();
        public RecordingAuthenticationBoundary Authentication { get; } = new(
            new ExecutionAuthenticationSnapshot(ExecutionAuthenticationState.Available,
                [U04TestSupport.Model("model-test"), U04TestSupport.Model("model-other"), U04TestSupport.Model("auto")],
                U04TestSupport.RuntimeIdentity()));
        public RecordingRunBoundary Runner { get; } = new((_, _, _) => throw new InvalidOperationException("No run was requested."));
        public SettingsFileStore Store { get; }
        public InputViewModel Input { get; }
        public QuantificationDesignViewModel Design { get; }
        public ExecutionViewModel Execution { get; }
        public SettingsViewModel Settings { get; }
        public SettingsView View { get; }
        public Window Window { get; }

        public async Task LoadInputAsync()
        {
            QuantificationDefinition definition = CreateDefinition();
            var metadata = U01TestSupport.ValidateMapping(definition).Metadata;
            Loader.Result = new InputWorkbookLoadResult(U01TestSupport.InputSnapshot(), metadata,
                new ColumnMappingSuggester().Suggest(metadata));
            string path = Path.Combine(Root, "synthetic-T17.xlsx");
            await Input.SetFilePathAsync(path, TestContext.Current.CancellationToken);
            Assert.True(await Input.ApplySavedDefinitionAsync(definition, TestContext.Current.CancellationToken));
            Settings.SynchronizeDrafts();
            Execution.Configure(Input.DefinitionDraft, metadata, path);
            Assert.True(Input.HasLoadedWorkbook);
        }

        public async Task SeedSettingsAsync(ApplicationSettings value) =>
            Assert.Equal(SettingsSaveStatus.Saved, (await Store.SaveAsync(value, TestContext.Current.CancellationToken)).Status);

        public async Task<ApplicationSettings> ReadSettingsAsync()
        {
            SettingsLoadResult loaded = await Store.LoadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(SettingsLoadStatus.Loaded, loaded.Status);
            return Assert.IsType<ApplicationSettings>(loaded.Settings);
        }

        public void Show()
        {
            Window.Show();
            Render();
        }

        public void Dispose()
        {
            Window.Close();
            Settings.Dispose();
            Execution.Dispose();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class SyntheticInputLoader : IInputWorkbookLoader
    {
        public InputWorkbookLoadResult? Result { get; set; }
        public int CallCount { get; private set; }

        public Task<InputWorkbookLoadResult> LoadAsync(string filePath, uint headerRow, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(Result ?? throw new InvalidOperationException("No synthetic input was configured."));
        }
    }
}

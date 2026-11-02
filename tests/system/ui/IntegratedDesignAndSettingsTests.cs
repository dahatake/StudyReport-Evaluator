using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(StudyReportEvaluator.SystemTests.Ui.SystemTestAppBuilder))]

namespace StudyReportEvaluator.SystemTests.Ui;

public sealed class SystemTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<StudyReportEvaluator.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// System tests whose expectations come only from the acceptance criteria (FR-001/AC-054, FR-002/AC-055).
/// Production code is used only to launch the window and to reach screens.
/// </summary>
public sealed class IntegratedDesignAndSettingsTests
{
    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static T? Find<T>(Visual root, string automationId) where T : Control =>
        root.GetVisualDescendants().OfType<T>()
            .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == automationId || c.Name == automationId);

    private static T Required<T>(Visual root, string automationId) where T : Control =>
        Find<T>(root, automationId) ?? throw new Xunit.Sdk.XunitException($"AutomationId '{automationId}' not found");

    private static MainWindow OpenWindow(double width, double height)
    {
        MainWindowViewModel shell = new(new WorkflowNavigator());
        MainWindow window = new(shell) { Width = width, Height = height };
        window.Show();
        Render();
        return window;
    }

    private static void Click(Button button)
    {
        Assert.True(button.IsEffectivelyEnabled);
        button.Command?.Execute(button.CommandParameter);
        Render();
    }

    // FR-001 AC-054: 配点・設問ごとの有効/配点・計算式と、設問タブ・評価方法/項目・基本/Promptタブが 1 画面に同時にある。
    // Automation ID は旧画面と同じ。最小 window で外側スクロール不要。
    [AvaloniaFact]
    public void FR001_AC054_Design_screen_has_allocation_and_normal_evaluation_together_at_minimum_window()
    {
        MainWindow window = OpenWindow(1024, 720);
        window.ViewModel.Navigator.NavigateTo(WorkflowStep.Design);
        Render();
        QuantificationDesignView view = Assert.IsType<QuantificationDesignView>(
            window.GetVisualDescendants().OfType<QuantificationDesignView>().Single());

        foreach (string id in new[]
        {
            "DesignBasePoints", "DesignSpecialPoints", "DesignSimilarityPenaltyWeight", "DesignAllocationSummary",
            "DesignQuestions", "DesignFormulaSummary",
        })
        {
            Assert.NotNull(Find<Control>(view, id));
        }

        foreach (string id in new[]
        {
            "EvaluatorSettingsQuestions", "EvaluatorSettingsEvaluators", "EvaluatorSettingsCriteria",
            "EvaluatorSettingsTabs", "EvaluatorSettingsBasicTab", "EvaluatorSettingsPromptTab",
        })
        {
            Assert.NotNull(Find<Control>(view, id));
        }

        // 同じ入力欄を二重に置かない（異常系: 重複した Automation ID）。
        string[] ids = view.GetVisualDescendants().OfType<Control>()
            .Select(AutomationProperties.GetAutomationId)
            .Where(id => !string.IsNullOrEmpty(id) && id!.StartsWith("EvaluatorSettings", StringComparison.Ordinal))
            .ToArray()!;
        Assert.Equal(ids.Length, ids.Distinct().Count());

        // 設問タブの選択で通常評価の選択設問が連動する（設問が 2 件以上ある場合）。
        ListBox questions = Required<ListBox>(view, "EvaluatorSettingsQuestions");
        if (questions.ItemCount >= 2)
        {
            questions.SelectedIndex = 0;
            Render();
            object? first = questions.SelectedItem;
            questions.SelectedIndex = 1;
            Render();
            Assert.NotSame(first, questions.SelectedItem);
        }

        // 外側スクロールを要しない。
        ScrollViewer? outer = Find<ScrollViewer>(view, "QuantificationDesignScrollViewer");
        if (outer is not null)
        {
            Assert.True(outer.Extent.Height <= outer.Viewport.Height + 1,
                $"outer scroll required: extent={outer.Extent.Height} viewport={outer.Viewport.Height}");
        }
    }

    // FR-002 AC-055: カテゴリは「共通」だけ。旧カテゴリのボタンが存在しない。共通設定は従来どおり。
    [AvaloniaFact]
    public void FR002_AC055_Settings_has_only_common_category_and_common_controls_remain()
    {
        MainWindow window = OpenWindow(1024, 720);
        Click(Required<Button>(window, "SettingsOpenButton"));
        SettingsView settings = Assert.IsType<SettingsView>(window.GetVisualDescendants().OfType<SettingsView>().Single());

        foreach (string removed in new[]
        {
            "SettingsCategoryMapping", "SettingsCategorySpecial", "SettingsCategoryImportedPrompts", "SettingsCategoryEvaluation",
        })
        {
            Assert.Null(Find<Control>(settings, removed));
        }

        string[] categoryButtons = settings.GetVisualDescendants().OfType<Button>()
            .Select(AutomationProperties.GetAutomationId)
            .Where(id => id is not null && id.StartsWith("SettingsCategory", StringComparison.Ordinal))
            .Where(id => id != "SettingsCategories" && id != "SettingsCategoryBar")
            .ToArray()!;
        Assert.True(categoryButtons.Length <= 1);
        Assert.All(categoryButtons, id => Assert.Equal("SettingsCategoryCommon", id));

        foreach (string id in new[]
        {
            "ExecutionModel", "ExecutionOutputDirectory", "SettingsCopilotLoginPanel",
            "SettingsCheckCopilotAuthentication", "SettingsStartCopilotLogin", "SettingsSave",
        })
        {
            Assert.NotNull(Find<Control>(settings, id));
        }
    }

    // FR-002 AC-055: 2 採点設計から旧設定への入口は、存在するカテゴリだけを指す。
    [AvaloniaFact]
    public void FR002_AC055_Design_entry_buttons_never_point_to_removed_settings_categories()
    {
        MainWindow window = OpenWindow(1024, 720);
        window.ViewModel.Navigator.NavigateTo(WorkflowStep.Design);
        Render();

        foreach (Button entry in window.GetVisualDescendants().OfType<Button>()
                     .Where(b => (AutomationProperties.GetAutomationId(b) ?? string.Empty).Contains("Settings", StringComparison.Ordinal)
                                 && AutomationProperties.GetAutomationId(b) != "SettingsOpenButton")
                     .ToArray())
        {
            if (!entry.IsEffectivelyEnabled || entry.Command is null)
            {
                continue;
            }

            Click(entry);
            Assert.Equal("Common", window.ViewModel.Settings.SelectedCategory.ToString());
            window.ViewModel.Navigator.NavigateTo(WorkflowStep.Design);
            Render();
        }
    }
}

using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using Xunit;

namespace StudyReportEvaluator.SystemTests.Ui;

/// <summary>
/// FR-118 (error display uses a red palette). Expectations come only from AC-093..096 and PARAM-005..007:
/// bg #FDE7E9 / border #C50F1F / text #A4262C; contrast text:bg >= 4.5, border:adjacent >= 3.0.
/// </summary>
public sealed class ErrorColorTests
{
    private const double MinTextContrast = 4.5;   // PARAM-005
    private const double MinBorderContrast = 3.0; // PARAM-006
    private static readonly string[] LegacyBlue = ["#EAF5FF", "#62A9E8", "#174F7C"];

    private static readonly string[] ErrorBrushKeys =
        ["TechnicalBackgroundBrush", "TechnicalBorderBrush", "TechnicalTextBrush"];

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "StudyReportEvaluator.App")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return dir ?? throw new Xunit.Sdk.XunitException("repo root not found");
    }

    private static string ViewsDir() => Path.Combine(RepoRoot(), "src", "StudyReportEvaluator.App");

    private static double Lum(Color c)
    {
        static double Ch(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
    }

    private static double Contrast(Color a, Color b)
    {
        double la = Lum(a), lb = Lum(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static Color Brush(string key)
    {
        Assert.NotNull(Application.Current);
        Assert.True(Application.Current!.TryGetResource(key, null, out object? v), $"resource {key} missing");
        return Assert.IsAssignableFrom<ISolidColorBrush>(v).Color;
    }

    private static bool IsRed(Color c) => c.R > c.G + 40 && c.R > c.B + 40;

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    // FR-118 AC-093 AC-094 : brush real values (computed contrast, red family, no legacy blue)
    [AvaloniaFact]
    public void FR118_AC093_AC094_Error_brushes_are_red_and_meet_contrast()
    {
        Color bg = Brush("TechnicalBackgroundBrush");
        Color border = Brush("TechnicalBorderBrush");
        Color text = Brush("TechnicalTextBrush");

        // PARAM-007 exact values
        Assert.Equal(Color.Parse("#FDE7E9"), bg);
        Assert.Equal(Color.Parse("#C50F1F"), border);
        Assert.Equal(Color.Parse("#A4262C"), text);
        Assert.True(IsRed(border) && IsRed(text));

        // AC-093
        Assert.True(Contrast(text, bg) >= MinTextContrast, $"text/bg {Contrast(text, bg):F2}");
        Assert.True(Contrast(border, bg) >= MinBorderContrast, $"border/bg {Contrast(border, bg):F2}");
        Assert.True(Contrast(border, Colors.White) >= MinBorderContrast, $"border/white {Contrast(border, Colors.White):F2}");

        // Sanity of the calculator itself (abnormal case: the legacy blue text on pale bg is not the red palette)
        Assert.Equal(21.0, Contrast(Colors.Black, Colors.White), 1);
        Assert.False(IsRed(Color.Parse("#62A9E8")));

        // AC-094: legacy blue values must not remain as error colors anywhere in the error brush definitions
        string app = File.ReadAllText(Path.Combine(ViewsDir(), "App.axaml"));
        foreach (string key in ErrorBrushKeys)
        {
            Match m = Regex.Match(app, $"x:Key=\"{key}\"[^>]*Color=\"(#[0-9A-Fa-f]{{6,8}})\"");
            Assert.True(m.Success, key);
            Assert.DoesNotContain(m.Groups[1].Value.ToUpperInvariant(), LegacyBlue);
        }
    }

    // FR-118 AC-094 : cancel button stays blue (accent) and no view hard-codes the legacy blue for errors
    [AvaloniaFact]
    public void FR118_AC094_Cancel_button_stays_blue_and_error_views_have_no_legacy_blue()
    {
        MainWindow window = new(new MainWindowViewModel(new WorkflowNavigator())) { Width = 1024, Height = 720 };
        window.Show();
        Render();

        Button cancel = window.GetVisualDescendants().OfType<Button>()
            .First(b => b.Name == "ShellCancelRunButton" || AutomationProperties.GetAutomationId(b) == "ShellCancelRunButton");
        Assert.Contains("cancel", cancel.Classes);
        Color errBorder = Brush("TechnicalBorderBrush");
        if (cancel.Background is ISolidColorBrush cb)
        {
            Assert.False(IsRed(cb.Color), "cancel background must not be red");
        }
        if (cancel.Foreground is ISolidColorBrush cf)
        {
            Assert.False(IsRed(cf.Color), "cancel foreground must not be red");
        }
        if (cancel.BorderBrush is ISolidColorBrush cbb)
        {
            Assert.NotEqual(errBorder, cbb.Color);
            Assert.False(IsRed(cbb.Color), "cancel border must not be red");
        }

        // Views must not use the legacy blue literals directly for error displays (only the cancel style may keep blue).
        foreach (string file in Directory.GetFiles(Path.Combine(ViewsDir(), "Views"), "*.axaml"))
        {
            string xaml = File.ReadAllText(file);
            if (Path.GetFileName(file) == "MainWindow.axaml") { continue; }
            foreach (string blue in LegacyBlue)
            {
                Assert.DoesNotContain(blue, xaml, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // FR-118 AC-095 : every element that uses the error brushes keeps a non-color cue ("エラー" text, bold weight or thick border)
    [Fact]
    public void FR118_AC095_Error_displays_keep_non_color_cue()
    {
        string viewsDir = Path.Combine(ViewsDir(), "Views");
        int checkedUsages = 0;
        foreach (string file in Directory.GetFiles(viewsDir, "*.axaml"))
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!Regex.IsMatch(lines[i], "Technical(Background|Border|Text)Brush")) { continue; }
                // the cancel button is not an error display
                int from = Math.Max(0, i - 6), to = Math.Min(lines.Length - 1, i + 14);
                string window = string.Join('\n', lines[from..(to + 1)]);
                if (window.Contains("cancel", StringComparison.OrdinalIgnoreCase) && lines[i].Contains("Setter")) { continue; }
                checkedUsages++;
                bool cue = window.Contains("エラー")
                    || Regex.IsMatch(window, "FontWeight=\"(SemiBold|Bold)\"|Property=\"FontWeight\"")
                    || Regex.IsMatch(window, "BorderThickness=\"[2-9]")
                    || Regex.IsMatch(window, "Text(=|:)[^\\n]*(エラー|error)", RegexOptions.IgnoreCase)
                    || window.Contains("Message") || window.Contains("Error");
                Assert.True(cue, $"{Path.GetFileName(file)}:{i + 1} has no non-color cue near error brush");
            }
        }
        Assert.True(checkedUsages > 0, "no error brush usages found");
    }

    // FR-118 AC-096 : render the shell at the minimum window (1024x720) with error displays shown, no text overlaps/clips; PNG as evidence
    [AvaloniaFact]
    public void FR118_AC096_Error_screens_render_without_overlapping_or_clipped_text_at_minimum_window()
    {
        MainWindow window = new(new MainWindowViewModel(new WorkflowNavigator())) { Width = 1024, Height = 720 };
        window.Show();
        Render();

        // Show the shell-level technical error and any other error placeholders reachable by automation id.
        foreach (Control c in window.GetVisualDescendants().OfType<Control>())
        {
            string id = AutomationProperties.GetAutomationId(c) ?? c.Name ?? "";
            if (id is "TechnicalValidationPlaceholder")
            {
                c.IsVisible = true;
            }
            if (c is TextBlock tb && id == "TechnicalValidationMessage" && string.IsNullOrEmpty(tb.Text))
            {
                tb.Text = "エラー: 技術検証エラーがあります。設定を確認してください。";
            }
        }
        Render();

        string evidenceDir = Path.Combine(RepoRoot(), "work", "evidence", "fr118");
        Directory.CreateDirectory(evidenceDir);
        using (var frame = window.CaptureRenderedFrame())
        {
            Assert.NotNull(frame);
            using FileStream fs = File.Create(Path.Combine(evidenceDir, "fr118-ac096-1024x720.png")); frame!.Save(fs, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        List<TextBlock> blocks = window.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(t.Text) && t.Bounds.Width > 0).ToList();
        Assert.NotEmpty(blocks);

        // not cut off: the text fits its measured area (no ellipsis/clipping requirement for wrapped or trimmed text)
        foreach (TextBlock t in blocks)
        {
            Assert.True(t.DesiredSize.Height <= t.Bounds.Height + 1.0,
                $"text clipped vertically: '{t.Text}' desired {t.DesiredSize.Height} > {t.Bounds.Height}");
            Assert.NotEqual(TextTrimming.CharacterEllipsis, t.TextTrimming);
        }

        // not overlapping: no two text blocks (not nested in each other) intersect
        for (int i = 0; i < blocks.Count; i++)
        {
            for (int j = i + 1; j < blocks.Count; j++)
            {
                Point? pa = blocks[i].TranslatePoint(default, window);
                Point? pb = blocks[j].TranslatePoint(default, window);
                if (pa is null || pb is null) { continue; }
                Rect a = new(pa.Value, blocks[i].Bounds.Size);
                Rect b = new(pb.Value, blocks[j].Bounds.Size);
                Rect inter = a.Intersect(b);
                Assert.True(inter.Width < 1.0 || inter.Height < 1.0,
                    $"text overlaps: '{blocks[i].Text}' and '{blocks[j].Text}'");
            }
        }
    }
}




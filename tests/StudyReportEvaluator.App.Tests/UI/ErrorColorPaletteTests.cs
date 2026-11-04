using System.Text.RegularExpressions;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

/// <summary>
/// FR-118 (エラー表示は赤系の配色). Unit/integration checks over the real brush values and the axaml usages.
/// </summary>
public sealed class ErrorColorPaletteTests
{
    private static string AppDir()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "StudyReportEvaluator.App")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return Path.Combine(dir ?? throw new InvalidOperationException("repo root not found"), "src", "StudyReportEvaluator.App");
    }

    private static (int R, int G, int B) Brush(string key)
    {
        string app = File.ReadAllText(Path.Combine(AppDir(), "App.axaml"));
        Match m = Regex.Match(app, $"x:Key=\"{key}\"\\s+Color=\"#([0-9A-Fa-f]{{6}})\"");
        Assert.True(m.Success, key);
        int v = Convert.ToInt32(m.Groups[1].Value, 16);
        return (v >> 16 & 0xFF, v >> 8 & 0xFF, v & 0xFF);
    }

    private static double Lum((int R, int G, int B) c)
    {
        static double Ch(int v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
    }

    private static double Contrast((int R, int G, int B) a, (int R, int G, int B) b)
    {
        double la = Lum(a), lb = Lum(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    // FR-118 AC-093 (PARAM-005/006/007): computed contrast from the real brush values.
    [Fact]
    public void FR118_AC093_error_brush_contrast_meets_thresholds()
    {
        var bg = Brush("TechnicalBackgroundBrush");
        var border = Brush("TechnicalBorderBrush");
        var text = Brush("TechnicalTextBrush");
        Assert.Equal((0xFD, 0xE7, 0xE9), bg);
        Assert.Equal((0xC5, 0x0F, 0x1F), border);
        Assert.Equal((0xA4, 0x26, 0x2C), text);
        Assert.True(Contrast(text, bg) >= 4.5);
        Assert.True(Contrast(border, bg) >= 3.0);
        Assert.True(Contrast(border, (255, 255, 255)) >= 3.0);
    }

    // FR-118 AC-094: no legacy blue anywhere; cancel keeps the blue accent; the error-colored text is not the border brush.
    [Fact]
    public void FR118_AC094_no_legacy_blue_and_cancel_uses_accent()
    {
        foreach (string file in Directory.GetFiles(AppDir(), "*.axaml", SearchOption.AllDirectories))
        {
            string xaml = File.ReadAllText(file);
            foreach (string blue in new[] { "#EAF5FF", "#62A9E8", "#174F7C" })
            {
                Assert.DoesNotContain(blue, xaml, StringComparison.OrdinalIgnoreCase);
            }
        }

        string main = File.ReadAllText(Path.Combine(AppDir(), "Views", "MainWindow.axaml"));
        Match cancel = Regex.Match(main, "Selector=\"Button\\.shell-action\\.cancel\">(.*?)</Style>", RegexOptions.Singleline);
        Assert.True(cancel.Success);
        Assert.DoesNotContain("Technical", cancel.Groups[1].Value);
        Assert.Contains("AppAccentSoftBrush", cancel.Groups[1].Value);
        Assert.Contains("AppAccentStrongBrush", cancel.Groups[1].Value);

        string results = File.ReadAllText(Path.Combine(AppDir(), "Views", "ResultsOutputView.axaml"));
        Assert.DoesNotContain("Foreground=\"{DynamicResource TechnicalBorderBrush}\"", results);
    }

    // FR-118 AC-095: error displays keep a non-color cue (thick border / bold / error wording).
    [Fact]
    public void FR118_AC095_error_displays_keep_non_color_cue()
    {
        string main = File.ReadAllText(Path.Combine(AppDir(), "Views", "MainWindow.axaml"));
        Assert.Matches("technical-validation\">(?:(?!</Style>).)*BorderThickness\" Value=\"2,0,0,0\"".Replace("(?:(?!</Style>).)*", "[\\s\\S]*?"), main);
        string prep = File.ReadAllText(Path.Combine(AppDir(), "Views", "ExecutionPreparationPanel.axaml"));
        Assert.Contains("エラー", prep);
    }
}

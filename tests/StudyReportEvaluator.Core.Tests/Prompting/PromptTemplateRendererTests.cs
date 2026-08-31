using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.Core.Tests.Prompting;

public sealed class PromptTemplateRendererTests
{
    private readonly PromptTemplateRenderer _renderer = new();
    private readonly PromptRenderContext _context = new(
        "QUESTION",
        "ANSWER",
        "SUPPORT",
        "CRITERIA",
        "1",
        "10");

    [Fact]
    public void Exactly_six_placeholders_are_allowed_and_rendered()
    {
        string result = _renderer.Render(
            "{設問}|{回答}|{補助情報}|{評価項目}|{最小点}|{最大点}",
            _context);

        Assert.Equal("QUESTION|ANSWER|SUPPORT|CRITERIA|1|10", result);
        Assert.Equal(6, PromptTemplateRenderer.Placeholders.Count);
    }

    [Fact]
    public void Literal_braces_are_escaped_without_counting_as_required_placeholders()
    {
        string result = _renderer.Render("{{literal}} {回答} {評価項目}", _context);

        Assert.Equal("{literal} ANSWER CRITERIA", result);
        PromptConfigurationException exception = Assert.Throws<PromptConfigurationException>(
            () => _renderer.Render("{{回答}} {評価項目}", _context));
        Assert.Equal("ANSWER_PLACEHOLDER_REQUIRED", exception.Code);
    }

    [Fact]
    public void Inserted_values_are_opaque_and_never_rescanned()
    {
        PromptRenderContext recursiveLike = _context with
        {
            Answer = "student text {設問} {{bad}} }",
            EvaluationCriteria = "criterion {回答}",
        };

        string result = _renderer.Render("A={回答}; C={評価項目}", recursiveLike);

        Assert.Equal("A=student text {設問} {{bad}} }; C=criterion {回答}", result);
    }

    [Theory]
    [InlineData("{未知} {回答} {評価項目}", "UNKNOWN_PLACEHOLDER")]
    [InlineData("{回答 {評価項目}", "MALFORMED_PLACEHOLDER")]
    [InlineData("{回答} {評価項目", "UNCLOSED_PLACEHOLDER")]
    [InlineData("{回答} {評価項目} }", "UNMATCHED_CLOSING_BRACE")]
    [InlineData("   ", "TEMPLATE_REQUIRED")]
    public void Unknown_unclosed_and_malformed_braces_fail_before_render(
        string template,
        string expectedCode)
    {
        PromptConfigurationException exception = Assert.Throws<PromptConfigurationException>(
            () => _renderer.Render(template, _context));

        Assert.Equal(expectedCode, exception.Code);
        Assert.DoesNotContain(template, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{評価項目}", "ANSWER_PLACEHOLDER_REQUIRED")]
    [InlineData("{回答}", "CRITERIA_PLACEHOLDER_REQUIRED")]
    public void Custom_required_placeholders_must_each_appear(string template, string expectedCode)
    {
        PromptConfigurationException exception = Assert.Throws<PromptConfigurationException>(
            () => _renderer.Render(template, _context));

        Assert.Equal(expectedCode, exception.Code);
    }
}

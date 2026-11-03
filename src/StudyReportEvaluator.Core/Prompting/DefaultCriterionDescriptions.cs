using StudyReportEvaluator.Core.Domain;

namespace StudyReportEvaluator.Core.Prompting;

public static class DefaultCriterionDescriptions
{
    public const string Knowledge =
        "学生が作成したレポートを高度に分析・解析をしてそれぞれの{論点}について説明がされているかどうかの評価を行ってください。\n"
        + "論点:\n"
        + "- xxx\n"
        + "- xxx\n"
        + "- xxx";

    public const string PromptAnalysis =
        "学生が作成したPromptを、Anthropic・OpenAI・Microsoftなどが公開するPrompt作成のベストプラクティスと照合して、高度に分析・評価を行ってください。\n"
        + "観点:\n"
        + "- 明確な役割と目的が示されているか\n"
        + "- 具体的で曖昧さのない指示になっているか\n"
        + "- 必要な文脈・背景情報が与えられているか\n"
        + "- 出力形式が指定されているか\n"
        + "- 例示（入出力の例）が適切に用いられているか\n"
        + "- 制約・禁止事項が明示されているか\n"
        + "- 段階的な思考や手順が促されているか\n"
        + "- 出力を検証できる基準が示されているか";

    public const string Custom =
        "（評価したい内容と判断の基準を、利用者が具体的に書き換えてください。）\n"
        + "評価の観点:\n"
        + "- xxx\n"
        + "- xxx\n"
        + "- xxx";

    public static string For(EvaluatorType type) => type switch
    {
        EvaluatorType.KnowledgeCoverage => Knowledge,
        EvaluatorType.PromptAnalysis => PromptAnalysis,
        EvaluatorType.CustomPrompt => Custom,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported evaluator type."),
    };
}

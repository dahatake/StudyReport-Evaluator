namespace StudyReportEvaluator.Core.Prompting;

public static class BuiltInPromptTemplates
{
    public const string KnowledgeTemplateVersion = "knowledge-v1";

    public const string KnowledgeSemanticTemplate = """
        次の設問への回答について、指定された各知識ポイントが回答内でどの程度説明されているかを評価してください。
        単語が存在するだけで満点にせず、内容上の説明、他の概念との関係、具体的な適用が確認できる程度を評価してください。
        各評価項目について指定range内の数値、短い理由、回答内の根拠を返してください。

        ### 設問
        {設問}

        ### 評価対象回答
        {回答}

        ### 補助情報
        {補助情報}

        ### 知識ポイントと評価項目
        {評価項目}
        """;

    public const string CustomPromptPreset = """
        次の設問に対して作成されたPromptを分析してください。
        評価項目ごとに、そのPromptが必要な視点を引き出せる具体性、論理性、実行可能性を指定range内で数値化してください。
        補助情報にPrompt作成時の工夫・観点・論点がある場合は、それを重要な評価情報として使い、実際のPromptへ反映されているかを評価してください。
        各評価項目について短い理由と、評価対象回答または補助情報内の根拠を返してください。

        ### 設問
        {設問}

        ### 評価対象Prompt
        {回答}

        ### 補助情報
        {補助情報}

        ### 評価項目
        {評価項目}
        """;

    public const string StructuredOutputInstruction = """
        [APP-OWNED STRUCTURED OUTPUT CONTRACT]
        Call the submit_quantification tool exactly once and do not substitute normal assistant text.
        Return only the requested evaluator ID and every expected criterion exactly once.
        For each criterion return only its raw score, a short reason, exact contiguous evidence, evidence source kind, and source column ID.
        Never return an evaluator score, question score, overall score, weight, pass/fail decision, or additional field.
        Evidence must come from the identified same-row primary or supporting source. If no evidence exists, return an empty evidence string, NONE, and an empty source column ID.
        """;

    public static string GetKnowledgeTemplate(string? version)
    {
        if (!string.Equals(version, KnowledgeTemplateVersion, StringComparison.Ordinal))
        {
            throw new PromptConfigurationException(
                "UNSUPPORTED_KNOWLEDGE_TEMPLATE_VERSION",
                "The configured Knowledge template version is not supported.");
        }

        return KnowledgeSemanticTemplate;
    }
}

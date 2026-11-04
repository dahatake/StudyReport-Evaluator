using System.Text.Json.Nodes;
using Xunit;

namespace StudyReportEvaluator.ControlEvaluation.Tests;

public sealed class ControlEvaluatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fr116-" + Guid.NewGuid().ToString("N"));

    public ControlEvaluatorTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    private const string Dataset = """
    {"dataset_version":"t1",
     "questions":[{"id":"Q1","group":"G1","text":"q","references":["the quick brown fox jumps"]},
                  {"id":"Q2","group":"G2","text":"q","references":["alpha beta gamma delta"]}],
     "items":[
      {"id":"A","question_id":"Q1","label":"自力","creation_method":"m","source_item_id":null,"relation":"独立","answer":"completely different words here"},
      {"id":"B","question_id":"Q1","label":"追加指示なし","creation_method":"m","source_item_id":null,"relation":"全文","answer":"the quick brown fox jumps"},
      {"id":"C","question_id":"Q2","label":"抜粋","creation_method":"m","source_item_id":null,"relation":"抜粋","answer":""},
      {"id":"D","question_id":"Q2","label":"編集","creation_method":"m","source_item_id":"C","relation":"編集","answer":"alpha beta gamma delta"}]}
    """;

    private string Write(string name, string text)
    {
        string p = Path.Combine(_dir, name);
        File.WriteAllText(p, text);
        return p;
    }

    private ControlEvaluator.Result Run(string split, string rules) =>
        ControlEvaluator.Evaluate(Write("d.json", Dataset), Write("s.json", split), Write("r.json", rules));

    private const string Split = """{"tuning_groups":["G1"],"holdout_groups":["G2"],"seed":1,"order":["G1","G2"]}""";
    private const string Rules = """{"tuned_on_groups":["G1"],"rules":[{"id":"R1","metric":"student_coverage","threshold":0.8},{"id":"RX","metric":"student_coverage"}]}""";

    // FR-116 AC-088: 正検知・正除外・保留（欠損回答）・分母なしの率は未算出
    [Fact]
    public void Ac088_CountsHoldMissingAndUndefinedRates()
    {
        ControlEvaluator.Result r = Run(Split, Rules);
        Assert.True(r.Valid);
        JsonNode overall = r.Record["counts"]!["R1"]!["overall"]!;
        Assert.Equal(2, (int)overall["tp"]!);   // B, D
        Assert.Equal(1, (int)overall["tn"]!);   // A
        Assert.Equal(1, (int)overall["hold"]!); // C（空）
        JsonNode holdout = r.Record["counts"]!["R1"]!["by_partition"]!["holdout"]!;
        Assert.Null(holdout["fpr"]);            // 陰性なし → 分母 0
        JsonNode none = r.Record["counts"]!["RX"]!["overall"]!;
        Assert.Equal(4, (int)none["hold"]!);    // 閾値なしは保留
        Assert.Null(none["tpr"]);
    }

    // FR-116 AC-087: holdout を調整に使う・空 holdout・別群の元回答は評価不成立
    [Fact]
    public void Ac087_InvalidSplitsAreNotEvaluated()
    {
        Assert.False(Run("""{"tuning_groups":["G1","G2"],"holdout_groups":[],"seed":1,"order":[]}""", Rules).Valid);
        Assert.False(Run(Split, Rules.Replace("[\"G1\"]", "[\"G1\",\"G2\"]")).Valid);
        Assert.False(Run("""{"tuning_groups":["G1","G2"],"holdout_groups":["G2"],"seed":1,"order":[]}""", Rules).Valid);
    }

    // FR-116 AC-089: 同じ入力で同じ結果
    [Fact]
    public void Ac089_RerunIsReproducible()
    {
        JsonObject a = Run(Split, Rules).Record;
        JsonObject b = Run(Split, Rules).Record;
        a.Remove("generated_at");
        b.Remove("generated_at");
        Assert.Equal(a.ToJsonString(), b.ToJsonString());
    }

    // FR-116 AC-090: 限界の明記
    [Fact]
    public void Ac090_LimitationsStated()
    {
        string text = (string)Run(Split, Rules).Record["limitations"]!;
        Assert.Contains("実学生について未検証", text);
        Assert.Contains("合成対照データでの評価", text);
    }
}

using System.Text.Encodings.Web;
using System.Text.Json;
using StudyReportEvaluator.ControlEvaluation;

// FR-116: dotnet run --project src/StudyReportEvaluator.ControlEvaluation -- --dataset d.json --split s.json --rules r.json --output dir
Dictionary<string, string> options = new(StringComparer.Ordinal);
for (int i = 0; i + 1 < args.Length; i += 2)
{
    options[args[i]] = args[i + 1];
}

foreach (string required in new[] { "--dataset", "--split", "--rules", "--output" })
{
    if (!options.ContainsKey(required))
    {
        Console.Error.WriteLine($"必須の引数がありません: {required}");
        return 2;
    }
}

ControlEvaluator.Result result = ControlEvaluator.Evaluate(options["--dataset"], options["--split"], options["--rules"]);
Directory.CreateDirectory(options["--output"]);
string json = result.Record.ToJsonString(new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
});
File.WriteAllText(Path.Combine(options["--output"], "evaluation-record.json"), json, new System.Text.UTF8Encoding(false));
Console.WriteLine(result.Valid ? "評価済み" : "評価不成立");
return result.Valid ? 0 : 1;

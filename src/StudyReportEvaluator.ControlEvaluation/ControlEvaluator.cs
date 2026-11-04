using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudyReportEvaluator.Core.Similarity;

namespace StudyReportEvaluator.ControlEvaluation;

/// <summary>
/// FR-116: ラベル付きローカル対照データの再現評価（保守者向けのオフライン評価専用）。
/// 製品の採点経路・UI には結びつけない。未校正の判定閾値・確率・不正判定は持たない。
/// 入力は読むだけで変更しない。実 AI・credential・ネットワーク・課金 API は使わない。
/// </summary>
public static class ControlEvaluator
{
    public const string StatusEvaluated = "評価済み";
    public const string StatusInvalid = "評価不成立";
    public const string LegacyVersion = "legacy-single-reference-composite (FR-114 以前の単独参照合成値)";
    public const string ImprovedVersion = "improved-surface-diagnostics (FR-114 の全文／部分／被覆率)";

    private static readonly string[] Labels = ["追加指示なし", "追加指示あり", "抜粋", "編集", "自力"];
    private static readonly string[] PositiveLabels = ["追加指示なし", "追加指示あり", "抜粋", "編集"];
    private static readonly string[] Metrics = ["legacy_composite", "student_coverage", "reference_coverage", "normalized_exact"];

    public const string Limitations =
        "本評価は合成対照データでの評価であり、実学生について未検証です。"
        + "ラベルは作成者が定めた対照条件であり、AI 使用の実証ではありません。"
        + "限界: (1) 実学生の履歴が存在しないため、学生ごとの傾向を評価していません。"
        + "(2) 参照回答の生成は非決定であり、別の参照集合では結果が変わり得ます。"
        + "(3) 短文の共通正答は、独立に書いても一致して要確認になり得ます（誤検知）。"
        + "(4) 追加指示・編集・抜粋により表面の一致が下がると、要確認にならず見逃しになり得ます。"
        + "(5) 結果は設問・model 条件に依存し、他の設問や model へ一般化できません。"
        + "holdout の結果が調整群より悪い場合も隠さず、そのまま報告します。"
        + "合成評価の成功を、実学生の識別性能・確率・不正の証明へ転用しません。";

    public sealed record Result(JsonObject Record, bool Valid);

    public static Result Evaluate(string datasetPath, string splitPath, string rulesPath)
    {
        List<string> invalid = [];
        byte[] datasetBytes = ReadBytes(datasetPath, "dataset", invalid);
        byte[] splitBytes = ReadBytes(splitPath, "split", invalid);
        byte[] rulesBytes = ReadBytes(rulesPath, "rules", invalid);
        JsonNode? dataset = Parse(datasetBytes, "dataset", invalid);
        JsonNode? split = Parse(splitBytes, "split", invalid);
        JsonNode? rules = Parse(rulesBytes, "rules", invalid);

        List<string> tuning = Strings(split?["tuning_groups"]);
        List<string> holdout = Strings(split?["holdout_groups"]);
        List<string> tunedOn = Strings(rules?["tuned_on_groups"]);
        List<string> order = Strings(split?["order"]);

        // 設問・回答の読み取り
        Dictionary<string, string> groupOf = new(StringComparer.Ordinal);
        Dictionary<string, string?> reference = new(StringComparer.Ordinal);
        foreach (JsonNode? q in Array(dataset?["questions"]))
        {
            string? id = Str(q?["id"]);
            if (id is null)
            {
                invalid.Add("設問に id がない");
                continue;
            }

            if (!groupOf.TryAdd(id, Str(q?["group"]) ?? string.Empty))
            {
                invalid.Add($"設問 id が重複している: {id}");
                continue;
            }

            reference[id] = Array(q?["references"]).Select(Str).FirstOrDefault();
        }

        List<JsonNode?> itemNodes = Array(dataset?["items"]).ToList();
        Dictionary<string, string> partitionOfItem = new(StringComparer.Ordinal);
        List<ItemRow> rows = [];
        foreach (JsonNode? n in itemNodes)
        {
            string? id = Str(n?["id"]);
            string? qid = Str(n?["question_id"]);
            if (id is null || qid is null || !groupOf.ContainsKey(qid))
            {
                invalid.Add($"回答の id または設問が不正: {id}");
                continue;
            }

            string group = groupOf[qid];
            string partition = holdout.Contains(group) ? "holdout" : tuning.Contains(group) ? "tuning" : string.Empty;
            if (partition.Length == 0)
            {
                invalid.Add($"グループ {group} が調整群にも holdout にも属さない");
            }

            if (!partitionOfItem.TryAdd(id, partition))
            {
                invalid.Add($"回答 id が重複している: {id}");
                continue;
            }

            string label = Str(n?["label"]) ?? string.Empty;
            if (!Labels.Contains(label))
            {
                invalid.Add($"回答 {id} のラベルが不正: {label}");
            }

            rows.Add(new ItemRow(id, qid, group, partition, label, n!));
        }

        // 分割の妥当性（AC-087）
        if (holdout.Count == 0)
        {
            invalid.Add("holdout が空");
        }

        foreach (string g in holdout.Intersect(tuning))
        {
            invalid.Add($"グループ {g} が調整群と holdout の両方にある");
        }

        foreach (string g in tunedOn.Intersect(holdout))
        {
            invalid.Add($"holdout のグループ {g} でルールを調整している");
        }

        foreach (ItemRow row in rows)
        {
            string? source = Str(row.Node["source_item_id"]);
            if (!string.IsNullOrEmpty(source))
            {
                if (!partitionOfItem.TryGetValue(source, out string? sp))
                {
                    invalid.Add($"回答 {row.Id} の元回答 {source} が存在しない");
                }
                else if (sp != row.Partition)
                {
                    invalid.Add($"回答 {row.Id} の元回答 {source} が別の群にある（漏れ）");
                }
            }
        }

        bool valid = invalid.Count == 0;
        JsonObject record = new()
        {
            ["status"] = valid ? StatusEvaluated : StatusInvalid,
            ["invalid_reasons"] = new JsonArray(invalid.Distinct().Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()),
            ["generated_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["dataset_version"] = Str(dataset?["dataset_version"]),
            ["dataset_sha256"] = Sha(datasetBytes),
            ["split_sha256"] = Sha(splitBytes),
            ["rules_sha256"] = Sha(rulesBytes),
            ["split"] = new JsonObject
            {
                ["tuning_groups"] = Clone(split?["tuning_groups"]) ?? new JsonArray(),
                ["holdout_groups"] = Clone(split?["holdout_groups"]) ?? new JsonArray(),
                ["seed"] = Clone(split?["seed"]),
                ["order"] = Clone(split?["order"]) ?? new JsonArray(),
            },
            ["conditions"] = new JsonObject
            {
                ["offline"] = true,
                ["external_calls"] = "なし（実 AI・credential・ネットワーク・外部書込・課金 API を使わない）",
                ["reference_selection"] = "設問の references の先頭（単独参照）",
                ["normalization"] = "NFKC → 小文字化 → 空白・句読点・記号を除く",
                ["coverage"] = "最長共通部分文字列の長さ ÷ 正規化後の長さ（4 桁丸め）",
                ["rule_judgement"] = "値 ≥ threshold で 要確認、未満で 非該当、欠損・不正は 保留",
                ["expected_mapping"] = "追加指示なし／追加指示あり／抜粋／編集 = 対照陽性、自力 = 対照陰性",
                ["scope"] = "保守者向けのオフライン評価専用。製品の採点・判定には使わない",
            },
            ["comparison_versions"] = new JsonObject { ["legacy"] = LegacyVersion, ["improved"] = ImprovedVersion },
            ["rules"] = Clone(rules?["rules"]) ?? new JsonArray(),
            ["tuned_on_groups"] = Clone(rules?["tuned_on_groups"]) ?? new JsonArray(),
            ["items"] = new JsonArray(),
            ["counts"] = new JsonObject(),
            ["limitations"] = Limitations,
        };

        if (!valid)
        {
            return new Result(record, false);
        }

        // 固定順序: split.order に載るグループを先に、残りは入力順。
        List<ItemRow> ordered = rows
            .Select((r, i) => (r, i))
            .OrderBy(t => { int k = order.IndexOf(t.r.Group); return k < 0 ? int.MaxValue : k; })
            .ThenBy(t => t.i)
            .Select(t => t.r)
            .ToList();

        SurfaceTextSimilarityCalculator calculator = new();
        List<RuleDef> ruleDefs = Array(rules?["rules"])
            .Select(r => new RuleDef(Str(r?["id"]), Str(r?["metric"]), Number(r?["threshold"])))
            .Where(r => r.Id is not null)
            .ToList();

        JsonArray itemOut = (JsonArray)record["items"]!;
        List<Classified> classified = [];
        foreach (ItemRow row in ordered)
        {
            string? answer = Str(row.Node["answer"]);
            string? refText = reference.GetValueOrDefault(row.QuestionId);
            Dictionary<string, double?> values = [];
            JsonNode? metrics = null;
            if (!string.IsNullOrWhiteSpace(answer) && !string.IsNullOrEmpty(refText))
            {
                SurfaceSimilarityResult r = calculator.Calculate(answer, refText);
                if (!r.IsEmpty)
                {
                    values["legacy_composite"] = (double)r.Score;
                    values["student_coverage"] = (double)r.StudentCoverage;
                    values["reference_coverage"] = (double)r.ReferenceCoverage;
                    values["normalized_exact"] = r.IsNormalizedExactMatch ? 1 : 0;
                    metrics = new JsonObject
                    {
                        ["legacy_composite"] = r.Score,
                        ["raw_exact"] = r.IsRawExactMatch,
                        ["normalized_exact"] = r.IsNormalizedExactMatch,
                        ["student_coverage"] = r.StudentCoverage,
                        ["reference_coverage"] = r.ReferenceCoverage,
                    };
                }
            }

            string expected = PositiveLabels.Contains(row.Label) ? "対照陽性" : "対照陰性";
            JsonObject cls = new();
            Dictionary<string, string> clsMap = [];
            foreach (RuleDef rule in ruleDefs)
            {
                string c = Classify(rule, values);
                cls[rule.Id!] = c;
                clsMap[rule.Id!] = c;
            }

            classified.Add(new Classified(row, expected, clsMap));
            itemOut.Add(new JsonObject
            {
                ["id"] = row.Id,
                ["question_id"] = row.QuestionId,
                ["label"] = row.Label,
                ["creation_method"] = Str(row.Node["creation_method"]),
                ["relation"] = Str(row.Node["relation"]),
                ["source_item_id"] = Str(row.Node["source_item_id"]),
                ["group"] = row.Group,
                ["partition"] = row.Partition,
                ["expected"] = expected,
                ["metrics"] = metrics,
                ["classifications"] = cls,
            });
        }

        JsonObject counts = (JsonObject)record["counts"]!;
        foreach (RuleDef rule in ruleDefs)
        {
            JsonObject byLabel = new();
            foreach (string l in Labels)
            {
                byLabel[l] = Tally(classified.Where(c => c.Row.Label == l), rule.Id!);
            }

            counts[rule.Id!] = new JsonObject
            {
                ["overall"] = Tally(classified, rule.Id!),
                ["by_label"] = byLabel,
                ["by_partition"] = new JsonObject
                {
                    ["tuning"] = Tally(classified.Where(c => c.Row.Partition == "tuning"), rule.Id!),
                    ["holdout"] = Tally(classified.Where(c => c.Row.Partition == "holdout"), rule.Id!),
                },
            };
        }

        return new Result(record, true);
    }

    private static string Classify(RuleDef rule, Dictionary<string, double?> values)
    {
        if (rule.Metric is null || !Metrics.Contains(rule.Metric) || rule.Threshold is null
            || !values.TryGetValue(rule.Metric, out double? v) || v is null)
        {
            return "保留";
        }

        return v.Value >= rule.Threshold.Value ? "要確認" : "非該当";
    }

    private static JsonObject Tally(IEnumerable<Classified> source, string ruleId)
    {
        int tp = 0, fp = 0, tn = 0, fn = 0, hold = 0, n = 0;
        foreach (Classified c in source)
        {
            n++;
            string cls = c.Classes[ruleId];
            bool positive = c.Expected == "対照陽性";
            if (cls == "保留")
            {
                hold++;
            }
            else if (positive)
            {
                if (cls == "要確認") tp++; else fn++;
            }
            else
            {
                if (cls == "要確認") fp++; else tn++;
            }
        }

        return new JsonObject
        {
            ["tp"] = tp,
            ["fp"] = fp,
            ["tn"] = tn,
            ["fn"] = fn,
            ["hold"] = hold,
            ["n"] = n,
            ["tpr"] = tp + fn == 0 ? null : (double)tp / (tp + fn),
            ["fpr"] = fp + tn == 0 ? null : (double)fp / (fp + tn),
        };
    }

    private static byte[] ReadBytes(string path, string name, List<string> invalid)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            invalid.Add($"{name} を読めない: {path}");
            return [];
        }
    }

    private static JsonNode? Parse(byte[] bytes, string name, List<string> invalid)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(bytes);
        }
        catch (JsonException)
        {
            invalid.Add($"{name} が JSON として不正");
            return null;
        }
    }

    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static JsonNode? Clone(JsonNode? node) => node?.DeepClone();

    private static IEnumerable<JsonNode?> Array(JsonNode? node) => node is JsonArray a ? a : [];

    private static List<string> Strings(JsonNode? node) => Array(node).Select(Str).OfType<string>().ToList();

    private static string? Str(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue(out string? s) ? s : null;

    private static double? Number(JsonNode? node)
    {
        if (node is JsonValue v && v.TryGetValue(out JsonElement e) && e.ValueKind == JsonValueKind.Number)
        {
            return e.GetDouble();
        }

        return node is JsonValue v2 && v2.TryGetValue(out double d) ? d : null;
    }

    private sealed record ItemRow(string Id, string QuestionId, string Group, string Partition, string Label, JsonNode Node);

    private sealed record RuleDef(string? Id, string? Metric, double? Threshold);

    private sealed record Classified(ItemRow Row, string Expected, Dictionary<string, string> Classes);
}

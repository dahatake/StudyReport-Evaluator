using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using GitHub.Copilot;
using Microsoft.Extensions.AI;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.Workbooks.Checkpoint;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.SystemTests.Integration;

/// <summary>
/// FR-120 AC-007 の System Test（integration）。通常評価のevaluator IDは要求に束縛されたアプリの値であり、
/// モデル向けの送信契約（Prompt・結果schema）はその生成・返却を求めない。
/// 期待値は要求定義書の受入基準（FR-120 AC-007、§7.6 の schema 不正は新 session で最大 1 回再試行）だけから決める。
/// 実装は、合成 run の起動・ローカルの応答代替（Copilot transport の代替）の差し込み・完成 workbook と経過ログを読むためにだけ使う。
/// 実 AI・実 CLI・実 credential・外部送信は使わない（合成データのみ）。
///
/// テストが前提にする観測点:
///   - モデルへ送る Prompt は MessageOptions.Prompt、結果 schema は SessionConfig.Tools の唯一の AIFunction.JsonSchema。
///   - 結果は完成 workbook の `Quantification_Results`（SourceRow 列で行を特定、`{設問}.{evaluator}.{criterion}.AI_Raw／Status`）。
///   - 技術ログ（`*-log.txt`）の失敗行は `row=&lt;source row&gt;` で対象行を示す。
/// </summary>
public sealed partial class RequestBoundEvaluatorIdTests
{
    private const string ResultsSheet = "Quantification_Results";

    // evaluator ID・criterion ID は Prompt・schema 内の偶然の一致を避けるため互いに部分文字列にならない値にする。
    private const string KnowledgeEvaluatorId = "EK7Q";
    private const string CustomEvaluatorId = "EX3M";
    private const string KnowledgeCriterionId = "CRK1";
    private const string CustomCriterionId = "CRX1";
    private const string UnknownEvaluatorId = "ZZUNKNOWN9";

    private const int RowA = 271; // source row 271 ／ record ID 271
    private const int RowB = 389; // source row 389 ／ record ID 389

    // 2 行に同じ合成回答を持たせる（回帰条件）。根拠はこの本文の連続部分文字列。
    private const string SharedAnswer = "Synthetic answer: recursion calls itself until the base case stops it.";
    private const string EvidenceQuote = "the base case stops it";

    // 応答代替が返す raw（行・evaluator ごとに異なる値にして帰属を判定する）。
    private static readonly Dictionary<(int Record, string Evaluator), decimal> Scores = new()
    {
        [(RowA, CustomEvaluatorId)] = 3m,
        [(RowB, KnowledgeEvaluatorId)] = 6m,
        [(RowB, CustomEvaluatorId)] = 8m,
    };

    [GeneratedRegex(@"^\$?([A-Z]+)\$?(\d+)$")]
    private static partial Regex CellRef();

    [GeneratedRegex(@"(?i)evaluator[\s_\-]*id")]
    private static partial Regex EvaluatorIdTerm();

    // ------------------------------------------------------------------
    // FR-120 AC-007: 通常 Knowledge／Custom 評価を動的に構成し、送信から結果受理までを合成データとローカルの応答代替で通す。
    //   (1) Prompt に evaluator ID の生成・返却指示がなく、結果 schema にその欄がない。
    //   (2) evaluator ID を省略した応答は、送信時の回答行・設問・evaluator へ帰属して受理される（交互に処理しても前の ID を流用しない）。
    //   (3) 271 と 389 に同じ回答。271 の Knowledge にだけ明示的不一致の ID を初回・schema retry で返す
    //       → 271 の Knowledge は AI_OUTPUT_INVALID（2 回の session）、点数は blank、ログの障害は row=271 だけ。389 は正常に受理。
    //   (4) 互換受信: 一致する明示 ID は受理、未知・別 evaluator の ID は EVALUATOR_ID_MISMATCH で拒否（補正しない）、
    //       不正な型・空値・未知／別の criterion ID・根拠列・引用の不正も拒否。
    //   (5) 入力 workbook は変更しない。
    // ------------------------------------------------------------------
    [Fact]
    public async Task FR120_AC007_request_bound_evaluator_id_is_not_requested_from_model_and_results_bind_to_the_sent_request()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDir dir = new();
        string input = Path.Combine(dir.Path, "input.xlsx");
        string output = Path.Combine(dir.Path, "out");
        Directory.CreateDirectory(output);
        CreateInput(input);
        string inputHashBefore = Sha(input);

        LocalResponderFactory transports = new();
        RecordingRunner runner = new(new EphemeralEvaluationRunnerAdapter(new EphemeralEvaluationRunner(transports)));
        WorkbookMetadata metadata = new WorkbookMetadataReader().Read(input);
        DurableQuantificationOrchestrator orchestrator = new(
            new OpenXmlEvaluationRowSource(input),
            runner,
            new ReferenceRunner(),
            new SpecialRunner());
        RunSummary summary = await orchestrator.RunAsync(
            new DurableQuantificationRunRequest
            {
                Run = new QuantificationRunRequest
                {
                    DraftDefinition = Definition(),
                    WorkbookMetadata = metadata,
                    InputPath = input,
                    ModelId = "model-test",
                    MaximumPromptTokens = 64_000,
                    MaximumContextWindowTokens = 128_000,
                    MaxConcurrency = 1,
                    UseDurableWorkflow = true,
                },
                Runtime = new CheckpointRuntimeIdentity
                {
                    ApplicationIdentity = "StudyReportEvaluator.App/4.0.0",
                    CliVersion = "1.0.79",
                    CliSha256 = new string('A', 64),
                    SdkInformationalVersion = "1.0.11",
                },
                OutputDirectory = output,
            },
            cancellationToken: ct);

        // 全行 × 両 evaluator が送信された（空行は送らない）。
        RequestRecord[] requests = [.. runner.Requests];
        Assert.Equal(4, requests.Length);
        Assert.All(requests, r => Assert.Contains(r.RecordId, new[] { RowA, RowB }));
        Assert.Equal(
            new[] { KnowledgeEvaluatorId, CustomEvaluatorId }.Order(StringComparer.Ordinal).ToArray(),
            requests.Select(r => r.Payload.EvaluatorId).Distinct().Order(StringComparer.Ordinal).ToArray());

        // (1) モデル向けの送信契約に evaluator ID の生成・返却がない（全 attempt の Prompt・schema）。
        foreach (RequestRecord request in requests)
        {
            Assert.NotEmpty(request.Attempts);
            foreach (AttemptRecord attempt in request.Attempts)
            {
                Assert.False(EvaluatorIdTerm().IsMatch(attempt.Prompt), $"Prompt に evaluator ID の指示がある（{request.Payload.EvaluatorId}）");
                Assert.DoesNotContain(KnowledgeEvaluatorId, attempt.Prompt, StringComparison.Ordinal);
                Assert.DoesNotContain(CustomEvaluatorId, attempt.Prompt, StringComparison.Ordinal);
                Assert.Empty(PropertyNamesMatching(attempt.Schema, name => name.Contains("evaluator", StringComparison.OrdinalIgnoreCase)));
                string schemaText = attempt.Schema.GetRawText();
                Assert.DoesNotContain(KnowledgeEvaluatorId, schemaText, StringComparison.Ordinal);
                Assert.DoesNotContain(CustomEvaluatorId, schemaText, StringComparison.Ordinal);
            }
        }

        // (2) 省略応答は送信時の要求へ束縛される。異なる evaluator の要求を交互に処理しても前の ID を流用しない。
        Assert.Contains(
            Enumerable.Range(1, requests.Length - 1),
            i => requests[i].Payload.EvaluatorId != requests[i - 1].Payload.EvaluatorId);
        foreach (RequestRecord request in requests.Where(r => !(r.RecordId == RowA && r.Payload.EvaluatorId == KnowledgeEvaluatorId)))
        {
            EvaluationRunnerResult result = Assert.IsType<EvaluationRunnerResult>(request.Result);
            Assert.Equal(ResultsStatusCodes.Success, result.StatusCode);
            QuantificationResult accepted = Assert.IsType<QuantificationResult>(result.AcceptedResult);
            Assert.Equal(request.Payload.EvaluatorId, accepted.EvaluatorId);
            CriterionQuantificationResult criterion = Assert.Single(accepted.Criteria);
            Assert.Equal(Scores[(request.RecordId, request.Payload.EvaluatorId)], criterion.RawScore);
            Assert.All(request.Attempts, a => Assert.False(a.Arguments.TryGetProperty("EvaluatorId", out _)));
        }

        // (3) 271 の Knowledge: 明示的不一致を初回・schema retry に返す → 新 session で 1 回だけ再試行して AI_OUTPUT_INVALID。
        RequestRecord mismatched = Assert.Single(requests, r => r.RecordId == RowA && r.Payload.EvaluatorId == KnowledgeEvaluatorId);
        Assert.Equal(2, mismatched.Attempts.Count);
        Assert.Equal(2, mismatched.Attempts.Select(a => a.SessionId).Distinct(StringComparer.Ordinal).Count());
        EvaluationRunnerResult failed = Assert.IsType<EvaluationRunnerResult>(mismatched.Result);
        Assert.Equal(ResultsStatusCodes.AiOutputInvalid, failed.StatusCode);
        Assert.Null(failed.AcceptedResult);

        // 完成 workbook: 結果は送信時の行・evaluator に帰属し、技術的失敗の点数は blank のまま。
        string finalPath = Assert.IsType<string>(summary.FinalPath);
        Dictionary<int, Dictionary<string, string?>> rows = ReadResults(finalPath);
        Assert.True(rows.ContainsKey(RowA) && rows.ContainsKey(RowB), "Results に 271・389 の行がない");
        string kRaw = $"Q1.{KnowledgeEvaluatorId}.{KnowledgeCriterionId}.{ResultsSheetWriter.AiRawSuffix}";
        string kStatus = $"Q1.{KnowledgeEvaluatorId}.{KnowledgeCriterionId}.{ResultsSheetWriter.StatusSuffix}";
        string xRaw = $"Q1.{CustomEvaluatorId}.{CustomCriterionId}.{ResultsSheetWriter.AiRawSuffix}";
        string xStatus = $"Q1.{CustomEvaluatorId}.{CustomCriterionId}.{ResultsSheetWriter.StatusSuffix}";

        Assert.Equal(ResultsStatusCodes.Success, Cell(rows, RowB, kStatus));
        Assert.Equal(6m, Dec(Cell(rows, RowB, kRaw)));
        Assert.Equal(ResultsStatusCodes.Success, Cell(rows, RowB, xStatus));
        Assert.Equal(8m, Dec(Cell(rows, RowB, xRaw)));
        Assert.False(string.IsNullOrEmpty(Cell(rows, RowB, "Q1.Question_Normalized")), "389 の通常評価率が blank（271 の障害を 389 へ帰属した）");
        Assert.False(string.IsNullOrEmpty(Cell(rows, RowB, ResultsSheetWriter.FinalScoreHeader)), "389 の総合点が blank");

        Assert.Equal(ResultsStatusCodes.AiOutputInvalid, Cell(rows, RowA, kStatus));
        Assert.True(string.IsNullOrEmpty(Cell(rows, RowA, kRaw)), "271 の技術的失敗の raw は blank");
        Assert.True(string.IsNullOrEmpty(Cell(rows, RowA, "Q1.Question_Normalized")), "271 の技術的失敗を 0 などに変換してはならない");
        Assert.True(string.IsNullOrEmpty(Cell(rows, RowA, ResultsSheetWriter.FinalScoreHeader)), "271 の総合点は blank");
        Assert.Equal(ResultsStatusCodes.Success, Cell(rows, RowA, xStatus));
        Assert.Equal(3m, Dec(Cell(rows, RowA, xRaw)));

        // 技術ログ: 不一致の障害証跡は 271 だけを指し、389 の障害とみなさない。
        string logPath = Assert.Single(Directory.GetFiles(output, "*-log.txt"));
        string[] logLines = await File.ReadAllLinesAsync(logPath, Encoding.UTF8, ct);
        string[] failureLines = logLines
            .Where(l => l.Contains("EVALUATOR_ID_MISMATCH", StringComparison.Ordinal)
                || l.Contains(ResultsStatusCodes.AiOutputInvalid, StringComparison.Ordinal))
            .ToArray();
        Assert.Contains(failureLines, l => l.Contains("EVALUATOR_ID_MISMATCH", StringComparison.Ordinal) && HasRow(l, RowA));
        Assert.All(failureLines, l => Assert.False(HasRow(l, RowB), "389 の障害として記録された: " + l));
        Assert.DoesNotContain(SharedAnswer, string.Join('\n', logLines), StringComparison.Ordinal);

        // (5) 入力は変更しない。
        Assert.Equal(inputHashBefore, Sha(input));

        // (4) 互換受信（同じ送信経路・応答代替で、明示の値を返す）。
        SafeEvaluationPayload k = Assert.Single(requests, r => r.RecordId == RowB && r.Payload.EvaluatorId == KnowledgeEvaluatorId).Payload;
        SafeEvaluationPayload x = Assert.Single(requests, r => r.RecordId == RowB && r.Payload.EvaluatorId == CustomEvaluatorId).Payload;

        EvaluationRunnerResult matched = await runner.EvaluateScriptedAsync(k, p => Response(p, 6m, evaluatorId: JsonValue.Create(KnowledgeEvaluatorId)), ct);
        Assert.Equal(ResultsStatusCodes.Success, matched.StatusCode);
        Assert.Equal(KnowledgeEvaluatorId, Assert.IsType<QuantificationResult>(matched.AcceptedResult).EvaluatorId);

        (string Label, SafeEvaluationPayload Payload, Func<SafeEvaluationPayload, string> Script)[] rejected =
        [
            ("未知の evaluator ID", k, p => Response(p, 6m, evaluatorId: JsonValue.Create(UnknownEvaluatorId))),
            ("別 evaluator の ID", k, p => Response(p, 6m, evaluatorId: JsonValue.Create(CustomEvaluatorId))),
            ("別 evaluator の ID（Custom 側）", x, p => Response(p, 8m, evaluatorId: JsonValue.Create(KnowledgeEvaluatorId))),
            ("evaluator ID の型が数値", k, p => Response(p, 6m, evaluatorId: JsonValue.Create(123))),
            ("evaluator ID が空", k, p => Response(p, 6m, evaluatorId: JsonValue.Create(string.Empty))),
            ("evaluator ID が null", k, p => Response(p, 6m, evaluatorId: null, includeNullEvaluatorId: true)),
            ("未知の criterion ID", k, p => Response(p, 6m, criterionId: "CRK9")),
            ("別 evaluator の criterion ID", k, p => Response(p, 6m, criterionId: CustomCriterionId)),
            ("RawScore の型が文字列", k, p => Response(p, 6m, rawScoreAsString: true)),
            ("送っていない根拠列", k, p => Response(p, 6m, sourceColumnId: "Z")),
            ("回答にない引用", k, p => Response(p, 6m, evidence: "a quote that is not in the answer")),
        ];
        foreach ((string label, SafeEvaluationPayload payload, Func<SafeEvaluationPayload, string> script) in rejected)
        {
            EvaluationRunnerResult r = await runner.EvaluateScriptedAsync(payload, script, ct);
            Assert.True(r.StatusCode == ResultsStatusCodes.AiOutputInvalid, $"{label}: {r.StatusCode}（補正して受理してはならない）");
            Assert.Null(r.AcceptedResult);
            Assert.Equal(2, r.AttemptCount);
        }

        // 明示的不一致の拒否理由は EVALUATOR_ID_MISMATCH（受理側の検証。補正して受理しない）。
        foreach ((SafeEvaluationPayload payload, string wrongId) in new[] { (k, UnknownEvaluatorId), (k, CustomEvaluatorId), (x, KnowledgeEvaluatorId) })
        {
            SubmitQuantificationTool tool = new(payload);
            ToolInvocation invocation = new()
            {
                SessionId = "st-fr120-" + Guid.NewGuid().ToString("N"),
                ToolCallId = Guid.NewGuid().ToString("N"),
                ToolName = EvaluationSchemaFactory.ToolName,
                Arguments = JsonDocument.Parse(Response(payload, payload.EvaluatorId == KnowledgeEvaluatorId ? 6m : 8m, evaluatorId: JsonValue.Create(wrongId))).RootElement.Clone(),
            };
            string ack = await tool.InvokeAsync(invocation, ct);
            SubmitQuantificationOutcome outcome = tool.GetOutcome();
            Assert.NotEqual(SubmitQuantificationStatus.Accepted, outcome.Status);
            Assert.False(tool.TryGetAcceptedResult(out _));
            Assert.NotEqual(SubmitQuantificationTool.AcceptedAcknowledgement, ack);
            Assert.Contains("EVALUATOR_ID_MISMATCH", outcome.ErrorCodes);
        }
    }

    // ------------------------------------------------------------------
    // 応答代替（モデルの代わり）。行は送信された補助列の record ID から判断する（回答本文は 2 行で同じ）。
    // 271 の Knowledge にだけ別 evaluator の ID を明示し、それ以外は evaluator ID を省略する。
    // ------------------------------------------------------------------
    private static string DefaultResponse(SafeEvaluationPayload payload, int recordId)
    {
        if (recordId == RowA && payload.EvaluatorId == KnowledgeEvaluatorId)
        {
            return Response(payload, 5m, evaluatorId: JsonValue.Create(CustomEvaluatorId));
        }

        return Response(payload, Scores[(recordId, payload.EvaluatorId)]);
    }

    private static string Response(
        SafeEvaluationPayload payload,
        decimal rawScore,
        JsonNode? evaluatorId = null,
        bool includeNullEvaluatorId = false,
        string? criterionId = null,
        bool rawScoreAsString = false,
        string? sourceColumnId = null,
        string? evidence = null)
    {
        JsonArray criteria = [];
        foreach (ExpectedCriterion expected in payload.ExpectedCriteria)
        {
            criteria.Add(new JsonObject
            {
                ["CriterionId"] = criterionId ?? expected.CriterionId,
                ["RawScore"] = rawScoreAsString
                    ? JsonValue.Create(rawScore.ToString(CultureInfo.InvariantCulture))
                    : JsonValue.Create(rawScore),
                ["Reason"] = "synthetic short reason",
                ["Evidence"] = evidence ?? EvidenceQuote,
                ["EvidenceSource"] = "PRIMARY_ANSWER",
                ["EvidenceSourceColumnId"] = sourceColumnId ?? payload.PrimarySource.SourceColumnId,
            });
        }

        JsonObject root = [];
        if (evaluatorId is not null || includeNullEvaluatorId)
        {
            root["EvaluatorId"] = evaluatorId;
        }

        root["Criteria"] = criteria;
        return root.ToJsonString();
    }

    private static int RecordIdOf(SafeEvaluationPayload payload)
    {
        EvaluationSourceCell record = Assert.Single(payload.SupportingSources, s => s.SourceColumnId == "B");
        return int.Parse(record.Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static QuantificationDefinition Definition() =>
        new()
        {
            Id = "DEF-ST120",
            Name = "FR-120 system-test definition",
            Revision = "1",
            SourceSheet = "Original",
            HeaderRow = 1,
            FirstDataRow = RowA,
            LastDataRow = RowB,
            BasePoints = 90m,
            SpecialPoints = 0m,
            SimilarityPenaltyWeight = 0m,
            RoundingDigits = 1,
            Questions =
            [
                new QuestionDefinition
                {
                    Id = "Q1",
                    DisplayName = "Question Q1",
                    QuestionText = "Explain recursion in one sentence.",
                    PrimarySourceColumn = "A",
                    SupportingSourceColumns = ["B"],
                    Points = 10m,
                    Enabled = true,
                    Evaluators =
                    [
                        new EvaluatorDefinition
                        {
                            Id = KnowledgeEvaluatorId,
                            DisplayName = "Knowledge evaluator",
                            Type = EvaluatorType.KnowledgeCoverage,
                            Weight = 1m,
                            Range = new ScoreRange(0m, 10m),
                            BuiltInTemplateVersion = BuiltInPromptTemplates.KnowledgeTemplateVersion,
                            Enabled = true,
                            Criteria =
                            [
                                new CriterionDefinition { Id = KnowledgeCriterionId, DisplayName = "Knowledge criterion", Description = "Mentions the base case.", Weight = 1m, Enabled = true },
                            ],
                        },
                        new EvaluatorDefinition
                        {
                            Id = CustomEvaluatorId,
                            DisplayName = "Custom evaluator",
                            Type = EvaluatorType.CustomPrompt,
                            Weight = 1m,
                            Range = new ScoreRange(0m, 10m),
                            CustomPromptTemplate = "Rate the answer.\n{設問}\n{回答}\n{補助情報}\n{評価項目}",
                            Enabled = true,
                            Criteria =
                            [
                                new CriterionDefinition { Id = CustomCriterionId, DisplayName = "Custom criterion", Description = "Is concise.", Weight = 1m, Enabled = true },
                            ],
                        },
                    ],
                },
            ],
        };

    // 271 と 389 だけに回答を置く（間の行は空回答＝送信しない）。B 列は record ID（= source row）。
    private static void CreateInput(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
        WorksheetPart part = workbookPart.AddNewPart<WorksheetPart>();
        SheetData data = new(new Row(Inline("A1", "Answer"), Inline("B1", "RecordId")) { RowIndex = 1 });
        foreach (int row in new[] { RowA, RowB })
        {
            string index = row.ToString(CultureInfo.InvariantCulture);
            data.Append(new Row(Inline("A" + index, SharedAnswer), Inline("B" + index, index)) { RowIndex = (uint)row });
        }

        part.Worksheet = new Worksheet(data);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(part), SheetId = 1, Name = "Original" });
        workbookPart.Workbook.Save();
    }

    private static bool HasRow(string line, int row) =>
        Regex.IsMatch(line, @"(?<![0-9A-Za-z])row=" + row.ToString(CultureInfo.InvariantCulture) + @"(?![0-9])");

    private static List<string> PropertyNamesMatching(JsonElement element, Func<string, bool> predicate)
    {
        List<string> found = [];
        void Walk(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty p in e.EnumerateObject())
                    {
                        if (predicate(p.Name))
                        {
                            found.Add(p.Name);
                        }

                        if (p.Name is "properties" && p.Value.ValueKind == JsonValueKind.Object)
                        {
                            foreach (JsonProperty child in p.Value.EnumerateObject())
                            {
                                if (predicate(child.Name))
                                {
                                    found.Add(child.Name);
                                }
                            }
                        }

                        Walk(p.Value);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (JsonElement item in e.EnumerateArray())
                    {
                        Walk(item);
                    }

                    break;
            }
        }

        Walk(element);
        return found;
    }

    private static Dictionary<int, Dictionary<string, string?>> ReadResults(string path)
    {
        Dictionary<int, Dictionary<string, string?>> rows = [];
        using SpreadsheetDocument doc = SpreadsheetDocument.Open(path, false);
        Sheet sheet = doc.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Single(s => s.Name == ResultsSheet);
        WorksheetPart part = (WorksheetPart)doc.WorkbookPart.GetPartById(sheet.Id!.Value!);
        Dictionary<string, string> columnToHeader = [];
        Row? headerRow = part.Worksheet!.Descendants<Row>().FirstOrDefault(r => r.RowIndex!.Value == 1);
        foreach (Cell cell in headerRow?.Elements<Cell>() ?? [])
        {
            string? text = CellText(doc, cell);
            if (!string.IsNullOrEmpty(text))
            {
                columnToHeader[CellRef().Match(cell.CellReference!.Value!).Groups[1].Value] = text;
            }
        }

        foreach (Row r in part.Worksheet.Descendants<Row>().Where(r => r.RowIndex!.Value > 1))
        {
            Dictionary<string, string?> cells = [];
            foreach (Cell cell in r.Elements<Cell>())
            {
                string col = CellRef().Match(cell.CellReference!.Value!).Groups[1].Value;
                if (columnToHeader.TryGetValue(col, out string? header))
                {
                    cells[header] = CellText(doc, cell);
                }
            }

            if (cells.TryGetValue(ResultsSheetWriter.SourceRowHeader, out string? source)
                && int.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sourceRow))
            {
                rows[sourceRow] = cells;
            }
        }

        return rows;
    }

    private static string? Cell(Dictionary<int, Dictionary<string, string?>> rows, int sourceRow, string header)
    {
        Dictionary<string, string?> row = rows[sourceRow];
        Assert.True(rows.Values.Any(r => r.ContainsKey(header)) || row.ContainsKey(header), "列がない: " + header);
        return row.TryGetValue(header, out string? value) ? value : null;
    }

    private static decimal Dec(string? text)
    {
        Assert.False(string.IsNullOrEmpty(text), "値が blank");
        return decimal.Parse(text!, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static string? CellText(SpreadsheetDocument doc, Cell cell)
    {
        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText;
        }

        string? value = cell.CellValue?.Text;
        if (value is not null && cell.DataType?.Value == CellValues.SharedString)
        {
            SharedStringTablePart? table = doc.WorkbookPart!.SharedStringTablePart;
            return table?.SharedStringTable!.Elements<SharedStringItem>().ElementAt(int.Parse(value, CultureInfo.InvariantCulture)).InnerText;
        }

        return value;
    }

    private static Cell Inline(string reference, string value) =>
        new()
        {
            CellReference = reference,
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(value)),
        };

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    // ------------------------------------------------------------------
    // 記録と応答代替
    // ------------------------------------------------------------------
    private sealed class AttemptRecord
    {
        internal required string SessionId { get; init; }
        internal required string Prompt { get; init; }
        internal required JsonElement Schema { get; init; }
        internal required JsonElement Arguments { get; init; }
    }

    private sealed class RequestRecord
    {
        internal required SafeEvaluationPayload Payload { get; init; }
        internal required int RecordId { get; init; }
        internal required Func<SafeEvaluationPayload, string> Script { get; init; }
        internal List<AttemptRecord> Attempts { get; } = [];
        internal EvaluationRunnerResult? Result { get; set; }
    }

    /// <summary>実際の送信経路（EphemeralEvaluationRunnerAdapter）の前で、送信時の要求を記録する。</summary>
    private sealed class RecordingRunner(IEvaluationRunner inner) : IEvaluationRunner
    {
        internal static readonly AsyncLocal<RequestRecord?> Current = new();
        private readonly object gate = new();
        private readonly List<RequestRecord> requests = [];

        internal IReadOnlyList<RequestRecord> Requests
        {
            get
            {
                lock (gate)
                {
                    return [.. requests];
                }
            }
        }

        public async Task<EvaluationRunnerResult> EvaluateAsync(SafeEvaluationPayload payload, string modelId, CancellationToken cancellationToken)
        {
            int recordId = RecordIdOf(payload);
            RequestRecord record = new() { Payload = payload, RecordId = recordId, Script = p => DefaultResponse(p, recordId) };
            lock (gate)
            {
                requests.Add(record);
            }

            return await RunAsync(record, modelId, cancellationToken);
        }

        internal Task<EvaluationRunnerResult> EvaluateScriptedAsync(SafeEvaluationPayload payload, Func<SafeEvaluationPayload, string> script, CancellationToken cancellationToken) =>
            RunAsync(new RequestRecord { Payload = payload, RecordId = RecordIdOf(payload), Script = script }, "model-test", cancellationToken);

        private async Task<EvaluationRunnerResult> RunAsync(RequestRecord record, string modelId, CancellationToken cancellationToken)
        {
            RequestRecord? previous = Current.Value;
            Current.Value = record;
            try
            {
                EvaluationRunnerResult result = await inner.EvaluateAsync(record.Payload, modelId, cancellationToken);
                record.Result = result;
                return result;
            }
            finally
            {
                Current.Value = previous;
            }
        }
    }

    /// <summary>Copilot transport のローカル代替。外部へ送信せず、記録中の要求の Script で submit tool を 1 回呼ぶ。</summary>
    private sealed class LocalResponderFactory : IEphemeralCopilotTransportFactory
    {
        public IEphemeralCopilotTransport Create() => new LocalTransport();
    }

    private sealed class LocalTransport : IEphemeralCopilotTransport
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<IEphemeralCopilotSession> CreateSessionAsync(SessionConfig config, CancellationToken cancellationToken) =>
            Task.FromResult<IEphemeralCopilotSession>(new LocalSession(config));

        public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class LocalSession(SessionConfig config) : IEphemeralCopilotSession
    {
        public string SessionId { get; } = config.SessionId ?? throw new InvalidOperationException("session id required");

        public async Task SendAndWaitAsync(MessageOptions options, CancellationToken cancellationToken)
        {
            RequestRecord record = RecordingRunner.Current.Value
                ?? throw new InvalidOperationException("送信時の要求が記録されていない");
            AIFunction tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(config.Tools!));
            JsonElement arguments = JsonDocument.Parse(record.Script(record.Payload)).RootElement.Clone();
            lock (record.Attempts)
            {
                record.Attempts.Add(new AttemptRecord
                {
                    SessionId = SessionId,
                    Prompt = options.Prompt ?? string.Empty,
                    Schema = tool.JsonSchema.Clone(),
                    Arguments = arguments,
                });
            }

            ToolInvocation invocation = new()
            {
                SessionId = SessionId,
                ToolCallId = Guid.NewGuid().ToString("N"),
                ToolName = tool.Name,
                Arguments = arguments,
            };
            AIFunctionArguments functionArguments = new()
            {
                Context = new Dictionary<object, object?> { [typeof(ToolInvocation)] = invocation },
            };
            _ = await tool.InvokeAsync(functionArguments, cancellationToken);
        }

        public Task AbortAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ReferenceRunner : IReferenceAnswerOperationRunner
    {
        public Task<AuxiliaryOperationResult<ReferenceAnswerResult>> EvaluateAsync(SafeReferenceAnswerPayload payload, CancellationToken cancellationToken) =>
            Task.FromResult(AuxiliaryOperationResult<ReferenceAnswerResult>.Succeeded(
                new ReferenceAnswerResult { QuestionId = payload.QuestionId, Answer = "synthetic reference answer about recursion" }));
    }

    private sealed class SpecialRunner : ISpecialEvaluationOperationRunner
    {
        public Task<AuxiliaryOperationResult<SpecialQuantificationResult>> EvaluateAsync(SafeSpecialEvaluationPayload payload, string modelId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no special evaluation");
    }

    private sealed class TempDir : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SRE-ST120-" + Guid.NewGuid().ToString("N"));

        internal TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

using System.Text.Json;
using GitHub.Copilot;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Copilot;

public sealed class SubmitQuantificationToolTests
{
    [Fact]
    public async Task One_complete_valid_invocation_is_accepted_as_one_whole_result()
    {
        SubmitQuantificationTool tool = new(CreatePayload());

        string acknowledgement = await tool.InvokeAsync(
            Invocation(ValidJson()),
            TestContext.Current.CancellationToken);

        Assert.Equal(SubmitQuantificationTool.AcceptedAcknowledgement, acknowledgement);
        Assert.True(tool.TryGetAcceptedResult(out QuantificationResult? accepted));
        Assert.Equal("E1", accepted.EvaluatorId);
        Assert.Equal(["C1", "C2"], accepted.Criteria.Select(criterion => criterion.CriterionId));
        Assert.Equal([4m, 1.5m], accepted.Criteria.Select(criterion => criterion.RawScore));
        SubmitQuantificationOutcome outcome = tool.GetOutcome();
        Assert.True(outcome.IsAccepted);
        Assert.Equal(SubmitQuantificationStatus.Accepted, outcome.Status);
        Assert.Equal(1, outcome.InvocationCount);
        Assert.Empty(outcome.ErrorCodes);
    }

    [Theory]
    [MemberData(nameof(InvalidCriterionPayloads))]
    public async Task Missing_duplicate_unknown_and_partial_criteria_reject_the_whole_payload(
        string criteriaJson,
        string expectedCode)
    {
        SubmitQuantificationTool tool = new(CreatePayload());
        string json = $$"""
            {
              "EvaluatorId": "E1",
              "Criteria": {{criteriaJson}}
            }
            """;

        string acknowledgement = await tool.InvokeAsync(
            Invocation(json),
            TestContext.Current.CancellationToken);

        Assert.Equal(SubmitQuantificationTool.RejectedAcknowledgement, acknowledgement);
        Assert.False(tool.TryGetAcceptedResult(out _));
        SubmitQuantificationOutcome outcome = tool.GetOutcome();
        Assert.Equal(SubmitQuantificationStatus.InvalidPayload, outcome.Status);
        Assert.Contains(expectedCode, outcome.ErrorCodes);
        Assert.Null(outcome.AcceptedResult);
    }

    public static TheoryData<string, string> InvalidCriterionPayloads => new()
    {
        { "[]", "MISSING_CRITERION" },
        { $"[{CriterionJson("C1", 4m)}, {CriterionJson("C1", 4m)}]", "DUPLICATE_CRITERION" },
        { $"[{CriterionJson("C1", 4m)}, {CriterionJson("UNKNOWN", 1.5m)}]", "UNKNOWN_CRITERION" },
        { $"[{CriterionJson("C1", 4m)}]", "MISSING_CRITERION" },
    };

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(10.0001)]
    public async Task Criterion_specific_range_is_enforced_without_clamp_or_partial_adoption(double score)
    {
        SubmitQuantificationTool tool = new(CreatePayload());
        string json = $$"""
            {
              "EvaluatorId": "E1",
              "Criteria": [
                {{CriterionJson("C1", (decimal)score)}},
                {{CriterionJson("C2", 1.5m)}}
              ]
            }
            """;

        await tool.InvokeAsync(Invocation(json), TestContext.Current.CancellationToken);

        SubmitQuantificationOutcome outcome = tool.GetOutcome();
        Assert.Equal(SubmitQuantificationStatus.InvalidPayload, outcome.Status);
        Assert.Contains("RAW_SCORE_OUT_OF_RANGE", outcome.ErrorCodes);
        Assert.Null(outcome.AcceptedResult);
    }

    [Fact]
    public async Task Evidence_must_bind_to_the_exact_sent_same_row_source()
    {
        SubmitQuantificationTool tool = new(CreatePayload());
        string json = $$"""
            {
              "EvaluatorId": "E1",
              "Criteria": [
                {{CriterionJson("C1", 4m, evidence: "different-row evidence")}},
                {{CriterionJson("C2", 1.5m)}}
              ]
            }
            """;

        await tool.InvokeAsync(Invocation(json), TestContext.Current.CancellationToken);

        SubmitQuantificationOutcome outcome = tool.GetOutcome();
        Assert.Contains("EVIDENCE_NOT_EXACT_SUBSTRING", outcome.ErrorCodes);
        Assert.Contains(outcome.ErrorDetails, detail => detail.StartsWith("EVIDENCE_NOT_EXACT_SUBSTRING@C1.Evidence", StringComparison.Ordinal));
        Assert.DoesNotContain(outcome.ErrorDetails, detail => detail.Contains("different-row", StringComparison.Ordinal));
        Assert.Null(outcome.AcceptedResult);
    }

    [Fact]
    public void Diagnostics_sanitize_content_and_report_only_inside_a_run_scope()
    {
        List<string> lines = [];
        StudyReportEvaluator.App.Logging.EvaluationDiagnostics.Report("OUTSIDE", "ignored");
        using (StudyReportEvaluator.App.Logging.EvaluationDiagnostics.BeginRun(lines.Add))
        using (StudyReportEvaluator.App.Logging.EvaluationDiagnostics.BeginContext("row=10"))
        {
            StudyReportEvaluator.App.Logging.EvaluationDiagnostics.Report("ATTEMPT_FAILED", "codes=A 学生の回答");
        }

        string line = Assert.Single(lines);
        Assert.Equal("DIAG ATTEMPT_FAILED row=10 codes=A ?????", line);
    }

    [Fact]
    public async Task Valid_supporting_and_none_evidence_contracts_are_accepted()
    {
        SubmitQuantificationTool tool = new(CreatePayload());
        string json = $$"""
            {
              "EvaluatorId": "E1",
              "Criteria": [
                {{CriterionJson(
                    "C1",
                    4m,
                    source: "SUPPORTING_COLUMN",
                    sourceColumn: "K",
                    evidence: "support K")}},
                {{CriterionJson(
                    "C2",
                    1.5m,
                    source: "NONE",
                    sourceColumn: "",
                    evidence: "")}}
              ]
            }
            """;

        string acknowledgement = await tool.InvokeAsync(
            Invocation(json),
            TestContext.Current.CancellationToken);

        Assert.Equal(SubmitQuantificationTool.AcceptedAcknowledgement, acknowledgement);
        Assert.True(tool.TryGetAcceptedResult(out _));
    }

    [Fact]
    public async Task Omitted_evaluator_id_is_filled_from_the_payload_but_a_wrong_one_is_rejected()
    {
        string criteria = $"[{CriterionJson("C1", 4m)}, {CriterionJson("C2", 1.5m)}]";
        SubmitQuantificationTool omitted = new(CreatePayload());
        SubmitQuantificationTool wrong = new(CreatePayload());

        await omitted.InvokeAsync(
            Invocation($$"""{ "Criteria": {{criteria}} }"""),
            TestContext.Current.CancellationToken);
        await wrong.InvokeAsync(
            Invocation($$"""{ "EvaluatorId": "E2", "Criteria": {{criteria}} }"""),
            TestContext.Current.CancellationToken);

        Assert.True(omitted.TryGetAcceptedResult(out QuantificationResult? accepted));
        Assert.Equal("E1", accepted.EvaluatorId);
        Assert.False(wrong.TryGetAcceptedResult(out _));
        Assert.Contains("EVALUATOR_ID_MISMATCH", wrong.GetOutcome().ErrorCodes);
    }

    // FR-120 AC-007 (互換受信 / 監査 C-01): 期待値と一致する明示 ID は受理し、未知・別 evaluator の ID は補正せず拒否する。
    [Theory]
    [InlineData("\"E1\"", true, null)]
    [InlineData("\"ZZUNKNOWN9\"", false, "EVALUATOR_ID_MISMATCH")]
    [InlineData("\"E2\"", false, "EVALUATOR_ID_MISMATCH")]
    [InlineData("\"e1\"", false, "EVALUATOR_ID_MISMATCH")]
    [InlineData("\"\"", false, null)]
    [InlineData("1", false, null)]
    [InlineData("null", false, null)]
    [InlineData("[\"E1\"]", false, null)]
    public async Task FR120_AC007_explicit_evaluator_id_is_accepted_only_when_it_matches_the_sent_request(
        string evaluatorIdJson,
        bool accepted,
        string? expectedCode)
    {
        string criteria = $"[{CriterionJson("C1", 4m)}, {CriterionJson("C2", 1.5m)}]";
        SubmitQuantificationTool tool = new(CreatePayload());

        await tool.InvokeAsync(
            Invocation($$"""{ "EvaluatorId": {{evaluatorIdJson}}, "Criteria": {{criteria}} }"""),
            TestContext.Current.CancellationToken);

        Assert.Equal(accepted, tool.TryGetAcceptedResult(out QuantificationResult? result));
        if (accepted)
        {
            Assert.Equal("E1", result!.EvaluatorId);
        }
        else
        {
            Assert.Null(tool.GetOutcome().AcceptedResult);
            Assert.NotEmpty(tool.GetOutcome().ErrorCodes);
            if (expectedCode is not null)
            {
                Assert.Contains(expectedCode, tool.GetOutcome().ErrorCodes);
            }
        }
    }

    // FR-120 AC-007: 異なる評価要求を交互に処理しても、省略応答は各要求の送信時の ID に帰属し、前の要求の ID を流用しない。
    [Fact]
    public async Task FR120_AC007_omitted_evaluator_id_binds_to_each_sent_request_when_requests_alternate()
    {
        string criteria = $"[{CriterionJson("C1", 4m)}, {CriterionJson("C2", 1.5m)}]";
        string[] order = ["E1", "E2", "E1", "E2"];
        List<string> bound = [];

        foreach (string evaluatorId in order)
        {
            SubmitQuantificationTool tool = new(CreatePayload(evaluatorId));
            await tool.InvokeAsync(
                Invocation($$"""{ "Criteria": {{criteria}} }"""),
                TestContext.Current.CancellationToken);
            Assert.True(tool.TryGetAcceptedResult(out QuantificationResult? accepted));
            bound.Add(accepted.EvaluatorId);
        }

        Assert.Equal(order, bound);

        // 前の要求（E2）の ID を明示した応答は、次の要求（E1）では拒否される。
        SubmitQuantificationTool next = new(CreatePayload());
        await next.InvokeAsync(
            Invocation($$"""{ "EvaluatorId": "E2", "Criteria": {{criteria}} }"""),
            TestContext.Current.CancellationToken);
        Assert.False(next.TryGetAcceptedResult(out _));
        Assert.Contains("EVALUATOR_ID_MISMATCH", next.GetOutcome().ErrorCodes);
    }

    [Theory]
    [MemberData(nameof(ClosedWirePayloads))]
    public async Task Unknown_missing_duplicate_and_wrong_type_fields_are_rejected_by_the_wire_parser(
        string json,
        string expectedCode)
    {
        SubmitQuantificationTool tool = new(CreatePayload());

        await tool.InvokeAsync(Invocation(json), TestContext.Current.CancellationToken);

        SubmitQuantificationOutcome outcome = tool.GetOutcome();
        Assert.Equal(SubmitQuantificationStatus.InvalidPayload, outcome.Status);
        Assert.Contains(expectedCode, outcome.ErrorCodes);
        Assert.Null(outcome.AcceptedResult);
    }

    public static TheoryData<string, string> ClosedWirePayloads => new()
    {
        {
            $$"""
            {
              "EvaluatorId": "E1",
              "Criteria": [{{CriterionJson("C1", 4m)}}, {{CriterionJson("C2", 1.5m)}}],
              "OverallScore": 100
            }
            """,
            "ROOT_UNKNOWN_PROPERTY"
        },
        {
            $$"""
            {
              "EvaluatorId": "E1",
              "EvaluatorId": "E1",
              "Criteria": [{{CriterionJson("C1", 4m)}}, {{CriterionJson("C2", 1.5m)}}]
            }
            """,
            "ROOT_DUPLICATE_PROPERTY"
        },
        {
            $$"""
            {
              "EvaluatorId": "E1",
              "Criteria": [
                {
                  "CriterionId": "C1",
                  "RawScore": 4,
                  "Reason": "reason",
                  "Evidence": "primary evidence",
                  "EvidenceSource": "PRIMARY_ANSWER",
                  "EvidenceSourceColumnId": "G",
                  "Weight": 1
                },
                {{CriterionJson("C2", 1.5m)}}
              ]
            }
            """,
            "CRITERION_UNKNOWN_PROPERTY"
        },
        {
            $$"""
            {
              "EvaluatorId": "E1",
              "Criteria": [
                {
                  "CriterionId": "C1",
                  "RawScore": 4,
                  "Reason": "reason",
                  "Evidence": "primary evidence",
                  "EvidenceSource": "PRIMARY_ANSWER"
                },
                {{CriterionJson("C2", 1.5m)}}
              ]
            }
            """,
            "CRITERION_MISSING_PROPERTY"
        },
        {
            $$"""
            {
              "EvaluatorId": "E1",
              "Criteria": [
                {
                  "CriterionId": "C1",
                  "RawScore": "4",
                  "Reason": "reason",
                  "Evidence": "primary evidence",
                  "EvidenceSource": "PRIMARY_ANSWER",
                  "EvidenceSourceColumnId": "G"
                },
                {{CriterionJson("C2", 1.5m)}}
              ]
            }
            """,
            "RAW_SCORE_INVALID"
        },
    };

    [Fact]
    public void Normal_assistant_text_without_a_tool_invocation_is_not_a_result()
    {
        const string ignoredAssistantBody = "Here is a normal assistant answer with score 10.";
        SubmitQuantificationTool tool = new(CreatePayload());

        SubmitQuantificationOutcome outcome = tool.GetOutcome();

        Assert.NotEmpty(ignoredAssistantBody);
        Assert.Equal(SubmitQuantificationStatus.NotInvoked, outcome.Status);
        Assert.Equal(0, outcome.InvocationCount);
        Assert.False(outcome.IsAccepted);
        Assert.False(tool.TryGetAcceptedResult(out _));
    }

    [Fact]
    public async Task A_duplicate_call_invalidates_an_earlier_valid_submission()
    {
        SubmitQuantificationTool tool = new(CreatePayload());
        ToolInvocation invocation = Invocation(ValidJson());

        string first = await tool.InvokeAsync(invocation, TestContext.Current.CancellationToken);
        string second = await tool.InvokeAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Equal(SubmitQuantificationTool.AcceptedAcknowledgement, first);
        Assert.Equal(SubmitQuantificationTool.RejectedAcknowledgement, second);
        SubmitQuantificationOutcome outcome = tool.GetOutcome();
        Assert.Equal(SubmitQuantificationStatus.DuplicateInvocation, outcome.Status);
        Assert.Equal(2, outcome.InvocationCount);
        Assert.Equal(["TOOL_INVOCATION_COUNT_INVALID"], outcome.ErrorCodes);
        Assert.Null(outcome.AcceptedResult);
        Assert.False(tool.TryGetAcceptedResult(out _));
    }

    [Fact]
    public async Task Concurrent_calls_are_counted_atomically_and_cannot_leave_a_result_adopted()
    {
        SubmitQuantificationTool tool = new(CreatePayload());

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Task<string>[] calls = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(
                async () => await tool.InvokeAsync(Invocation(ValidJson()), cancellationToken),
                cancellationToken))
            .ToArray();
        await Task.WhenAll(calls);

        SubmitQuantificationOutcome outcome = tool.GetOutcome();
        Assert.Equal(SubmitQuantificationStatus.DuplicateInvocation, outcome.Status);
        Assert.Equal(32, outcome.InvocationCount);
        Assert.Null(outcome.AcceptedResult);
        Assert.False(tool.TryGetAcceptedResult(out _));
    }

    [Fact]
    public async Task Rejections_and_string_representations_do_not_disclose_reason_or_evidence()
    {
        const string canary = "PRIVATE-REASON-EVIDENCE-CANARY";
        SubmitQuantificationTool tool = new(CreatePayload());
        string json = $$"""
            {
              "EvaluatorId": "E1",
              "Criteria": [
                {{CriterionJson("C1", 4m, reason: canary, evidence: canary)}},
                {{CriterionJson("C2", 1.5m)}}
              ]
            }
            """;

        string acknowledgement = await tool.InvokeAsync(
            Invocation(json),
            TestContext.Current.CancellationToken);
        SubmitQuantificationOutcome outcome = tool.GetOutcome();
        string rendered = string.Join(
            "|",
            [acknowledgement, tool.ToString(), outcome.ToString(), .. outcome.ErrorCodes]);

        Assert.DoesNotContain(canary, rendered, StringComparison.Ordinal);
        Assert.Equal(SubmitQuantificationTool.RejectedAcknowledgement, acknowledgement);
        Assert.Null(outcome.AcceptedResult);
    }

    [Fact]
    public async Task Invocation_must_use_the_registered_tool_name()
    {
        SubmitQuantificationTool tool = new(CreatePayload());
        ToolInvocation invocation = Invocation(ValidJson());
        invocation.ToolName = "other_tool";

        await tool.InvokeAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Contains("UNEXPECTED_TOOL_NAME", tool.GetOutcome().ErrorCodes);
        Assert.False(tool.TryGetAcceptedResult(out _));
    }

    private static ToolInvocation Invocation(string json) => new()
    {
        SessionId = "synthetic-session",
        ToolCallId = Guid.NewGuid().ToString("N"),
        ToolName = EvaluationSchemaFactory.ToolName,
        Arguments = JsonDocument.Parse(json).RootElement.Clone(),
    };

    private static string ValidJson() => $$"""
        {
          "EvaluatorId": "E1",
          "Criteria": [
            {{CriterionJson("C1", 4m)}},
            {{CriterionJson("C2", 1.5m)}}
          ]
        }
        """;

    private static string CriterionJson(
        string id,
        decimal score,
        string reason = "short reason",
        string source = "PRIMARY_ANSWER",
        string sourceColumn = "G",
        string evidence = "primary evidence") =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["CriterionId"] = id,
            ["RawScore"] = score,
            ["Reason"] = reason,
            ["Evidence"] = evidence,
            ["EvidenceSource"] = source,
            ["EvidenceSourceColumnId"] = sourceColumn,
        });

    private static SafeEvaluationPayload CreatePayload(string evaluatorId = "E1") => new(
        "Q1",
        evaluatorId,
        "<private-prompt>",
        new EvaluationSourceCell(
            EvaluationSourceKind.PrimaryAnswer,
            "G",
            "primary evidence is here"),
        [
            new EvaluationSourceCell(
                EvaluationSourceKind.SupportingColumn,
                "K",
                "support K is here"),
        ],
        [
            new ExpectedCriterion("C1", "Criterion 1", new ScoreRange(0m, 10m)),
            new ExpectedCriterion("C2", "Criterion 2", new ScoreRange(-2.5m, 3.5m)),
        ]);
}
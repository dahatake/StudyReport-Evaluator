using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
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
/// FR-115（質問文字列による複数参照と条件・互換性の一貫性）の System Test。
/// 期待値は受入基準 AC-009・AC-080〜AC-085 と PARAM-003（設問ごとの参照回答数 = 5）だけから決める。
/// 実 AI・実 credential は使わず、fake の参照・通常評価 runner と合成 workbook だけで durable run を流す。
/// </summary>
public sealed class MultiReferenceSetTests
{
    /// <summary>PARAM-003: 新規runの設問ごとの参照回答数。</summary>
    private const int Param003Slots = 5;

    private const string ModelId = "model-st115";
    private const string Effort = "high";

    // ------------------------------------------------------------------
    // ケース 1: FR-115 AC-009 / AC-085（参照生成の入力・件数・計画）
    // ------------------------------------------------------------------

    // FR-115 AC-009 AC-085: 有効設問ごとに PARAM-003 の固定 slot を、質問文字列だけを比較入力として生成し、
    // 学生数に応じて再生成しない。通常評価と同じ model・effort。予定生成数を表示し、失敗を追加生成で隠さない。
    [Fact]
    public async Task FR115_Generation_AC009_AC085_fixed_slots_question_only_shared_by_all_students()
    {
        using Fixture fx = new(questionCount: 2, studentRows: 3, disabledQuestion: true);
        RecordingReferenceRunner refs = new();
        refs.FailCalls.Add(2); // 異常系: 終端失敗の slot を別の追加生成で埋めない
        RecordingNormalRunner normal = new();
        List<DurableEvaluationProgress> progress = [];

        RunSummary summary = await fx.RunAsync(refs, normal, progress: p => { lock (progress) { progress.Add(p); } });

        // 有効設問 2 × PARAM-003（学生 3 人でも 3 倍にならない。無効設問は生成しない。追加生成もしない）
        Assert.Equal(2 * Param003Slots, refs.Calls.Count);
        foreach (string q in new[] { "Q1", "Q2" })
        {
            Assert.Equal(Param003Slots, refs.Calls.Count(c => c.QuestionId == q));
        }

        Assert.DoesNotContain(refs.Calls, c => c.QuestionId == "Q3");

        // 比較入力は質問文字列のみ（学生回答・Prompt・補助列・採点基準・他設問・他参照回答・無効設問を混入させない）
        string[] earlierBodies = [];
        foreach (RecordedReferenceCall call in refs.Calls)
        {
            Assert.Contains(Fixture.QuestionText(call.QuestionId), call.Prompt, StringComparison.Ordinal);
            foreach (string forbidden in Fixture.ForbiddenInReferencePrompt(call.QuestionId))
            {
                Assert.DoesNotContain(forbidden, call.Prompt, StringComparison.Ordinal);
            }

            foreach (string body in earlierBodies)
            {
                Assert.DoesNotContain(body, call.Prompt, StringComparison.Ordinal);
            }

            earlierBodies = call.Body is null ? earlierBodies : [.. earlierBodies, call.Body];
        }

        // 通常評価と同じ model・effort（無断の別 model 切替なし）
        Assert.All(refs.Calls, c => Assert.Equal(ModelId, c.ModelId));
        Assert.All(refs.Calls, c => Assert.Equal(Effort, c.Effort));
        Assert.NotEmpty(normal.ModelIds);
        Assert.All(normal.ModelIds, m => Assert.Equal(ModelId, m));

        // 予定生成数（PARAM-003 を含む）を計算・表示する
        Assert.Contains(progress, p => p.ReferenceTotal == 2 * Param003Slots);
        Assert.DoesNotContain(progress, p => p.ReferenceTotal > 2 * Param003Slots);

        // retry 込み attempt 上限を計算・表示する（予定生成数以上の値として公開される）
        long?[] attemptFigures = PublicAttemptFigures(progress.LastOrDefault()).Concat(PublicAttemptFigures(summary)).ToArray();
        Assert.Contains(attemptFigures, v => v >= 2 * Param003Slots);

        // 全学生が同じ集合を使う: 完成 workbook の References には 10 slot 分の本文が各 1 回だけ載る
        Assert.NotNull(summary.FinalPath);
        OutputBook wb = OutputBook.Read(summary.FinalPath!);
        foreach (RecordedReferenceCall call in refs.Calls.Where(c => c.Body is not null && !c.Failed))
        {
            Assert.Equal(1, wb.Sheet("References").Count(v => v.Contains(call.Body!, StringComparison.Ordinal)));
        }
    }

    // ------------------------------------------------------------------
    // ケース 2: FR-115 AC-080 / AC-081 / AC-084（条件の保存・集計診断・Excel と checkpoint の一貫性）
    // ------------------------------------------------------------------

    // FR-115 AC-080 AC-081 AC-084: model `auto`・SDK 未観測の条件、正常＋重複・一部失敗・全失敗の集合を
    // References / Results / Run sheet と checkpoint が同じ snapshot として示し、本文を log に残さず formula として実行しない。
    [Fact]
    public async Task FR115_Conditions_AC080_AC081_AC084_set_conditions_diagnostics_and_outputs_agree()
    {
        using Fixture fx = new(questionCount: 3, studentRows: 2, disabledQuestion: false);
        RecordingReferenceRunner refs = new();
        // Q1: slot 2 と 3 が同一本文（重複）、slot 5 は formula 風の本文。Q2: 1 slot 失敗（暫定）。Q3: 全 slot 失敗。
        refs.BodyOverride[2] = "REFBODY-DUPLICATE-Q1";
        refs.BodyOverride[3] = "REFBODY-DUPLICATE-Q1";
        refs.BodyOverride[5] = "=HYPERLINK(\"http://example.invalid/x\",\"REFBODY-FORMULA\")";
        refs.FailWhen = c => (c.QuestionId == "Q2" && c.PerQuestionIndex == 5) || c.QuestionId == "Q3";
        RecordingCheckpointStore store = new();
        // 学生行 2 の Q1 回答は Q1 slot 4 と同一（最大値の参照が slot 4 と結び付く）
        fx.StudentAnswerOverride = (row, q) => row == 2 && q == "Q1" ? RecordingReferenceRunner.DefaultBody(4) : null;
        fx.Recreate();

        RunSummary summary = await fx.RunAsync(refs, new RecordingNormalRunner(), modelId: "auto", store: store);

        Assert.Equal(3 * Param003Slots, refs.Calls.Count);
        Assert.NotNull(summary.FinalPath);
        OutputBook wb = OutputBook.Read(summary.FinalPath!);
        IReadOnlyList<string> references = wb.Sheet("References");
        IReadOnlyList<string> results = wb.Sheet("Results");
        IReadOnlyList<string> runSheet = wb.Sheet("Run");
        string checkpointJson = store.LastSavedJson ?? throw new Xunit.Sdk.XunitException("checkpoint が保存されていない");

        // AC-080: question ID・質問文字列・slot・本文・status・指定 model と未観測値を集合と結び付ける
        foreach (string q in new[] { "Q1", "Q2", "Q3" })
        {
            Assert.Contains(Fixture.QuestionText(q), references);
            Assert.Equal(Param003Slots, wb.RowsWithCell("References", q));
        }

        foreach (RecordedReferenceCall ok in refs.Calls.Where(c => !c.Failed))
        {
            Assert.Contains(references, v => v.Contains(ok.Body!, StringComparison.Ordinal));
            Assert.Contains(ok.Body!.Replace("\"", "\\u0022", StringComparison.Ordinal), checkpointJson.Replace("\\\"", "\\u0022", StringComparison.Ordinal), StringComparison.Ordinal);
        }

        Assert.Contains("auto", references);      // 指定 model
        Assert.Contains(Effort, references);      // 指定 effort
        Assert.Contains(references, v => v.Contains("未取得", StringComparison.Ordinal)); // 実model・temperature・seed 等は観測不能
        Assert.Contains(ResultsStatusCodes.AiRuntimeFailed, references);
        Assert.Contains(ResultsStatusCodes.AiRuntimeFailed, checkpointJson, StringComparison.Ordinal);

        // AC-081: 成功数／予定数、重複の表示、一部失敗の暫定表示（Q1 5/5, Q2 4/5, Q3 0/5）
        string resultsText = string.Join("\n", results);
        Assert.Matches(new Regex(@"(^|\D)5\s*[/／]\s*5(\D|$)"), resultsText);
        Assert.Matches(new Regex(@"(^|\D)4\s*[/／]\s*5(\D|$)"), resultsText);
        Assert.Matches(new Regex(@"(^|\D)0\s*[/／]\s*5(\D|$)"), resultsText);
        Assert.Contains("重複", resultsText, StringComparison.Ordinal);
        Assert.Contains("暫定", resultsText, StringComparison.Ordinal);
        // AC-081（ff31b8d 改訂）: 最大・最小・中央値は記述統計として確認できる。中央値・最小値・差は表示だけで採点・減点に使わない
        // （減点は完全な参照集合の参照別合成値の最大値 = AC-011 側で検証）。
        Assert.Contains("最大", resultsText, StringComparison.Ordinal);
        Assert.Contains("最小", resultsText, StringComparison.Ordinal);
        Assert.Contains("中央値", resultsText, StringComparison.Ordinal);

        // AC-084: Run sheet の比較条件要約は References と同じ指定 model を示す
        Assert.Contains(runSheet, v => v.Contains("auto", StringComparison.Ordinal));

        // AC-084: 出力文字列を Excel formula として実行しない
        Assert.False(wb.HasFormulaContaining("HYPERLINK"), "参照本文が formula として書き込まれた");
        Assert.Contains(references, v => v.Contains("REFBODY-FORMULA", StringComparison.Ordinal));

        // AC-084: 本文・Prompt を通常 log や設定ファイルへ追加保存しない（出力フォルダ内の text 系ファイル）
        foreach (string file in Directory.EnumerateFiles(fx.Root, "*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotContain("REFBODY", text, StringComparison.Ordinal);
            Assert.DoesNotContain(Fixture.QuestionText("Q1"), text, StringComparison.Ordinal);
        }

        // AC-084: 元本保持
        Assert.Equal(fx.InputSha256, Sha256(fx.InputPath));
    }

    // ------------------------------------------------------------------
    // ケース 3: FR-115 AC-082 / AC-083（保存・再開・旧形式互換）
    // ------------------------------------------------------------------

    // FR-115 AC-082: 中断後の再開は保存済み slot（終端失敗を含む）を再生成せず、未保存 slot だけを生成する。
    [Fact]
    public async Task FR115_Resume_AC082_saved_slots_reused_and_only_unsaved_slots_resumed()
    {
        using Fixture fx = new(questionCount: 2, studentRows: 2, disabledQuestion: false);
        using CancellationTokenSource cts = new();
        RecordingReferenceRunner first = new();
        first.FailCalls.Add(1);              // 保存済みの終端失敗
        first.CancelAtCall = 7;              // 生成中の中断
        first.Cancellation = cts;
        RecordingCheckpointStore store = new();
        string? partial = null;

        try
        {
            RunSummary s1 = await fx.RunAsync(first, new RecordingNormalRunner(), store: store, cancel: cts,
                progress: p => partial ??= p.PartialPath);
            partial = s1.PartialPath ?? partial;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.NotNull(partial);
        CheckpointLoadResult loaded = new CheckpointStore().Load(partial!, TestContext.Current.CancellationToken);
        Assert.True(loaded.IsSuccess);
        string savedJson = JsonSerializer.Serialize(loaded.Envelope);
        int savedBodies = first.Calls.Count(c => !c.Failed && c.Body is not null && savedJson.Contains(c.Body, StringComparison.Ordinal));
        int savedFailures = Regex.Matches(savedJson, ResultsStatusCodes.AiRuntimeFailed).Count;
        Assert.True(savedBodies >= Param003Slots - 1, "Q1 の成功 slot が保存されていない");
        Assert.Equal(1, savedFailures);
        Assert.DoesNotContain(ResultsStatusCodes.Cancelled, savedJson, StringComparison.Ordinal);

        RecordingReferenceRunner second = new(startCall: 100);
        RunSummary s2 = await fx.RunAsync(second, new RecordingNormalRunner(), resumePartialPath: partial);

        // 保存済み slot は再生成しない（保存済みの終端失敗も同一 run で勝手に再試行しない）。未保存 slot だけを生成する。
        Assert.Equal((2 * Param003Slots) - savedBodies - savedFailures, second.Calls.Count);
        Assert.Equal(QuantificationRunStatusCodes.Success, s2.StatusCode);
        OutputBook wb = OutputBook.Read(s2.FinalPath!);
        foreach (RecordedReferenceCall c in first.Calls.Where(c => !c.Failed && savedJson.Contains(c.Body!, StringComparison.Ordinal)))
        {
            Assert.Contains(wb.Sheet("References"), v => v.Contains(c.Body!, StringComparison.Ordinal)); // 保存済み本文を再利用
        }

        Assert.Contains(ResultsStatusCodes.AiRuntimeFailed, wb.Sheet("References")); // 全学生が同じ失敗集合を使う
    }

    // FR-115 AC-082（異常系）: checkpoint 書込失敗は以前の checkpoint を保持し、quota 枯渇の対象は保存せず新規送信を止める。
    [Fact]
    public async Task FR115_Resume_AC082_write_failure_keeps_previous_checkpoint_and_quota_stops_sending()
    {
        // 書込失敗
        using (Fixture fx = new(questionCount: 2, studentRows: 1, disabledQuestion: false))
        {
            RecordingReferenceRunner refs = new();
            RecordingCheckpointStore store = new() { FailUpdateWhenJsonContains = RecordingReferenceRunner.DefaultBody(3) };
            string? partial = null;
            try
            {
                RunSummary s = await fx.RunAsync(refs, new RecordingNormalRunner(), store: store, progress: p => partial ??= p.PartialPath);
                partial = s.PartialPath ?? partial;
                Assert.NotEqual(QuantificationRunStatusCodes.Success, s.StatusCode); // 書込失敗を成功へ丸めない（以前の checkpoint が残る）
            }
            catch (QuantificationRunException)
            {
            }

            Assert.NotNull(partial);
            CheckpointLoadResult loaded = new CheckpointStore().Load(partial!, TestContext.Current.CancellationToken);
            Assert.True(loaded.IsSuccess);
            string json = JsonSerializer.Serialize(loaded.Envelope);
            Assert.DoesNotContain(RecordingReferenceRunner.DefaultBody(3), json, StringComparison.Ordinal);
            Assert.Equal(store.LastSavedJson, json); // 以前の checkpoint をそのまま保持
        }

        // quota 枯渇
        using (Fixture fx = new(questionCount: 2, studentRows: 1, disabledQuestion: false))
        {
            RecordingReferenceRunner refs = new();
            refs.QuotaCalls.Add(3);
            RecordingCheckpointStore store = new();
            string? partial = null;
            try
            {
                RunSummary s = await fx.RunAsync(refs, new RecordingNormalRunner(), store: store, progress: p => partial ??= p.PartialPath);
                partial = s.PartialPath ?? partial;
            }
            catch (QuantificationRunException)
            {
            }

            Assert.Equal(3, refs.Calls.Count); // 新規送信停止
            Assert.NotNull(partial);
            CheckpointLoadResult loaded = new CheckpointStore().Load(partial!, TestContext.Current.CancellationToken);
            Assert.True(loaded.IsSuccess);
            Assert.DoesNotContain(ResultsStatusCodes.QuotaExhausted, JsonSerializer.Serialize(loaded.Envelope), StringComparison.Ordinal);
        }
    }

    // FR-115 AC-083: 旧単独参照 checkpoint は旧形式の単独集合として保持し PARAM-003 へ水増ししない。未保存の旧参照も旧予定数で生成し、
    // 欠けた条件は「未取得」。条件不一致・不正な形式は safe error で拒否し旧 bytes を保持する。
    [Fact]
    public async Task FR115_Resume_AC083_legacy_single_reference_checkpoint_is_kept_and_mismatch_rejected()
    {
        using Fixture fx = new(questionCount: 2, studentRows: 2, disabledQuestion: false);
        string legacy = fx.CreateLegacyCheckpoint(schemaVersion: 2, q1Answer: "OLD-REF-Q1-legacy");

        RecordingReferenceRunner refs = new(startCall: 200);
        RunSummary s = await fx.RunAsync(refs, new RecordingNormalRunner(), resumePartialPath: legacy);

        // 未保存の旧参照（Q2）は旧予定数 1 件だけ生成し、保存済みの旧参照（Q1）は再生成しない
        Assert.Single(refs.Calls);
        Assert.Equal("Q2", refs.Calls.Single().QuestionId);
        Assert.Equal(QuantificationRunStatusCodes.Success, s.StatusCode);
        OutputBook wb = OutputBook.Read(s.FinalPath!);
        IReadOnlyList<string> references = wb.Sheet("References");
        Assert.Equal(1, references.Count(v => v.Contains("OLD-REF-Q1-legacy", StringComparison.Ordinal)));
        Assert.Equal(1, wb.RowsWithCell("References", "Q1")); // 5 件へ水増ししない
        Assert.Equal(1, wb.RowsWithCell("References", "Q2"));
        Assert.Contains(references, v => v.Contains("未取得", StringComparison.Ordinal)); // 欠けた条件を新規定義へ黙って変換しない

        // 異常系: model 条件の不一致は safe error で拒否し、旧 bytes を保持する（再開条件を緩めない）
        string legacy2 = fx.CreateLegacyCheckpoint(schemaVersion: 2, q1Answer: "OLD-REF-Q1-mismatch");
        string before = Sha256(legacy2);
        RecordingReferenceRunner none = new(startCall: 300);
        QuantificationRunException mismatch = await Assert.ThrowsAsync<QuantificationRunException>(
            () => fx.RunAsync(none, new RecordingNormalRunner(), resumePartialPath: legacy2, modelId: "model-other"));
        Assert.Equal(CheckpointAdmissionStatusCodes.ModelMismatch, mismatch.Code);
        Assert.Empty(none.Calls);
        Assert.Equal(before, Sha256(legacy2));

        // 異常系: 不正な形式（未知の schema）は safe error で拒否し、旧 bytes を保持する
        string bogus = fx.CreateLegacyCheckpoint(schemaVersion: 99, q1Answer: "OLD-REF-Q1-bogus");
        string bogusBefore = Sha256(bogus);
        RecordingReferenceRunner none2 = new(startCall: 400);
        await Assert.ThrowsAsync<QuantificationRunException>(
            () => fx.RunAsync(none2, new RecordingNormalRunner(), resumePartialPath: bogus));
        Assert.Empty(none2.Calls);
        Assert.Equal(bogusBefore, Sha256(bogus));
    }

    // ------------------------------------------------------------------
    // 足場
    // ------------------------------------------------------------------

    private static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    /// <summary>公開プロパティのうち名前に Attempt を含む数値（AC-085 の「retry 込み attempt 上限の表示」の観測点）。</summary>
    private static IEnumerable<long?> PublicAttemptFigures(object? source)
    {
        if (source is null)
        {
            yield break;
        }

        foreach (PropertyInfo p in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.Name.Contains("Attempt", StringComparison.OrdinalIgnoreCase) || p.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object? v = p.GetValue(source);
            yield return v switch
            {
                int i => i,
                long l => l,
                _ => null,
            };
        }
    }

    private sealed record RecordedReferenceCall(
        int CallNumber,
        int PerQuestionIndex,
        string QuestionId,
        string Prompt,
        string ModelId,
        string? Effort,
        string? Body,
        bool Failed);

    private sealed class RecordingReferenceRunner : IReferenceAnswerOperationRunner
    {
        private readonly object gate = new();
        private readonly Dictionary<string, int> perQuestion = new(StringComparer.Ordinal);
        private int callNumber;

        internal RecordingReferenceRunner(int startCall = 0)
        {
            callNumber = startCall;
        }

        internal List<RecordedReferenceCall> Calls { get; } = [];

        internal HashSet<int> FailCalls { get; } = [];

        internal HashSet<int> QuotaCalls { get; } = [];

        internal Dictionary<int, string> BodyOverride { get; } = [];

        internal Func<RecordedReferenceCall, bool>? FailWhen { get; set; }

        internal int? CancelAtCall { get; set; }

        internal CancellationTokenSource? Cancellation { get; set; }

        internal static string DefaultBody(int call) => $"REFBODY-{call:D3}-synthetic reference text";

        public Task<AuxiliaryOperationResult<ReferenceAnswerResult>> EvaluateAsync(
            SafeReferenceAnswerPayload payload,
            CancellationToken cancellationToken) =>
            EvaluateAsync(payload, "<unspecified>", null, cancellationToken);

        public Task<AuxiliaryOperationResult<ReferenceAnswerResult>> EvaluateAsync(
            SafeReferenceAnswerPayload payload,
            string modelId,
            string? reasoningEffort,
            CancellationToken cancellationToken)
        {
            int n;
            int perQ;
            lock (gate)
            {
                n = ++callNumber;
                perQ = perQuestion[payload.QuestionId] = perQuestion.GetValueOrDefault(payload.QuestionId) + 1;
            }

            string body = BodyOverride.TryGetValue(n, out string? o) ? o : DefaultBody(n);
            RecordedReferenceCall probe = new(n, perQ, payload.QuestionId, payload.RenderedPrompt, modelId, reasoningEffort, body, false);
            AuxiliaryOperationResult<ReferenceAnswerResult> result;
            bool failed = true;
            if (CancelAtCall == n)
            {
                Cancellation?.Cancel();
                result = AuxiliaryOperationResult<ReferenceAnswerResult>.Failed(ResultsStatusCodes.Cancelled);
            }
            else if (QuotaCalls.Contains(n))
            {
                result = AuxiliaryOperationResult<ReferenceAnswerResult>.Failed(ResultsStatusCodes.QuotaExhausted);
            }
            else if (FailCalls.Contains(n) || FailWhen?.Invoke(probe) == true)
            {
                result = AuxiliaryOperationResult<ReferenceAnswerResult>.Failed(ResultsStatusCodes.AiRuntimeFailed);
            }
            else
            {
                failed = false;
                result = AuxiliaryOperationResult<ReferenceAnswerResult>.Succeeded(
                    new ReferenceAnswerResult { QuestionId = payload.QuestionId, Answer = body });
            }

            lock (gate)
            {
                Calls.Add(probe with { Failed = failed, Body = failed ? null : body });
            }

            return Task.FromResult(result);
        }
    }

    private sealed class RecordingNormalRunner : IEvaluationRunner
    {
        internal ConcurrentBag<string> ModelIds { get; } = [];

        public Task<EvaluationRunnerResult> EvaluateAsync(SafeEvaluationPayload payload, string modelId, CancellationToken cancellationToken)
        {
            ModelIds.Add(modelId);
            return Task.FromResult(EvaluationRunnerResult.Succeeded(new QuantificationResult
            {
                EvaluatorId = payload.EvaluatorId,
                Criteria = payload.ExpectedCriteria.Select(c => new CriterionQuantificationResult
                {
                    CriterionId = c.CriterionId,
                    RawScore = 6m,
                    Reason = "synthetic",
                    Evidence = payload.PrimarySource.Value,
                    EvidenceSource = EvidenceSourceKind.PrimaryAnswer,
                    EvidenceSourceColumnId = payload.PrimarySource.SourceColumnId,
                }).ToImmutableArray(),
            }));
        }
    }

    /// <summary>実 CheckpointStore を包み、保存内容の JSON を記録し、指定した内容を含む Update だけを失敗させる。</summary>
    private sealed class RecordingCheckpointStore : ICheckpointStore
    {
        private readonly CheckpointStore inner = new();

        internal string? LastSavedJson { get; private set; }

        internal string? FailUpdateWhenJsonContains { get; init; }

        public CheckpointSaveResult Create(CheckpointEnvelope envelope, CancellationToken cancellationToken)
        {
            CheckpointSaveResult r = inner.Create(envelope, cancellationToken);
            if (r.IsSuccess)
            {
                LastSavedJson = Reload(envelope.PartialPath);
            }

            return r;
        }

        public CheckpointSaveResult Update(CheckpointEnvelope envelope, CancellationToken cancellationToken)
        {
            string json = JsonSerializer.Serialize(envelope);
            if (FailUpdateWhenJsonContains is not null && json.Contains(FailUpdateWhenJsonContains, StringComparison.Ordinal))
            {
                return CheckpointSaveResult.Failed(CheckpointStatusCodes.SaveFailed);
            }

            CheckpointSaveResult r = inner.Update(envelope, cancellationToken);
            if (r.IsSuccess)
            {
                LastSavedJson = Reload(envelope.PartialPath);
            }

            return r;
        }

        public CheckpointLoadResult Load(string partialPath, CancellationToken cancellationToken) =>
            inner.Load(partialPath, cancellationToken);

        private string? Reload(string partialPath)
        {
            CheckpointLoadResult l = inner.Load(partialPath, CancellationToken.None);
            return l.IsSuccess ? JsonSerializer.Serialize(l.Envelope) : null;
        }
    }

    /// <summary>合成 workbook（設問ごとの回答列＋補助列＋氏名列）と durable run の足場。</summary>
    private sealed class Fixture : IDisposable
    {
        internal const string SupportCanary = "SUPPORT-CANARY-115";
        internal const string StudentNameCanary = "STUDENT-NAME-CANARY-115";
        internal const string PromptCanary = "EVAL-PROMPT-CANARY-115";
        internal const string CriterionCanary = "CRITERION-CANARY-115";

        private readonly int questionCount;
        private readonly int studentRows;
        private readonly bool disabledQuestion;

        internal Fixture(int questionCount, int studentRows, bool disabledQuestion)
        {
            this.questionCount = questionCount;
            this.studentRows = studentRows;
            this.disabledQuestion = disabledQuestion;
            Root = Path.Combine(Path.GetTempPath(), "SRE-ST-FR115-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            InputPath = Path.Combine(Root, "input.xlsx");
            OutputDirectory = Path.Combine(Root, "out");
            Directory.CreateDirectory(OutputDirectory);
            Recreate();
        }

        internal string Root { get; }

        internal string InputPath { get; }

        internal string OutputDirectory { get; }

        internal string InputSha256 { get; private set; } = string.Empty;

        internal Func<int, string, string?>? StudentAnswerOverride { get; set; }

        private int TotalQuestions => questionCount + (disabledQuestion ? 1 : 0);

        internal static string QuestionText(string questionId) => $"QUESTION-TEXT-{questionId}-再帰を説明せよ";

        internal static string StudentAnswer(int row, string questionId) => $"STUDENT-ANSWER-{questionId}-ROW{row}";

        /// <summary>AC-009: 参照生成の比較入力に混入してはならない文字列。</summary>
        internal static IEnumerable<string> ForbiddenInReferencePrompt(string questionId)
        {
            yield return "STUDENT-ANSWER-";
            yield return SupportCanary;
            yield return StudentNameCanary;
            yield return PromptCanary;
            yield return CriterionCanary;
            yield return "REFBODY-";
            foreach (string other in new[] { "Q1", "Q2", "Q3", "Q4" }.Where(q => q != questionId))
            {
                yield return QuestionText(other);
            }
        }

        internal void Recreate()
        {
            if (File.Exists(InputPath))
            {
                File.Delete(InputPath);
            }

            CreateWorkbook();
            InputSha256 = Sha256(InputPath);
        }

        internal QuantificationDefinition Definition() =>
            new()
            {
                Id = "DEF-ST115",
                Name = "FR-115 synthetic definition",
                Revision = "1",
                SourceSheet = "Original",
                HeaderRow = 1,
                FirstDataRow = 2,
                LastDataRow = 1 + studentRows,
                BasePoints = 100m - (2m * questionCount),
                RoundingDigits = 1,
                Questions = Enumerable.Range(1, TotalQuestions).Select(i => new QuestionDefinition
                {
                    Id = "Q" + i,
                    DisplayName = "Question Q" + i,
                    QuestionText = QuestionText("Q" + i),
                    PrimarySourceColumn = Column(i),
                    SupportingSourceColumns = [Column(TotalQuestions + 1)],
                    Points = 2m,
                    Enabled = i <= questionCount,
                    Evaluators =
                    [
                        new EvaluatorDefinition
                        {
                            Id = "E1Q" + i,
                            DisplayName = "Evaluator E1",
                            Type = EvaluatorType.CustomPrompt,
                            Weight = 1m,
                            Range = new ScoreRange(0m, 10m),
                            CustomPromptTemplate = PromptCanary + " {回答} {評価項目}",
                            Enabled = true,
                            Criteria =
                            [
                                new CriterionDefinition
                                {
                                    Id = "C1Q" + i,
                                    DisplayName = "Criterion C1",
                                    Description = CriterionCanary,
                                    Weight = 1m,
                                    Enabled = true,
                                },
                            ],
                        },
                    ],
                }).ToImmutableArray(),
            };

        internal async Task<RunSummary> RunAsync(
            IReferenceAnswerOperationRunner refs,
            IEvaluationRunner normal,
            string modelId = ModelId,
            ICheckpointStore? store = null,
            string? resumePartialPath = null,
            Action<DurableEvaluationProgress>? progress = null,
            CancellationTokenSource? cancel = null)
        {
            DurableQuantificationOrchestrator orchestrator = new(
                new OpenXmlEvaluationRowSource(InputPath),
                normal,
                refs,
                new NoSpecialRunner(),
                new PhysicalInputSnapshotBoundary(),
                store ?? new CheckpointStore(),
                new OutputPathPlanner(),
                new WorkbookDurableRunFinalizer(),
                new PhysicalPartialCheckpointCleaner());
            return await orchestrator.RunAsync(
                new DurableQuantificationRunRequest
                {
                    Run = RunRequest(modelId),
                    Runtime = Runtime(),
                    OutputDirectory = OutputDirectory,
                    ResumePartialPath = resumePartialPath,
                },
                progress,
                cancel?.Token ?? TestContext.Current.CancellationToken);
        }

        /// <summary>FR-115 以前の形式（設問ごとに単独の参照、追加条件欄なし）の checkpoint を作る。</summary>
        internal string CreateLegacyCheckpoint(int schemaVersion, string q1Answer)
        {
            QuantificationSnapshot snapshot = QuantificationSnapshot.Create(Definition());
            OutputPathReservation reservation = new OutputPathPlanner().Reserve(InputPath, DateTimeOffset.Now, OutputDirectory);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            CheckpointEnvelope envelope = new()
            {
                SchemaVersion = schemaVersion,
                InputPath = Path.GetFullPath(InputPath),
                Input = new PhysicalInputSnapshotBoundary().Capture(InputPath),
                DefinitionCanonicalJson = snapshot.CanonicalJson,
                DefinitionSha256 = snapshot.Sha256,
                NormalModelId = ModelId,
                ReferenceModelId = ModelId,
                ReasoningEffort = Effort,
                Runtime = Runtime(),
                FinalPath = reservation.FinalPath,
                PartialPath = reservation.PartialPath,
                StartedAtUtc = now,
                SavedAtUtc = now,
                References =
                [
                    new CheckpointReference
                    {
                        QuestionId = "Q1",
                        Answer = q1Answer,
                        StatusCode = ResultsStatusCodes.Success,
                        GeneratedAtUtc = now,
                        AttemptCount = 1,
                    },
                ],
            };
            CheckpointSaveResult created = new CheckpointStore().Create(envelope, CancellationToken.None);
            if (!created.IsSuccess && !File.Exists(reservation.PartialPath))
            {
                // 不正な形式を store が書けない場合は、旧 schema で書いてから schema 値だけを壊す
                CheckpointSaveResult fallback = new CheckpointStore().Create(envelope with { SchemaVersion = 2 }, CancellationToken.None);
                Assert.True(fallback.IsSuccess);
                CorruptSchemaVersion(reservation.PartialPath, schemaVersion);
            }

            return reservation.PartialPath;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void CorruptSchemaVersion(string partialPath, int schemaVersion)
        {
            using SpreadsheetDocument doc = SpreadsheetDocument.Open(partialPath, true);
            foreach (SharedStringItem item in doc.WorkbookPart!.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>() ?? [])
            {
                foreach (Text t in item.Descendants<Text>())
                {
                    t.Text = Regex.Replace(t.Text, "\"SchemaVersion\"\\s*:\\s*2", $"\"SchemaVersion\":{schemaVersion}");
                }
            }

            foreach (WorksheetPart ws in doc.WorkbookPart.WorksheetParts)
            {
                foreach (Text t in ws.Worksheet!.Descendants<Text>())
                {
                    t.Text = Regex.Replace(t.Text, "\"SchemaVersion\"\\s*:\\s*2", $"\"SchemaVersion\":{schemaVersion}");
                }
            }
        }

        private static string Column(int index) => ((char)('A' + index - 1)).ToString();

        private static CheckpointRuntimeIdentity Runtime() => new()
        {
            ApplicationIdentity = "StudyReportEvaluator.App/4.0.0",
            CliVersion = "1.0.79",
            CliSha256 = new string('A', 64),
            SdkInformationalVersion = "1.0.11",
        };

        private QuantificationRunRequest RunRequest(string modelId) => new()
        {
            DraftDefinition = Definition(),
            WorkbookMetadata = new WorkbookMetadataReader().Read(InputPath),
            InputPath = InputPath,
            ModelId = modelId,
            ReasoningEffort = Effort,
            MaximumPromptTokens = 64_000,
            MaximumContextWindowTokens = 128_000,
            MaxConcurrency = 1,
            UseDurableWorkflow = true,
        };

        private void CreateWorkbook()
        {
            using SpreadsheetDocument document = SpreadsheetDocument.Create(InputPath, SpreadsheetDocumentType.Workbook);
            WorkbookPart workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            DocumentFormat.OpenXml.Spreadsheet.Sheets sheets = workbookPart.Workbook.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Sheets());
            WorksheetPart part = workbookPart.AddNewPart<WorksheetPart>();
            int supportCol = TotalQuestions + 1;
            int nameCol = TotalQuestions + 2;
            SheetData data = new();
            Row header = new() { RowIndex = 1 };
            for (int c = 1; c <= nameCol; c++)
            {
                header.Append(Inline(Column(c) + "1", c <= TotalQuestions ? "Answer Q" + c : c == supportCol ? "Support" : "Name"));
            }

            data.Append(header);
            for (int r = 2; r <= 1 + studentRows; r++)
            {
                Row row = new() { RowIndex = (uint)r };
                for (int c = 1; c <= TotalQuestions; c++)
                {
                    string q = "Q" + c;
                    row.Append(Inline(Column(c) + r, StudentAnswerOverride?.Invoke(r, q) ?? StudentAnswer(r, q)));
                }

                row.Append(Inline(Column(supportCol) + r, SupportCanary));
                row.Append(Inline(Column(nameCol) + r, StudentNameCanary + r));
                data.Append(row);
            }

            part.Worksheet = new Worksheet(new SheetDimension { Reference = $"A1:{Column(nameCol)}{1 + studentRows}" }, data);
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(part), SheetId = 1, Name = "Original" });
            workbookPart.Workbook.Save();
        }

        private static Cell Inline(string reference, string value) => new()
        {
            CellReference = reference,
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(value)),
        };
    }

    private sealed class NoSpecialRunner : ISpecialEvaluationOperationRunner
    {
        public Task<AuxiliaryOperationResult<SpecialQuantificationResult>> EvaluateAsync(
            SafeSpecialEvaluationPayload payload,
            string modelId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The FR-115 synthetic definition has no special evaluation.");
    }

    /// <summary>完成 workbook の表示値（共有文字列・inline・数値）を sheet 名ごとに読む。</summary>
    private sealed class OutputBook
    {
        private readonly Dictionary<string, List<List<string>>> rows = new(StringComparer.Ordinal);

        private bool hasFormula;
        private readonly List<string> formulas = [];

        internal static OutputBook Read(string path)
        {
            OutputBook result = new();
            using SpreadsheetDocument doc = SpreadsheetDocument.Open(path, false);
            WorkbookPart wb = doc.WorkbookPart!;
            SharedStringItem[] shared = wb.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().ToArray() ?? [];
            foreach (Sheet sheet in wb.Workbook!.Descendants<Sheet>())
            {
                WorksheetPart part = (WorksheetPart)wb.GetPartById(sheet.Id!.Value!);
                List<List<string>> sheetRows = [];
                foreach (Row row in part.Worksheet!.Descendants<Row>())
                {
                    List<string> values = [];
                    foreach (Cell cell in row.Elements<Cell>())
                    {
                        if (cell.CellFormula is not null)
                        {
                            result.hasFormula = true;
                            result.formulas.Add(cell.CellFormula.Text ?? string.Empty);
                        }

                        string value = cell.DataType?.Value == CellValues.SharedString
                            && int.TryParse(cell.CellValue?.Text, out int i) && i < shared.Length
                                ? shared[i].InnerText
                                : cell.DataType?.Value == CellValues.InlineString
                                    ? cell.InlineString?.InnerText ?? string.Empty
                                    : cell.CellValue?.Text ?? string.Empty;
                        values.Add(value);
                    }

                    sheetRows.Add(values);
                }

                result.rows[sheet.Name!.Value!] = sheetRows;
            }

            return result;
        }

        internal IReadOnlyList<string> Sheet(string name) => rows[Resolve(name)].SelectMany(r => r).ToArray();

        internal int RowsWithCell(string sheet, string exact) =>
            rows[Resolve(sheet)].Skip(1).Count(r => r.Contains(exact, StringComparer.Ordinal));

        /// <summary>AC-084 の References / Results / Run sheet（アプリ所有の接頭辞付きの名前を許容）。</summary>
        private string Resolve(string name)
        {
            string? key = rows.Keys.FirstOrDefault(k => k == name)
                ?? rows.Keys.FirstOrDefault(k => k.EndsWith("_" + name, StringComparison.Ordinal));
            Assert.True(key is not null, $"sheet '{name}' がない（実在: {string.Join(", ", rows.Keys)}）");
            return key!;
        }

        internal bool HasFormulaContaining(string token) =>
            hasFormula && formulas.Any(f => f.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}

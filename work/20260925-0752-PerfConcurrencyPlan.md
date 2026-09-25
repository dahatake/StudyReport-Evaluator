# 2026-09-25 性能・並列化実装計画

## 目的

`feat/perf-concurrency`では、採点run全体の壁時計時間を短縮しつつ、Copilot SDKへの呼び出し条件をrun内で固定し、不要なtoken消費と再試行を抑える。対象は通常評価、参照回答、特別評価、既存の類似度評価境界である。類似度の算出方式やreasoning effort選択規則は他作業の担当範囲なので変更しない。

## 現状確認

- `SdkEphemeralCopilotTransportFactory.Create()`ごとに`CopilotClientFactory.CreateAsync`、同梱CLIの検証、`CopilotClient.StartAsync`、`GetAuthStatusAsync`、`ListModelsAsync`が発生する。`CreateSessionAsync`内の`ResolveReasoningEffort`は毎attemptでmodel一覧を取得している。
- `DurableQuantificationOrchestrator`は参照回答を先に保存した後、学生行をsource row昇順に1行ずつ処理する。`DurableEvaluationScheduler`は1行内のnormal/special/similarityだけを`SemaphoreSlim(maxConcurrency)`で並列化する。
- checkpoint schemaは`CompletedRows`の配列を持つ。resume admissionと`RunAsync`は現在`completedRows.Count`を完了済みprefixとして扱うため、未完了の穴を永続化しない設計が安全である。
- SDK 1.0.11のXML docでは`SessionErrorData.ErrorType`に`rate_limit`、`quota`があり、`ErrorCode`に`user_global_rate_limited`、`user_model_rate_limited`、`rate_limited`、`integration_rate_limited`、`quota_exceeded`等が入る。

## 実装方針

### 1. run共有Copilot client

- `SharedCopilotClientPool`を追加し、production runではnormal/reference/special/similarityの全transport factoryへ同一poolを渡す。
- poolは最初のattemptでCLI processを起動し、認証状態と`ListModelsAsync`結果をrun内cacheする。各attemptは従来どおり新しいsession ID、同じsession config制約、tool制約、permission denial、`DeleteSessionAsync`を使う。
- `ResolveReasoningEffort`は既存関数のまま残し、cached model listを渡すだけにする。reasoning effortの値や選択条件は変更しない。
- transportのattempt後始末では共有clientをstop/disposeしない。run終了・取消・例外時に`await using`でpoolをdisposeする。
- CLI process死亡やtransport切断が疑われる例外ではpoolをinvalidateし、次attemptでclientを再作成する。rate limit/quotaはprocess異常ではないためinvalidateしない。

### 2. rate limit / quota対応

- `SessionErrorEvent`を購読し、`errorType=rate_limit`を`RATE_LIMITED`、`errorType=quota`を`QUOTA_EXHAUSTED`へ分類する。SDKが`InvalidOperationException("Session error: ...")`だけを投げる場合のmessage fallbackも持つ。
- rate limitは既存のtransient attempt上限（最大3attempt）内で指数backoff＋jitterを入れて再試行する。SDK eventにretry-after相当が得られた場合はその値以上を尊重するseamを残す。quota exhaustedは再試行せず、runをcleanに終了させる。
- `AdaptiveEvaluationConcurrencyLimiter`を導入し、rate limit観測時はAIMDのmultiplicative decrease（概ね半減、下限1）、成功が続くとadditive increase（1ずつ、利用者設定上限まで）を行う。

### 3. row-level pipelining

- durable row処理は最大`MaxConcurrency`行を先行launchする。ただしAI operationは全行共有のglobal adaptive limiterを通すため、同時attempt数は利用者設定値を超えない。
- checkpointはsource row順の連続prefixだけを保存する。後続行が先に完了しても、前の行が保存されるまでmemoryに保持し、永続化順序と出力順序を決定的にする。
- crash時は永続化済みprefix以降（最大pipeline幅分）が再実行され得る。従来の「最大1行再実行」より広がるが、壁時計短縮を優先し、checkpointの穴を作らないことでresume correctnessを維持する。

## 並列度の既定値と上限

- 既定値は **4**、UI/設定上限は **8** とする。
- 共有CLIにより10〜25秒のprocess起動・認証・model列挙がattemptごとに乗らなくなるため、server側のper-user rate limitが主な制約になる。典型workload（40〜200名 × 3〜5問）では、1attemptの応答待ちが数十秒でも4並列なら行間の空きslotを埋められる。
- 8を上限にするのは、大規模定義で利用者が短時間runを選べる余地を残しつつ、rate limit時にAIMDが速やかに4→2→1へ落とせる範囲に抑えるためである。既定8はrate limitとtoken burstのリスクが高く、既定3以下は少問・多行workloadでslotが空きやすい。
- ログイン済み実CLIでの小規模実測は、他agentとの並行作業中に実student dataを避けるため必須にしない。可能ならsynthetic fixtureで1〜2行のみ確認し、未実測なら上記推論として記録する。

## 検証

- fake transport単体テストで、共有poolが`Start`/`ListModels`をattemptごとに繰り返さないこと、rate limitがbackoff付きretryとdistinct statusになること、quotaが非retryになることを確認する。
- durable orchestrator testで複数行がin-flightになり、checkpoint更新はsource row prefix順になることを確認する。
- `dotnet build .\StudyReportEvaluator.slnx -c Release`、`dotnet test .\StudyReportEvaluator.slnx -c Release`を実行し、失敗は修正または既知flakyとして証跡を残す。

## 統合時の改訂（2026-09-26）

- reasoning effortはrun開始時に1回だけ解決する方式（`work/20260925-0828-UniformReasoningEffortPlan.md`）になったため、attemptごとの`ListModelsAsync`は不要になった。共有poolのmodel一覧cacheは使われなくなったので削除した。
- 類似度はローカル計算になり、共有clientを使うLLM operationは参照回答・通常評価・固有評価の3種になった。
- 行pipelineの窓は「実行中の行＋完了済みだがcheckpoint未保存の行」の合計で並列度以下に制限した（当初は実行中の行だけを数えており、前の行が遅いと未保存の行が際限なく増えた）。crash時の再実行は最大で並列度分の行に収まる。
- 並列度は合成Promptの実測（`work/20260926-0020-ConcurrencyAndEffortMeasurement.md`）で、並列8でスループットが頭打ちになったため、**既定8・上限16**へ改めた（上記「並列度の既定値と上限」の既定4・上限8を置き換える）。


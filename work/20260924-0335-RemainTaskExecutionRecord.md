# `work/` 残タスク実行記録

- 計画: [202609240300-RemainTaskExecutionPlan.md](202609240300-RemainTaskExecutionPlan.md)
- 第2ラウンド（未完了タスクの解決）の記録: [20260924-0725-IncompleteTaskResolution.md](20260924-0725-IncompleteTaskResolution.md)。下の「実施しなかったタスク」の表は第1ラウンド時点のもので、各タスクの最新の状態は第2ラウンドの記録にある
- 開始: 2026-09-24 03:35 JST。基準 HEAD `d27dc0ee03b05ddaadf060c9285e2517fb0f686a`（`origin/main` と一致）。ホスト `DAHATAKE-STD2`、.NET SDK 10.0.401（CI は 10.0.400）
- 並行作業は、同じ HEAD から作った detached worktree（`C:\GitHub\sre-wt-*`）で行い、差分を patch にしてこの作業ツリーへ取り込んだ。同じ workspace を同時に編集していない
- TRX はリポジトリの外（セッション領域 `files\trx\`）に保存し、SHA-256 をここに記録する
- Phase 0 の所有者確認（最小テスト方針は今も有効か）は未回答。そのため計画の既定どおり、RT-20 の再 package・実 AI・公開・commit・push は行わない

## 再現（RT-02 手順 1）

`repro-baseline.trx`（`F2F0BB67E12204735DA94056D164CD99A3515EA34916A5F596F46BB6E96602E5`）: Release、12 クラスの合計 269 件のうち **29 件が失敗**。CI run 35275935192 の A 群 4 件と B 群 25 件に、テスト名で一致した。

## RT-01 単一 EXE profile の契約 — 完了

- テストの契約を今の profile に合わせた。profile 自体は変更していない。
  - `declares_…_once`: 閉じた集合に `RuntimeFrameworkVersion=10.0.11` を加え、13 項目の完全一致を検査する
  - `scopes_…`: 要素の並びが `[PropertyGroup, ItemGroup, ItemGroup]` と完全一致すること、固定用 group の中身が `KnownILLinkPack` 1 つだけであることを検査する。値（10.0.11 など）は既存の `WindowsSingleFilePublishTests.Single_file_profile_pins_runtime_and_ILLink_only_for_the_App` が検査しているので、重複させていない
  - allowlist の検査（`registers_…`、`DocumentationContractTests.Package_script_…`）: Content 用の group は「`Content` を 1 つでも含む ItemGroup がちょうど 1 つ」として特定し、子要素がすべて `Content` であることを検査する。ほかの group に紛れ込んだ `Content` も検出できる
- ADR-0016 の Decision 1-2 に、2 つの固定を入れた理由（SDK 10.0.401 が暗黙に ILLink 10.0.12 を選ぶ。D4 の手順 2〜3）を追記した
- `rt01.trx`（`3D4E8ECC2F382E6C025C051B5CADCC2B48F3633C90F8E3E482CF2AAD65262561`）: `WindowsSingleFile*` と `DocumentationContractTests` で 134 件中、成功 126・失敗 0・スキップ 8。スキップはすべて opt-in の実成果物テスト（`RUN_WINDOWS_SINGLEFILE_PACKAGE_TESTS`／`RUN_WINDOWS_SINGLEFILE_ARTIFACT_TESTS` が未設定。RT-20 の範囲）
- 敵対的レビュー: 指摘 0 件

## RT-04 D1 への訂正の注記 — 完了

- D1 の表の下に「訂正の注記」を追記した。状態欄は書き換えていない。ea91d9f で変更されたのは、`tests/StudyReportEvaluator.App.Tests/` 配下の 17 ファイルと `tests/SystemTest-prompt.md` の 1 ファイル（`git show --name-only ea91d9f` で確認）
- 敵対的レビュー: 指摘 0 件

## RT-13 `ResumeAdmissionEvaluatorTests` の拡張 — 完了

- 追加したテスト: 6 項目について、単独の不一致を 1 項目ずつ試す Theory（5 件。Definition は既存テストで試している）と、複数の不一致が重なったときの優先順位（入力 → 採点設計 → モデル → runtime → 構造）のテスト。優先順位は `730a225:DurableQuantificationOrchestrator.ValidateResumeAdmission` の判定順と一致する
- 回答、Prompt、reason、evidence の canary が説明文と表示文に入らないこと、`ToString` が内容を伏せる（redacted）ことを assert した
- 敵対的レビューの指摘 2 件（runtime と構造の優先順位を検査していない／Definition 不一致テストの内容検査が弱い）と提案 1 件（reason/evidence の canary）を反映した
- `rt13b.trx`（`7F1B58DE5627828FDE614B6CF1D591B2C13ED53569B1425D23E740419E11A2AE`）: `ResumeAdmissionEvaluatorTests` と `DurableQuantificationOrchestratorTests` の 20 件が成功、失敗 0。`DurableQuantificationOrchestratorTests` は変更していない
## RT-14 T-G／T-I の取りこぼし — 完了

- T-G の 4 点:
  1. Close を繰り返しても待機を飛ばさない → 既存の `Repeated_window_close_waits_for_run_completion`
  2. 閉じるのは内部 Close だけ → 同じテスト（外部からの 2 回の Close では閉じず、drain の後に閉じる）
  3. 待機は最大 10 秒で、過ぎたら閉じる → 追加した 2 件。`Window_close_during_an_unresponsive_run_closes_after_the_ten_second_bound`（run が止まらないまま window を閉じると、9〜15 秒の間に閉じ、その時点で run はまだ終わっていない）と、`Drain_with_an_unbounded_timeout_still_returns_within_the_finite_bound`（`InfiniteTimeSpan` を渡しても 9〜15 秒で戻る）
  4. 実行していなければすぐに閉じる → 追加した `Window_close_without_a_run_closes_immediately`
- T-I: [SystemTest-prompt.md](../tests/SystemTest-prompt.md) の ST-UC-17 に、部分ケース ST-UC-17-R を別の期待値で追加した。既存の固定 oracle は変えていない
- [traceability.md](../dev/docs/traceability.md) の AC-038 行の件数を、今の件数（ResumeWorkflowTests 14 件、ResumeAdmissionEvaluatorTests 8 件）に合わせた。216 行目（TR-37）には件数が書かれていないので、変更していない
- 敵対的レビューの指摘 2 件（10 秒の上限と、その後に閉じることを assert していない／ST-UC-17-R の記述がテストの実際より強い）を反映した
- `ResumeWorkflowTests` 14 件と `DocumentationContractTests` 22 件がすべて成功。全体回帰（RT-03）でも成功した

## RT-06 AIクレジットの数値表示（G2） — 資料確認完了、判断は BLOCKED のまま

2026-09-24 に取り直した資料:

| ID | URL | 該当箇所 | 判定 |
|---|---|---|---|
| W1 | https://github.com/github/copilot-sdk/blob/main/docs/features/usage-and-billing.md （`v1.0.11` の tag でも同じ文面） | "The exact conversion to AI credits … are defined by GitHub Copilot billing, not by the SDK … The examples divide by `1e9` as a convenience … confirm this matches current billing before relying on it." | 換算を課金資料に委ねている |
| W2 | https://docs.github.com/en/copilot/concepts/billing-and-usage/individuals/billing | "1 AI credit = $0.01 USD" | nano-AIU への言及なし |
| W3 | https://docs.github.com/en/copilot/concepts/billing-and-usage/organizations-and-enterprises/billing | "the total is converted into AI credits, where 1 AI credit = $0.01 USD." | nano-AIU への言及なし |
| W4 | https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing | "billed in GitHub AI Credits at the per-token rates … (1 AI credit = $0.01 USD)" | nano-AIU への言及なし |

- 結論: `TotalNanoAiu` を AI クレジットへ換算する数値は、公式資料には**明記されていない**。SDK のテスト fixture（`creditsUsed: 1.25` と `1250000000`）から 1e9 倍と推測はできるが、正式な根拠ではない。ADR-0018 の「換算規則を課金正本で確認できるまで表示しない」に従い、**BLOCKED のまま**とする。同梱 CLI 1.0.79 自身の文書は確認していない

## RT-02 `d27dc0e`で新たに出た25件 — 完了

detached worktreeを3つ使い、B1・B2・B3を並行して調べた。3つのpatchをこの作業ツリーに統合したあと、影響する15クラスを実行した: `rt02-merged.trx`では298件中、失敗0（統合後にレビューの指摘を反映したので、該当クラスは再実行した）。

| 群 | テスト | 判断 | 根拠 | 変更 |
|---|---|---|---|---|
| B1 | `Login_controls_…` | テストが仕様変更に追いついていない | D7の65行目「UI文言・状態表示を「中断」へ統一」、P2 | 期待値を`中断`に変えた。AutomationId `CancelQuantification`の検査は残した。TextBoxの検査は`CopilotLoginPanel`全体に対して行う元の形のまま（B2でpanelを分けたので成立する） |
| B1 | `Live_progress_…(cancelRun: True)` | テストが追いついていない | このテストのsummaryは`CreateSummaryAsync`で作る非durableのもので、partial checkpointがない。したがって`ExecutionViewModel.cs`の`Cancelled`（`HasInterruptedRun`でない場合）の文言「保存済み checkpoint はありません」が正しい。製品の不具合ではない | 期待する文言を変えた |
| B1 | `SettingsWorkflowSystemTests.Cancel_saves_…` | テストが追いついていない | 要求定義の920行目（AC-038「開始前に項目別検証」）、`ResumeWorkflowTests.An_interrupted_run_stays_on_the_execution_step_…` | 新しいinstanceで開始前検証（`PrepareResumeAsync`）を行い、全項目OKであることと、この時点でrun requestがないことをassertしてからStartするようにした。完了済みのAIとreferenceを繰り返さないという検証は残した。ヘルパーでの画面遷移は`StatusCode == Success`ならResults、それ以外はExecutionとassertする。テスト用のapp identityを実際の`QuantificationRunBoundary.ApplicationIdentity()`に変えた（開始前検証のruntime照合を通すため。不一致の検証は別のテストで残っている） |
| B1 | `ExecutionSettingsTests`の2件、`WorkflowStateTests`の2件 | テストが追いついていない | AC-038、ADR-0017の13行目、要求定義の918行目と966行目 | 受理される開始前検証の手順を加えた。`never_falls_back_from_a_missing_checkpoint`の意図（checkpointがなければ新規runに切り替えない）は残した |
| B2 | `ResponsiveLayoutTests`の4件、`CompactWorkflowLayoutTests`の3件 | **製品の不具合** | [ui-layout-contract.md](../dev/docs/ui-layout-contract.md)の51行目と55〜58行目（局所scrollで到達できること、切れないこと） | `ExecutionView.axaml`で、ログインと再開の領域を分けた。`CopilotLoginInstructions`を認証scrollの最後に置いた。ExecutionViewの局所既定を`TextTrimming=None`にし、`ResultsOutputView`の`ResultsSnapshotCaption`からellipsisを外した。テストの期待値は変えていない |
| B2 | `PrimaryJourneyAccessibilityTests`の1件 | **製品の不具合** | ui-layout-contract.mdの58行目（固定ナビ→内容→前後操作のTab順） | TabIndexを振り直した。再開元は、入力画面と同じ「path→参照…」の順（InputViewのFilePath 0 → PickFile 1）。これを固定するassertを追加した。D7のP3の実装メモ「既存のTabIndex 4→5の間へ入る」とは順番が逆になるが、既存のアクセシビリティ試験（`PrimaryJourneyAccessibilityTests`の128〜130行目。`d27dc0e`では変更されていない）と入力画面の慣例を優先した |
| B2 | `DocumentationScreenshotTests`の4件 | **製品の不具合** | `DocumentationScreenshotTests.cs`の461行目（`CopilotLoginPanel`にTextBoxがないこと）。非表示の`ResumePartialPathTextBox`と`ResumeFindingDetail`がpanelの中にあった | panelを分けたことで解消した。画像は再生成していない |
| B3 | `MainWindowTests`の5件 | **製品の不具合** | 要求定義の539・541・920行目、ADR-0017の13行目（再開準備は明示操作だけで行い、自動では変更しない） | `d27dc0e`で、再開モードでない構成変更でも`ValidationSummary`の変更通知が出るようになっていた。通知を再開モードのときだけに絞った。`ResumeValidationText`と`ResumeReport`の通知は従来どおり出す（レビューを反映） |
| B3 | `CopilotLoginCommandTests.Default_and_existing_…` | **製品の不具合**（API互換） | `CopilotLoginCommandTests.cs`の65行目が3引数のコンストラクタを要求している | 既存の3引数コンストラクタを戻した |

敵対的レビューでは、B2について1件（Tab順）、B1・B3について2件（`SetResumeValidationText`の早期returnで`ResumeReport`の変更通知が落ちる／画面遷移の不変条件）の指摘があり、すべて反映した。

## RT-05 D3の独立静的レビューの検討事項 — 完了（1件は所有者判断へ）

| 項目 | 判断 | 内容 |
|---|---|---|
| 1 証跡の出力先と入力の衝突 | 修正 | 証跡のwriterは`File.Move(..., overwrite: true)`で上書きするので、証跡の出力先が入力と同じパスなら、書き込む前に拒否するガードを加えた。パスはfull pathに正規化し、Windowsでは大文字小文字を区別せずに比べる。入力と同じディレクトリは許可する。opt-inが無効のままで動く決定的なテストを2件追加した |
| 2 GUI子プロセスを利用者環境から隔離する | **未修正（残るリスク。所有者の判断が必要）** | 製品は`Environment.GetFolderPath(LocalApplicationData)`（`ServiceRegistration.cs`の46〜48行目）で`setting.txt`の場所を決める。Windowsではこの呼び出しが`LOCALAPPDATA`環境変数を無視することを、子プロセスを使って確認した。そのため、環境変数を差し替えても隔離にならない。隔離するには、製品側に上書きの仕組みを加えるか、別のWindowsユーザーで実行する必要があり、RT-05の範囲を超える。実データのsmokeは既定で無効のまま |
| 3 失敗時のプロセス回収 | 修正 | GUI子プロセスを起動した後の例外経路を`finally`で回収するようにした。本体で例外が出ている場合は、後片付けの失敗でその例外を上書きしない。本体が成功している場合は、後片付けの失敗でテストを失敗させる |
| 4 C03のホスト検出 | 修正 | D3の指摘（`ReadToEnd`の後に`WaitForExit(10_000)`を呼んでいる）と一致した。読み取りを非同期にしてtimeoutの対象に入れた。killした後は、プロセスの終了と2つの読み取りタスクの完了を、上限を決めて待つ |

`RealDataSystemSmokeTests`と`ReleaseMatrixBuilderTests`: 23件成功・失敗0（worktreeで実行）。実データのsmokeは、opt-inが無効なので実データに触れる前に戻る。敵対的レビューでは3件の指摘があった（環境変数の差し替えが効いていない／後片付けが元の例外を隠す／kill後に回収していない）。すべて反映した（1件目は差し替えを取り消し、上の「未修正」として記録した）。

## RT-10 元プランのテスト計画と実際のテストの突き合わせ — 完了

敵対的レビューを2回行い、テストを強化した（C08はbarrierでモデル別の内訳を検査、C14はキューに積まれたPostを検査、など）。テストで示せなかったものは、PARTIALまたはNOT_RUNに分類し直した。**COVERED 12・PARTIAL 10・GAP→追加 1・NOT_RUN 2**。

注記: §10.1/D5 の `JobCostLoggerCanaryTests`、`CopilotUsageAdapterTests`、`JobLogWriterTests`、`JobCostPresentationTests`、`JobCostWorkflowTests` は提案名。実対応先は既存の `JobCostBackendTests`、`SdkUsageAdapterTests`、`UsageProvenanceTests`、`JobUsageTrackerTests`、`JobCostSnapshotTests`、`JobCostViewTests`、`CostAttemptLifecycleTests`、`RetryAndCleanupCoordinatorTests`、`EphemeralEvaluationRunnerTests`、`AuxiliaryEvaluationRunnerTests` にマップした。反射と 256-record/2秒前提の writer seam は既存テスト由来なので変更していない。

最終行数: COVERED 12 / PARTIAL 10 / ADDED 1 / NOT_RUN 2。

| ケース | 内容要約 | 対応テスト(クラス.メソッド) | 判定 COVERED/PARTIAL/GAP→ADDED/NOT_RUN | 根拠(assert要約) |
|---|---|---|---|---|
| C01 | token／nano-AI／premiumすべて取得 | JobUsageTrackerTests.Unknown_and_explicit_zero_are_distinct_and_partial_coverage_is_retained; SdkUsageAdapterTests.FromFinal_model_totals_replace_events_without_adding_model_prices | COVERED | JSON/LogText に input、nano-AI units、premium request の値・単位を assert。FinalRpc model totals と session totals を二重加算しない。 |
| C02 | creditだけ欠落／tokenだけ欠落／全欠落 | JobUsageTrackerTests.All_missing_metrics_remain_unknown_in_terminal_record; JobUsageTrackerTests.Unknown_and_explicit_zero_are_distinct_and_partial_coverage_is_retained; SdkUsageAdapterTests.FromEvent_preserves_explicit_zero_input_and_leaves_absent_input_unknown | PARTIAL | 全欠落 terminal JSON は Input/Output/Nano/Premium が Null、token absent と explicit 0 を区別。credit だけ欠落は AIクレジット数値を実装しない方針（ADR-0018:15 / requirements:658）のため NOT_RUN。 |
| C03 | 明示0・未送信・送信不明 | SdkUsageAdapterTests.FromEvent_preserves_explicit_zero_input_and_leaves_absent_input_unknown; JobCostSnapshotTests.Typed_snapshot_has_utc_times_immutable_breakdown_and_per_metric_coverage | COVERED | absent input は null、explicit 0 は 0。初期 snapshot の未観測時刻 null、未取得 metric は null/partial。 |
| C04 | `LastCall*`しかない | UsageProvenanceTests.Final_cost_is_complete_even_when_tokens_fall_back_to_last_call; SdkUsageAdapterTests.FromFinal_empty_model_metrics_uses_last_call_as_partial | COVERED | source=LastCall、partial=true、LastCall provenance を assert。イベント累計があれば LastCall token を置換。 |
| C05 | usage event重複・順序逆転・final複数通知 | JobUsageTrackerTests.Revisions_replace_even_when_lower_and_distinct_attempts_add | PARTIAL | 低い revision の逆転通知、完了後の高 revision、IsFinished=true 後の高 revision final duplicate を無視し二重加算しないことを assert。SDK event id 重複 handler 直叩きは未実施。 |
| C06 | 複数LLM呼出し＋最終metrics | SdkUsageAdapterTests.FromFinal_model_totals_replace_events_without_adding_model_prices; UsageProvenanceTests.Final_cost_is_complete_even_when_tokens_fall_back_to_last_call | COVERED | FinalRpc model totals を合算し、PreferFinal/MergeFinal でイベント・LastCall を二重加算しない。 |
| C07 | retry 2〜3attempt、失敗後成功 | CostAttemptLifecycleTests.Retries_share_generated_operation_id_and_report_actual_attempt_number_after_cleanup; JobUsageTrackerTests.Unobserved_retry_counts_without_manufacturing_zero_usage; JobUsageTrackerTests.Revisions_replace_even_when_lower_and_distinct_attempts_add | COVERED | retry lifecycle は attempt 1/2・同一 operationId・別 session・outcome を assert。未観測 retry は AttemptCount=2 だが未取得を 0 にしない。 |
| C08 | 並列3、全4operation種別 | JobUsageTrackerTests.Operation_breakdown_keeps_all_operations_separate_under_parallel_updates | GAP→ADDED | Barrier（30秒上限+TestContext cancellation）で3更新を同時解放し、4 operation の OperationBreakdown、総 input/nano、distinct model key と model totals を assert。 |
| C09 | 学生行途中取消／row未保存 | JobCostViewTests.Cancelled_run_keeps_terminal_cost_for_discarded_row_snapshot; JobUsageTrackerTests.Cancelled_job_keeps_observed_usage_for_unsaved_row; DurableEvaluationSchedulerTests.Cancellation_during_any_operation_discards_the_whole_in_progress_row | PARTIAL | tracker 単体は Cancelled 終端に観測値が残る。VM test は fake boundary が usage を製造し callback を直接呼ぶため、実 `QuantificationRunBoundary` の SDK usage wiring までは未検証。既存 scheduler test は discarded row の未保存だけを確認。 |
| C10 | timeout・Abort失敗・metrics失敗・cleanup失敗 | RetryAndCleanupCoordinatorTests.App_owned_timeout_terminates_even_when_attempt_ignores_its_token; RetryAndCleanupCoordinatorTests.Cleanup_failure_overrides_an_accepted_payload_and_attempts_later_cleanup_steps; CostAttemptLifecycleTests.Observation_exceptions_do_not_change_success_or_cleanup; AttemptOutcomeLoggingTests.Outcome_is_logged_once_without_changing_numeric_observations | COVERED | timeout は有限終了、cleanup failure は accepted を上書き、観測 hook 例外は成功/cleanup に影響せず、outcome は数値を変えない。 |
| C11 | summaryなし境界例外・出力／checkpoint失敗 | JobCostViewTests.Summaryless_boundary_failure_keeps_terminal_cost_in_view_model; JobCostSnapshotTests.Summaryless_terminal_failures_keep_observed_metrics_and_failure_reason; JobCostSnapshotTests.Backend_failure_codes_are_failed_in_terminal_record | PARTIAL | VM-level で boundary が cost callback 後に CHECKPOINT_SAVE_FAILED を throw しても Cost.Snapshot に input/output と `ジョブ終了（Failed）` が残る。terminal record は closed reason code を永続化せず `Completion=Failed` のみなので、OUTPUT_INVALID/CHECKPOINT_SAVE_FAILED の exact reason retention は未対応。 |
| C12 | checkpoint v1から再開 | JobUsageTrackerTests.Resume_context_logs_current_invocation_without_importing_previous_usage; DurableQuantificationOrchestratorTests.Resume_reuses_reference_and_completed_rows_then_runs_only_the_first_unfinished_row; DurableQuantificationOrchestratorTests.Real_partial_reopens_across_orchestrator_instances_and_resumes_only_the_unfinished_row | PARTIAL | cost JSON は IsResume=true、SchemaVersion=1、AggregationScope=CurrentInvocation、AttemptCount=1、今回値のみを assert。既存 Workflow は checkpoint の過去 row 再利用と未完了 row のみ実行を assert。合成 v1 checkpoint 内の過去 usage 値と cost tracker の非取込を同一公開APIで結合する test は未実施。 |
| C13 | 次回draft編集・画面往復・override／別名出力 | JobCostViewTests.Results_keep_completed_run_cost_when_execution_changes_and_clear_it_for_legacy_run | PARTIAL | Results 側 completed RunContext の Cost は execution 側 Reset/新 job Apply 後も不変。draft 編集・override/別名出力の全導線は cost test で未直接。 |
| C14 | 次ジョブ開始後の古い通知・VM dispose | JobCostViewTests.Stale_cost_notification_from_previous_run_cannot_replace_new_run_cost; JobCostViewTests.Disposed_execution_view_model_ignores_late_cost_callback; JobCostViewTests.Shared_view_preserves_paused_reader_detaches_and_binds_both_host_toggles | PARTIAL | 2回目 run 中の1回目 callback stale snapshot は同期経路で無視。Dispose 後の queued SynchronizationContext.Post は drain 後も Cost.Snapshot が更新されないことを assert。queued Post after new run は未検証。 |
| C15 | `auto`／報告モデルなし／複数モデル | JobCostModelTests.Tracker_replaces_then_groups_models_without_leaking_raw_identifiers; UsageProvenanceTests.Missing_model_cost_is_not_comparable_and_is_never_allocated; AuxiliaryEvaluationRunnerTests.Reference_runner_uses_auto_one_tool_and_complete_cleanup; AuxiliaryEvaluationRunnerTests.Special_runner_uses_selected_model_and_returns_zero_to_one_result | COVERED | requested auto/非auto と reported model を匿名 key で別管理。missing model cost は NotComparable で配賦しない。 |
| C16 | 小数premium・微小credit・大整数・異常値 | JobUsageTrackerTests.Unknown_and_explicit_zero_are_distinct_and_partial_coverage_is_retained; JobUsageTrackerTests.Overflow_and_negative_values_are_not_saturated_or_reported_as_zero; SdkUsageAdapterTests.FromEvent_rejects_negative_tokens_and_nan_cost_without_erasing_valid_fields; SdkUsageAdapterTests.FromFinal_rejects_negative_model_tokens_nan_nano_and_negative_premium_cost | PARTIAL | micro nano と 0.5 premium、overflow/負数/NaN の invalid 化は assert。AIクレジットの微小値は ADR-0018:15 / requirements:658 で表示しないため deterministic UI/log test 対象外（NOT_RUN: spec 外）。 |
| C17 | SDK非nullable値にJSON項目欠落 | SdkUsageAdapterTests.FromFinal_preserves_nullable_nano_zero_but_nonnullable_premium_zero_is_unknown | COVERED | TotalNanoAiu=0 は 0、非nullable premium 0 は unknown(null) として assert。 |
| C18 | session／モデル別／イベント値の不一致 | UsageProvenanceTests.Per_metric_sources_and_mismatch_reach_the_snapshot_and_the_log; UsageProvenanceTests.Model_cost_comparison_uses_one_final_rpc_only | COVERED | SourceCounts、Complete/ObservedAttemptCount、ModelCostMismatchCount、LogText/JSON の mismatch count を assert。 |
| L01 | 複数process／並列通知／終了flush | JobCostBackendTests.Parallel_trackers_create_unique_complete_logs_with_terminal_totals; JobCostBackendTests.Bounded_writer_counts_queue_capacity_and_io_drops_without_callback_interference | PARTIAL | Barrier（30秒上限+TestContext cancellation）の3 trackerで一意 LogPath、全 JSONL 行の JobId/revision/metric shape、terminal totals を parse/assert。observer notification delivery under parallelism と別 OS process 同時書込は未検証（NOT_RUN: 単体test内でプロセス分離を追加しない方針）。 |
| L02 | permission拒否・disk full・queue飽和・容量上限 | JobUsageTrackerTests.Writer_failure_does_not_erase_observations_or_throw; JobCostBackendTests.Bounded_writer_counts_queue_capacity_and_io_drops_without_callback_interference | PARTIAL | blocker path 保存失敗でも SummaryText に入力値が残り、LogStatusText が保存失敗。queue/容量上限は dropped count と final 欠落数を assert。実 disk full/ACL は外部環境前提のため NOT_RUN。 |
| L03 | Prompt／回答／path／credential canary・改行モデル名 | JobUsageTrackerTests.Jsonl_contains_no_presentation_path_or_arbitrary_completion_text; JobCostBackendTests.Reader_keeps_last_200_safe_lines_and_marks_partial_tail_without_echoing_fields; JobCostModelTests.Final_models_are_bounded_anonymous_and_independent_of_authoritative_total | COVERED | PRIVATE_PROMPT/ghp/path/SummaryText/LogText/LogPath を JSONL から排除。forged newline と raw model id も safe render/匿名 key で排除。 |
| L04 | 強制終了想定の途中行・終端なし | JobCostBackendTests.Reader_keeps_last_200_safe_lines_and_marks_partial_tail_without_echoing_fields; JobCostBackendTests.Reader_rejects_foreign_identity_invalid_enum_and_oversized_line | COVERED | unfinished JSON 追記で IsIncomplete/HasInvalidRecords=true、既存 safe lines のみ維持。foreign identity/invalid enum/oversized line も invalid/truncated。 |
| L05 | 30日保持／別process稼働／削除拒否 | JobCostBackendTests.Retention_deletes_only_old_owned_unlocked_logs_and_warns_independently_on_failure; JobCostBackendTests.Symlink_is_not_followed_by_reader_or_retention_when_creation_is_available | COVERED | old owned は削除、active handle/read-only/arbitrary/unrelated/current は残る。symlink は権限なし環境では expected skip（external privilege NOT_RUN）。 |
| U01 | 全表示状態・大桁数・長いモデル名 | JobCostViewTests.Shared_view_preserves_paused_reader_detaches_and_binds_both_host_toggles（一部） | NOT_RUN (RT-02 B2 scope) | cost view の bounds/visibility/read-only は一部 assert 済み。通常2寸法・大桁数・長いモデル名の native/UI レイアウト網羅は RT-02 B2 scope。 |
| U02 | 760×600／scale 2／Tab／コピー／ログを開く | JobCostViewTests.Shared_view_preserves_paused_reader_detaches_and_binds_both_host_toggles; JobCostViewTests.File_commands_reject_non_generated_and_traversal_paths_and_only_call_injected_launcher（一部） | NOT_RUN (RT-02 B2 scope) | AutomationId と安全な generated log path/open command は一部 assert 済み。760×600、DPI scale、Tab/コピーなどアクセシビリティ実機相当は RT-02 B2 scope。 |

## RT-11 コスト関連の証跡の取り直し — 完了

`rt11-cost-final.trx`（`152E0775C26458125B34DC4E12E3CDA7C1E55D101746FA4CEC96055221C4A2CC`）: 計画に挙がった12クラスで102件中、成功101・失敗0・スキップ1。スキップは`Symlink_is_not_followed_by_reader_or_retention_when_creation_is_available`だけで、理由はシンボリックリンクを作るWindows特権がないこと（RT-09）。

## RT-12 D5／D6の整理 — 完了

1. D6の「4c」節が重複していたので、「同じて」の版を削除し、「同じく」の版を残した。
2. D5の冒頭の「状態」と§11の最後に、RT-10とRT-11の結果を追記した。
3. D6の§3に「残る差分の分類」の表を追加した（ADR-0018の非目標、本書の方針による非目標、現状のまま記録するもの、所有者の判断待ち）。

## RT-03 ローカルの全体回帰 — 完了（CIと同じ範囲で失敗0。除外クラスのうち1件は環境の問題）

[ci.yml](../.github/workflows/ci.yml)と同じ順番で実行した: `dotnet restore .\StudyReportEvaluator.slnx --locked-mode` → `dotnet build .\StudyReportEvaluator.slnx --configuration Release --no-restore`（警告0・エラー0）→ CIと同じ`--filter`で`dotnet test .\StudyReportEvaluator.slnx --configuration Release --no-build --no-restore`。環境変数は`CI=true`。SDKはCIが10.0.400、このホストが10.0.401で、版が異なる。

| 対象 | 結果 | TRX（SHA-256） |
|---|---|---|
| Core.Tests | 190件すべて成功 | `dahatake_DAHATAKE-STD2_2026-09-24_04_39_56_net10.0.trx`（`87021DDE9BE4A612E82F14FA68D84F63FAA444007FDA98DD82356824765E2D4B`） |
| App.Tests | 1870件中、成功1868・**失敗0**・スキップ2（16分17秒） | `dahatake_DAHATAKE-STD2_2026-09-24_04_39_53_net10.0.trx`（`974BA06169BEA903EC01D4994D91FFE5D9711EDC9B9E4A2587C59557363DED0F`） |

スキップした2件の理由:
- `JobCostBackendTests.Symlink_is_not_followed_by_reader_or_retention_when_creation_is_available`: シンボリックリンクを作るWindows特権がない（RT-09）
- `WindowsSingleFileArtifactTests.Opted_in_P02_artifact_is_packaged_by_the_actual_script_with_identical_bytes_and_versions`: `RUN_WINDOWS_SINGLEFILE_ARTIFACT_TESTS`が未設定（opt-in。RT-20）

CIが除外している2クラス（`rt03-excluded.trx`、`72CBE0F2BAA5115FBEE11D4240E89996489904F0BC2C24DA488DB7FB0B7C47ED`）: 4件中、成功3・失敗1。
- 失敗: `SampleWorkbookStructuralTests.Repository_sample_is_opened_read_only_and_only_structural_metadata_drives_mapping`。期待`A1:J531`に対し、実際は`A1:L531`
- 原因は**環境**。このホストの非公開ローカルサンプル`sample/SampleReport.xlsx`（Git管理外）は470,458 bytes、SHA-256 `A3261829E5B7C71B8CDC6962CD41DD662EAD3AEADC8008FA94BBDB6545BFE411`、最終更新は2026-09-14T17:16:40Z（UTC）。D3で作り直したprofile（[sample-workbook-profile.md](../dev/docs/preflight/sample-workbook-profile.md)の92行目と122行目: 469,976 bytes、`A1:J531`）とは別のファイルである。計画のとおり、sampleも期待値も変更していない

## 実施しなかったタスク（外部の前提・所有者の判断が必要）

所有者に確認する機会がなかったので、計画§4の既定に従った。commit、push、Actionsの起動、Release、署名、実AI、再package、公開はどれも行っていない。

| タスク | 状態 | 理由・次にやること |
|---|---|---|
| RT-07 実Copilotでの検証 | NOT_RUN | 対象アカウント・モデル・合成入力・呼び出し回数の上限について、承認をまだ得ていない |
| RT-08 native Windowsでのコスト表示の確認 | NOT_RUN | 隔離したnative環境がない。headlessでの成功をnativeの成功とは読み替えない |
| RT-09 symlink保護テスト | NOT_RUN | このホストにはシンボリックリンクを作る特権がない。上のTRXでもスキップされている |
| RT-15 本人・native・別プロセス・OS shutdownでの受け入れ確認 | NOT_RUN | 40行の合成入力、使い捨てのVM、実AIの承認がどれもない。代わりに、headlessの部分ケースST-UC-17-Rを追加した（RT-14）。これで受け入れ確認を置き換えたことにはしない |
| RT-16 版を`0.9.0`に上げる | BLOCKED（OWNER） | 所有者が判断するまで行わない。リモートに`v1.0.0`というtagがあることは、判断の材料として示す |
| RT-17 CIの単一EXE検証（P06／P07） | BLOCKED（OWNER） | 計画§4では、commit／pushの承認を得てCIのWindows jobを確かめた後に行う。ローカルで再現するには単一EXEの再packageが要るが、Phase 0で承認されていない。RT-03でdeterministic testsが通ったので、CIのステップが先へ進む前提はそろった |
| RT-18 MSIXについての所有者の判断 | BLOCKED（OWNER） | D2の§12.3にある7項目はすべて未決。値や承認を推測で決めていない |
| RT-19 MSIXの実装 | BLOCKED | RT-18とRT-17に依存する |
| RT-20 現HEADでローカルWindowsビルドを作り直す | NOT_RUN | Phase 0での承認がない。関連するopt-inのテストは、上のとおりスキップされている |
| RT-21 最終的な配布物のclean-host確認と公開承認 | NOT_RUN／BLOCKED（OWNER） | RT-20で配布物を作っていない。公開の指示もない |
| RT-05の項目2 GUI子プロセスの隔離 | OWNER | 製品側に上書きの仕組みを加えるか、別のユーザーで実行するかの判断が必要（RT-05の節を参照） |
| RT-12 同じジョブの中でログの保存をやり直すUI | OWNER | D6の§3に追加した分類表を参照 |

## 最後に行ったこと

- [CHANGELOG.md](../CHANGELOG.md)の`[Unreleased]`: 冒頭の概要文に1文を加え、`### Fixed`に2件を追加した（実行画面の配置／再開モード以外で出ていた再検証の通知）。
- 版の更新: 指示で名前の挙がった4つのパッケージ（`hve`、`Markdown query Skill`、`Code Query skill`、`Autonomous Task Graph`）は、このリポジトリには存在しない（`/hve-dev/`もない。[SystemTest-prompt.md](../tests/SystemTest-prompt.md)の25行目にも同じ記載がある）。今回変更したパッケージがないので、PATCHは上げていない。製品版の`0.8.6`は、RT-16の所有者判断が出るまで変更しない。
- 最後の`DocumentationContractTests`: 22件すべて成功（`final-doc.trx`、`321499F2055677B17939D1F299BB50E806900DCD5F163F0A6E7B61A6BA7EF11F`）。
- 一時的に作ったworktree（`C:\GitHub\sre-wt-*`）はすべて削除した。

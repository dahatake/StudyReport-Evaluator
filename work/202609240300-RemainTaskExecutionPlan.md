# `work/` 残タスク実行プラン（完成版）

- 作成: 2026-09-24 03:00 JST。改訂: 同日 03:22 JST に敵対的レビュー（[レビュー記録](20260924-0322-adversarial-review-RemainTaskExecutionPlan.md)、33件）の指摘を反映
- 基準: HEAD `d27dc0ee03b05ddaadf060c9285e2517fb0f686a`。`git ls-remote origin refs/heads/main`で同じ値であることを確認し、調査を始めた時点の作業ツリーはclean
- 調査ホスト: `DAHATAKE-STD2`、.NET SDK 10.0.401、PowerShell 7.6.6。D3・D4のTRXファイル名には`DAHATAKE-OFFICE`が入っており、別のホストで作られた記録
- 対象: `work/`直下にある既存の7文書（§1）
- 書き方:
  - §1〜§2は**今回確認した事実**で、出典またはコマンドを付けています。
  - §3以降は**これからの計画**で、実装済み・成功済みとは主張しません。
  - 確認できなかったことは「未確認」または「仮説」と書き、推測で埋めていません。
- 実行記録の置き場所: 実行の結果・判断・TRXのSHA-256は`work/{yyyymmdd-hhmm}-RemainTaskExecutionRecord.md`に1本にまとめて記録します（この計画書には結果を書き込みません）。

---

## 0. 今回の調査で行ったこと

| 区分 | 内容 |
|---|---|
| 文書の精読 | `work/`の7文書をすべて通読 |
| Git | `git log`／`git status`／`git show --stat d27dc0e`、`git show 8400f3f -- …WindowsSingleFile.pubxml`、`git show --name-only ea91d9f`、関連ファイルの履歴 |
| GitHub（読み取りのみ） | 直近8 runのCIの結論、HEAD・`730a225`・`8400f3f`のfailed log、MSIXの過去artifactの失効状態、Secretsの件数、Environment、最新Release、tag |
| ローカルテスト（Debug、限定） | 4クラス72件 → 69成功・3失敗（§2.2） |
| ファイルの確認 | work文書が挙げるTRXや成果物がこのホストにあるか、pubxmlの構造、テストクラスやfixtureがあるか |

**実施していないこと**: ローカルでの全suite実行、実AI／実ログイン、native UI、署名、commit／push、Actionsの起動、Release操作。

**再現方法**: §2.2のコマンドを同じHEADで実行し、`--logger "trx"`を付けて出力してください。CIのログは`gh run view <run-id> --repo dahatake/StudyReport-Evaluator --log-failed`で取れます。今回取得したTRX（SHA-256 `A394B0FF38CA43F8CCEAC763C460BCB483C167C958162E9E7CB45FBFD308971B`）とCIのログは、リポジトリに入れずセッション領域に保存しています。

---

## 1. 文書ごとの状態

| # | 文書 | 文書上の記載 | 今回の確認結果 | 残タスク |
|---|---|---|---|---|
| D1 | [20260915-1200-auto-model-normal-evaluation.md](20260915-1200-auto-model-normal-evaluation.md) | T3〜T5とT7〜T9は、それぞれ節を設けて「完了」と記録。T1／T2は表で完了。T6は表で「実行中」のままで、個別の節はない。表ではT8が「T6と統合」、T9が「未着手」のまま | 次をすべて確認した: 版`0.8.6`（[Directory.Build.props](../Directory.Build.props)）、[CHANGELOG.md](../CHANGELOG.md)のFixed、[version-management.md](../dev/docs/version-management.md)の「0.8.5の履歴」、要求定義の`Auto model selection source`行。`ea91d9f`で`tests/`配下の17ファイルが変更されている | **あり（軽微）**: 表とログの食い違い（RT-04） |
| D2 | [20260915-github-actions-windows-msix-investigation-report.md](20260915-github-actions-windows-msix-investigation-report.md) | 調査と提案だけで、実装はしていない。所有者が決めるべき事項が7つ（§12.3）。実施順序の§13-2に「既存CIのEXE検証失敗は別途調査」 | MSIX専用のworkflowはない（`.github/workflows`にあるのは3ファイルだけ）。要求定義は今も本体のuploadを禁止（[requirements-definition.md](../docs/requirements-definition.md)の757行目）。Secretsは0件、Environmentは`publish`だけ。過去の証跡artifact `9930636795`は失効済み（`expired: true`）。`8f8fa39`以降の6 runは、すべて**Run deterministic testsで失敗**し、ZIP／単一EXE／MSIXのステップはskipped | **あり**: CIの単一EXE検証失敗の調査（RT-17）、所有者の判断（RT-18）、判断後の実装（RT-19） |
| D3 | [20260916-app-tests-root-cause-report.md](20260916-app-tests-root-cause-report.md) | 最終の全体実行（14:49〜15:17）は1781成功・0失敗・8スキップ。独立静的レビューの検討事項は「未対応として残す」 | この全体実行は、D4が**その後**に行ったpubxmlの変更より前のもの（D4は15:38以降）。同じ`8400f3f`をCIで回すと4件失敗している（§2.1のA群）。[RealDataSystemSmokeTests.cs](../tests/StudyReportEvaluator.App.Tests/E2E/RealDataSystemSmokeTests.cs)は`8400f3f`以降変更されていない | **あり**: 静的レビューの検討事項を評価し、必要なものを直す（RT-05） |
| D4 | [20260916-latest-local-build.md](20260916-latest-local-build.md) | ローカルWindowsビルドの生成と検証は完了 | pubxmlに次の2つを追加していた: PropertyGroupへの`RuntimeFrameworkVersion 10.0.11`と、`KnownILLinkPack`のItemGroup。その後に回したのは単一EXE系のテストだけで、profileと文書の契約テストは回していない。このホストの`artifacts/package/`にあるEXE（2026-09-08付け、283,410,362 bytes、`E691A8D7…4B25`）は、記録にあるEXE（283,496,648 bytes、`11B2FACB…9A83`）とは別物。記録が挙げるTRXもこのホストにはない | **あり**: 壊れたテスト契約の修正（RT-01）、現HEADでの再ビルド（RT-20） |
| D5 | [20260917-job-cost-observability-implementation-plan.md](20260917-job-cost-observability-implementation-plan.md) | 「計画全体は未完了」と明記 | [ADR-0018](../dev/docs/adr/0018-job-cost-observability.md)はADOPTED。計画で提案されたテストクラス名（`JobCostLoggerCanaryTests`など5つ）は、ファイルとしてもクラス宣言としても存在しない。実際にあるのは別の名前の9クラス（§3.4） | **あり**（RT-06〜12） |
| D6 | [20260917-job-cost-execution-audit.md](20260917-job-cost-execution-audit.md) | §3に外部の前提4件と、元プランとの差分を列挙 | 「4c」節が2つある。中身はほぼ同じで、違いは「同じて」と「同じく」の1文字だけ。R09の結果はTRXに残していない（本文に明記あり）。`job-cost-audit-final.trx`はこのホストにない | **あり**（RT-06〜12） |
| D7 | [20260917-run-interruption-resume-handoff-plan.md](20260917-run-interruption-resume-handoff-plan.md) | P1〜P6は実装済み。残る受入確認として「本人操作・Narrator・native DPI、別processでの再起動・OS shutdown、exact配布物のclean-host／公開承認」。0.9.0への版上げはリリース時に判断する。最小テストの指示に従い、全suite・実AI・40行での実機試験・再package・公開は行っていない | [ADR-0017](../dev/docs/adr/0017-run-interruption-and-resume-handoff.md)はADOPTED。`ResumeAdmissionEvaluatorTests`は2件だけ（計画T-Aは6項目）。[SystemTest-prompt.md](../tests/SystemTest-prompt.md)のST-UC-17（1019〜1071行目）には、再起動もpickerも出てこない。[traceability.md](../dev/docs/traceability.md)の173行目には「ResumeWorkflowTests 8件」とあるが、今の実行結果は11件。`d27dc0e`のCIでは新たに25件が失敗している（原因は未確定、§2.1のB群） | **あり**（RT-02、RT-13〜16、RT-21） |

---

## 2. 現在の検証ベースライン（事実）

### 2.1 HEADのCI（run [35275935192](https://github.com/dahatake/StudyReport-Evaluator/actions/runs/35275935192)）

- Core.Tests: 190件すべて成功。App.Tests: **1847件中、1817成功・29失敗・1スキップ**。
- そのため、ZIP、単一EXE、MSIX、repository integrityのステップと、証跡のuploadはすべてskippedになっている。
- `730a225`（run 35179301411）と`8400f3f`（run 35073318743）で失敗していたのは、同じ**4件**（A群）だった。`730a225`で変わったのは`work/`の2ファイルだけ。したがってB群の25件は`d27dc0e`で初めて出たもの。

**A群: 単一EXE profileの契約（4件）**

原因は`8400f3f`でpubxmlに入った2つの変更で、`git show`で確認済み。

| テスト | 失敗の内容 | どちらの変更が原因か |
|---|---|---|
| `WindowsSingleFileProfileTests.Windows_single_file_profile_declares_standard_self_contained_settings_once` | 期待12、実際13 | PropertyGroupの子要素の数をassertしている（67〜68行目）。`RuntimeFrameworkVersion`が1つ増えたため |
| `WindowsSingleFileProfileTests.Windows_single_file_profile_scopes_all_declarations_to_the_app` | 期待`[PropertyGroup, ItemGroup]`、実際はItemGroupが2つ | `KnownILLinkPack`のItemGroupが加わったため |
| `WindowsSingleFileProfileTests.Windows_single_file_profile_registers_only_the_exact_safe_public_allowlist` | `Assert.Single`の失敗（ItemGroupが2つ） | 同上 |
| `DocumentationContractTests.Package_script_includes_every_public_document_image_and_license` | `Assert.Single`の失敗（ItemGroupが2つ） | 同上 |

**B群: `d27dc0e`で新たに出た失敗（25件）**

CIのエラーメッセージをもとに仮分類したものです。**原因はどれも確定していません。**

| 仮分類 | テスト（件数） | 観測したメッセージ |
|---|---|---|
| B1 仕様変更にテストが追いついていない可能性 | `ExecutionViewTests.Login_controls_bind_accessible_commands_without_automatic_activity`(1) | 596行目で期待`cancel`、実際`中断` |
| | `ExecutionViewTests.Live_progress_keeps_current_run_separate_from_next_settings_through_completion(cancelRun: True)`(1) | `部分結果`が見つからない。実際の文言は「中断しました。再開元として確認できる保存済み checkpoint はありません。」 |
| | `SettingsWorkflowSystemTests.Cancel_saves_a_real_partial_and_new_instances_resume_without_repeating_completed_AI_or_reference`(1) | 期待`Results`、実際`Execution` |
| | `ExecutionSettingsTests.Running_settings_edits_preserve_immutable_request_progress_and_stop(resume: True)`(1)、`ExecutionSettingsTests.Resume_ignores_relative_new_run_output_and_never_falls_back_from_a_missing_checkpoint`(1)、`WorkflowStateTests.Unedited_same_input_round_trip_retains_new_or_resume_and_completed_context(resume: True)`(1) | `Assert.True`の失敗（メッセージからは原因を推定できない） |
| | `WorkflowStateTests.Saved_definition_apply_across_shell_transitions_is_atomic_on_success_or_failure(failApply: True)`(1) | 「再開元と現在の条件を確認してください。条件の変更後は再確認が必要です。」 |
| B2 実行画面の配置が崩れた可能性 | `ResponsiveLayoutTests.Execution_view_keeps_actions_reachable_with_local_scroll_at_a_narrow_viewport`(2: scale 1／2) | `TextBlock#CopilotLoginInstructions`が760×600の表示範囲からはみ出している（y=-57／-55.5） |
| | `ResponsiveLayoutTests.Normal_shell_contains_every_step_without_body_scroll_at_actual_client_size`(2)、`CompactWorkflowLayoutTests.Long_content_in_four_steps_and_every_settings_category_keeps_normal_body_contained`(2)、`CompactWorkflowLayoutTests.Resizing_real_main_lists_grows_capacity_and_keeps_the_last_selected_item`(1) | 期待`None`、実際`CharacterEllipsis` |
| | `PrimaryJourneyAccessibilityTests.Execution_actual_values_and_explicit_actions_are_keyboard_accessible_at_two_hundred_percent`(1) | フォーカスがButtonではなくTextBoxにある |
| | `DocumentationScreenshotTests`の4件（`Synthetic_screenshot_renders_are_repeatable`、`Execution_documentation_screenshot_renders_are_repeatable`、`Minimum_client_screenshots_verify_all_eight_actual_pixel_frames_without_publishing`、`Documentation_screenshots_use_only_synthetic_state_and_have_expected_dimensions`） | `Assert.Empty`の失敗（TextBoxが一覧に残る） |
| B3 見当がついていない | `MainWindowTests.Input_replacement_during_reload_publishes_only_coherent_execution_state`(3)、`MainWindowTests.Workflow_and_settings_sync_follow_the_latest_editor_without_reentrant_configuration`(1) | 期待1、実際4 |
| | `MainWindowTests.Active_run_A_then_input_B_then_execution_shows_current_A_and_next_B_without_revalidation`(1) | 期待0、実際12 |
| | `CopilotLoginCommandTests.Default_and_existing_two_argument_constructors_leave_login_unstarted`(1) | `NullReferenceException` |

件数の内訳: B1が7件、B2が12件、B3が6件で、合計25件。

補足: [ui-layout-contract.md](../dev/docs/ui-layout-contract.md)には`d27dc0e`で追記があり、実行画面に追加したControlについて「追加後のnative DPI／Narrator／全レイアウト再測定は未実施」と書かれています。

### 2.2 ローカルでの限定実行（今回）

実行したコマンド:
`dotnet test tests\StudyReportEvaluator.App.Tests\StudyReportEvaluator.App.Tests.csproj -c Debug --filter "FullyQualifiedName~DocumentationContractTests|FullyQualifiedName~ExecutionViewTests|FullyQualifiedName~ResumeWorkflowTests|FullyQualifiedName~ResumeAdmissionEvaluatorTests"`

TRXのクラス別集計:

| クラス | 成功 | 失敗 |
|---|---|---|
| DocumentationContractTests | 21 | 1 |
| ExecutionViewTests | 35 | 2 |
| ResumeWorkflowTests | 11 | 0 |
| ResumeAdmissionEvaluatorTests | 2 | 0 |

失敗した3件はCIでも失敗しているもので、原因も同じです。

### 2.3 証跡がこのホストにない

D3・D4・D6・D7が挙げているTRX（`TestResults/app-fixes-20260916/…`、`latest-build-20260916/…`、`job-cost-audit-20260917/…`、`resume-focused/…`）は、このホストにはありません。D3とD4は別のホストで作られた記録です。これは過去の記録が誤りだという意味ではありません。ただ、**今の完了判定に使える証跡はここにはない**ので、それを前提に計画しています。

---

## 3. 残タスクの詳細

凡例:
- **種別**: `AUTO`＝自分たちで実行できる／`EXT`＝外部の環境・権限・本人の操作が要る／`OWNER`＝所有者の判断・承認が要る
- **完了条件**は、測った結果（TRX・ログ・hash）が残った場合だけ満たしたとみなします。

### RT-01 単一EXE profileとテストの契約を合わせる — `AUTO`

- 出典: §2.1のA群、D4の手順3と7、D6の4c節。
- 方針: `RuntimeFrameworkVersion`と`KnownILLinkPack`を10.0.11に固定する設定は、D4でpublishを通すために入れたものなので**外しません**。直すのはテストの契約のほうです。
  - PropertyGroupは、期待する宣言の一覧に`RuntimeFrameworkVersion=10.0.11`を加えて、完全一致の検査を続けます。
  - ItemGroupは「ILLinkの固定用が1つ」と「公開allowlistのContent用が1つ」の**ちょうど2つ**と決め、どちらもApp限定の条件付きとします。
  - `Assert.Single`を外してゆるく照合するだけの修正はしません。
- 手順:
  1. 4つのテストを読み、ItemGroupが1つ、宣言が12個という前提がどのassertに入っているかを洗い出す。
  2. `8400f3f`で[WindowsSingleFilePublishTests.cs](../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsSingleFilePublishTests.cs)に追加された回帰テスト（21行）が何を検査しているかを読み、同じ検査を重複して作らない。
  3. `declares_…_once`の期待値に`RuntimeFrameworkVersion`を加える。
  4. 残りの3件は、Content用のItemGroupを「子要素がすべて`Content`」という条件で1つに特定する。ILLink用のItemGroupは、`KnownILLinkPack Update="Microsoft.NET.ILLink.Tasks"`と10.0.11であることを別のassertで検査する。
  5. [ADR-0016](../dev/docs/adr/0016-windows-one-action-startup.md)など単一EXE profileを扱う開発文書に、この2つの固定とその理由が書かれているかを確認する。書かれていなければ追記する。
- 完了条件: 4件が成功し、`WindowsSingleFile*`と`DocumentationContractTests`のクラス全体も成功する。D4では契約テストの回し忘れが起きたので、pubxmlを変えたときは必ずこの範囲を回す。

### RT-02 `d27dc0e`で新たに出た25件の根本原因を調べて直す — `AUTO`

- 出典: §2.1のB群、D6の4b節、D7。
- 原則:
  - 1件ずつ、製品の不具合なのか、正しい仕様変更にテストが追いついていないだけなのかを判断し、**根拠を実行記録に書きます**。
  - 根拠に使うのは、[requirements-definition.md](../docs/requirements-definition.md)のAC-038（920行目）、§10.6（536行目）、§11.3（592行目）、§11.9（653行目）、ADR-0017、D7のP2・P4、[ui-layout-contract.md](../dev/docs/ui-layout-contract.md)です。
  - skipにする、テストを消す、許容値をゆるめる、といった方法で失敗を消すことはしません。
  - 同じworkspaceを並行して編集しません（D1とD6で、並行編集が原因の取り違えが起きています）。
- 手順:
  1. **再現**: B群のクラスをReleaseで実行し、TRXをリポジトリの外に保存して、CIと同じ失敗になることを確かめる。
  2. **B1（以下はすべて仮説）**
     - `Login_controls_…`: D7の設計方針1（UIの文言を「中断」に揃える）とP2（変えるのは文言とAutomation名だけ）のとおりかを確かめる。そのとおりなら、`Content`とAccessible Nameの期待値を今の仕様の値に合わせる。AutomationId（`CancelQuantification`）の検査は残す。
     - `Live_progress_…(cancelRun: True)`: 「保存済み checkpoint はありません」という文言が、このテストの状況で本当に正しいのかを確かめる。本来ならcheckpointがあるはずの状況であれば、**製品側の不具合**として直す。
     - `SettingsWorkflowSystemTests.Cancel_saves_…`: 中断したら実行画面に留まる、という動作は[implementation-status.md](../dev/docs/implementation-status.md)の11行目と`ResumeWorkflowTests.An_interrupted_run_stays_on_the_execution_step_instead_of_announcing_results`で裏づけられる。そのうえで、テストの流れに「再開準備→開始前検証→明示的なStart」を加える。**完了済みのAIと参照を繰り返さない**という本来の検証は残す。
     - `ExecutionSettingsTests`の2件と`WorkflowStateTests`の2件: 「再開モードでは、開始前検証で再開可能と判定されていなければ開始できない」（AC-038、D7のP4）ことが原因だという仮説を、テストごとに確かめる。確認が取れたものだけ、開始前検証の手順を加える。`never_falls_back_from_a_missing_checkpoint`のような、テスト本来の意図は残す。
  3. **B2**: まず**製品側の配置を直す**。テストの期待値は変えない。最小1024×720、初期1180×800、760×600、scale 2のそれぞれで、次を満たすようにする。
     - 主要な操作は44 DIP以上、本文は14 DIP
     - 通常のサイズでは外側のスクロールが出ない
     - `CharacterEllipsis`が出ない
     - `CopilotLoginInstructions`が表示範囲に収まる
     - Tab順が契約どおり
     `DocumentationScreenshotTests`は、どの要素で状態が合っていないのかを特定してから直す。画像の更新が要る場合は[images/README.md](../images/README.md)の規則に従う。
  4. **B3**: まずassertが何の回数を数えているのかを読む。次にdetached worktreeで`d27dc0e`の変更をファイル単位で戻していき（候補は`MainWindowViewModel`／`InputViewModel`／`ExecutionViewModel.Resume.cs`）、どの変更で失敗するのかを切り分ける。`CopilotLoginCommandTests`のNREは、既存のコンストラクタを通ったときにnullのままの依存がないかを確かめる（仮説）。原因が製品側にあれば製品を直す。
  5. 1つ直すたびに、影響するクラスをまとめて再実行する。
- 完了条件: 25件が成功する。1件ずつ、何を根拠にどう判断したかが実行記録にある。変更したクラスの全件が成功する。

### RT-03 ローカルの全体回帰 — `AUTO`（Phase 1の出口）

- 手順:
  1. [ci.yml](../.github/workflows/ci.yml)と同じ順番で実行する: `dotnet restore .\StudyReportEvaluator.slnx --locked-mode` → `dotnet build .\StudyReportEvaluator.slnx --configuration Release --no-restore` → CIと同じ`--filter`で`dotnet test .\StudyReportEvaluator.slnx --configuration Release --no-build --no-restore`。CIのSDKは10.0.400、このホストは10.0.401なので、その差を記録する。
  2. CIが除外している`SampleWorkbookStructuralTests`と`WindowsPublishPackageTests`を別に実行する。`SampleWorkbookStructuralTests`は非公開のローカルサンプル（`sample/SampleReport.xlsx`、Git管理外）を使う。このホストのサンプルがD3で作り直したprofileと同じかどうかは**未確認**。一致しなければ環境の問題として記録し、hashは書き換えない。
- 完了条件: ローカルの全体TRXで失敗が0件。スキップはすべて理由付きで記録されている。

### RT-04 D1のタスク表への訂正を追記する — `AUTO`

- 手順: 表の状態欄は書き換えない。表の下に「訂正の注記」を追記する。
  - T8とT9のログ節は、ともに「完了」と記録している。
  - T6の根拠は、T8のログ（期待値を更新した後にすべて成功）と、`ea91d9f`でのテストファイル17本の変更。「fixtureの現実化」は個別には確認していないので、その旨を書く。
  - T8の時点で並行セッションが原因だった失敗は、D3で解決済み（リンクを付ける）。
  - 検証をやり直したとは書かない。
- 完了条件: 注記が入り、`DocumentationContractTests`のリンク検査が成功する。

### RT-05 D3の独立静的レビューの検討事項を評価し、必要なものを直す — `AUTO`

- 出典: D3の「独立静的レビューの別途検討事項」。どれも「検討事項」なので、**まず今のコードと照らして評価し**、直すかどうかとその理由を実行記録に書きます。
- 対象:
  1. **証跡の出力先と入力の衝突**: [RealDataSystemSmokeTests.cs](../tests/StudyReportEvaluator.App.Tests/E2E/RealDataSystemSmokeTests.cs)で、出力先が入力と同じパスになる、または入力を上書きし得る場合に、開始前に拒否できているかを確認する。拒否できていなければガードを足す。入力と同じディレクトリに出力することは禁止しない。
  2. **GUI子プロセスの利用者環境からの隔離**: grepの範囲では、`LOCALAPPDATA`を差し替えている箇所は見つからなかった（940行目にあるのはuserProfileの文字列を検出する処理だけ）。子プロセスが利用者の`setting.txt`を触らないかを確認し、必要なら一時ディレクトリへ向ける。
  3. **失敗時のプロセス回収**: HEAD時点の`finally`（416／758／901行目）と`Kill(entireProcessTree: true)`（798／1084行目）で、例外が起きたときに取りこぼしがないかを確認する。
  4. **C03のホスト検出**: [ReleaseMatrixBuilderTests.cs](../tests/StudyReportEvaluator.App.Tests/Packaging/ReleaseMatrixBuilderTests.cs)の471〜473行目では、PowerShellの版を調べる際に`ReadToEnd`をした**後**で`WaitForExit(10_000)`を呼んでいる。D3の指摘箇所とこれが一致するかを確かめ、一致すれば、読み取りもtimeoutの対象に入れる。
- 制約: 実データのsmokeは無効のままにする。検証は、opt-inを無効にしたままのガードの単体テストで行う。
- 完了条件: 4項目それぞれについて判断が記録され、手を入れたテストが成功する。opt-inが無効なら実データに触れないことが、テストで確かめられている。

### RT-06 AIクレジットの数値表示（G2ゲート） — 資料確認は`AUTO`、実装の判断は`OWNER`

- 出典: D5の§9.1（G2）、D6の§3-1、ADR-0018。
- 手順: D6のW1〜W4（GitHub公式の課金資料）を取り直す。固定しているSDK 1.0.11（[Directory.Packages.props](../Directory.Packages.props)）と同梱CLI 1.0.79（D1の実測）の`TotalNanoAiu`について、AIクレジットへの換算が明記されているかを確認する。明記がなければ**BLOCKEDのまま**にする。明記があれば、ADR-0018を改訂する案を作り、所有者の承認を得てから実装する。
- 完了条件: 取得日・URL・該当箇所と、「実装する」か「BLOCKEDのまま」かの判断が記録されている。

### RT-07 実Copilotでの検証 — `EXT`＋`OWNER`

- 前提: 事前に次を示して承認を得る。認証済みの対象アカウント、使うモデル（`auto`と明示モデルを分けて扱う）、合成した入力、呼び出し回数の上限。既存の認証情報を読み出したり、承認なしにAIを呼んだりはしない。
- 手順: 正常終了、複数回の呼び出し、取消、そして実SDKの例外が起きた場合の再試行について、SDKの値と画面・JSONLの値を突き合わせる。取れなかった値は「未取得」と記録する。生の応答や認証情報は保存しない。
- 完了条件: 版・条件・許可した数値・観測した状態が記録されている。取れなかったものは`NOT_RUN`または未取得と明記されている。

### RT-08 native Windowsでの確認（コスト表示） — `EXT`

- 利用者の設定や認証に影響しない隔離環境で、次を確認する: 実DPI、キーボード操作、画面内のログへの到達、「ログを開く」「保存先を開く」。headlessでの成功をnativeの成功と読み替えない。
- 完了条件: 項目ごとにPASS、FAIL、NOT_RUNのどれかを、環境（OS build・DPI）とあわせて記録する。

### RT-09 シンボリックリンク保護テストを実際に通す — `EXT`

- Developer Modeなどでシンボリックリンクを作れる、使い捨ての環境で`Symlink_is_not_followed_by_reader_or_retention_when_creation_is_available`を実行する。
- 完了条件: スキップではなく**成功**したTRXが残っている。

### RT-10 元プランのテスト計画と実際のテストを突き合わせ、足りないものを補う — `AUTO`

- 出典: D5の§10.1とT08、D6の§3。
- 手順:
  1. D5の§10.1にある25ケース（C01〜C18、L01〜L05、U01〜U02）について、それぞれを担っている既存のテストメソッドを表にする。対応するテストがないものは`GAP`とする。**テストの名前だけで判断せず、assertの中身まで確認する。**
  2. `GAP`のうち、fakeとsyntheticなデータで決定的に試せるものを追加する。候補としてはC09、C11〜C14、L01〜L05がある（突き合わせる前なので、まだ確定ではない）。U01／U02はRT-02のB2とまとめて扱う。
  3. D5で提案されたテストクラス名は「提案名」だと注記し、実際のクラス名との対応を追記する。
- 完了条件: 対応表ができていて、すべての`GAP`が「追加して成功」か「外部の前提があるため未実施（理由付き）」のどちらかになっている。

### RT-11 コスト関連の結果を、残る証跡として取り直す — `AUTO`

- 出典: D6の4a節（TRXを保存していない）、§2.3。
- 手順: RT-02とRT-10のあとで、TRXを出力しながら次のテストを実行する。
  - コスト系の9クラス: `CostAttemptLifecycleTests`、`JobCostBackendTests`、`JobCostViewTests`、`AttemptOutcomeLoggingTests`、`JobCostModelTests`、`JobCostSnapshotTests`、`JobUsageTrackerTests`、`SdkUsageAdapterTests`、`UsageProvenanceTests`
  - 実行系の3クラス: `RetryAndCleanupCoordinatorTests`、`EphemeralEvaluationRunnerTests`、`AuxiliaryEvaluationRunnerTests`

  TRXのパスとSHA-256を実行記録に書く。
- 完了条件: 失敗が0件。スキップはsymlinkの1件だけで、理由が書かれている。

### RT-12 D5／D6の記述を整理し、残っている差分を分類する — `AUTO`（一部は`OWNER`）

- 手順:
  1. D6の「4c」節の重複をなくす。「同じく」と書かれているほうを残す。
  2. D5の冒頭の「状態」と、§11の最後にある検証結果を、RT-10とRT-11の結果で書き直す。
  3. D6の§3に残っている差分を分類する。
     - 非目標（ADR-0018）: 過去ログの再取込UI、再開前からの通算、予算超過での停止。
     - 非目標（D6の方針）: 保持の上限で内訳を切り詰めたとき、どの識別子が落ちたかは示さない。
     - 現状のとおりと記録するもの: 過去のファイルの読込結果を画面に出す導線（readerは内部機能）。
     - `OWNER`の判断待ち: 同じジョブの中で保存をやり直すUI（D5の§7.3では任意とされている）。
- 完了条件: 文書の契約テストが成功し、同じ見出しが重複しておらず、上の項目がすべて分類されている。

### RT-13 `ResumeAdmissionEvaluatorTests`を計画T-Aの範囲まで広げる — `AUTO`

- 出典: D7の§5 P1と§6 T-A。今あるテストは`Matching_checkpoint_is_admitted`と`Different_definition_is_rejected_without_reporting_content`の2件。
- 手順:
  1. `ResumeAdmissionItem`の6項目（PartialPath／InputIdentity／Definition／NormalModel／Runtime／CheckpointShape。[ResumeAdmissionEvaluator.cs](../src/StudyReportEvaluator.App/Workflow/ResumeAdmissionEvaluator.cs)の10行目）それぞれについて、一致する場合と一致しない場合を作る。
  2. 複数の項目が一致しないとき、`BlockingStatusCode`が`730a225`時点の`DurableQuantificationOrchestrator.ValidateResumeAdmission`と同じ順番で決まることを確かめる。
  3. 説明文に回答、Prompt、reason、evidenceが入らないことをassertする。
- 完了条件: 追加したテストと、既存の再開・mismatchのテストが成功している。`DurableQuantificationOrchestratorTests`は変更しない（`d27dc0e`でも変更されていない）。

### RT-14 計画T-G／T-Iの取りこぼしを埋める — `AUTO`

- T-G: D7の確定記録とADR-0017を基準にする（D7の§5 P5の当初案は置き換えられている）。[ResumeWorkflowTests.cs](../tests/StudyReportEvaluator.App.Tests/UI/ResumeWorkflowTests.cs)の`Repeated_window_close_waits_for_run_completion`と`Drain_is_bounded_cancels_active_run_and_prevents_restart`が、次の4点をassertしているかを確認し、足りないものを追加する。
  1. Closeを繰り返しても待機を飛ばさない
  2. 閉じるのは内部Closeだけに限られる
  3. 待機は最大10秒で、それを過ぎたら閉じる
  4. 実行していなければ、すぐに閉じる
- T-I: pickerを使う経路は、headlessのテスト（`Picker_cancel_preserves_selection_and_never_starts_AI`など）で担保する。[SystemTest-prompt.md](../tests/SystemTest-prompt.md)のST-UC-17には、「再起動したのと同じ状態から再開する」部分ケースを、既存の期待値とは**別の期待値**で追加する。既存の期待値は変えない。
- [traceability.md](../dev/docs/traceability.md)の173行目と216行目（AC-038／TR-37）にあるテスト件数を、実際の数に合わせる。
- 完了条件: 追加したテストが成功し、そのあとも`DocumentationContractTests`が成功する。

### RT-15 本人・native・別プロセス・OS shutdownでの受け入れ確認 — `EXT`＋`OWNER`

- 出典: D7の冒頭の「残る受入確認」と、§9の完了条件3。
- 前提: 入力はすべて**合成データ**にする。今のfixtureは10人分の`SystemTest-10Students.xlsx`だけで、40行のものは**ない**ので、合成した40行の入力を新たに用意する。実データは使わない。実AIを使う部分は、RT-07と同じ承認を得てから行う。
- 手順:
  1. 40行を12行目で中断し、同じセッションで再開準備からStartまで進める。13行目から再開し、参照回答を作り直さないことを確かめる。
  2. アプリを再起動してから、pickerで`.partial.xlsx`を選び、すべての項目がOKになって再開できることを確かめる。
  3. 採点設計を変えてから再開を試みる。開始前に項目ごとの不一致が表示され、`.partial.xlsx`のhashが変わらないことを確かめる。
  4. 実行中にウィンドウを閉じる。10秒以内に閉じ、partialが壊れず、再起動後に再開できることを確かめる。
  5. Narrator、native DPI、OS shutdown（中断を要求するだけで、保存の完了は保証しない）を観測する。OS shutdownは使い捨てのVMで行う。
- 完了条件: 項目ごとにPASS、FAIL、NOT_RUNのどれかが記録されている。headlessの結果で置き換えていない。

### RT-16 `0.9.0`への版上げ — `OWNER`

- 今は`0.8.6`。D7は、他の未公開の機能と合わせてリリースのときに判断する、としている。所有者が判断するまで**実施しない**。
- 事前に確認すること: リモートには`v1.0.0`というtagがある（`gh api …/tags`）。一方、最新のReleaseは`v0.8.1`。このtagの経緯は調べていない。`0.9.0`と版の順番がぶつかったり、[release.yml](../.github/workflows/release.yml)のtag検証に影響したりしないかを、判断の材料として所有者に示す。
- 判断が出たら: `dev/version.ps1 bump -Part minor`、`dev/version.ps1 verify`、`dev/version.tests.ps1`を実行し、版の文字列を固定している文書と`DocumentationContractTests`を同期する（D1のT9と同じ手順）。
- 完了条件: 所有者の判断が記録されている。実施した場合は、verifyと版管理ツールのテストが成功している。

### RT-17 CIの単一EXE検証（P06／P07）の失敗を調べる — `AUTO`

- 出典: D2の§3.2と§13-2。run 34141745681（`77d1dd6`）で`P07_FAIL stage=p06-trx code=P07_TRX_TEST_NOT_PASSED`。それ以降は、その手前のステップで止まっているため、**今の状態は確認できていない**。
- 手順: RT-03でdeterministic testsが通ったあと、`Build and validate Windows development-host single-file package`と同じ内容をローカルで再現する。失敗したP06のテストと、その根本原因を特定する（CIのGUI環境、テスト、アプリのどこに原因があるか）。既存のゲートを無効にすることで通したりはしない。
- 完了条件: 原因と修正（または外部要因である根拠）が記録され、ローカルのP06／P07が成功している。

### RT-18 MSIXについて所有者の判断を得る — `OWNER`

- D2の§12.3にある7項目を1つずつ確認する。
  1. どこまで到達したいか（A: 生成を検証する／B: 開発用の本体を取得する／C: 一般に配布する）
  2. 本体をuploadしない、という禁止（要求定義の757行目）をどこまで変えるか
  3. 発行者、証明書、署名サービス
  4. Identity、表示名、アイコン
  5. artifactまでにするか、Releaseまで出すか
  6. 版、更新、設定の移行、削除時の扱い
  7. 承認者、許可するref、clean-hostでの検証の担当者と環境
- 値や承認を推測で決めない。
- 完了条件: 7項目の判断が記録されている（未決のものは未決と明記する）。

### RT-19 判断に沿ってMSIXを段階的に実装する — `AUTO`（判断のあと）＋`EXT`

- 前提: RT-03とRT-17が終わり、CIがMSIXの生成ステップまで進むこと（到達点A）を確認できていること。CIでの確認はPhase 1の後に置くOWNERゲートで行う。
- 手順: D2の§13の順に、判断で決めた到達点のところまで進める。
  1. 承認された範囲だけ要求定義を改訂する
  2. MSIX専用の`workflow_dispatch`を作る（到達点がB以上の場合）
  3. 開発用と本番用を区別するよう契約テストを直す（テストは消さない）
  4. 到達点がCの場合は、署名とclean Windows 11での検証（D2の§11 V01〜V14）
- 完了条件: 選んだ到達点までの項目が、測った結果で満たされている。

### RT-20 現HEADでローカルWindowsビルドを作り直し、実成果物のopt-inを実行する — `AUTO`

- D4のビルドは`ea91d9f`と未コミットの変更の時点のものなので、今のHEADとは違う。
- 手順: RT-03のあとで、`publish-windows.ps1 -SingleFile` → `package-windows-singlefile.ps1` → P06の7シナリオ → `WindowsSingleFilePackageTests`と、単一EXEの実成果物を使うopt-inの8件をD4と同じ条件で実行する。EXEとZIPのhashを記録する。署名と公開はしない。
- 完了条件: 生成に成功し、sidecarが一致し、opt-inの8件とP06の7シナリオが成功している（TRXまたは証跡JSONがある）。

### RT-21 最終的な配布物のclean-host確認と公開承認 — `EXT`＋`OWNER`

- 出典: D7の確定記録（「exact配布物のclean-host／公開承認」）。[implementation-status.md](../dev/docs/implementation-status.md)の166行目と168行目（CH-01〜06は`NOT_RUN_EXTERNAL_PREREQUISITE`、新EXEの公開は`BLOCKED_EXTERNAL`）。
- 手順: RT-20で作った**同じバイト列**のEXEを使い、まっさらなWindows 11 x64の標準ユーザーでCH-01〜06を実測する（[SystemTest-prompt.md](../tests/SystemTest-prompt.md)の手順に従う）。公開については、所有者の明示的な指示を得てから、candidateとprotected publishの手順を進める。
- 完了条件: CHの各項目にPASS、FAIL、NOT_RUNのどれかが記録されている。公開は、指示がなければ実施しないことを記録する。

---

## 4. 実行順序と依存関係

```
Phase 0 準備
   ・HEAD／status／SDKの版を記録し、並行セッションがないことを確かめる
   ・所有者に確認する（OWNER）: D6とD7に記録されている「最小テスト方針」がまだ有効かどうか。
     確認が取れるまでは、RT-03の全体回帰（CIを復旧させるのに必要）だけを実行する。
     RT-20の再package、実AI、公開は行わない
   │
Phase 1 CIの復旧（AUTO）
   RT-01 ─┐
   RT-02 ─┴─> RT-03 ローカルの全体回帰で失敗0  ← Phase 1の出口
   │
   ├──(OWNER: commit／pushの承認)──> CIのWindows jobの全ステップを確認
   │                                  → RT-17 P06／P07（CIとローカル）
   │
Phase 2 自分たちだけで進める補完（AUTO。Phase 1のあと、並行して進めてよい）
   RT-04 ／ RT-05 ／ RT-06（資料の確認）／ RT-10 → RT-11 → RT-12 ／ RT-13 ／ RT-14
   │
Phase 3 もう一度回帰を回す: RT-03を再実行する ＋ RT-20（Phase 0で認められた場合）
   │
Phase 4 外部の前提があるもの（EXT。それぞれ承認を得てから）
   RT-07 ／ RT-08 ／ RT-09 ／ RT-15（RT-07と承認をまとめてよい）／ RT-21
   │
Phase 5 所有者の判断
   RT-06（実装するか）／ RT-12の再保存UI ／ RT-16 ／ RT-18 → RT-19
```

- Phase 2以降の完了判定は、RT-03（ローカル）で失敗が0になってから行います。CIでの確認は所有者の承認に依存するので、Phase 2を止める条件にはしません。
- Phase 4とPhase 5は、承認が出るまで`NOT_RUN`または`BLOCKED`として記録し、完了扱いにはしません。

---

## 5. 各タスクで守ること

1. **捏造しない**: 実行していない試験を成功と書かない。スキップを成功と数えない（D6のR04で起きた教訓）。仮説は仮説と書く。
2. **弱めない**: 失敗を消すために、skipにしたり、テストを消したり、期待値をゆるめたりしない。仕様の変更に合わせてテストを直すときは、根拠にした要求定義やADRの条文を記録する。
3. **固定値を守る**: サンプルやfixtureの固定hashを書き換えない。実データを使うsmokeとLive AIは、既定では無効のままにする。
4. **並行編集を避ける**: 同じworkspaceでは同時に1セッションだけにする。基準を測るときは、必要に応じてdetached worktreeを使う（D1のT8と同じやり方）。
5. **証跡を残す**: TRXはリポジトリの外に保存し、そのパスとSHA-256を実行記録（冒頭に書いた記録ファイル）に書く。
6. **外への操作は承認を得てから**: commit、push、Actionsの起動、Release、署名、実AI、課金が発生する操作は、所有者の承認を得てから行う。

---

## 6. 範囲外（`work/`の文書に書かれていないもの）

- [CHANGELOG.md](../CHANGELOG.md)の`[Unreleased]`冒頭にある「追加の4画面実機確認は…停止（FAIL）」と、[implementation-status.md](../dev/docs/implementation-status.md)のT39（BLOCKED）。どちらも`work/`の外で管理されている残作業です。RT-15とRT-21の実機環境を用意するときに、あわせて計画すると効率がよくなります。
- CH-01〜06は`work/`の外でも管理されていますが、D7に書かれているので、この計画ではRT-21として扱います。

# auto を通常評価モデルとして選択可能にする — 実行記録

- 開始: 2026-09-15
- 対象: `MODEL_PROMPT_LIMIT_UNAVAILABLE` により `auto` 選択時に run を開始できない問題の解消
- 方針: 「SDK が上限を公開しない」を一級の状態として扱い、model 相対の context budget 検査だけを適用対象外にする

## 根拠となる実測（2026-09-15）

バンドル CLI 1.0.79 / SDK 1.0.11 の `ListModelsAsync` 生応答:

```
id=auto             maxPrompt=null    maxContext=0
id=claude-sonnet-5  maxPrompt=936000  maxContext=1000000
（auto 以外の 22 件はすべて正の値）
```

`auto` は router のため実体モデルが実行時まで決まらず、上限を宣言できない。

## タスク一覧

| # | タスク | 状態 |
|---|---|---|
| T1 | 調査（根本原因の実測確定） | 完了 |
| T2 | 設計（修正プラン確定） | 完了 |
| T3 | 要求定義改訂 R1〜R5 | 完了・レビュー反映済み |
| T4 | コア実装 P1〜P4（容量 nullable 化） | 完了・レビュー反映済み |
| T5 | UI 実装 P5〜P6 | 完了・レビュー反映済み |
| T6 | テスト更新・追加・fixture 現実化 | 実行中 |
| T7 | 文書更新 | 完了 |
| T8 | 全テスト実行 | T6 と統合 |
| T9 | CHANGELOG・PATCH 版上げ | 未着手 |

> **訂正の注記（2026-09-24 追記、上表の状態欄は書き換えていない）**
> - T8 と T9 の実行ログ節は、ともに「完了」と記録している（下記「T8 最終検証（完了）」「T9 CHANGELOG・版上げ（完了）」）。表の「T6 と統合」「未着手」はログと食い違う。
> - T6 の根拠は、T8 のログ（UI 文字列の期待値更新後に全通過）と、commit `ea91d9f` での `tests/StudyReportEvaluator.App.Tests/` 配下 17 ファイルの変更（`tests/SystemTest-prompt.md` を含めると `tests/` 配下 18 ファイル。`git show --name-only ea91d9f`）である。「fixture 現実化」は個別の記録がなく、未確認。
> - T8 の時点で並行セッションが原因だった失敗（`MainWindowTests`／`SettingsWorkflowSystemTests`／`WorkflowStateTests` 19件、`DocumentationContractTests` 3件）は、[20260916-app-tests-root-cause-report.md](20260916-app-tests-root-cause-report.md) の「原因と解決方針」で解決済み。
> - この注記は記録の整合を取るためのもので、検証をやり直したものではない。

各タスク完了時に敵対的レビューを行い、結果を反映してから次へ進む。

## 実行ログ

### T3 要求定義改訂（完了）

`docs/requirements-definition.md` へ R1〜R5 を適用。差分 10 追加 / 3 削除。

- R1 §7.1: `auto` を含む全 model を通常評価に選択可能と明記。上限不明時は model 相対検査を適用しないが、絶対上限と attempt 上限は常に適用、画面に明示。
- R2 §15: app-owned request 上限は常に検査、model context の安全 margin は上限公開時のみ。
- R3 §16: 「選択modelの上限がSDK未公開」行を追加。
- R4 §10.3: `auto` 選択時に実 routing 先が記録されないことを明記。
- R5 §11.3: 実行画面の責務へ「上限の既知／不明」を追加。

**レビュー指摘1（反映済み）**: §10.4 再開条件は model ID の完全一致を要求するが、`auto` では ID 一致でも同一実 model への routing を保証しない。§10.4 へ注記を追加。

**編集事故と復旧**: R4 の適用時、oldString の後方コンテキストに含めた 2 行（`app、SDK、CLI runtime identity` / `参照回答とstatus`）を newString へ書き忘れて誤削除した。`git diff` で即検出し復旧済み。最終差分の削除 3 行はすべて意図した置換。

### T4 コア実装（完了）

- `QuantificationRunRequest.MaximumPromptTokens` / `MaximumContextWindowTokens` を `int?` 化。`null` = SDK 未公開。
- 両 orchestrator の静的ゲートを「SDK が値を返したのに非正」に限定。`MODEL_PROMPT_LIMIT_UNAVAILABLE` / `MODEL_CONTEXT_LIMIT_UNAVAILABLE` は内部不整合の防御として存続。
- `DurableEvaluationScheduler` / `DurableQuantificationOrchestrator` の容量引数を `int?` へ伝播。既定 `int.MaxValue` → `null`（旧既定の budget は実質無制限で挙動同等）。
- `EvaluationRequestCapacityValidator` が `int?` を受け、上限不明時は `ModelContextBudget` を `null` として model 相対検査だけを外す。絶対 scalar 上限は常に適用。
- `EvaluationScheduler` の null-skip ガードを撤去し、常に validator を呼ぶよう変更（R2「常に適用する」への準拠）。

**レビュー指摘1（反映済み）**: `ExecutionViewModel` の switch に `MODEL_SELECTION_REQUIRED` を出す case が 2 つ並び、後者が到達不能な死にコードになっていた。1 つの case へ統合。

### T5 UI 実装（完了）

- `SelectedModelPromptTokenLimit` / `SelectedModelLimitText` を追加。上限不明は「SDK未公開・事前検証なし」と表示。
- 実行画面の実効条件表示へ「· 上限: …」を追加。新規 Control は追加していないため UI layout contract の画面責務は不変。
- `ExecutionEffectiveModel` の Accessible Name と ToolTip を更新。

**レビュー指摘1（反映済み）**: 現在 run の上限が画面に出ず、実行中に上限不明で走っていることが分からなかった。`CurrentRunLimitText` を追加し、実行中／前回 run の表示にも上限を含めた。

### T7 文書更新（完了）

`docs/settings.md` / `docs/troubleshooting.md` / `docs/features.md` / `dev/docs/detailed-design.md` / `dev/docs/ui-layout-contract.md` / `dev/docs/readme-claim-ledger.md` / `dev/docs/traceability.md` を新契約へ更新。

### T8 最終検証（完了）

並行して別 Agent セッションが同一 workspace を編集していたため、HEAD の detached worktree（`520bc30`）を作って基準を測定し、自分の変更だけを隔離検証した。

| 失敗 | 件数 | 帰属 | 根拠 |
|---|---|---|---|
| `ExecutionViewTests` / `PrimaryJourneyAccessibilityTests` の UI 文字列 | 8 | 自分 → 修正済み | `· 上限:` 追加に伴う期待値更新後、全通過 |
| `docs/privacy-and-data-handling.md` の見出し anchor 切れ | 1 | 自分 → 修正済み | 版表記更新で見出しを変えた際、link を更新し忘れた。隔離実行の `Current_documents_link_to_existing_local_markdown_headings` で検出 |
| `SampleWorkbookStructuralTests` | 1 | HEAD で既存 | HEAD worktree で 110/111、同一 test のみ FAIL |
| `ReleaseMatrixBuilderTests` / `WindowsPublishPackageTests` | 2 | HEAD で既存（環境由来） | HEAD worktree で同一 2 件が FAIL。`DecoderFallbackException: Unable to translate bytes [82]`（CP932） |
| `MainWindowTests` / `SettingsWorkflowSystemTests` / `WorkflowStateTests` | 19 | 並行セッション | 未 commit の `cachedModels` 機能。`git show HEAD:...ApplicationSettings.cs` に `cachedModels` は 0 件、作業コピーに 2 件 |
| `DocumentationContractTests` 3 件 | 3 | 並行セッション | `SystemTest-prompt.md` を repository root から `tests/` へ移動したことによる `FileNotFoundException`。同一 3 件は HEAD worktree（root に当該 file あり）へ自分の変更を適用すると 21/21 通過 |

最終確認（実 workspace、影響 7 クラス）: **155 合格 / 3 失敗**。失敗 3 件はすべて上記 `SystemTest-prompt.md` 移動。
隔離確認（HEAD worktree ＋ 自分の変更）: `DocumentationContractTests` **21/21 通過**。

### T9 CHANGELOG・版上げ（完了）

- `CHANGELOG.md` の `## [Unreleased]` へ Changed（要求定義書 v4.6 の改訂）と Fixed（`auto` 選択時の恒久ブロック解消）を追加。
- `dev/version.ps1 bump -Part patch` で `0.8.5` → `0.8.6`。
- 版表記は repository の契約として 11 の公開文書と `DocumentationContractTests` に文字列固定されているため同期した。**0.8.5 で得た試験証跡・artifact identity・`dev/docs/archive/**` は付け替えず**、`dev/docs/version-management.md` に既存の「0.8.4の履歴」と同じ形式で「0.8.5の履歴」節を追加した。

**レビュー指摘（反映済み）**: 要求定義書の決定表に今回の規範変更（`auto` 選択可・上限不明 model の扱い）が記録されていなかった。`Auto model selection source` 行を追加した。


# ステップ3 実行の「中断して後から再開」機能追加 — 実装プラン

- 作成: 2026-09-17
- 対象: ステップ3（実行 / Execution）の run を利用者の意思で停止し、後から続きを再開する導線
- 版: `0.8.6`（未公開候補）を起点。ADR-0014 に従い機能追加のため MINOR bump（`0.9.0`）を推奨

---

## 1. 前提調査の結論（実測）

**中断と再開の「機構」はすでに実装済みである。不足しているのは利用者の導線と事前検証である。**

### 1.1 すでにあるもの

| 機構 | 実体 | 場所 |
|---|---|---|
| 行境界での停止 | `runCancellation.Cancel()` → 参照ループ・行ループが境界で `break` | [ExecutionViewModel.cs](src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs#L1382)、[DurableQuantificationOrchestrator.cs](src/StudyReportEvaluator.App/Workflow/DurableQuantificationOrchestrator.cs#L317) |
| 部分結果の保持 | 参照1件ごと・学生行1行ごとに `checkpointStore.Update(checkpoint, CancellationToken.None)` | [DurableQuantificationOrchestrator.cs](src/StudyReportEvaluator.App/Workflow/DurableQuantificationOrchestrator.cs#L350) |
| 停止時の状態 | `QuantificationRunStatusCodes.Cancelled`。`.partial.xlsx` は削除しない | [DurableQuantificationOrchestrator.cs](src/StudyReportEvaluator.App/Workflow/DurableQuantificationOrchestrator.cs#L420) |
| 再開実行 | `ResumePartialPath` → `checkpointStore.Load` → `ValidateResumeAdmission` → 参照再利用・完了行 skip | [DurableQuantificationOrchestrator.cs](src/StudyReportEvaluator.App/Workflow/DurableQuantificationOrchestrator.cs#L268) |
| 再開条件の検査 | partial path / input path / input snapshot / definition SHA・canonical / model ID / runtime identity（app major + CLI version + CLI sha + SDK version） | [DurableQuantificationOrchestrator.cs](src/StudyReportEvaluator.App/Workflow/DurableQuantificationOrchestrator.cs#L815) |
| UI | `CancelRunButton`、`ResumeModeCheckBox`、`ResumePartialPathTextBox`、`PartialPathTextBox`（読取専用の予約名） | [ExecutionView.axaml](src/StudyReportEvaluator.App/Views/ExecutionView.axaml#L120) |
| 要求 | §10 checkpointと再開、AC-014／AC-015、TR-13／TR-14 | [requirements-definition.md](docs/requirements-definition.md#L477) |
| 既存テスト | `Resume_reuses_reference_and_completed_rows_then_runs_only_the_first_unfinished_row` | [DurableQuantificationOrchestratorTests.cs](tests/StudyReportEvaluator.App.Tests/Workflow/DurableQuantificationOrchestratorTests.cs#L124) |

### 1.2 不足しているもの（本件のスコープ）

| ID | ギャップ | 根拠 |
|---|---|---|
| **G1** | 停止完了後、再開指定への引き継ぎがない。`ResumePartialPath` は空、`IsResumeMode` は false のまま。利用者が `PartialPathTextBox` から手でコピーし、チェックボックスを入れる必要がある | `StartAsync` の完了処理に handoff がない（[ExecutionViewModel.cs](src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs#L1332)） |
| **G2** | 再開元 `.partial.xlsx` を選ぶ手段が素のテキスト入力だけ。入力Excelには native picker があるのに再開元にはない | [ExecutionView.axaml](src/StudyReportEvaluator.App/Views/ExecutionView.axaml#L126)、[InputView.axaml.cs](src/StudyReportEvaluator.App/Views/InputView.axaml.cs#L38) |
| **G3** | 再開条件の不一致が **run 開始後** にしか分からない。`QuantificationRunException(code)` が投げられ、画面には `CHECKPOINT_*` / `*_MISMATCH` の code が出るだけで、どの項目がどう違うかを示さない | `ValidateResumeAdmission` は `private static`、返り値は単一の status code 文字列 |
| **G4** | 「停止」の意味論が UI 上「cancel」であり、破棄ではなく中断であること・次に何をすればよいかを示していない | `RunStatusText` は「cancel 済みの部分結果をpartial checkpointへ保持しました。」で終わる |
| **G5** | 実行中にウィンドウを閉じると `Closing` を介さず `Closed` → `Dispose()` → `Cancel()` となり、直前に完了した行の checkpoint 保存完了を待たない | [MainWindow.axaml.cs](src/StudyReportEvaluator.App/Views/MainWindow.axaml.cs#L201)、[ExecutionViewModel.cs](src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs#L1396) |

### 1.3 非スコープ（現行要求に準拠するため変更しない）

| 項目 | 理由 |
|---|---|
| 学生行の途中 unit を checkpoint する | 要求 §10.5「同じ学生行の途中状態は完了扱いにせず、その行を再実行する」。変更には要求改訂と atomic 境界の再設計が必要で、本件の目的（停止→再開の導線）とは別課題 |
| 再開指定を `setting.txt` へ永続化する | 要求 §11（L641）「新規／再開・partial指定は設定fileへ永続化しない」で明示的に禁止。§13 により入力xlsxのpathも保存しない |
| runtime identity（CLI version／sha、SDK version）の完全一致要件の緩和 | 要求 §10.4。app 側はすでに major 一致で緩和済み（`SameApplicationMajor`） |
| 出力ディレクトリ内の再開可能 checkpoint 一覧 | picker で目的を満たす。一覧は partial を全件開いて envelope を読む必要があり、privacy 境界と I/O コストの検討が別途必要 |

---

## 2. 設計方針

> **`.partial.xlsx` を「再開ハンドル」の単一の実体として扱う。停止した run は同一セッションならワンクリック、アプリ再起動後なら partial を1つ選ぶだけで再開できる。復元は必ず明示操作とし、現在の状態を勝手に書き換えない。**

この方針が成立する根拠：`.partial.xlsx` は入力Excelの byte-copy に `Quantification_Checkpoint` sheet を足したもので、envelope に `InputPath` / `Input`（SHA-256・size・last-write）/ `DefinitionCanonicalJson` / `DefinitionSha256` / `NormalModelId` / `Runtime` / `FinalPath` / `PartialPath` が揃っている（[CheckpointEnvelope.cs](src/StudyReportEvaluator.App/Workbooks/Checkpoint/CheckpointEnvelope.cs)）。`CheckpointStore.Load(path, ct)` は partial 単体から envelope を読めるため、**設定ファイルへの永続化なしに再開条件を復元できる**。

派生する4つの決定：

1. **停止は破棄ではない。** UI 文言・状態表示を「中断」へ統一し、`.partial.xlsx` が残っていること、それが再開に使えることを停止完了時に必ず示す。
2. **再開条件の検査は run 開始前に行う。** `ValidateResumeAdmission` を項目別の結果を返す純粋関数へ切り出し、orchestrator と ViewModel の両方が同じ実装を使う。二重実装を作らない。
3. **不一致の解消は利用者の明示操作。** checkpoint から入力パス・モデルIDを「自動で当てはめる」ことはしない。項目別に「この checkpoint の入力を読み込む」「この checkpoint のモデルを選ぶ」ボタンを出し、押されたときだけ変更する。採点定義は Design step の所有物なので自動適用せず、不一致の事実だけを示す。
4. **アプリ終了は妨げない。** 確認ダイアログは出さず（要求 §11「warning操作をrun／cancel／resumeの条件にしない」）、中断要求と最終 checkpoint 保存の完了だけを有限 timeout で待って閉じる。

---

## 3. 要求定義の改訂

現行 §10 は「機構」を規定するが「中断→再開の引き継ぎ」と「事前検証」を規定していない。実装前に [docs/requirements-definition.md](docs/requirements-definition.md) を改訂する。

| ID | 対象 | 内容 |
|---|---|---|
| R1 | §10.6 新設「中断と再開の引き継ぎ」 | 中断は破棄ではなく `.partial.xlsx` を残すこと、中断完了時に再開元 path を画面へ提示すること、同一セッションでの明示的な再開操作を1つ提供すること、自動再開しないこと |
| R2 | §10.4 へ追記 | 再開条件の検査は run 開始前に行い、不一致は項目別（入力／定義／モデル／runtime／path）の safe error として日本語で表示する。回答・Prompt・reason・evidence 本文は含めない |
| R3 | §10.6 へ追記 | 再開元 `.partial.xlsx` は native picker または path 直接入力で指定する。picker 取消時は現在の状態を変更しない（§8.1 の入力 picker と同じ規律） |
| R4 | §11.3 実行画面の責務へ追記 | 「中断」「再開元の選択」「再開前検証の項目別結果」「checkpoint 条件への明示的な合わせ込み」を責務に追加 |
| R5 | §16 状態表へ行追加 | `app close 中の run` → 新規送信停止、行境界で中断、最終 checkpoint の保存完了を有限 timeout で待機、timeout 後も最後に atomic 保存した checkpoint から再開可能 |
| R6 | §17 AC 追加 | **AC-037**: 中断後、同一セッションでは明示操作1つで再開でき、アプリ再起動後は `.partial.xlsx` の選択だけで再開条件を検査できる。不一致は開始前に項目別へ表示し、checkpoint を変更しない |
| R7 | §18 TR 追加 | **TR-33**: 中断→handoff→再開、picker 取消の状態不変、項目別 mismatch 表示、app close 中断の deterministic test |

**R2 の注意**: §10.4 の既存文「不一致時は当該checkpointを変更せず、具体的なsafe errorを表示して再開しない」は維持する。追加するのは「検査のタイミングを run 開始前へ前倒しする」点だけで、検査項目そのものは変えない。

---

## 4. ADR

**`dev/docs/adr/0017-run-interruption-and-resume-handoff.md`**（現行最新は 0016）

記録する決定：

1. `.partial.xlsx` を再開ハンドルの単一の実体とし、再開指定を設定ファイルへ永続化しない（要求 §11 L641 との整合）。
2. 再開条件の検査ロジックを `ResumeAdmissionEvaluator` へ一元化し、orchestrator と ViewModel が同一実装を共有する。
3. checkpoint 条件への合わせ込みは自動適用せず、項目別の明示操作とする。採点定義は自動適用の対象外とする（利用者の Design 編集を失わせない）。
4. app close 時は確認ダイアログを出さず、有限 timeout の保存待ちのみを行う。
5. 却下案：(a) 再開指定の設定ファイル永続化 — 要求違反。(b) 出力ディレクトリの partial 自動走査と一覧 — privacy 境界と I/O コストの検討が未了。(c) 行内 unit 単位 checkpoint — 要求 §10.5 と衝突。

---

## 5. 実装タスク

### P1 — 再開条件検査の切り出し（他すべての前提）

**新規** `src/StudyReportEvaluator.App/Workflow/ResumeAdmissionEvaluator.cs`

```csharp
public enum ResumeAdmissionItem { PartialPath, InputIdentity, Definition, NormalModel, Runtime, CheckpointShape }

public sealed record ResumeAdmissionFinding(
    ResumeAdmissionItem Item,
    bool IsSatisfied,
    string StatusCode,   // 既存の CheckpointAdmissionStatusCodes / CheckpointStatusCodes を再利用
    string Description);  // 日本語。回答・Prompt・reason・evidence を含めない

public sealed record ResumeAdmissionReport(
    ImmutableArray<ResumeAdmissionFinding> Findings,
    string? BlockingStatusCode)   // null なら再開可
{
    public bool CanResume => BlockingStatusCode is null;
}
```

- [DurableQuantificationOrchestrator.ValidateResumeAdmission](src/StudyReportEvaluator.App/Workflow/DurableQuantificationOrchestrator.cs#L815)（L815–880）と `RuntimeCompatible` / `SameApplicationMajor` / `PathsEqual`（L1155–1183）をここへ移す。
- orchestrator は `ResumeAdmissionEvaluator.Evaluate(...).BlockingStatusCode` を使うだけにする。**判定順序と status code を現行と完全に一致させる**（既存テストが code を assert しているため）。
- `ValidateCompletedRowsAsync`（行の shape 検証、非同期・行読み取りを伴う）はコストが高いので **orchestrator 側に残す**。ViewModel の事前検査は envelope だけで判定できる項目に限定し、その旨を画面に明示する（「実行開始時に完了行の内容も検証します」）。

**テスト**: `tests/StudyReportEvaluator.App.Tests/Workflow/ResumeAdmissionEvaluatorTests.cs`（新規）— 6項目それぞれの一致／不一致、複数不一致時の `BlockingStatusCode` が現行 orchestrator と同じであること。

### P2 — 中断 handoff（G1・G4）

`src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs`

- `StartAsync` の完了処理（L1332 付近）に追加：`summary.StatusCode` が `Cancelled` かつ `summary.PartialPath` が非空なら
  ```csharp
  interruptedPartialPath = summary.PartialPath;
  ```
  を保持する。**`IsResumeMode` / `ResumePartialPath` は自動で書き換えない**（利用者が別の操作を始めている可能性があるため）。
- 新規プロパティ `HasInterruptedRun`、`InterruptedRunSummaryText`（例: 「中断しました。参照 3/3・行 12/40 まで保持しています。」）。
- 新規コマンド `ResumeInterruptedRunCommand`：`IsResumeMode = true`、`ResumePartialPath = interruptedPartialPath` を設定し、**`StartAsync` は呼ばない**。押下後に検証結果を表示し、利用者が「定量化を開始」を押す。要求の no-auto-run を守る。
- `RunStatusText` の `Cancelled` 分岐を改訂：「中断しました。`.partial.xlsx` に部分結果を保持しています。「中断したrunを再開」を押すと再開元を設定します。」
- `CancelCommand` は互換のため残し、`StopCommand` を別名で公開しない（コマンドは1つに保つ。変えるのは文言と Automation 名のみ）。
- `interruptedPartialPath` は `ApplyConfiguration` / `ClearConfiguration` の resume リセット（L1030／L1065）と同じ条件でクリアし、`ResumeResetReason` に理由を出す。

### P3 — 再開元 picker（G2）

`src/StudyReportEvaluator.App/Views/ExecutionView.axaml.cs`

- `IResumeCheckpointPicker` / `NativeResumeCheckpointPicker` を [NativeInputWorkbookPicker](src/StudyReportEvaluator.App/Views/InputView.axaml.cs#L30) と同形で追加。
  - `FilePickerFileType`: `"中断した run の checkpoint (.partial.xlsx)"`, `Patterns = ["*.partial.xlsx"]`
  - `TryGetLocalPath()` が null なら `ResumeCheckpointPickerPathUnavailableException`
- picker が `null`（取消）を返したら **`ResumePartialPath` を含め何も変更しない**。
- `isPicking` ガードで二重起動を防ぐ（InputView と同じ）。

`ExecutionView.axaml`

- `ResumePartialPathTextBox` の隣に「参照…」ボタン（`AutomationId="ExecutionPickResumeCheckpoint"`）。`IsVisible="{Binding IsResumeMode}"`。
- 既存の TabIndex 4→5 の間へ入るため、以降の TabIndex を再採番する。

### P4 — 再開前検証の表示と合わせ込み（G3）

`ExecutionViewModel`

- `ResumePartialPath` の setter（L563）からデバウンスして `PrepareResumeAsync` を起動する… **のではなく**、明示的に `ValidateResumeCheckpointCommand`（「再開元を確認」）で実行する。path 編集のたびにファイルを開くのを避け、自動 I/O を発生させない方針に合わせる。ただし P3 の picker 選択直後は確定した選択なので自動で1回実行する。
- `PrepareResumeAsync(string partialPath, CancellationToken ct)`:
  1. `checkpointStore.Load(partialPath, ct)` → 失敗なら `CheckpointLoadResult.Code` を日本語化して表示（`CHECKPOINT_SCHEMA_UNSUPPORTED` / `CHECKPOINT_HASH_MISMATCH` / `CHECKPOINT_INVALID`）。
  2. 成功なら `ResumeAdmissionEvaluator.Evaluate(envelope, 現在の snapshot／input／model／runtime)` を呼ぶ。
     - 現在 Input が未設定なら `InputIdentity` を「入力が未選択」として不一致にする。
  3. `ResumeFindings`（`ObservableCollection`）へ反映。
- 合わせ込みコマンド（押されたときだけ現在状態を変更する）:
  - `ApplyCheckpointInputCommand` — `envelope.InputPath` を Input step へ読み込ませる。`ExecutionViewModel` は `InputViewModel` を直接操作せず、`MainWindowViewModel` 経由のイベント（既存の `ModelCatalogRefreshed` と同じ `Func<..., Task>` 形）で依頼する。読み込み失敗時は現在状態を変えない。
  - `ApplyCheckpointModelCommand` — `envelope.NormalModelId` が現在のモデル一覧にあれば選択する。無ければ理由を表示して何もしない。
  - 定義不一致には合わせ込みコマンドを**出さない**。「採点設計が checkpoint と異なります。中断時の設計へ戻すか、新規 run を開始してください。」と表示するのみ。
  - runtime 不一致（アプリ更新後・CLI 更新後）にも合わせ込みコマンドを出さない。「このアプリ／CLI の版では再開できません」と表示する。
- `CanStart` の条件へ「`IsResumeMode` の場合は `ResumeReport?.CanResume == true`」を追加し、確実に落ちる run を開始させない。

`ExecutionView.axaml`

- 再開検証の結果を、既存の `ExecutionValidationSummary`（技術的な問題）と**同じ見た目・同じ操作規律**の一覧＋詳細で表示する。新規パターンを増やさない。
- `AutomationId`: `ExecutionResumeFindings`、`ExecutionResumeFindingDetail`、`ExecutionApplyCheckpointInput`、`ExecutionApplyCheckpointModel`、`ExecutionValidateResumeCheckpoint`。

### P5 — app close 時の安全な中断（G5）

`src/StudyReportEvaluator.App/Views/MainWindow.axaml.cs`

- `Closing` ハンドラを追加（現在は `Closed` のみ）。
  ```
  if (実行中でない || 既に停止待機済み) → そのまま閉じる
  else:
      e.Cancel = true
      停止待機済みフラグを立てる
      _ = ViewModel.Execution.StopAndDrainAsync(TimeSpan.FromSeconds(10))
            .ContinueWith(_ => Dispatcher.UIThread.Post(Close))
  ```
- 二度目の `Closing` では必ず閉じる（無限ループを作らない）。

`ExecutionViewModel`

- `StartAsync` が作る Task を `LastRunTask` として保持する（`LastLoginTask` と同じ形）。
- `StopAndDrainAsync(TimeSpan timeout)`:
  1. `Cancel()`
  2. `LastRunTask` を `timeout` 付きで待つ（`Task.WhenAny`）
  3. timeout しても例外にしない。orchestrator の `checkpointStore.Update` は `CancellationToken.None` なので、途中の保存は atomic replace により壊れた partial を残さない。
- `Dispose()` は現状どおり `runCancellation.Cancel()` を行う（`Closed` は `Closing` の後なので二重呼び出しは無害）。

### P6 — 文言・contract の更新

| ファイル | 変更 |
|---|---|
| [dev/docs/ui-layout-contract.md](dev/docs/ui-layout-contract.md) | ExecutionView へ追加した Control・AutomationId・TabIndex 再採番・`MinWidth 1024 / MinHeight 720` 内に収まることを記載 |
| [dev/docs/detailed-design.md](dev/docs/detailed-design.md) | §7 checkpoint 設計へ「再開前検証の一元化」と「app close の drain」を追記 |
| [dev/docs/architecture.md](dev/docs/architecture.md) | run flow 図へ Interrupt → Handoff → Preflight → Resume を追記 |
| [dev/docs/traceability.md](dev/docs/traceability.md) | AC-037 / TR-33 と test 名の対応を追加 |
| [dev/docs/implementation-status.md](dev/docs/implementation-status.md) | 本機能の状態を追加 |
| [dev/docs/readme-claim-ledger.md](dev/docs/readme-claim-ledger.md) | 公開 claim を出す場合のみ行を追加。出さないなら触らない |
| [docs/features.md](docs/features.md) | 実行の行を「新規／checkpoint再開、**中断と再開の引き継ぎ**、開始・停止、進捗」へ |
| [docs/getting-started.md](docs/getting-started.md) | 中断と再開の手順を追記（L127・L163 付近の既存記述と整合させる） |
| [docs/troubleshooting.md](docs/troubleshooting.md) | 再開できないときの項目別の見方（入力／定義／モデル／版）を追記 |
| [CHANGELOG.md](CHANGELOG.md) | `[Unreleased]` の `Added` へ |

---

## 6. テスト計画

| ID | 対象 | 内容 |
|---|---|---|
| **T-A** | `ResumeAdmissionEvaluatorTests`（新規） | 6項目それぞれの一致／不一致。複数不一致時の `BlockingStatusCode` が現行 orchestrator の判定順序と同一であること |
| **T-B** | `DurableQuantificationOrchestratorTests`（既存） | 既存の resume テストと mismatch テストが**無変更で通ること**。P1 のリファクタが挙動を変えていない証拠 |
| **T-C** | `ExecutionViewModelTests` | 中断完了後に `HasInterruptedRun == true`、`ResumeInterruptedRunCommand` が `ResumePartialPath` を設定し **run を開始しない**、`ApplyConfiguration` で handoff がクリアされ理由が出る |
| **T-D** | `ExecutionViewModelTests` | `PrepareResumeAsync` の項目別結果。入力未選択／定義不一致／モデル不一致／runtime 不一致／path 不一致それぞれの日本語説明。**回答・Prompt・reason・evidence を含まない**ことの assert |
| **T-E** | `ExecutionViewModelTests` | `IsResumeMode && !CanResume` のとき `CanStart == false` |
| **T-F** | `ExecutionViewTests` | picker 取消で `ResumePartialPath` を含む状態が不変。二重起動しない。AutomationId・keyboard 到達・200% scale |
| **T-G** | `MainWindowTests` | 実行中の `Closing` で `e.Cancel` が一度だけ立ち、drain 後に閉じる。timeout でも必ず閉じる。非実行中は即閉じる |
| **T-H** | `ExecutionViewModelTests` | `StopAndDrainAsync` が timeout しても例外を投げない |
| **T-I** | [tests/SystemTest-prompt.md](tests/SystemTest-prompt.md) | ST-UC-17（10人 fixture new→interrupt→resume E2E）へ、アプリ再起動を挟む resume と picker 経由の指定を追加 |

実行: `dotnet test` をソリューション全体で。CI は `CI=true` で live/external smoke を除外する既存の分離をそのまま使う。

---

## 7. 実施順序と依存

```
R1-R7 要求改訂 ──> ADR-0017
        │
        v
      P1 検査の切り出し ──> T-A, T-B（ここで挙動不変を確定させる）
        │
        ├──> P2 handoff ──> T-C
        ├──> P3 picker  ──> T-F
        │        │
        │        v
        └──────> P4 事前検証UI ──> T-D, T-E
        │
        └──> P5 close drain ──> T-G, T-H
                 │
                 v
              P6 文書 ──> T-I ──> CHANGELOG ──> 版 bump（0.9.0）
```

各タスク完了時に敵対的レビューを行い、指摘を反映してから次へ進む（本リポジトリの既存プランと同じ運用）。

---

## 8. リスクと対処

| リスク | 影響 | 対処 |
|---|---|---|
| P1 のリファクタで判定順序が変わり、mismatch 時の status code が変化する | 既存テスト・要求 §10.4 の契約を破る | P1 を**純粋な移動**に限定し、T-B で既存テストを無変更で通す。順序変更が必要になった場合は要求改訂として別扱いにする |
| `ApplyCheckpointInputCommand` が Input step を書き換える横断操作になる | Design step の draft や現在の選択を壊す | ExecutionViewModel から直接触らず、`MainWindowViewModel` 経由のイベントで依頼する。読み込み失敗時は現在状態を変えない。既存の `ApplyConfiguration` の resume リセット経路と整合させる |
| `Closing` での `e.Cancel` がアプリを閉じられなくする | 致命的 | 停止待機済みフラグで二度目は必ず閉じる。有限 timeout（10秒）。T-G で両方を assert |
| partial の絶対 path 一致要件により、ファイルを移動した利用者が再開できない | 「再開できない」の主要因になり得る | 本件では要件を変えない。`ResumeAdmissionItem.PartialPath` の不一致説明に「checkpoint は作成時の場所から移動せずに使ってください」を明記し、troubleshooting へも記載 |
| アプリ更新後に既存 partial が再開できない | 長時間 run の途中でアプリを更新した利用者が損をする | app は major 一致で許容済み。CLI／SDK の更新は要求 §10.4 の完全一致。runtime 不一致の説明で「中断した run は更新前の版で完了させてください」と示す |
| 表示文字列に評価内容が混入する | privacy 境界（§14）違反 | `ResumeAdmissionFinding.Description` は固定文＋ID・SHA の先頭のみ。T-D で assert。既存の `ToString()` は `<redacted>` 規約を踏襲 |

---

## 9. 完了条件

1. R1–R7 が [docs/requirements-definition.md](docs/requirements-definition.md) へ反映され、ADR-0017 が ADOPTED である。
2. P1–P6 が実装され、T-A〜T-I が PASS。既存テストが無変更で PASS。
3. 実機で次が成立する。
   - 40行の run を12行目で中断 → 「中断したrunを再開」→ 「定量化を開始」で13行目から再開し、参照回答を再生成しない。
   - アプリを終了して再起動 → 同じ入力を読み込み → picker で `.partial.xlsx` を選択 → 検証が全項目 OK → 再開が成立する。
   - 採点設計を1箇所変えてから再開を試す → **開始前に**「採点設計が checkpoint と異なります」と項目別に出て、開始できず、`.partial.xlsx` が変更されない。
   - 実行中にウィンドウを閉じる → 10秒以内に閉じ、`.partial.xlsx` が破損せず、再起動後に再開できる。
4. [CHANGELOG.md](CHANGELOG.md) `[Unreleased]` 更新、`dev/version.ps1 bump minor` で `0.9.0`、`dev/version.ps1 verify` と `dev/version.tests.ps1` が PASS。

# コスト表示 — 実行状況と残タスクの再監査

日付: 2026-09-17。対象: HEAD `730a225`と作業ツリーのコスト表示変更。別途進行している中断・再開UIの変更は今回の完了判定に含めない。commit／push／公開はしていない。

## 結論と前回報告の訂正

**SDKから観測できたコスト情報を画面とJSONLに出す実装はある。ただし、元プランの全項目完了・実際のAIクレジット消費量表示の完了とはいえない。**

- 前回「33/33成功」はrunnerの出力自体としては正しかったが、権限不足でreturnしたテスト1件を含んでいた。シンボリックリンク保護の実検証成功を意味しない。
- 旧調査メモの「ログrotation」「checkpoint経由でコストを再開累計へ継承」「fsync保証」等は実装から確認できない。メモを根拠にしない。実際は1ファイル上限、今回ジョブのみの独立集計、非同期flushである。[S1–S3]
- SDK報告のnano-AI unitsを表示することと、換算根拠・実契約で確認したAIクレジットを表示することは別である。[W1–W4]

## 1. 実装状況（元プランT01〜T09）

| タスク | 確認状況 | 根拠／制約 |
|---|---|---|
| T01 SDK・課金単位 | SDK adapter実装あり。公式資料を再取得。クレジット換算は未確定 | W1は`/1e9`の例を載せる一方、課金資料での確認を要求する。W2〜W4に1 credit=$0.01の説明はあるが、取得本文でnano-AI unitsとの直接換算を確認できなかった。実サービス値も未確認 |
| T02 仕様・privacy | 利用者文書更新済み、実装差分を本書で明示 | 今回分、本文非記録、モデル識別子匿名化、Windowsの保持整理、既存Excel維持。元プランの名称・全項目がそのまま実装されたとはしない |
| T03 集計 | attemptごとのrevision置換、null／0区別、範囲・overflow検証あり | S1、S4。今回6テストを実行。品質は一部attempt単位で保守的に扱い、項目別の完全なprovenance実装ではない |
| T04 SDK接続 | 通常・参照・固有・類似度にtracker注入、イベントと破棄前RPC取得あり | S3。実際のSDKセッションで取消・遅延イベント・全4種を通した検証は未実施 |
| T05 ジョブログ | JSONL、容量上限、終端用領域、保存失敗表示、安全なreader・Windows保持整理あり | S2、S5。画面内ログの不足を今回修正。readerは内部機能であり、過去ファイルの読込結果を画面に出す導線はない |
| T06 実行・結果接続 | callback、finally最終通知、contextへのスナップショット受渡しあり | S3、S6。前回のfake UIテスト履歴はあるが、今回全経路を再実行したとはしない |
| T07 画面 | 実行・結果画面と共有コスト／ログタブあり | S6。今回XAML変更なし、native実測・全寸法の再検証なし |
| T08 回帰E2E | 既存Excel・checkpointと別の集計方式 | 全C01〜U02を通した試験は未実施。特に実SDKライフサイクルと再開E2Eの追加検証は残る |
| T09 文書・最終検証 | 文書訂正、最小テストのTRX保存済み | 実AI・実課金照合・native・配布物再作成／公開は未実施 |

## 2. 今回見つけて実行した残タスク

| ID | 不足 | 対応結果 |
|---|---|---|
| R01 | JSONLにはコストがあるが、画面内のログ行は入力・出力だけだった | `JobCostLogReader.Render`に推論・cache・nano-AI units・premium・クレジット未換算の表示を追加。最新200行の上限と累計置換方式を維持 |
| R02 | JSONLに原単位方針・今回分scopeの明示がなかった | 固定値の`AggregationScope`／`UnitPolicy`を追加。自由文やSDK応答全文を追加せず、schema 1の数値の意味を変えない |
| R03 | Windows以外の保持整理の説明が曖昧だった | 非対応を明示する状態文言と利用者文書に変更。Windows限定の安全な削除を他OSの危険なpath削除で代替しない |
| R04 | 実行不能テストをreturnで成功扱いし、任意IOExceptionまで隠していた | `Assert.SkipUnless`／`Assert.Skip`を使用。IOExceptionのスキップはWindows error 1314（必要特権なし）に限定 |
| R05 | 文書の全完了表記・内部readerのUI説明が実装を超えていた | プラン、privacy、troubleshootingを訂正。本書に事実・差分・残タスクを記録 |
| R06 | 前回は実行結果を永続的なテスト証跡で示していなかった | 変更対象の2クラスだけを実行し、TRXを保存。単位／scope／画面ログの値は既存テストへassertを追加し、ケース数は増やしていない |
| R07 | JobStartedに実行条件がなかった | `JobUsageContext`を追加。実際のassembly／requestからアプリ・SDK・CLIの数値版、匿名化要求モデル、並列度、resume有無を開始・終端ログへ記録。任意本文・版のsuffixは保持せず、nullは不明 |
| R08 | 観測値の下方訂正が表示されなかった | attemptの`HasDownwardCorrection`を保持し、詳細欄と該当ログ行へ表示。新しいrevisionの値へ置換する既存の集計は維持 |
| R09 | attemptのretry番号／相関ID／終端結果（成否・スキーマ不正・中断等）を記録する専用イベントがなかった | `UsageAttemptOutcome`列挙、`RetryAndCleanupCoordinator`が生成した相関ID・実試行番号をtransport経由でSDKセッションへ伝播し、`JobUsageTracker.RecordAttemptOutcome`が`JobCostEvent.AttemptFinished`として1回だけ記録。数値観測（revision／IsFinished）は変更しない。既存のRecordingAttempt系テストと`AttemptOutcomeLoggingTests`で検証 |
| R10 | 項目別の取得元が失われ、モデル内訳とセッション総量の不一致を示せなかった | `MetricProvenance`で項目別に取得元と部分取得を保持し、`UsageProvenance.MergeFinal`でイベント・最終RPC・最後の呼び出しの優先順と取得元を同時に決定。最終RPCの費用項目はトークンがLastCallでも単独で観測完了とし、イベント・LastCallは常に部分取得。`MetricObservation.SourceCounts`と詳細欄に項目別の取得元を表示。nano-AI unitsは同一の最終RPCで全モデルの費用を取得できた場合のみ比較し、`ModelCostComparison`と不一致件数を画面・JSONL（終端行を含む）へ記録。配賦・補正はしない。外部から渡された取得元・比較結果はtrackerで再検算し、根拠のない主張は`NotComparable`へ降格 |

## 3. なお残る項目と実行条件

### 外部根拠・環境が必要

1. **AIクレジット数値表示（G2）:** 固定SDK／CLIの`TotalNanoAiu`と契約上のAIクレジットの対応確認。今はnano-AI units原値・premium値を表示し、AIクレジットを0や架空値で表示しない。確定額表示は未実装・未完了。
2. **実Copilot検証:** 認証済みの対象アカウント、利用モデル、課金を伴う少量の合成呼出しへの承認が必要。既存認証情報の読出しや無断の実AI呼出しはしていない。実契約で値が返らなければ未取得を正常表示する。
3. **native Windows確認:** 実DPI・keyboard・画面内ログの到達性・既定アプリ起動の確認。headlessをnative成功へ読み替えない。利用者設定／起動時の認証再確認を伴わない隔離環境が必要。
4. **シンボリックリンク保護テスト:** Developer Mode等で作成権限のあるテスト環境が必要。本環境ではスキップ。UAC・管理者昇格・保護設定変更は実施しない。

### 元の詳細プランに対する未完了・実装差分

以下も元プランを全完了とする場合には別途解消が必要であり、勝手に完了扱いしない。今回はユーザー指定の最小テスト方針を優先し、動いている採点境界への大規模変更・全E2E追加は行っていない。

- attemptのretry番号／成否専用イベントはR09で補完済み（`JobCostEvent.AttemptFinished`、相関ID、実試行番号、`UsageAttemptOutcome`）。ただし実Copilotセッションを通した全経路（AuthRequired・NetworkFailed等の実SDK例外）での検証はfakeベースのみで、実認証環境での確認は別途必要。
- 最終RPCとイベントの項目別取得元、モデル内訳と総量の不一致状態はR10で補完済み。ただし比較は同一の最終RPCで全モデルの費用を取得できた場合に限定され、実SDKで内訳が欠落する頻度は未確認。モデル内訳を総量へ上乗せしない方針は維持。減少訂正の専用フラグはR08で補完済み。
- 保持上限によるモデル内訳の切り詰めは部分取得として示すが、どの識別子が落ちたかは示さない（匿名化した識別子でも上限を超えた列挙はしない方針）。
- 破損した過去ログの再取込UI、ログ保存失敗の同一ジョブ内の再保存UIはない。最新ジョブのメモリ履歴と既定アプリでファイルを開く導線を提供する。
- SDKセッションを通す全ライフサイクルE2E、全レイアウト／アクセシビリティ条件の検証は未実施。今回の限定テストをその代替とはしない。

### 残る差分の分類（2026-09-24追記）

| 分類 | 項目 | 根拠 |
|---|---|---|
| 非目標（ADR-0018） | 過去ログの再取込UI、再開前からの通算、予算超過での停止 | [ADR-0018](../dev/docs/adr/0018-job-cost-observability.md)の25行目「過去ジョブの累計表示、再開前からの通算、ログの再取込UI、予算超過による停止は本決定に含めない」 |
| 非目標（本書の方針） | 保持の上限でモデル内訳を切り詰めたとき、どの識別子が落ちたかは示さない | 上の3つ目の項目（匿名化した識別子でも、上限を超えた列挙はしない） |
| 現状のとおりと記録 | 過去ファイルの読込結果を画面に出す導線（readerは内部機能） | §1のT05行 |
| `OWNER`の判断待ち | 同じジョブの中でログの保存をやり直すUI | [元プラン](20260917-job-cost-observability-implementation-plan.md)の§7.3では「画面からの明示保存再試行を提供する場合も」と任意の扱い。実装するかどうかは所有者が判断する |

## 4a. R09（再試行番号・相関ID・評価結果ログ）の実行結果
対象: `CostAttemptLifecycleTests`、`AttemptOutcomeLoggingTests`（新規）、既存回帰の`RetryAndCleanupCoordinatorTests`・`EphemeralEvaluationRunnerTests`・`AuxiliaryEvaluationRunnerTests`・`JobUsageTrackerTests`・`JobCostBackendTests`。実AI・実認証は使用していない（すべてfake transport/attempt）。

- ソリューション全体ビルド: 0警告・0エラー。
- 変更範囲の限定テスト: **29件中28成功・1スキップ（Symlink検証、権限不足のため既知のスキップ）・失敗0件**。
- 既存回帰テスト（Coordinator／Runner系）: **35件成功・失敗0件・スキップ0件**。
- 上記いずれもCLIでの`dotnet test --filter`実行。TRXファイルへの保存は行っていない（今回は端末出力で結果を確認）。

## 4. 今回の最小テスト結果

対象: `JobUsageTrackerTests` と `JobCostBackendTests` のみ。実AI、実学生データ、利用者設定への書込なし。

- エディター経由: 11成功・1件「Test could not be run」。スキップ表現が不明確だったため、CLIで正式結果を確認。
- CLI初回: **12件中11成功・1スキップ・失敗0件**。build含む約10.2秒（端末出力）。
- R07／R08後の再実行は別testhostのDLLロックでbuildのコピー工程が失敗。所有不明のprocessを終了せず出力先を分離して解消。
- **最終実行: 12件中11成功・1スキップ・失敗0件。** build含む約19.2秒（端末出力）、テスト本体約1秒。runtimeヘッダーのJSON往復・機密文字列非保持と下方訂正フラグを含む。
- スキップ: `Symlink_is_not_followed_by_reader_or_retention_when_creation_is_available`。理由は必要なWindows特権なし。
- 最終証跡: `TestResults/job-cost-audit-20260917/job-cost-audit-final.trx`（ローカル生成物。公開checkoutへの存在保証なし）。初回は同directoryの`job-cost-audit.trx`。
- 対象ファイルのエディター診断: エラーなし。全体テストは再実行していない。

## 4b. R10（項目別取得元・モデル内訳の不一致）の実行結果

対象: `UsageProvenanceTests`（新規）と既存のコスト系（`Usage`・`JobCost`・`CostAttemptLifecycleTests`）。実AI・実認証は使用していない。

- ソリューション全体ビルド: 0警告・0エラー。
- コスト系限定テスト: **58件中57成功・1スキップ（Symlink、権限不足）・失敗0件**。
- 途中で`MetricObservation`へ不変辞書を追加した際、recordの既定等価が参照比較になり`AttemptOutcomeLoggingTests`が2件失敗。値としての等価を定義して解消。
- 回帰確認（`EphemeralEvaluationRunnerTests`・`AuxiliaryEvaluationRunnerTests`・`RetryAndCleanupCoordinatorTests`・`ExecutionViewTests`）: 72件中70成功・**2失敗**。失敗は`ExecutionViewTests`の取消ボタン文言（`cancel` 期待に対し実際は「中断」）と中断時の状態文言で、並行する中断・再開機能が`ExecutionView.axaml`・`ExecutionViewModel.cs`を変更した一方で同機能のテストが未追従なもの。コスト機能の変更とは無関係であり、他機能の実装中コードへは手を入れていない。

## 4c. R11（文書契約への反映）の実行結果

対象: `DocumentationContractTests`のみ（実行時間を最小化するため、他のsuiteは再実行していない）。

- 結果: **22件中21成功・1失敗**。
- 残る1件は`Package_script_includes_every_public_document_image_and_license`。`WindowsSingleFile.pubxml`に`ItemGroup`が2つあるため`Assert.Single`が失敗するもので、該当fileはHEADから未変更。**本作業前（HEAD相当の状態）でも同じく1件失敗することを、文書変更を退避した状態で実測確認済み**（21成功・1失敗）。単一EXE同梱契約の既存破綻であり、本機能の対象外のため変更していない。
- 途中で、並行する中断・再開機能が要求定義§19の必須記述「T01時点の未実装・試験NOT_RUNは履歴」を落とし、追跡表headerの要求版日付を`2026-09-07`から`2026-09-17`へ変え、AC-038行をAC-015の直後へID順外で挿入していたことを検出。いずれも同じ文書契約試験を壊し、AC-039の登録と同時には満たせないため、記述の復元と行順の修正だけを行った。再開機能の実装code・試験には手を入れていない。

## 5. 出典

- S1: [JobUsageTracker.cs](../src/StudyReportEvaluator.App/Usage/JobUsageTracker.cs)、[JobCostSnapshot.cs](../src/StudyReportEvaluator.App/Usage/JobCostSnapshot.cs): 今回分scope、revision、観測状態、画面履歴。
- S2: [JobCostLogger.cs](../src/StudyReportEvaluator.App/Logging/JobCostLogger.cs)、[JobCostLogEntry.cs](../src/StudyReportEvaluator.App/Logging/JobCostLogEntry.cs): JSONL・数値ログ表示・容量上限・原単位・Windows保持整理。
- S3: [EphemeralEvaluationRunner.cs](../src/StudyReportEvaluator.App/Copilot/EphemeralEvaluationRunner.cs)、[ExecutionViewModel.cs](../src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs)、[SdkUsageAdapter.cs](../src/StudyReportEvaluator.App/Usage/SdkUsageAdapter.cs): SDK取得と全runnerへの注入。
- S4: [JobUsageTrackerTests.cs](../tests/StudyReportEvaluator.App.Tests/Usage/JobUsageTrackerTests.cs): null／0、二重加算防止、コスト文字列、scope、単位、JSONL安全性。
- S5: [JobCostBackendTests.cs](../tests/StudyReportEvaluator.App.Tests/Logging/JobCostBackendTests.cs): ファイル読込・保持・制限・スキップ条件。
- S6: [JobCostView.axaml](../src/StudyReportEvaluator.App/Views/JobCostView.axaml)、[JobCostViewModel.cs](../src/StudyReportEvaluator.App/ViewModels/JobCostViewModel.cs)、[ResultsOutputViewModel.cs](../src/StudyReportEvaluator.App/ViewModels/ResultsOutputViewModel.cs): UI接続・安全なパス・結果snapshot。
- S7: [JobUsageContext.cs](../src/StudyReportEvaluator.App/Usage/JobUsageContext.cs)、[UsageMath.cs](../src/StudyReportEvaluator.App/Usage/UsageMath.cs): ヘッダー値の数値版限定・匿名化・下方訂正検出。
- W1: [GitHub SDK Usage and billing metrics](https://github.com/github/copilot-sdk/blob/main/docs/features/usage-and-billing.md)、[Streaming events](https://github.com/github/copilot-sdk/blob/main/docs/features/streaming-events.md)。2026-09-17取得。main可変資料で固定SDK互換の証拠ではない。
- W2: [個人のusage-based billing](https://docs.github.com/en/copilot/concepts/billing-and-usage/individuals/billing)。2026-09-17取得。
- W3: [組織のusage-based billing](https://docs.github.com/en/copilot/concepts/billing-and-usage/organizations-and-enterprises/billing)。2026-09-17取得。
- W4: [モデルと料金](https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing)。2026-09-17取得。料金表を製品にハードコードしていない。

W2〜W4の1 AI credit=$0.01の記述は、SDKのnano-AI unitsとの換算や実アカウントへの課金一致まで証明しない。本書は請求監査の保証ではない。
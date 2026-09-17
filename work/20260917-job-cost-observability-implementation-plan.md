# 画面・ジョブログへのコスト情報表示 — 詳細実装プラン

- 作成日: 2026-09-17
- 状態: **観測値の画面・JSONL表示は実装あり。計画全体の完了ではない。クレジット数値の換算と実SDK／native検証は未完了。最新状況は[実行状況と残タスク](20260917-job-cost-execution-audit.md)を参照。**
- 当初の計画作成基準: commit `8400f3f50a835b36370fb654ff07204578247fd8`。2026-09-17の再監査基準はHEAD `730a225`と未コミットの作業ツリー。以下の計画・当初調査の記述を実装完了証拠として扱わない。
- 要求: Excelを開かなくても、アプリ画面とジョブログでトークン消費量・クレジット等のコスト情報を確認できるようにする。
- 読み方: §1と§12は確認した事実・出典。§2〜11のクラス名、仕様、数値、工数は**提案**であり、既存実装・実測結果ではない。

## 1. 確認済みの事実と設計への影響

| 確認済み事項 | 設計への影響 | 出典 |
|---|---|---|
| アプリは `GitHub.Copilot.SDK` 1.0.11 を参照する | 最新ドキュメントだけで実装を決めず、この版の型・同梱CLIで確認する | S01 |
| 現行 `GetUsageAsync` は `ModelMetrics` の5種類のトークン数を集計。なければ `LastCallInputTokens`／`LastCallOutputTokens` へフォールバック、例外時は `Unavailable` | 最後の呼び出しだけの値を全消費量と表示しない。取得元・取得範囲を保持する | S02 |
| 通常・補助評価は `SendAndWaitAsync` の後で使用量を取得している | 送信中の例外・取消・タイムアウトでも可能な限り観測する経路が必要 | S02、S03 |
| 再試行コーディネーターは各attemptの観測トークンを加算する | 新しいジョブ集計と既存集計を二重加算しない | S04 |
| durable集計は参照回答と `CompletedRows` に含まれる通常・固有・類似度結果から作る | 保存されなかった途中行の消費量を扱うため、採点結果集計から独立した観測台帳を作る | S05 |
| 画面用境界は通常・補助runnerを生成し、durable進捗を既存進捗へ変換する。完了は `ExecutionRunContext` で結果画面へ渡す | ここをジョブスコープの生成・観測通知・最終スナップショットの接続点とする | S06 |
| `SafeLogger` は閉じたコード・試行回数等・生成IDだけを受ける。既定の `None` は何も記録しない | 既存loggerの存在をジョブログ保存の実装とみなさない。専用の型付きコストログと実ファイルsinkを追加する | S07 |
| 現行プライバシー文書はトークン使用量数値をapplication logへ出さないと明記している | 数値だけを専用ジョブログに記録する方針へ文書・テストを明示的に改訂する | S08 |
| checkpoint schemaは1で未知メンバーを拒否する | コスト用プロパティをcheckpointへ無計画に追加しない | S09 |
| UIは通常最小1024×720 DIP、初期1180×800 DIP、操作44 DIP・本文14 DIP等の契約を持つ | カード追加による切れ・操作不能・外側スクロールの増加を防ぐ | S10 |

### 1.1 クレジット関連APIの確認状況

導入済みNuGetパッケージのXMLドキュメントから、次の**メンバーの実在と説明**を確認した。[S11]

| メンバー | SDKの説明 | 本機能での扱い |
|---|---|---|
| `Rpc.UsageGetMetricsResult.TotalNanoAiu` | session-wide accumulated nano-AI units cost | セッション累計のSDK報告値の候補 |
| `Rpc.UsageMetricsModelMetric.TotalNanoAiu` | accumulated nano-AI units cost for this model | モデル別内訳の候補。セッション全体値と足し合わせない |
| `Rpc.UsageGetMetricsResult.TotalPremiumRequestCost` | user-initiated premium request cost。倍率により小数になり得る | プレミアムリクエスト消費量として別単位で扱う。整数件数／通貨ではない |
| `AssistantUsageCopilotUsage.TotalNanoAiu` | total cost in nano-AI units for this request | 呼び出し単位の途中観測候補 |
| `AssistantUsageData.Cost` | model multiplier cost for billing purposes | AIクレジットや円・ドルと解釈しない |

公式SDK資料は、途中表示用に `assistant.usage`、最終集計用に `session.usage.getMetrics` を案内している。ただし、nano-AI unitsからAIクレジットへの厳密な換算とpremium request会計の意味はGitHub課金ドキュメントを正本として確認するよう注意している。資料の `/ 1e9` 例だけでは本製品の課金表示仕様を確定しない。[S12]

**未確認:** 固定CLIが各契約・各モデル・`auto`で返す実データ、欠落と0の区別、イベント到着順・重複・最終確定タイミング、正確な課金換算、請求画面との一致。SDK XMLに項目があることは実データ取得成功の証拠ではない。SDK propsのCLI既定値は1.0.79だが、実検証時は生成manifestの版・SHAを記録する。[S11]

## 2. 実装範囲と非目標

### 2.1 必須範囲

1. 「3 実行」画面に、実行中／終了後の今回ジョブのコスト要約を表示する。
2. 「4 結果」画面に、対応するジョブの最終観測値とログへの導線を表示する。
3. ジョブ単位のUTF-8 JSON Linesログを作成し、画面内でも直近のログを読めるようにする。
4. 入力・出力トークン、利用可能な推論／キャッシュ内訳、AIクレジット関連のSDK報告値、プレミアムリクエスト消費量を扱う。
5. 通常評価・参照回答生成・固有評価・類似度評価・それぞれの再試行を対象にする。
6. 成功／技術失敗／取消／タイムアウト／出力失敗に関係なく観測済み値を残す。
7. 取得できない値は理由付きで表示し、0や推定値に置き換えない。

### 2.2 推奨する境界

- 「Excelではなく」は**画面とジョブログを新機能の主経路にする**と解釈する。既存Excel項目の削除までは要求されていないため、既存出力は互換性維持のため残し、新しいコスト機能をExcelに依存させない。
- 既存 `EvaluationTokenUsage` とcheckpoint schema 1の意味・保存形式を維持する。新コスト観測は別型で管理する。
- 今回は予算超過による停止、アカウント月次請求管理、全ジョブ検索画面、通貨換算、事前料金見積もり、外部テレメトリー送信を追加しない。
- トークン単価×件数やモデル倍率から「実際のクレジット」を自作しない。アカウント残量の前後差も、他アプリの消費を含み得るためジョブ消費量の代替にしない。
- 設定画面やログ表示を開くだけで、認証／AI呼び出し／課金APIの照会を開始しない。

## 3. 表示値の意味・品質を先に確定する

### 3.1 表示項目

| 項目 | 表示方針 |
|---|---|
| 入力トークン／出力トークン | 別々に整数表示。未取得は `—（未取得）` |
| 推論／キャッシュ読み取り／書き込み | 詳細に表示。返却されなければ `—（SDK未提供）` |
| 合計トークン | 入力＋出力の重複関係をT01で確認できた場合に限り表示し、定義を明記。推論・キャッシュを無条件に上乗せしない |
| AIクレジット | T01で単位・換算を確認できた値のみ `SDK報告値` として表示。確認できない間は `—（課金単位未確認）`、詳細に `nano-AI units（SDK報告値）` を示す |
| プレミアムリクエスト消費量 | クレジットとは別行。小数を保持し、通常の送信回数と区別する |
| 観測状態 | 実行中／観測完了／一部取得／未取得／未送信／異常値。項目別に管理する |
| 観測範囲 | 今回の起動操作に対応するジョブのみ。送信attempt数・取得attempt数・不明attempt数と最終観測時刻 |
| ログ状態 | 保存中／保存済み／保存失敗／詳細記録上限到達。採点の成否とは別表示 |

画面に「GitHubから取得できた使用量です。請求確定額・アカウント全体の利用量ではありません。」を表示する。

### 3.2 データモデル案

`App/Usage/` に次を新設する。名称は提案であり、SDK型ではない。

- `UsageMetricValue`: 値、単位、状態、取得元、範囲、観測時刻。未取得の値はnull。項目別の観測済み合計と欠落数を保持する。
- `AttemptUsageSnapshot`: JobId、AttemptId、operation種別、再試行番号、セッション観測revision、要求モデル／報告モデル、各metric、final観測状態。
- `JobCostSnapshot`: ジョブID、revision、開始／終了時刻、実行状態、項目別集計、送信状況、内訳、ログ状態。
- `JobUsageTracker`: スレッドセーフなattempt単位の置換更新と、immutableなジョブスナップショット生成。
- `IJobUsageObserver`: UI／ジョブログ向けの型付き通知境界。

保存は整数トークン、SDK型の範囲に適合する精密な数値を使う。nano-AI unitsとpremium値の実際の.NET型・nullable性はT01で確認し、正確に保持できるdecimal等へ変換する。単位を混ぜず、計算overflow・負数・NaN／Infinityは異常値として扱う。無言のclampやゼロ化はしない。

仮にAIクレジット換算を `nanoAiu / 1,000,000,000` と確定した場合でも、内部の原値を維持し表示時だけ丸める。小さい非ゼロ値が `0` に見えない表示桁数／閾値表示を試験する。換算規則の版・出典をログヘッダーへ固定コードで記録する。

### 3.3 不明と0の区別

- 明示的に返された0: `0（SDK報告値）`。
- フィールド欠落／RPC非対応／取得失敗: null＋閉じた理由コード。
- 一部attemptのみ観測: `観測済み N（一部未取得）`。全体の確定消費量としない。
- 取得attempt数はmetricごとに数える。入力トークン取得済み・クレジット欠落のattemptは、入力側では取得済み、クレジット側では不明とする。attempt開始数・送信確認数・送信不明数は別counterとし、取得数の分母に未送信attemptを混ぜない。
- AI送信が確実に0件: `未送信（このジョブのAI送信なし）`。RPCが0を報告したこととは区別。
- 送信受付を確認できない通信失敗: `送信状況不明`。未送信とはしない。
- `LastCall*`だけ取得: `最後の呼び出しのみ` として部分取得。複数呼び出しの合計へ昇格させない。
- `auto`は要求モデルとして保持し、報告モデルがあれば別に示す。返却がなければ特定モデルを推測しない。

## 4. 取得・集計ライフサイクル

### 4.1 ジョブの開始と終了

1. `QuantificationRunBoundary` の各 `RunAsync` にジョブスコープを作り、JobIdを生成する。ログ開始に失敗してもin-memory集計は動作させる。
2. 同じtrackerを通常・参照・固有・類似度runnerへ明示注入する。static/global集計にしない。
3. AttemptIdは生成IDとし、学生名・行内容・パス・Promptから作らない。再試行は別AttemptIdにする。
4. 採点結果が保存される前に観測をtrackerへ渡す。結果の棄却・checkpoint書き込み失敗で観測値を削除しない。
5. boundaryのfinallyで最終スナップショットと終端ログを生成する。`RunSummary` が返らない例外経路も対象とする。
6. 正常returnでは同じ最終スナップショットを結果表示へ渡す。例外経路では実行画面のコストとログを残し、成功した採点結果を捏造しない。

### 4.2 セッションからの取得

- `SdkEphemeralCopilotSession` 内で、送信前から使用量イベントだけを購読する。イベント全体・SDKオブジェクト全体をログへserializeしない。
- 実行中は `assistant.usage` の確認済み数値から途中観測を更新する。context-window占有率は消費量とは別なので本集計に使わない。
- 成功時はセッション破棄前に最終metricsを取得する。取消／タイムアウト／失敗時も、必要なAbortを優先したうえで破棄前にbest effortの最終取得を行う。
- 最終取得はキャンセル済みのrun tokenをそのまま使わず、専用の有限期限を持つ。初期提案は最大2秒／attempt、追加の無限retryはしない。セッション操作の並行可否をT01で確認し、送信処理が収束しない場合は危険な同時RPCを避けてイベント観測だけを部分値として残す。
- SDKのlate task／遅延イベントは購読解除・終端封印で隔離し、破棄済みセッションや次のジョブへ書き込ませない。同期contextを待たずcleanupできるようにする。
- トークン／クレジット取得失敗だけで採点を再送しない。観測の失敗が評価・Abort・Dispose・Deleteの結果を上書きしない。
- 最終取得フックは一箇所へ集約する。通常／補助runnerとコーディネーターから同じfinal値を複数回取得・計上しない。既存 `GetUsageAsync` は必要なら取得済み値の互換投影として残す。

### 4.3 二重加算防止と整合

- trackerは `AttemptId → 最新スナップショット` を保持し、同一attemptの累計更新は**置換**する。
- 呼び出しイベントの重複排除は、固定SDKで確認した一意ID・順序情報に基づく。識別性が証明できない場合はイベント合計を完全取得とせず、最終metricsを優先する。
- 最終セッション累計にイベント合計を上乗せしない。各metricについて取得元とcoverageを比較して採用する。
- session全体のnano-AI unitsとモデル別内訳も加算しない。内訳合計が全体と異なる場合は相違を記録し、架空のモデル配賦で一致させない。
- 新しいrevisionの値が小さくても `max(旧,新)` で隠さず、訂正／不整合として明示する。古いrevisionは無視する。
- 採点結果由来の `RunSummary.OperationTokenUsage` は新trackerへ再加算しない。ジョブログ再読込時もイベント更新と終端スナップショットを合算しない。
- UIの通知は集約し、初期提案は最大4回／秒。終端通知は間引かず即時反映。実測して調整する。

## 5. 再開・異常終了・互換性

### 5.1 初期リリースで保証する範囲

- 「開始」「checkpointから再開」の各明示実行ごとに新JobIdを作り、画面・ログの主表示は**今回分**に限定する。
- 再開前の使用量を今回分に混ぜない。既存checkpointは今回追加のクレジット情報を持たないため、過去クレジットを0で補わない。
- 過去ログは元のJobIdのファイルとして残す。checkpoint名や入力ファイル名だけから同一ジョブ系列を推測しない。
- 「再開前からの累計コスト」の自動表示は初期範囲外。必要になった場合は、明示的な系列ID・sidecar参照・欠落状態・移行設計を別途追加する。
- 既存checkpoint schema、admission検証、Excelの既存使用量出力は変更しない。互換投影は新ジョブコストとは別の既存集計範囲であることを文書化する。

### 5.2 終了状態の扱い

| 状態 | コストの扱い |
|---|---|
| 全評価成功・final保存成功 | 観測済み最終値を画面と終端ログへ |
| 評価失敗・再試行上限 | 失敗attemptを含む観測済み値を残す |
| 取消・timeout | 中断までの値を残し、最終取得できなければ部分取得とする |
| checkpoint／Excel出力失敗 | コストを消さない。採点出力とログ出力の状態を分離 |
| boundary例外でsummaryなし | 実行画面とジョブログにはコストを保持。過去の結果画面に今回値を混ぜない |
| プロセス強制終了・電源断 | 書き込み済みログのみ残る。終端レコードなしは未完了。最後の未flush分や未報告の実消費は復元保証しない |
| override／別名Excel出力 | AI再実行ではないためコストを増やさない。元ジョブのスナップショットを参照 |

## 6. 画面設計

### 6.1 「3 実行」

- 見出し `今回のコスト（観測値）`。終了後は `前回のコスト（観測値）`。
- コンパクト要約に入力／出力トークン、AIクレジットの値または未取得理由、観測状態を表示する。
- 同じ内容領域で `進捗／コスト詳細／ジョブログ` を切り替えられるようにし、開始／取消ボタンの固定配置は維持する。
- 詳細は推論・キャッシュ・premium消費量・モデル別／operation種別内訳・観測数・最終時刻を表示する。モデル名は検証・長さ制限した文字列を通常テキストとして扱う。
- 終了／取消後も同じ値を保持し、次回draftのモデル変更や画面遷移で再計算しない。次ジョブ開始時だけ新しいスコープへ切り替える。
- 次ジョブ開始後の遅延通知はJobIdとrevisionで拒否する。dispose済みVMへの通知も拒否する。
- `ログを開く`／`保存先を開く` を明示操作として提供。存在しない／保存失敗時は理由を表示する。自身が生成した絶対パスのみを使い、shell文字列を組み立てない。

### 6.2 「4 結果」

- 採点結果に対応したJobIdの最終 `JobCostSnapshot` を表示する。`RunSummary`の保存行トークンから再構成しない。
- 最新の実行画面の値を直接bindしない。新規実行中でも前回結果のコストは元のままとする。
- 一覧・overrideの領域を確保するため、コスト詳細／ログは既存内容領域の切替で表示し、巨大なカードを縦に積まない。
- 新snapshotがない既存テストfixture／legacy contextは `この実行のコスト記録なし`。0としない。

### 6.3 ログ表示とアクセシビリティ

- ログ欄は日本語のイベント説明と数値を表示し、最新200件までのbounded collectionとする。全件はファイルで確認する。
- 自動追従のオン／オフを設け、閲覧中にスクロール位置を奪わない。UI制限による非表示とファイル記録欠落を区別する。
- 新しいAutomationIdは固定・一意、日本語Accessible Nameを付ける。キーボードで詳細切替・コピー・ログ導線に到達できること。
- 1024×720／1180×800、760×600の例外・scale 2、長い値／大きい桁数を検証する。本文14 DIP・操作44 DIP、通常サイズの外側非スクロール条件を維持する。[S10]

## 7. ジョブログ設計

### 7.1 保存先・形式

- 提案保存先: OSの `LocalApplicationData` 配下 `StudyReportEvaluator/jobs/<JobId>.jsonl`。入力ファイル名をログ名に含めず、入力／Excel出力先の状態から独立させる。
- 新規ジョブで新規作成し、既存ログを上書きしない。複数アプリprocessは別JobId・別writer。
- UTF-8 JSON Lines、schemaVersion 1、時刻はUTC ISO 8601。UIだけローカル時刻に変換する。
- テストでは一時directoryを注入し、実利用者のログ／設定へ触れない。ログ保存用secret・追加認証・`.env`は不要。

### 7.2 イベントと許可項目

| イベント案 | 内容 |
|---|---|
| `JobStarted` | JobId、開始時刻、app／SDK／CLI版、要求モデル、並列度、resume有無、集計scope、単位規則 |
| `AttemptStarted` | 生成AttemptId、operation種別、retry番号、送信状態 |
| `UsageUpdated` | attemptの最新revision、**累計スナップショット**、取得元・項目別状態 |
| `AttemptFinished` | 成否コード、観測範囲、最終attempt値 |
| `UsageUnavailable`／`UsageInvalid` | 閉じた理由コード。例外本文なし |
| `JobFinished` | 最終ジョブスナップショット、終了理由、記録欠落数、ログ終端状態 |

- 数値・生成ID・閉じたコード・検証済みのSDKモデルID／版のみを専用DTOからserializeする。
- **禁止:** Prompt、回答、reason、evidence、学生識別子、設問本文／名称、入力・出力の実パス、認証token／PAT、アカウント識別子、SDK応答全文、例外メッセージ／stack trace。
- トークン「消費数」と認証「token」をコード・文書・テストで区別する。元の `SafeLogger` のfree-text禁止・canaryを弱めず、別の `JobCostLogger` にも同等の構造的制約を設ける。[S07、S13]
- ログファイルはローカル平文の使用状況記録であり、自動公開／Git追跡／配布物同梱をしない。開いた後の外部共有は利用者の操作である旨を記載する。

### 7.3 非干渉・容量・故障

- 単一writerで並列通知を直列化する。UIスレッドに同期ディスクI/Oを置かず、無制限queueを使わない。
- 中間 `UsageUpdated` はattemptごとにcoalesce可能。破棄した詳細数は明示する。最終snapshotは元trackerから作り、落としたイベントの再加算に依存しない。
- 初期提案: 1ジョブ32 MiBを上限とし終端用領域を予約。上限時は中間詳細を抑止して警告し、終端要約を優先する。書込不能時の終端保存は保証しない。
- attempt終了／job終了でflushする。終了時のdrainも有限期限とし、採点取消・終了を無期限に待たせない。ログが永続化された時点のみ `保存済み` とする。
- permission拒否、disk full、writer例外、queue飽和をUIの独立警告にする。評価を再実行せず、取得済みin-memory値は残す。画面からの明示保存再試行を提供する場合もAIを再実行しない。
- 初期提案の保持期間は30日。ジョブ開始時に自身の命名規則に合う非稼働ログだけを整理し、ロック中のファイルを削除しない。保持仕様は利用者文書に明記する。自動削除を望まない場合は実装着手時にこの方針だけを変更する。
- 破損／途中行を含むログの閲覧では有効な直前レコードまでとし、未完了／記録欠落を表示する。ログの編集・削除を検出できない場合、監査証明や請求証拠とは称さない。

## 8. 変更対象と依存関係

以下の新規ファイル名は設計案。既存ファイルは§12の出典から参照できる。

| 領域 | 変更対象 | 内容 |
|---|---|---|
| 使用量ドメイン | 新規 `src/StudyReportEvaluator.App/Usage/` | metric／attempt／job snapshot、tracker、formatter、状態規則 |
| SDK adapter | `Copilot/EphemeralEvaluationRunner.cs` | 数値イベントの限定購読、metrics抽出、単位／欠落状態、購読解除 |
| 全評価の共通終端 | `Copilot/RetryAndCleanupCoordinator.cs`、`AuxiliaryEvaluationRunner.cs` | attempt生成・送信状態通知・破棄前のbounded最終観測、既存retryとcleanup維持 |
| runner／workflow | `Workflow/DurableEvaluationOperations.cs`、`EvaluationScheduler.cs`、`DurableEvaluationScheduler.cs`、`DurableQuantificationOrchestrator.cs` | operation種別の伝搬、必要最小限のobserver注入。保存行からコストを再集計しない |
| ジョブ境界 | `ViewModels/ExecutionViewModel.cs` 内 `QuantificationRunBoundary`／`IQuantificationRunBoundary`／`ExecutionRunContext` | ジョブ所有、途中コスト通知、finally通知、最終snapshot受渡し |
| ViewModel | `ExecutionViewModel.cs`、`ResultsOutputViewModel.cs`、必要なら共有 `JobCostViewModel` | immutable snapshot表示、JobId／revision guard、ログ導線、例外経路保持 |
| View | `Views/ExecutionView.axaml`、`ResultsOutputView.axaml` とcode-behind | コスト要約、詳細／ログ切替、動的高さ・keyboard対応 |
| ジョブログ | 新規 `Logging/JobCostLogger.cs`、`JobLogWriter.cs`、`JobLogEntry.cs` 等 | allowlist DTO、JSONL、bounded writer、障害状態、保存先境界 |
| 文書 | `docs/privacy-and-data-handling.md`、`features.md`、`getting-started.md`、`troubleshooting.md` | 平文数値ログ、保存先・保持期間、取得不能・単位・Excel集計との違い |
| 開発契約 | `dev/docs/ui-layout-contract.md`、`detailed-design.md`、`traceability.md`、必要な要求正本／ADR | コスト集計境界・観測品質・ログ方針の正式化 |
| リリース | `CHANGELOG.md`、通常の版管理手順に従う | 実装・検証後に更新。計画段階で版や公開実績は変更しない |

コスト途中通知は既存の `EvaluationProgress` に数値フィールドを大量追加せず、独立した型付きobserverを基本案とする。既存boundaryのテストfake／実装を一括追跡し、旧呼び出しは観測なしの互換経路を残す。採点進捗の変換でコストが欠落する設計を避ける。

## 9. 実装タスクと完了条件

| ID | 作業 | 依存 | 完了条件／成果物 | 参考工数 |
|---|---|---|---|---|
| T01 | 固定SDK／CLI使用量API・課金単位の調査 | なし | 型・nullable・欠落・イベントID・contextと消費の違い・クレジット換算・premium意味を出典付きで記録。単位未確認を明示 | 0.5〜1.5日 |
| T02 | 仕様・プライバシー境界・ログ契約確定 | T01 | §2〜7の決定をADR／要求へ反映。特にExcel維持・今回分scope・未取得表示・ログ保持を確定 | 0.5日 |
| T03 | usageモデル／tracker／formatter | T02 | 値と品質、並列置換、重複排除、丸め・overflowのunit test合格 | 1〜1.5日 |
| T04 | SDK取得と全runnerの観測接続 | T03 | 全4種、成功・retry・取消・cleanup、部分取得のfake test合格 | 1〜2日 |
| T05 | JSONL writer／ログ閲覧用モデル | T03 | 実一時ファイル、容量・flush・破損・IO障害・canary test合格 | 1〜1.5日 |
| T06 | boundary／VM最終化と例外経路 | T04、T05 | summaryなしでも値を保持。UI／ログの同revision一致、旧通知拒否、既存fake互換 | 1日 |
| T07 | 実行・結果画面の表示／導線 | T06 | 実Avalonia viewで全状態表示、レイアウト・keyboard・ログ操作のheadless試験合格 | 1〜1.5日 |
| T08 | 再開・失敗・出力回帰E2E | T07 | 今回分分離、途中行破棄、出力失敗、override、checkpoint v1互換を検証 | 1日 |
| T09 | 利用者文書・native／実SDK検証・最終回帰 | T08 | 検証条件・実行証跡・未確認範囲を記録。公開claimは証拠範囲内 | 1〜2日 |

合計は**8〜12.5人日程度の概算**。既存環境が利用できる前提で、課金仕様調査・外部認証・native環境待ち・SDK不具合修正は変動要因。T04とT05は別ファイル中心で並行可能だが、最終集計契約はT03で統一する。

### 9.1 必須ゲート

- **G1 APIゲート:** 固定版で型・シグネチャを検証。最新main資料だけを根拠にSDK／CLIを自動更新しない。
- **G2 課金ゲート:** クレジット換算の公式根拠・実返却値の確認なしで、数値にAIクレジット／通貨のラベルを付けない。未対応なら未取得表示とraw単位表示は実装できるが、クレジット数値表示はBLOCKEDとする。
- **G3 品質ゲート:** fakeの期待値とUI・ログ集計が一致し、途中・最終の二重加算がない。
- **G4 安全ゲート:** canary漏洩なし、ログ障害で採点／cleanupを壊さない。
- **G5 出荷ゲート:** 実装・自動テスト・native検証・実SDK取得を別々に記録する。クレジット数値が未確認なら機能全体を無条件に完了扱いしない。

## 10. テスト計画と受入条件

### 10.1 決定論的テスト

| ID | ケース | 期待結果 |
|---|---|---|
| C01 | token／nano-AI／premiumすべて取得 | 項目別値と単位・取得元がUI／ログで一致 |
| C02 | creditだけ欠落／tokenだけ欠落／全欠落 | 取得できた項目は表示、不明はnull。0で補わない |
| C03 | 明示0・未送信・送信不明 | 3状態を区別 |
| C04 | `LastCall*`しかない | 一部取得表示。セッション全量としない |
| C05 | 同一usageイベント重複・順序逆転・final複数通知 | 二重加算なし。古いrevision拒否 |
| C06 | 複数LLM呼出しを持つ1session＋最終metrics | イベント合計とsession累計を二重加算しない |
| C07 | retry 2〜3attempt、失敗後成功 | 各attemptを一度ずつ含め、実際に観測できなかった分は不明 |
| C08 | 並列3、全4operation種別 | 取りこぼし・混線なし、モデル別／種別別内訳が整合 |
| C09 | 学生行の途中で取消／row未保存 | その行で観測した消費も今回ジョブには残る |
| C10 | timeout・Abort失敗・metrics失敗・cleanup失敗 | cleanup保証を維持し、有限期限で終了。コストだけの理由で再送なし |
| C11 | summaryなしの境界例外・出力／checkpoint失敗 | UIとログへ観測値・終了理由を保持 |
| C12 | checkpoint v1から再開 | 今回分のみ。過去値の二重加算・schema変更なし |
| C13 | 次回draft編集・画面往復・override／別名出力 | 元ジョブのsnapshot不変、コスト増加なし |
| C14 | 次ジョブ開始後の古い通知・VM dispose | 新しいジョブや破棄済みUIへ反映されない |
| C15 | `auto`／報告モデルなし／複数モデル | 要求と報告を区別。モデル推測・不明分の配賦なし |
| C16 | 小数premium・微小credit・大整数・異常値 | 精度と単位を維持。非ゼロを0に見せず、overflow／負数を隠さない |
| C17 | SDK非nullable値にJSON項目欠落 | 型の既定0を報告済み0と誤認しない。能力未確認なら不明 |
| C18 | session／モデル別／イベント値の不一致 | sourceとcoverageを維持し、不一致を隠さない |
| L01 | 複数process／並列通知／終了flush | 一意ファイル、行破損なし、終端値一致 |
| L02 | permission拒否・disk full・queue飽和・容量上限 | 採点継続、UI警告、詳細欠落数。in-memory値保持 |
| L03 | Prompt／回答／path／credential canary・改行モデル名 | JSONL／UIログ／例外表示に禁止情報なし、ログ偽装なし |
| L04 | 強制終了想定の途中行・終端なし | ログを完了成功としない。有効な範囲だけを表示 |
| L05 | 30日保持／別process稼働／削除拒否 | 自身の古い非稼働ログ以外を削除しない |
| U01 | 全表示状態・大桁数・長いモデル名 | 通常2寸法で切れなし、既存主要操作が常に到達可能 |
| U02 | 760×600／scale 2／Tab／コピー／ログを開く | 到達性・一意AutomationId・安全なファイル導線 |

既存の拡張先: `EphemeralEvaluationRunnerTests`、`DurableEvaluationSchedulerTests`、`DurableQuantificationOrchestratorTests`、`SettingsWorkflowSystemTests`、`ExecutionViewTests`、`ExecutionSettingsTests`、`ResultsOutputViewTests`、`ResultsPresentationTests`、`ResponsiveLayoutTests`、`CompactWorkflowLayoutTests`。`SafeLoggerCanaryTests` は既存保証として維持し、新しい `JobCostLoggerCanaryTests` を追加する。[S13、S14]

新規専用test案: `JobUsageTrackerTests`、`CopilotUsageAdapterTests`、`JobLogWriterTests`、`JobCostPresentationTests`、`JobCostWorkflowTests`。テストデータはすべて合成値で、実利用量と記載しない。

### 10.2 実SDK／nativeの検証

- 実AI検証は認証・課金を伴うため、実施時に対象モデル・合成入力・少量の呼び出し範囲を提示して明示承認を得る。今回の計画作成では実施しない。
- 固定SDK／実manifestのCLIを使い、使用可能な明示モデルと`auto`を分けて検証する。利用不可のモデルへ勝手に切り替えない。
- 可能な範囲で正常、複数呼び出し、取消を観測し、SDKの数値をUI・JSONLと比較する。quota／請求画面との一致は別検証であり、これだけで保証しない。
- SDKが返さなかった項目はそのまま未取得を証跡化する。アカウントによるavailability差を製品不具合と混同しない。
- nativeは最小／初期寸法、実DPI、画面切替・取消・ログファイル操作を検証。headless成功をnative成功に転記しない。
- 証跡に記録するのは版・合成条件・許可数値・観測状態・テスト結果。生応答・認証情報を保存しない。

## 11. 完了の定義と最初の着手順

1. T01で固定SDK型と公式課金仕様を照合し、クレジット表示の可否を確定する。
2. T02で今回分scope、Excel互換、数値ログのプライバシー変更を契約化する。
3. trackerのテストを先に作り、実装はT03→T04／T05→T06→T07→T08→T09の順で進める。
4. 画面・ファイルの両方に同じ観測値を出せること、失敗／取消でも値を失わないこと、未取得を偽らないことを必須にする。
5. 文書・実装・テストの整合と既存採点／Excel／checkpoint回帰を確認して完了とする。外部検証が未実施なら `NOT_RUN`／`BLOCKED` を残す。

**実装後の検証:** 最新の限定実行は12ケース中11成功・1スキップ・失敗0。対象は集計・ログ・安全なジョブヘッダー・観測値訂正の変更箇所。`TestResults/job-cost-audit-20260917/job-cost-audit-final.trx`に記録。以前の33成功の報告には、シンボリックリンク権限不足でreturnした未検証ケースが含まれていたため、その保護機能を実証したことにはしない。今回実AI・課金照合・native UI・全体テスト・公開操作は実施していない。

## 12. 出典一覧

現行コードは調査基準commitで確認した。行番号はその時点の位置で、以後の実装により変わり得る。

- **S01** [Directory.Packages.props](../Directory.Packages.props#L3): SDK固定版。
- **S02** [EphemeralEvaluationRunner.cs](../src/StudyReportEvaluator.App/Copilot/EphemeralEvaluationRunner.cs#L196-L242): 通常評価取得タイミング。[同531〜572行](../src/StudyReportEvaluator.App/Copilot/EphemeralEvaluationRunner.cs#L531-L572): metrics集計・fallback。
- **S03** [AuxiliaryEvaluationRunner.cs](../src/StudyReportEvaluator.App/Copilot/AuxiliaryEvaluationRunner.cs#L207-L241): 補助評価の取得タイミング。
- **S04** [RetryAndCleanupCoordinator.cs](../src/StudyReportEvaluator.App/Copilot/RetryAndCleanupCoordinator.cs#L81-L149): 既存usage型。[ExecuteCoreAsync](../src/StudyReportEvaluator.App/Copilot/RetryAndCleanupCoordinator.cs#L306): retry／cleanup／attempt.TokenUsage加算。
- **S05** [RunSummary.cs](../src/StudyReportEvaluator.App/Workflow/RunSummary.cs#L254-L262): operation集計。[EnumerateDurableUsage](../src/StudyReportEvaluator.App/Workflow/RunSummary.cs#L598): 参照・完成行由来の集計。
- **S06** [ExecutionViewModel.cs](../src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs#L133-L220): 実行境界／runner生成。[ExecutionRunContext](../src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs#L277)、[StartAsync](../src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs#L1263)、[進捗通知](../src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs#L1671)。[ResultsOutputViewModel.cs](../src/StudyReportEvaluator.App/ViewModels/ResultsOutputViewModel.cs): 結果ロード／Excel出力。
- **S07** [SafeLogger.cs](../src/StudyReportEvaluator.App/Logging/SafeLogger.cs): 閉じたログ項目、Null sink、非干渉。
- **S08** [privacy-and-data-handling.md](../docs/privacy-and-data-handling.md#L59): 現行の数値ログ非出力方針。
- **S09** [CheckpointEnvelope.cs](../src/StudyReportEvaluator.App/Workbooks/Checkpoint/CheckpointEnvelope.cs#L130)、[CheckpointPayloadCodec.cs](../src/StudyReportEvaluator.App/Workbooks/Checkpoint/CheckpointPayloadCodec.cs#L510): schema 1・未知メンバー拒否。
- **S10** [ui-layout-contract.md §2〜4](../dev/docs/ui-layout-contract.md): UI寸法・到達性・状態所有の契約。[ExecutionView.axaml](../src/StudyReportEvaluator.App/Views/ExecutionView.axaml)、[ResultsOutputView.axaml](../src/StudyReportEvaluator.App/Views/ResultsOutputView.axaml): 現行画面。
- **S11** 導入済みNuGet `GitHub.Copilot.SDK` 1.0.11 の `lib/net8.0/GitHub.Copilot.SDK.xml`。ローカル確認先: `C:/Users/dahatake/.nuget/packages/github.copilot.sdk/1.0.11/`。11526行: model TotalNanoAiu、11565行: session TotalNanoAiu、11568行: TotalPremiumRequestCost、31476行: AssistantUsageData.Cost、33348行: AssistantUsageCopilotUsage.TotalNanoAiu。`build/GitHub.Copilot.SDK.props` のCLI既定値は1.0.79。これは開発環境の一次資料で、リポジトリへの同梱を意味しない。
- **S12** [GitHub Copilot SDK公式 Usage and Billing](https://github.com/github/copilot-sdk/blob/main/docs/features/usage-and-billing.md): 2026-09-17、Context7経由で本文該当箇所を取得。使用量イベント、metrics、nano-AI units換算の注意、quotaと使用量の用途の違い。mainは可変資料であり固定SDKの互換保証ではない。同資料が指定する[GitHub課金資料](https://docs.github.com/en/copilot/managing-copilot/understanding-and-managing-copilot-usage)は**T01で確認する参照先**であり、本計画作成時に本文・換算規則を確認済みとはしていない。
- **S13** [SafeLoggerCanaryTests.cs](../tests/StudyReportEvaluator.App.Tests/Logging/SafeLoggerCanaryTests.cs): free-text／Exception禁止、credential・path等canary、sink故障の非干渉。
- **S14** [UIテスト](../tests/StudyReportEvaluator.App.Tests/UI/)、[Workflowテスト](../tests/StudyReportEvaluator.App.Tests/Workflow/)、[SettingsWorkflowSystemTests.cs](../tests/StudyReportEvaluator.App.Tests/E2E/SettingsWorkflowSystemTests.cs): 既存回帰テストの拡張先。今回実行済みという意味ではない。
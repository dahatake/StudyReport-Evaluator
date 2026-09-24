# attempt timeout 60秒・reasoning effort medium（2026-09-24 21:40）

## 0. 指示（要求所有者、2026-09-24 21:38）

- 要求定義書7.6節のattempt timeoutは、SDKの60秒に従う。
- このアプリが使うmodelのThinking Effort（SDK `SessionConfig.ReasoningEffort`）はmedium。答案の定量化なのでhighは不要。
- 要求定義書・設計・実装を変更する。

## 1. 調査（出典）

| # | 事実 | 出典 |
|---|---|---|
| F1 | SDKの`SendAndWaitAsync`は`timeout`がnullなら60秒待機し、満了で`TimeoutException` | SDK 1.0.11 `Session.cs` 330-412（`timeout ?? 60s`）、RR-03実測で送信開始から約60.3秒でTimedOut（`work/20260924-2045-RR03RetryRootCause.md` §4） |
| F2 | アプリは`timeout: null`で呼び、attempt全体（起動・認証確認・session作成・送信）を既定120秒のlinked CTSで囲む | `EphemeralEvaluationRunner.cs` 15行・送信呼出し、`RetryAndCleanupCoordinator` |
| F3 | `SessionConfig.ReasoningEffort`はstring。有効値は"low","medium","high","xhigh","max"。`capabilities.supports.reasoningEffort`がtrueのmodelにだけ適用 | SDK 1.0.11 `Types.cs`（`SessionConfig.ReasoningEffort`のXML doc）、`ModelSupports.ReasoningEffort`（4143行）、`ModelInfo.SupportedReasoningEfforts`（4227行） |
| F4 | 同梱CLIのmodels.listでは、`auto`、claude-haiku-4.5、gemini-3.7-flash、gemini-3.8-flashが`supports.reasoningEffort=false`。他のmodelはtrueで、全て"medium"を列挙。既定effortは全てnull | 使い捨て診断console（SDK 1.0.11、同梱CLI）の実測 |
| F5 | 非対応modelへ"medium"を渡すと**session作成が失敗**する。`auto`は「Reasoning effort is not supported when using the "auto" model.」、claude-haiku-4.5は「Model 'claude-haiku-4.5' does not support reasoning effort configuration. Use models.list to check which models support reasoning effort.」。対応model（claude-sonnet-5）は"medium"で成功 | 同じ診断consoleでの実測（3 request、session削除済み） |
| F6 | `CopilotClient.ListModelsAsync`は、client単位で初回成功時にcacheし、切断でcacheを消す | SDK 1.0.11 `GitHub.Copilot.SDK.xml`（`ListModelsAsync`のremarks） |
| F7 | 4 operationのsession作成は全て`SdkEphemeralCopilotTransport.CreateSessionAsync`を通る。attemptごとに新しいclientを作る | `EphemeralEvaluationRunner.cs`（Normal）、`AuxiliaryEvaluationRunner.cs`（Reference／Special／Similarity） |
| F8 | この端末では、attemptの送信前の準備に約20〜26秒かかる。内訳はclient作成（同梱CLI 159,403,296 bytesのSHA-256照合2回を含む）約15〜20秒、起動約3秒、認証確認約1秒、models.list約1.4秒 | 使い捨てprobe（`StudyReportEvaluator.App.dll`を参照し、`BundledCopilotCliPathResolver`／`CopilotClientFactory`／`CopilotClient`を計測、3回） |
| F9 | 同じ理由で、アプリの「Copilot 状態を確認」（`CopilotAuthenticationService.DefaultCheckTimeout`＝15秒）がこの端末では期限切れとなり「Copilot runtime の確認に失敗しました」になる。同じ処理はtimeout 60秒なら約18.5秒で`Available`になった | 同probe：timeout 15秒→`RuntimeFailed`（15,030ms）、60秒→`Available`（18,498ms）、15秒→`RuntimeFailed`（15,008ms）。アプリの自動操作でも4回連続で同じ表示 |

## 2. 設計（最小）

- D1 timeout（最終）: `DefaultAttemptTimeout`を120秒から60秒へ変更する。60秒は起動・認証確認・session作成・応答待ちを含むattempt全体に掛かる。SDKの呼び出しは`timeout: null`のまま（SDK既定60秒）とし、先に満了した方で`TimedOut`とする。retry規則は変更しない。
  - 経緯: 一度は「応答待ち＝SDKの60秒、attempt全体＝外側上限120秒」と読み替える案を採った。しかし第2回レビューで、所有者の指示は§7.6の**attempt timeout**そのものを60秒にすることであり、この読み替えは指示と矛盾する（Blocking）と指摘された。さらに、外側上限のtokenは送信にも渡るため、準備が長引けば応答待ちの60秒も保証できない。そのため指示どおり、当初案（attempt全体60秒）へ戻した。代償として、F8のとおりこの端末では準備に約20〜26秒かかり、応答待ちに使えるのは実質約34〜40秒になる。応答待ちに60秒を丸ごと残すには段階別のtimeoutが必要になるが、これは要求にない追加であり、所有者の判断を待つ。
- D2 reasoning effort: F5のとおり、一律に指定すると`auto`と非対応modelのsession作成が失敗する。そのため`SdkEphemeralCopilotTransport.CreateSessionAsync`で次のように決める。
  - 要求modelが`auto`なら、models.listを呼ばずに未設定とする。
  - それ以外は、同じclientの`ListModelsAsync`から要求modelを探す。`supports.reasoningEffort=true`で、かつ`SupportedReasoningEfforts`に"medium"がある場合だけ`ReasoningEffort="medium"`とする。
  - 非対応model、effort未列挙のmodel、一覧にないmodelは未設定とし、runtimeの既定に任せる。
  - 採らない案: modelの対応可否を`CopilotModelAvailability`→run request→durable workflow→resume→設定cacheへ運ぶ案。変更箇所が多く、実行時点のcapabilityとずれ得るため。
  - 採らない案: session作成errorの文言を見てfallbackする案。文言に依存し壊れやすいため。
  - 代償: `auto`以外のattemptではmodels.list RPCが1回増える（F8で約1.4秒、premium requestではない）。失敗は既存の`TranslateAsync`で分類する（network→再試行、timeout→再試行、その他→Fatal）。この約1.4秒もattemptの60秒の中で消費する。

## 3. 実行記録

| 手順 | 結果 | 証跡 |
|---|---|---|
| 実装 | `EphemeralEvaluationRunner.cs`：`DefaultAttemptTimeout`を60秒へ変更（コメント付き）、`SdkEphemeralCopilotTransport.MediumReasoningEffort`と`ResolveReasoningEffort`を追加、`CreateSessionAsync`でeffortを設定 | git diff |
| 単体試験 | `Reasoning_effort_is_medium_only_for_models_that_support_it`（7ケース：対応2、`auto`、非対応、effort未列挙、high限定、一覧外）を追加。Copilot試験とDocumentationContractTestsで213/213成功 | `dotnet test ... --filter Copilot|DocumentationContractTests` |
| 全体回帰 | App.Tests全体：1877成功・2失敗・9スキップ（48分40秒）。失敗1は`SampleWorkbookStructuralTests`で、`sample/`はgit管理外（ignored）。手元のsampleの寸法が`A1:L531`で、期待値`A1:J531`と一致しない。CIはこのclassを除外している（`.github/workflows/ci.yml` 84行）。失敗2は`ReleaseMatrixBuilderTests.Concurrent_candidate_builders...`で、`C03 process test timed out`。単独で再実行すると、変更なし（stash）で成功（1分3秒）、変更ありでも成功（44秒）。全体実行中の負荷による時間切れで、今回の変更とは無関係 | `dotnet test`出力 |
| 実環境probe | 変更後の`StudyReportEvaluator.App.dll`（win-x64 build）の`SdkEphemeralCopilotTransport`をreflectionで生成し、実際のCLIでsessionを作成・送信した。結果はclaude-sonnet-5が`effort=medium`で送信完了、`auto`と claude-haiku-4.5は`effort=(unset)`で送信完了。3 request、session削除済み | probe出力 |
| アプリ画面での実行 | 実施できず。F9のとおり、認証確認がこの端末で15秒を超えて失敗するため。これは今回の変更前からある事象（RR-03でも2回発生）で、今回の範囲外 | `files\effort-run.log` |

## 4. 敵対的レビュー（rubber-duck）

| # | 指摘 | 対応 |
|---|---|---|
| 1 | Blocking：effort未列挙を"medium"扱いしており、要求の「mediumがある場合だけ」と矛盾する | 反映。列挙に"medium"がある場合だけとし、試験の期待値を`null`へ変更 |
| 2 | Non-blocking：`auto`でも毎attempt不要なmodels.listを呼ぶ | 反映。`auto`はmodels.listを呼ばずに未設定とする |
| 3 | Non-blocking：試験が純粋関数だけで、統合順序と失敗分類を検証していない。client facadeを注入可能にすべき | 見送り。facade追加は今回の要求にない抽象化（YAGNI）。統合経路は実環境probeで確認し、失敗分類は既存の`TranslateAsync`をそのまま使う |
| 4 | Non-blocking：`implementation-status.md`が120秒・所有者判断待ちのまま。作業記録が未完 | 反映。新しい状況欄と本記録§3を追記（過去欄は履歴として残す） |
| 5 | （第2回）Blocking：「応答待ち60秒＋外側120秒」は§7.6のattempt timeoutの読み替えで、所有者の指示と矛盾する。また外側のtokenが送信にも渡るので、応答待ちの60秒も保証しない | 反映。attempt全体を60秒へ戻し、要求・設計・試験・文書を合わせた（D1の経緯） |
| 6 | （第2回）Non-blocking：win-x64 buildで正規の`packages.lock.json`（App・Core）にRID節が追加された | 反映。両fileを`git checkout`で戻した |

## 5. 敵対的レビュー後の訂正（2026-09-25）

- D1（attempt全体60秒）は撤回した。実機の通し実行で、attempt側がSDKの60秒より先に満了し参照回答が`AI_TIMEOUT`になったため、`DefaultAttemptTimeout`は外側上限120秒に戻し、応答待ちはSDK既定60秒とした。詳細は`work/20260925-0005-AdversarialReviewTimeoutEffort.md`。
- 「準備に約20〜26秒」「client作成が遅い原因はSHA-256照合」は、測定時点の値と推測を事実のように書いていた。実測では非同期4 KiB読込のSHA-256が1回約5.7〜6.5秒（同期約0.5秒）で、client作成に2回含まれる。bufferを1 MiBにしてclient作成は約0.4秒になった。
- F9（RR-03の認証失敗は同じ原因）は推測だった。上記の測定で主因とみられるが、個々の失敗の原因は確認していない。
- F1/F3の出典は削除済みの一時コピーだった。上流の`github/copilot-sdk` `dotnet/src/Session.cs`（`SendAndWaitAsync`の`effectiveTimeout = timeout ?? TimeSpan.FromSeconds(60)`、`SessionErrorEvent`で`InvalidOperationException`）を出典とする。

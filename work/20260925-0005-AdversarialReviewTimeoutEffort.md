# 敵対的レビュー: attempt timeout 60秒・reasoning effort medium（commit `841cac0`）

対象: 要求所有者の指示「§7.6のattempt timeoutはSDKの60秒に従う」「Thinking Effortはmedium（Highは不要）」に対する要求定義・設計・実装・記録。

## 1. 実施した検証（証跡）

証跡はsession folder（`files\`）にある。repositoryにはcommitしない。

| ID | 内容 | 結果 |
|---|---|---|
| E1 | アプリ画面の通し実行（1行、全operation、proxy、既存tunnelだけ75秒停止） | 15/15成功。停止は処理中のtunnelに当たらず、timeout経路は通らなかった |
| E2 | E1と同じだが、停止中に新しく開いたtunnelも停止（65秒） | 参照1が`AI_RUNTIME_FAILED`、再試行なし。job logのattempt outcomeは`Fatal` |
| E3 | SDKを直接使うprobe（`auto`、送信3秒後に全通信を停止）×2 | 約34秒と33秒後に`SessionErrorEvent`（`errorType=query`、message `error sending request for url (...): client error (Connect): operation timed out [ETIMEDOUT]`）。SDKは`InvalidOperationException("Session error: ...")`を送出 |
| E4 | 同梱CLI（約159 MB）のSHA-256計測 | PowerShell同期約0.44〜0.54秒。アプリと同じ`FileOptions.Asynchronous`・既定buffer約5.7〜6.5秒。buffer 1 MiBで約0.31〜0.41秒 |
| E5 | `CopilotAuthenticationService.CheckAsync`（15秒）×3、修正前 | RuntimeFailed 16.2秒／Available 16.0秒（60秒枠）／Available 12.0秒。境界上で不安定 |
| E6 | 通信失敗の分類を修正後、E2と同じ条件（通常評価claude-sonnet-5） | 参照1: 1回目`NetworkFailed`→2回目成功。参照5: 1回目`TimedOut`、2・3回目は送信前に60秒を使い切った（通信なし）→`AI_TIMEOUT` |
| E7 | SHA-256修正後、E6と同じ条件 | client作成0.43秒、状態確認約6.8秒。参照1は`NetworkFailed`→`TimedOut`→`TimedOut`、参照2は`TimedOut`→成功。`TimedOut`の打切りは、応答の受信完了とほぼ同時のものを含む（proxy log 00:48:52受信完了、00:48:53打切り） |
| E8 | attempt全体の上限を120秒に戻し、proxyなしで通し実行（通常評価claude-sonnet-5・medium） | 15/15成功（error 0）。送信〜完了は通常25.5〜36.4秒、参照21.6〜29.4秒、類似度26.3〜35.8秒 |

## 2. 指摘一覧

| No. | 軸 | 重大度 | 指摘箇所 | 問題の説明 | 修正案 |
|---|---|---|---|---|---|
| 1 | 目的適合性 | Critical | `SdkEphemeralCopilotSession.SendAndWaitAsync`（catch-all） | 送信中の通信断をCLIがsession errorとして返すと、SDKの`InvalidOperationException`がcatch-allで`Fatal`になり、再試行されない（E2・E3）。§7.6「transient network errorは新sessionで最大2回再試行」に違反。RR-03のPASSは、この経路を通らない1つの停止パターンだけの証跡だった | messageに`error sending request for url`（reqwestの送信エラー表記）を含む場合は`EvaluationNetworkException`へ変換する |
| 2 | 目的適合性 | Critical | `DefaultAttemptTimeout`＝attempt全体60秒 | attempt側が起動・session作成（約10〜25秒）も数えるため、SDKの60秒の応答待ちより必ず先に満了する。実機で`AI_TIMEOUT`が発生し、応答完了とほぼ同時の打切りもあった（E6・E7）。「SDKの60秒に従う」はSDKの60秒が実際の応答待ちにならないため満たせず、評価も失敗する | 応答待ちはSDK既定60秒（`timeout: null`のまま）とし、attempt全体の外側上限は変更前の120秒に戻す。§7.6・設計・承認記録を更新 |
| 3 | 内容の妥当性 | Major | `CopilotClientFactory`（SHA-256 2か所） | 非同期FileStreamの既定4 KiB bufferで約159 MBを2回hashし、client作成に約12秒かかる（E4・E5）。attempt・状態確認の両方で時間を浪費し、No.2と「状態確認15秒の失敗」の主因とみられる | `BufferSize = 1 MiB`を指定（E7でclient作成0.43秒） |
| 4 | 根拠性 | Major | implementation-status「原因は、同梱CLIのSHA-256照合を含むclient作成に時間がかかること」 | 当時はhashの所要時間を測っておらず、原因は推測だった（直後のPowerShell同期計測は0.5秒で、そのままでは矛盾する） | E4の計測値で書き直し、「主因とみられる」と留保する |
| 5 | 根拠性 | Major | implementation-status「この端末では送信前の準備に約20〜26秒」「実質約34〜40秒」 | 1回のprobeの値を端末の定常値として書いていた。過去のjob logではjob開始から最初の送信まで約7〜10秒（rr02・rr03m3）で、値は大きく変動する | 断定を削除し、実測範囲と条件で書く |
| 6 | 根拠性 | Major | implementation-status「未解決（範囲外）…そのためアプリ画面での通し実行はできていない」 | 状態確認は再試行すれば成功することがあり（E5・E6）、通し実行はできた。調べずに不可能と扱っていた | 通し実行を実施（E1〜E8）し、記述を実測結果へ置換 |
| 7 | 整合性 | Major | 要求§7.1「全operationのreasoning effortは`medium`とする」、implementation-status・CHANGELOG「全operationで指定」 | 参照回答生成・類似度評価は`auto`固定なので、effortは一度も指定されない。E1〜E5の既定設定（`auto`）では通常評価にも指定されない。「全operation」は誤り | 「指定する場合は`medium`」とし、`auto`固定operationと非対応modelでは指定されず、実効effortは観測できないことを明記 |
| 8 | 根拠性 | Major | 作業記録§3・implementation-status「確認」 | probeで示したのは「session作成と送信が受理された」ことだけで、runtimeが実際に`medium`で推論したことは観測していない | 「受理を確認」までに留め、実効effortは観測不能と明記（No.7と同じ文） |
| 9 | 品質・運用性 | Major | 検証全般 | 変更後のアプリ画面でのmedium指定経路（ListModels→effort付きsession作成）は未検証だった | E8で通常評価claude-sonnet-5の通し実行が成功（15/15） |
| 10 | 整合性 | Major | 要求§7.6・設計§5.5「SDK側とattempt側のどちらが先に満了しても」 | 60秒の同値でattempt側が先に始まるため、SDK側が先に満了することは実際上ない。記述が誤解を招く | No.2の修正で、SDK 60秒が応答待ち、120秒が外側上限という関係に書き直す |
| 11 | 品質・運用性 | Major | 要求の試験一覧15 | reasoning effortの指定条件、CLI session errorの通信失敗分類の試験項目がない | 試験一覧15へ追記し、単体test（effort 7件、分類3件）を対応させる |
| 12 | 品質・運用性 | Major | `ListModelsAsync`をattemptごとに呼ぶ | SDKはclient単位でcacheするが、attemptごとにclientを作るので大規模jobでは`models.list`が数百回になる。SDKのdocにrate limit回避のためのcacheとある | **修正しない**。rate limitは未観測で、1回約1.4秒は120秒の上限内に収まる。factory単位のcacheは状態の追加になるため、必要が確認されるまで入れない（YAGNI） |
| 13 | 品質・運用性 | Major | effortをworkbook・checkpoint・job logに記録しない | 再現性の確認や、異なる版をまたいだ再開でeffortが混ざることを追跡できない | **修正しない**。要求にない記録項目で、checkpoint互換へ影響する。所有者判断事項として報告 |
| 14 | 内容の妥当性 | Major | proxy経由のE6・E7の通常評価（claude-sonnet-5・medium） | schema不正が15 attempt中7件、各runで1設問が`AI_OUTPUT_INVALID`。effort未指定の同modelのrr02は0/5 | **原因は断定しない**。proxyなしのE8では0/5で、medium起因の証拠はない。観測事実として記録するのに留める |
| 15 | 整合性 | Minor | CHANGELOG概要文 | 新しい文が「（製品コードの変更なし）」の直後に続き、製品コードを変えていないように読める | 括弧書きの対象を限定し、文を分けて修正内容を列挙 |
| 16 | 品質・運用性 | Minor | コードコメント「follows the SDK SendAndWaitAsync default」 | 実際はattempt側がSDKの値を先取りしており、コメントが実態と違った | 外側上限であり、応答待ちを先取りしてはならない旨とSDK更新時の再確認へ修正 |
| 17 | 根拠性 | Minor | 値60秒の由来 | 「SDKに従う」がSDKから取得した値ではなく定数であることと、SDK更新時に再確認が必要なことが書かれていない | 要求§7.6とコードコメントに「SDK版の更新時に再確認」を追記 |
| 18 | 品質・運用性 | Minor | troubleshooting `AI_TIMEOUT`・`NETWORK_FAILED` | timeoutの値と、CLIのsession errorも通信失敗に含むことが利用者に示されていない | 表へ追記 |
| 19 | 根拠性 | Minor | 作業記録F1/F3 | 出典が削除済みの一時コピー（`%TEMP%\sdk-*.cs`）で、追跡できない | 上流`github/copilot-sdk`の`dotnet/src/Session.cs`を出典として追記 |
| 20 | 根拠性 | Minor | 作業記録F9「RR-03の認証失敗は同じ原因」 | 推測を事実として書いていた | 「主因とみられる、個々の失敗は未確認」へ訂正 |
| 21 | 品質・運用性 | Minor | `CreateSessionAsync`の`"auto"`文字列 | 同じ文字列が同じファイル内（`SdkEphemeralCopilotSession`）とRunnerの`ModelId`定数に重複 | **修正しない**。既存コードも同じ文字列を直接使っており、この箇所だけ変えると不揃いになる |
| 22 | 品質・運用性 | Minor | `auto`の早期return、`CreateSessionAsync`の統合 | 単体testがない（resolverの`auto`ケースは本番の早期return経路を通らない） | **修正しない**。`CopilotClient`はsealedで差し替えにはfacadeが必要（YAGNI）。E8の実機通し実行で代替 |
| 23 | 内容の妥当性 | Minor | `ResolveReasoningEffort` | nullを許さないmemberにも`?.`を付けており、防御が過剰 | **修正しない**。SDKのJSON逆シリアル化でnullになり得るため、無害な防御として残す |
| 24 | 根拠性 | Minor | 要求表の「bundled CLI `1.0.79`」 | 同梱CLIの`--version`は`1.0.88`と表示する（file versionとmanifestは1.0.79） | **修正しない**。今回の変更の範囲外。事実として報告のみ |
| 25 | 品質・運用性 | Minor | 「Copilot 状態を確認」15秒 | 修正後も初回は約15.7秒かかることがある（E7前のprobeの1回目） | **修正しない**。15秒は既存の仕様値で今回の範囲外。No.3の修正で通常は約7秒 |

## 3. サマリー

- Critical: 2件（No.1、No.2）→ 修正済み
- Major: 12件（修正9件、修正しない3件：No.12・13・14）
- Minor: 11件（修正6件、修正しない5件）
- 合格判定: 修正前 **FAIL**（Critical 2件）→ 修正後の再検証（E8、単体test）で **PASS**（Critical 0）

## 4. 修正内容

- `EphemeralEvaluationRunner.cs`: CLIの通信失敗session errorを`EvaluationNetworkException`へ変換（`IsTransportSessionError`）。`DefaultAttemptTimeout`を120秒（外側上限）へ戻し、コメントを実態に合わせた。
- `CopilotClientFactory.cs`: CLIのSHA-256計算の読込bufferを1 MiBにした（2か所）。
- test: 通信失敗の分類3件を追加し、既定値testを120秒へ戻した。
- 要求定義書§7.1・§7.6・試験一覧15・承認記録、設計§5.5、SystemTest-prompt、troubleshooting、implementation-status、CHANGELOG、作業記録を更新した。
- 出典: SDK `github/copilot-sdk` `dotnet/src/Session.cs`（`SendAndWaitAsync`、`SessionErrorEvent`→`InvalidOperationException`）。reqwest `src/error.rs`（`Kind::Request => "error sending request"`、` for url (...)`）。

# RR-03 実際の SDK 例外による retry の観測: 根本原因と解決策（2026-09-24 20:45 JST）

- 契機: 所有者の指示（2026-09-24 20:40）「RR-03: 根本原因を調査して、解決策を立案して、PASS するまで実行」。同じ指示で RR-01・RR-04・RR-11 はタスクから削除された（[計画](202609240830-RemainTaskExecutionPlan.md) の各節に注記）
- RR-03 の DoD（計画の RR-03 節）: 「実際の例外の後に retry が行われたことをジョブログで観測する」
- 基準: `main` の HEAD `86b905c`。製品コードは第3ラウンドから変わっていない

## 1. 根本原因（なぜ第3ラウンドで PASS しなかったか）

### 1.1 方法 1（CLI の process を止める）は、仕様により retry が起きない

- 要求定義書 [requirements-definition.md](../docs/requirements-definition.md) の 7.6 節（350 行目）: 「cleanup失敗後は追加retryを行わない」
- 各 attempt は専用の transport（CLI の子 process）を持ち、後始末で session の abort・dispose・delete と、client の stop・dispose を行う（[EphemeralEvaluationRunner.cs](../src/StudyReportEvaluator.App/Copilot/EphemeralEvaluationRunner.cs) の 210〜255 行目と 257〜322 行目）。CLI の process が無くなった後は、この後始末の RPC の相手がいない。第3ラウンドの方法 1 の実行では、実際に後始末が失敗した（`AttemptOutcome=7`）
- 後始末が失敗すると `CleanupFailed` で即座に終わり、`ShouldRetry` に進まない（[RetryAndCleanupCoordinator.cs](../src/StudyReportEvaluator.App/Copilot/RetryAndCleanupCoordinator.cs) の 466〜481 行目）
- したがって、CLI の process を止めて後始末が失敗した場合、要求定義書どおりの製品は **retry しない**。第3ラウンドの観測（`AttemptOutcome=7`、全 attempt が `AttemptNumber=1`）はこの設計と一致する。方法 1 は、後始末が失敗する限り DoD の「retry の観測」には届かない

### 1.2 方法 2（Sandbox でネットワークを切る）は、この環境では実施できない

- Sandbox の中での本人の login が必要（計画の RR-03 方法 2）。ホストでネットワーク adapter を切ることは、非管理者であり、共有のホストの通信も止めるので行わない

### 1.3 retry の対象になる「実際の SDK 例外」の条件（コードと SDK の出典）

- retry されるのは、失敗の種類が `Network` か `Timeout` で、後始末が成功した場合だけ（[RetryAndCleanupCoordinator.cs](../src/StudyReportEvaluator.App/Copilot/RetryAndCleanupCoordinator.cs) の `ShouldRetry`。`SchemaInvalid` は 1 回）
- `Network` になるのは `IOException`・`HttpRequestException`・`SocketException`・`WebSocketException`、`Timeout` になるのは `TimeoutException` とアプリの attempt timeout（[EphemeralEvaluationRunner.cs](../src/StudyReportEvaluator.App/Copilot/EphemeralEvaluationRunner.cs) の 617〜640 行目）
- GitHub Copilot SDK 1.0.11 の `CopilotSession.SendAndWaitAsync`（[github/copilot-sdk の dotnet/src/Session.cs、tag v1.0.11](https://github.com/github/copilot-sdk/blob/v1.0.11/dotnet/src/Session.cs) の 330〜412 行目）:
  - `timeout` が null のときの待ち時間は 60 秒（`var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(60);`）。超えると `TimeoutException` を投げる
  - CLI から `session.error` event が来ると `InvalidOperationException("Session error: …")` を投げる。アプリはこれを `Fatal` に分類するので retry しない
- アプリは `timeout: null` で呼んでいる（[EphemeralEvaluationRunner.cs](../src/StudyReportEvaluator.App/Copilot/EphemeralEvaluationRunner.cs) の 610〜615 行目）
- つまり、**CLI の process が生きたまま、応答が 60 秒以上返らない**状況を作れば、SDK が実際に `TimeoutException` を投げ、後始末は成功し、retry が起きるはずである（3 章の実行で確かめた）

## 2. 解決策（方法 3: ローカルの proxy で通信を止める）

- Copilot CLI は `HTTPS_PROXY` 環境変数の proxy を使う（同梱 CLI の `copilot help environment` の出力: "`HTTP_PROXY`, `HTTPS_PROXY` (case-insensitive): proxy server URL for outgoing HTTP and HTTPS connections"）。アプリは CLI に環境変数を明示的に渡しておらず（[CopilotClientFactory.cs](../src/StudyReportEvaluator.App/Copilot/CopilotClientFactory.cs) の 413〜424 行目の `BuildOptions`）、CLI はアプリの環境変数を引き継ぐ（3 章の proxy の接続ログで確かめた）
- 手順:
  1. 127.0.0.1 だけで待ち受ける CONNECT proxy（Python、セッション領域の `rr03-proxy.py`。通信の中身は復号も記録もしない。記録するのは時刻・接続先の host と port・転送 byte 数だけ）を起動する
  2. `HTTPS_PROXY=http://127.0.0.1:<port>` を設定したシェルからアプリを起動する（ほかの手順は第3ラウンドの RR-03 と同じ。`COPILOT_*` を消し、最終データ行 2 の 1 行だけ）
  3. 実行が始まって CLI が通信している間に、proxy に「その時点で開いているトンネルを 75 秒間止める（転送しない）」指示を出す。止めている間に新しく開かれたトンネルは通常どおり転送する。75 秒後、止めたトンネルは閉じる
  4. 止めたトンネルで応答を待っていた attempt は、60 秒後に SDK の `TimeoutException` で終わり、後始末（ローカルの stdio だけ）が成功して、新しい session で retry されるはず
- DoD の判定: ジョブログで、同じ operation ID に `AttemptNumber=2` 以上の attempt があり、その前の attempt の終端結果が `TimedOut`（または `NetworkFailed`）であること
- 変えないもの: 製品のコード、retry の規則、テスト。proxy はリポジトリに入れない（計画 5 章の「retry を起こすための製品のフックを加えない」）。ホストのネットワーク設定は変えない。止めるのは自分が起動したアプリの CLI の通信だけ
- 時間の上限: 1 回の実行は 15 分。うまくいかない場合は、原因を 1 つずつ確かめて止め方（止める時点・時間）を変え、最大 3 回まで行う

## 3. 実行の記録

### 3.1 準備と、途中で起きたこと

- proxy の動作確認: `curl.exe -x http://127.0.0.1:18931 https://api.github.com/zen` が 200 を返し、proxy のログに `connect id=1 api.github.com:443` が出た
- 1 回目の起動（20:51）: アプリの「Copilot の確認」が「Copilot runtime の確認に失敗しました。」になり、「定量化を開始」が無効のまま script が止まった。自分が起動したアプリ（PID 53904）を止めた
- 切り分け:
  - 同梱 CLI を `HTTPS_PROXY` 付きで直接 `copilot -p "Reply with the single word OK."` として実行すると `OK` が返り、proxy のログに `api.enterprise.githubcopilot.com:443` などへの接続が出た（CLI が proxy を使うことを確認）。**この実行は実 AI を 1 回呼び、CLI の表示は "AI Credits 8.27"。また利用者の既定の Copilot home（`%USERPROFILE%\.copilot`）に session（`4cf8f103-…`）を 1 件作ったので、そのフォルダーを削除した**
  - proxy なしでアプリの確認を行っても、1 回目は同じく失敗した。SDK 1.0.11 の小さな確認用 console（`%TEMP%` に作成し、後で削除）で、同梱 CLI の start・ping・認証状態・model 一覧（27 件）・stop はすべて成功した
  - 直後にもう一度アプリの確認を行うと「既存の Copilot CLI login を利用できます。」になった。失敗の原因は**確定していない**（アプリの確認の時間の上限は 15 秒。[CopilotAuthenticationService.cs](../src/StudyReportEvaluator.App/Copilot/CopilotAuthenticationService.cs) の 208 行目。2 回目の失敗の時刻に `%LOCALAPPDATA%\copilot\pkg\win32-x64\1.0.79` の更新時刻が 20:55:02 になっていたので、CLI の初回の展開に時間がかかった可能性があるが、確かめていない）。proxy が無くても同じ失敗が起きたので、この失敗の再現に proxy は必要なかった

### 3.2 本実行（20:59〜21:04）: **PASS**

- 手順: 2 章の proxy（127.0.0.1:18931）を起動したまま、`HTTPS_PROXY=http://127.0.0.1:18931` を設定し、`COPILOT_*` を消したシェルから、第3ラウンドの RR-03 と同じ EXE（`bin\Release\net10.0\win-x64\StudyReportEvaluator.App.exe`、更新 2026-09-24 04:58）・入力（`tests\fixtures\system-test\SystemTest-10Students.xlsx` のコピー）・最終データ行 2 の 1 行で実行した。モデルは既定の設定（`auto`）。script はセッション領域の `rr03m3-run.ps1`、画面の記録は `rr03m3-2.log`
- 経過（画面の記録、proxy のログ、ジョブログ）:
  - 20:59:12 開始。20:59:22.049 に最初の attempt（Reference、operation ID `6470246c…`、`AttemptNumber=1`）が始まり、20:59:22.088 に送信を始めた（ジョブログの AttemptStarted と UsageUpdated）
  - 20:59:26 この attempt の CLI が `api.enterprise.githubcopilot.com:443` へのトンネル（id=38）を開いた。20:59:30 proxy に 75 秒の停止を指示し、ログは `freeze secs=75.0 ids=[35, 37, 38]`
  - 21:00:22.343 最初の attempt が `AttemptOutcome=4`（`TimedOut`。[AttemptOutcome.cs](../src/StudyReportEvaluator.App/Usage/AttemptOutcome.cs)）で終わった（ジョブログ 6 行目の AttemptFinished）。送信の開始から約 60.3 秒で、アプリの attempt timeout（120 秒）より前であり、1.3 の SDK の既定の待ち時間（60 秒）の `TimeoutException` と一致する。後始末の失敗（`CleanupFailed`）にはなっていない
  - 21:00:31.696 新しい attempt が始まり（ジョブログ 7 行目の AttemptStarted。この行には attempt ID だけがある）、21:00:50 に終わった AttemptFinished（12 行目）で、**同じ operation ID `6470246c…` の `AttemptNumber=2`、`AttemptOutcome=1`（`Succeeded`）**と分かる。同じころ、画面の記録では子プロセスの `copilot.exe` の PID が 5700 から 31924 に変わり、proxy のログでは新しいトンネル（id=40〜42。止めた後に開いたので転送された）が開いた。どちらも新しい CLI と時刻が合うが、トンネルと PID の対応は記録していない
  - 21:00:45 proxy が止めたトンネル（id=35・37・38）を閉じた
  - 21:04:19 終了。結果画面は「15 / 15 完了 · error 0 · cancelled 0」、行 2 は「成功」、`ResultsCostSummary` は「入力 66832（観測 16/17 試行・部分取得）／出力 3859（観測 16/17 試行・部分取得）」、`ExportStatus` は「検証済みfinal workbookを自動commitしました。」
- ジョブログ（`c983c83892fd4eb6bf36bfb895878fb2.jsonl`、103 行、SHA-256 `98423134160D52781B75ED414D09E351D4F80BD24EABA2C278B2FF92896CC103`。コピーはセッション領域の `rr03m3\`）: 最後の行は `Completion=1`（Completed）、`AttemptCount=17`、`AttemptStatuses={"FinalObserved":17}`。15 の operation に対して attempt が 17 件で、2 回目の attempt は次の 2 件
  | operation ID | Operation | 1 回目 | 2 回目 |
  |---|---|---|---|
  | `6470246c…` | Reference（1） | 21:00:22 `TimedOut`（4）。proxy で止めた通信による | 21:00:50 `Succeeded`（1） |
  | `58a8f24d…` | Normal（0） | 21:03:19 `SchemaInvalid`（2）。proxy はこの時刻には何も止めていない（止めたのは 20:59:30〜21:00:45） | 21:03:52 `Succeeded`（1） |
- proxy のログ（`rr03m3\proxy.log`、SHA-256 `9B018AC32C12978E67B580C712AA157C99416758D67E7CE0E988A9265326F8C3`）: 記録は時刻・接続先の host と port・転送 byte 数だけ
- DoD の判定: 通信を止めたことによる実際の SDK の `TimeoutException` の後に、同じ operation で新しい session の retry（`AttemptNumber=2`）が行われ、成功したことをジョブログで観測した。**RR-03 の DoD を満たす（PASS）**。あわせて、schema 不正の後の retry（要求定義書 7.6 節の「最大1回」）も、fault を入れていない状態で 1 件観測した
- 変えなかったもの: 製品のコード、retry の規則、テスト、ホストのネットワーク設定。proxy と script はセッション領域だけに置き、リポジトリに入れていない
- 後始末: proxy（PID 3652）を止めた。`setting.txt` は、20:50 の準備のときに `4C9785CB…3A` であることを確かめた（その後の 20:51 の起動と 2 回の確認の起動の前後、および本実行の直前には確かめていない）。本実行の後に SHA-256 が `F7E00597…A410` に変わっていたので、退避したものに戻した（`4C9785CB…3A`）。ジョブログをセッション領域へ移し、`%TEMP%\sre-rt07`（入力のコピーと出力の workbook）・`%TEMP%\sre-rr03p`・`%TEMP%\sre-diag` を削除した。アプリは window を閉じて終了した（`exited=True`）

## 4. 副次的な所見

- 1.3 のとおり、アプリの attempt timeout の既定は 120 秒（要求定義書 7.6 節 349 行目、[EphemeralEvaluationRunner.cs](../src/StudyReportEvaluator.App/Copilot/EphemeralEvaluationRunner.cs) の 15 行目）だが、送信の待ちには SDK の既定の 60 秒が別にかかる。このため、送信（`SendAsync`）から 60 秒以内に `session.idle` が来ないと、120 秒の前に `TimeoutException` になる（3.2 で、送信の開始から約 60.3 秒での `TimedOut` を観測した）。要求定義書の「attempt timeoutの既定は120秒」との関係は所有者の判断が必要であり、この RR-03 では製品を変えない

## 5. 敵対的レビュー

rubber-duck のレビューで blocking の指摘は無かった（ジョブログと proxy のログの SHA-256、同じ operation ID の AttemptNumber=2、SDK 1.0.11 の 60 秒の既定、製品のフックを加えていないことを確認）。blocking でない指摘 3 件と提案 1 件を反映した: TimedOut の時刻を AttemptFinished の行（21:00:22.343、約 60.3 秒）に直した。方法 1 が「決して」retry に届かないという書き方、proxy が「原因ではない」という書き方、PID とトンネルの対応の書き方を、観測の範囲に弱めた。setting.txt の実行直前の SHA-256 を確かめていないことを明記した。4 章の 60 秒の説明を SDK の動作（session.idle を待つ）に合わせた

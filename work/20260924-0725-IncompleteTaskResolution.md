# 未完了タスクの解決記録（2026-09-24 第2ラウンド）

- 計画: [202609240300-RemainTaskExecutionPlan.md](202609240300-RemainTaskExecutionPlan.md)。第1ラウンドの記録: [20260924-0335-RemainTaskExecutionRecord.md](20260924-0335-RemainTaskExecutionRecord.md)
- 指示: 「全ての完了していないタスクの解決策を立案して、タスクを完了させてください。実施のための全てのリソースの仕様を承認します」。これを commit／push、CI の起動、実 AI 呼び出し、再 package、Windows Sandbox の利用の承認として扱った。**Release の作成・公開・署名は行っていない**（下の RT-21）
- ホスト `DAHATAKE-STD2`（Windows 11 Enterprise Insider build 29671、非管理者、Developer Mode なし）、.NET SDK 10.0.401（CI は 10.0.400）
- 証跡（TRX、JSONL、ログ）はリポジトリの外（セッション領域 `files\`）に置き、SHA-256 をここに記録した

## 解決策の一覧

| タスク | 解決策 | 結果 |
|---|---|---|
| RT-17 CI の P06／P07 | CI と同じ手順を detached worktree（`C:\GitHub\sre-ci`）で再現し、commit・push して CI で確認する | **完了**。ローカル P06/P07 は 7/7 `PASS_REQUIRED`、CI run 35925523213 は全ステップ成功 |
| RT-19 MSIX（到達点 A） | 同じ再現で `test-windows-msix-unsigned.ps1` を実行する | **完了**。失敗の原因を特定・修正（下記）。ローカル `PASS_MECHANISM`、CI 成功 |
| RT-20 現 HEAD のローカルビルド | worktree で publish → package → P06 → opt-in テスト | **完了** |
| RT-09 symlink 保護テスト | 使い捨ての Windows Sandbox（管理者ユーザー）でテスト DLL を実行する | **完了**（成功。スキップではない） |
| RT-07 実 Copilot | 合成入力で実アプリを UIA 操作する | **完了**（retry の実観測と明示モデルは NOT_RUN） |
| RT-08 native のコスト表示 | 実アプリを 125% DPI で操作する | **完了**（Narrator は NOT_RUN） |
| RT-15 受け入れ確認 | 40 行の合成入力を作り、実アプリで中断・再開・window close・再起動・設計不一致を操作する | 手順 1〜4 **PASS**（手順 1 の再開後は利用者の指示で途中停止）、手順 5 は一部 NOT_RUN |
| RT-16 版上げ | 判断材料をそろえて判断を記録する | **判断を記録**（`0.8.6` のまま） |
| RT-18 MSIX の 7 項目 | 要求定義の現行の規定に沿って既定の判断を記録する | **判断を記録** |
| RT-21 clean-host と公開 | 前提の有無を確かめ、Sandbox で参考観測する | CH-01〜06 は **NOT_RUN**、公開は**実施しない** |
| RT-06／RT-12／RT-05 項目2／SampleWorkbook | 判断を記録する | 下記 |

## RT-17／RT-19／RT-20 ローカル CI 再現と MSIX の修正

[ci.yml](../.github/workflows/ci.yml) と同じ順番で worktree で実行した。packaging の script は git の作業ツリーが clean であることを要求するので、第1ラウンドの変更を先に commit した（`481dac5`）。

1. restore（locked-mode）、`version.ps1 verify`、`version.tests.ps1`、Release build: 成功
2. deterministic tests: Core 190/190。App は、worktree で自分が `global.json` を書き換えたことによる `PackageLockTests` の 1 件を除いて成功（書き換えを戻した後、`PackageLockTests` 9/9 成功）
3. `scripts/test-windows-zip.ps1`: `PASS_REQUIRED`
4. `publish-windows.ps1 -SingleFile` → `package-windows-singlefile.ps1` → `test-windows-singlefile.ps1`: P06 の 7 シナリオ 7/7、`PASS_REQUIRED`。sidecar の一致は script（`Assert-ExactSidecar`）が検査している
5. `test-windows-msix-unsigned.ps1`: **失敗**。`MSIX entry is missing or empty: docs\technical-guid.md`

MSIX の失敗の原因: `$RequiredPublicEntries` のうち `ea91d9f` で追加した 4 件（`docs\technical-guid.md` と `images\` の SVG 3 件）が `\` 区切りだった。`ZipArchive.GetEntry` は package 内の entry 名（`/` 区切り）と完全一致で探すので、実在しても見つからない。`/` に直した（`e152764`）。`WindowsInstallerPackageTests` と `DocumentationContractTests` 28/28 成功。再実行で `PASS_MECHANISM`、MSIX の SHA-256 `A6E1B5C181C550B6331596A3C0C5E9ADC1D862AD04942B7CAB4A7A3AFFA095F5`。

CI: `e152764` を push し、run [35925523213](https://github.com/dahatake/StudyReport-Evaluator/actions/runs/35925523213) は **success**。Windows job の deterministic tests、ZIP、単一 EXE（P06／P07）、unsigned MSIX、repository integrity のすべてのステップが成功した。MSIX の artifact は 2,033 bytes（証跡だけで、本体は upload していない）。

RT-20 の成果物（worktree、`e152764`）:

| 成果物 | bytes | SHA-256 |
|---|---|---|
| `StudyReportEvaluator-win-x64.exe` | 283,769,208 | `2275BE7780B3AAE5486611C409CFB303DCDEA324B28746AAFE462EDDFA63C8E1` |
| `StudyReportEvaluator-win-x64.zip` | 156,696,152 | `75798FA2E99D7C124B1625A7A4337E5D71F2A1A3E8A43B038C5C96AF25D71D8A` |

実成果物の opt-in テスト（第1ラウンドでスキップされていた 8 件）: `WindowsSingleFilePackageTests` の 7 件（P06 の 7 シナリオ。`RUN_WINDOWS_SINGLEFILE_PACKAGE_TESTS` で有効）と `WindowsSingleFileArtifactTests.Opted_in_P02_artifact_is_packaged_by_the_actual_script_with_identical_bytes_and_versions`（`RUN_WINDOWS_SINGLEFILE_ARTIFACT_TESTS` で有効）の 1 件。

- 7 件: `test-windows-singlefile.ps1`（P07）が opt-in を有効にして実行した。`windows-singlefile.trx`（`DB6895CA0B99A7A22D3A217C9E756368A703157335F3C77B63B1D93539DA079A`）で 7 件すべて Passed
- 1 件: D4（[20260916-latest-local-build.md](20260916-latest-local-build.md)）と同じく 2 つの opt-in を有効にして `WindowsSingleFilePublishTests`／`WindowsSingleFileArtifactTests` を実行した。`rt20-optin.trx`（`7B8A22A262E2763E3A51E617EE94308F2E81CC5B81E6CAACE23B4992B7BD99A4`）: **101 件成功・失敗 0・スキップ 0**。上の 1 件はここで Passed（D4 の時点ではこの 2 クラスで 100 件）

署名と公開はしていない。

## RT-09 symlink 保護テスト

Windows Sandbox（ネットワーク無効）に、dotnet（`C:\Program Files\dotnet`）とテストの出力（`e152764` の Release build）を読み取り専用で割り当てて実行した。Sandbox のユーザー `WDAGUtilityAccount` は Administrators に属し、`SeCreateSymbolicLinkPrivilege` を持つ。

- `rt09-symlink.trx`（`B79BD08190E2314D4F427FAD2CF9D27279C4973617DDF32133ADF9B89C25F688`）: `JobCostBackendTests.Symlink_is_not_followed_by_reader_or_retention_when_creation_is_available` **1 件成功**（executed 1、passed 1、notExecuted 0）
- これで RT-10 の L05 の「権限がない環境ではスキップ」は、実際に成功した記録に置き換わった

## RT-07／RT-08／RT-15 実 Copilot と native での確認

実アプリ（`src\StudyReportEvaluator.App\bin\Release\net10.0\win-x64\StudyReportEvaluator.App.exe`）を UIA で操作した。入力は `tests/fixtures/system-test/SystemTest-10Students.xlsx` のコピー（合成データ）と、それをもとに作った 40 行の合成 workbook（下の RT-15）。利用者の `setting.txt` は開始前に退避し、終了後に元のバイト列（`4C9785CB…3A`）に戻した。ジョブログ（`%LOCALAPPDATA%\StudyReportEvaluator\jobs`）と一時フォルダーは削除した（ログのコピーはセッション領域にある）。

RT-07 の前提（計画の 191 行目）として記録する条件:

- 承認: 今回の指示の包括承認（「実施のための全てのリソースの仕様を承認します」）。個別の事前承認は得ていない
- 対象アカウント: このホストで既に login している Copilot CLI の資格情報（所有者本人のもの）。アプリも作業者も資格情報を読み出していない（CLI に委ねた）
- 合成入力: 上の 2 つの workbook だけ。実データは使っていない
- 呼び出し回数の上限: ジョブ全体の上限は事前に決めていない。アプリが run ごとに表示・適用する attempt の上限（例: 2 人・5 問で最大 75）の範囲で実行した。実際の回数は下の表に記録した

### 実行したジョブ（画面の値とジョブログの値）

| ジョブ | 内容 | 終了 | attempt | 入力 / 出力 token | attempt の合計との一致 | Operation（Normal/Reference/Similarity） |
|---|---|---|---|---|---|---|
| `75c31698` | 通常（2 人・5 問） | 完了 | 25（エラー 0） | 102,257 / 5,210 | 一致 | 10 / 5 / 10 |
| `b157b3eb` | 実行中に中断（4 人中 1 人目の後） | 中断 | 15 | 60,678 / 3,277 | 一致 | 5 / 5 / 5 |
| `8253a955` | 同じ session で再開 | 完了 | 26 | 108,028 / 5,355 | 一致 | 13 / **0** / 13 |
| `eb3105a3` | 実行中に window を閉じる | 中断 | 15 | 61,284 / 3,299 | 一致 | 5 / 5 / 5 |
| `f0bb299d` | 設定を保存してから実行し、window を閉じる | 中断 | 15 | 61,920 / 3,601 | 一致 | 5 / 5 / 5 |
| `637616df` | 再起動し、保存定義を適用して再開 | 完了 | 26 | 109,233 / 5,406 | 一致 | 13 / **0** / 13 |
| `932e786e` | 既定の `COPILOT_HOME` で 1 人分（下の所見 F2） | 完了 | 15 | 60,798 / 3,350 | 一致 | 5 / 5 / 5 |
| `d437c430` | 40 行の合成入力。12 行目の後で中断（RT-15 手順 1） | 中断 | 115 | 478,386 / 22,712 | 一致 | 55 / 5 / 55 |
| `cab02c5f` | 同じ session で再開。**利用者の指示で途中停止**（下記） | 終端記録なし | 60（停止まで） | 250,374 /（停止まで） | — | 30 / **0** / 30 |

- 9 ジョブ合計で attempt は 312 件、すべて成功（エラー 0）。**実際の SDK 例外による retry は観測できなかった**（未観測。retry はテストの fake でだけ確認済み）
- 画面で値を読んだ 5 件（`75c31698`、`b157b3eb`、`8253a955`、`637616df`、`d437c430`）では、画面の token がジョブログの値と一致した（例: `75c31698` は画面 102257 / 5210、`d437c430` は画面「入力 478386（観測 115/115 試行）／出力 22712」）。window を閉じた 2 件は画面が残らないので、ジョブログだけで確認した。`PremiumRequests` は `75c31698`・`b157b3eb`・`8253a955`・`932e786e` が未取得（null。`75c31698` の画面は「未取得」）、`eb3105a3`・`f0bb299d`・`637616df` は SDK が 1〜2 を報告した。AI クレジットは RT-06 のとおり「課金単位未確認」と表示される
- モデルは `auto` だけで実行した。**明示モデルでの実行は NOT_RUN**（利用者の指示で長時間の実行を打ち切ったため、未実施のまま）
- ジョブログには学生の回答文が含まれていないことを確認した。`Context` は AppVersion 0.8.6、SDK 1.0.11、CLI 1.0.79
- 再開したジョブ（`IsResume=true`）では Reference（参照解答の生成）が 0 件で、中断前に完了した行も再評価していない
- window を閉じた場合の終了は 0.2〜0.4 秒。partial の workbook は壊れておらず、プロセスは残らなかった

### RT-08 native のコスト表示

- DPI 120（125%）で表示した。Tab キーで `ResultsCost-OpenLogButton` と `OpenDirectoryButton` まで移動できる。右矢印キーでジョブログのタブに切り替わり、ログの本文が表示される
- 「ログを開く」は shell に渡される。このホストでは `.jsonl` に関連付けがないため、Windows の「アプリを選択」画面が出た（所見 F3）。「保存先を開く」は Explorer で jobs フォルダーを開いた
- Narrator: **NOT_RUN**（読み上げの内容を人が聞いて判断する必要がある）

### RT-15 受け入れ確認

番号は計画（[202609240300-RemainTaskExecutionPlan.md](202609240300-RemainTaskExecutionPlan.md) の 260〜264 行目）に合わせた。40 行の入力は、10 人分の fixture の 10 行を 4 回複製し、ID・メール・名前・回答の番号を 01〜40 に振り直して作った合成 workbook（41 行、SHA-256 `3B650EC7DCAC209E8BC762F2ADDFFC5D0330BEB7F4D31A0A752D34B4FD913424`。一時ファイルで、リポジトリには追加していない）。

| 手順 | 結果 |
|---|---|
| 1 40 行を 12 行目で中断し、同じ session で再開 | **PASS**。`d437c430` を「実測: 参照 5 / 5 · 行 12 / 40」の時点で中断し、中断は 1 秒で確定した。「中断した処理を再開準備」の後に開始ボタンが有効になり、再開した `cab02c5f`（`IsResume=true`）は「行 12 / 40」から 13 行目以降を評価した。Reference は 0 件で、参照回答を作り直していない。再開後の実行は約 13 分（60 attempt）で、**利用者の指示により途中で停止した**。40 行すべての完了は観測していない |
| 2 再起動後に picker で partial を選んで再開 | PASS。ただし**保存定義を適用する導線だけ**（`f0bb299d` → `637616df`。6 項目の検証がすべて SUCCESS）。設計を保存せずに再起動すると Definition が MISMATCH になり、開始できない（所見 F1） |
| 3 設計を変えてから再開 | PASS。類似度の重みを 0.1→0.2 に変えると Definition が MISMATCH、CheckpointShape が「要確認 CHECKPOINT_INVALID」になり、開始ボタンは無効のまま。partial の hash（`D84A995D…`）は変わらなかった |
| 4 実行中に window を閉じ、再起動後に再開 | PASS。`eb3105a3` と `f0bb299d` は 0.2〜0.4 秒で終了し、partial は壊れず、プロセスも残らなかった。`f0bb299d` の partial から、再起動後に `637616df` で再開して完了した |
| 5 Narrator・native DPI・OS shutdown | native DPI は RT-08 で確認（125%）。Narrator と OS shutdown は **NOT_RUN**。Narrator は上と同じ理由。OS shutdown には使い捨ての VM と、その中での本人の Copilot login が要る。Windows Sandbox の中で本人の login を行えるかは**未検証**（第3ラウンドでも確かめられなかった。[20260924-1815-RemainTaskExecutionRecord3.md](20260924-1815-RemainTaskExecutionRecord3.md) の RR-04） |

停止の方法: 監視していた PowerShell を止めたときに、そこから起動したアプリのプロセスも一緒に終了した（通常の window close ではない）。そのため `cab02c5f` のジョブログには終端の記録がない。partial（`eval-20260924-0744.partial.xlsx`、27,273 bytes、SHA-256 `45F934292B0B5446C99A021E64677905B6D2608FBBC2AB86755B0C36838E0C62`）は停止の 10 秒前に更新されており、ZIP として開けた（10 entries）。

### ST-UC-20（`AuthenticatedSyntheticSmokeTests`）

`STUDY_REPORT_EVALUATOR_COPILOT_LIVE_SMOKE=1` で実行したが `SKIPPED_NOT_AUTHENTICATED` になった。テストの出力フォルダーには同梱 CLI も `copilot-runtime.json` もないので CLI を解決できず（`BundledCopilotCliPathResolver` は `AppContext.BaseDirectory` を見る）、テストはこれを「未認証」として扱う（所見 F4）。実 AI の確認は、上の実アプリの操作で代わりに行った。

## 所見

| ID | 内容 | 根拠 | 対応 |
|---|---|---|---|
| F1 | 再起動後の再開は、中断前に設計を保存し、再起動後に保存定義を適用した場合だけ受け付けられる。設問と評価者の ID が起動するたびに新しく作られ（`InputViewModel.cs`、`QuantificationDesignViewModel.cs` の `NewId`）、再開の受け入れ判定（`ResumeAdmissionEvaluator.cs`）が定義を完全一致で比べるため | 上の RT-15 手順 2。[privacy-and-data-handling.md](../docs/privacy-and-data-handling.md) の 125 行目にも、再起動後に残すには明示保存が要るとある | 製品は設計どおりなので変えない。[getting-started.md](../docs/getting-started.md) の「checkpointから再開」に手順を 1 段落追記した |
| F2 | 最初の試行では、既定の `COPILOT_HOME` で `COPILOT_RUNTIME_FAILED` になった | 後で 2 回の認証確認と 1 回の実行（`932e786e`、15/15 成功）では再現しなかった | 一過性として記録し、対応しない。なお「同梱 CLI が新しい版に委譲しているのでは」という仮説は否定された: `copilot.exe --version` を引数なしで実行すると 1.0.88 と出るが、アプリが起動する子プロセスのコマンドラインは `--headless --no-auto-update --log-level none --stdio` で、`--no-auto-update` を付けると 1.0.79 と出る |
| F3 | `.jsonl` に関連付けがないホストでは「ログを開く」で Windows の「アプリを選択」画面が出る | RT-08 の観測 | shell に渡すのは設計どおり。対応しない |
| F4 | ST-UC-20 はテストの出力フォルダーから CLI を解決できないので、認証済みでも実行されない | 上のとおり | テストの設計上の制約として記録する。今回は要求された範囲の外なので変更しない |

## 判断の記録

### RT-16 版上げ — `0.8.6` のまま

- 判断: 今は `0.9.0` に上げない。D7 の「他の未公開の機能と合わせてリリースのときに判断する」に従い、リリースを行う時点で判断する。今回はリリースを行わない（RT-21）ので、上げる理由がない
- 判断の材料: tag `v1.0.0`（annotated、2026-09-02、commit `b68e757`「Prepare v1.0.0 release」）は HEAD の祖先にあるが、GitHub Release は作られていない。その後 `268efde` で版が `0.8.0` に戻された。最新の Release は `v0.8.1`。[release.yml](../.github/workflows/release.yml) の tag の検査（形式、HEAD を指すこと、annotated であること、`version.ps1 verify -Tag`、CHANGELOG の日付付きの節）にも、[dev/version.ps1](../dev/version.ps1) にも、版が単調に増えることの検査はない。したがって、`0.9.0` にしても道具の上では衝突しない。ただし `v1.0.0` の tag より小さい版になることは、リリースのときに所有者が判断する
- これは今回の指示による委任で記録した既定の判断で、所有者が覆せる

### RT-18 MSIX の 7 項目

要求定義（[requirements-definition.md](../docs/requirements-definition.md) の 17・713・757・874 行目）は、development MSIX について「既存の source と機構の回帰を保つだけで拡張しない」「本体を upload しない」と定めている。これに沿って記録した。値や承認を推測で決めたものはない。

| 項目 | 判断 |
|---|---|
| 1 到達点 | **A**（生成を検証する）。今回、ローカルと CI で達成した |
| 2 本体を upload しない、という禁止 | **変えない**（757 行目のまま） |
| 3 発行者・証明書・署名サービス | **未決**（到達点 A では不要） |
| 4 Identity・表示名・アイコン | **変えない** |
| 5 artifact か Release か | **artifact だけ**（証跡。本体は含めない） |
| 6 版・更新・設定の移行・削除時の扱い | **未決**（到達点 A では不要） |
| 7 承認者・許可する ref・clean-host の担当と環境 | **未決** |

RT-19 は到達点 A までで完了。手順 1〜4（要求定義の改訂、MSIX 専用の workflow、契約テストの分離、署名と clean Windows 11 での検証）は到達点 B 以上の場合の作業なので、該当しない。

### RT-21 clean-host と公開

- CH-01〜06: **NOT_RUN**。[SystemTest-prompt.md](../tests/SystemTest-prompt.md) の 1422 行目は、GitHub の draft／public candidate がない段階では CH-01〜06 を `NOT_RUN` とする、と定めている。今回は candidate を作っていない。また、使える Windows Sandbox のユーザーは管理者で、OS は Insider build なので、CH-01（fresh OS・標準ユーザー）の前提を満たさない
- 参考観測（CH の判定には使わない）: Sandbox（ネットワーク無効、.NET も Copilot CLI も未導入）に RT-20 の EXE（`2275BE77…`）をコピーして起動した。主 window「StudyReport Evaluator」が表示され、入力と採点設計のステップが有効、実行と結果のステップが無効（入力前なので正しい）。起動しただけでは `setting.txt` もジョブログも作られない。window を閉じると exit code 0 で終わり、プロセスは残らず、EXE も変わらなかった
- 公開: 所有者からの明示的な指示がないので、candidate の作成も protected publish も**実施しない**

### その他

| 項目 | 判断 |
|---|---|
| RT-06 AI クレジットの数値表示 | **BLOCKED のまま**。第1ラウンドで確認した公式資料（W1〜W4）に、`TotalNanoAiu` を AI クレジットへ換算する規則は書かれていない |
| RT-12 同じジョブの中でログの保存をやり直す UI | **実装しない**。要求にない任意の機能（YAGNI） |
| RT-05 項目2 GUI 子プロセスの隔離 | **製品は変えない**。上書きの仕組みを加えるのは要求にない。隔離が必要な試験は、別の Windows ユーザーか Windows Sandbox で行う（RT-09 で Sandbox が使えることを確かめた）。実データの smoke は既定で無効のまま |
| SampleWorkbook の不一致 | **環境の問題として記録**。このホストの Git 管理外のサンプルは profile とは別のファイル。サンプルも期待値も変えない |
| RT-10 の PARTIAL・NOT_RUN | L05 は RT-09 で成功。U02 のうち DPI・Tab・「ログを開く」は RT-08 で native に観測した。そのほかの PARTIAL は第1ラウンドの記録のまま |

## 後片付け

- 利用者の `setting.txt` を元のバイト列（`4C9785CB47280C4E546C518964E85F43CF1F0F4C9069F93D30F58F7C34728F3A`）に戻した
- ジョブログのフォルダー、`%TEMP%` の作業フォルダー、Windows Sandbox を削除・終了した
- アプリと CLI のプロセスは残っていない

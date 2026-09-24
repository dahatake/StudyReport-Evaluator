# 残タスク実行計画（第3ラウンド, 2026-09-24 08:30 JST）

- 基準: `main` の HEAD `d4c1eaa`（「docs: record remaining-task resolution and restart resume steps」）。この計画ファイルを作る前の作業ツリーは clean（未追跡はこの計画ファイルだけ）
- 前回までの計画と記録:
  - 計画: [202609240300-RemainTaskExecutionPlan.md](202609240300-RemainTaskExecutionPlan.md)（RT-01〜RT-21。DoD は 183〜316 行目）
  - 第1ラウンドの記録: [20260924-0335-RemainTaskExecutionRecord.md](20260924-0335-RemainTaskExecutionRecord.md)
  - 第2ラウンドの記録: [20260924-0725-IncompleteTaskResolution.md](20260924-0725-IncompleteTaskResolution.md)
- ホスト: `DAHATAKE-STD2`（Windows 11 Enterprise Insider build 29671、非管理者、120 DPI）、.NET SDK 10.0.401（CI は 10.0.400。`global.json` は編集しない）
- 表記: 「事実」には出典（ファイルと行、run ID、コマンドの出力）を付ける。「計画」は実行前の手順で、結果ではない。まだ確かめていない前提には **未検証** と書く

---

## 1. 完了済みタスクの分析

### 1.1 RT-01〜RT-21 の状態

| RT | 内容 | 状態 | 根拠 |
|---|---|---|---|
| RT-01〜05, 10〜14 | 第1ラウンドの実装・文書・テスト | 完了（DoD 充足） | 第1ラウンドの記録 |
| RT-05 項目2 | GUI 子プロセスの隔離 | 判断済み（製品は変えない） | 第2ラウンドの記録「その他」 |
| RT-06 | AI クレジットの数値表示 | **BLOCKED**（換算規則が公式資料 W1〜W4 にない） | 同上 |
| RT-07 | 実 Copilot での合成実行 | 完了。ただし **明示モデルの実行と、実際の SDK 例外による retry は NOT_RUN／未観測** | 第2ラウンドの記録 72〜89 行目（84・86 行目） |
| RT-08 | native のコスト表示 | 完了。ただし **Narrator は NOT_RUN** | 同 91〜95 行目 |
| RT-09 | symlink 保護テスト | 完了（Sandbox で 1/1 成功） | 同 50〜56 行目 |
| RT-12 | ログ再保存の UI | 判断済み（実装しない、YAGNI） | 同「その他」 |
| RT-15 | 受け入れ確認 | 手順 1〜4 PASS。**手順 5 の Narrator と OS shutdown は NOT_RUN** | 同 97〜109 行目 |
| RT-16 | 版上げ | 判断済み（`0.8.6` のまま。リリースのときに再判断） | 同「RT-16」 |
| RT-17 | CI の P06／P07 | `e152764` の run 35925523213 では完了。**ただし `d4c1eaa` の run 35933450815 で P07 が失敗（下の 1.2）** | `gh run view` |
| RT-18 | MSIX の 7 項目 | 判断済み（到達点 A。項目 3・6・7 は未決） | 同「RT-18」 |
| RT-19 | MSIX 到達点 A | 完了 | 同 24〜36 行目 |
| RT-20 | 現 HEAD のローカルビルド | 完了 | 同 38〜48 行目 |
| RT-21 | clean-host と公開 | **CH-01〜06 は NOT_RUN、公開は未実施**（所有者の指示なし） | 同「RT-21」 |

### 1.2 新しく判明した事実（今回の調査）

- CI run [35933450815](https://github.com/dahatake/StudyReport-Evaluator/actions/runs/35933450815)（`d4c1eaa`）は **failure**。macOS の 2 job は success。Windows job はステップ「Build and validate Windows development-host single-file package」で `P07_FAIL stage=p06-trx code=P07_TRX_TEST_NOT_PASSED` を出して失敗し、以降の MSIX と repository integrity のステップは skipped（`gh run view 35933450815 --log-failed`、`--json jobs`）
- この例外は [test-windows-singlefile.ps1](../scripts/test-windows-singlefile.ps1) の 433 行目で投げられる。P06 の TRX に `outcome` が `Passed` でない結果が 1 件以上あった、という意味
- `e152764` と `d4c1eaa` の差分は文書だけ（`CHANGELOG.md`、`dev/docs/implementation-status.md`、`dev/docs/traceability.md`、`docs/getting-started.md`、`work/` の 2 ファイル。`git diff --stat e152764 d4c1eaa`）。製品もテストも変わっていないので、**再現性のない失敗（flaky）である可能性が高いが、未確定**
- どのシナリオが失敗したかは artifact から分からない。P07 の生の TRX はわざと upload しておらず（[ci.yml](../.github/workflows/ci.yml) の 147 行目）、単一 EXE の証跡の upload はステップが成功したときだけ（同 168 行目）。`test-results-35933450815` に入っているのは deterministic tests と ZIP の TRX だけだった（ダウンロードして確認）
- CI の履歴: 直近 8 回のうち成功は `e152764` の 1 回だけ（`gh run list --workflow ci.yml -L 8`）。それ以前の失敗は第2ラウンドで直した MSIX の問題などを含む

### 1.3 残タスクではないもの

| 項目 | 理由 |
|---|---|
| RT-10 の PARTIAL | DoD は充足済み（第1ラウンドの記録 88〜123 行目） |
| RT-15 の 40 行すべての完了 | 手順 1（前回の計画の 261 行目）は「13 行目から再開し、参照回答を作り直さない」ことで、第2ラウンドで PASS。全行の完了は手順にも完了条件（266 行目）にもない。**任意**とし、この計画では実施しない |
| RT-12、RT-05 項目2 | 判断済み。再度扱わない |
| worktree `artifacts/test/singlefile-spike/source` | 自分が作ったものではない。触らない |

### 1.4 前回の完了条件との関係

前回の計画の RT-07（193 行目）と RT-15（266 行目）の完了条件は「PASS、FAIL、NOT_RUN のどれかが（理由つきで）記録されている」ことで、第2ラウンドで**満たしている**。したがって RR-02〜RR-05 は、完了条件を満たすための作業ではなく、**NOT_RUN を実際の観測に置き換えるための追加の作業**である。実施できない場合に NOT_RUN のまま閉じても、前回の完了条件には反しない。

---

## 2. 残タスクの一覧

| ID | 内容 | 由来 | 実施者 | 前提・承認 |
|---|---|---|---|---|
| RR-00 | CI run 35933450815 の P07 失敗の原因を確定し、CI を緑に戻す | 1.2 | エージェント | 既存の包括承認（CI の再実行） |
| RR-01 | ST-UC-20 の CLI 解決（所見 F4） | 第2ラウンドの F4 | エージェント | 本人 login 済みの CLI |
| RR-02 | RT-07 明示モデルでの実行 | RT-07 | エージェント | 実 AI 呼び出し（包括承認） |
| RR-03 | RT-07 実際の SDK 例外による retry の観測 | RT-07 | エージェント | 同上 |
| RR-04 | RT-15 OS shutdown | RT-15 手順 5 | エージェント + 本人（login） | 使い捨ての環境と本人の login |
| RR-05 | Narrator（RT-08、RT-15、T39） | RT-08, RT-15 | **所有者（人）** | 人が聞いて判断する |
| RR-06 | T39-N2: native の 4 画面・設定 5 分類の再観測 | 前回の計画 §6 の範囲外項目 | エージェント | **所有者の明示的な承認**（3 回で停止する規則の例外。下記） |
| RR-07 | T39: 隔離ユーザーでの native の設定保存 | 同上 | エージェント | Windows Sandbox。RR-06 と同じ承認 |
| RR-08 | T39: 本人の 4 項目の walkthrough | 同上 | **所有者（人）** | — |
| RR-09 | リリース（版の判断 → candidate → clean-host CH-01〜06 → 公開） | RT-16, RT-21 | **所有者が指示** | 所有者の明示的な指示 |
| RR-10 | RT-18 項目 3・6・7 と、RT-19 の到達点 B／C の作業 | RT-18, RT-19 | 所有者（判断）+ エージェント（実装） | 到達点を B／C にする場合だけ |
| RR-11 | RT-06 の再確認（W1〜W4） | RT-06 | エージェント | リリースの直前 |
| RR-12 | SampleWorkbook の不一致 | 第2ラウンド「その他」 | **所有者** | profile と同じファイル |

各タスクは、実行しても DoD に届かない場合に「NOT_RUN（理由つき）」または「BLOCKED（理由つき）」で閉じる方法を決めておく（下の各節の「閉じ方」）。**NOT_RUN や BLOCKED を PASS として書かない。**

---

## 3. タスクごとの詳細

### RR-00 CI の P07 失敗（最優先）

- 出典: 1.2 のとおり
- 手順:
  1. `gh run rerun 35933450815 --failed` で Windows job だけ再実行する（約 40 分）
  2. 成功した場合: flaky として第3ラウンドの記録に run ID と結果を書く。製品もテストも変えない
  3. 失敗した場合: 第2ラウンドと同じく、detached worktree（例 `C:\GitHub\sre-ci3`、`d4c1eaa`）で ci.yml の順番どおりに `publish-windows.ps1 -SingleFile` → `package-windows-singlefile.ps1` → `test-windows-singlefile.ps1` を実行し、ローカルに残る P06 の TRX から失敗したシナリオと出力を特定する。原因に対して最小の修正を行い、commit・push して CI で確かめる
  4. ローカルでも CI でも再現しない場合: 再実行をもう 1 回だけ行い、結果を記録する（無制限に再実行しない）
- DoD: `main` の最新 commit で CI の全 job が success。または、失敗したシナリオと原因を TRX の該当箇所つきで記録し、修正を push して CI が success
- 時間の上限: 再実行 2 回まで。ローカル再現は 1 回（約 20 分）
- 閉じ方: 2 回再実行して 2 回とも失敗し、ローカルで再現しない場合は、失敗したシナリオ名が分からないことも含めて BLOCKED として記録する。**テストを弱めたり、skip にしたりしない**
- 注意: CI の失敗が分かりにくいからといって、P07 の生の TRX を upload するように ci.yml を変えることはしない（147 行目の方針。要求にない）

### RR-01 ST-UC-20 の CLI 解決（F4）

> **削除（2026-09-24 20:40、所有者の指示）**。§8 を参照

- 出典: [StudyReportEvaluator.App.csproj](../src/StudyReportEvaluator.App/StudyReportEvaluator.App.csproj) の 22〜47 行目（`WriteBundledCopilotManifest` は `_CopilotRid` が設定されたとき、つまり RID を指定したビルドのときだけ `copilot-runtime.json` を書く）。[AuthenticatedSyntheticSmokeTests.cs](../tests/StudyReportEvaluator.App.Tests/Copilot/AuthenticatedSyntheticSmokeTests.cs) の 83〜91 行目（Available 以外の状態をすべて NotAuthenticated として扱う）
- 手順:
  1. コードを変えずに、RID を指定してテストを実行する。RID を指定すると implicit restore で追跡対象の `packages.lock.json`（App、Core、App.Tests の 3 つ）が書き換わり、同梱 CLI 1.0.79 のダウンロードも走る（`StudyReportEvaluator.App.csproj` の 7〜8・22〜26 行目で、RID があると `CopilotSkipCliDownload` が false になる。今回のレビューでこのコマンドを実行した際に lock ファイル 3 つが書き換わり、CLI のダウンロードが TLS のエラーで失敗したと報告された。その後の `git status` では差分は残っていない）。そのため **detached worktree**（例 `C:\GitHub\sre-rr01`、`d4c1eaa`）で実行し、`main` の作業ツリーは汚さない: `$env:STUDY_REPORT_EVALUATOR_COPILOT_LIVE_SMOKE='1'; dotnet test tests\StudyReportEvaluator.App.Tests -c Release -r win-x64 --filter FullyQualifiedName~AuthenticatedSyntheticSmokeTests`
  2. テストの出力フォルダーに `copilot-runtime.json` と同梱 CLI があるかを確かめる
  3. 結果を次のように分けて記録する（テストは Available 以外をすべて NotAuthenticated にまとめる〔89〜91 行目〕ので、テストの出力だけで分類しない）:
     - restore または CLI のダウンロードに失敗した → `BLOCKED_ENVIRONMENT`（ネットワークなど環境の問題）
     - CLI はあるが resolver が受け付けない → 同梱の仕組みの問題として原因を記録する
     - CLI を解決できたが login がない → `SKIPPED_NOT_AUTHENTICATED`
     - smoke が成功／失敗した → `PASS`／`FAILED_ADVISORY`
  4. RID を指定して PASS になった場合: 実行方法を [SystemTest-prompt.md](../tests/SystemTest-prompt.md) の ST-UC-20 に 1 行追記する
  5. RID を指定しても CLI を解決できない場合（環境の問題を除く）: テストの設計上の制約として記録し、コードは変えない
  6. worktree を削除する（`git worktree remove`）
- DoD: 上の 4 分類のどれかに、具体的な観測（出力フォルダーの内容と状態）つきで記録されている
- 注意: 実行の前後で `setting.txt` を退避・復元する（5 章）。資格情報をファイルに出力しない
- 閉じ方: 上の手順 5

### RR-02 RT-07 明示モデルでの実行

- 出典: 前回の計画の 189〜193 行目（RT-07 の完了条件）、第2ラウンドの記録 86 行目（NOT_RUN）
- 用意してあるもの: セッション領域の `files\explicit-model-run.ps1`。設定を開き、combo `ExecutionModel` を展開して `auto` 以外のモデル（mini／haiku／flash を優先）を選び、`SettingsRequestClose` で閉じてから 1 行を実行する
- 手順:
  1. `setting.txt` を退避する（SHA を記録）
  2. 10 人の fixture のコピーから 1 行だけの合成 workbook を作る
  3. script を実行する。選ばれたモデル名を画面とジョブログの両方で確かめる
  4. 画面の token とジョブログの値が一致すること、attempt のエラーが 0 であることを確かめる
  5. `setting.txt` を元のバイト列に戻し、ジョブログと一時フォルダーを削除する（ログのコピーはセッション領域に残す）
- DoD: `auto` 以外のモデルで 1 行以上が完了し、ジョブログのモデル名が選んだモデルと一致する
- 時間の上限: 15 分。attempt の上限は 1 行分（アプリの表示の範囲）
- 閉じ方: combo に `auto` 以外の選択肢がない場合は、その観測（選択肢の一覧）を記録して NOT_RUN とする

### RR-03 RT-07 実際の SDK 例外による retry の観測

- 出典: 第2ラウンドの記録 84 行目（312 attempt がすべて成功し、retry は未観測）
- 方法（上から順に試す。どれも結果は「観測したとおり」に記録する）:
  1. ホストで、実行中の 1 行の途中で、アプリの子プロセスの `copilot.exe`（自分が起動したものだけ。PID で指定）を `Stop-Process -Id` で止める。アプリが retry するか、エラーとして止まるかは **未検証**
  2. Windows Sandbox（ネットワーク有効）で、実行中に `Disable-NetAdapter` でネットワークを切り、数秒後に戻す。Sandbox の中での本人の login が必要（RR-04 の手順 1 と同じ Sandbox を使い、**RR-04 の shutdown より前に**行う。shutdown で Sandbox と login は消える）
- 手順（共通）: 1 行だけの合成 workbook で実行し、ジョブログの attempt（`RetryCount` などの項目名は実行前にログの schema で確かめる）と画面の表示を記録する
- DoD: 実際の例外の後に retry が行われたことをジョブログで観測する。または、上の 2 つの方法で retry が起きないことを観測し、その観測（エラーの種類と画面の表示）を記録する
- 時間の上限: 方法ごとに 15 分
- 閉じ方: retry が起きない場合も、それ自体が観測結果。製品の retry 規則は変えない

### RR-04 RT-15 OS shutdown

> **削除（2026-09-24 20:40、所有者の指示）**。§8 を参照

- 出典: 前回の計画の 265 行目（手順 5）、第2ラウンドの記録 107 行目
- 第2ラウンドの記録は「Windows Sandbox の中では本人の login を行えない」と書いたが、**これは試していない判断**だった。この計画では未検証の前提として扱い、最初に確かめる
- 手順:
  1. 実現性の確認（10 分）: ネットワーク有効の Sandbox に RT-20 の EXE（`2275BE77…`、または RR-00 の後の最新 CI の EXE）を入れて起動し、アプリの「GitHubにログイン」（CLI の `login --web-flow`）から本人が login できるかを確かめる。Sandbox に Edge があることも **未検証**
  2. login できた場合: 書き込みのできるフォルダー（ホストの一時フォルダーを割り当てる）に 10 人の fixture のコピーを置いて実行し、2 行目以降の途中で Sandbox の中から `shutdown /s /t 0` を実行する
  3. ホスト側で、割り当てたフォルダーの partial の workbook が ZIP として開けるか、行が欠けていないかを確かめる。ジョブログは Sandbox の `%LOCALAPPDATA%` にあり、Sandbox の破棄で消えるので、**ジョブログの確認はこの DoD に含めない**（ジョブログを外に出す仕組みは加えない）
  4. login できない場合: 所有者に、Azure の使い捨て Windows 11 VM（Azure CLI はこのホストで login 済み）を使ってよいか、費用とともに確認する。VM の作成は所有者の承認の後
- DoD: OS shutdown の後に partial が壊れていないこと（または壊れたこと）を観測して記録する。前回の計画の 265 行目のとおり、OS shutdown は中断を要求するだけで保存の完了は保証しないので、壊れていた場合も観測として記録し、製品の不具合とは即断しない
- 閉じ方: Sandbox で login できず、VM の承認も得られない場合は NOT_RUN（理由つき）。107 行目の記述を、確かめた結果に合わせて直す
- 注意: 本人の資格情報は Sandbox の中だけで使い、Sandbox を閉じて破棄する

### RR-05 Narrator（所有者）

- 出典: 第2ラウンドの記録 95 行目、107 行目
- 手順（所有者向けのチェックリストを記録に置く）: 結果画面のコスト表示、「ログを開く」「保存先を開く」、設定画面の各分類で、Narrator の読み上げに、ボタンの名前、状態（有効／無効）、コストの値が含まれるかを聞いて判断する
- DoD: 所有者が各項目を PASS／FAIL で記録する
- 閉じ方: 所有者が実施しない場合は NOT_RUN のまま。エージェントは UIA の Name の値を代わりに PASS とはしない

### RR-06 T39-N2 native の再観測

- 出典:
  - `artifacts/test/ui-settings/t39/native-final-attempt.json`（0.8.4 の EXE、`4D80FA24…E507`）: 作業画面は全部で 4 つ（Input／Design／Execution／Results）。1180×800 では Input が `CONTROL_ID_PREDICATE_NOT_UNIQUE` で FAIL、残りの 3 画面は NOT_RUN。1024×720 では 4 画面すべて NOT_RUN。設定 5 分類（Common／Mapping／Evaluation／Special／ImportedPrompts）は両方の大きさで NOT_RUN。キーボード（Tab／Shift+Tab／Enter）も NOT_RUN。`humanReview` は `HUMAN_PENDING`
  - [ui-layout-contract.md](../dev/docs/ui-layout-contract.md) の 167 行目: 3 回試行した後は再試行を停止する
  - T39 の定義: [20260907-ui-settings-redesign-plan-v2.md](../dev/docs/archive/work/20260907-ui-settings-redesign-plan-v2.md) の 403 行目、§10.2（463〜479 行目。760×600 の単体表示と native DPI を含む）
- 前提（ゲート）: 0.8.4 での 3 回の試行は終わっており、167 行目は再試行の停止を明確に定めている。版を変えて名前を「T39-N2」にしても、前回止まった条件（特定の条件が 1 件に決まらない）を避けるための再試行であることに変わりはない。したがって **所有者の明示的な承認（または ui-layout-contract の改訂）がない限り RR-06 と RR-07 は実施せず、T39 は現状（FAIL で停止）のまま**にする。これまでの包括承認は「リソースの仕様」の承認で、この規則の例外の承認とは扱わない
- 対象の EXE: CI の artifact `windows-singlefile-development-host-35925523213`（`e152764`、期限 2026-10-07T22:18:50Z）を `gh run download` で取得する。RR-00 で新しい CI が成功した場合は、その run の artifact を使う
- 手順:
  1. EXE の SHA-256 を記録する
  2. UIA で要素を特定する条件を `AutomationId` と `ControlType` の組にして、前回の `CONTROL_ID_PREDICATE_NOT_UNIQUE` を避ける（条件が 1 件に決まらない場合は、その要素の一覧を記録して FAIL とする。条件をゆるめて通さない）
  3. 4 画面 + 設定 5 分類を 1180×800 と 1024×720 DIP で表示し、はみ出し・切れ・重なりがないかを ui-layout-contract の条件で判定する
  4. キーボードで Tab／Shift+Tab／Enter の移動を確かめる
  5. 結果を `artifacts/test/ui-settings/t39/` に新しい JSON（例 `native-n2.json`）として保存する（前回の JSON は上書きしない）
- DoD: 全項目が PASS／FAIL で判定され、NOT_RUN が理由つきのものだけになる。承認がない場合は「承認なしのため実施せず、T39 は FAIL で停止のまま」と記録されている
- 試行の上限: 承認された場合も 3 回（167 行目と同じ）
- 閉じ方: 3 回で終わらない場合は、その時点の結果で閉じる
- 状態が変わったときに合わせる文書: ui-layout-contract.md（11・107・110・146・167・169 行目）、[implementation-status.md](../dev/docs/implementation-status.md)（26〜30 行目）、SystemTest-prompt.md（222・796・1598 行目）、[requirements-definition.md](../docs/requirements-definition.md)（7・1011・1040 行目）、traceability、readme-claim-ledger、architecture、detailed-design、CHANGELOG の Unreleased の要約（7 行目「追加の4画面実機確認は…停止（FAIL）」）

### RR-07 T39 隔離ユーザーでの native の設定保存

- 出典: `native-final-attempt.json` の settingsSave = `BLOCKED_REQUIRES_ISOLATED_USER`
- 方法: Windows Sandbox のユーザー `WDAGUtilityAccount` は別のプロファイルなので、利用者の `setting.txt` に触れずに保存を試せる（RT-09 で Sandbox が使えることは確認済み）。ネットワークは無効でよい（保存は AI を呼ばない）
- 手順: RR-06 と同じ EXE を Sandbox で起動し、設定を変えて保存し、Sandbox の中の `setting.txt` が作られ、再起動後に値が戻ることを確かめる。ホストの `setting.txt` の SHA が変わっていないことも確かめる
- DoD: 保存と再読み込みが PASS／FAIL で判定される
- 閉じ方: Sandbox で UIA を動かせない場合は、手で操作した結果を記録する。それもできない場合は BLOCKED のまま

### RR-08 T39 本人の walkthrough（所有者）

- 出典: 同じ計画の §10.4（495〜505 行目）の 4 項目: 入力と 30/10 の配点で合計 100、設定を保存して戻る、再起動して明示的に適用、上書きが未保存と分かる
- 手順: 所有者向けのチェックリストを記録に置き、所有者が PASS／FAIL を書く
- DoD: 4 項目のすべてに、所有者が PASS／FAIL を記録している
- 閉じ方: 所有者が実施しない場合は NOT_RUN のまま

### RR-09 リリース（所有者の指示が必要）

- 出典: [release.yml](../.github/workflows/release.yml)（入力は tag。HEAD を指す annotated tag を検査し、draft を作る）、[publish-release.yml](../.github/workflows/publish-release.yml)（入力は tag、candidate_run_id、clean_host_evidence_json。environment `publish` の承認が必要）、SystemTest-prompt.md の 1422 行目と ST-UC-26（CH-01〜06）
- 既存の tag: `v0.8.1`（Release あり、最新）、`v1.0.0`（annotated、commit `b68e757`、Release なし）
- 手順（所有者が指示した場合だけ実施する）:
  1. RT-16 の版の判断（`0.8.6` のままか、`0.9.0` などに上げるか。`v1.0.0` の tag より小さい版でよいか）
  2. RR-11 と RR-10 の判断を先に済ませる（到達点を B／C にした場合は、RR-10 の作業が終わるまでリリースしない）
  3. CHANGELOG の Unreleased を日付付きの節にする
  4. annotated tag を HEAD に作り、release.yml を実行して draft と 4 つの asset を作る
  5. clean-host CH-01〜06: fresh な Windows 11 x64 の標準ユーザー。ブラウザーでダウンロードして MOTW を付け、CH-06 は本人の login が必要。このホストの Hyper-V は使えず（非管理者で `Get-VM` が拒否される）、Sandbox は管理者ユーザーで Insider build なので、使えるのは Azure の Windows 11 VM か物理 PC（費用と作成は所有者の承認の後）
  6. publish-release.yml を実行し、environment `publish` の承認を所有者が行う
- DoD: 公開された Release、または所有者の「公開しない」という判断の記録
- 閉じ方: 所有者の指示がない場合は、この計画でも**実施しない**（RT-21 と同じ）

### RR-10 RT-18 項目 3・6・7 と RT-19 の到達点 B／C

- 条件: 所有者が MSIX の到達点を B／C にすると決めた場合だけ。要求定義（17・713・757・874 行目）は development MSIX を拡張せず、本体を upload しないと定めているので、決めない限り何もしない（到達点 A のまま）
- 決めた場合の手順（前回の計画の 294〜302 行目の RT-19 の手順）:
  1. 所有者が項目 3（発行者・証明書・署名サービス）、6（版・更新・設定の移行・削除時の扱い）、7（承認者・許可する ref・clean-host の担当と環境）を決める
  2. 要求定義の該当の条文（757 行目など）を所有者の判断に合わせて改訂する
  3. 到達点 B 以上: MSIX 専用の `workflow_dispatch` を作る
  4. 開発用と本番用を区別するよう契約テストを直す（テストは消さない）
  5. 到達点 C: 署名と clean Windows 11 での検証（D2 の §11 V01〜V14）
- DoD: 到達点の判断と項目 3・6・7 の値が記録されている。B／C の場合は、選んだ到達点までの手順が測った結果で満たされている（前回の計画の 302 行目）
- 順番: RR-09 より前に行う

### RR-11 RT-06 の再確認

> **削除（2026-09-24 20:40、所有者の指示）**。§8 を参照

- 手順: リリースの直前に W1〜W4 の公式資料をもう一度取得し、`TotalNanoAiu` を AI クレジットに換算する規則が書かれたかを確かめる。出典の URL と取得日を記録する
- DoD: 規則があれば出典つきで表示の実装を計画する。なければ BLOCKED のままにして、確認日を記録する

### RR-12 SampleWorkbook

- 必要なもの: profile と同じファイル（469,976 bytes、`A1:J531`）を所有者が用意する、または不一致を受け入れる判断
- DoD: 用意されたファイルで SampleWorkbook のテストが成功する、または所有者が不一致を受け入れたことが記録されている
- 閉じ方: どちらもない場合は、環境の問題としての記録のまま。サンプルも期待値も変えない

---

## 4. 進め方と並列化

GUI／UIA の操作は SendInput が前面の window を必要とするので、**ホストでは 1 つずつ**行う。Sandbox も同時に 1 つだけ起動する。

| フェーズ | タスク | 並列 | 完了の条件 |
|---|---|---|---|
| P0 | RR-00（CI の再実行を待つ間）と RR-01（ホストの CLI。GUI を使わない） | **並列** | RR-00 と RR-01 の DoD |
| P1 | ホストの GUI: RR-02 → RR-03 方法1 →（承認がある場合）RR-06 | 順番 | 各 DoD |
| P2 | Sandbox: （承認がある場合）RR-07（ネットワーク無効、1 回目の Sandbox）→ 2 回目の Sandbox（ネットワーク有効）で RR-04 手順1（login）→ RR-03 方法2 → RR-04 手順2（shutdown。これで Sandbox は消える） | 順番。P1 と同時には行わない | 各 DoD |
| P3 | 人の作業: RR-05、RR-08（所有者にチェックリストを渡す） | 別のマシンで行う場合だけ P1・P2 と並列。同じホストでは前面の window を奪い合うので、P1・P2 の後に順番に行う | 所有者の記録 |
| P4 | 所有者の指示がある場合: RR-11 → RR-10（判断と、B／C の場合の作業）→ RR-09 | 順番 | 各 DoD |
| 終了 | 記録、文書、CHANGELOG、commit／push／CI | — | 下記 |

各タスクの後に敵対的レビュー（rubber-duck）を 1 回行い、指摘を反映したことを確かめてから次に進む。レビューで存在しない問題を指摘された場合は、根拠がないことを記録して反映しない。

### 終了のときの作業

1. 第3ラウンドの記録を `work/{yyyymmdd-hhmm}-RemainTaskExecutionRecord3.md` に書く（証跡の SHA-256、run ID、NOT_RUN の理由）
2. 状態が変わった文書だけを直す（RR-06 の一覧、RR-01 の SystemTest-prompt、RR-04 の 107 行目など）
3. `dotnet test tests\StudyReportEvaluator.App.Tests -c Release --filter FullyQualifiedName~DocumentationContractTests` で文書の契約テストを実行する
4. CHANGELOG の Unreleased に、利用者に影響する変更だけを Keep a Changelog の形で追記する（CHANGELOG の 3 行目は、利用者に影響する変更を記録すると定めている。作業記録だけの場合は追記しない）
5. パッケージの版: `hve`、Markdown query Skill、Code Query skill、Autonomous Task Graph はこのリポジトリにないので、版を上げるものはない
6. commit のメッセージはファイルに書いて `git commit -F` で渡し、trailer `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>` を付ける。push して CI（Windows job は約 40 分）の成功を確かめる

---

## 5. 守ること

- 捏造をしない。観測していないものを PASS と書かない。出典のない主張をしない
- テストを弱めない。skip や条件の緩和で通さない
- 実 AI を使う実行の前後で、利用者の `setting.txt` を退避・復元し、SHA-256（`4C9785CB47280C4E546C518964E85F43CF1F0F4C9069F93D30F58F7C34728F3A`）で確かめる。ジョブログと一時フォルダーを削除する
- 入力は合成データだけ。資格情報を読み出したり、ファイルやログに書いたりしない
- 自分が起動したプロセスだけを PID で止める
- オーバーエンジニアリングをしない: CI の診断のための upload の追加、テスト用の CLI 解決の仕組み、retry を起こすための製品のフックなど、要求にないものは加えない
- watchdog: 3 分以上進みがなければ状況を報告する。10 分以上実質的に進まなければ止めて、理由・最後に成功した処理・疑わしい箇所・次に見るログを報告する

---

## 6. リスクと未確定事項

| 項目 | 内容 | 対応 |
|---|---|---|
| RR-00 の原因 | flaky か実際の不具合か未確定。生の TRX は CI に残らない | 再実行 → ローカル再現の順で確かめる |
| Sandbox での login | 未検証（第2ラウンドの記録 107 行目の記述も未検証） | RR-04 手順 1 で最初に確かめ、結果に合わせて記述を直す |
| T39 の 3 回の規則 | 167 行目は再試行の停止を定めている | 所有者の明示的な承認（または契約の改訂）がない限り RR-06／RR-07 を実施しない |
| CI の artifact の期限 | `windows-singlefile-development-host-35925523213` は 2026-10-07T22:18:50Z に消える | RR-06 をそれより前に行うか、新しい CI の artifact を使う |
| 版と tag | `v1.0.0` の tag があり、`0.8.6` や `0.9.0` はそれより小さい | RR-09 の手順 1 で所有者が判断する |
| Azure VM | 費用と承認が必要 | 所有者の承認の後にだけ作る |
| SDK 例外の再現 | 子プロセスを止めたときのアプリの挙動は未検証 | 観測したとおりに記録する |

---

## 7. 敵対的レビューの反映（2026-09-24）

rubber-duck によるレビューの指摘 10 件を、引用元を確かめたうえで反映した。

| 指摘 | 反映 |
|---|---|
| RR-06 が 3 回で停止する規則（ui-layout-contract.md 167 行目）を名前の変更で回避している | 所有者の明示的な承認（または契約の改訂）を前提にし、承認がなければ実施しない |
| RR-10 がリリースの後に置かれ、B／C の作業がない | RR-10 に RT-19 の手順を入れ、RR-09 より前に置いた |
| RR-01 のコマンドが lock ファイルを書き換え、環境の失敗を誤分類する | detached worktree で実行し、結果を 4 つに分類する |
| Sandbox の shutdown で login が消えた後に RR-03 方法2 を置いていた | RR-03 方法2 を shutdown の前にした |
| RR-04 で破棄されるジョブログを確認しようとしていた | DoD から外した |
| RR-08／RR-10／RR-12 に DoD がない | 追加した |
| 人の作業を同じホストで並列にしていた | 別のマシンの場合だけ並列にした |
| 行番号の誤り（6 か所） | 直した |
| T39 の画面の数の書き方 | 4 画面のうち 1180×800 の Input が FAIL、ほかは NOT_RUN と書き直した |
| 作業ツリーが clean という記述 | 計画ファイルの作成前と限定した |

## 8. 所有者の指示による変更（2026-09-24 20:40）

- RR-01、RR-04、RR-11 をタスクから削除した。第3ラウンドの記録にある結果（BLOCKED／NOT_RUN）は、その時点の観測として残す
- RR-03 は、根本原因を調べて解決策を立て、PASS するまで実行する。根本原因・解決策（方法 3: ローカルの proxy）・実行の記録は [20260924-2045-RR03RetryRootCause.md](20260924-2045-RR03RetryRootCause.md) に書く。RR-04 の削除により、RR-03 方法 2（RR-04 の Sandbox を使う）も行わない

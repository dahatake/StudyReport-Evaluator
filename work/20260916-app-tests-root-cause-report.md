# App.Tests 失敗の根本原因と修正記録

## 最終結果（2026-09-16 15:17:35 JST）

最終コードで除外フィルターなしのApp.Tests実行が成功。単一TRXで1789件中1781 Passed、0 Failed、8 Skipped（実行1781件、error／timeout／abortedすべて0）を確認した。開始14:49:24、終了15:17:35、CLI報告28.1934分。

元の23失敗と、後続で検出したサンプル契約・C03 timeout・ZIP文書リンクの失敗は今回の実行で解消した。8件の単一EXE実成果物opt-in試験、実AI・実ログイン・外部Excel・clean-hostの成功を意味しない。独立静的レビューの別途検討事項は未対応として以下に残す。

## 現在の実行計画（2026-09-16追補）

利用者から「固定ハッシュでの一致は無視」「全件解決に向け計画して実行」の指示を受けた。構造差分の扱いを確認したところ不在のため自律的な判断を求められ、元ファイルを変更せず現在sampleを再profileする方針とした。

1. [x] 旧実行を中止し、残留した当該testhost子プロセスによるDLLロックを解除。
2. [x] 過去の固定SHA／bytes／sheet name hashを非ゲート化。現在入力の前後SHA／size／last-write一致は維持。
3. [x] 構造を本文非記録で再profile。469976 bytes、13パーツ、9リレーション、外部0、A1:J531、531行10列。旧11/8は履歴値のまま保持。
4. [x] 現行列候補D〜I、主回答D/E/G/H、補助E→F・H→I、4問各10点を実読み取りで検証。旧12列syntheticは変更しない。
5. [x] 関連文書・任意technical E2Eの旧5問前提を更新。Live AI等のopt-inは無効のまま。
6. [x] App.Tests全体の終了TRXを確認。full-app-finalは1789件中1778 Passed、3 Failed、8 Skipped。大型durable E2Eは20分21秒でPassed。失敗はC03 timeout2件とZIP内READMEリンク切れ1件。
7. [x] C03を既存排他コレクションに移し、READMEの非同梱開発ファイルリンク11箇所をGitHubへ変更。再検証46件すべてPassed。90秒制限・二重builderの明示的並行テストは維持。
8. [x] Core.Tests190件Passed、version tests15 assertions Passed、編集ファイル診断エラーなし、git diff --check成功。
9. [x] 最終コードで除外フィルターなしのApp.Tests全体再実行（acceptance）の単一TRXを確認。1781 Passed、0 Failed、8 Skipped。

### 最終段階の検証記録

| 実行 | 結果／証拠 |
|---|---|
| 全体1回目（固定ハッシュ方針変更後） | `TestResults/app-fixes-20260916/full-app-final/dahatake_DAHATAKE-OFFICE_2026-09-16_14_05_13_net10.0.trx`、1789件／1778 Passed／3 Failed／8 Skipped、33.7414分 |
| 残る3失敗の修正後、領域全体再検証 | `TestResults/app-fixes-20260916/remaining-final/dahatake_DAHATAKE-OFFICE_2026-09-16_14_41_31_net10.0.trx`、46件すべてPassed、7.5246分。C03対象2ケース29秒・33秒、ZIPの展開・起動・リンク・再現性試験Passed |
| Core | `TestResults/app-fixes-20260916/core-final/dahatake_DAHATAKE-OFFICE_2026-09-16_14_34_15_net10.0.trx`、190件すべてPassed |
| 最終全体再実行 | `TestResults/app-fixes-20260916/acceptance/dahatake_DAHATAKE-OFFICE_2026-09-16_14_49_29_net10.0.trx`、1789件／1781 Passed／0 Failed／8 Skipped、28.1934分 |

全体1回目の8 Skippedは単一EXE実成果物のopt-in8件。外部smokeの一部は既存コードで環境変数無効時にreturnするため、TRXのPassed数が実AI・実Excel等の実行を意味するわけではない。

### 独立静的レビューの別途検討事項（今回の観測失敗とは別）

任意のRealDataSystemSmokeでは、証跡出力先と入力の衝突拒否、GUI子プロセスの利用者環境隔離、失敗時finallyでのプロセス回収を強化する余地が指摘された。C03のホスト検出も同期ReadToEnd後のタイムアウトという潜在的制約がある。今回これらの経路で失敗・副作用を観測したわけではなく、実データsmokeは無効のまま。既存テスト失敗の解消と未実施経路の安全性保証を混同しない。

### 現行sampleの検証証拠

- `TestResults/app-fixes-20260916/sample-profile/dahatake_DAHATAKE-OFFICE_2026-09-16_13_59_37_net10.0.trx`: 構造と候補を観測。旧列候補のassertでFailed（測定成功をテスト成功とは扱わない）。
- `TestResults/app-fixes-20260916/sample-final/dahatake_DAHATAKE-OFFICE_2026-09-16_14_03_43_net10.0.trx`: サンプル検査Passed、文書21件Passed、旧A1:J531禁止assert1件Failed。この禁止assertはその後、新旧profileを明示検査する契約に修正。
- `full-app/`は途中中止のため最終成功証拠ではない。
- C03 timeoutは`Final_rejects_caller_identity_different_from_the_candidate_and_preserves_output`のrepository/run2ケース。失敗位置はFinal拒否処理ではなく、その前のCandidate生成待機。排他化後の最終全体実行では両ケースが21秒／25秒でPassed。負荷競合は有力仮説であり、内部のCPU／I/O要因を計測して確定したものではない。

## 初回修正時点の判定（以下は追補前の履歴）

- 元の App.Tests TRX: 1776件、1752 Passed、23 Failed（executed 1775）。
- 修正後の対象別実行: 文書・文字コード25件、UI・設定・E2E297件、いずれも失敗0。
- フィルターなしの全体実行は継続中。追加で非公開ローカルサンプルの固定SHA-256不一致1件を検出した。**全件成功・作業完了とは判定しない。**
- 実AI・実ログイン・外部表計算ソフトの成功証拠ではない。CI=trueと既存の3つの外部smoke環境変数=0で実行した。

## 原因と解決方針

| 元の失敗 | 根本原因 | 実施した修正 |
|---|---|---|
| DocumentationContractTests: 3件 | SystemTest-prompt.mdをtests/へ移動した後の参照更新漏れ | テストの読み取りパス・claim文字列と文書リンクを現配置に修正。旧配置の重複を禁止する検査も維持 |
| WorkflowStateTests: 6件、MainWindowTests: 8件、SettingsWorkflowSystemTests: 5件 | 認証後のモデル一覧自動保存／キャッシュあり起動時の認証確認という現仕様に、旧テストの「ファイル不変・未作成」「起動時は常に未認証」という前提が追従していなかった | キャッシュだけの変更を厳密検査して認証後のバイト列を基準に採用。実行・遷移・編集での不変性は維持。再起動認証と明示確認の回数を分離し、起動時AI呼び出し0件を検証 |
| ReleaseMatrixBuilderTests: 1件 | 子PowerShellのローカライズされたエラー出力と親プロセスの厳密UTF-8デコーダーの不一致 | スクリプトの引数検証より前に子のConsole.OutputEncodingをUTF-8へ設定。パス・値はコードへ埋め込まず引数として渡す。日本語・引用符・引数検証エラーの回帰2件を追加 |

### 仕様を弱めないための検証

- `docs/settings.md`、`SettingsViewModel.PersistModelCatalogAsync`、`LoadCoreAsync`、既存の`ModelCatalogTests`を根拠とした。製品のモデルキャッシュ機能は撤去していない。
- 新しい`Settings/ModelCatalogPersistenceAssert.cs`は、カタログのID・順序・token上限を比較し、カタログ以外の全プロパティをJSON原文で比較する。未作成ファイルでは既定値＋カタログ以外を許可しない。
- 認証による保存を検査した後の実行・画面遷移では、従来どおりファイル全体のバイト一致を検証する。
- テストをskip化して元の失敗を隠す変更、固定サンプルのハッシュ書き換え、製品コードの変更は行っていない。

## 再現・再検証証拠

いずれもリポジトリ相対パス。TRXは実際のローカル実行出力である。

| 範囲 | TRX | 結果 |
|---|---|---|
| 修正前App.Tests | `TestResults/local-build-20260916/dahatake_DAHATAKE-OFFICE_2026-09-16_12_41_12_net10.0.trx` | 23 Failed |
| 文書検査全体＋元の包装テスト失敗＋日本語回帰2件 | `TestResults/app-fixes-20260916/focused-docs-encoding/dahatake_DAHATAKE-OFFICE_2026-09-16_13_28_38_net10.0.trx` | 25 Passed、0 Failed |
| WorkflowState/MainWindow/SettingsWorkflowSystem/ModelCatalog/SettingsViewModel/SettingsView | `TestResults/app-fixes-20260916/focused-settings/dahatake_DAHATAKE-OFFICE_2026-09-16_13_30_37_net10.0.trx` | 297 Passed、0 Failed、0 Skipped |
| フィルターなしApp.Tests | `TestResults/app-fixes-20260916/full-app/` | 実行中。完了件数未確定 |

環境: Windows、PowerShell Core 7.6.6、Release/net10.0。ビルドと上記対象別テストは成功。変更テストのエディター診断はエラーなし。

## 全体再実行で追加検出した環境依存の失敗

`SampleWorkbookStructuralTests.Repository_sample_is_opened_read_only_and_only_structural_metadata_drives_mapping`の34行目が失敗した。

- 期待: `73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA`
- 実際のテスト出力の先頭: `90376F54BAE9A34E89704FA56F9022F4E67DD2CF80D9812827…`（出力が省略されているため完全値はここでは推測しない）
- `before.Sha256`の検査であり、メタデータ読み取り前に失敗している。テストの読み取り処理がファイルを変更したことを示す失敗ではない。
- `sample/SampleReport.xlsx`は非公開・非追跡のローカル前提資料。`.github/workflows/ci.yml`のdeterministic testsはこのクラスを明示的に除外している。環境変数CI=trueだけではこのFactを除外しない。
- `dev/docs/preflight/sample-workbook-profile.md`のDrift ruleは、SHA変更時の黙認・単純な期待値更新を禁止する。ファイル変更の理由は未確認。

解消には、既知の正本を復元するか、変更理由を確認したうえでread-only構造再profile、mapping/privacyのレビュー、関連仕様・試験の正式な改版が必要。今回の修正ではサンプルも固定ハッシュも変更していない。

## 長時間実行について

前回の長時間実行をハングとは断定しない。`Fixed_seed_531_row_durable_new_and_resume_matches_uninterrupted_run`は実チェックポイント保存を使う大型synthetic E2Eである。

- 通常実行532回、中断267回、再開265回のUpdateを明示検証する（計1064回）。
- 530データ行について通常結果・再開結果のハッシュ、数式、入力不変、実ファイル検証を行う。
- AI境界はfakeであり、実AI待機とは異なる。
- I/O主体の重い処理である可能性はあるが、プロファイリング未実施のためCPU/I/O等の時間内訳や性能劣化原因は未確定。安全性の検証回数を減らす修正は行わない。
# README 正式公開向け再構成 — 詳細実装計画

| 項目 | 内容 |
|---|---|
| 対象 | `/README.md` |
| 作成日 | 2026-09-02 |
| 主読者 | 教師・講師・採点担当者、評価Promptを設計する担当者、利用者として導入・運用するソフトウェアエンジニア |
| 成果物 | 正式公開用README、同期済み利用者文書、現行UIの合成スクリーンショット、文書契約テスト |
| 方針 | 実装済みかつ検証可能な事実だけを記載し、計画・進捗・commit・gate・test件数などの開発途中情報はREADMEへ載せない |
| 状態 | 実行中。README/docs/images/package/testは実装済み。B-05の外部ブロッカーにより正式公開gateは未完了 |

本計画内の現状説明には、末尾の[出典台帳](#12-出典台帳)に対応する`[Sxx]`を付ける。編集方針、作業手順、受入基準は本依頼から定める実装判断であり、製品仕様の事実とは区別する。

### 実行状況（2026-09-02）

| ID | Status | 実測結果 |
|---|---|---|
| B-01 | RESOLVED | SDK固定CLI 1.0.79、manifest、RID、SHA-256をWindows ZIPへ同梱。publish/package/extract/default resolver/clean launch/repackage test PASS |
| B-02 | RESOLVED | 要求v4.1とADR-0013で初版正式公開をWindows 11 x64へ限定し、文書契約PASS |
| B-03 | RESOLVED | production UIから7画像を再生成。全て1440×1050、synthetic/fake境界をmanifest/captionへ記載し、敵対review finding 0 |
| B-04 | RESOLVED | `docs/`の利用者文書をv4.1へ同期。relative link欠落0、warning完全一致、敵対review finding 0 |
| B-05 | BLOCKED_EXTERNAL | public GitHub release API確認時にrelease 0件。正規asset URLを推測せず、required gate失敗中のpackageを公開しない |
| B-06 | RESOLVED | production UIから`U-03`/`U-04`を除去。全4step UI test PASS、敵対review finding 0 |
| B-07 | RESOLVED | ユーザー配置の`sample/SampleReport.xlsx`をread-only再profile。469,995 bytes、SHA-256 `F7C536...A2E3D`、`Sheet2!A1:J531`、direct test 1/1 PASS、入力identity不変、敵対review finding 0 |
| B-08 | RESOLVED | performance testの重複reopen validationを除きproductionの一回validationへ一致。最新3回中央値4.074791秒 ≤ 30秒 |

| Task | Status | 実行結果 |
|---|---|---|
| RD-00 Release contract | PARTIAL_BLOCKED | B-01〜B-04/B-06〜B-08完了、B-05のみ外部blocked |
| RD-01 Claim ledger | COMPLETE | `dev/docs/readme-claim-ledger.md`作成、32 claim、relative link欠落0、敵対review finding 0 |
| RD-02 User docs sync | COMPLETE | 利用者文書7件をcurrent contractへ同期、敵対review finding 0 |
| RD-03 UI screenshots | COMPLETE | generatorをdurable v4.1 fixtureへ更新、7画像再生成・目視・review完了 |
| RD-04 README rewrite | COMPLETE_CONTENT | 正式READMEへ全面置換。B-05の実在download URLだけ外部blocked |
| RD-05 Contract tests | COMPLETE | 正式README用13件へ再構成し13/13 PASS、敵対review finding 0 |
| RD-06 Release validation | PARTIAL_BLOCKED | B-07 direct sample testは1/1 PASS。B-07-only依頼のためfull aggregateは未再実行。B-05は継続blocked |
| RD-07 Independent review | COMPLETE_WITH_EXTERNAL_BLOCKERS | 3観点の最終並列reviewを実施。再現した全7画像package assertion不足を修正し、文書＋package契約16/16再PASS。古い性能/UI指摘は一次ソースで却下 |

## 1. 目的と完了像

正式公開後のREADMEを、次の役割に限定する。

1. 初見の利用者が、このアプリで何ができるか、何ができないかを短時間で判断できる。
2. 対応環境、必要なaccount/runtime、入力形式、AIへ送る情報を実行前に確認できる。
3. 入手、検証、起動、4ステップ操作、出力確認、再開まで迷わず進める。
4. AI定量値を最終評点と誤認せず、空回答と技術的失敗の違いを理解できる。
5. 詳細説明が必要な場合だけ、同期済みの利用者文書へ移動できる。
6. ソフトウェアエンジニアも「利用者」として、`--input`／`--prompt`による事前入力やCustom Promptを利用できる。build、test、ADR、traceabilityはREADMEの主導線から外す。

## 2. 捏造防止と情報採用ルール

### 2.1 情報源の優先順位

| 優先度 | 情報源 | READMEでの扱い |
|---:|---|---|
| 1 | 現在UIへ接続されているproduction source | 現行動作の第一根拠とする。[S01][S02][S03][S04] |
| 2 | production動作を直接検証する決定的test | 実装の裏付けとする。[S05][S06][S07][S08][S09][S10] |
| 3 | 実際のpublish/package scriptとpackage test | 対応OS、同梱物、署名、runtime前提の根拠とする。[S11][S12][S13] |
| 4 | 承認済み要求 | 目標仕様の確認に使う。ただし実装・配布証跡が一致しない項目はREADMEへ採用しない。[S14][S15] |
| 5 | 既存の利用者文書とスクリーンショット | 文案候補としてのみ使う。コードと矛盾する箇所は先に更新する。[S16][S17][S18][S19][S20][S21][S22][S23] |

### 2.2 claim ledger

READMEへ入れる各事実を、実装時に`dev/docs/readme-claim-ledger.md`へ新規作成するclaim ledgerへ登録する。このledgerは監査用であり、正式READMEや配布ZIPからはリンクしない。

| Claim ID | README記述 | Production source | Direct test / package evidence | 状態 |
|---|---|---|---|---|
| C-001 | 例: native pickerとpath入力を利用できる | `InputView.axaml.cs` | `InputViewTests` | VERIFIED / BLOCKED / EXCLUDED |

運用規則:

- `VERIFIED`だけを断定形で記載する。
- `BLOCKED`はREADME本文へ書かず、公開前課題へ残す。
- `EXCLUDED`は要求書にあっても未実装・未検証なら記載しない。
- 外部serviceの料金、model、login手順、対応planなど時間変化する情報は、公開直前に公式一次資料を再確認し、URLと確認日をclaim ledgerへ記録する。
- 仮定、感度分析、将来予定、未実行smokeの結果を「現在の機能」へ変換しない。

## 3. 現状監査と公開前ブロッカー

### 3.1 必須ブロッカー

| ID | 現状 | READMEへの影響 | 解消条件 |
|---|---|---|---|
| B-01 | 既定`CopilotClientFactory`はapp directoryの`copilot-runtime.json`と同梱CLIだけを解決し、manifestがなければ`CliUnavailable`となる。一方、Windows publish/packageはCLI downloadをskipし、`copilot.exe`がないことを検証している。[S24][S37][S11][S12][S13] | 現行ZIPでAI評価を利用できるという導入手順は書けない。既存文書の「PATH上のCLIを使う」はproduction配線と一致しない。[S16][S17][S21] | **推奨:** 承認済みv4契約どおり互換CLIとmanifestをpackageへ同梱し、hash検証とpackage起動testを通す。[S14] 別案としてPATH探索を正式仕様にする場合は、resolver実装・test・要求変更を先に完了する。どちらの場合も実物packageで認証状態確認を再実行する。 |
| B-02 | 要求書はmacOS arm64/x64を対象に含むが、現存する配布scriptとpackage testはWindows 11 x64だけで、`scripts/`にmacOS用成果物生成scriptはない。[S14][S11][S12][S13] | macOS対応をREADMEで断定できない。 | Windows-only正式版として公開するなら、READMEではWindows 11 x64だけを対応環境とし、要求・release scopeも同期する。macOSも同時公開するなら、実package・署名・notarization・macOS実機/runner証跡が揃うまでREADME公開を保留する。 |
| B-03 | 既存画像は旧UIを表示し、native picker、output/resume、v4配点、最新warningを反映していない。画像manifestも旧200-unit画面を説明している。[S18][S23][S01][S02] | READMEに貼ると現行操作を誤案内する。 | production XAML/ViewModelから全画像を再生成し、入力・設計・実行・結果の主要操作が現行画面と一致することを目視確認する。 |
| B-04 | 利用者文書同士でv3/v4、native picker、手動/自動出力、checkpoint、CLI入手方法が矛盾する。[S16][S17][S19][S20][S21][S22] | READMEから詳細文書へ移動した利用者が異なる操作を案内される。 | READMEと同じclaim ledgerで`docs/`を同期するか、同期できない文書へのリンクを正式READMEから外す。packageは`docs/`全体を同梱するため、リンクを外すだけでなくpackage内文書も更新する。[S12][S13] |
| B-05 | 現READMEに正式なrelease asset URLはなく、ZIPはrepository-local generated artifactとして説明されている。[S25] | 初見利用者が正規配布物を入手できない。推測URLは記載できない。 | 公開するrelease assetの実在URL、ファイル名、SHA-256 sidecarを確認後にだけ「入手」節へ記載する。release未作成ならREADMEにplaceholderを残さない。 |
| B-06 | `MainWindowViewModel`と`MainWindow.axaml`には`U-03`／`U-04`等の開発task文言が残る。[S26] | 表示経路によっては正式画面に開発途中情報が現れる。 | 実画面で到達可能性を確認し、表示される文言は利用者向け表現へ置換してから画像を再生成する。非表示ならUI testで非表示を固定する。 |
| B-07 | **RESOLVED 2026-09-02:** ユーザーが`sample/SampleReport.xlsx`を配置。production readerで本文非表示のread-only再profileを行い、469,995 bytes、SHA-256 `F7C5364449B1026F2725828F47418B8E105D7E50CF4DF0B224FE4EAF134A2E3D`、13 parts、9 relationships、`Sheet2!A1:J531`を確認した。[S38] | sample構造と入力不変のdirect testを再実行できる。 | direct test 1/1 PASS、検査前後のSHA-256/size/mtime一致、敵対review finding 0。 |
| B-08 | 2026-09-02のPowerShell 7によるsample非依存test runで、Windows 531行read/write/final validationの3回中央値は31.238620秒となり、testが定める30秒基準を超過した。[S39][S40] | full required testがPASSせず、READMEで性能値や性能保証を公開できない。 | 同一release候補を制御したWindows 11 x64環境で再測定し、system loadと実装regressionを切り分ける。基準を結果に合わせて緩和せず、改善後に3回中央値が既定基準内となることを確認する。 |

### 3.2 README単独で修正可能な問題

| 現行READMEの箇所 | 問題 | 処置 |
|---|---|---|
| 冒頭のGATE-ACCEPTANCE banner [S25] | commit、HEAD、test件数、gap closureを利用者へ提示している | 全削除 |
| 「読者別の入口」[S25] | QA、要求所有者、開発者の導線が主導線へ混在 | 教師、評価設計者、運用者、ソフトウェアエンジニア利用者の4導線へ再構成 |
| 「対応範囲」[S25] | v3とv4の前提が混在 | package実物とclaim ledgerに基づき再作成 |
| 「文書」[S25] | requirements、architecture、traceabilityを前面に出す | 利用者文書だけを本文へ残し、開発文書は末尾の小さな保守リンクへ移すかREADMEから外す |
| 「教育倫理上の注意」[S25] | 趣旨は必要だが、周辺説明が内部gate用語を含む | application resourceと完全一致するwarning本文を残し、利用判断の短い説明へ簡素化。[S27] |
| 「4ステップの操作」[S25] | v3出力フローとv4自動finalizationが混在 | 現行durable workflowへ更新。[S02][S05][S06] |
| 「画面プレビュー」[S25] | 画像が旧UI | B-03解消後の画像へ差替え |
| 「100名×2設問のトークン計画値」[S25] | 仮定値、commit、内部admission説明が長く、正式READMEのquick-startを阻害 | READMEから全削除。必要性が別途承認された場合だけ、実測と公式料金出典を持つ独立文書を新設 |
| 「unsigned ZIPから実行」[S25] | repository生成者向けpathと一般利用者向け導入が混在 | 正式release assetの「入手・hash確認・展開・起動」へ書換え。署名状態は実package証跡に合わせる。[S12][S13] |
| 「sourceからbuild/test/package」[S25] | 開発・保守手順 | READMEから削除し、必要なら`dev/docs/README.md`への1リンクだけを末尾に置く |
| 「出力workbook」[S25] | v3の3 sheet説明 | v4のfinal 4 sheet、partial checkpoint、自動finalization、別名override出力へ更新。[S02][S04][S05][S28] |
| 「privacy/security」[S25] | 有用だが実装詳細が過多 | 実行前に必要な情報だけへ短縮し、詳細文書へリンク。[S29][S30][S20] |
| 「検証済み証跡」[S25] | gate、commit、test、optional smokeが主内容 | 全削除 |
| 「run開始前の技術検証」[S25] | gap IDとclosure履歴を提示 | 全削除。利用者が対処可能なエラーだけトラブルシューティングへ残す |
| 「制限と非保証」[S25] | native picker等に古い記述がある | 現在の対応環境・形式・再import非対応・AI非保証だけへ再構成 |

## 4. 正式READMEの章立て

READMEは次の順序とし、最初の画面内で「用途」「対応環境」「AIの注意」を把握できる構成にする。

### 4.1 タイトルと一文要約

記載内容:

- 製品名は`StudyReport Evaluator`。
- 標準`.xlsx`の回答をローカルで読み込み、GitHub Copilotを使って定量化候補を作り、入力を変更せず別の`.xlsx`へ出力するデスクトップアプリ、と説明する。[S01][S02][S05][S14]
- 「自動採点」「公平性保証」「不正検知」とは表現しない。類似度は不正行為の判定ではない。[S14]

### 4.2 重要な注意

- `EthicsWarningText.Message`と完全一致する日本語warningを引用する。[S27]
- AIの値は確認対象であり、最終的な評点と利用判断は利用者が行うと明記する。[S14]
- checkbox、同意gate等の内部仕様説明はREADMEへ載せない。

### 4.3 できること

最大6項目に絞る。

1. native pickerまたはfull pathから標準`.xlsx`をread-onlyで読み込む。[S01][S09]
2. 質問、主回答列、補助列、Knowledge／Custom evaluator、固有評価を設定する。[S01][S14]
3. Base／Question／Special配点と類似度減点係数を設定する。[S31][S32]
4. 参照回答、通常評価、固有評価、類似度を実行し、学生行単位でcheckpointする。[S02][S05][S06]
5. 全処理の完了時にfinal workbookを自動生成し、cancel時はpartial checkpointを保持して再開できる。[S02][S05][S06]
6. raw scoreを確認し、必要ならoverride反映版を別名で出力する。[S03][S04][S10]

### 4.4 対応環境と前提

表形式で次を示す。

- 対応OS/architecture: B-02で確定したrelease実物だけを記載する。
- 配布形態: self-containedか、署名済みか、installerかZIPかをpackage test結果から記載する。[S11][S12][S13]
- 入力: 標準`.xlsx`のみ。`.xls`、`.xlsb`、CSV、PDF、macro-enabled、暗号化・権利保護fileは対象外。[S33]
- spreadsheet runtime: packageの実行にExcel/Office/LibreOfficeを要求しない。[S11][S13]
- AI前提: B-01解消後の実装とpackageに一致するCLI/login条件だけを記載する。[S24]
- GitHub Copilot account/plan等の外部条件は、公開直前に公式一次資料を再確認してリンクする。

### 4.5 入手・整合性確認・起動

1. 正式release assetのURLを示す。B-05未解消なら本節を完成扱いにしない。
2. asset名とsidecar名を実物と一致させる。[S12][S13]
3. hash照合は、一般利用者がdownload directoryで実行できる手順として記載する。repositoryの`artifacts/`相対pathを使わない。
4. unsignedならその事実とWindows warningの意味を明記し、署名済みと誤認させない。[S12][S13]
5. 展開後の実行file名を実物packageから確認して記載する。[S13]
6. B-01で確定したCopilot login準備を記載する。
7. source build手順は本節へ混ぜない。

### 4.6 5分クイックスタート

現行4-step UIに合わせる。[S01][S02][S03]

1. **入力** — fileを選択し、sheet、質問文行、回答行、主回答/補助列を確認する。
2. **定量化設計** — Base/Special/Question points、Similarity penalty、Knowledge/Custom、固有評価、Promptを設定し、技術検証を通す。
3. **実行** — Copilot状態、model、並列度、新規run/再開、出力directoryを確認して明示的に開始する。
4. **結果・出力** — final/partial path、行別配点・減点・FinalScore、statusを確認し、必要ならoverride版を新しい名前で出力する。

この節には現行画像を2枚まで掲載する。候補は「入力」と「結果」。各captionで合成データ、fake auth/resultであることを明記し、live AI品質の証跡と誤認させない。[S23]

### 4.7 点数の読み方

READMEでは概念だけを短く示し、詳細式は`docs/features.md`へ委譲する。

- 配点合計は`Base + Special + enabled Question points = 100`。[S31]
- 設問獲得点、固有評価獲得点、類似度減点から`FinalRaw`を計算し、`FinalScore`は0〜100へ収める。[S32]
- 空の主回答はAIへ送らず0相当、技術的AI失敗はblankとして最終結果へ伝播する。[S05][S32]
- 高い類似度は不正行為の証明ではない。[S14]

### 4.8 出力と再開

- 新規runの既定directoryと`eval-yyyyMMdd-HHmm[-NN].xlsx`／`.partial.xlsx`命名を記載する。[S34][S07]
- finalは元workbook全体を保持し、`Quantification_Config`、`Quantification_References`、`Quantification_Results`、`Quantification_Run`を追加する。[S05][S28]
- 入力に同名sheetがある場合は既存sheetを変更せず一意名を使う。[S28]
- cancelまたはprocess終了後は、最後に保存済みのcomplete rowからpartialを再開する。[S05][S06]
- 既存final/partialを黙って上書きしない。[S06][S07]
- final作成後のoverrideは別名workbookへの任意出力であり、既存finalを上書きしない。[S03][S04][S10]

### 4.9 データとprivacy

READMEには実行判断に必要な要点だけを置く。

- workbook由来でAIへ送るのは、現在行の選択済みprimary/supporting/special値。質問文、Prompt、criterion metadata、参照回答、closed schema metadataも処理に応じて送る。[S29]
- 他行、非選択列、workbook pathは通常評価payloadへ含めない。[S29]
- application logは回答、Prompt、reason、evidence、credentialを受け取るfree-text surfaceを持たない。[S30]
- final/partialは入力全体、Prompt、参照回答、AI結果を含むため、入力と同等以上に機密として扱う。[S20][S06]
- 詳細は同期済み`docs/privacy-and-data-handling.md`へリンクする。[S20]

### 4.10 Promptファイルから起動する

ソフトウェアエンジニアを含む高度な利用者向けに短く記載する。

- `--input`は0または1回、`--prompt`は複数指定できる。[S35][S08]
- Prompt fileはUTF-8 `.txt`で、読み込んだ順にDesign画面へ表示する。[S35][S08]
- Promptは対象を選び「Promptを適用」したときだけcopyされ、起動引数だけでAI実行しない。[S04][S08]
- 長いcopy-paste例は同期済み`docs/prompt-launch.md`へ委譲する。[S22]
- packageにsample workbookを同梱しない現状では、repository sampleの手順を一般package手順として案内しない。[S12][S13][S22]

### 4.11 制限と非保証

- 対応外OS/architectureはB-02のrelease scopeに合わせて列挙する。
- `.xlsx`以外、encrypted/macro/external relationship等は対象外。[S33]
- 保存済みfinal workbookのアプリ再importとdefinition profile単独save/loadは、production source/testで実装確認できないため非対応として記載する。[S17]
- AI品質、教育的妥当性、公平性、法的・組織policy適合性、不正行為判定を保証しない。[S14]
- 未実測の処理時間、token数、費用を保証値として書かない。

### 4.12 詳細ガイド・問い合わせ・ライセンス

本文から案内する利用者文書は、同期確認済みのものだけにする。

- はじめに
- 機能と点数
- Custom evaluator
- Prompt起動
- データとprivacy
- トラブルシューティング

issue受付先やsupport policyはrepositoryに正本がないため、ownerが実在URLと運用方針を確定するまで推測して書かない。ライセンスはMITとして`LICENSE`へリンクする。[S36]

## 5. 同期対象ファイル

READMEから参照する公開文書とpackage同梱物を同じ変更単位で更新する。[S12][S13]

| ファイル | 必須変更 |
|---|---|
| `README.md` | 本計画の章立てへ全面再構成 |
| `docs/README.md` | v3 gate/実装状態説明を削除し、利用者persona別indexだけにする |
| `docs/getting-started.md` | v4配点、native picker、自動finalization、resume、optional override exportへ更新 |
| `docs/features.md` | 「native pickerなし」を削除し、v4 score/References/checkpoint/statusへ更新 |
| `docs/custom-evaluator-guide.md` | normal Customとspecial Promptのplaceholder差、v4配点との関係を確認 |
| `docs/prompt-launch.md` | package利用とrepository sample利用を分離し、CLI runtime前提をB-01の決定へ同期 |
| `docs/privacy-and-data-handling.md` | reference/similarity/special payload、partialの機密性を反映 |
| `docs/troubleshooting.md` | bundled/PATHの確定方式、auto model、checkpoint mismatch、partial cleanupを反映 |
| `images/*.png` | 現行production UIから再生成 |
| `images/README.md` | 新画像の生成日、画面内容、synthetic/fake境界へ更新 |
| `tests/.../DocumentationContractTests.cs` | 正式README契約、禁止開発語、platform/package整合、リンク対象を検証 |
| `tests/.../DocumentationScreenshotTests.cs` | 現行v4 UI stateで画像を生成し、古い200-unit fixture依存を更新 |
| `tests/.../WindowsPublishPackageTests.cs` | B-01/B-02で確定したrelease package内容と必須文書を検証 |

`dev/docs/`、`work/`、gate artifactはREADMEから参照しない。必要な開発記録の更新は別changeとして実施し、利用者READMEの本文へ混ぜない。

## 6. 実装手順

### Phase 0 — release契約を確定する

1. B-01のCopilot runtime方式を決定し、production/package/testを一致させる。
2. B-02の公開platformを決定する。
3. 正式release assetの公開方法、URL、署名状態、checksumを確定する。
4. UIに到達可能な開発task文言を除去または非表示testで固定する。
5. 正規sampleを取得し、固定size/hashを確認してrequired E2Eを再実行可能にする。正規sampleを入手できない場合は、testやREADMEの値を別fileへ合わせて変更しない。
6. Windows性能基準超過を再現確認し、環境負荷または実装regressionを特定して既定基準内へ戻す。
7. Phase 0完了時に、packageから起動、Copilot状態確認、synthetic 1件のauthenticated smokeを実施する。外部credentialを使うsmokeは、実行日時、package hash、成功/失敗をrequired deterministic testと分けて記録する。credentialまたはnetwork前提がない場合は`NOT_RUN_EXTERNAL_PREREQUISITE`と記録し、AI機能を含む正式公開のPhase 0は未完了のままとする。

**Phase 0 exit:** READMEの「対応環境」「入手」「AI前提」を仮定なしで書ける。

### Phase 1 — claim ledgerを作る

1. 章4の全記述候補を一文単位へ分割する。
2. production sourceとdirect testを1件以上ずつ割り当てる。
3. package/runtime/platform claimはpackage script/testも必須にする。
4. 外部service claimへ公式URLと確認日を付ける。
5. source間で矛盾するclaimを`BLOCKED`へ戻し、README執筆を先行させない。

**Phase 1 exit:** READMEで断定する全事実が`VERIFIED`。

### Phase 2 — 利用者文書とUI画像を同期する

1. `docs/`のv3記述をv4 production behaviorへ更新する。
2. packageに存在しないsampleやinstallerを一般手順から分離する。
3. production XAML/ViewModelの合成fixtureで画像を再生成する。
4. 画像ごとに、synthetic data、fake auth、fake score、非liveであることをmanifestへ記録する。
5. 教師1名相当の視点で、READMEからリンクを順にたどり、同じ操作名・file名・status名になっていることを確認する。

**Phase 2 exit:** READMEからリンク可能な利用者文書と画像に既知の旧契約がない。

### Phase 3 — READMEを書き換える

1. 現READMEを章単位で置換し、部分追記で旧説明を残さない。
2. 冒頭を「一文要約 → warning → できること → 対応環境」の順にする。
3. installとquick startを、初見利用者が上から順に実行できる順序で書く。
4. 英語の内部用語は、UI上に表示される名称または必要なfile/status名だけ残し、初出時に日本語で意味を説明する。
5. コマンド例は一般利用者が実際に受け取るasset配置で検証したものだけ載せる。
6. 開発文言、test実績、commit ID、gate、gap、ADR、future planを除去する。
7. 最後に詳細ガイドとMIT licenseへのリンクを置く。

**Phase 3 exit:** README単体で導入判断と最初のrun準備ができ、詳細は同期済みdocsへ遷移できる。

### Phase 4 — 自動検証を追加・更新する

`DocumentationContractTests`へ少なくとも次を追加する。[S15]

1. READMEに禁止語・開発IDがない: `GATE-ACCEPTANCE`、`IMPL-GAP-`、`HEAD `、特定commit hash、`U-03`、`U-04`、`IMPLEMENTATION_IN_PROGRESS`、test件数表現。
2. READMEの相対link先が存在し、packageへ含めるべきものはpackage testのmanifestにも存在する。
3. warning本文が`EthicsWarningText.Message`と一致する。
4. 対応platform記述がpackage script/testと一致する。
5. CLI同梱/PATH記述がruntime resolverとpackage testに一致する。
6. 展開済みpackageから既定resolverを実行し、確定したCLI pathを解決でき、manifest/hash不一致とPATHへの意図しないfallbackを拒否する。
7. `.xlsx`対応/非対応形式が`FileFormatClassifier`と一致する。
8. final 4 sheet名とpartial checkpoint名がproduction定数と一致する。
9. `--input`／`--prompt`記述がparser contractと一致し、auto-runを案内しない。
10. READMEに「自動採点」「不正検知」「AI品質保証」等の未証明claimがない。
11. 画像captionにsynthetic/fake境界がある。

### Phase 5 — release候補を検証する

順に実施する。

1. locked restore。
2. Release build。
3. 正規sampleのsize/hash確認。
4. Core/App全決定的test。
5. 文書契約test。
6. 現行画像の明示再生成と再度の文書契約test。
7. Windows publish/package test。
8. 生成ZIPとsidecarのhash照合。
9. cleanな展開先からself-contained app起動。
10. B-01で確定したruntimeを使う認証状態確認。
11. synthetic inputによる新規run、cancel、partial再開、final作成、override別名出力。
12. 入力SHA-256/size/mtime不変と、既存output no-overwriteを確認。
13. README記載どおりの手順だけを使う第三者walkthrough。
14. `git diff --check`、broken relative link確認、禁止語scan。

実在学生dataは検証へ使わない。credentialed/live smokeはsynthetic payloadだけで行い、教育品質の証明としてREADMEへ掲載しない。

## 7. README受入基準

| ID | 受入条件 |
|---|---|
| R-AC-01 | READMEの主対象が教師・評価設計者・運用者・利用者としてのソフトウェアエンジニアである。 |
| R-AC-02 | 冒頭から開発進捗、commit、gate、test件数、ADRへの導線を除去している。 |
| R-AC-03 | 断定する全製品claimがclaim ledgerで`VERIFIED`かつ出典付きである。 |
| R-AC-04 | 対応OS、architecture、署名、installer、CLI同梱/PATH方式が実packageと一致する。 |
| R-AC-05 | release asset URL、ファイル名、hash手順が実在物で確認済みで、placeholderや推測URLがない。 |
| R-AC-06 | 対応入力・非対応形式がproduction classifierと一致する。 |
| R-AC-07 | 4-step説明がnative picker、v4配点、new/resume、自動finalization、optional override出力を正しく反映する。 |
| R-AC-08 | warning本文がapplication resourceと完全一致する。 |
| R-AC-09 | 空回答=0相当、技術的失敗=blankを混同しない。 |
| R-AC-10 | final 4 sheet、partial checkpoint、no-overwrite、入力不変を正しく説明する。 |
| R-AC-11 | AIへ送る情報とfinal/partialの機密性を実行前に確認できる。 |
| R-AC-12 | `--input`／複数`--prompt`は事前入力だけで、AIを自動開始しないと明記する。 |
| R-AC-13 | 画像が現行UIで再生成され、synthetic/fake境界をcaptionに明記する。 |
| R-AC-14 | READMEからリンクする全利用者文書が同じcurrent contractへ同期している。 |
| R-AC-15 | build、全決定的test、文書契約test、package test、clean launch、README手順walkthroughが成功する。 |
| R-AC-16 | AI品質、公平性、法的適合性、組織policy適合性、不正行為判定、未実測性能/費用を保証しない。 |
| R-AC-17 | `LICENSE`へのMIT linkがあり、license本文を独自要約で改変しない。 |

## 8. review方法

### 8.1 教師視点

- 専門用語なしで用途と注意を説明できるか。
- 自分のworkbookのどの列が送信対象か判断できるか。
- blankを0点と誤読しないか。
- final/partialの保存場所と再開方法が分かるか。

### 8.2 ソフトウェアエンジニア利用者視点

- release assetの検証と起動を再現できるか。
- `--input`／`--prompt`の境界と明示適用を理解できるか。
- package同梱runtimeと外部依存を正確に把握できるか。
- source build情報がquick-startを妨げていないか。

### 8.3 privacy/運用視点

- AIへ送る値、送らない値、output/partialに残る値が区別されているか。
- 実在学生dataをrepository、issue、log、test artifactへ貼るよう促していないか。
- unsigned/署名済みの状態を誤表示していないか。

### 8.4 敵対的事実確認

各reviewerはREADMEの数字、固有名、file名、sheet名、status、対応platform、外部URLを一つずつsourceへ戻して検証する。sourceが見つからない文は削除するか、claim ledgerで`BLOCKED`へ戻す。

## 9. リスクと対策

| リスク | 対策 |
|---|---|
| 要求書を実装済みと誤認する | production配線とdirect testを優先し、要求だけの項目は除外する。[S15] |
| READMEだけv4化してpackage内docsが旧版のまま残る | `docs/`と画像を同じchange/gateで更新する。[S12][S13] |
| CLI導入案内を書いてもpackageからAIを開始できない | B-01をPhase 0 blockerにし、実package認証確認後だけ記載する。[S24][S13] |
| macOSを要求だけで対応済みと書く | 実package・署名・runner証跡がなければWindows-onlyとする。[S11][S13][S14] |
| synthetic screenshotを実データ結果と誤認される | captionとmanifestの両方にfake/synthetic境界を書く。[S23] |
| 仮定token数や古いtest件数が将来陳腐化する | READMEから削除し、必要時は実測値・環境・確認日付きの別文書にする。 |
| READMEの相対linkがpackageで壊れる | repositoryと展開済みpackageの両方でlink対象の存在をtestする。[S13] |
| 一般利用者向け手順にrepository固有pathが混ざる | package導線とsource/repository導線を分離する。 |

## 10. 作業分割

| Task | 依存 | 主な変更 | 完了確認 |
|---|---|---|---|
| RD-00 Release contract | なし | B-01〜B-08解消 | 実packageとrequired testで前提を実証 |
| RD-01 Claim ledger | RD-00 | README全claimとsource/test対応 | 全claim VERIFIED |
| RD-02 User docs sync | RD-01 | `docs/*.md` | 文書間矛盾0 |
| RD-03 UI screenshots | RD-00/RD-02 | `images/*.png`, manifest, generator | current v4 UI目視一致 |
| RD-04 README rewrite | RD-01..03 | `/README.md` | R-AC-01〜14 |
| RD-05 Contract tests | RD-04 | documentation/package tests | stale/dev/unsupported claimを検出 |
| RD-06 Release validation | RD-05 | code変更なし、検証のみ | R-AC-15〜17 |
| RD-07 Independent review | RD-06 | finding修正のみ | blocker/high 0、再検証PASS |

## 11. Definition of Done

次をすべて満たした時だけ「正式公開用README完成」とする。

1. B-01〜B-08が解消済み。
2. R-AC-01〜17をすべて満たす。
3. READMEとpackage内利用者文書に、既知のv3/v4矛盾がない。
4. READMEの全事実がclaim ledgerと出典へ追跡可能。
5. current packageをREADME記載手順だけで入手・hash確認・展開・起動できる。
6. synthetic runで新規実行、cancel、resume、final、override別名出力を確認済み。
7. 入力workbookがbyte-for-byte不変。
8. 未実測platform、署名、AI品質、性能、費用を成功・保証として記載していない。
9. 独立reviewで未解決の再現可能なblocker/high findingが0。
10. 利用者が既に行った無関係なworkspace変更を変更・復元していない。

## 12. 出典台帳

| ID | 出典 | 使用目的 |
|---|---|---|
| S01 | [`WorkflowNavigator.cs`](../src/StudyReportEvaluator.App/Navigation/WorkflowNavigator.cs)、[`InputView.axaml.cs`](../src/StudyReportEvaluator.App/Views/InputView.axaml.cs)、[`QuantificationDesignView.axaml`](../src/StudyReportEvaluator.App/Views/QuantificationDesignView.axaml) | 4-step、native picker、設計UI |
| S02 | [`ExecutionViewModel.cs`](../src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs)、[`ExecutionView.axaml`](../src/StudyReportEvaluator.App/Views/ExecutionView.axaml) | durable workflow、新規/再開、進捗、自動finalization |
| S03 | [`ResultsOutputViewModel.cs`](../src/StudyReportEvaluator.App/ViewModels/ResultsOutputViewModel.cs) | 結果review、override、optional export |
| S04 | [`ResultsOutputView.axaml`](../src/StudyReportEvaluator.App/Views/ResultsOutputView.axaml) | 結果画面の利用者操作 |
| S05 | [`DurableQuantificationOrchestratorTests.cs`](../tests/StudyReportEvaluator.App.Tests/Workflow/DurableQuantificationOrchestratorTests.cs) | reference-first、checkpoint、resume、4-sheet final、empty/failure semantics |
| S06 | [`CheckpointStoreTests.cs`](../tests/StudyReportEvaluator.App.Tests/Workbooks/Checkpoint/CheckpointStoreTests.cs) | partial保存、atomic update、入力保持 |
| S07 | [`OutputPathPlannerTests.cs`](../tests/StudyReportEvaluator.App.Tests/Workbooks/Writing/OutputPathPlannerTests.cs) | result命名、suffix、no-overwrite |
| S08 | [`LaunchOptionsTests.cs`](../tests/StudyReportEvaluator.App.Tests/Launch/LaunchOptionsTests.cs) | 起動引数、UTF-8 Prompt、明示適用、no-auto-run |
| S09 | [`InputViewTests.cs`](../tests/StudyReportEvaluator.App.Tests/UI/InputViewTests.cs) | picker選択/cancel、read-only loader、質問行 |
| S10 | [`ResultsOutputViewTests.cs`](../tests/StudyReportEvaluator.App.Tests/UI/ResultsOutputViewTests.cs) | override、blank、collision、別workbook出力 |
| S11 | [`publish-windows.ps1`](../scripts/publish-windows.ps1) | Windows 11 x64 self-contained publish、CLI除外、Office不要 |
| S12 | [`package-windows.ps1`](../scripts/package-windows.ps1) | unsigned ZIP、sidecar、同梱docs/images、CLI除外 |
| S13 | [`WindowsPublishPackageTests.cs`](../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsPublishPackageTests.cs) | package layout、clean launch、hash、署名なし |
| S14 | [`requirements-definition.md`](../docs/requirements-definition.md) | 承認済みv4目的、対象利用者、score、privacy、非保証、目標platform |
| S15 | [`traceability.md`](../dev/docs/traceability.md)、[`DocumentationContractTests.cs`](../tests/StudyReportEvaluator.App.Tests/Content/DocumentationContractTests.cs) | v4実装進行中の証拠境界、既存文書contract |
| S16 | [`docs/README.md`](../docs/README.md) | 既存利用者indexとv3/gate記述 |
| S17 | [`docs/getting-started.md`](../docs/getting-started.md)、[`docs/features.md`](../docs/features.md) | 既存手順と旧機能記述 |
| S18 | [`images/01-input-workbook.png`](../images/01-input-workbook.png)、[`images/05-execution-auto.png`](../images/05-execution-auto.png)、[`images/06-results-review.png`](../images/06-results-review.png) | 旧UI画像の目視確認 |
| S19 | [`docs/custom-evaluator-guide.md`](../docs/custom-evaluator-guide.md) | 既存Knowledge/Custom説明 |
| S20 | [`docs/privacy-and-data-handling.md`](../docs/privacy-and-data-handling.md) | 既存data flowと機密性説明 |
| S21 | [`docs/troubleshooting.md`](../docs/troubleshooting.md) | 既存CLI/path/error説明 |
| S22 | [`docs/prompt-launch.md`](../docs/prompt-launch.md) | Prompt起動とrepository sample手順 |
| S23 | [`images/README.md`](../images/README.md)、[`DocumentationScreenshotTests.cs`](../tests/StudyReportEvaluator.App.Tests/UI/DocumentationScreenshotTests.cs) | screenshot provenanceと生成方法 |
| S24 | [`CopilotClientFactory.cs`](../src/StudyReportEvaluator.App/Copilot/CopilotClientFactory.cs)、[`StudyReportEvaluator.App.csproj`](../src/StudyReportEvaluator.App/StudyReportEvaluator.App.csproj) | 既定のbundled-only resolver、manifest/hash、RID時CLI asset設計。PATH resolverは現行productionに存在しない |
| S25 | [`README.md`](../README.md) | 現行READMEの棚卸し |
| S26 | [`MainWindowViewModel.cs`](../src/StudyReportEvaluator.App/ViewModels/MainWindowViewModel.cs)、[`MainWindow.axaml`](../src/StudyReportEvaluator.App/Views/MainWindow.axaml) | UI内の開発task文言 |
| S27 | [`EthicsWarningText.cs`](../src/StudyReportEvaluator.App/Resources/EthicsWarningText.cs) | warning正本文 |
| S28 | [`AppOwnedSheetNameResolver.cs`](../src/StudyReportEvaluator.App/Workbooks/Writing/AppOwnedSheetNameResolver.cs)、[`DurableRunFinalizer.cs`](../src/StudyReportEvaluator.App/Workflow/DurableRunFinalizer.cs) | final 4 sheetと一意名、自動finalization |
| S29 | [`SafeEvaluationPayloadBuilder.cs`](../src/StudyReportEvaluator.Core/Prompting/SafeEvaluationPayloadBuilder.cs) | normal/special/reference/similarity payload |
| S30 | [`SafeLogger.cs`](../src/StudyReportEvaluator.App/Logging/SafeLogger.cs) | application logのclosed fields |
| S31 | [`QuantificationDefinition.cs`](../src/StudyReportEvaluator.Core/Domain/QuantificationDefinition.cs)、[`ScoringAllocationCalculator.cs`](../src/StudyReportEvaluator.Core/Scoring/ScoringAllocationCalculator.cs) | Base/Special/Question/Similarity設定と配点合計 |
| S32 | [`WeightedScoreCalculator.cs`](../src/StudyReportEvaluator.Core/Scoring/WeightedScoreCalculator.cs) | QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw/FinalScore、blank semantics |
| S33 | [`FileFormatClassifier.cs`](../src/StudyReportEvaluator.App/Workbooks/Intake/FileFormatClassifier.cs) | 対応/非対応形式とpackage安全境界 |
| S34 | [`OutputPathPlanner.cs`](../src/StudyReportEvaluator.App/Workbooks/Writing/OutputPathPlanner.cs) | 既定result directoryとfile命名 |
| S35 | [`LaunchOptions.cs`](../src/StudyReportEvaluator.App/Launch/LaunchOptions.cs)、[`ServiceRegistration.cs`](../src/StudyReportEvaluator.App/Composition/ServiceRegistration.cs) | `--input`／`--prompt` parseとGUI事前入力 |
| S36 | [`LICENSE`](../LICENSE) | MIT license |
| S37 | [`CopilotClientFactoryTests.cs`](../tests/StudyReportEvaluator.App.Tests/Copilot/CopilotClientFactoryTests.cs) | bundled manifest/CLI検証とCLI unavailable動作 |
| S38 | [`SampleWorkbookStructuralTests.cs`](../tests/StudyReportEvaluator.App.Tests/E2E/SampleWorkbookStructuralTests.cs)、[`.gitignore`](../.gitignore) | required sampleのpath、固定size/hash、欠落時failure、`/sample/`のGit除外 |
| S39 | [`WindowsX64PerformanceEvidenceTests`](../tests/StudyReportEvaluator.App.Tests/E2E/SampleWorkbookStructuralTests.cs#L94-L145) | 531行read/write/final validationの3回中央値と30秒基準 |
| S40 | 2026-09-02 local validation output: `dotnet test StudyReportEvaluator.slnx --no-build --no-restore -c Release --filter FullyQualifiedName!~SampleWorkbookStructuralTests` | 実測中央値31.238620秒とperformance test failure |

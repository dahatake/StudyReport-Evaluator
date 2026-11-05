# StudyReport Evaluator 要求定義書

| 項目 | 内容 |
|---|---|
| 文書版 | 4.6 |
| 基準日 | 2026-09-07 |
| 同版内追補 | 文書版4.6・基準日は承認baselineとして固定し、2026-09-15以降の要求追加・変更は版を上げずに同版内追補として扱う。各追補は節見出しの日付と§22のsource行に記録する。最終追補は2026-10-05の要求定義書レビュー指摘の整合（§22） |
| 状態 | 要求承認済み。UI・設定保存差分と記録済み文書contractはVERIFIED_SCOPED。T01〜T38はREVIEWED、T39は追加native FAIL・本人確認等の外部前提によりBLOCKED。製品0.8.6は2026-10-01に公開済み、F01はREVIEWED。公開はrepository ownerの明示判断でclean-host試験CH-01〜06を実施せず、protected publish-release workflowではなくdraft解除で行った。追加native FAIL、Narrator／本人walkthrough／隔離利用者native保存の未実施、T39 BLOCKED、CH-01〜06 NOT_RUN_EXTERNAL_PREREQUISITEは維持 |
| 入力 | Microsoft Forms または Google Forms から export した標準 `.xlsx` 1ファイル |
| 出力 | 入力を変更せず作成する別の標準 `.xlsx` 1ファイル |
| 対応環境 | Windows 11 x64。macOS、Linux、Windows Arm64は現版の正式公開対象外 |
| UI / Runtime | Avalonia `12.1.1` / .NET `10.0.11` self-contained。build SDK `10.0.400`を固定 |
| AI | GitHub Copilot SDK for .NET `1.0.11`を固定。実行時はOS利用者がPATHに導入したCopilot CLIを使い、版・実行ファイルSHA-256は観測診断情報であり信頼証明ではない。参照回答・通常評価・固有評価は同じ解決CLIと利用者選択model・思考レベル・Context Sizeをrunで統一（§11.18）。類似度はローカル計算 |
| 旧版 | v4.5の業務・入力・採点・数式・checkpoint・privacy・delivery境界をcarry forwardし、承認されたUI簡素化・設定保存・復元の差分だけを追加。v3.0はv4.0により全面的にsupersede済み |

> 本版は、要求所有者が2026-09-07に[UI・設定保存プランv2](../dev/docs/archive/work/20260907-ui-settings-redesign-plan-v2.md)のD01〜D20デフォルト・全実装を明示承認した差分を反映する。元プランの承認待ち表記は履歴であり、後続指示を優先する。最小実装契約と測定範囲は[UI layout contract](../dev/docs/ui-layout-contract.md)へ分離する。D17の当初上書き「全タスク完了後だけUnreleased追記、続いて製品PATCH `0.8.4` → `0.8.5`」と「T01では製品版・CHANGELOGを変更しない」は履歴として保持する。さらに後続の利用者不在時の自律続行指示により、T39をBLOCKEDのままF01／F02を進める。F01はREVIEWED、親担当による製品PATCHは反映済みだが、G4・全タスク完了・公開PASSを意味しない。以後のF02最終再検証は[実行記録](../dev/docs/archive/work/20260907-ui-settings-execution-record.md)の最新欄を正本とする。
>
> v4.5で継承したdelivery契約は、2026-09-06に承認された[1操作起動プラン](../dev/docs/archive/work/20260906-0617-one-action-startup-plan.md)、[実行上書き](../dev/docs/archive/work/20260906-one-action-startup-execution.md)、[ADR-0016](../dev/docs/adr/0016-windows-one-action-startup.md)に基づくWindows 11 x64 self-contained unsigned単一EXEの主配布追加である。[ADR-0013](../dev/docs/adr/0013-windows-only-public-release.md)・[ADR-0015](../dev/docs/adr/0015-windows-macos-installer-delivery.md)のZIPとSHA-256 sidecarは代替経路として維持する。既存のnon-public development MSIX sourceと機構回帰は保持するだけで拡張せず、一般利用者へ配布しない。macOS source/static contractと公開対象外の境界は変更しない。
>
> [S01/G1](../dev/docs/preflight/windows-singlefile-feasibility.md)は固定version・開発hostでの.NET標準App限定single-file全内容展開の方式適合だけを確認した。clean-host、MOTW／Windows保護、本人loginは`NOT_RUN`であり、本書は新EXEのOS-only受入完了を示さない。`v0.8.6`は公開済みだが、clean-hostと本人loginは公開前に実施していない。
>
> 本書中の試験件数・TRX path・hash・native観測等の検証記録は、要求の背景と判断根拠を示す参考記録であり、それ自体は要求ではない。現在の実装・検証状態の正本は[traceability](../dev/docs/traceability.md)とする。
>
> 本書の「AI評価」は成績を確定する自動判定ではない。AIは定量化候補を作り、最終的な評点と利用判断の責任は利用者が負う。

## 1. 目的

本アプリケーションは、Formsから出力された学生レポート回答workbookを元本として読み込み、次を行うローカルデスクトップアプリケーションである。

1. 利用者が元本 `.xlsx` を容易に選択する。
2. 1行目または2行目にある質問文を候補として取得する。
3. 学生ごとの回答をGitHub Copilot SDKへPromptとして送り、通常設問と設問固有項目を定量化する。あわせて設問ごとに参照回答集合を生成し、学生回答との表層類似度をLLMを使わずアプリ内で計算する。学生のAI履歴は入手できない前提とし、未検証の確認指標として扱う（FR-114、FR-115）。
4. AIは定量値、理由、根拠だけを返す。
5. ベース点、設問配点、固有設定配点、類似度減点はExcel数式で計算する。
6. 元本全体を保持した別workbookを作成し、元本は一切変更しない。
7. 長時間処理の進捗を表示し、プロセス終了後もcheckpointから再開できるようにする。
8. Windows 11 x64で、取得済みのself-contained単一EXEを開く1操作から入力画面へ到達する配布経路とSHA-256 sidecarを提供し、ZIPとsidecarも代替として残す。追加runtime導入・手動展開を主導線に要求せず、OS保護・本人認証は§13の別条件として明示する。

## 2. 対象利用者と基本原則

### 2.1 対象利用者

- 学生レポートを評価する教師、講師、採点担当者
- 評価Promptや採点項目を調整する教育担当者
- Custom Prompt、配布、検証を保守するソフトウェアエンジニア

| ペルソナ | 業務 | 主要な判断 | 使うデータ | 役割 |
|---|---|---|---|---|
| 教師・講師・採点担当者 | 学生レポートの回答workbookをAIで定量化し、結果workbookを得る | AIの定量化候補を最終評点・利用判断に使うか（責任は利用者、§2.2・冒頭注記） | 入力workbook（Forms export）、出力workbook、評価定義、設定 | 利用者（実行・評価結果の最終判断） |
| 教育担当者 | 評価Promptや採点項目（配点・評価方法・評価項目）を調整する | 配点・評価項目・Promptの内容 | 評価定義、Prompt、設定 | 利用者（設定の編集） |
| ソフトウェアエンジニア | Custom Prompt、配布、検証を保守する | 配布経路・検証結果の採否 | 配布物、検証記録（traceability） | 保守者 |

### 2.3 既定制約（ui_policy）

本アプリケーションは画面を持つ。既存の記述（§11、§2.2）にある次の制約を、画面を持つ要求の既定制約とする。新しい制約ではない。

- Windows 11 x64のAvalonia desktopアプリである（冒頭の表）。
- 通常画面は最小window（1024×720 DIP）で外側スクロールを要しない（§11.5）。
- 教育上の警告は常時表示するが、checkbox・同意・承認・score gate・実行blockにしない（§2.2、§11.2）。
- 設定は実行開始時にimmutable snapshotへ固定し、実行中の編集を現在runへ混入させない（§2.2、§11.8）。

### 2.2 基本原則

1. 入力workbookはread-onlyで扱い、成功、失敗、取消、再開のいずれでも変更しない。
2. 出力は必ず別fileとして作り、既存fileを黙って上書きしない。
3. AIが行うのは定量化だけであり、配点の加減算と最終評点はExcel数式が行う。
4. 設問数、通常評価方法数、通常評価項目数、固有評価項目数を業務上の固定値にしない。
5. 設定は実行開始時にimmutable snapshotへ固定し、実行中の編集を現在runへ混入させない。
6. 空回答と技術的AI失敗を区別する。空回答は0点相当、技術的失敗は空欄とする。
7. 回答本文、Prompt本文、AI理由、AI根拠、credentialをapplication logへ記録しない。
8. 教育上の警告は常時表示するが、checkbox、同意、承認、score gate、実行blockにしない。
9. 実測していないAI品質、対応platform、性能、署名状態を保証として表示しない。
10. 要求にない汎用plugin、cloud backend、database、policy engine、抽象layerは追加しない。

## 3. 利用前提

### 3.0 近似値チェックの前提・制約（2026-11-04追補）

学生のAI履歴は入手できない。参照回答は履歴の復元ではなく、指定した条件の比較用回答である。今回の実行で実AIへの追加実験、外部書込、課金サービス利用、deploy、pushは行わない。通常機能としての参照生成回数の変更はSRC-005で明示承認された範囲であり、品質や検出率の向上を保証しない。

| ID | 名前 | 値 | 単位 | 根拠 | 決定状態 |
|---|---|---|---|---|---|
| PARAM-002 | 新規採点定義の類似度減点係数 | 0 | 係数 | SRC-005の明示指定。既存の明示値には適用しない | 承認済み（依頼 2026-11-04） |
| PARAM-003 | 新規runの設問ごとの参照回答数 | 5 | 件 | SRC-005の開始点。固定の有限集合とし件数設定UIを増やさない | 承認済み（依頼 2026-11-04） |
| PARAM-005 | エラー表示の文字と背景のコントラスト比の下限 | 4.5 | 比 | WCAG 2.2 SC 1.4.3。案の配色（文字#A4262C・背景#FDE7E9）は約6.1:1で満たす | 承認済み（依頼 2026-11-05） |
| PARAM-006 | エラー表示の枠・アイコンと隣接色のコントラスト比の下限 | 3.0 | 比 | WCAG 2.2 SC 1.4.11。案の枠#C50F1Fは背景#FDE7E9と約5.1:1、白と約6.1:1 | 承認済み（依頼 2026-11-05） |
| PARAM-007 | エラー表示の配色（背景／枠／文字） | #FDE7E9／#C50F1F／#A4262C | 色コード | 依頼の案。Microsoft Design（Fluent 2）のエラー色（danger系の赤）の考え方に沿う。値を変えるときもPARAM-005・006を満たすこと | 承認済み（依頼 2026-11-05） |
| PARAM-004 | 短文注意の正規化後文字数上限 | 30 | 文字 | AI補足の表示上の注意値。識別閾値・減点条件ではない。共通のPARAM変更で戻せる | 承認済み（包括承認 2026-11-04・approval_policy） |

| 用語 | 定義 | 禁止同義語 |
|---|---|---|
| 類似度診断 | 学生回答と比較用参照回答との未検証の表層一致指標。履歴、AI使用確率、不正の判定ではない | なし |
| 参照回答集合 | 同一設問・同一runの比較条件に固定された参照回答と各生成状態の集合 | なし |
| ローカル対照評価 | ラベル付きの合成対照回答で行うオフラインの再現評価。実学生のAI使用の正解データではない | なし |

| 対象エンティティ | 状態 | 遷移の条件 |
|---|---|---|
| 類似度診断 | 未計算、算出済み、未検証、失敗、旧形式 | ローカル計算、技術的失敗、旧結果読込。算出成功でも品質は未検証 |
| 参照回答集合 | 生成中、完了、一部失敗、失敗、旧形式 | 有限生成、atomic保存、中断・再開、旧データ読込 |
| ローカル対照評価 | 未実行、評価済み、保留 | 固定データ・条件による評価、欠損・不正・未定義条件による保留 |

### 3.1 必須入力

- Microsoft FormsまたはGoogle Formsからexportされた、標準Office Open XML `.xlsx` 1ファイル
- 回答を含むworksheet
- 質問文を含む1行目または2行目
- 1行を1学生または1提出として扱える回答行

アプリはexport元サービスを推測して処理を分岐しない。入力形式の契約は標準 `.xlsx` とworksheet構造で定義する。

### 3.2 GitHub Copilot

AI処理にはGitHub Copilotを利用できるaccount、login、必要なnetwork接続と組織policy上の利用許可が必要である。これらはGUI起動の前提とは分離する。

- AI runtimeは固定SDKとOS利用者がインストールしたCopilot CLIを使う。PATHから一意に解決した同じCLIを認証確認、login、model列挙、参照回答生成、通常評価、固有評価に一貫して使用し、同梱・展開されたCLIやruntime manifestへ切り替えない。
- Windowsでは絶対PATH entryだけを探索し、空または相対entryを無視して`copilot.exe`を解決する。Unix source foundationではPATH上の実行可能な`copilot`を解決する。CLIがない場合はOSへのインストールとPATH設定を促し、WindowsではWinGetを推奨する。SDKとの互換性がない場合は、互換性エラーとして明示し、AI処理を開始しない。
- CLI versionと実行file hashは、実行時に観測できたidentityの診断記録に限り、固定値との一致検証、runtime manifestによる選択、信頼性のattestationに使わない。CLIの既存loginを利用し、アプリはtoken、password、device codeを読み取り・解析・収集・保存・log出力しない。
- loginは、起動時にPATHから解決したCLIがOS利用者の既存GitHub資格情報を利用できるか確認する自動確認（§11.10）を先に行い、利用できる資格情報がない場合だけCLI／ブラウザーを通じたGitHubとの対話認証を自動で開始する。手動の「GitHubにログイン」も維持する。
- CLIまたはloginが利用できない場合、アプリ起動、Excel読込、mapping、設計編集、checkpoint確認は利用できるが、新しいAI処理は開始できない。
- 利用できない理由、明示的な「GitHubにログイン」開始・取消、既存の「Copilot 状態を確認」による再確認を入力画面（§11.15）へ表示する。D-06は採用済みとし、動作とprocess所有範囲は§11.3に従う。

### 3.3 Spreadsheet runtime

Microsoft Excel、Office、LibreOffice、COM automationはrequired runtimeではない。formula対応spreadsheetで開く場合にExcel数式が再計算されるよう設定するが、アプリ内previewとrequired testはOfficeなしで成立させる。

### 3.4 GUI起動とAI利用の分離

- 取得済みの単一EXEだけで、Windows 11 x64の標準userがofflineでGUI、Excel読込、mapping、採点設計を利用できることを要求する。管理者昇格、setup script、terminalへのcommand入力、手動展開を要求しない。
- GUI起動に.NET Runtime／SDK、PowerShell、Node.js／npm、Git、GitHub CLI（`gh`）、別Copilot CLI、Office、IDEの導入を要求しない。sidecar、repository、隣接DLL／manifest、既存のCLI cacheや認証情報も起動の前提にしない。
- GUIは、認証確認やloginの完了を待たずに表示・操作可能とする。起動時の自動認証確認・自動login（§11.10）はGUI表示後に非同期で行い、AI評価は自動開始しない。CLIのStart/Ping、認証状態確認、login、実AI評価は別々に検証し、GUI表示やCLI helpの成功をAI-readyへ読み替えない。
- OS保護による警告・拒否と、network／account／認証の必要性は追加runtime不要の契約とは別であり、すべての端末での無警告・無条件起動を保証しない。

## 4. 入力workbook契約

### 4.1 file選択

1. 起動直後の入力画面にnative open-file pickerを設ける。
2. pickerは単一の `.xlsx` だけを選択対象として提示する。
3. full pathの直接入力も維持し、Prompt起動時の事前入力と高度な利用を可能にする。
4. picker取消時は現在の入力状態を変更しない。
5. 選択後、fileをread-onlyで検査してから採用する。

### 4.2 対応形式

受け入れるのは標準 `.xlsx` だけとする。次は明示的に対象外とする。

- `.xls`、`.xlsb`、CSV、PDF
- `.xlsm`等のmacro-enabled形式
- password／rights-protected／暗号化file
- 破損ZIP、外部relationship等の安全境界に違反するpackage

### 4.3 worksheet・質問行・回答行

1. worksheetを利用者が選択できる。
2. 質問文行は1行目または2行目から選択でき、既定候補を構造から提示する。
3. 質問文行より後の行を回答開始行として選択する。
4. 回答終了行を選択できる。
5. 1回答行を1学生または1提出として扱う。
6. 各設問は、質問文、主回答列、0件以上の補助列を持つ。
7. mapping候補は自動提示するが、すべて画面で変更できる。
8. 同一質問内で主回答列と補助列を重複させない。別質問間で同じ列を使うことは許可する。
9. 利用者が主回答列を選択した場合、選択中worksheetの質問文行と当該列が交差する1セルの値を、同じ設問の質問文へ即時反映する。回答行や列全体の値は連結しない。
10. 主回答列を選び直すたびに質問文を新しい交差セルの値で置き換える。反映後も利用者は質問文を手動変更できるが、その後に主回答列を再選択した場合は再び交差セルの値を反映する。
11. 交差セルが空または存在しない場合は質問文を空として扱い、列名や代替文を生成しない。質問文の必須検証を表示し、利用者の手入力で解消できるようにする。
12. 質問文行を変更してmetadataを再読込する前は、以前の質問文行の値を反映しない。現在の質問文を保持し、質問文行とmetadataの不一致を解消する再読込を要求する。

### 4.4 サンプルworkbook

repository内の現行サンプル正本は次とする。

`sample/SampleReport.xlsx`

**現行の構造契約は、本節後半の「2026-09-16 現行sample採用追補」（10列、target D〜I）である。** 次の表（12列、target F〜K）と、その直後の2つの追補で扱う測定値は履歴であり、現行sampleの期待値として使わない。

以下は2026-09-03に回答本文を出力せず、production readerで構造とheader由来mapping候補だけを再確認した履歴profileである。このexact pathだけをrepository sample契約として使い、同directoryの他fileを列挙、fallback、代用しない。local deterministic technical E2Eも同じfileを使う。

| 項目 | 2026-09-03の履歴測定値 |
|---|---|
| Bytes | 470,806 |
| SHA-256 | `73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA` |
| package entries / relationships | 11 / 8 |
| worksheet / dimension | 1件 / `A1:L531` |
| worksheet name SHA-256 | `88D759EA02CEF4B82885C6C620473162757C75522805707C20E2BE76A40A2825` |
| 初期primary候補 | F、G、H、I、J |
| 学生Prompt primary候補 | G、J |
| supporting候補 | H、K |
| Jの初期supporting候補 | K |
| 初期target / 対象外 | F〜K / A〜E、L |

**2026-09-16 利用者明示承認によるtest方針追補:** 上表のbytes・SHA-256・worksheet name hash・package entries／relationships 11／8は履歴測定として保持し、現在sampleとの固定一致を合否条件にしない。technical E2Eの過去の先頭128 bytes比較も診断専用とする。package件数はread-onlyで取得したZIPの実`Entries.Count`と`classification.PackagePartCount`の一致、metadataのpackage／relationship件数とclassificationの一致、relationship件数が正かつclassifierの安全上限内であることを検証する。canonical path、標準形式・外部relationship 0、sheet／dimension・行列数・mapping候補の検証と§4.5の実行前後SHA-256／size／last-write time一致は維持する。privacy・opt-in・実データのLive AI送信禁止は緩和しない。

**2026-09-16のread-only観測:** 現在fileは469,976 bytes、ZIP entries 13、relationships 9（root 4〔classificationlabelsを含む〕、workbook 4、sheet1のtable 1）、external relationships 0で、classifierは`StandardXlsx`として受理した。13／9は今回の観測値であり、新たな固定件数gateではない。旧fileの元bytesがないため正確な差分・変更原因は証明できず、旧fixtureが何を変更されたかは断定しない。詳細は`dev/docs/preflight/sample-workbook-profile.md`に記録する。

**2026-09-16 現行sample採用追補:** 利用者不在時の自律続行指示に基づき、production `WorkbookMetadataReader`＋`ColumnMappingSuggester`の測定済みprofileを現行契約とする。証跡は`TestResults/app-fixes-20260916/sample-profile/dahatake_DAHATAKE-OFFICE_2026-09-16_13_59_37_net10.0.trx`の標準出力。1 worksheet、`A1:J531`、531行／10列、header 1、data 2〜531（530行）、target D/E/F/G/H/I、対象外 A/B/C/J。D/Gは`PrimaryAnswer`、E/Hは`PrimaryAnswer | StudentPromptPrimary`、F/Iは`Supporting`。初期supportingはE→F、H→Iで、他候補は空。既定設問はD/E/G/Hの4問、KnowledgeCoverage／CustomPrompt／KnowledgeCoverage／CustomPrompt、各10点、base 60・special 0となる。旧12列profileは履歴として保持し、現行sampleの期待値だけを本追補で上書きする。既存12列syntheticと10人fixtureの契約は変更しない。証跡のtest自体は旧期待値との不一致でFAILであり、構造測定を修正後test／E2EのPASSへ読み替えない。構造assertをskip・任意化せず、元本不変と全安全境界を維持する。

列の役割はheader semanticsから得た候補であり、列位置だけで固定しない。利用者は実際のheaderと授業設計を確認し、通常Questionまたは固有評価のprimary/supportingを画面で変更する。

### 4.5 元本不変

- 処理開始時にSHA-256、size、last-write timeを取得する。
- checkpoint更新、再開、final commit前に元本identityを再確認する。
- identityが一致しない場合は新規AI送信とfinal commitを停止し、`INPUT_CHANGED`を表示する。
- 出力は元本のbyte-copyから作り、元の全sheetとdataを保持する。

## 5. 評価定義

### 5.1 Root設定

1つの評価定義は次を持つ。

- definition ID、name、revision
- source sheet、question text row、first/last data row
- ベース点 `BasePoints`。既定60
- 固有設定配点 `SpecialPoints`。既定0
- 類似度減点係数 `SimilarityPenaltyWeight`。新規定義の既定は{PARAM-002}。既存の明示係数は保持する（FR-114）
- 丸め桁数 `RoundingDigits`。既定1、範囲0〜6
- 1件以上の設問

数値制約は次とする。

$$
0\le BasePoints\le100
$$

$$
0\le SpecialPoints\le100
$$

$$
0\le SimilarityPenaltyWeight\le1
$$

### 5.2 通常設問

各設問は次を持つ。

- question ID、display name、question text
- primary answer column
- 0件以上のsupporting columns
- 設問配点 `Points`
- 1件以上の通常evaluator
- 0件以上の固有評価項目
- enabled flag

通常evaluatorは次の3種類とする（2026-11-03、画面の表示名はKnowledge／Prompt／Custom、§11.20）。

- `KNOWLEDGE_COVERAGE`（Knowledge）: app-owned semantic instructionで知識の説明、関係、適用を評価
- `PROMPT_ANALYSIS`（Prompt）: app-ownedの組込み評価用Promptテンプレートで、学生が書いたPromptをPrompt作成のベストプラクティスと照合して評価（採点・出力・AI契約はKnowledgeと同じ構造。テンプレートはBuiltInTemplateVersionで固定し、利用者は編集しない）
- `CUSTOM_PROMPT`（Custom）: 利用者が入力したPromptで評価（「Prompt」タブで編集できるのはこの種類だけ。既存の保存済み定義の`CUSTOM_PROMPT`はそのままCustomとして読める）

各通常evaluatorは1件以上のcriterion、raw range、criterion内weightを持つ。AIのcriterion rawを0〜100へ正規化し、evaluatorおよび設問内の通常評価値をExcel数式で加重平均する。

### 5.3 設問固有評価項目

各設問は、Prompt能力等を評価する0件以上の固有評価項目を持てる。各項目は次を持つ。

- special item ID、display name
- 評価対象source column 1件
- 0件以上のsupporting columns
- Custom Prompt template
- enabled flag

固有評価のAI定量値は0〜1とする。初版では固有項目ごとのweightを設けず、同一設問内のenabled項目を等分平均する。これは要求されていない追加設定を増やさないための意図的な制約である。

主値が空の場合、その固有項目はAIへ送らず0とする。技術的AI失敗では空欄とする。

### 5.4 Prompt placeholder

通常Custom evaluatorと固有評価で許可するplaceholderは次の6件だけとする。

- `{設問}`
- `{回答}`
- `{補助情報}`
- `{評価項目}`
- `{最小点}`
- `{最大点}`

通常Custom evaluatorは`{回答}`と`{評価項目}`を必須とする。固有評価は単一の0〜1値を返すため、`{回答}`を必須とし、`{評価項目}`は任意とする。

literal braceは`{{`と`}}`でescapeする。未知placeholder、未閉鎖brace、不正escapeはAI送信前に拒否する。挿入値を再走査しない。

この6 placeholder制約は、利用者が編集できる通常Custom evaluatorと固有評価のtemplateにだけ適用する。KnowledgeとPromptはapp-owned template（Promptは組込みの`prompt-analysis-v1`）、参照回答と類似度はapp-owned Promptであり、利用者は編集できない。参照回答は固定template内の`{設問}`だけをappが置換し、類似度は検証済みの設問、学生回答、参照回答を固定sectionへ直接組み立てる。`{参照回答}`を第7の利用者placeholderとして公開せず、app-owned Promptを利用者template validatorへ通さない。

## 6. 配点

### 6.1 配点不変条件

有効な通常設問を$q=1..N$、各設問配点を$P_q$、ベース点を$B$、固有設定配点を$S$とする。

$$
B+S+\sum_{q=1}^{N}P_q=100
$$

- `Points`は0以上の有限数とする。
- 合計が100と一致しない定義ではrunを開始しない。
- 比較はdecimalのexact valueで行い、表示上の丸め値で判定しない。

### 6.2 初期配点

有効設問数が$N>0$のとき、初期設問配点は次とする。

$$
P_q=\frac{100-B-S}{N}
$$

例:

- ベース60、固有0、2問: 各20点
- ベース60、固有10、2問: 各15点

割り切れない場合は、最後以外の有効設問へ$(100-B-S)/N$を小数第6位で0方向へ切り捨てた値を割り当て、最後の有効設問へ`100-B-S`との差分を割り当てて、合計を正確に`100-B-S`へ一致させる。例: ベース60、固有0、3問では`13.333333`、`13.333333`、`13.333334`。配点の算出は`RoundingDigits`（表示・出力の丸め）に依存しない。

### 6.3 手動配点と均等配分

- 利用者は各設問のPointsを個別に変更できる。
- 例: ベース60、固有0、設問1を30、設問2を10。
- base、special、設問の追加／削除／有効化変更時に、既存の手動Pointsを黙って変更しない。
- 初期mapping作成時と、利用者が明示的に「均等配分」を実行した場合だけ自動配分する。
- 合計不一致は明確な技術検証errorとして表示する。

## 7. AI処理

### 7.1 共通境界

- GitHub Copilot SDK for .NETの固定versionを使う。
- 実行時はOS利用者のPATHから解決したCopilot CLIを全AI操作で一貫して使う。bundled CLI／manifestへの選択・fallbackはしない。SDK非互換は明示的なruntime errorとし、CLI identity（観測version・SHA-256）は診断情報に限る。
- checkpoint内のCLI runtime identityは実行環境の診断・再開互換情報であり、CLIの真正性・信頼性を証明するattestationではない。
- 通常評価のmodelは、SDKが実行時に列挙したmodelから利用者が選択する。`auto`を含む列挙された全modelを選択できる（列挙の除外条件と件数の扱いは§11.13、FR-MS-01〜05）。
- 参照回答生成・通常評価・固有評価は、runで選択した同じmodelを使う。類似度評価はLLMを使わない（7.5節）。
- 保存希望modelが列挙されない場合は未選択とし、別modelへ黙ってfallbackしない。
- SDKがmodelのprompt／context上限を公開しない場合がある。`auto`はrouterであり上限を公開しない。
- 上限が不明なmodelでは、model相対のcontext budget検査を適用しない。上限不明を理由にrunを拒否せず、既定値を推定せず、別modelへfallbackしない。
- 上限が不明でも、app-owned requestの絶対上限とretry込みattempt上限は常に適用する。
- 実効modelの上限が既知か不明かを実行画面に明示する。
- 1 attemptごとにrestricted sessionを使用し、app-owned structured result toolだけを公開する。
- reasoning effort（SDK `SessionConfig.ReasoningEffort`）は§11.18の利用者選択値をrun開始時に固定し、同じrunの参照回答生成・通常評価・固有評価のすべてへ同じ値を指定する。未編集時だけ希望`low`を`none < minimal < low < medium < high < xhigh < max`の最も近い対応値（同距離は高い側）へ解決する。`auto`、reasoning effort非対応、対応値未列挙のmodelでは未指定とする。実際に使われたeffortはSDKから観測できない場合がある。指定値（または未指定）はcheckpoint、Run sheet、Reference sheet、ジョブログへ記録し、再開時は同じmodelと値を要求する（11.9節）。Context Sizeのtierも§11.18に従い同一runで固定する。
- shell、filesystem、Web、GitHub write、MCP、ambient memoryを公開しない。
- finite timeout、有限retry、cancel、session cleanupを必須とする。

### 7.2 参照回答生成

各有効設問について、質問文字列だけを比較の入力として、runで選択したmodel・reasoning effort・Context Sizeで{PARAM-003}の参照回答集合を作る。業務契約はFR-115に集約する。旧runの保存済み参照は集合を増やさず再利用する。生成失敗・欠損を一致なしや正常値へ変換せず、採点への伝播はFR-114の係数条件に従う。

参照回答と類似度はAI生成品質に依存する。高い類似度は不正行為を証明せず、低い類似度は回答品質を保証しない。学生の履歴を推定したとは表示しない。

### 7.3 通常回答評価

評価単位は1回答行 × 1通常設問 × 1enabled evaluatorとする。

AIはenabled criterionごとの次だけを返す。

- criterion ID
- raw score
- short reason
- evidence
- evidence source kind
- same-row stable source column ID

AIに配点、設問獲得点、固有設定獲得点、類似度減点、最終評点、合否を返させない。

evaluator IDはアプリが保持する値で、AIが省略した場合はアプリが補う。AIが返した場合は期待値との一致を検証し、不一致はschema不正とする。

主回答が空の場合はAIへ送らず、その設問の通常評価率と設問獲得点を0とする。必須・任意の区別はなく、空回答は常に同じ扱い（AIへ送らず0）とする。技術的AI失敗では通常評価率と獲得点をblankにする。

### 7.4 固有評価

評価単位は1回答行 × 1設問 × 1enabled special itemとする。

AIは次だけを返す。

- special item ID
- score 0〜1
- short reason
- evidence
- evidence source kind
- same-row stable source column ID

主値が空の場合はAIへ送らず0とする。技術的失敗はblankとする。

### 7.5 類似度評価

各回答行の各有効設問について、学生回答と、その設問の各参照回答の表層類似度を、LLMを使わずアプリ内の決定的な計算で0〜1として求める。目的は教師が回答の一致を確認する材料であり、AI履歴・使用確率・不正を識別する検出器ではなく、意味の近さ（正答どうしの類似）も測らない（FR-114）。

- 比較前にNFKC正規化、invariant小文字化、空白・句読点・記号除去を行い、文字n-gram（原則3、短文は2／1）を使う。
- 値は`max(0.70×学生n-gram包含率 + 0.20×Dice + 0.10×Jaccard, 最長共通部分文字列の被覆率)`を4桁に丸めたもの。0は類似しない、1は同一を表す。
- 学生回答が空の場合は、参照回答・参照集合の状態（一部失敗・失敗を含む）にかかわらず0とし、減点も0とする（AC-012）。学生回答が空でなく参照回答がblankの場合はblankとする。
- 参照集合が複数参照のとき、設問の類似度（§8.3の$L_q$）は完全な参照集合の参照別合成値の最大値とする（AC-010、AC-011）。中央値・最小値・差は記述統計の表示だけで採点・減点に使わない（AC-081、§17.2）。
- reasonは指標値だけの機械生成文とし、学生本文を含めない。
- 同じ設問の他学生回答との最大類似度と相手行を情報列として出力する。採点式・減点には使わない。
- 類似度は不正行為の証明ではない。

### 7.6 retryとfailure

- schema不正は新sessionで最大1回再試行する。
- transient network errorとtimeoutは新sessionで最大2回再試行する。Copilot CLIがAI呼び出しの通信失敗（接続・時間切れ）をsession errorとして返した場合もnetwork errorとして扱う。
- AIの応答待ちtimeoutは、Copilot SDKの`SendAndWaitAsync`既定の60秒に従う（アプリは値を指定しない。SDK版を更新する場合はSDK既定を再確認する）。起動・認証確認・session作成を含むattempt全体には、この60秒の応答待ちを先取りしない外側の上限として120秒を掛ける。どちらが満了してもtimeoutとして扱う。
- cleanup失敗後は追加retryを行わない。
- rate limit（SDK session errorの`rate_limit`または既知のrate limit code）は`RATE_LIMITED`とし、最大2回まで、指数backoff＋jitterを入れて新sessionで再試行する。
- 失敗の種類を問わず、1評価単位の総attemptは初回を含めて3回以内とする。種類別の再試行回数の上限（schema不正1回、network／timeout 2回、rate limit 2回）は、この総attempt上限の範囲内でだけ使える。観測時はrun全体の有効並列度を半減（下限1）し、成功が続けば設定値まで1ずつ戻す。
- quota枯渇（SDK session errorの`quota`または既知のquota code）は`QUOTA_EXHAUSTED`とし、再試行せず新規送信を止めてpartialを保持する。該当の参照回答・学生行はcheckpointへ保存せず、再開時に再実行する。
- cancel後に新規sessionを開始しない。
- 技術的失敗を0へ変換しない。

### 7.7 履歴なしの近似値チェック改善（2026-11-04、run 202611040836）

既存の§7.2・§7.5・§8とAC-004・AC-009〜012を再利用し、IDのなかった業務契約を以下の要求に構造化した。既存の他要求のUI配置・権限・通常評価・固有評価は変更しない。ただし§11.19の結果詳細には、FR-114（AC-077・AC-078・AC-081）の類似度の確認指標（参照別の値・一致位置・被覆率・短文注意）を「確認指標・未検証」として表示する（A-RV-02を改訂、包括承認 2026-11-04・202611040836）。

| 要求 ID | 題名 | 決定状態 | 優先度 |
|---|---|---|---|
| FR-114 | 未検証類似度の確認指標化と採点保護 | 承認済み（依頼 2026-11-04） | MUST |
| FR-115 | 質問文字列による複数参照と条件・互換性の一貫性 | 承認済み（依頼 2026-11-04） | MUST |
| FR-116 | ラベル付きローカル対照データによる再現評価 | 承認済み（依頼 2026-11-04） | MUST |

#### FR-114 未検証類似度の確認指標化と採点保護
- 要求: 教師は類似度診断を通常評価と分離した未検証の確認指標として確認できる。新規既定減点は{PARAM-002}、既存の明示係数は保持し、係数がゼロなら診断の失敗や欠損が採点を壊さない。全文一致、部分一致、双方向被覆率、一致箇所、短文注意を区別し、履歴・確率・不正を断定しない。
- 決定状態: 承認済み（依頼 2026-11-04）　出自: 依頼原文　優先度: MUST（履歴を得られない条件で誤認と不要な採点失敗を避ける）
- 上位: 既定制約（§1の定量化候補と利用者判断、§2.2の安全境界）　出典: SRC-005
- 対象エンティティ: 類似度診断　関係する状態: 未計算→算出済み、未検証、失敗、旧形式　参照パラメータ: PARAM-002、PARAM-004
- 関連する既存資産: §7.5、§8、FR-001の係数入力、FR-113の総合点参照。SurfaceTextSimilarityCalculator、ResultsSheetWriter、結果詳細表示。
- 受入基準 AC-004: 事前条件: 新規定義、明示係数を持つ既存定義・設定・Config読戻しと係数欠損の旧形式。起点: 作成・読込・保存・再起動。結果: 新規だけ{PARAM-002}を表示し、明示係数はゼロを含めそのまま往復する。旧形式で係数が欠損した場合は旧既定値0.1を読込時に補い、その事実を表示する。壊れた・範囲外の値は検証エラーであり新既定値へ黙って置換しない。基礎配点・固有配点の初期値と明示編集は従来どおり。
  - 検証レベル: system
- 受入基準 AC-010: 事前条件: 固定した学生回答と参照集合。起点: ローカル計算。結果: 各参照の従来の§7.5の正規化・合成指標とその最大値を保持し、原文全文一致と正規化後全文一致、最長連続部分一致の文字数、学生側と参照側それぞれの正規化後全文長を分母にした被覆率を別項目で確認できる。合成値が最大でも全文一致とは表示しない。最大値は参照別値と結び付く。完全な参照集合での最大値を§8.3の減点に使うSimilarityとする。
  - 検証レベル: system
- 受入基準 AC-011: 事前条件: 正常な通常・必要固有評価、および正常／欠損／失敗した診断。起点: Excel出力・Config係数変更・再計算。結果: `QuestionEarned`、`SpecialEarned`、`SimilarityPenalty`、`FinalRaw`とclamp済み`FinalScore`はConfig参照の数式とUIのcached previewで一致する。係数ゼロでは診断がblankでも減点はゼロとなり総合点は通常・固有評価から算出される。係数を正に変更した場合、完全な診断のみ`Points × Similarity × Weight`を使い、Similarityは完全な参照集合の参照別合成値の最大値（AC-010）とする。中央値・最小値・差は減点に使わない。完全な診断とは、新形式では当該runの予定slotがすべて成功し各参照の合成値が算出済みであること、旧形式（単独参照）では旧予定数1の参照が成功し合成値があることをいい、新詳細の欠損は完全性に含めない。一部失敗・成功0は不完全な診断であり、不完全な診断では減点と総合点はblankになる。ただし空回答はAC-012に従いSimilarity 0・減点0とする。通常・必要固有評価の失敗によるblankは係数ゼロでも残す。`採点結果`シートの合計も同じ条件に従う。
  - 検証レベル: system
- 受入基準 AC-012: 事前条件: 空回答、生成失敗、診断計算失敗、通常／固有評価失敗の各入力。起点: 評価完了・再開・出力。結果: 空の学生回答はAIへ送らずゼロとし、参照集合の状態（一部失敗・失敗を含む）にかかわらずSimilarity 0・減点0とする（AC-011のblank伝播より優先）。技術的失敗をゼロや一致なしに変換せず、対象値blankと失敗statusを保つ。診断だけの失敗は係数ゼロで通常評価の完了・checkpoint・総合点・出力を妨げず、係数が正の場合はAC-011のblank伝播を行う。取消・quota枯渇によるrun中断の既存契約は変えない。
  - 検証レベル: system
- 受入基準 AC-077: 事前条件: 全文一致、抜粋、編集、同じ短い正答、表記差を含む合成回答。起点: 診断詳細表示。結果: 一致した最長連続箇所の双方の位置を確認でき、原文位置と正規化文字位置を混同しない。表示する回答本文は既存の入力identity確認済み選択行の読取経路を用い、位置情報だけを新規診断へ保存する。正規化後いずれかの全文長が{PARAM-004}以下なら短文注意を表示し、被覆率の分母と正規化の影響を説明する。注意値は未校正であり判定・減点閾値ではない。
  - 検証レベル: system
- 受入基準 AC-078: 事前条件: 算出済み・失敗・旧形式の診断。起点: 実行速報・結果詳細・Excel出力の確認。結果: 通常評価とは別に「確認指標・未検証」と表示する。高値をAI使用履歴、AI使用確率、盗用・不正の確定と表示せず、低値を自力や品質の保証とも表示しない。学生間最大類似度は従来どおり情報のみで減点に使わない。新たなAI検出器・自動不正判定を追加しない。
  - 検証レベル: system
- 受入基準 AC-079: 事前条件: 旧結果workbook・partial・設定に新診断欄がない。起点: 読込・再開・結果表示。結果: 旧合成値・係数・採点値・元本を保持し、新詳細の欠損は「旧形式・未取得」と表示する。既存値から双方向指標や一致位置を捏造せず、旧結果を開くだけで再生成・再採点・上書きしない。旧形式の再開は旧参照集合を使い、旧予定数1の参照が成功している旧形式の診断はAC-011の完全な診断として扱い旧採点値を保持する。失敗時の係数条件はAC-011に従う。
  - 検証レベル: system

#### FR-115 質問文字列による複数参照と条件・互換性の一貫性
- 要求: 教師は質問文字列だけを入力とした有限の参照回答集合を同一runで共有し、比較条件と参照間のばらつきを確認できる。集合・条件・状態・診断の保存、再開、Excel、UIを一貫させ、旧データを破壊しない。
- 決定状態: 承認済み（依頼 2026-11-04）　出自: 依頼原文　優先度: MUST（単独参照の偶然を説明し、再開で比較条件を変えない）
- 上位: 既定制約（§1の比較材料、§2.2のrun snapshotと元本保護）　出典: SRC-005
- 対象エンティティ: 参照回答集合　関係する状態: 生成中→完了、一部失敗、失敗、旧形式　参照パラメータ: PARAM-003
- 関連する既存資産: INT-001、§7.1・§7.2・§7.6、AC-014・AC-015、FR-MO-07。ReferenceAnswerEvaluationRunner、ReferenceAnswersSheetWriter、DurableQuantificationOrchestrator。
- 受入基準 AC-009: 事前条件: 新規runの有効設問とfake AI transport。起点: 参照生成。結果: 設問ごとに{PARAM-003}の固定slotを生成し、比較入力はその質問文字列のみとする。学生回答・学生Prompt・補助列・採点基準・追加の解答指示・他参照回答・外部検索・履歴を混入させない。既存のclosed schemaと結果収集のためのsystem指示は維持し、比較内容とは区別して条件に記録する。全学生が同じ集合を使い、通常評価と同じmodel・effort・Context Sizeを指定する。
  - 検証レベル: system
- 受入基準 AC-080: 事前条件: 生成成功・失敗・model `auto`・SDKで未観測の条件。起点: 保存と表示。結果: question ID、質問文字列、slot識別、参照本文、status、時刻、指定model・effort・tier、比較入力の制約、system/template版、アプリの比較方式版・正規化条件を集合と結び付ける。指定値と実際の観測値は区別し、実model・temperature・seed等が観測不能なら未取得と記録する。同じ条件で再生成しても同一回答が得られるとは保証しない。
  - 検証レベル: system
- 受入基準 AC-081: 事前条件: 正常・重複・一部失敗の参照集合。起点: 集計と診断表示。結果: 参照別値、最大・最小・中央値・最大と最小の差、成功数／予定数、最大値の参照を確認できる。重複参照を独立の裏付けとせず重複を表示する。一部失敗なら成功参照に限った暫定集計と明示し、完全な集合の値や正常な減点入力へ読み替えない。中央値・最小値・差は記述統計の表示だけで採点・減点に使わない（減点はAC-011の最大値、§17.2）。成功参照なしの場合は集計をblankとする。
  - 検証レベル: system
- 受入基準 AC-082: 事前条件: 保存済みslot、生成中の中断、checkpoint書込失敗、取消・quota枯渇。起点: 生成・atomic保存・再開。結果: 保存済みslotの本文・条件・statusを再利用し再生成しない。未保存slotだけを既存の有限retry・timeout規則内で再開する。保存済みの終端失敗は同一runで勝手に再試行せず、全学生が同じ失敗集合を使う。書込失敗は以前のcheckpointを保持する。quota枯渇の対象を保存せず新規送信停止する従来規則も維持する。
  - 検証レベル: system
- 受入基準 AC-083: 事前条件: 旧単独参照checkpoint、旧結果、追加条件欄欠損、不正な新形式。起点: 読込・再開。結果: 旧参照は旧形式の単独集合として保持し{PARAM-003}へ水増ししない。未保存の旧参照生成も旧予定数で行う。欠けた条件は未取得とし新規定義へ黙って変換しない。不正な新形式・条件不一致はsafe errorで拒否し旧bytesを保持する。既存のinput・definition・model・runtime再開条件を緩めない。
  - 検証レベル: system
- 受入基準 AC-084: 事前条件: 同じrunの正常・暫定・旧形式の比較結果。起点: checkpoint復元、完成Excelの出力、UI再読込。結果: References sheetの集合と条件、Results sheetの診断と採点、Run sheetの比較条件要約、Configの係数、実行・結果UIの値／状態が同じsnapshotを示す。本文・引用・Promptを通常logやsetting.txtへ追加保存せず、出力文字列をExcel formulaとして実行しない。既存のsheet名衝突、元本保持、別名atomic出力を維持する。
  - 検証レベル: system
- 受入基準 AC-085: 事前条件: 設問・予定slot・既存retry上限から求めたfakeの実行計画。起点: preflight・参照生成。結果: {PARAM-003}を含む予定生成数とretry込みattempt上限を計算・表示し、既存のrequest容量・attempt容量・cancel規則を全slotに適用する。件数設定UIや無限生成を追加せず、同一runの学生数に応じて参照を再生成しない。失敗を隠す追加生成、無断の別model切替、実AIへの追加検証実験を行わない。
  - 検証レベル: system

#### FR-116 ラベル付きローカル対照データによる再現評価
- 要求: 保守者は、追加指示あり／なし／抜粋／編集／自力のラベル付きローカル対照データで、設問単位のholdoutを分離して比較指標を再現評価できる。誤検知・見逃し・保留と評価の限界を報告し、未検証のまま不正判定や確率を製品へ追加しない。
- 決定状態: 承認済み（依頼 2026-11-04）　出自: 依頼原文　優先度: MUST（改善を実AI実験なしで検証し、未評価を成功と誤認させない）
- 上位: 既定制約（§1の利用者判断、§19のdeterministic検証）　出典: SRC-005
- 対象エンティティ: ローカル対照評価　関係する状態: 未実行→評価済み、保留　参照パラメータ: なし
- 関連する既存資産: §7.5のローカル計算、FR-114・FR-115、§19の合成データ検証。実装コードを正解にしない独立oracle。
- 受入基準 AC-086: 事前条件: 実学生・履歴を用いない固定の合成質問、参照回答、対照回答。起点: ローカル評価。結果: 「追加指示なし」「追加指示あり」「抜粋」「編集」「自力」の各ラベルに対応するデータ、作成方法、元回答との関係、設問のグループ、データ版／hashを確認できる。ラベルは作成者が定めた対照条件でありAI使用の実証ではない。追加指示ありの代替回答もローカルで固定し、実AIで新たに生成しない。
  - 検証レベル: system
- 受入基準 AC-087: 事前条件: 同じ設問由来の全文・抜粋・編集・言換えを含む対照群。起点: 調整群とholdoutの分割。結果: 同一設問の派生回答と参照集合は同じ側へまとめ、両群へ漏れない。分割manifest・固定順序またはseedを記録し、空のholdoutや設問重複は評価不成立とする。holdoutを用いたルール調整を評価成功としない。
  - 検証レベル: system
- 受入基準 AC-088: 事前条件: 固定のローカル評価専用ルールと期待ラベル。起点: 集計。結果: AI由来条件の合成群（追加指示あり／なし、抜粋、編集）を対照陽性、自力群を対照陰性とする対応を明示し、ルールによる要確認／非該当／保留を集計する。誤検知、見逃し、正検知、正除外、保留、対象数と分母を全体・ラベル別・分割別に報告する。保留を正解に数えず、分母なしの率は未算出とする。ルール未指定・欠損・不正入力は保留とし、製品用の未校正判定閾値・AI使用確率を設けない。
  - 検証レベル: system
- 受入基準 AC-089: 事前条件: 同じデータ・分割・参照集合・比較版・評価ルール。起点: オフラインで再実行。結果: 指標・分類・件数が同じになる。データhash、分割、条件、比較版、ルール、結果を再実行可能な評価記録に含める。従来の単独参照合成値と改善した診断を同じ対照群で比較し、独立した期待値で全文／部分／被覆率と誤検知・見逃し・保留の集計を照合する。実AI、実credential、外部書込、課金APIは呼ばない。
  - 検証レベル: system
- 受入基準 AC-090: 事前条件: 合成対照評価結果、未評価・欠損条件。起点: 評価記録を読む。結果: 「合成対照データでの評価・実学生について未検証」と明記し、履歴不在、参照生成の非決定性、短文の共通正答、追加指示・編集・抜粋による見逃し、設問／model条件への依存を説明する。holdout結果が悪い場合も隠さず報告し、合成評価成功を実学生の識別性能・確率・不正の証明へ転用しない。評価ツールは学生の点数や保存済み結果を変更しない。
  - 検証レベル: system

## 8. Excel計算

### 8.1 通常設問獲得点

設問$q$の通常評価率を$R_q\in[0,1]$とする。既存criterion正規化値が0〜100である場合は100で除算する。

$$
QuestionEarned_q=P_qR_q
$$

空回答では$R_q=0$、技術的AI失敗では$R_q=\mathrm{blank}$とする。

### 8.2 固有設定獲得点

設問$q$にenabled special itemが$K_q>0$件ある場合、各scoreを$s_{q,k}\in[0,1]$として次を計算する。

$$
SpecialQuestion_q=\frac{\sum_{k=1}^{K_q}s_{q,k}}{K_q}
$$

enabled special itemを1件以上持つ有効設問の集合を$Q_S$、その件数を$M=|Q_S|$とする。

$$
SpecialEarned=
\begin{cases}
S\displaystyle\frac{\sum_{q\in Q_S}SpecialQuestion_q}{M} & S>0\\
0 & S=0
\end{cases}
$$

- `SpecialPoints > 0`の場合、1件以上のenabled special itemを必須とする。
- `SpecialPoints > 0`かつenabled special itemが0件の場合、Design検証とrun前preflightの両方で`SPECIAL_ITEMS_REQUIRED`として拒否する。
- `SpecialPoints = 0`の場合、固有評価AIを実行せず、`SpecialEarned=0`とする。
- 固有項目または対象設問の技術的失敗が1件でもあれば`SpecialEarned`はblankとする。

### 8.3 類似度減点

設問$q$の類似度を$L_q\in[0,1]$、減点係数を$W\in[0,1]$とする。$L_q$は完全な参照集合の参照別合成値の最大値とする（従来の単独参照と同じく保守的な値。AC-010の最大値と一致）。完全な参照集合とは、新形式では当該runの予定slotがすべて成功し各参照の合成値が算出済みであること、旧形式（単独参照）では旧予定数1の参照が成功し合成値があることをいう（新詳細の欠損は完全性に含めない。AC-011、AC-079）。学生回答が空なら参照の状態にかかわらず$L_q=0$・減点0とする（AC-012）。

$$
SimilarityPenalty_q=P_qL_qW
$$

各設問の類似度と類似度減点を、通常設問評価とは別の列へ出力する。

例: $P_q=20$、$L_q=0.99$、$W=0.1$のとき、減点は1.98点。

$W=0$でも、参照回答の生成と類似度の計算は省略しない。出力後にConfigの$W$を変更して再計算できるようにするためである（§8.6）。ただし診断の失敗・欠損は採点から分離し、$W=0$なら`SimilarityPenalty`は0とする。$W>0$で参照集合が不完全または診断失敗の場合は減点と総合点をblankとする（FR-114、AC-011）。

### 8.4 FinalRawとFinalScore

$$
FinalRaw=B+\sum_q QuestionEarned_q+SpecialEarned-\sum_q SimilarityPenalty_q
$$

通常評価または必要な固有評価が技術的失敗でblankの場合、`FinalRaw`と`FinalScore`もblankとする。参照回答・類似度診断だけの失敗・欠損については、$W>0$の場合だけblankを伝播し、$W=0$では減点0として通常・固有評価から採点を続ける（AC-011、AC-012）。

監査用に`FinalRaw`を保持し、表示用評点は0〜100へclampする。

$$
FinalScore=
\begin{cases}
0 & FinalRaw<0\\
100 & FinalRaw>100\\
FinalRaw & \text{otherwise}
\end{cases}
$$

### 8.5 formula ownership

- AI raw、reason、evidence、source、statusはliteral cellとする。
- effective raw、normalization、平均、QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScoreはExcel formula cellとする。
- base、special、question points、similarity weight、range、rounding digitsはConfig cellを参照する。
- 配点や係数をformula textへliteral埋込みしない。
- raw user formulaを生成しない。
- formula cellへapp previewのcached valueを保存する。
- formula allowlist、参照、length、function arguments、DAGをAI送信前とfinal write時に検証する。

### 8.6 手動変更

出力後、利用者がConfigのbase、special、question points、similarity weightを変更するとformulaが再計算される構造とする。合計100制約を破った場合はConfig上の検証列で明示し、`FinalScore`をblankにする。

既存の通常criterion overrideは維持する。固有評価と類似度は初版UIでoverride欄を追加せず、必要な変更は出力workbookのliteral値またはformulaを利用者責任で編集する。

## 9. 出力workbook

### 9.1 pathと命名

出力先は内部では明示指定（output override）と実効pathを分離するが、共通設定の表示・変更対象は次回の出力フォルダーに統一する（FR-002、2026-11-03追補）。今回の選択操作、保存済みの明示指定、未指定（`null`）の順に決定し、明示指定はabsolute pathとして再起動・入力Excel変更後も保持する。「既定に戻す」の画面操作は`null`への明示変更として扱い、過去の保存済み指定へ戻さない。旧仕様の空欄への手入力による解除をこの操作に置き換える。

明示指定が`null`の場合だけ、入力fileのdirectoryを基準に`result` subdirectoryを都度算出する。自動算出したresult pathを明示指定として保存しない。入力未選択なら「入力後に決定」と表示する。利用者は開始前に別の既存または作成可能directoryへ変更でき、次回の実効出力先を主画面で確認できる。

指定先が利用不可でも別pathへfallbackせず、修正または既存の作成可能先検証を促す。設定復元だけではdirectoryを作成しない。既存checkpointの予約済みfinal／partial pathと再開条件は変更しない。

明示指定がない場合の既定完成名（指定時も同じbasename規則）:

`result/eval-{yyyyMMdd-HHmm}.xlsx`

`{yyyyMMdd-HHmm}`は、新規runを開始してpathを予約した時点のOSローカル時刻とし、invariant cultureで書式化する。再開時は予約済みのpathを使い、名前を再計算しない。

同一分に衝突した場合は次の未使用名を使う。

- `eval-{yyyyMMdd-HHmm}-02.xlsx`
- `eval-{yyyyMMdd-HHmm}-03.xlsx`
- 以下同様

既存fileを上書きしない。完成pathはrun開始時に予約し、画面とcheckpointへ保存する。

結果画面の「別名 workbook 出力」欄の既定名は、同じ規則で出力先directoryの未使用名を作り直す（FR-107、§11.24）。ジョブのテキストログ`{yyyyMMddHHmm}-log.txt`も同じ出力先へ上書きなしで保存する（FR-108、§11.24）。

### 9.2 app-owned sheets

| Sheet | 内容 |
|---|---|
| `Quantification_Config` | immutable definition snapshot、base、special、question points、similarity weight、Prompt、mapping、range、rounding |
| `Quantification_References` | 設問ごとの質問文、runで選択したmodelによる生成回答、model、status、生成時刻、reasoning effort |
| `Quantification_Results` | 行ごとの通常評価、固有評価、類似度、減点、FinalRaw、FinalScore、理由、根拠、status、Excel formula |
| `Quantification_Run` | input/definition/checkpoint identity、app/SDK/CLI/model、開始/終了、件数、error、token usage、実sheet名 |

`Quantification_Results`の類似度列名`.Similarity_AI_Raw`は、旧版との互換のために残した名前である。値はAIではなくアプリのローカル計算（§7.5）による。

同名sheetが入力に存在する場合は既存sheetを変更せず、` (2)`、` (3)`の最小suffixを付ける。

### 9.3 atomic finalization

- target directory内の一意tempへ元本をbyte-copyする。
- app-owned sheetsとformulaを書き、flush、close、read-only reopen、validateする。
- 元本identityを再確認する。
- 同一volumeのno-overwrite renameだけで完成名を作る。
- valid finalまたはfinalなしのどちらかにする。

### 9.4 結果Excelの解説文書（2026-09-30追加）

出典: 依頼原文（2026-09-30「実行結果のExcelシートの詳細な解説を、ITに詳しくない大学・高校の教授や先生が理解できるような説明用のドキュメントを作成して`/docs/result-excel-description.md`に保存をして、`/README.md`から適切な文章とリンクもつけてください。」）、`src/StudyReportEvaluator.App/Workbooks/Writing/`の各sheet writer、`dev/docs/excel-contract.md`、`docs/features.md`。

§9.2の`Quantification_`で始まる4つのapp-owned sheet（と、完成workbookだけに入る結果一覧`採点結果`、FR-113）の各列の意味を、教員が読める形で説明した文書がなかった（列名・型は開発者向けの`dev/docs/excel-contract.md`だけにあり、Config・Runの列と、References・Resultsの一部の列（`ReasoningEffort`、`Similarity_Peer_Max`、`Similarity_Peer_Row`）は記載がなかった）。本節はその文書を要求する。

| ID | 優先度 | 要求 |
|---|---|---|
| FR-XD-01 | MUST | `docs/result-excel-description.md`を作成し、final workbookの`Quantification_`で始まる4つのapp-owned sheetそれぞれの目的と、結果一覧`採点結果`（FR-113、完成workbookだけの5枚目）の目的・列の意味を説明し、次の全列・全項目の意味を説明する。①`Quantification_Results`: 先頭`SourceRow`、評価項目ごとの10列（`.Scorable`〜`.Status`）、`.Evaluator_Score`、設問ごとの4列（`.Answer_Present`〜`.Question_Earned`）、固有評価の6列と`.Special_Question_Rate`、類似度の6列（`.Similarity_AI_Raw`〜`.Similarity_Peer_Row`）、末尾の`Base_Points`／`Special_Earned`／`Final_Raw`／`Final_Score`。②`Quantification_References`: A〜H列の8列。③`Quantification_Config`: A〜AG列の33列と`RecordType`の全値（`DEFINITION`、`CANONICAL_JSON`、`QUESTION`、`SUPPORTING_SOURCE`、`EVALUATOR`、`CRITERION`、`SPECIAL_EVALUATION`、`SPECIAL_SUPPORTING_SOURCE`）。④`Quantification_Run`: `Field`列の26項目（FR-MO-07の`ContextTier`を含む）。列名・列記号・並び・literal／formulaの区別は実装（§8、§9.2、`ResultsSheetWriter`／`ConfigSheetWriter`／`ReferenceAnswersSheetWriter`／`RunSheetWriter`）と一致させる。 |
| FR-XD-02 | MUST | 文書はITに詳しくない大学・高校の教員を読者とし、専門用語（評価項目、正規化、数式セル、識別コード等）を初出で平易に説明する。次を含める。①最終点数（`Final_Score`）を最初に見る場所として案内する。②元sheetと結果sheetの行番号が同じであること。③`AI_Raw`／`Override`／`Effective_Raw`の関係と、`Override`欄の入力制約（範囲外・空回答行は入力不可）。④§8の式による点数の流れと、数値入りの計算例（基礎点60、2設問各20点、係数0.1、`Final_Raw`75）。⑤空欄と0の違い（§2.2の6）。⑥`ResultsStatusCodes`の12個の状態コードすべての意味と対応の目安。⑦§8.6の手動変更（基礎点・固有評価配点・類似度減点係数・設問配点）と合計100の条件、および実行時に`SpecialPoints`が0だった場合に後から増やすと`Final_Score`がblankになること。 |
| FR-XD-03 | MUST | 文書の冒頭に教育上の警告文（§11.2の固定文言）、対象版の注記（公開`v0.8.6`、旧公開`v0.8.1`との区別、clean-host試験CH-01〜06と本人loginは公開前に未実施）を表示し、機密性（結果Excelは入力と同等以上に機密、§14）、入力Excelを変更しないこと、AIの点・類似度は採点の材料で最終判断は利用者が行うこと、類似度は不正行為の証明ではないことを明記する。 |
| FR-XD-04 | MUST | `README.md`の本文（「出力と再開」）に、文書の目的を示す1文以上の説明とリンクを置き、「ガイド」の一覧にもリンクを置く。`docs/README.md`の読者別ガイド、`docs/getting-started.md`と`docs/features.md`のfinal workbookの節からもリンクする。 |
| FR-XD-05 | MUST | 文書は、Windows単一EXE・ZIP・非公開MSIXの公開文書allowlist（§13、AC-030）に含め、配布物内のREADMEからのリンクが切れないようにする。開発者向け資料（`dev/`）や実装の型名へ利用者を誘導しない。 |
| FR-XD-06 | SHOULD | 上記sheetの列を追加・変更・削除する変更では、同じ変更で本文書を更新する。AC-045の試験が、実装が書いた見出し・列記号・`Field`名・定数との不一致を検出する。 |

境界・例外: 文書は公開`v0.8.6`の出力を説明する。旧公開`0.8.1`が同じ列を出力するとは保証しない（FR-XD-03の版注記）。実際の列名のうち設問・評価方法・評価項目のコード部分は設計ごとに異なるため、列名は「コード＋列の種類」の形で説明し、コードの値は列挙しない。文書はAI品質・公平性・法的適合性を保証しない（§2.2の9）。

## 10. checkpointと再開

### 10.1 checkpoint file

run開始時、完成名に対応する次のfileを作る。

`eval-{yyyyMMdd-HHmm}.partial.xlsx`

衝突suffixは完成名と同じ番号を使う。checkpointは元本全体のbyte-copyに`Quantification_Checkpoint` sheetを追加した標準 `.xlsx` とする。

### 10.2 保存単位

- 参照回答集合の各slotを生成するたびに保存する（FR-115）。
- 学生1行に必要な通常評価、固有評価、類似度がすべて終わるたびに保存する。
- 保存はtarget-local tempへのwrite、reopen validation、atomic replaceで行う。
- process強制終了時も、最後に成功したatomic checkpointまで復旧できる。

### 10.3 保存内容

- schema version
- input identity
- definition canonical snapshotとSHA-256
- final/partial path
- 通常評価model ID、参照回答model ID（通常評価と同じ）、run-level reasoning effort、Context Size tier（`contextTier`。既定tierは省略、拡張は`long-context`、FR-MO-07）
- 通常評価modelに`auto`を選んだ場合、記録されるのは`auto`であり、routerが実際に選んだmodelは記録されない。同じcheckpointから再開しても同一modelへroutingされる保証はない。
- app、SDK、CLI runtime identity
- 参照回答とstatus
- 完了済みunitの定量値、reason、evidence、status、token usage
- 完了済み学生行番号
- 開始時刻、最終checkpoint時刻

### 10.4 再開条件

再開時は次を完全一致で検証する。

- checkpoint schema version
- input SHA-256、size、last-write time
- definition SHA-256
- 通常評価model ID
- reference model ID（通常評価と同じ）、run-level reasoning effort、Context Size tier（省略時は既定tier。不一致は`CHECKPOINT_MODEL_MISMATCH`、FR-MO-07）
- app major schema compatibility
- Copilot CLI runtime identity

CLI runtime identityはPATHから実行したOS-installed CLIについて観測した診断・再開互換情報であり、同梱manifestやSHA-256による信頼attestationではない。

model IDの一致はID文字列の一致であり、`auto`の場合に同一の実modelへroutingされることを保証しない。

不一致時は当該checkpointを変更せず、具体的なsafe errorを表示して再開しない。回答、Prompt、reason、evidence本文をerrorへ含めない。

再開元を明示指定した後、run開始前に入力、採点設計、通常評価model、runtime、checkpoint構造の項目別結果をread-onlyで表示する。開始時には同じ条件と完了行内容を再検証する。

### 10.5 再開動作

- 保存済み参照回答は再利用する。
- 完了済み学生行はskipする。
- 最初の未完了行以降だけを処理する。
- 同じ学生行の途中状態は完了扱いにせず、その行を再実行する。
- 完成成功後にpartial fileを削除する。削除失敗は完成fileを無効にせず、明確なcleanup warningを表示する。
- cleanup warningは結果画面へnon-modalに表示し、完成file pathと残存partial pathを示す。確認操作をworkflow条件にしない。
- 新規runの候補pathに既存partial fileがある場合は上書きせず、再開可能性を検査するか、次の未使用suffixを割り当てる。

### 10.6 中断と再開の引き継ぎ

- 停止はpartialを破棄せず、最後のatomic checkpointを再開元として保持する。
- 同一セッションでは「中断した処理を再開準備」の明示操作で再開元を設定・検査する。この操作とpicker選択はAI処理を開始しない。
- 再起動後はnative pickerまたはfull pathの直接入力で`.partial.xlsx`を指定する。picker取消は現在の状態を変更しない。
- checkpointの入力またはmodelへ合わせる操作は利用者が明示的に選ぶ。採点設計およびruntimeは自動変更しない。

## 11. UI workflow

アプリは4 stepを維持する。

1. **入力**
   - native pickerまたはpath入力
   - sheet、質問文行1/2、回答行
   - 対象設問、有効化、主回答列、設問text、必須エラー。補助列・候補詳細等は同じ対象の設定へ移動
   - 実行の準備（§11.15）: Copilot loginの明示開始・取消・状態再確認、checkpoint再開の準備と再開条件の検証、技術検証エラー
2. **採点設計**
   - base、special、question points、similarity weight
   - 均等配分
   - Knowledge / Prompt / Custom evaluatorとcriteria、固有項目の有効設定概要と詳細設定への入口
   - 読込Promptの件数と適用の入口（設定の「読込Prompt」は廃止、§11.23・A-MG-02）
   - formula／capacity preflight
3. **実行**
   - 通常modelの実効選択とその上限の既知／不明、次回runの思考レベル・Context Size（選択欄は共通設定、§11.18。Copilot loginとcheckpoint再開の準備は入力画面、§11.15）
   - 実効output／partial pathと設定変更への入口
   - 参照生成、通常評価、固有評価、類似度、finalizationの段階表示
   - completed rows / total rows、in-flight、error、cancel
   - 「実測」の直下に、Excelの学生行と1:1の速報値一覧（対象の文字列・Prompt・定量値、§11.12）
   - 実行開始の判断結果と、「定量化を開始」「中断」
4. **結果**
   - 完了／一部失敗／取消を明示
   - 見出し行に、今回のジョブの総実行時間（`HH:MM:SS`）とAIクレジット（§11.21）
   - finalまたはpartial path
   - 一覧（プレビュー）では行ごとのFinalScore、SpecialEarned、SimilarityPenalty、行状態だけを示し、設問別得点は示さない（FR-RS-06）。詳細で、選択行のFinalRawと設問別のQuestionEarnedを、設問IDでなく元のExcel由来の設問文で示す（§11.11）
   - 詳細で、選択した基準について設問文と学生の回答（元のExcel）、AIの点・理由・根拠・根拠の場所・評価項目の説明（§11.19）
   - criterion overrideの確認と、override反映版を既存fileへ上書きせず任意の別workbookとして出力する操作

### 11.1 完了表示

全処理とfinal validationが成功した場合、次を表示する。

- 「処理が終了しました」
- 完成file path
- 対象学生行数、成功、空回答、技術失敗件数
- total similarity penaltyとFinalScore preview
- 元本が不変であること

技術失敗を含む場合は成功と誤表示せず、「処理は完了しましたが、空欄の評価があります」と表示する。

### 11.2 教育倫理warning

次の文面をshell rootのpersistent non-modal bannerとして全stepと設定画面で常時全文表示する。

> 生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません

- 文言を省略、要約、言い換えない。
- warningは設定値、snapshot、score、statusへ保存しない。
- checkbox、同意、dismiss、role、期限を要求しない。
- warning操作をrun、cancel、resume、formula、finalizationの条件にしない。

### 11.3 OS-installed PATH CLIによるlogin開始（D-06採用済み、2026-11-05変更）

1. loginは、利用者が「GitHubにログイン」を押した場合、または§11.10（FR-AL-03）の起動時自動loginの場合だけ開始する。画面遷移、Prompt適用、設定の読込／保存／適用、手動の認証状態確認から暗黙に開始しない。
2. §3.2の規則でOS利用者がPATHに導入したCLIを解決し、そのCLIの`login` subcommandを直接子processとして起動する。Windowsは絶対PATH entryにある`copilot.exe`、Unix source foundationは実行可能な`copilot`を対象とし、空・相対PATH entryを探索しない。bundled CLI、manifest、固定CLI版へ切替・fallbackしない。shell command文字列、PowerShell、`cmd /c`、任意command実行を介さない。
3. 認証用console／ブラウザーとcredential保管はCLIに委譲する。アプリはtokenやdevice code等を解析・収集・保存せず、引数・標準入力・application logへ渡さない。独自OAuth、token入力UI、WebView、callback serverを追加しない。
4. loginの二重開始と評価実行中のlogin開始を防ぐ。取消・失敗時もGUI、Excel読込、mapping、設計編集を利用可能に保ち、safeな理由と再試行操作を表示する。
5. login子processを取消・強制終了するのは利用者のlogin取消またはアプリ終了時だけ（起動時自動loginも同じ）とし、アプリが開始・所有した当該login processだけを終了・解放する。process tree全体や名前一致で一括killせず、ブラウザー、他のCLI、credential storeに触れない。credentialの削除、logout、失効を行わない。
6. login完了後は認証状態とmodel一覧を再確認する（完了時の自動再確認、および取消・失敗後の既存「Copilot 状態を確認」）。process起動・終了codeだけを認証成功とせず、自動model選択変更やAI評価開始を追加しない。
7. login前後に観測したCLI version／file hashは診断にだけ記録し、固定CLIとの一致やmanifestを信頼判定に使わない。CLIの更新はOS利用者が管理する。CLI欠落時はOSへのインストールとPATH設定（WindowsではWinGetを推奨）を案内し、SDK互換性がない場合は明示的なruntime errorを表示する。bundled CLIを選択せず、hashによる信頼attestationを行わない。

### 11.4 主画面と設定の分担

- 固定の「1 入力 / 2 採点設計 / 3 実行 / 4 結果」を維持する。設定は同一ウィンドウ内の独立した内容画面であり、第5ステップ、modal、drawerではない。
- 主画面は対象、配点、実行の開始判断、結果を扱い、有効値と変更入口を残す。「変更」から同じ対象IDの設定（共通）の入口へ1操作で移動し、「設定から戻る」で元ステップへ復帰する。同じ入力欄を二重配置しない。
- 設定は「共通」だけとする（2026-11-02変更、§11.23。旧: 「共通」「入力詳細」「通常評価」「固有評価」「読込Prompt」の5カテゴリ）。共通内の定義名・revision・丸めはDesignの採点定義に属し、独立したカテゴリや実行設定へ移さない。
- 旧「通常評価」（既存evaluator／criterionのCRUD・range・weight・Knowledge読取・Custom編集、設問タブ〔§11.22〕）は、「2 採点設計」の統合画面（§11.23）で扱う。旧「入力詳細」「固有評価」「読込Prompt」は設定から削除する（§11.23）。結果とoverrideは設定へ移さない。
- 設定は共通だけで、対象がない入口は位置を保ち、利用できない理由を表示する。設定内の表示切替でworkflowを進めず、前後操作は行先を明示する。各stepの「設定済み」表示（§11.16）と準備完了・処理成功を区別し、結果画面に無効な最終ステップ主ボタンを残さない。
- 既存FluentThemeと必要な静的Fluent System Iconsだけを使い、常時見える日本語ラベルを併記する。素材・revision・LICENSE／NOTICEは採用時に記録し、新UI framework、renderer、NuGet依存、テーマ切替を追加しない。

### 11.5 通常表示・ページ切替・例外到達

- 最小1024×720 DIP、初期1180×800 DIPを維持し、寸法を引き上げて達成扱いにしない。Window指定値と実ClientSizeを分けて実測する。
- 最小サイズ以上の通常画面は外側スクロール不要とし、初期offsetで警告全文、現在地、主要操作、有効値、前後移動が実viewport内に完全包含されることを要求する。外側ScrollViewerを残す場合は`Extent <= Viewport`を確認し、scrollbarの非表示やclippingだけを合格にしない。
- 多数の設問・結果はコンパクト一覧とページ切替で扱い、前／次、表示範囲、全件数、結果の元行番号への移動を提供する。表示件数は残領域・実際の行高から決め、業務上限にしない。有限高さとvirtualizationを維持し、全件Control生成を避ける。
- 追加・削除・並替え・リサイズ時にページ範囲を補正し、対象が残る限りIDで選択を保持する。削除時だけ隣接対象へ移動する。空一覧を明示し、多数のevaluator／criterionは選択詳細で編集する。複数エラーも件数・対象・次の問題への移動を示し、隠して検証成功にしない。
- 長文の設問text／Prompt、全文path、dropdown候補には局所スクロールを許容し、keyboardで全文へ到達可能にする。760×600 standaloneと200%表示はreflow・表示行数削減を先に行い、不足時だけ本文の縦スクロールを許容する。固定領域で操作やfocusを覆わず、通常サイズの非スクロール成功へ算入しない。
- 本文・入力14 DIP、主操作target最小44 DIPを維持する。MinHeightは下限であり実高さの固定値ではない。余白は4／8／12／16を基準に重複を減らし、未測定の固定表示件数を約束しない。
- Tab／Shift+Tab／Enter／Space、設定内の表示切替後と設定終了時のfocus復帰、日本語Accessible Name、文字／icon／focusのcontrast、色以外の状態表示を検証する。Automation IDは既存の安定IDを可能な限り保ち、対象ID＋操作名等で一意にする。ページ連番・表示名を識別子にしない。
- native Windowsの実DPI／Narrator確認とheadless測定を区別する。論理作業領域が最小window未満の環境で無条件に収まることや、WCAG適合認証を保証しない。

### 11.6 設定fileの読込と明示保存

- 保存先はOSのLocalApplicationData配下`StudyReportEvaluator/setting.txt`とする。Windowsでは通常`%LOCALAPPDATA%`配下であり、EXE、入力、出力、抽出cache、CLI credential storeと分離する。新しい製品CLI引数・必須環境変数を追加しない。
- UTF-8 JSON、設定schema整数1で、共通設定（通常model希望ID、並列度1〜16・既定8、absoluteな明示出力先または`null`、model別の思考レベル・Context Size希望`modelPreferences`〔FR-MO-04〕、表示専用のmodel一覧cache`cachedModels`〔FR-MS-05〕）と任意の採点定義1件だけを保存する。設定schemaはcanonical schema・要求版・製品版とは独立する。
- 採点定義はID・name・revision、sheet／header／行範囲、base／special／similarity係数、設問text・mapping・Points・enabled、evaluator／criterion／range／weight、固有評価、適用済みPrompt、丸めを含む。ID・順序・decimal・Unicode・改行・Promptとcanonical hashの往復一致を要求する。
- 保存しないものは入力xlsxのpath・bytes、回答行を自動収集した本文、AI結果・reason・evidence・参照回答、run／checkpoint状態、credential・login状態・CLI hash、warning承認状態、未適用Prompt一覧・本文、Control・Command・選択ID・表示ページ・操作履歴とする。平文に含まれ得る内容は§14に従う。
- 有効な編集は次回用draftへ反映するが、diskへの書込は利用者の明示保存だけとする。起動・読込・主列変更・画面遷移・終了で自動保存しない。fileなしは既定値で継続し、最初の明示保存（または次の例外による自動保存）までfileを作らない。設定読込完了前に未読の保存定義を空で上書きできないようにする。
- 唯一の例外として、表示専用の`cachedModels`は、認証・model一覧の確認が成功し、一覧が保存済みcacheと異なる場合だけ自動保存する。この自動保存では保存済みfileを読み直し、`cachedModels`だけを差し替える。未保存のdraft・`modelPreferences`の編集・その他の共通設定を混入しない。fileがない場合はこの時点で作成する。読込失敗・破損・未知版のfileには書き込まず、4096件を超える一覧は保存しない（FR-MS-05）。自動保存の成否は明示保存の状態表示と分けて示す。
- BOMあり／なしを受理し、型・必須値・enum・範囲、schema欠損／不正型／未知版を検査する。同schemaの未知項目も拒否し、黙って捨てて再保存しない。破損・未知版・読込拒否でも元fileを保持し通知してoffline編集を継続する。自動修復・移行・削除を行わない。
- 明示保存前に共通値と、保存対象にある定義・Prompt・配点を既存validatorで検証する。不正draftと旧fileを保持し、入力未読込で共通設定だけを保存する場合は読込済みの保存定義を消さない。
- 保存時点の値を固定し、同じdirectoryの一意tempへwrite／flush／close後にatomic置換する。先に旧fileを削除したり、直接切り詰め書込したりしない。失敗時は旧file・現在draftを保持し、自分のtempだけを後始末する。
- 「未保存」「保存中」「保存済み」「保存失敗」を実際の結果で区別する。保存中に再編集した現在draftは保存成功後も未保存とする。同一画面の二重保存を防ぎ、別processでは最後に成功した保存が優先する。merge・監視・履歴を追加せず、電源断・任意network filesystemまで無条件の耐久性を保証しない。
- production構成でのみ保存場所を解決する。自動testは一時directoryのabsolute pathをstoreへ渡し、単体VMの既定生成から実利用者設定へアクセスしない。汎用filesystem抽象を追加しない。

### 11.7 保存定義の明示適用

- 起動時は保存定義を保持するだけで自動適用しない。Excel読込後、保存済みsheet・行範囲・mapping・設問数を示し、利用者が「現在の入力に適用」を明示した場合だけ適用する。run中の一括適用は無効にして理由を示す。
- 保存定義のheader行に対応したmetadataを既存read-only loaderから取得し、sheet・行・列・定義全体を検証する。以前のheader metadataを流用しない。
- 成功時だけInput metadata・選択値・draftとDesignを一緒に更新する。失敗・取消では現在の状態と保存fileを変更しない。
- 保存したID・順序・設問text・Prompt・配点を保持し、候補再生成や別header値への暗黙置換を行わない。その後に利用者が主回答列を変更した場合だけ、従来の§4.3の設問text同期を行う。
- Imported Promptの一覧・本文・順序を変更・消去せず、未適用Promptを定義へcopyしない。別Excelの意味的な適合は利用者が確認し、列・sheetの存在だけで授業内容一致やcheckpoint再開可能とは判定しない。

### 11.8 往復・次回設定・現在runの分離

- 同一起動中の値・対象ID・ページ・設定内の表示（共通のみ）・入力途中の編集状態を往復で保持する。Input／Designが既存draftを所有し、設定はその編集面を束ねる。最新の編集元を1回同期し、古いcopyや再生成前のDesign参照で上書きしない。Undo／Redo engineや第三の汎用draft storeを作らない。
- 共通設定は今回の明示編集 → 保存済み共通設定 → 従来既定値の順に使い、出力先は§9.1に従う。保存modelの希望IDと実効選択を分離し、利用者の明示状態確認で候補に存在するときだけ実効選択へ反映する。
- 希望modelが利用不可なら実効選択を未選択にして明示変更を要求し、別modelへfallbackしない。確認失敗だけで保存希望IDを消さない。希望ID未指定の初回は従来の明示確認後の初期選択を維持する。固定`auto`の別availability検証は行わない。
- 画面遷移、Prompt／保存定義の適用、設定読込・保存から認証確認・login・AI runを自動開始しない。login完了時は、§11.3の6に従って認証状態とmodel一覧の再確認だけを自動で行い、loginの再開始やAI runは自動開始しない。起動時の自動確認・自動loginは§11.10に従う。開始requestには実際に利用可能と確認した値だけを入れる。
- run開始時にrequest／immutable snapshotを固定し、実行中も戻る・次回用編集を許可するが現在runを再構成しない。進捗入口・停止・予約済みpathを全画面で利用可能にし、現在runと次回条件を別表示する。設定表示中にrunが完了しても設定を閉じず、結果到着だけを通知する。
- 同じ入力・定義の単なる往復では新規／再開・partial指定・進捗・直前runを初期化しない。入力／定義を変更した場合だけ次回準備を更新し、再開指定を再確認または解除して理由を表示する。新規／再開・partial指定は設定fileへ永続化しない。
- 結果は前回runとして保持し、次回draftを過去結果へ混入させない。結果の一覧／詳細は追加ステップではなく、ページ移動後も元Results collectionのoverrideを保持する。0とblank、完成版と未保存修正版、予定名／予約済み／保存中／完成を実データから区別し、未実行scoreや未完成fileを生成済みとして表示しない。

### 11.9 実行コストの表示とジョブログ

- 「3 実行」「4 結果」でExcelを開かずに、今回のAI処理で観測できた使用量を確認できる。対象は開始操作ごとのジョブ1件で、入力・出力・推論・キャッシュのtoken数、SDKが報告した`nano-AI units`、premium request消費量、観測状態を示す。
- 通常評価・参照回答生成・固有評価と、それぞれの再試行を対象とする。類似度評価はローカル計算（§7.5）でAIを呼ばないため、使用量は発生しない。成功・技術失敗・取消・timeout・出力失敗、checkpointへ保存されなかった行で観測できた値も今回ジョブに残す。
- 取得できない値は理由とともに未取得として示し、0や推定値へ置き換えない。明示的な0、未送信、送信状況不明、最後の呼び出しのみの部分取得を区別する。
- 単位はSDK報告の原単位を保持し、通貨へ換算しない。AIクレジットはnano-AI unitsからの表示時換算だけを§11.21（FR-CR-01〜04）に従って示す（2026-10-01変更: 旧規定「換算根拠を確認できるまでAIクレジットの数値を表示しない」を置き換えた）。
- 同一attemptの累計は置換で更新し、イベント合計とセッション累計、モデル別内訳とセッション総量を加算しない。内訳と総量が一致しない場合は不一致として示し、配賦・補正で一致させない。
- 項目ごとに取得元（イベント／最終RPC／最後の呼び出しのみ）と部分取得を保持する。再試行は別attemptとして数え、試行番号・相関ID・終端結果を記録する。
- ジョブ単位のUTF-8 JSON Linesログを利用者別のローカル領域へ作り、数値・生成ID・閉じたコードだけを記録する。回答・Prompt・reason・evidence・学生識別子・実path・credentialを記録せず、モデル名は匿名化した識別子だけを保存する。ジョブcontextとattemptごとに、アプリが指定したrun-level reasoning effort（例: `low`、未指定はnull）を記録する。
- 同じ秘匿規則で、ジョブのテキストログを出力先のresult directoryへ`{yyyyMMddHHmm}-log.txt`としても保存する（FR-108、§11.24）。
- ログの保存失敗・記録欠落・容量上限は採点と分けて表示し、観測済みの画面表示を消さない。コスト取得の失敗だけでAIを再送しない。
- 本節は既存Excelの使用量出力とcheckpoint schemaを変更しない（checkpointへのContext Size tierの追加は、後続のFR-MO-07による）。過去ジョブの累計表示・再取込は本版の対象外とする。

### 11.10 起動時の自動Copilotログイン（2026-09-30追加）

出典: 依頼原文（2026-09-30「アプリケーションの起動時に、ユーザーがPCやMacなどにログインしているアカウントで、GitHub Copilot CLIに自動的（可能な限りユーザーが何もしなくてもいいように）にログインしてください」）、[Authenticating GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/authenticate-copilot-cli)（確認日2026-09-30）。§3.4・§11.3の「起動時に自動開始しない」を本節が置き換える。

**既存ログイン**とは、PATHから解決したOS-installed CLIが当該OS利用者の環境で利用する認証状態を指す。認証の取得・保存・解決はCLIへ委ね、アプリはtoken等の値を読み取らず、既存の認証確認（`UseLoggedInUser = true`、`GitHubToken = null`）を用いる。2026-09-30の出典は当時の自動login依頼を示す履歴であり、CLIの配布・固定版に関する現行要件は本節の2026-11-05変更を優先する。

| ID | 優先度 | 要求 |
|---|---|---|
| FR-AL-01 | MUST | メインwindowが最初に表示された直後（Openedイベント）に、1アプリ起動につき1回だけ、既存の「Copilot 状態を確認」と同じ認証確認を自動で開始する。利用者操作を要さず、GUI表示・入力操作を待たせない（非同期）。起動引数の有無、Prompt適用の有無によらず同じである。起動引数の検証エラーwindowでは開始しない。 |
| FR-AL-02 | MUST | 認証確認が`Available`の場合、login子process・ブラウザー・入力欄を出さない。認証状態とmodel一覧を反映し、login状態文へ「この PC で利用中の GitHub アカウントで自動的にログインしました。」を表示する。 |
| FR-AL-03 | MUST | 認証確認が`AuthRequired`（PATHから解決したCLIは正常に動作したが利用できる資格情報がない）の場合だけ、§11.3と同じ経路（OS-installed CLIの直接起動）でloginを1回自動開始する。開始時は「利用できる GitHub ログインが見つからないため、自動ログインを開始します…」を表示し、完了後は§11.3 6の自動再確認でmodel一覧を更新する。CLI不在、SDK非互換、`RuntimeFailed`、`Cancelled`ではloginを開始せず、CLI不在時はインストールとPATH設定を案内し、その他は「起動時の自動ログイン確認を完了できませんでした。「Copilot 状態を確認」で再試行してください。」を表示する。 |
| FR-AL-04 | MUST | 自動loginが取消・失敗・終了しても、同じアプリ起動中に自動再確認・自動再loginをしない。以後は利用者の「Copilot 状態を確認」「GitHubにログイン」操作だけとする。利用者が起動直後に手動で認証確認・login・runを行い自動確認を開始できない状態のときは、自動確認を行わず、後から再開もしない。 |
| FR-AL-05 | MUST | 自動loginは、§11.3の2〜7（shell非使用、二重開始・評価中開始の防止、「ログインを取り消す」での取消、取消／アプリ終了時の所有process限定終了、PATH CLIの一貫利用、identity診断のみ）をすべて満たす。 |
| FR-AL-06 | MUST | 自動処理は認証確認とlogin開始だけを行う。AI評価・run開始、Prompt適用、自動model選択変更（既存の認証確認後のmodel選択規則を超える変更）、別modelへのfallbackをしない。 |
| FR-AL-07 | MUST | 自動確認・自動loginの成否によらず、GUI、Excel読込、mapping、設計編集を利用可能に保つ。失敗はsafeな理由と再試行操作（「Copilot 状態を確認」「GitHubにログイン」）で表示し、CLI不在時はOSへのインストールとPATH設定（WindowsではWinGetを推奨）、SDK非互換時はruntime incompatibility errorを明示する。例外内容・path・token・device codeは表示しない。 |
| FR-AL-08 | MUST | 環境変数`STUDY_REPORT_EVALUATOR_AUTO_COPILOT_LOGIN`が前後の空白を除いて`0`、または大文字小文字を区別せず`false`のとき、FR-AL-03の自動login開始をしない（FR-AL-01の認証確認は行い、`AuthRequired`時は「利用できる GitHub ログインが見つかりませんでした。「GitHubにログイン」を押してください。」を表示する）。未設定・空・その他の値は自動login有効とする。この環境変数は任意の抑止手段であり、必須ではない。 |
| SEC-AL-01 | MUST | アプリはtoken、password、device code、`COPILOT_GITHUB_TOKEN`／`GH_TOKEN`／`GITHUB_TOKEN`の値、`gh`のtoken出力を読取・解析・保存・log出力・引数／標準入力への受渡しをしない。アプリは`gh`を起動せず、独自OAuth・token入力UI・WebView・callback serverを追加しない。 |
| SEC-AL-02 | MUST | credentialの削除・logout・失効、ブラウザー・他CLIの終了をしない。PATHから解決したOS CLI以外（同梱CLIを含む）を選択・fallbackせず、CLIのSHA-256を信頼attestationとして使わない。 |
| SEC-AL-03 | MUST | 自動確認・自動loginで送信し得るのはCLIによるGitHub認証通信だけとする。学生回答・Prompt・workbook pathを送らず、AI送信は0件である。 |
| NFR-AL-01 | SHOULD | 認証確認が失敗・timeoutしても（既定15秒）UI操作を妨げない。offline時はGUI・Excel読込・mapping・設計を従来どおり利用できる。 |

境界・例外:

- 環境変数のtokenは、CLI仕様上、保存済みOAuth tokenより優先される。無効なtoken（例: 非対応の`ghp_`classic PAT）が設定されている場合は、自動loginで別のログインをしても認証確認が`AuthRequired`のままとなり得る。この場合は環境変数の解除をtroubleshootingへ案内する。
- 期待動作の例: ①既存ログインあり → 起動後数秒でExecution画面の状態が「既存の Copilot CLI login を利用できます。」、login process起動0回。②資格情報なし → 起動後にブラウザー認証が1回始まり、承認後にmodel一覧が更新される。取消後は再自動起動しない。③`STUDY_REPORT_EVALUATOR_AUTO_COPILOT_LOGIN=0`で資格情報なし → ブラウザーは開かず、手動ボタン待ち。

### 11.11 結果画面の設問文表示（2026-09-30追加）

出典: 依頼原文（2026-09-30「[4.結果・出力]の画面で、questionの文字列は、元のExcelのデータを表示してください。どの文字列が評価されたのかを私が理解しやすくするためです。元の学生のレポートを見ないと判断ができません。」）、`src/StudyReportEvaluator.App/ViewModels/ResultsOutputViewModel.cs`の従来表示（設問ID `question-<hash>: 2`）。

従来の「設問別得点」は内部の設問ID（例: `question-0050af69578d41da82063aa8f88746ab: 2`）を表示し、どの設問の点か判別できなかった。本節はこれを、run開始時のimmutable snapshotが持つ設問文へ置き換える。§4.3の9により、設問文は入力Excelの質問文行と主回答列が交差するセルの値で初期化され、利用者が手動編集した場合はその値である。

| ID | 優先度 | 要求 |
|---|---|---|
| FR-RS-01 | MUST | 「4 結果」の選択行の詳細の「設問別得点の全文」（`ResultsQuestionEarnedFull`）は、run snapshotの有効な設問ごとに`<設問文>: <獲得点>`を、snapshotの設問順に区切り文字` · `（半角空白・U+00B7・半角空白）で連結して表示する。設問ID・表示名（例: `質問 1`）を設問文の代わりに表示しない。（2026-10-01変更: 表示先から一覧の「設問別得点」列を除いた。FR-RS-06） |
| FR-RS-02 | MUST | 詳細の設問別得点で使う`<設問文>`は、snapshotの設問文（原文）から、連続する空白（半角・全角空白、タブ、CR、LFを含むUnicode空白）を1個の半角空白へ置換し、前後の空白を除いたものとする。省略記号への短縮や文字数上限を設けず、詳細の欄内で折り返して全文を表示する（欄の高さを超える分は欄内の縦scrollで確認できる）。`<獲得点>`は従来どおり（有効桁29の不変カルチャ表記。未確定・未評価は`—`）とする。 |
| FR-RS-03 | MUST | 設問文が空または空白のみの有効な設問（通常は入力検証で発生しない）は、その設問の表示名を`<設問文>`の代わりに使う。表示名も空白のみの場合は設問IDを使う。 |
| FR-RS-04 | MUST | 選択行の詳細で、選択した基準の回答欄（§11.19 FR-RV-01の左側の欄）の先頭に「設問文（元のExcel）」として、当該設問の設問文を原文のまま（改行を保持し、正規化・短縮しない）折り返して全文表示する。（2026-10-01変更: 表示位置を「編集領域の先頭」から回答欄の先頭へ変更） |
| FR-RS-05 | MUST | 本表示は読取専用であり、採点値、override、Excel出力、checkpoint、AI送信内容、ジョブログ、設定ファイルを変更しない。設問文をapplication logへ記録しない（§14）。 |
| FR-RS-06 | MUST | 「4 結果」の一覧（`RowScoreList`、詳細を開く前の学生行のプレビュー）は、見出し・各行とも左から「元の行」「最終点」「固有点」「類似減点」「状態」の5列だけを、この順に表示する。設問別得点（見出し「設問別得点」、設問文・獲得点の連結文字列）を一覧の見出し・行・tooltipに表示しない。設問数が増えても一覧の列数は5のままとする。選択行の設問別得点は詳細の`ResultsQuestionEarnedFull`（FR-RS-01）で確認する。本要求は表示だけを変更し、`QuestionEarned`の計算・Excel出力・checkpoint・overrideを変更しない（FR-RS-05）。（2026-10-01追加: 依頼原文「[4.結果・出力]画面の、[設問別得点]は、削除してください。プレビューで表示するには情報量が多すぎるためです。」、A-RS-04） |

例（設問文が`レポート課題1：\n機械学習とは何か`と`課題2：活用例`の2問で、獲得点が2と未確定）: 詳細の設問別得点は`レポート課題1： 機械学習とは何か: 2 · 課題2：活用例: —`。一覧の当該行は`元の行 / 最終点 / 固有点 / 類似減点 / 状態`の5値だけで、この文字列を含まない。

境界・例外: 別設問が同じ設問文を持つ場合も設問順で並べ、統合しない。設問文に` · `や`:`を含む場合も加工しない。学生の回答本文と、評価の理由・根拠は§11.19で詳細に表示する（2026-10-01にA-RS-01の「回答本文を表示しない」を覆した）。

### 11.12 実行中の速報値表示（2026-09-30追加）

出典: 依頼原文（2026-09-30「実行中の画面で、現在、どの文字列を、どんなPromptで、どう定量化したのかを、画面右側の中央あたりの{実測}の下に表示してください。元のExcelデータの1行と1:1になるように速報値として表示をしてください。全件を表示します。」）、`src/StudyReportEvaluator.App/Views/ExecutionView.axaml`の現行「実測」表示（確認日2026-09-30）。

**速報値**とは、AIまたはローカル類似度計算が返した生の定量値（通常評価のcriterion `RawScore`、固有評価の`Score`、類似度の`Similarity`）をExcel数式の計算前に写した表示である。配点、QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScoreはExcel数式が最終workbookで計算する値であり、速報値に含めない（A-LP-02）。**対象の文字列**とは、当該評価項目が使う選択済みの主回答列・補助列の当該行のセル値である（A-LP-01）。

| ID | 優先度 | 要求 |
|---|---|---|
| FR-LP-01 | MUST | 実行画面の右列で、「実測: 参照 a / b · 行 c / d」の直下に「速報値」領域を常時配置する。領域は、行一覧（AutomationId `ExecutionLivePreviewList`）と、選択行の詳細（読取専用TextBox、AutomationId `ExecutionLivePreviewDetail`）からなる。run未開始・入力変更後は行一覧を空とし、詳細に「実行を開始すると、Excel の行ごとの速報値をここに表示します。」を表示する。 |
| FR-LP-02 | MUST | 行一覧は、当該runが対象とする学生行（回答行範囲の先頭行〜末尾行）とExcel行番号昇順で1:1に並べる。1行=1件で、間引き・件数上限・ページ外の欠落を作らない（回答行上限20,000行まで）。全件に到達でき、仮想化された有限高さのlistとする。run開始後、事前検査を通過して評価計画が確定した時点で、全行を「待機中」として一覧化する。参照回答は行に対応しないため一覧に含めない（A-LP-03）。 |
| FR-LP-03 | MUST | 行一覧の各件は2〜3行で、1行目に`Excel 行 {SourceRowNumber}`、2行目に`{状態} · 項目 {完了数} / {総数}`、3行目に値の要約を表示する。状態は「待機中」「評価中」「完了」「未処理」「中断」「再開前に完了」のいずれかである。項目は、その行の通常評価（設問×evaluator）、固有評価、類似度の各1件で、総数はその合計、完了数はAI応答・ローカル計算・空回答判定などで結果が確定した件数である。値の要約は、項目順に`通常 {raw1},{raw2}`（当該evaluatorのcriterion順）、`固有 {score}`、`類似 {similarity}`を` / `で連結する。結果が未確定または値がない項目は`—`とする。数値は不変カルチャーで小数の末尾0を除いた最大4桁とする。例: `通常 8,6 / 固有 0.8 / 類似 0.42`。 |
| FR-LP-04 | MUST | 詳細には、選択した行（未選択のときは、評価中の行のうち行番号が最小の行、評価中の行がなければ最後に更新された行、いずれもなければ先頭行）について、その行の全項目を`通常評価`→`固有評価`→`類似度`の順（各群は設問順・定義順）に、次の3点を必ず表示する。①**対象の文字列**: 使用する各列を`[列 {列ID} · 主回答]`／`[列 {列ID} · 補助]`の見出しつきでセル値の全文（FR-LP-08の上限まで）。②**Prompt**: AIへ送信した完成Prompt（テンプレート展開・app-owned契約文を含む送信文そのもの）。類似度は「なし（ローカル計算のためAIへ送信しません）」とし、比較文字列として参照回答を表示する。③**定量化の結果（速報値）**: 通常評価は`{criterion表示名}: {RawScore}（範囲 {min}〜{max}）`と理由・根拠、固有評価は`固有評価スコア: {Score}（範囲 0〜1）`と理由・根拠、類似度は`類似度: {Similarity}（範囲 0〜1）`と理由。項目見出しに`状態`（待機中／AI評価中／成功／空回答／未実行／取消／失敗）を付ける。 |
| FR-LP-05 | MUST | 表示は項目単位で更新する。Promptを組み立ててAI呼出しを始めた時点で対象の文字列・Promptと「AI評価中」を、AI応答の検証が終わった時点で結果を、行の完了を待たず反映する。空回答は対象の文字列を空のまま`空回答: AIへ送信せず0点相当`、固有配点0の固有評価は`未実行: 固有配点0のためAIへ送信せず`、技術的失敗は`失敗（{statusコード}）: 値は空欄`とし、失敗を成功値や0として表示しない。 |
| FR-LP-06 | MUST | checkpointから再開したrunでは、再開前に完了済みの行を「再開前に完了」として一覧に含め、checkpointに保存済みの定量値をFR-LP-04③と同じ形式で表示する。回答本文・Promptはcheckpointに保存しないため、①②は`（再開前に完了した行のため、対象の文字列とPromptは保存されていません）`とする。 |
| FR-LP-07 | MUST | runが完了・取消・失敗で終わったとき、AI呼出し中または一部だけ結果が出ていて確定しなかった行を「中断」（再開時に最初から評価する）、未着手の行を「未処理」とする。確定済みの行は「完了」のままとする。表示は、次のrun開始、入力・採点定義の変更・入力未選択化、アプリ終了までは保持し、その時点で破棄する。 |
| FR-LP-08 | MUST | 詳細・要約・行一覧の各文字列（対象の文字列の各セル値、Prompt、比較文字列、理由、根拠）は、先頭2,000 UTF-16文字までを保持・表示し、超過時は末尾へ`…（全 {N} 文字中、先頭 2,000 文字を表示）`を付ける。保持文字数の合計が32,000,000文字を超えた後に更新される行は、上限を各200文字へ下げ、同じ形式の注記（`先頭 200 文字`）を付ける。行数の上限や間引きには使わない（A-LP-04）。 |
| FR-LP-09 | MUST | 領域見出しは`速報値（Excel 行ごと・{N} 行）`とし、Nは一覧の件数とする。見出しのToolTipで、AIまたはローカル計算の生の値であり最終評点ではないこと、配点とFinalScoreはExcel数式が計算することを示す。 |
| SEC-LP-01 | MUST | 速報値の本文（対象の文字列、Prompt、比較文字列、AI理由、AI根拠）は、実行中のメモリと画面だけに置く。application log、ジョブ単位JSON Linesログ、checkpoint、setting.txt、出力workbookへ、この表示のために新たに保存しない（出力workbookとcheckpointが既存契約で保持する内容は変更しない）。速報値の型の`ToString()`、例外、AutomationのNameへ本文を含めない。詳細のテキストは利用者が選択・コピーできるが、アプリは自動でclipboardやfileへ書かない。 |
| SEC-LP-02 | MUST | 速報値表示は、既存のAI呼出しの入力と結果を写すだけとし、追加のAI送信、追加の通信、追加のtoken消費、追加の再試行を発生させない。表示の更新の失敗・例外は評価結果、checkpoint、cancel、進捗を変えない。 |
| NFR-LP-01 | MUST | 速報値の更新は、AI呼出し・checkpoint保存・cancelをUIスレッドで待たせない（UIへの適用だけをUI同期文脈へ投げる）。最小1024×720 DIP以上の実行画面で、警告全文・主要操作・「実測」表示（進捗ブロックに縦scrollを出さない）・予約名が従来どおりviewport内に収まり、速報値領域は行一覧・詳細とも高さ44 DIP以上を保って局所scrollで全件に到達できる。1024×720未満の狭小画面では進捗ブロックの局所縦scrollを許容する（§11.5）。速報値領域は要素の日本語Accessible Nameを持ち、行一覧と詳細はkeyboardで到達できる。 |

境界・例外:

- 詳細の表示対象は1行だけであり、全行の全文を同時に展開しない（行一覧が全件、詳細が選択行）。行を選び直すと、その時点の最新の内容を表示する。
- 類似度の対象の文字列は学生の主回答、比較文字列は同一runで生成された参照回答である。参照回答の生成が失敗した設問の類似度は、FR-LP-05の失敗表示（statusコードを含む）とする。
- 期待動作の例: 採点設計が、1設問・通常評価方法1件（基準2件）・固有評価1件・類似度1件の場合（1行あたり3項目）。先頭行が2の5行のsampleでは、run開始直後に`Excel 行 2`〜`Excel 行 6`がすべて`待機中 · 項目 0 / 3`で並ぶ。行2のPrompt組立後、詳細に行2の主回答全文とPrompt全文が`AI評価中`で表示され、応答後に`知識網羅: 8（範囲 0〜10）`と理由が加わり、一覧が`完了 · 項目 3 / 3`と`通常 8,6 / 固有 0.8 / 類似 0.42`になる。

### 11.13 利用できる全modelの選択（2026-09-30追加）

出典: 依頼原文（2026-09-30「私がGitHub Copilotで利用できる全てのモデルを選択できるようにしてください。」）、`src/StudyReportEvaluator.App/Copilot/CopilotAuthenticationService.cs`の列挙処理と`src/StudyReportEvaluator.App/Settings/SettingsFileStore.cs`のcache検証（確認日2026-09-30）、同梱CLI 1.0.79／SDK 1.0.11の`ListModelsAsync`実測（確認日2026-09-30、1 accountで`auto`を含む29 modelを返し、`policy.state`は`enabled`または未設定）、[GitHub Copilot SDK](https://github.com/github/copilot-sdk)のSDK XML doc（`ModelPolicy.State`は「"enabled", "disabled"等」、確認日2026-09-30）。

**利用できるmodel**とは、認証確認が`Available`のとき、同梱CLI経由のSDK `ListModelsAsync`が返すmodelのうち、FR-MS-02の除外に当たらないものをいう。アプリは固定のmodel名一覧・許可リスト・課金区分（premium／無料）・reasoning対応可否による絞込みを持たない。

| ID | 優先度 | 要求 |
|---|---|---|
| FR-MS-01 | MUST | 「通常モデル」の選択肢は、利用できるmodelの全件を、SDKが返した順序で1件ずつ表示する。`auto`を含む。件数の上限で切り捨てず、同一IDが複数返った場合は最初の1件だけを表示する。SDKの新しいmodel IDは、アプリ更新なしで選択肢に現れる。 |
| FR-MS-02 | MUST | 選択肢から除外するのは次の2種類だけとする。(a) IDが不正: null・空・空白のみ・前後に空白・制御文字（`char.IsControl`）を含む・257文字以上。(b) `policy.state`が、大文字小文字を区別しない`disabled`（そのaccountでポリシー上無効）。`enabled`、未設定（null）、`unconfigured`、未知の値はすべて選択可能とする。 |
| FR-MS-03 | MUST | 除外対象のmodelが混在しても、他のmodelの列挙と認証確認を失敗させない。除外により認証確認が`RuntimeFailed`になったり、一覧全体が空になったりしない（除外後に0件なら、通常どおり0件の`Available`）。 |
| FR-MS-04 | MUST | 選択したmodelの上限（prompt／context）とreasoning effortの扱いは§7.1・§11.18に従う。非表示・非対応を理由にmodelを選択肢から外さない。 |
| FR-MS-05 | MUST | setting.txtの`cachedModels`（表示専用cache）の上限は4096件とする。4096件を超える一覧はcacheへ保存せず（既存cacheと保存済み設定を変更しない）、その一覧は選択肢として全件を利用できる。cacheが4096件を超えるsetting.txtは不正として読み込まない。 |

例: SDKが`[auto, claude-sonnet-5, gpt-5.6-sol, grok-4.5(policy未設定), old-model(policy=disabled), "bad id "]`を返した場合、選択肢は`[auto, claude-sonnet-5, gpt-5.6-sol, grok-4.5]`の4件である（`old-model`はFR-MS-02(b)、`"bad id "`はFR-MS-02(a)で除外）。

### 11.14 教師向け手順「設問の詳細」の説明（2026-09-30追加）

出典: 依頼原文（2026-09-30「`## 2. 採点設計`に、{設問の詳細}を押した後の詳細設定の画面の使い方の説明についてもスクリーンショットもつけて記載をしてください。文章は大学や高校の教員が理解できるようにしてください。」）、`src/StudyReportEvaluator.App/Views/QuantificationDesignView.axaml`の`OpenQuestionSettingsButton`（入力詳細の設定を開く、確認日2026-09-30）、[設定ガイド](settings.md)の「入力詳細」。

| ID | 優先度 | 要求 |
|---|---|---|
| FR-DOC-01 | 廃止（依頼 2026-11-02・統合→AC-059） | `docs/getting-started.md`の`## 2. 採点設計`配下に、見出し`### 「設問の詳細」を押した後の画面`で始まる小節を置く。小節は`## 3. 実行`より前にあり、「設問の詳細」が入力詳細の設定画面を開くこと、その画面が配点や有効／無効を決めないことを書く。 |
| FR-DOC-02 | 廃止（依頼 2026-11-02・統合→AC-059） | 小節に、実画面から生成済みの`../images/02-input-mapping.png`を1枚だけ埋め込む。新しいPNGは追加しない（画像一式は8枚のまま）。代替テキストは画像の内容（合成の設問2、主回答列C、補助列D、読取専用の設問文）を説明する。 |
| FR-DOC-03 | 廃止（依頼 2026-11-02・統合→AC-059） | 画面上の実ラベル`設問`、`追加`、`複製`、`上へ`、`下へ`、`削除`、`設問名`、`設問文`、`補助列`、`補助に含める`、`列候補の詳細`、`候補一式を再適用`、`設定から戻る`、`設定を保存`を、場所と役割を対応付けて説明する。 |
| FR-DOC-04 | 廃止（依頼 2026-11-02・統合→AC-059） | 文章は教員向けとし、番号付きの基本手順（設問の確認→設問名→設問文の確認→補助列→設定から戻る）と、注意点（候補一式の再適用で手動設定が置き換わること、追加・複製・削除・並替えで配点が自動調整されないこと、有効／無効と主回答列は「1. 入力」で変更すること、設定から戻るが保存でも破棄でもないこと）を含む。実装が持たない動作（保存の自動実行、AI評価の開始）を主張しない。 |

### 11.15 実行準備部品の入力画面への集約（2026-10-01追加）

出典: 依頼原文（2026-10-01「[3.実行]画面の中の、左型の[GitHub Copilot CLIへのログイン]などの全ての画面コンポーネントを、「1.入力」の画面の中に移動させてください。」）、`src/StudyReportEvaluator.App/Views/ExecutionView.axaml`の従来配置（左列）。

従来の「3 実行」画面の左列にあった部品（以下「実行準備部品」）を、「1 入力」画面へ移す。移動は配置だけの変更であり、各部品の動作・有効条件・状態文・Automation ID・ToolTip・§11.3／§11.10／§10.5の契約は変えない。

実行準備部品（すべて移動対象。追加・省略しない）:

| 区分 | 部品（表示文言／Automation ID） |
|---|---|
| Copilot認証 | 認証状態文（`CopilotAuthenticationStatus`）、「Copilot 状態を確認」（`CheckCopilotAuthentication`）、「GitHubにログイン」（`StartCopilotLogin`）、「ログインを取り消す」（`CancelCopilotLogin`）、確認中の進行表示、ログイン状態文（`CopilotLoginStatus`）、ログイン説明文（`CopilotLoginInstructions`） |
| checkpoint再開 | 「checkpoint から再開」（`ExecutionResumeMode`）、再開元path入力（`ExecutionResumePartialPath`）と「参照…」（`ExecutionPickResumeCheckpoint`）、選択状態文（`ExecutionResumePickerStatus`）、中断した処理の概要（`ExecutionInterruptedRunSummary`）と「中断した処理を再開準備」（`ExecutionResumeInterruptedRun`）、「再開元を確認」（`ExecutionValidateResumeCheckpoint`）、「中断時の入力を読み込む」（`ExecutionApplyCheckpointInput`）、「中断時のモデルを選ぶ」（`ExecutionApplyCheckpointModel`）、再開検証の状態文（`ExecutionResumeValidationStatus`）、再開条件の一覧（`ExecutionResumeFindings`）と詳細（`ExecutionResumeFindingDetail`） |
| 実行前の判断 | 新規run／再開の出力方針文（`OutputModeSummary`）、技術的な問題の一覧（`ExecutionTechnicalErrors`）と詳細（`ExecutionTechnicalErrorDetail`）を含む`ExecutionValidationSummary` |

| ID | 優先度 | 要求 |
|---|---|---|
| FR-PREP-01 | MUST | アプリのメインwindowの「1 入力」画面に、「実行の準備を開く」button（`InputTogglePreparation`、ファイルpath行の右端）を表示する。押すと、同じ画面内で範囲・設問・検証の区画を上表のすべての部品の区画へ置き換え（button文言は「入力に戻る」）、もう一度押すと元に戻る。部品の表示名・Automation IDは従来と同一で、開閉で入力値・選択・ページを失わない。初期状態は閉じている。部品が対象外の状態（再開モードでない、中断runがない、技術的な問題がない）で従来から非表示のものは、同じ条件で非表示とする。 |
| FR-PREP-02 | MUST | 「3 実行」画面には上表の部品を1つも表示しない（Automation ID・名前付きcontrolとも0件）。「3 実行」は、実行条件の要約と「変更」、次回の出力先と実行予定・進捗・実測・速報値・予約名・コスト、「定量化を開始」「中断」、前後移動を従来どおり表示する。 |
| FR-PREP-03 | MUST | 入力画面の実行準備部品は、メインwindowが保持する単一の`ExecutionViewModel`へ結び付く。入力画面で行った認証確認・login・再開元指定・再開検証は「3 実行」の判断（「定量化を開始」の可否、実行条件）へ反映され、画面を往復しても状態（checkbox、path、選択した再開条件）を失わない。 |
| FR-PREP-04 | MUST | 入力workbookの読込・範囲・設問編集の既存操作、最小window（1024×720 DIP）での表示と到達性を損なわない。実行準備部品は「1 入力」の本文内に置き、本文の高さ（450 DIP）を増やさず、§11.5の「通常画面は外側スクロール不要」を維持する。そのため既存の入力部品（範囲・設問・検証）と同時には表示せず、FR-PREP-01の切替で同じ本文領域を共有する。ファイルpath・読込・切替buttonは常に表示する。 |
| FR-PREP-05 | MUST | 画面表示だけでは認証確認・login・AI評価・設定fileの保存を開始しない。自動確認・自動login（§11.10）は、どの画面が表示中でも従来どおり1アプリ起動につき1回である。入力画面が表示されていない間もloginの取消・再確認の状態は保持される。 |
| FR-PREP-06 | MUST | 「1 入力」を開いたときの初期focusは従来どおりファイルpath入力とし、「3 実行」の初期focusは実行画面内の操作可能な最初の部品とする（移動した部品へは移さない）。Tab順は、既存の入力部品の後に実行準備部品が続く。 |
| FR-PREP-07 | MUST | 入力画面を単体で（メインwindowの`ExecutionViewModel`なしに）表示した場合は、実行準備部品を表示せず、従来の入力部品の配置・寸法を変えない。 |

境界: 実行中（`IsRunning`）の有効・無効条件、run中にloginを開始できない条件、再開モードの編集可否は従来の`ExecutionViewModel`の判定をそのまま使う。移動で新しい入力検証・永続化・外部通信を追加しない。

### 11.16 step状態の表示名「設定済み」（2026-10-01追加）

出典: 依頼原文（2026-10-01「画面上部の[訪問済み]の表現を[設定済み]に変更してください。」）。

| ID | 優先度 | 要求 |
|---|---|---|
| FR-STEP-01 | MUST | 画面上部の4つのstep button（「1 入力」「2 採点設計」「3 実行」「4 結果」）で、現在表示していないが過去に表示した（従来「訪問済み」）stepの状態文を「設定済み」と表示する。「現在・選択中」「未着手」の表示は変えない。 |
| FR-STEP-02 | MUST | 状態文は、stepのbuttonのAccessible Name（`InputStep.AccessibleName`等）にも含まれ、画面上の表示と同じ「設定済み」とする。画面内・利用者向け文書に、同じ状態を指す「訪問済み」を残さない。 |
| FR-STEP-03 | MUST | 「設定済み」は従来の「訪問済み」と同じ判定（過去に表示した）であり、値の保存・検証成功・準備完了・処理成功を意味しない。判定・アイコン（◉）・色・遷移規則は変えない。 |

例: 「1 入力」を開いた後に「2 採点設計」へ進むと、「1 入力」のbuttonの2行目は「設定済み」、「2 採点設計」は「現在・選択中」、「3 実行」「4 結果」は「未着手」。

### 11.17 共通設定へのCopilotログイン操作の追加（2026-10-01追加）

出典: 依頼原文（2026-10-01「GitHub Copilotへのログイン画面がありません。…アプリケーションの起動時に私が介在しなくても自動的にログインを行うようにしているかを確認してください。もしそうでなければ、ログインする機能を添付の[設定]の[共通設定]の中に追加してください。」）。調査結果: 起動時の自動確認・自動loginは§11.10（FR-AL-01〜08）で実装済み。ただしログイン状態・操作は入力画面の実行準備部品（§11.15）にしかなく、設定の共通設定から到達できなかった。自動loginが失敗・抑止された場合（既存資格情報なし、CLI利用不可、環境変数での抑止）に利用者がモデル一覧を更新する手段を共通設定へ追加する。

| ID | 優先度 | 要求 |
|---|---|---|
| FR-SETLOGIN-01 | MUST | 設定画面の「共通設定」タブ（`SettingsCommonEditTab`）の本文に、モデル・出力先の行の直下（採点定義名の上）へ、GitHub Copilotログイン区画（Automation ID `SettingsCopilotLoginPanel`）を表示する。区画は、認証状態文（`SettingsCopilotAuthenticationStatus`、`ExecutionViewModel.AuthenticationStatusText`）、ログイン状態文（`SettingsCopilotLoginStatus`、`LoginStatusText`）、「Copilot 状態を確認」（`SettingsCheckCopilotAuthentication`、`CheckAuthenticationCommand`）、「GitHubにログイン」（`SettingsStartCopilotLogin`、`LoginCommand`）、「ログインを取り消す」（`SettingsCancelCopilotLogin`、`CancelLoginCommand`）を持つ。 |
| FR-SETLOGIN-02 | MUST | 区画のボタンは入力画面の実行準備部品と同じ`ExecutionViewModel`の同じコマンドに結び付ける。動作・有効条件・二重開始／評価中開始の防止・所有processだけの取消・token非収集は§11.3、§11.10、SEC-AL-01〜02のとおりで、新しいlogin経路・独自OAuth・token入力欄を追加しない。設定画面の表示・遷移・保存・読込から認証確認やloginを自動開始しない。 |
| FR-SETLOGIN-03 | MUST | ログイン後のモデル一覧更新は共通設定の「通常モデル」（`ExecutionModel`）へ反映される。「通常モデル」のプレースホルダーは「下の「Copilot 状態を確認」を押してください」とする。 |

例: 起動時の自動loginで資格情報が得られず一覧が保存済みのままのとき、利用者は設定→共通設定で「GitHubにログイン」を押し、ブラウザーで認証すると「通常モデル」の一覧が更新される。

### 11.18 思考レベル・Context Sizeの選択（2026-10-01追加）

出典: 依頼原文（ログイン後に利用できる全モデルの表示・選択、思考レベルとContext Sizeの選択）、既存の§11.13、`src/StudyReportEvaluator.App/Copilot/ReasoningEffortPolicy.cs`、固定SDK `1.0.11`の公式[Types.cs](https://github.com/github/copilot-sdk/blob/v1.0.11/dotnet/src/Types.cs)と[生成RPC型](https://github.com/github/copilot-sdk/blob/v1.0.11/dotnet/src/Generated/Rpc.cs)（確認日2026-10-01）。

| ID | 優先度 | 要求 |
|---|---|---|
| FR-MO-01 | MUST | 共通設定で「通常モデル」の全件選択（FR-MS-01〜05）を維持し、ログイン正常終了後の認証・一覧確認成功で最新一覧へ更新する。取得失敗時は旧一覧を表示専用として残し、実効model・思考レベル・contextの選択を解除し、認証失敗と再確認方法を表示する。空一覧は空のままで新規AI実行不可。固定model名や件数による絞込みはしない。 |
| FR-MO-02 | MUST | 共通設定に「思考レベル」（AutomationId `ExecutionReasoningEffort`）の選択欄を設ける。`auto`以外でSDKが対応を報告した値を重複なしで選べる。順序は`none, minimal, low, medium, high, xhigh, max`、それ以外の安全な値は末尾へOrdinal昇順。表示は順に「なし」「最小」「低」「中」「高」「非常に高い」「最大」、未知値は原値。SDKのdefaultが一致した選択肢には` (Default)`を付ける。未編集時だけ§7.1の既定解決を使う。非対応・未列挙・`auto`は選択不可で「未指定（model非対応またはauto）」を表示し、SDKへnullを送る。metadata内の対応フラグがfalseなら値が列挙されても非対応とする。 |
| FR-MO-03 | MUST | 「Context Size」（AutomationId `ExecutionContextSize`）の選択欄を設ける。既定tierは全modelで利用でき、SDKの既定prompt budget（`billing.tokenPrices.maxPromptTokens`、なければ`capabilities.limits.max_prompt_tokens`）を表示する。`billing.tokenPrices.longContext.maxPromptTokens`が正のintで、既定prompt budgetが既知ならそれより大きい場合だけ拡張tierを追加する。廃止された`contextMax`は新項目が未設定のときだけ使う。表示は、値が1,000,000の整数倍なら`{値÷1,000,000}M`（例: `1M`）、そうでなく1,000の整数倍なら`{値÷1,000}K`（例: `272K`）、それ以外は不変カルチャーの整数とし、既定tierに` (Default)`を付ける。未知容量は「SDK未公開 (Default)」。ここでの容量は入力promptのtoken枠であり、出力込み総contextと同一とは保証しない旨を説明する。`auto`は既定のみ。任意容量、固定1Mの推測、拡張の自動選択はしない。 |
| FR-MO-04 | MUST | 明示編集した思考レベルとcontext tierをmodel IDごとに保持し、別modelへ漏らさない。`setting.txt` schema整数1に任意`modelPreferences`配列（最大4096、ID重複不可）を追加する。各objectは`modelId`（既存ID制約）、`reasoningEffort`（nullまたは既存安全文字列制約）、`contextTier`（`default`または`long-context`）のみ。null配列は保存時省略、旧fileはそのまま読める。明示「設定を保存」で永続化し、一覧cache自動保存に未保存編集を混入しない。読込中の明示編集を読込結果で上書きしない。不正file・保存失敗時は既存bytesと編集値を保持しエラーを表示する。 |
| FR-MO-05 | MUST | 再取得で明示選択値が利用不能になった場合は希望値を保持し、該当欄は未選択、`MODEL_REASONING_EFFORT_UNAVAILABLE`または`MODEL_CONTEXT_TIER_UNAVAILABLE`を表示して実行を拒否する。黙って別値へ変更しない。有効な値の選び直し、または「既定に戻す」（AutomationId `ExecutionResetModelOptions`）の明示操作で、そのmodelの明示希望を削除して復旧できる。reasoning対応自体がなくなり欄が無効でも、この操作で未指定へ復旧できる。リセットは現在modelだけに作用し、保存は明示操作、実行中条件は変えない。model変更・欄の変更・リセットだけで認証・AI通信を始めない。 |
| FR-MO-06 | MUST | run開始時にmodel、effort、context tier、選択tierのprompt budgetをimmutable requestへ固定する。通常・参照・固有評価と全retryのSDK sessionへ同じeffortとContextTierを渡す。既定tierは既存挙動のnull（未指定）、拡張はSDK `ContextTier.LongContext`。事前容量検査には選択tierのprompt上限を使い、拡張時に既定総context上限で誤って制限しない。絶対request上限・timeout・有限retry・restricted tools・入力read-onlyは変えない。次回設定の変更は実行中requestへ影響しない。 |
| FR-MO-07 | MUST | checkpointへ任意`contextTier`（既定nullで省略、拡張`long-context`）を保存し、追記時と再開事前検査でmodel・effort・tierの一致を要求する。既存schema2・既定tierのcheckpointは変更せず読める。不一致は`CHECKPOINT_MODEL_MISMATCH`で送信前に拒否する。「中断時のモデルを選択」は利用可能な場合だけeffort・tierも復元する。最終Run sheetの`Field`に`ContextTier`を追加（既定`default`）、実行画面で現在runと次回設定を区別してtierを表示する。 |
| NFR-MO-01 | MUST | 2つの選択欄は日本語Accessible NameとToolTipを持ち、keyboardで到達でき、44 DIP以上の操作寸法を維持する。共通設定本文の局所scrollで最小viewportでも全項目へ到達できる。実APIを呼ぶ試験・課金を伴う評価・実資格情報の採取は受入自動化に用いない。拡張contextや高い思考レベルで使用量が変わり得ることを表示し、自動で有料処理を開始しない。 |

例: model Aがeffort `[low, medium, high]`、既定`medium`、prompt枠272000・拡張1000000を返す場合、思考レベルは`低, 中 (Default), 高`、Context Sizeは`272K (Default), 1M`。利用者が`高`と`1M`を選ぶと全評価sessionへ`high`と`long-context`を指定する。model B（reasoning非対応、上限未知）へ切り替えてもmodel Aの希望値は残り、Bではeffort未指定・既定contextとなる。

技術選択: 固定SDKの型付きmetadataと`SessionConfig.ContextTier`を再利用する。独自model一覧、model名に依存した容量表、BYOK、SDK更新、追加依存は採用しない。

検証記録（2026-10-01）: 最終差分は`artifacts/test/model-options/verified/model-options-acceptance-verified.trx`で498/498成功（skip 0）。思考非対応化後の明示resetも含む。reset追加前の全体deterministic回帰はCore 199件・App 2006件成功、既存のsymlink権限・opt-in単一EXE artifactの2件は環境前提不足でskip（`artifacts/test/model-options/final-regression/model-options-regression-final.trx`はAppの証跡、Coreはrunner出力）。実account・実課金・native本人login・clean-host・公開gateの成功へ読み替えない。

### 11.19 結果画面での学生の回答と評価内容の表示（2026-10-01追加）

出典: 依頼原文（2026-10-01「添付の実行後の画面での評価状況で表示される文字や情報を以下にしてください。設問に対して、学生の回答と、その回答をどう評価したのか?これは作成するExcelで作成されている情報です。中高校の先生や大学の先生が生徒のテストへの回答やレポートの評価をする際に、自分の設定の確認をするという目的で表示させたいです。」と、「4 結果・出力」の一覧・詳細の画面写真）、`src/StudyReportEvaluator.App/Views/ResultsOutputView.axaml`の従来の詳細（設問文・AI raw・override・計算previewだけで、回答本文・理由・根拠は出力workbookでしか確認できなかった）、`docs/result-excel-description.md` 5.3節（`.Reason`・`.Evidence`・`.Evidence_Source`・`.Evidence_SourceColumn`の意味）。

目的: 先生が、学生の回答に対して自分の採点設計（評価項目）がどう適用されたかを、Excelを開かずに結果画面の詳細で確認できるようにする。表示する評価内容は、結果Excelの`Quantification_Results`に書く値と同じ出所（runの採用済みAI結果とrun snapshot）を使う。学生の回答は、結果Excelの元のシートと同じ内容である入力Excelの当該セルを、実行時と同一であることを確かめてから表示する。

用語: **回答列**とは、選択した基準が属する設問の主回答列と、その設問の補助列（run snapshotの順）である。AIへ送った回答と同じ列であり、それ以外の列・他の行は表示しない（A-RV-01）。

| ID | 優先度 | 要求 |
|---|---|---|
| FR-RV-01 | MUST | 「4 結果」の詳細（「詳細・override」で開く画面）で、選択した基準の表示領域（Automation ID は従来どおり基準の`AutomationId`）を左右2つの欄に分ける。左の**回答欄**（`ResultsCriterionAnswerPane`）には上から「設問文（元のExcel）」（`ResultsCriterionQuestionText`、FR-RS-04）と「学生の回答（元のExcel）」（`ResultsCriterionStudentAnswer`）を置く。右の**評価欄**（`ResultsCriterionEvaluationPane`）には上から、`設問表示名 / 評価方法表示名 / 基準表示名`の見出し、従来のAI raw・override入力・range・status、overrideエラー、「評価の理由（Reason）」（`ResultsCriterionReason`）、「根拠となる回答の引用（Evidence）」（`ResultsCriterionEvidence`）、「根拠の場所（Evidence_Source）」（`ResultsCriterionEvidenceSource`）、「評価項目の説明（採点設計）」（`ResultsCriterionDescription`）、従来の計算preview（適用値・正規化・評価・設問・総合）を置く。両欄はそれぞれ独立した縦の局所scrollを持ち、横scrollを出さず折り返す。一覧画面の表示は変えない。 |
| FR-RV-02 | MUST | 「学生の回答（元のExcel）」は、回答列ごとに1ブロックを主回答列→補助列（snapshot順）の順で並べる。各ブロックは見出し行`[列 {列記号} · 主回答]`または`[列 {列記号} · 補助]`と、次の行からのセル値で構成する。セル値は改行を含め原文のまま（正規化・短縮・文字数上限なし）とし、値が無い・空文字・空白文字だけの場合は`（空欄）`とする。ブロック間は空行1つ（改行2つ）で区切り、改行は`\n`で表す。列記号は大文字（例: `D`）。 |
| FR-RV-03 | MUST | 学生の回答は、そのrunの入力Excel（`ExecutionRunContext.InputPath`）の、run snapshotの`SourceSheet`・当該元行番号・回答列のセルを読取専用で読む。読む前と読んだ後の2回、入力ファイルのSHA-256・バイト数・最終更新日時（UTC）を算出し、いずれもrunの`RunSummary.InputSnapshot`と一致した場合だけ表示する。読込は詳細が表示されていて学生行が選択されているときに、その1行だけを対象として開始し、一覧表示中や他の行には行わない。成功した行の回答は同じ実行結果を表示している間だけ記憶し、同じ行を再表示しても読み直さない。 |
| FR-RV-04 | MUST | 学生の回答の状態表示は次の文言とする。読込中: `学生の回答を読み込んでいます…`。入力Excelが実行時と一致しない: `入力Excelが実行時から変更されているため、学生の回答を表示できません。元のExcelまたは結果Excelで確認してください。`。ファイルが無い・開けない・読めない・形式不正・シートが無い等: `入力Excelを読み取れないため、学生の回答を表示できません（移動・削除・ほかのアプリで使用中など）。元のExcelまたは結果Excelで確認してください。`。失敗は記憶せず、行の再選択または詳細の再表示で読み直す。失敗時に以前の行の回答や部分的な値を表示しない。 |
| FR-RV-05 | MUST | 評価内容は、runの採用済み通常評価結果（`Quantification_Results`の`.AI_Raw`・`.Reason`・`.Evidence`・`.Evidence_Source`・`.Evidence_SourceColumn`・`.Status`に書く値と同じもの）とrun snapshotの基準定義から作る。①「評価の理由（Reason）」: 採用済み結果があればReasonを原文のまま。Reasonが空または空白だけなら`（理由は空欄です）`。採用済み結果が無い場合、statusが`EMPTY`なら`回答が空欄のため、AIで評価していません。`、`CANCELLED`なら`取消または未処理のため、評価していません。`、その他は`技術的な失敗（{status}）のため、評価結果はありません。`。②「根拠となる回答の引用（Evidence）」: 採用済み結果のEvidenceが空でなければ原文のまま、空なら`（引用なし）`、採用済み結果が無ければ`—`。③「根拠の場所（Evidence_Source）」: `PRIMARY_ANSWER`は`主回答（列 {列記号}）`、`SUPPORTING_COLUMN`は`補助（列 {列記号}）`（列記号は`Evidence_SourceColumn`の値。空なら括弧部分を付けず`主回答`／`補助`）、`NONE`は`なし`、採用済み結果が無ければ`—`。④「評価項目の説明（採点設計）」: snapshotの基準の説明を原文のまま。空または空白だけなら`（説明は未設定です）`。 |
| FR-RV-06 | MUST | 本表示は読取専用であり、採点値、override、Excel出力、checkpoint、AI送信内容・回数（追加のAI呼出しは0件）、ジョブログ、設定ファイルを変更しない。入力Excelのバイト列と最終更新日時を変更しない。 |
| SEC-RV-01 | MUST | 学生の回答・理由・根拠・基準の説明は、メモリと画面だけに置く。application log、ジョブ単位JSON Linesログ、checkpoint、setting.txt、その他のファイルへ書かない。型の`ToString()`、例外メッセージ、Automation の Name、状態表示の文言へ回答本文やファイルパスを含めない。記憶した回答は、別の実行結果を読み込んだとき、または結果画面のViewModelを破棄したときに破棄する。 |
| NFR-RV-01 | MUST | ファイルの読込とハッシュ計算はUIスレッド外で行い、画面操作を待たせない。読込中に行の選択を変えた・詳細を閉じた・別の実行結果を読み込んだ場合、古い読込の結果を画面へ反映しない（最新の要求の結果だけを反映する）。最小1024×720 DIPの詳細表示で、override入力欄はviewport内に収まり、回答欄・評価欄は高さ44 DIP以上で横にはみ出さない。 |

例（主回答列`D`の値が`機械学習は\nデータから学ぶ。`、補助列`F`が空のとき）: 「学生の回答（元のExcel）」は`[列 D · 主回答]\n機械学習は\nデータから学ぶ。\n\n[列 F · 補助]\n（空欄）`。採用済み結果が`RawScore=3`、`Reason=用語の説明が正確`、`Evidence=データから学ぶ`、`EvidenceSource=PRIMARY_ANSWER`、`EvidenceSourceColumnId=D`なら、評価欄は理由`用語の説明が正確`、引用`データから学ぶ`、場所`主回答（列 D）`。

境界・例外: 固有評価の項目別の値・理由は詳細に追加しない（行の固有点・類似減点は従来どおり一覧と詳細の見出し行に出る、A-RV-02）。類似度は、FR-114（AC-077・AC-078・AC-081）の確認指標の詳細（参照別の値・一致位置・被覆率・短文注意）を「確認指標・未検証」として表示する（2026-11-04改訂）。結果Excel（final／partial）を読みに行かない（A-RV-03）。入力Excelが別のアプリで書込み用に開かれていて共有読取できない場合は、読み取れない場合の文言になる。

### 11.20 評価項目の説明の初期値（2026-10-01追加）

出典: 依頼原文（2026-10-01「[2.定量化設計]の画面の[通常評価]の中の、画面右側の[評価項目]の説明の初期値を、[評価方法]に対応して以下としてください。」。Knowledge Coverの場合・Prompt 分析の場合の2本文）、2026-11-03の依頼（評価方法の種類をKnowledge・Prompt・Customの3つにする変更。A-CD-01、A-CD-04、A-CD-05）。

通常評価の評価項目の「説明」（`CriterionDefinition.Description`、画面右側の「説明」欄）の初期値を、その評価項目が属する評価方法（evaluator）の種類で決める。種類との対応は次の3つだけとし（2026-11-03に2つから3つへ拡張）、本文は`DefaultCriterionDescriptions`（Core）を唯一の定義とする。改行は`\n`（LF）、末尾に改行・空白を付けない。`{論点}`と`xxx`は利用者が書き換えるための文字どおりの記載であり、置換しない。評価方法の種類の表示名と追加ボタンも、Knowledge／Prompt／Customの3種類に対応する。

| 評価方法の種類 | 初期値の本文 |
|---|---|
| `KNOWLEDGE_COVERAGE`（Knowledge） | `学生が作成したレポートを高度に分析・解析をしてそれぞれの{論点}について説明がされているかどうかの評価を行ってください。`＋改行＋`論点:`＋改行＋`- xxx`＋改行＋`- xxx`＋改行＋`- xxx` |
| `PROMPT_ANALYSIS`（Prompt） | `学生が作成したPromptを、Anthropic・OpenAI・Microsoftなどが公開するPrompt作成のベストプラクティスと照合して、高度に分析・評価を行ってください。`＋改行＋`観点:`＋改行＋`- 明確な目的が示されているか`＋改行＋`- 具体的で曖昧さのない指示になっているか`＋改行＋`- 必要な文脈・背景情報が与えられているか`＋改行＋`- 制約・禁止事項が明示されているか`＋改行＋`- 出力を検証できる基準が示されているか` |
| `CUSTOM_PROMPT`（Custom） | `（評価したい内容と判断の基準を、利用者が具体的に書き換えてください。）`＋改行＋`評価の観点:`＋改行＋`- xxx`＋改行＋`- xxx`＋改行＋`- xxx` |

| ID | 優先度 | 要求 |
|---|---|---|
| FR-CD-01 | MUST | 通常評価で評価項目を新規に作る次の経路は、評価方法の種類に対応する上表の本文を説明の初期値とする。①評価項目の「追加」（既存の評価方法へ追加。種類は追加先の評価方法の現在の種類）、②評価方法の追加時に自動で作る最初の評価項目、③採点定義を新規に作る既定（Knowledge）、④入力画面で列の対応から提案する設問の最初の評価項目（学生のPrompt列の候補は種類Prompt、それ以外はKnowledge）。評価方法の種類の選択肢はKnowledge・Prompt・Customの3つで、種類を選ぶと右側の評価項目の説明の既定値がその種類の本文になる。既定の評価方法名は、入力画面の学生のPrompt列の提案では「Prompt 分析」（種類Prompt）とする。 |
| FR-CD-02 | MUST | 初期値は新規作成時だけに設定する。既存の評価項目の説明、利用者が編集した説明、保存定義・読込定義の説明（既存の`CUSTOM_PROMPT`定義の説明を含む）、評価項目の複製、評価方法の種類の変更（Knowledge・Prompt・Custom間）は、説明を書き換えない。種類の変更後に追加する評価項目は、変更後の種類の本文を使う。 |
| FR-CD-03 | MUST | 評価項目の名前・重み・range・個別の範囲の初期値は変えない。初期値の説明は必須入力検査（空白でない）と文字数上限を満たし、定義の検証エラーを増やさない。 |

例: 評価方法の種類が`Prompt`の質問で評価項目の「追加」を押すと、新しい評価項目の説明は`学生が作成したPromptを、Anthropic・OpenAI・Microsoftなどが…`で始まる本文になる。同じ質問の評価方法を`Knowledge`へ変えても既存の説明は変わらず、その後に追加した評価項目だけ`学生が作成したレポートを…`で始まる本文になる。

#### FR-109 Prompt種類の組込みテンプレート評価とEvaluatorTypeの3値
- 要求: 評価方法の種類`PROMPT_ANALYSIS`（Prompt）は、組込みテンプレート`prompt-analysis-v1`（`BuiltInTemplateVersion`で固定、利用者は編集しない）でAIへ送る評価指示を組み立て、採点・出力・AI契約はKnowledgeと同じ構造とする。未知のテンプレートversionは拒否する。結果Excelと設定シートの`EvaluatorType`列は`KNOWLEDGE_COVERAGE`／`PROMPT_ANALYSIS`／`CUSTOM_PROMPT`の3値を書き、設定シートの読込で同じ種類へ往復できる。既存の`CUSTOM_PROMPT`はCustomのまま読む。
- 決定状態: 承認済み（包括承認 2026-11-03・approval_policy）　出自: AI提案　優先度: MUST（A-CD-04の評価動作を検証可能にする。監査指摘X-01）
- 上位: 既定制約（ui_policy）　出典: 依頼原文 2026-11-03（§11.20）、A-CD-04
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: なし
- 関連する既存資産: なし
- 受入基準 AC-064: `SafeEvaluationPayloadBuilder`がPrompt種類で`prompt-analysis-v1`の組込みテンプレートを使い、未知のversionを拒否する。`ConfigSheetWriter`が3種類を`KNOWLEDGE_COVERAGE`／`PROMPT_ANALYSIS`／`CUSTOM_PROMPT`として書き、設定シートの読込で同じ種類に往復し、既存の`CUSTOM_PROMPT`がCustomとして読める。検証は`dotnet test`（Core.TestsのSafeEvaluationPayloadBuilder関連、App.TestsのConfigSheetWriter関連。実CLI・実AI・実資格情報は使わない）。
  - 検証レベル: integration

#### FR-110 設定シートからの採点定義の読み戻し（ConfigSheetReader）
- 要求: 結果workbookおよび設定シートの設定シートに`ConfigSheetWriter`（FR-109）が書いた`CANONICAL_JSON`行から、採点定義（評価方法の種類`EvaluatorType`を含む）を復元する本番のライブラリ機能（`ConfigSheetReader`相当。CoreまたはAppの`Workbooks/Reading`配下）を設ける。種類は`KNOWLEDGE_COVERAGE`／`PROMPT_ANALYSIS`／`CUSTOM_PROMPT`の3値を同じ種類へ往復し、既存の`CUSTOM_PROMPT`はCustomのまま読む。壊れた・欠けた・未知の種類名の設定シートは、例外ではなく検証エラーを持つ結果として返し、採点定義を返さない。画面からの読込操作（ボタン等）への接続は本要求の範囲外とする（A-CD-06）。FR-109の書出し契約は変えない。
- 決定状態: 承認済み（包括承認 2026-11-03・approval_policy）　出自: AI提案　優先度: MUST（FR-109の「設定シートの読込で同じ種類へ往復できる」を本番コードで検証可能にする）
- 上位: 既定制約（ui_policy）　出典: 依頼原文 2026-11-03（設定シートの読み戻し）、FR-109、A-CD-04、A-CD-06
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: なし
- 関連する既存資産: なし
- 受入基準 AC-065: `ConfigSheetWriter`が書いた設定シートを読み戻すと、`CANONICAL_JSON`から復元した採点定義が書出し前の定義と等価になり、`EvaluatorType`の3値（`KNOWLEDGE_COVERAGE`／`PROMPT_ANALYSIS`／`CUSTOM_PROMPT`）をそれぞれ同じ種類で往復し、既存の`CUSTOM_PROMPT`だけの設定シートはCustomとして読める。検証は`dotnet test`（実CLI・実AI・実資格情報は使わない）。
  - 検証レベル: integration
- 受入基準 AC-066: `CANONICAL_JSON`行がない・空・JSONとして壊れている・必須項目が欠けている設定シートを読むと、例外を投げず、失敗と検証エラー（原因の区別つき）を持つ結果を返し、採点定義は返さない。
  - 検証レベル: unit
- 受入基準 AC-067: `EvaluatorType`が3値以外の未知の種類名のとき、例外を投げず検証エラー（未知の種類名を含む）として拒否し、既知の種類に暗黙に置き換えない。`CANONICAL_JSON`内の種類と`EvaluatorType`列が食い違うときも検証エラーとして拒否する。
  - 検証レベル: unit

#### FR-113 結果workbookへの「採点結果」シート（評価点と総合点だけの一覧）
- 要求: 完成した結果workbookに、設問の評価点と総合点だけを並べたシート`採点結果`を追加する。先頭列`ID`は`Quantification_Results`の`SourceRow`（元シートの行番号）、行は同シートの学生行と同じ。有効な設問ごとに、設問の順番N（1始まり）で`質問Nの評価`（その設問の`.Question_Normalized`）と、その設問に有効な`PROMPT_ANALYSIS`評価方法があるときだけ`質問NのPromptの評価`（その評価方法の`.Evaluator_Score`）を並べ、右端に`合計`（`Final_Score`）を置く。無効な設問、有効な`PROMPT_ANALYSIS`評価方法がない設問のPrompt列は作らない。値は`Quantification_Results`のセルを参照する数式とし、Override・配点の変更に追従する。参照元が空欄・失敗のときは空欄のまま表示し、0にしない。シート名は既存のapp-owned sheet名の衝突回避規則に従い、`.partial`には入れない。衝突回避でシート名が変わったときは、`採点結果`の数式の参照先に、実際にworkbookへ書かれた`Quantification_Results`のシート名を使う（名前を固定の文字列で書かない）。`docs/result-excel-description.md`の「4枚のシート」の説明（§1・§3・目次）を、本シートを含む形に更新する。FR-XD-01の「`Quantification_`で始まる4つのapp-owned sheet」に加えて完成workbookだけに入る5枚目の結果一覧であり、FR-XD-01・§9.4・AC-045②は本依頼でこの区別に整合させた（A-SR-01）。
- 決定状態: 承認済み（包括承認 2026-11-04・approval_policy）　出自: AI提案　優先度: SHOULD（先生が点数だけを一覧で確認・転記しやすくする）
- 上位: 既定制約（ui_policy）　出典: SRC-004
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: なし
- 関連する既存資産: なし
- 受入基準 AC-074: 評価方法にKnowledge・Promptを持つ設問が複数ある実行で完成workbookを実際に生成し、Open XML（xlsxの実体）で読むと、`採点結果`シートの見出しが`ID`、設問順に`質問Nの評価`（Promptの有効な設問は直後に`質問NのPromptの評価`）、最後に`合計`の順に並び、無効な設問と有効なPrompt評価方法のない設問のPrompt列がない。データ行は`Quantification_Results`の学生行と同数で、`ID`は各行の`SourceRow`と一致する。シートは`.partial`にはなく、元シートに同名があれば既存規則で名前が変わる。
  - 検証レベル: system
- 受入基準 AC-075: `採点結果`の評価・Prompt評価・合計のセルは、`Quantification_Results`の`.Question_Normalized`・`.Evaluator_Score`・`Final_Score`を参照する数式で、固定値ではない。`Quantification_Results`のOverrideまたは配点を変えて再計算すると、`採点結果`の値が追従する。
  - 検証レベル: integration
- 受入基準 AC-076: 参照元が空欄または失敗（status）の学生・設問のセルは、`採点結果`でも空欄のままで、0にならない。参照元が0のセルは0として表示される。参照元の`Final_Score`が空欄の学生は、`合計`も空欄になる（0にならない）。シート名が衝突回避で変わった実行でも、数式は実際に書かれた`Quantification_Results`のシート名を参照し、値が表示される。
  - 検証レベル: integration

### 11.21  結果画面の総実行時間とAIクレジット（2026-10-01追加）

出典: 依頼原文（2026-10-01「実行結果の画面に以下の情報を付与してください。定量化のジョブの総実行時間(HH:MM:SS)/AI Credit (これは金額のコストに関わるので大変重要)」と「4 結果・出力」の画面写真）、§11.9、`src/StudyReportEvaluator.App/Usage/JobUsageTracker.cs`、GitHub Docs [Usage and billing metrics（Copilot SDK）](https://docs.github.com/en/copilot/how-tos/copilot-sdk/features/usage-and-billing)と固定SDK `1.0.11`の同文書[usage-and-billing.md](https://github.com/github/copilot-sdk/blob/v1.0.11/docs/features/usage-and-billing.md)（`totalNanoAiu`は「AI credit cost, in nano-AI units」、例は`÷ 1e9`）、GitHub Docs [Usage-based billing for individuals](https://docs.github.com/en/copilot/concepts/billing-and-usage/individuals/billing)（AIクレジットの定義）、SDK [nodejs/src/workflow.ts](https://github.com/github/copilot-sdk/blob/main/nodejs/src/workflow.ts)（`NANO_AIU_PER_AIU = 1_000_000_000`）。いずれも確認日2026-10-01。

本節の**ジョブ**は§11.9と同じ「定量化を開始」または再開の1操作に対応するジョブ1件である。値の正本は、結果画面へ読み込んだ実行結果に付属するそのジョブのコスト記録（`JobCostSnapshot`、以下「コスト記録」）とする。本節は§11.9の旧規定「換算根拠を確認できるまでAIクレジットの数値を表示しない」を置き換える（A-CR-01）。

| ID | 優先度 | 要求 |
|---|---|---|
| FR-RT-01 | MUST | 「4 結果」の見出し行で、件数要約（`ResultsRunSummary`）の右に`ResultsRunMetrics`（TextBlock）を置き、実行結果を読み込んでいる間だけ`総実行時間 {時間} · AIクレジット {クレジット}`を表示する（区切りは半角空白・U+00B7・半角空白）。未読込時は空文字で非表示。ToolTipとAccessible Nameは本文の後に改行とFR-CR-04の注記を続けた文とする。1024×720 DIP以上の通常画面で省略記号なしに全文が見える。 |
| FR-RT-02 | MUST | `{時間}`はコスト記録の`EndedAtUtc − StartedAtUtc`（ジョブ開始から、参照回答・評価・類似度・最終workbook確定・cleanup・使用量記録の終了まで）。経過時間の秒未満を切り捨てた整数秒Tから、HH＝floor(T÷3600)を2桁以上で0埋め（100時間以上は桁を増やす）、MM＝floor((T mod 3600)÷60)、SS＝T mod 60を各2桁0埋めとし、`HH:MM:SS`を不変カルチャで示す。コスト記録がない、開始・終了時刻のどちらかがない（未終了）、差が負のときは`—（未計測）`とする。 |
| FR-CR-01 | MUST | AIクレジット＝コスト記録の`nano-AI units`ジョブ合計（§11.9の「nano-AI units（SDK報告値）」と同じ値）÷ 1,000,000,000。decimalで計算し、小数第5位を四捨五入（`MidpointRounding.AwayFromZero`）した小数4桁を、不変カルチャ・3桁区切り`,`・常に小数4桁（書式`#,##0.0000`）で示す。合計が正で丸め結果が0のときは`<0.0001`、合計が0のときは`0.0000`とする。 |
| FR-CR-02 | MUST | `ResultsRunMetrics`の`{クレジット}`は、次の上から最初に当てはまる形とする。コスト記録なし→`—（コスト記録なし）`。送信試行0→`—（AI送信なし）`。nano-AI units未取得（null）→`—（未取得）`。コスト記録が未終了、nano-AI unitsの観測状態がない、または部分取得（観測完了の試行数＜送信試行数）→`{値}（一部取得）`。それ以外→`{値}`。未取得を0へ置き換えない。 |
| FR-CR-03 | MUST | 「3 実行」「4 結果」で共有するコスト要約（`ExecutionCostSummary`／`ResultsCostSummary`）の2行目を`AIクレジット {値}（SDK報告値から換算・観測 {観測試行数}/{送信試行数} 試行・観測完了｜部分取得）`、未取得は`AIクレジット —（未取得）`とする。コスト詳細の`nano-AI units（SDK報告値）`の行の直後に`AIクレジット（nano-AI units ÷ 1,000,000,000。SDK報告値からの換算で請求確定額ではありません） {同じ形}`を追加する。ジョブログ表示（`LogText`の各行）のAIクレジット欄は`／AIクレジット {値}（SDK報告値から換算・部分取得｜観測完了）`、未取得は`／AIクレジット —（未取得）`とし、部分取得の判定は同じ行の`nano-AI units`と同じにする。`{値}`の書式はFR-CR-01。 |
| FR-CR-04 | MUST | AIクレジットが観測値の換算であり、請求確定額・アカウント全体の利用量・残量ではないことを示す。`ResultsRunMetrics`の注記は`GitHub Copilot SDKが報告したnano-AI unitsを1,000,000,000で割ったAIクレジットです。今回のジョブで観測できた値だけで、請求確定額ではありません。`とする。円・ドル等の通貨への換算とプラン残量の表示はしない。 |
| NFR-CR-01 | MUST | 本表示のためにAIを呼ばず、GitHubの課金・利用量APIへ問い合わせず、追加のnetwork通信をしない。JSONLログのschema・記録項目、Excel出力、checkpoint、設定ファイルを変えない（AIクレジットは記録済みのnano-AI unitsから表示時に計算する）。表示の失敗・未取得で採点・出力の成否を変えない。 |

例: コスト記録が開始`2026-10-01T03:34:24.100Z`、終了`2026-10-01T03:36:33.900Z`（経過129.8秒）、nano-AI units合計`12345678901`、24試行すべて観測完了 → `総実行時間 00:02:09 · AIクレジット 12.3457`、コスト要約2行目は`AIクレジット 12.3457（SDK報告値から換算・観測 24/24 試行・観測完了）`。同じ値で1試行が部分取得 → `総実行時間 00:02:09 · AIクレジット 12.3457（一部取得）`。経過360000秒・nano-AI units `50000` → `総実行時間 100:00:00 · AIクレジット 0.0001`。nano-AI units `49999` → `AIクレジット <0.0001`。nano-AI units `1234567890000000` → `1,234,567.8900`。

境界・例外: 再開した実行では今回の再開ジョブだけの時間・クレジットを示し、中断前のジョブの値を加算しない（A-RT-01）。`ResultsRunIdentity`の「開始」「終了」はcheckpointのrun開始時刻と評価終了時刻であり、総実行時間と一致しないことがある。

### 11.22 通常評価の設問タブ（2026-10-01追加）

出典: 依頼原文（2026-10-01「画面の[2.定量化設定]の[通常評価]で、各設問がコンボボックスで切り替えられるようになっています。これをタブで切り替えられるようにしてください。タブには設問名を表示してください。」）。本節は当初§11.18として追加したが、§11.18（思考レベル・Context Size）と番号が重複したため、2026-10-05に§11.22へ改番した。要求ID（FR-QT-01〜05）は変更しない。

（2026-11-02注記: 通常評価は設定ではなく「2 採点設計」の統合画面へ移る（§11.23）。本節の要求は統合画面の設問タブへ読み替えて維持し、Automation IDは変えない。FR-QT-05の「設定画面を開いた直後の初期focus」は「統合画面を開いた直後の初期focus（基礎配点入力`BasePointsTextBox`）」と読む。）

設定の「通常評価」（2.定量化設計の「通常評価」タブと同じ内容）で、編集する設問の切替手段をcombobox（ドロップダウン）からタブへ変更する。

| ID | 優先度 | 要求 |
|---|---|---|
| FR-QT-01 | MUST | 通常評価の画面（Automation ID `EvaluatorSettingsView`）の設問の選択は、ドロップダウンではなくタブで行う。Automation ID `EvaluatorSettingsQuestions`の設問選択部品は`ComboBox`であってはならず、設問1件につき1つのタブを表示する。 |
| FR-QT-02 | MUST | 各タブの文字は、その設問の表示名（`QuestionDesignItemViewModel.DisplayName`、例: `質問 1`）とする。設問の並び順は`Questions`の順と同じ。長い名前は240 DIP幅で省略記号で切り、全文はToolTipで確認できる。設問選択部品のAccessible Nameは「通常評価を編集する設問」とする。 |
| FR-QT-03 | MUST | タブを選ぶと`SelectedQuestion`がその設問になり、評価方法・評価項目・編集欄は選んだ設問の内容へ切り替わる。`SelectedQuestion`が別の経路（入力の同期、並替え、他画面）で変わったときは、タブの選択が追従する。選択の解除（null選択）では`SelectedQuestion`を変えず、設問本文・配点・採点定義（`Draft`）は選択操作で変わらない。 |
| FR-QT-04 | MUST | 設問が0件のときは、従来の「設問を選択してください。設問がない場合は、設問の設定で追加してください。」を表示する。 |
| FR-QT-05 | MUST | タブ領域は、最小window（1024×720 DIP）と200%表示でも外側スクロールを要せず横へはみ出さない。タブが1行に収まらないときは折り返して最大2行（92 DIP）まで表示し、超える分は局所の縦スクロールで到達できる。各タブの高さは44 DIP以上、文字は14 DIPで、keyboard（Tab）でタブ領域へ到達でき、「2 採点設計」の統合画面（FR-001）を開いた直後の初期focusは基礎配点入力（`BasePointsTextBox`、統合画面で最初の入力部品）とする。評価方法・評価項目の選択部品（combobox）は変更しない。 |

例: 設問が`質問 1`〜`質問 4`の4件なら、タブは`質問 1`〜`質問 4`の4つがこの順に並ぶ。`質問 3`のタブを選ぶと、評価方法・評価項目・編集欄が`質問 3`の内容へ切り替わる。設問の並替えで`質問 3`が先頭へ移ると、タブの選択も同じ設問に追従する。

### 11.23 採点配分と通常評価の統合画面・設定は共通のみ（2026-11-02追加）

出典: 依頼原文（2026-11-02「[2.定量化設定]の画面は、採点配分の画面（基礎配点・固有配点・類似度減点係数、配点合計、設問一覧と配点、設問ごとの有効/配点、計算式など）と、[通常評価]の画面（設問タブ、評価方法・評価項目の選択/追加、基本/Prompt タブ、重み・最小・最大など）をマージした1つの画面にする。[設定]は[共通]だけにする。それ以外は[2.定量化設定]の内容になる。次のタブは設定から削除する: [入力詳細]、[固有評価]、[読み込みPrompt]。」。添付画像は参照できず、既存の要求と`src/`の画面（`QuantificationDesignView`、`EvaluatorSettingsView`、`SettingsView`）から解釈した）。

統合: 設定の「通常評価」（FR-QT-01〜05）と主画面「2 採点設計」の採点配分を1つの画面にする。旧FR-QT・§11.4の5カテゴリ・§12.2の「読込Prompt」一覧の置き場所を本節が上書きする。

#### FR-001 採点配分と通常評価の統合画面
- 要求: 「2 採点設計」は、採点配分（基礎配点・固有配点・類似度減点係数、配点合計、設問一覧と配点、設問ごとの有効／配点、計算式）と、通常評価（設問タブ、評価方法・評価項目の選択と追加、基本／Promptタブ、重み・最小・最大など。FR-QT-01〜05）を、1つの画面に統合して表示する。利用者は別カテゴリへ移動せず同じ画面で両方を編集できる。値・対象設問・選択状態・編集内容・保存の契約（§11.6、§11.8）、Automation IDは変えない。同じ入力欄を二重に置かない。
- 決定状態: 承認済み（依頼 2026-11-02）　出自: 依頼　優先度: MUST（利用者の画面統合の指示）
- 上位: 既定制約（ui_policy）　出典: 依頼原文 2026-11-02（§11.23）
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: なし
- 受入基準 AC-054: 「2 採点設計」の1画面に、配点合計・設問ごとの有効／配点・計算式と、設問タブ・評価方法・評価項目・基本／Promptタブが同時に存在し、設問タブの選択で通常評価の編集内容と選択設問が連動する。Automation IDは旧画面と同じ。最小window（1024×720 DIP）で外側スクロールを要しない（§11.5）。
  - 検証レベル: system

#### FR-002 共通設定と出力フォルダー選択の統一
- 要求: 設定画面のカテゴリは「共通」だけとする。「入力詳細」「通常評価」「固有評価」「読込Prompt」のタブ・カテゴリ選択ボタンを設定から削除する（通常評価はFR-001の統合画面にある）。共通設定の「指定出力先」と「実効出力先」は、既存の別workbookの出力フォルダーを確認・変更する単一の欄に統一し、次回実行に使うフォルダーを表示する。フォルダーの変更はネイティブフォルダーピッカーの画面操作で行い、アプリにはパスの手入力・貼付けによる変更手段を設けない。既定値への解除も画面操作で行う。内部の明示指定と自動算出の区別、保存・復元・実行開始時snapshot・出力fileの命名と安全性の契約（§9.1、§11.6、§11.8）、共通設定の他の内容（§11.17、§11.18）は変えない。2 採点設計から旧カテゴリへ移動する入口（「固有評価」「設問の詳細」ボタンなど）は、削除したカテゴリを指さない。
- 決定状態: 承認済み（依頼 2026-11-03）　出自: 依頼原文　優先度: MUST（利用者の共通設定・出力先統一と画面選択の指示）
- 上位: 既定制約（ui_policy）　出典: SRC-002、SRC-003、依頼原文 2026-11-02（§11.23）
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: なし
- 関連する既存資産: カタログのFR-002（SettingsView、MainWindowViewModel）。入力workbookのネイティブ選択（InputView）は参照のみで挙動を変えない。
- 受入基準 AC-055: 設定画面のカテゴリは「共通」だけで、`SettingsCategoryMapping`・`SettingsCategorySpecial`・読込Prompt・通常評価のカテゴリボタンが存在しない。2 採点設計の旧設定への入口は、存在するカテゴリだけを指す。共通設定のCopilot login・modelは従来どおり動き、出力先は次の条件を満たす。①「指定出力先」「実効出力先」の別々のパス欄はなく、単一の出力フォルダー欄が次回実行先の全文を確認・コピーできる読取専用表示となる。キー入力・貼付けでパスを変更できない。②マウスまたはキーボードで選択操作を起動するとネイティブフォルダーピッカーが開き、フォルダーを選択・確定すると表示と次回の明示指定が同じ絶対pathに更新される。保存・再起動後も復元され、別の入力Excelへ変更しても指定先を保持する。③ピッカーの取消は表示・明示指定・保存済み指定を変えない。ピッカーを開けない、または選択先のローカルpathを取得できない場合は理由を表示して指定を維持し、手入力へのfallbackをしない。④「既定に戻す」の画面操作で明示指定を`null`にでき、保存・再起動後も旧指定に戻らない。未指定では入力隣接の`result`を表示し、入力変更に追従するが、算出pathを明示指定として保存しない。入力未選択では「入力後に決定」と表示する。⑤新規実行は表示された次回出力フォルダーへ既存の別workbookを出力する（AC-013）。利用不可の指定先は別pathへfallbackしない。選択・取消・設定復元だけではアプリがフォルダーや出力fileを作成しない。実行中の設定変更は現在runのsnapshotへ混入せず、既存checkpointの予約済みfinal／partial pathと再開条件を変えない。
  - 検証レベル: system

#### FR-003 削除した3タブの機能の廃止
- 要求: 設定から削除した3タブ（入力詳細・固有評価・読込Prompt）が扱っていた次の3機能は、置き場所を移さず機能ごと廃止する（Q-001回答B）。画面のどこからも到達できず、操作できない。(1) 入力詳細の編集UI（設問名・補助列・列候補の再適用・設問の追加／複製／並替え／削除）。(2) 固有評価の編集UI（source・補助列・Prompt・enabledの編集と、固有評価項目の追加）。(3) 読込Promptの一覧表示と「Promptを適用」の操作。§5.2・§5.3・§7.4・§12.2・§11の関係箇所のうち、これらのUI操作を前提とする記述は本要求が優先し、廃止したものとして扱う。次は廃止の範囲外で維持する（A-MG-03）: 採点の計算式（§6・§8、固有配点を含む。固有配点を0固定にしない）、既存の採点定義・設定fileの読込・保存の互換（固有評価項目や補助列を含む定義はそのまま読込・保存・計算できる）、command lineの`--prompt`引数の受理（§12.1。受理するが、UIへは反映せず適用もしない）、通常評価の設問タブと評価方法・評価項目の編集（FR-001、FR-QT-01〜05）。
- 決定状態: 承認済み（Q-001 2026-11-02）　出自: 利用者回答　優先度: MUST（利用者の機能廃止の指示）
- 上位: 既定制約（ui_policy）　出典: 依頼原文 2026-11-02（§11.23）、Q-001回答B
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: なし
- 受入基準 AC-057: 画面（設定・2 採点設計・1 入力・3 実行）のどこにも、入力詳細の編集UI（設問の追加／複製／上へ／下へ／削除、設問名・補助列の編集、候補一式を再適用）、固有評価の編集UI、読込Promptの一覧・「Promptを適用」が存在せず、「設問の詳細」「固有評価」ボタンも存在しない。通常評価の設問タブ・評価方法・評価項目の編集は従来どおり動く。
  - 検証レベル: system
- 受入基準 AC-058: 固有評価項目と補助列を含む既存の採点定義を読み込んで保存し直しても、それらの内容は失われず、固有配点を含む獲得点・FinalRaw・FinalScoreが変更前と同じ値で計算される。`--input`と`--prompt`を指定した起動は起動errorにならず、読み込んだPromptはUIに表示されず、適用もされず、AI送信は0件である。
  - 検証レベル: unit
- 受入基準 AC-059: `docs/`の利用者向け文書（getting-started、settings等）に、廃止した3機能の画面・操作（入力詳細の設定画面、固有評価の編集、読込Promptの一覧と適用）の説明が残らない。docs/id-registry.md（ID台帳）の廃止項目に廃止機能名（「入力詳細」等）が含まれる場合は、利用者向け文書ではなく台帳なので本ACの対象外とする。
  - 検証レベル: integration

質問票:

| ID | 重要度 | 質問 | 選択肢 | 推奨 | 状態 | 回答 |
|---|---|---|---|---|---|---|
| Q-002 | 中 | Prompt種類・Custom種類の評価項目の説明の既定本文（§11.20、A-CD-05）は、rd-authorが作文した案である。この文面で承認するか。 | a) 現状の本文で承認 b) 文面を指定する | a（回答まで現状の本文で継続） | 回答済み | a（現状の本文で承認。2026-11-03） |
| Q-001 | 高 | 設定から削除する入力詳細・固有評価・読込Promptの機能の置き場所は。 | a) 統合画面内へ移す b) 機能ごと廃止する c) 入力画面へ移す | a | 回答済み | B（機能ごと廃止する。2026-11-02） |

変更履歴: 2026-11-02 Q-001回答Bを反映。FR-DOC-01〜04・AC-044を廃止（廃止した3機能の操作説明のため。統合→AC-059）。A-MG-02を決定済みにし、C-1・C-4を解消。影響: 実装・テスト・利用者文書の更新が必要（上記AC、AC-037・AC-048のtest名の更新は実装側で必要）。
変更履歴: 2026-11-03（run 202611032044）FR-002を改題・変更、AC-055を変更し、§9.1の解除手段を画面操作へ整合。出力フォルダーの表示・変更という同じ目的の既存項目を再利用し、要求・ACの新規採番・廃止・統合はなし。similar／similar --pairsは候補なしだったが、カタログとtraceでFR-002・AC-055の対象一致を確認した。影響: AC-055のE2E-002（既存PASSは変更前の証跡、test-designerが期待値とnative選択・取消・保存・既定・実行のシナリオを更新）、SettingsView、SettingsViewModel、ExecutionViewModel、MainWindowViewModel、SettingsViewTests、MainWindowSettingsTests、IntegratedDesignAndSettingsTests、docs/settings.mdの二重欄・手入力説明。AC-013・AC-037は既存契約として参照し本文変更・再queueはしない。実装・System Test・台帳・利用者文書はこの工程では変更しない。
変更履歴: 2026-11-03（run 202611032044 最終監査転記）最終集計で採用されたP-01（3/3、MEDIUM）はFR-001〜003にG-ID対応がない既存の追跡性課題として監査指摘へ記録し、要求を再設計しない。C-01（2/3、暫定LOW）は実ファイル・台帳コマンドを確認し、`tests/system/ui/OutputFolderSelectionTests.cs` は既に記載済み、E2E-002の実行対象 `tests/system/test_fr002_output_folder.py` が未記載と確認してカタログへ追記し解消。E2E-002はheadless harnessによる検証であり、実OSダイアログの実操作・見た目・focusは未確認の限界として扱い、実機確認済みとはしない。非採用のQ-01（数値直書き）・Q-02（旧UI記述）は各1/3で、新たな修正指摘として追加しない。
変更履歴: 2026-11-02 利用者承認（「修正してよいです」）によりC-2・C-3を解決。FR-QT-05、AC-048 ⑥の初期focusを統合画面（FR-001）に、§11.5・§11.8・§11.22・AC-037・§18の「5カテゴリ」「カテゴリ」を「設定は共通のみ」「設定内の表示」に書き換えた（意味はFR-001〜003に合わせ、値・閾値は不変）。同日追記: 初期focusは実装・既存テスト（MainWindowSettingsTests・QuantificationDesignViewTests）と整合する最小変更として基礎配点入力（`BasePointsTextBox`）にした（設問タブ領域へのTab到達は維持）。過去の実測の記述（行1303付近）と履歴は過去事実のため変えない。影響: AC-037・AC-048の文言変更、台帳ケースの期待値の更新（test-designer）。

### 11.24 別名workbook出力の既定名とジョブのテキストログ（2026-11-03追加）

出典: 依頼原文（2026-11-03「別名 workbook 出力欄（結果画面 ResultsOutputViewModel の OutputPath）の既定値が、run完了後は自動出力済みのfinal pathになり『完成名が既に存在します。上書きせず、別の名前を指定してください。』と出てそのまま出力できない。ジョブを動かすたび・結果を開くたびに、出力先ファイル名が必ず既存と異なる（未使用の）名前になるようにしたい」「ジョブ実行時のログを `出力先のパス\{yyyymmddhhmm}-log.txt` に保存する。トラブルシューティング用」）、§9.1、§11.8、§11.9、`OutputPathPlanner`、`DurableQuantificationOrchestrator`、`JobCostLogger`。

本節は§9.1の命名規則（`eval-yyyyMMdd-HHmm[-NN]`、既存fileを上書きしない）と§11.9のジョブログの秘匿規則を維持し、結果画面の別名出力欄の既定値と、ジョブログのテキスト保存を追加する。`OutputPathPlanner`の命名規則を共有し、別の命名規則を作らない。

#### FR-107 別名workbook出力欄の既定名は未使用名
- 要求: 結果画面の「別名 workbook 出力」欄（`ResultsOutputViewModel.OutputPath`）の既定値は、run完了後（`Summary.IsDurable`）や結果を開いたときも、自動出力済みのfinal／partialと一致せず、出力先directory（自動出力のresult directory）で未使用の名前とする。既定名は§9.1・`OutputPathPlanner`と同じ`eval-{yyyyMMdd-HHmm}[-NN].xlsx`（同一分の衝突は`-02`、`-03`…の次の未使用名）で、時刻は既定値を作る時点のOSローカル時刻・不変カルチャとする。ジョブを動かすたび・結果を開くたびに既定値を作り直し、利用者が編集した値は同じ結果の表示中は保つ。既存fileの上書き禁止（§9.1）と、利用者が明示指定した既存名への「完成名が既に存在します。上書きせず、別の名前を指定してください。」の検証は維持する。既定名の生成に失敗しても結果表示・採点・出力を止めず、欄を空にして利用者の指定を促す。
- 決定状態: 承認済み（依頼 2026-11-03）　出自: 依頼　優先度: MUST（既定値のままでは出力できない不具合の解消）
- 上位: 既定制約（ui_policy）　出典: 依頼原文 2026-11-03（§11.24）
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: なし
- 関連する既存資産: §9.1、§11.8、`OutputPathPlanner`、`ResultsOutputViewModel`
- 受入基準 AC-060: 出力先directoryに`eval-20261103-2136.xlsx`（自動出力済みのfinal）がある合成run結果を開いた「別名 workbook 出力」欄の既定値は、既存のどのfileとも異なる`eval-yyyyMMdd-HHmm[-NN].xlsx`の名前で、検証して出力を選ぶと上書きなしでそのまま出力できる。同じ分の既存名（`eval-yyyyMMdd-HHmm.xlsx`、`-02`）があれば次の未使用の連番になる。
  - 検証レベル: unit
- 受入基準 AC-061: ジョブを実行してrunが完了（`IsDurable`）した直後の結果画面と、同じ結果を再度開いたときの両方で、既定値が自動出力済みのfinal pathと異なる未使用名になり、既存fileは変更されない。利用者が明示指定した既存名は従来どおり「完成名が既に存在します。上書きせず、別の名前を指定してください。」で拒否される。
  - 検証レベル: integration

#### FR-108 ジョブ実行ログのテキスト保存
- 要求: ジョブ（「定量化を開始」または再開の1操作、§11.9）を実行するときの経過ログを、トラブルシューティング用に自動出力のresult directoryへ`{yyyyMMddHHmm}-log.txt`（UTF-8のplain text）として保存する。時刻はジョブ開始のOSローカル時刻・不変カルチャとする。同名のfileがあれば上書きせず、`{yyyyMMddHHmm}-02-log.txt`、`-03-log.txt`…の次の未使用名にする。出力先は§9.1の実効出力先（自動出力のresult directory）と同じである。記録できる内容は§11.9のJSONLログと同じ秘匿規則に従い、数値・生成ID・閉じたコード・匿名化したmodel識別子だけとする。回答・Prompt・reason・evidence・学生識別子・実path・credential・設問文を記録しない。ログの保存失敗（directory作成不可・書込不可・容量等）は採点・出力・§11.9の利用者別JSONLログ・画面表示を止めず、安全な理由（閉じたコードの文言。実pathや例外の本文を含めない）で表示する。
- 決定状態: 承認済み（依頼 2026-11-03）　出自: 依頼　優先度: SHOULD（障害調査のための補助。採点の成否に影響しない）
- 上位: 既定制約（ui_policy）　出典: 依頼原文 2026-11-03（§11.24）
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: なし
- 関連する既存資産: §9.1、§11.9、`JobCostLogger`、`DurableQuantificationOrchestrator`
- 受入基準 AC-062: 合成run（fake AI・合成workbook）を実行すると、出力先の result directoryに`{yyyyMMddHHmm}-log.txt`（ジョブ開始のローカル時刻）が1件できる。同名が既にあるときは上書きせず`-02`の連番で別fileができ、既存fileの内容は変わらない。
  - 検証レベル: integration
- 受入基準 AC-063: 合成の回答・Prompt・reason・evidence・学生識別子・設問文・実path・credentialに見える固定文字列を入れたrunで、保存した`-log.txt`にそれらの文字列が一切含まれない。出力先を書込不可にした場合も、採点・自動出力・利用者別JSONLログは成功し、画面には実pathを含まない安全な理由が表示される。
  - 検証レベル: integration

仮定: ログ内容は§11.9のJSONLログで記録済みの項目の範囲とし、新しい記録項目は足さない。連番の書式は§9.1に合わせた`-02`とする（既存の`{yyyyMMddHHmm}-log.txt`を1番目とする）。再開ジョブは再開時刻で別のログfileを作る。ログの保持期間・自動削除は定めない。覆す条件: 要求所有者が別の書式・保持を指示した場合。

変更履歴: 2026-11-03 依頼を反映。§9.1・§11.9に参照を追記（既存規則は変更せず）。FR-107・FR-108、AC-060〜063を追加（既存に同目的の要求なし。`rdcheck similar`で類似0件）。影響: `ResultsOutputViewModel`の既定名生成、`OutputPathPlanner`の再利用、`DurableQuantificationOrchestrator`／`JobCostLogger`のテキストログ出力、台帳のケース追加（test-designer）、利用者文書（troubleshooting）。

### 11.25 [実行]画面の同一情報の重複表示の解消（2026-11-03追加）

出典: 依頼原文（2026-11-03「[実行]画面（ExecutionView / MainWindow のシェル帯）には、複数の同じ情報が表示されている。同じ情報は 1 つだけにする。」(1)「次回新規出力」欄と「今回run 新規出力先: …」欄（`CurrentRunOutputTextBox`）の二重表示、(2)実行中の画面本文の進捗と下部シェル帯の進捗の二重表示）、`src/StudyReportEvaluator.App/Views/ExecutionView.axaml`、`MainWindow`のシェル帯の現行表示（確認済み）。

本節は§11.15のFR-PREP-02（「3 実行」が次回の出力先・進捗・「定量化を開始」「中断」を従来どおり表示する）を変えず、同じ内容を2か所に表示しない規則を足す。「同じ情報」は、同じ時点で同じ文言・値になる表示を指す。

#### FR-111 [実行]画面の同一情報は1か所だけ表示する
- 要求: 「3 実行」を表示しているとき、同じ情報を2か所に表示しない。(1) 「次回新規出力」欄の出力先と「今回run 新規出力先: …」欄（`CurrentRunOutputTextBox`）の出力先が同一のときは、今回run側の欄を表示しない（次回側だけ表示する）。両者が異なるとき（再開時を含む）は、今回run側の欄を従来どおり表示する。(2) 実行画面（Execution step）を表示中（設定画面を開いていない間。run前・実行中・完了後を問わない）は、下部のシェル帯の進捗表示（ステージ文・処理単位のテキスト・進捗バー）を隠し、画面本文の進捗表示（学生行の評価中・進捗バー・「処理単位: n / m 完了」）だけを表示する。シェル帯のボタン（「進捗へ」「停止」）は実行画面でも残し、従来の有効・無効条件と動作を変えない。Execution以外のstep（入力・採点設計・結果）や設定表示中は、従来どおりシェル帯に進捗を表示する。表示の出し分けだけを変え、進捗の計算・checkpoint・停止の動作・§11.15の部品配置は変えない。
- 決定状態: 承認済み（依頼 2026-11-03）　出自: 依頼　優先度: SHOULD（同じ情報の重複による読み違いと画面の混雑の解消）
- 上位: 既定制約（ui_policy）　出典: 依頼原文 2026-11-03（§11.25）
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: なし
- 関連する既存資産: §11.15（FR-PREP-02）、AC-037（全画面で進捗・停止を保持）、`ExecutionView`、`MainWindow`
- 受入基準 AC-068: 次回新規出力の出力先と今回runの新規出力先が同一のとき、「3 実行」で`CurrentRunOutputTextBox`が表示されず、次回新規出力の欄だけが出力先を表示する。両者が異なる（再開時など）ときは両方が表示される。
  - 検証レベル: integration
- 受入基準 AC-069: 「3 実行」（Execution step）を表示中（設定画面を開いていない間）、シェル帯の進捗のステージ文・テキスト・進捗バーが表示されず、画面本文の進捗（評価中・バー・「処理単位: n / m 完了」）は表示され、シェル帯の「進捗へ」「停止」は表示・操作できる。入力・採点設計・結果のstepへ移るとシェル帯に進捗が表示される。
  - 検証レベル: integration

仮定: 「同一」は表示文言（出力先path）の一致で判定する。覆す条件: 要求所有者が別の判定を指示した場合。

変更履歴: 2026-11-03（run 202611032335）依頼を反映。FR-111、AC-068・AC-069を追加。`rdcheck similar`で類似0件のため、FR-PREP-02（配置は不変）・AC-037（進捗・停止の保持は不変）を参照するだけで本文は変更しない（目的が「重複表示の解消」で既存と異なる）。影響: `ExecutionView.axaml`の`CurrentRunOutputTextBox`の表示条件、`MainWindow`シェル帯の進捗の表示条件、ExecutionViewTests・MainWindowTests、台帳のケース追加（test-designer）。競合なし。
変更履歴: 2026-11-04（run 202611040021）回答反映。FR-111とAC-069の条件を「Execution step表示中（設定画面を開いていない間）」に統一、AC-037に「実行画面では本文の進捗を保持とみなす」を追記（A-1・A-2解決）。実装は既にこの動作でコード変更なし。影響: AC-069のテストは条件文言のみ（台帳のケース追従はtest-designer）。

### 11.26 結果画面から選んだ行だけの再実行（2026-11-03追加）

出典: 依頼原文（2026-11-03「タスクが完了して［結果・出力］画面が自動表示された後で、エラーがあった場所（技術エラー等の行）や、利用者がチェックした特定の行だけを、再度［実行］画面に戻って、再タスク実行できるようにしてほしい」。添付画像: 行一覧に「状態: 成功／技術エラー」、左下に「← 実行へ戻る」ボタン）、§10.5、§11.1、`ExecutionViewModel`（Resume）、`ResultsOutputViewModel`。

§10.5の再開（未完了行以降を処理）は、中断したrunの続きを行う機能であり、完了済みrunの特定行の再処理は含まない。本節はこの差を埋める。既存のFR-001〜003・107〜110とは目的・対象が異なる（`rdcheck similar`で類似0件）。

#### FR-112 結果画面で選んだ行だけの再実行
- 要求: 利用者は、runの完了後に表示された結果画面の行一覧で、状態が「技術エラー」の行をまとめて選ぶ操作と、任意の行にチェックを付ける操作ができ、選んだ行の元の行番号だけを対象として実行画面へ戻り再実行できる。実行画面では対象行の件数と元の行番号が表示され、利用者が明示的に実行を開始するまでAI処理を始めない。再実行は、選ばれなかった行の既存の結果・採点・出力を変更せず、再実行した行の結果だけを置き換える。入力workbook・採点定義・modelを変更した場合など再実行の前提が元のrunと一致しないときは、不一致の項目を安全な文言で示して開始しない（§10.4と同じ検証）。選択が0件のときは再実行操作を無効にする。既存の「実行へ戻る」と§10.5の再開は変更しない。
- 決定状態: 承認済み（依頼 2026-11-03）　出自: 依頼　優先度: MUST（エラー行のために全行を再実行する費用と時間の削減）
- 上位: 既定制約（ui_policy）　出典: 依頼原文 2026-11-03（§11.25）
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: なし
- 関連する既存資産: §10.4、§10.5、§11.1、`ExecutionViewModel`、`ResultsOutputViewModel`
- 受入基準 AC-070: 合成run（行42〜51のうち2行が技術エラー、他は成功）の完了後の結果画面で「技術エラーの行を選ぶ」操作をすると、技術エラーの2行だけにチェックが付き、成功の行には付かない。任意の行のチェックの付け外しもでき、選択件数が表示される。選択が0件のとき再実行操作は無効である。
  - 検証レベル: unit
- 受入基準 AC-071: 結果画面で行を選んで再実行操作をすると、実行画面に切り替わり、対象の件数と元の行番号（例: 43、47）が表示される。この時点でAI処理は開始されておらず、利用者が実行を開始して初めて処理が始まる。
  - 検証レベル: integration
- 受入基準 AC-072: 選んだ行だけを再実行すると、fake AIへの呼出しは選んだ行の分だけで、選ばれなかった行の結果・採点値は再実行の前後で変わらず、選んだ行の結果だけが新しい結果（成功または再度の技術エラー）に置き換わる。
  - 検証レベル: system
- 受入基準 AC-073: 入力workbookまたは採点定義を元のrunから変えた状態で再実行を開始しようとすると、不一致の項目が実path・回答本文を含まない安全な文言で表示され、AI処理は開始されず、既存の結果は変更されない。
  - 検証レベル: integration

仮定: 再実行した結果は元のrunの出力へ行単位でマージし、元の出力workbookは上書きせず§9.1の規則で新しい名前に出力する。再実行に使うmodelは元のrunと同じとする。「技術エラー」以外のエラー種別（採点不能など）の一括選択は、利用者の任意チェックで対応し一括操作は技術エラーだけとする。覆す条件: 要求所有者が別のマージ・出力方式を指示した場合。

変更履歴: 2026-11-03 依頼を反映。FR-112、AC-070〜073を追加（既存に同目的の要求なし。`rdcheck similar`で類似0件、参照されたFR-001〜003・107〜110とは対象が異なる）。影響: `ResultsOutputViewModel`（行選択・再実行操作）、`ExecutionViewModel`（対象行の受取・件数表示・§10.4検証の再利用）、ResultsOutputView／ExecutionView、台帳のケース追加（test-designer）、docs/settings.md等の利用者文書。

### 11.27 結果画面の成功件数とエラー種別ごとの件数（2026-11-04追加）

出典: SRC-006（依頼原文「[4.結果・出力]画面で、Copilotによるタスクが完了したら、成功件数とエラー件数（エラーの種類別）も画面内に表示してほしい。エラーがあったかどうかの確認に使う」。添付画像: 「前回の実行結果」の`3,916 / 3,916 完了 · error 73 · cancelled 0`と、行の状態列の「成功」「技術エラー」）、`ResultsOutputViewModel`（`RunSummaryText`、`ResultsRowStatus`）、§11.1、§11.26。

既存の件数要約（`ResultsRunSummary`）はoperation（評価の送信単位）の完了数・error数・cancelled数だけを示し、エラーの種類と、学生行としての成功件数が分からない。本節は、既存の行状態の分類（成功・回答空欄・取消・未処理・未確定・技術エラー）に従う学生行の件数を併せて示す。既存の件数要約は変更しない。既存のFR-RT・FR-RS・FR-112とは目的が異なる（`rdcheck similar`で類似0件）。

#### FR-117 結果画面の行状態別件数（成功件数とエラー種別ごとの件数）
- 要求: 利用者は、runの完了後の結果画面で、件数要約の近くに、学生行の状態別の件数（成功の件数、および成功以外の状態＝技術エラー・回答空欄・取消・未処理・未確定ごとのエラー件数）を、一覧の「状態」列と同じ名称で確認できる。件数は結果画面が保持するrun結果の全行を集計し、一覧のページや選択に左右されない。0件の状態も0として示し、全状態の件数の合計は全行数と一致する。run結果を読み込んでいないときは表示しない。表示は読取専用で、採点値・出力・checkpoint・overrideを変更しない。
- 決定状態: 承認済み（依頼 2026-11-04）　出自: 依頼　優先度: MUST（エラーの有無と種類を再実行や修正の前に確認するため）
- 上位: 既定制約（ui_policy）　出典: SRC-006
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: なし
- 関連する既存資産: §11.1、§11.26、`ResultsOutputViewModel`、`ResultsOutputView`
- 受入基準 AC-091: 合成run（全10行: 成功6、技術エラー2、回答空欄1、取消1）の結果画面を読み込むと、状態別件数の表示が`成功 6 · 技術エラー 2 · 回答空欄 1 · 取消 1 · 未処理・未確定 0`と完全一致し、合計が10行と一致する。技術エラーが0件のrunでは`技術エラー 0`と表示する。run結果の未読込時は表示が空で非表示である。
  - 検証レベル: unit
- 受入基準 AC-092: 結果画面の一覧のページを移動しても、行を選択しても、状態別件数は変わらない。別のrun結果を読み込むと新しい結果の件数に更新され、表示は1024×720 DIP以上で省略記号なしに全文が見え、Accessible Nameに同じ文を持つ。
  - 検証レベル: integration

仮定: 「エラー種別」は既存の行状態の分類（技術エラー・回答空欄・取消・未処理・未確定）とし、技術エラーの内部の詳細な原因別（timeout等）の内訳は含めない。成功以外の状態をすべて「エラー件数」の種別として並べる。覆す条件: 要求所有者が技術エラーの原因別の内訳を求めた場合。

変更履歴: 2026-11-04 依頼を反映。FR-117、AC-091〜092を追加（既存に同目的の要求なし。`rdcheck similar`で類似0件、§11.21・FR-112とは対象が異なる）。影響: `ResultsOutputViewModel`（状態別件数のプロパティ）、`ResultsOutputView`、台帳のケース追加（test-designer）、docs/features.md等の利用者文書。

### 11.28 エラー表示の配色を赤系にする（2026-11-05追加）

出典: SRC-007（依頼原文「エラー表示（技術検証エラー・設定エラー・上書きエラー・必須エラー）の配色を赤系にする」）、`App.axaml`の`Technical*Brush`、§2.3。

現行のエラー表示（`TechnicalBackgroundBrush`・`TechnicalBorderBrush`・`TechnicalTextBrush`）は青系で、エラーと気づきにくい。既存の画面要求（FR-111・FR-117など）は配色を規定せず、配色・アクセシビリティの既存の要求・NFRはない（`rdcheck similar`で類似0件）ため、新規に1件追加する。

#### FR-118 エラー表示は赤系の配色で、読みやすく表示する
- 要求: 利用者は、技術検証エラー・設定エラー・上書きエラー・必須エラーの表示を、赤系の配色（{PARAM-007}。背景・枠・文字）で見て、エラーだと見落とさずに判別できる。エラー文字と背景のコントラスト比は{PARAM-005}以上、枠・アイコンと隣接色のコントラスト比は{PARAM-006}以上とする（WCAG 2.2の1.4.3・1.4.11）。色だけに頼らず、「エラー」を示す文字または枠の太さを併用する（既存を維持する）。エラー表示で青系のままの箇所を残さない。キャンセル操作（MainWindowの`Button.shell-action.cancel`）はエラーではないため、青系（アクセント）のままにする。画面内のすべての文字は、重ならず、切れずに読める。配色以外（文言・表示条件・データ・動作）は変えない。Microsoft Design（Fluent 2）の原則に従う。
- 決定状態: 承認済み（依頼 2026-11-05）　出自: 依頼　優先度: MUST（エラーの見落としの防止）
- 上位: 既定制約（ui_policy）　出典: SRC-007
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: PARAM-005、PARAM-006、PARAM-007
- 関連する既存資産: §2.3、`App.axaml`（`TechnicalBackgroundBrush`・`TechnicalBorderBrush`・`TechnicalTextBrush`）、`MainWindow`
- 受入基準 AC-093: エラー表示の文字色と背景色のコントラスト比が{PARAM-005}以上、枠の色と隣接色（エラー背景と画面背景の両方）とのコントラスト比が{PARAM-006}以上である（ブラシの実値から計算して確かめる）。
  - 検証レベル: unit
- 受入基準 AC-094: 技術検証エラー・設定エラー・上書きエラー・必須エラーの各表示が、赤系のブラシ（{PARAM-007}）だけを使い、青系の旧値（#EAF5FF・#62A9E8・#174F7C）をエラー表示に使う箇所が残らない。キャンセル操作（`Button.shell-action.cancel`）は青系（アクセント）のままである。
  - 検証レベル: integration
- 受入基準 AC-095: 各エラー表示は、色に加えて「エラー」を示す文字または太い枠を持つ（既存の併用を維持し、色を取り除いても識別できる）。
  - 検証レベル: integration
- 受入基準 AC-096: 4種のエラーを表示した状態を、最小window（1024×720 DIP）で実際に描画し、画面内のすべての文字が重ならず、切れずに全部読めることを確かめる（描画した画像を証跡とする）。
  - 検証レベル: system

仮定: 「エラー表示」は現行の`Technical*Brush`を使う表示を指し、成功・警告・情報の表示の配色は変えない。覆す条件: 要求所有者が他の表示の配色変更も求めた場合。

変更履歴: 2026-11-05 依頼を反映。FR-118、AC-093〜096、PARAM-005〜007、SRC-007を追加（既存に同目的の要求なし。FR-111・FR-117とは対象が異なる）。影響: `App.axaml`の3ブラシ（背景#FDE7E9・枠#C50F1F・文字#A4262C）、`Technical*Brush`の参照箇所、`MainWindow`のキャンセル操作（青のまま）、台帳のケース追加（test-designer）。競合なし。

### 11.29 実行画面の処理単位の進捗に成功数・失敗数を加える（2026-11-05追加）

出典: SRC-008（依頼原文、`ExecutionViewModel.ProgressText`、`ExecutionView.axaml`の`OperationProgressSummary`、`MainWindow.axaml`のAutomationProperties.Name、`DurableQuantificationOrchestrator`のcheckpoint、`RunSummary.OperationFailureCount`の定義）。

`rdcheck similar`で類似0件。FR-111は進捗の「表示場所」（1か所だけ）、FR-117は結果画面の「学生行」の状態別件数で、本件の「実行中の処理単位の成功・失敗数」とは対象が違うため新規に1件追加する。FR-111・AC-069は変更しない（シェル帯が同じ進捗文を使うため連動して表示が変わってよい）。

#### FR-119 [実行]画面の処理単位の進捗に成功数と失敗数を示す
- 要求: 利用者は、「3 実行」の統計情報の進捗行（`OperationProgressSummary`）で、処理単位（AIへの1回の評価のまとまり）の完了数・総数に加えて、成功した処理単位の数と失敗した処理単位の数を確認できる。表示は「処理単位: 124 / 3,916 完了 · 成功 120 · 失敗 4 · 15 実行中 · 3.2%」の形（完了・成功・失敗・実行中・割合の順）とする。(1) 実行中の成功数・失敗数は、checkpoint済みの処理単位の結果の状態コードから集計する（成功＝Success・Empty、失敗＝Success・Empty・NotRunZeroBudget以外。`RunSummary.OperationFailureCount`と同じ定義）。(2) run完了時は`RunSummary.OperationFailureCount`の値を失敗数に使い、成功数は完了数から失敗数とNotRunZeroBudget（予算ゼロで未実行）の数を引いた値と一致させる。(3) 成功＋失敗は完了数を超えず、実行中の件数は含めない。(4) 集計できない間（run前・件数未報告）は成功・失敗を表示しない（0と偽らない）。シェル帯の進捗文は同じ進捗文を使うため連動してよい（FR-111・AC-069の表示の出し分けは変えない）。進捗行のツールチップとAccessible Name（ExecutionViewとMainWindowのシェル帯）は、「成功数とは別」「送信回数とは別」と書かず、「処理単位の完了数・成功数・失敗数・実行中の件数です。学生行数やAIへの送信回数ではありません」の趣旨に改める（成功数・失敗数を表示するため、「成功数とは別」の旧文は矛盾する）。読取専用で、進捗の計算・checkpoint・停止・採点値・出力は変えない。表示は1024×720 DIP以上で省略記号なしに全文が見える。
- 決定状態: 承認済み（依頼 2026-11-05）　出自: 依頼　優先度: MUST（実行中に失敗の有無を早く確認し、停止・再実行の判断をするため）
- 上位: 既定制約（ui_policy）　出典: SRC-008
- 対象エンティティ: なし　関係する状態: なし　参照パラメータ: なし
- 関連する既存資産: FR-111、FR-117、§11.15、`ExecutionViewModel`（`ProgressText`）、`ExecutionView`、`MainWindow`（`ShellRunProgressText`）、`DurableQuantificationOrchestrator`、`RunSummary`
- 受入基準 AC-097: 処理単位の結果の状態コードが（Success 3、Empty 1、技術エラー 1、NotRunZeroBudget 1）で、完了6・総数10・実行中2のとき、進捗文が`処理単位: 6 / 10 完了 · 成功 4 · 失敗 1 · 2 実行中 · 60.0%`に完全一致する（NotRunZeroBudgetは成功にも失敗にも数えず、成功＋失敗が完了数を超え得る前提を置かない）。run前・件数未報告の間は成功・失敗の語が表示されない。
  - 検証レベル: unit
- 受入基準 AC-098: run完了後、進捗文の失敗数が`RunSummary.OperationFailureCount`と一致し、成功数が完了数から失敗数を引いた値（NotRunZeroBudgetを除く）と一致する。実行中から完了へ切り替わっても、失敗数が減らず、完了時の値で確定する。
  - 検証レベル: integration
- 受入基準 AC-099: 「3 実行」の`OperationProgressSummary`・そのツールチップ・Accessible Name、およびシェル帯（`ShellRunProgressText`）のAccessible Nameに、「成功数とは別」「送信回数とは別」の語が残らず、成功数・失敗数を含む説明になっている。進捗行の全文は1024×720 DIP以上で省略記号なしに見え、Accessible Nameが表示文と同じ内容を持つ。FR-111のAC-069（実行画面ではシェル帯の進捗を隠す）は変わらず満たす。
  - 検証レベル: integration

仮定: 「失敗」はFR-117の学生行の状態分類（技術エラー・回答空欄・取消・未処理・未確定）ではなく、処理単位の状態コードで数える（`RunSummary.OperationFailureCount`の定義に従う）。そのため学生行の件数（FR-117）と数が一致するとは限らない。覆す条件: 要求所有者が学生行単位の数を求めた場合。

変更履歴: 2026-11-05 依頼を反映。FR-119、AC-097〜099、SRC-008を追加。既存の要求に「成功数とは別」の文はなく、矛盾は実装側の文言（`ExecutionView.axaml`:128・132、`MainWindow.axaml`:242）だけにあったため、FR-119の要求・AC-099で改める対象にした。影響: `ExecutionViewModel`の集計と`ProgressText`、`ExecutionView`・`MainWindow`の文言、台帳のケース追加（test-designer）。競合なし（FR-111・AC-069・FR-117は変更しない）。

## 12. Promptファイルからの起動

### 12.1 command line

次をサポートする。

`StudyReportEvaluator.App --input <xlsx-path> --prompt <txt-path> [--prompt <txt-path> ...]`

単一EXEの配布名`StudyReportEvaluator-win-x64.exe`でも同じ引数契約を提供する。ZIP内の`StudyReportEvaluator.App.exe`による既存起動を維持し、新しい起動引数は追加しない。

- `--input`は任意。指定時は入力pathを事前入力し、安全なread-only読込を開始する。
- `--prompt`は複数指定可能。
- Prompt fileはUTF-8 plain text `.txt`とする。
- 1fileは32,767文字以下とする。
- unknown option、重複`--input`、missing value、非txt Promptを明確な起動errorとする。
- 相対pathは起動時のcwdを基準に解決し、EXE配置先やruntime抽出先へ変更しない。任意cwd、日本語・空白を含むpath、複数Promptの順序と同一pathの既存取扱いを維持する。

### 12.2 GUIへの反映

- 【廃止（Q-001回答B、2026-11-02、FR-003）】Promptの一覧表示・件数・「Promptを適用」の操作は廃止した。command lineの`--prompt`は受理するが、UIへ反映せず適用しない。以下の一覧・適用に関する記述は廃止した機能のものとして読む。
- 読み込んだPromptはbasenameと本文を一覧へ表示し、Design主画面に件数と適用の入口を残す。設定の「読込Prompt」カテゴリは廃止した（2026-11-02、§11.23）。
- Prompt一覧はcommand lineへ指定された順序を維持し、同じPromptを複数の対象へ再利用できる。
- 利用者が一覧からPromptを選び、通常Custom evaluatorまたは固有項目を選んだうえで「Promptを適用」buttonを明示実行する。
- filename規則による設問への暗黙割当を行わない。
- Prompt適用はtemplateのcopyだけを行い、step遷移、認証確認、loginやAI処理を開始しない。Execution画面の「実行」buttonを利用者が押すまでAI送信を0件とする。未適用Prompt一覧はsetting.txtへ保存せず、保存定義の適用でも変更・消去しない。
- warningと設計検証を表示し、利用者が実行buttonを押した場合だけrunを開始する。

独自VS Code拡張、custom URI scheme、background daemonは初版へ追加しない。GitHub CopilotのPromptから実行fileと引数を指定する運用例を文書化する。

## 13. 配布とinstall

### 13.1 共通

- end-user packageは.NET 10 self-containedとし、利用端末への.NET Runtime／SDK導入を不要にする。
- end-user runtimeのCopilot CLIはOS利用者が導入し、PATHから解決する。CLI runtimeの同梱は必須ではない。
- 通常のApp buildでは`CopilotSkipCliDownload=true`を設定し、SDK／buildがCLIを自動downloadしない。歴史的packageのpublishだけは明示的にCLI fileを供給して同梱してよいが、runtimeは同梱fileもmanifestも選択・利用しない。
- source build用SDKを一般利用者端末へ導入しない。
- GitHub loginは利用者本人の対話が必要であり、scriptがcredentialを収集しない。
- end-user primary pathは正式配布元から取得済みのWindows単一EXEを開く操作からGUI表示までとする。ダブルクリックを1起動gestureと数え、download、任意のhash比較、OS警告への操作、本人loginは含めない。ZIPは手動展開を伴う代替経路として残す。
- SHA-256 sidecarはEXE／ZIPの両方で公開し、CI・公開gateで最終bytesとのexact一致を必須とする。利用者の手動hash比較は任意の推奨であり、起動の必須操作ではない。sidecarをEXEのruntime依存にしない。同一配布元のhash一致は発行者の真正性やSmartScreen reputationを保証しない。
- package作成、展開、起動は入力／出力／checkpoint workbookを変更または削除しない。
- test certificate、unsigned package作成、cross-publishだけの成功は`PASS_MECHANISM`であり、正式公開証跡にしない。単一EXEの公開には§13.6〜13.7のexact artifact検証とclean-host実測を別途要求する。

### 13.2 Windows

- Windows 11 x64のpublic primary artifactは`StudyReportEvaluator-win-x64.exe`と`StudyReportEvaluator-win-x64.exe.sha256`とする。GitHub Releasesからdirect配布し、取得済みEXE1個から手動展開・追加installなしに入力画面を開く。
- 代替artifactは`StudyReportEvaluator-win-x64.zip`と`StudyReportEvaluator-win-x64.zip.sha256`とする。ZIPを新しいdirectoryへ展開し、`StudyReportEvaluator.App.exe`を起動する既存経路を維持する。両経路の手動hash比較は§13.1の任意推奨とする。
- EXE／ZIPはunsignedであり、SmartScreen、Smart App Control（SAC）、企業policyによる警告・実行拒否があり得る。「誰でも1操作」「すべての端末で警告なし」とは表示しない。拒否を回避する保護無効化、MOTW除去、証明書の自動trust、execution policy変更、UAC回避を実装・案内しない。
- Windows 11で証明書なしのcontainer互換性を先行試験する場合は、Publisherの最終fieldに固定marker`OID.2.25.311729368913984317654407730594956997722=1`を置き、明示的なunsigned test artifact名を使用する。実行codeを含むため、使い捨てVMまたは復元可能なsnapshot上で管理者PowerShellの`Add-AppxPackage -AllowUnsigned`による全ユーザーinstallとして実行し、結果を`PASS_MECHANISM`に限定する。この経路を一般利用者setup、signature trust、production配布の証拠にしない。
- development MSIXの既存required gateはpackage作成、unpack、manifest/version/RID、block map、public payload、歴史的package内CLI hash、sidecar、policy negative、cleanupまでのpackage integrity確認として維持する。これはruntimeでbundled CLIを選ぶ根拠ではない。install／launch／upgrade／repair／uninstallは実行した場合だけ追加の`PASS_MECHANISM` evidenceとし、初回公開をblockしない。
- development MSIXの既存sourceと非公開regressionを維持するだけとし、新しいinstaller機能やrequired install試験へ拡張しない。
- 既存public ZIPのclean展開先におけるbundled CLI identity確認は歴史的package artifactのintegrity確認として扱い、runtimeではPATH CLIだけを使う。installer、trusted package、SmartScreen reputation確立済みとは表示しない。
- engineering用publish/package scriptはPowerShell 7以上だけを使用し、Windows PowerShell 5.1へfallbackしない。PowerShellをend-user setup要件にしない。

### 13.3 macOS source foundation（現版公開対象外）

- `osx-arm64`と`osx-x64`のpublish、bundle、sign、notary script foundationをrepositoryに保持し、secretなしのstatic contractを検証できる。
- bundle identifierは`com.github.dahatake.study-report-evaluator`とし、`Info.plist`のexecutable、version、icon、minimum OSをartifactへ一致させる。
- DMGの一般利用者手順とdownload URLは、将来のproduction evidenceが揃うまで公開しない。
- apphostとPATHから解決するUnix `copilot` executableをnative architectureで検証し、x64成功をArm64へ、Rosetta成功をnative x64／Arm64へ代用しない。
- Developer ID Applicationでnested executableを内側から署名してからouter appを署名する。歴史的packageにCLI file／manifestを明示的に残す場合の署名・hash確認はpackage integrityだけに使い、runtimeはmanifestを参照せずPATH CLIを使う。outer署名後にbundleを変更しない。
- hardened runtime、必要最小限のentitlement、secure timestamp、appとDMGのnotarization／stapling／validation、quarantine付きFinder launchを必須とする。
- macOS 14、15、26は将来の試験候補であり、現版では対応表示しない。将来scopeへ追加する場合だけ、exact OS build × architectureの`PASS_PRODUCTION`行を要求する。

### 13.4 非対応platform・外部前提・release境界

- Linux、Windows Arm64、macOS 13以前、universal macOS artifact、Microsoft Store、Mac App Storeは本版scope外とする。
- Windows production signing identityは現版で要求しない。Apple Team／Developer ID／notarization credential、approved visual assets、macOS clean hostsをrepository内の仮値で代用しない。
- macOS外部入力がない状態は現版のrequired release blockerにせず、macOS artifactと対応claimを公開しない。
- Windowsのbuild、macOS cross-publish、Avalonia/.NETの一般的なcross-platform対応を、対象platformでの本製品install／launch／CLI／workbook証跡として代用しない。

### 13.5 Windows単一EXEの標準publish・展開

- production projectは既存のCoreとAppの2つだけを維持する。Windows single-file profileをAppだけへ適用し、solution全体やCoreへ適用しない。通常folder／ZIP publishの引数なし動作・出力とmacOS source/static contractは変更しない。
- restoreとpublishに同じ`win-x64`、self-contained、`PublishSingleFile=true`、`IncludeNativeLibrariesForSelfExtract=true`、`IncludeAllContentForSelfExtract=true`の条件を渡し、folder出力から分離する。`PublishTrimmed=false`、`PublishReadyToRun=false`、圧縮無効、symbols非配布を維持する。固定versionはheaderのとおり変更せず、実在するsingle-file build-only依存だけをlockし、検証やanalyzerを無効化して通さない。
- `IncludeAllContentForSelfExtract`はMicrosoftが**非推奨**とする.NET Core 3.1互換モードであり、将来削除される可能性がある。S01で固定.NET SDK `10.0.400`／runtime `10.0.11`と固定Avalonia／SDK／CLIへの適合を開発hostで確認したことを条件に採用する。これはclean-host成功や将来versionの互換性を保証せず、正式公開前には§13.7の実測を必須とする。
- default App buildは`CopilotSkipCliDownload=true`を設定し、Copilot CLIをdownloadしない。歴史的publishだけが必要に応じCLI file／manifestを明示供給できる。repository全体のglobを使わず、sample、利用者input／final／partial、setting.txt、work、tests、secretを含めない。最終配布名へ配置したEXEのbytesからsidecarを作り、検証後にEXEを書き換えない。
- runtimeは`AppContext.BaseDirectory`配下のCLIやmanifestを解決に使わず、§3.2のPATH規則でOS-installed CLIを検出する。歴史的packageのRID／CLI版／CLI SHA-256検査はpackage artifactの検証に限定し、runtimeの固定版一致・trust attestation・PATH CLIからのfallback禁止には使わない。
- Windowsでは.NET標準hostが起動前に通常`%TEMP%/.net/<app>/<bundle-id>/`配下へ内容を展開する。1ファイル配布は「ディスク上でも1ファイル」「痕跡なし」を意味しない。`DOTNET_BUNDLE_EXTRACT_BASE_DIR`は試験の隔離に使えるが、利用者の設定や独自環境変数・API keyを要求しない。
- 標準hostのcache再利用・欠落復元・並行起動処理を利用し、独自の展開／cache／locking engine、cache管理UI、自動掃除を追加しない。標準抽出を全cached fileの暗号学的検証とみなさず、CLI integrity検証を残し、別権限userから書換え可能な抽出先を対応済みとしない。
- cacheはアプリ配置用であり、input／final／partial／setting.txtの保存先にしない。§9.1の明示出力先を復元し、未指定（null）の場合だけ入力隣接の`result`を使う。配布・展開・起動のためにworkbookを移動・削除しない。checkpoint形式と同じ製品版・runtime identityのEXE／ZIP間の既存再開条件を維持する。§9〜10のatomic finalizationと完成成功後のpartial cleanupは変更しない。

### 13.6 公開matrix v2とcandidate拘束

新経路の公開はmachine-readable matrix v2を使い、次の3行だけのclosed setとする。v1は過去の契約として残し、unknown／duplicate／missing rowを受け入れない。

| Required row | artifact／sidecar | publish | 必要な証跡 |
|---|---|---|---|
| Windows x64単一EXE | `StudyReportEvaluator-win-x64.exe` / `StudyReportEvaluator-win-x64.exe.sha256` | `true` | package required testsとexact EXEのCH-01〜06 |
| Windows x64 ZIP | `StudyReportEvaluator-win-x64.zip` / `StudyReportEvaluator-win-x64.zip.sha256` | `true` | 既存ZIPのpackage／clean extract／起動／CLI／data保護のrequired tests |
| Windows x64 development MSIX | candidateで実物検証したartifact／sidecarのdescriptor | `false` | 既存範囲の`PASS_MECHANISM`検証記録 |

1. 順序は**候補生成 → exact EXEのclean-host試験 → protected publish時のv2最終matrix確定**とする。candidate生成時に未実施のclean-host結果や公開可能matrixを生成しない。candidateのpackage／mechanism required testsを満たした後に限り、EXE／ZIPと各sidecarの計4 assetをdraftへ添付する。publicのassetも同じ4個だけとする。
2. EXE／ZIPは同じsource commit、製品版、SDK／CLI版から作る。candidate run ID／commit、artifact basename／bytes／SHA-256、sidecar、package evidence、clean-host evidenceを同じ成果物へ結び付ける。doc同梱や版変更等で最終EXEのbytesが変われば、以前のclean-host結果を流用せず再package・再検証する。
3. development MSIXは同じcandidate runで実物検証した結果とartifact／sidecar descriptorを既存の内部control artifactに保存する。本体をGitHub ReleaseにもActions artifactにもuploadしない。公開時はcandidateに拘束された記録を照合し、MSIX本体を再取得・再作成・再検証したと扱わない。
4. clean-host担当者は当該candidateのEXEで試験し、candidate run ID／commit、EXE basename／bytes／SHA-256／製品版を含むmetadata限定のclosed JSONを作る。OS edition／build／architecture、標準user、追加依存・保護状態、実build SDK／bundled runtime／Copilot SDK・CLIの版とCLI hash、試験ID別結果と操作数、実施記録の参照／hashを記録し、username、token、device code、学生本文、環境変数値一覧、生ログを含めない。公開前に製品repositoryへcommitしない。
5. 既存protected publish workflowの`clean_host_evidence_json`入力でJSONを受け、環境変数経由で一時file化し、型・長さ・許可field・必須試験IDを検証する。workflow式をshell本文へ直接埋め込まない。新しいstorage／workflow／証跡基盤を作らず、人の試験記録であることを明記し、hash一致だけで実施事実が自動証明されたとしない。
6. protected publishはcandidateが指定repositoryの`release.yml`による成功runであることとtag commitの一致を確認する。EXE／ZIPの4 assetを再downloadしてversion／bytes／hash／sidecarを照合し、MSIXは同runの検証記録とdescriptorを照合する。v2最終matrixと受領JSONは内部control artifactへ保存し、public assetへ含めない。
7. 既存Core／App／packageのrequired testsと§13.7の必須試験がすべてPASSの場合だけ公開可能とする。必須試験の欠落・FAIL・NOT_RUN、別candidate／source／version／hashの証跡、sidecar不一致を拒否する。unsignedの`PASS_REQUIRED`は署名・installer等の`PASS_PRODUCTION`を意味しない。tag／push／draft／public Releaseの操作承認は実装承認から分離する。public化にはprotected environmentの公開承認を要求する。

### 13.7 clean-host公開必須試験

fresh Windows 11 x64実機またはVMの標準userでexact EXEを試験する。開発hostのPATH／.NET環境変数隔離やhosted CI成功をOS-only証跡にしない。guestに検証用SDKやPowerShellを導入してからOS-onlyと呼ばず、OS付属.NET Framework等と追加.NET Runtimeを区別する。EXE size、展開容量、初回／再起動の所要時間は実測を記録し、数値SLAは設けない。

| ID | 内容 | 公開条件 |
|---|---|---|
| CH-01 | OS edition／build／x64、fresh標準user、.NET SDK／Runtime、PowerShell 6+、Node／npm、Git／gh、別Copilot CLI、Office、IDEの未導入を確認 | 必須PASS |
| CH-02 | EXE1個だけからofflineで入力画面、Excel読込、設計を利用。sidecar／repository／隣接file／既存CLI cache・認証に依存しない | 必須PASS。保護機能による実行拒否を起動成功としない |
| CH-03 | GUI-only起動をCLI未導入の状態で確認した後、OS利用者がWinGet等で導入したCLIを絶対PATH entryから解決し、Start／Ping／auth／model／AI runtimeの契約を確認。空・相対PATH entryを無視し、CLI identityは診断情報だけとする | 必須PASS。正常な未認証応答、CLI不在、SDK非互換を区別し、bundled CLI／manifestを使わない |
| CH-04 | 移動、再起動、同時起動、任意cwd／起動引数、日本語・空白path、read-onlyなEXE配置先、cache欠落からの復元、data保護 | 必須PASS |
| CH-05 | 標準ブラウザーで取得したMOTW付きEXEのSmartScreen／SAC／企業policy状態、警告・拒否、実際の操作数を記録し承認範囲と比較 | 必須PASS。無警告を一律要求せず、警告や追加操作を隠して1操作成功へ丸めない |
| CH-06 | fresh userの本人login、既存buttonでの完了後・再起動後の再確認、取消／アプリ終了時の当該login processだけの終了と他process／data／credential保護 | D-06採用済みのため必須PASS。JSONで任意化・N/A化しない |
| ADV-01 | 本人が明示承認したsynthetic入力による実AI評価 | 任意。NOT_RUN可。必須GUI／CLI／login試験の代替にしない |
| ADV-02 | Office等の外部spreadsheetによる再計算 | 従来どおり任意。NOT_RUN可 |

fake／help／process終了だけの成功はCH-06の本人認証に代用しない。必須試験未実施は公開を拒否するが、ADV-01／02のNOT_RUNを必須試験の失敗へ変換しない。

## 14. privacy・security・安全境界

- AIへ送るworkbook由来の値は、現在行の選択済みprimary/supporting/special sourceだけとする。
- question text、Prompt、criterion metadata、参照回答、closed schema metadataは必要範囲で送る。
- 他行、非選択列、workbook pathを送らない。
- reference answerはその質問の全学生比較へ使用する。類似度はローカル計算のため、学生回答と参照回答を類似度のためにAIへ送らない。
- untrusted textはExcel string cellとして保存し、formulaとして実行しない。
- outputとpartial workbookは元本全体、Prompt、AI結果を保持するため、元本と同等以上に機密として扱う。
- 実行画面の速報値（§11.12）の対象の文字列・Prompt・AI理由・根拠は画面とメモリだけに置き、log・checkpoint・setting.txtへ新たに保存しない（SEC-LP-01）。
- 結果画面の詳細は、選択した1行の回答列だけを入力Excelから読取専用で再読込し、実行時のSHA-256・サイズ・更新日時と一致した場合だけ表示する。回答・理由・根拠は画面とメモリだけに置く（§11.19、SEC-RV-01）。
- app-owned cloud backend、database、telemetry本文送信を追加しない。
- checkpointは暗号化containerではない。保存先のaccess controlは利用者のOS権限に従う。
- runtime抽出cacheと利用者workbook／CLI credential storeを分離する。配布・展開・起動のために利用者workbookを移動・削除しない。アプリはcacheの再帰削除、旧版の自動掃除、credential削除を行わない。
- loginではPATHから解決したOS CLIとブラウザーに本人認証を委譲し、アプリはtoken／device code等を収集・解析・保存・log出力しない（起動時の自動loginも同じ、§11.10 SEC-AL-01〜03）。取消／アプリ終了による終了対象は§11.3の所有login processだけとする。
- CLI version／実行file hashは観測可能な場合に診断へ記録するだけで、実行可否の信頼証明には使わない。SDK互換性エラーやCLI不在を隠さず表示する。
- Windowsの保護設定を変更せず、実行拒否を回避しない。公開用evidenceも§13.6のmetadataに限定し、利用者本文やcredentialをcontrol artifact／公開物へ混入させない。
- 定義を明示保存すると、主回答列選択によって見出しセルから取り込まれた設問textもsetting.txtに平文で含まれる。Prompt・評価基準等へ利用者が貼り付けた内容、sheet名、明示出力pathも含まれ得る。読込・主列変更だけで自動保存する意味ではない。
- 回答行本文の自動収集は追加しないが、機密な本文が設定へ絶対に含まれないとは保証しない。setting.txtは暗号化containerではなく、OSの利用者別保存先のaccess controlに従う。§11.6の非保存対象を守り、設定の実内容を公開物・画像・log・共有証跡へ混入させず、自動削除機能を追加しない。

## 15. performance・capacity

- 回答行上限は20,000とする。
- concurrencyは既定8、最大16とする。
- Excel row、column、cell、formula、function argument上限をwrite前に検査する。
- Promptとschemaのapp-owned request上限を測定し、AI送信前に検査する。この検査はmodel上限の既知／不明にかかわらず常に適用する。
- model contextの安全marginは、SDKが当該modelの上限を公開している場合だけAI送信前に検査する。上限不明のmodelでは検査せず、検査していないことを画面に明示する。
- retry込みattempt上限を実行前に検査し、黙って切り詰めない。
- checkpoint保存時間をprogressへ含める。
- AI待機を除く531行workbookのread/write/final validation性能は、対応OSごとに実測値と環境を記録する。未実測platformの数値を保証しない。
- 表示ページの件数を業務上限にせず、全件到達と有限高さのvirtualizationを維持する。UI試験の100行／530行syntheticと20,000件のページ境界計算は分離し、境界計算成功を20,000件実画面の性能実測へ読み替えない。

## 16. failure behavior

| Condition | Result |
|---|---|
| 空の通常回答 | AI callなし、通常評価率0、QuestionEarned 0、similarity 0 |
| 空の固有項目 | AI callなし、当該special score 0 |
| 質問文行とmetadataの不一致 | 現在のQuestionTextを保持し、以前の質問文行の値を反映せず、`HEADER_METADATA_MISMATCH`で再読込を要求 |
| invalid definition / 配点不一致 | AI call前に具体的field error |
| 選択modelが未選択・一覧に不在 | AI call前に停止（`MODEL_SELECTION_REQUIRED`）。別modelへfallbackしない |
| 選択modelの上限がSDK未公開 | run前のmodel相対検査を行わず実行する。app-owned絶対上限とattempt上限は適用する。送信後のmodel上限超過は対象値blank、行ごとの技術status |
| reference generation failure | reference / similarityはblankと技術status。集合の一部失敗は暫定集計であることを明記。減点係数が正ならFinalRaw / FinalScore blank、ゼロなら診断だけの失敗は採点へ伝播しない（FR-114） |
| invalid AI response after retry | 対象値blank、FinalRaw / FinalScore blank、`AI_OUTPUT_INVALID` |
| authentication unavailable | 新規AI callなし、`AUTH_REQUIRED` |
| Copilot CLI not found or incompatible with SDK | 新規AI callなし。CLI不在はOSへのインストール／PATH設定を案内し、SDK非互換は明示的runtime errorで示す。bundled CLIへfallbackしない |
| timeout after retry | 対象値blank、`AI_TIMEOUT` |
| network failure after retry | 対象値blank、`NETWORK_FAILED` |
| rate limit after retry | 対象値blank、`RATE_LIMITED`。有効並列度を縮退 |
| quota exhausted | 再試行せず新規送信停止、`QUOTA_EXHAUSTED`。該当の参照回答・行はcheckpointへ保存せず再開時に再実行 |
| cancel | 新規送信停止。最後の完了行checkpointを保持 |
| process終了 | 最後にatomic保存したcheckpointから再開可能 |
| input changed | checkpoint更新とfinal renameを停止、`INPUT_CHANGED` |
| checkpoint mismatch | checkpointを変更せず再開拒否 |
| app close中のrun | 新規送信を停止し、最大10秒だけ中断処理を待つ。繰返しcloseも待機を飛ばさない。OS shutdownは中断要求のみで終了を妨げない。復旧は最後に成功したatomic checkpointに限定 |
| output validation failure | 完成名を作らずpartialを保持、`OUTPUT_INVALID` |
| partial cleanup failure after success | finalは有効、cleanup warningを表示 |

## 17. Scope

### 17.1 v4.6 required scope

- Windows 11 x64
- RID別.NET 10 self-contained app
- 固定GitHub Copilot SDKとPATHから解決するOS-installed Copilot CLI runtime。Windowsは`copilot.exe`、Unix source foundationは実行可能な`copilot`。互換性確認・CLI不在の案内・identityの診断記録を含む
- standard `.xlsx` from Microsoft Forms or Google Forms
- native input picker
- base / question / special point allocation
- reference answer generation and per-question similarity penalty
- Excel-owned formulas
- per-student-row `.partial.xlsx` checkpoint and process-restart resume
- GUI prefill from input/Prompt text command-line options
- App限定の.NET標準single-file全内容展開によるWindows self-contained unsigned EXEとSHA-256 sidecarを主配布へ追加
- Windows self-contained unsigned ZIPとSHA-256 sidecarを代替として維持
- D-06採用によるOS-installed PATH CLIの明示login開始・取消・既存buttonでの認証再確認
- 起動時の既存GitHubログイン自動確認と、資格情報がない場合の自動login開始（§11.10）
- 実行画面の、Excel学生行と1:1・全件の速報値表示（対象の文字列・Prompt・生の定量値、画面内のみ、§11.12）
- non-public development MSIXのpackage/unpack/integrity `PASS_MECHANISM` evidence
- clean extract／apphost起動と、歴史的package内CLI identity（存在する場合）のpackage-only確認、およびPATH CLI runtimeのWindows evidence
- exact EXEのclean-host CH-01〜06と、EXE／ZIP／development MSIXのclosed 3-row matrix v2による公開判定
- teacher, operator, engineer documentation and actual synthetic screenshots
- 4ステップを維持した同一window内の設定（共通のみ。§11.23）、通常最小サイズの外側スクロール不要、ページ切替と長文／狭小／拡大の到達性例外
- 利用者別setting.txtへの共通設定＋採点定義1件の明示保存、検証後の保存定義明示適用、明示出力先復元とnull時だけの入力隣接result
- 値・対象・ページ・設定内の表示（共通のみ）の往復保持、保存model希望IDと実効選択の分離、現在run／次回draft／前回結果の区別
- 実行・結果画面とジョブ単位ローカルJSON Linesログによる今回ジョブのAI使用量表示（SDK報告の原単位と、表示時に換算したAIクレジット。通貨換算なし）
- 結果画面の見出し行への、今回のジョブの総実行時間（`HH:MM:SS`）とAIクレジットの表示（§11.21）
- 利用できる全modelの選択（§11.13）と、model別の思考レベル・Context Sizeの選択・明示保存・run固定（§11.18）
- 実行準備部品（Copilot login・checkpoint再開準備・技術検証）の入力画面への集約と、step状態の表示名「設定済み」（§11.15〜§11.16）
- 設定の共通設定からのCopilot login操作（§11.17）と、通常評価の設問タブ（§11.22）
- 結果画面での設問文・学生の回答・評価内容の表示（§11.11、§11.19）と、評価項目の説明の初期値（§11.20）
- 教員向けの「設問の詳細」手順（§11.14）と結果Excelの解説文書（§9.4）

### 17.2 out of scope

- macOS、Linux、Windows Arm64、universal macOS artifact support claim
- `.xls`、CSV、PDF、macro、encrypted workbook
- Microsoft Forms API、Google Forms API、LMS API
- cloud database、server backend、multi-user service
- custom VS Code extension、custom URI protocol、background daemon
- 採点値を決めるための repeated stochastic voting／majority／median（FR-115・AC-081の参照集合の中央値・最小値・差は記述統計の表示だけで採点に使わないため、この対象外に含まない）
- plagiarism or misconduct determination
- AI text detector claim
- institutional policy、legal、fairness approval enforcement
- automated credential collection
- automatic AI run from command-line arguments
- production-signed/public MSIX、Developer ID/notarized DMG、Microsoft Store、Mac App Store
- end-user primary pathとしてのPowerShell／shell setup script
- 新しいproduction project／application、独自launcher／自己展開engine、汎用process runner／認証provider、将来用の拡張点
- online bootstrap、auto-update／差分更新、独自cache／locking engine、cache管理UI、runtime cacheの自動cleanup
- 新規installer、既存development MSIXの機能拡張、registry／PATH恒久変更、file association、保護機能の回避
- token入力UI、独自OAuth／WebView／callback server、依頼に伴うSDK／CLI／Avalonia更新、Native AOT化、trimming全面適用
- 設定の自動保存・保存定義の自動適用、複数profile、cloud同期、設定暗号化container、migration／backup／監視／merge／永続操作履歴engine
- 第5workflowステップ、汎用設定provider／filesystem抽象／router／form builder／pagination framework、Undo／Redo、AI設定提案、実AI preview、新規UI framework／NuGet／製品CLI引数／必須環境変数

## 18. Acceptance criteria

AC-004・AC-009〜012は同じIDのまま§7.7へ構造化して移動した。他の既存ACの表形式は維持する。

| ID | 条件 |
|---|---|
| AC-001 | native pickerまたはpathから標準 `.xlsx`を選び、元本を変更せず別 `.xlsx`を作る。 |
| AC-002 | question text rowを1または2から選び、sheet、回答行、質問／通常回答／固有項目列を変更可能な候補として表示する。主回答列を選択すると、同じsheet・question text rowの交差セル値を当該設問textへ即時反映する。 |
| AC-003 | §4.4の現行sampleでD/E/G/Hをprimary候補、E/Hを学生Prompt primary候補、F/Iをsupporting候補、E→F・H→Iを初期supporting候補として提示し、元本identityを維持する。旧12列profileは履歴として分離する。 |
| AC-005 | 設問Pointsの初期値が`(100-base-special)/有効設問数`となり、明示的な均等配分以外で手動値を変更しない。 |
| AC-006 | `base + special + Σ question points = 100`をrun前とformulaで検証する。 |
| AC-007 | 通常Knowledge/Custom評価を動的に構成し、AIはcriterion rawだけを返す。 |
| AC-008 | 各設問へ0件以上の固有評価項目を設定し、同一設問内と対象設問間を等分平均してSpecialEarnedを計算する。 |
| AC-013 | §9.1の実効出力先へ`eval-yyyyMMdd-HHmm[-NN].xlsx`をno-overwrite atomic commitし、元全sheetと4 app-owned sheetsを保持する。明示出力先がnullの場合だけ入力隣接`result`を使う。 |
| AC-014 | run開始時に`.partial.xlsx`を作り、参照生成および各学生行完了後にatomic checkpointする。 |
| AC-015 | input、definition、model（model ID・run-level reasoning effort・Context Size tier）、runtime identityの一致時だけ再開し、参照回答と完了行を再実行しない。 |
| AC-016 | 進捗、処理段階、完了／一部失敗、final／partial pathと件数を実データから表示する。4ステップと設定は最小1024×720 DIP以上の通常画面で外側スクロール不要かつhorizontal overflowなしとし、警告全文・主要操作・状態を実viewport内へ完全包含する。多数項目はページ切替・対象行移動、長文は局所scroll、760×600 standalone／200%表示はreflow後に必要な本文縦scrollで全操作へ到達し、keyboard、44 DIP target、安定した一意Automation ID、virtualizationを維持する。clippingを合格にしない。 |
| AC-017 | 指定警告文を全stepと設定画面で常時全文nonblocking表示する。 |
| AC-018 | `--input`と複数`--prompt`でGUIを事前入力し、利用者操作なしにAI実行しない。 |
| AC-019 | selected same-row dataだけをAIへ送り、本文／Prompt／reason／evidence／credentialをlogへ残さない。 |
| AC-020 | Windows 11 x64 self-contained unsigned ZIPがcleanな展開先で起動し、SHA-256 sidecarと、歴史的packageにCLIが含まれる場合のartifact identityをpackage integrityとして検証できる。runtimeはPATH CLIだけを使用する。 |
| AC-021 | `PASS_PRODUCTION`がないplatform、architecture、installer、signing、notarizationを対応済みとして表示しない。ただし本版のunsigned Windows EXE／ZIPは§13.6〜13.7の`PASS_REQUIRED`を公開判定とし、署名・installer・notarization等のproduction trustを満たしたとは表示しない。 |
| AC-022 | README、教師tutorial、Prompt例、install、privacy、troubleshooting、開発設計、実画面screenshotsを現行UIとcurrent delivery evidenceへ同期する。 |
| AC-023 | Windows x64 development MSIXを作成・unpackし、manifest/version/RID、block map、payload、歴史的package内CLI（存在する場合）、SHA-256 sidecar、test-only identityをpackage integrityとして検証し、一般配布しない。runtimeは同梱CLI／manifestを使わない。 |
| AC-024 | macOS publish/sign/notary source foundationのstatic contractを検証し、native production evidenceなしにartifactまたはsupport claimを公開しない。 |
| AC-025 | macOS scriptがnested sign、shipped CLI hash、outer sign、notary log、app／DMG staple、strict verificationの順序を要求する。実行結果は現版のrequired acceptanceにしない。 |
| AC-026 | Windows public EXEを主導線、ZIPの取得・展開・起動を代替として文書化する。sidecar公開とCIのexact hash検証は必須、利用者の手動比較は任意推奨、sidecarはEXE起動の前提にしない。.NET Runtime／SDK、PowerShell／Node／Git／gh、別Copilot CLI、Officeの導入やsetup scriptをGUI起動に要求せず、development MSIXを一般利用者手順へ含めない。 |
| AC-027 | public EXE／ZIP/packageとdevelopment MSIXにsample、利用者入力、final、partialを含めず、package作成・展開・起動で利用者workbookを変更・削除しない。 |
| AC-028 | release workflowはcandidateのpackage／mechanism required testsを満たしたEXE／ZIPと各sidecarの4 assetだけをdraftへ添付する。exact EXEの必須clean-host結果を照合したprotected publish時にEXE／ZIP／non-public development MSIXのclosed 3行でmatrix v2を確定し、`publish=true`かつrequired statusを満たすartifactだけを公開する。development MSIX本体・secret・未実測claimを漏らさない。 |
| AC-029 | OSのみのfresh Windows 11 x64・標準userで、取得済みEXE1個を開く1起動gestureからofflineで入力画面を表示し、手動展開・追加導入・昇格を要求しない。OS警告・拒否・実操作数は別途記録し、無条件・無警告を保証しない。 |
| AC-030 | 既定App buildでは`CopilotSkipCliDownload=true`とし、単一EXEに.NET/native依存・既存公開docs／画像／LICENSEを含める。歴史的publishは明示供給CLI／manifestを含めてもよいが、runtimeはそれらを選択せずOS PATH CLIを使う。GUI起動はCLI／SDK／sidecarに依存せず、AI runtimeは互換SDKとPATH CLIを必要とし、不在・非互換を明示する。App限定profileとCore／Appの2 production projectを維持する。 |
| AC-031 | EXE／ZIPで任意cwd、日本語・空白path、相対`--input`、複数`--prompt`、既存invalid入力・明示適用の契約を維持する。起動によるAI送信は0件で、EXE配置先や抽出先へcwd基準を変えない。 |
| AC-032 | 初回・再起動・同時起動・cache欠落・抽出中断・容量／権限不足でも利用者input／final／partialを変更・削除しない。cacheとdata／setting.txtを分離し、明示出力先の復元と未指定時だけの入力隣接result、同版EXE／ZIP間の既存checkpoint再開条件を維持する。 |
| AC-033 | 利用者のbutton操作または§11.10の起動時自動loginだけで、絶対PATH entryから解決したOS-installed CLIのloginを直接開始し、shellを介さずcredential／device codeを読み取り・収集・解析しない。Windowsは`copilot.exe`、Unix source foundationは実行可能な`copilot`を使い、空・相対PATH entryを無視する。bundled CLI／manifestへのfallbackをせず、CLI version／hashは診断のみとする。二重開始・評価中開始を防ぎ、取消・失敗後もGUIを継続する。取消／アプリ終了時だけ所有login processを終了し、ブラウザー・他CLI・credentialに触れず、既存buttonで再確認する。CLI不在はOS install／PATH設定を案内し、SDK非互換は明示runtime errorとする。自動AI実行しない。 |
| AC-034 | candidate run／source／version／bytes／hashに拘束されたexact EXEとCH-01〜06のPASSが一致した場合だけ新経路をprotected publishする。metadata限定JSON、candidate-bound MSIX検証記録、4 public assetsの再download照合を要求し、必須試験の欠落・FAIL・NOT_RUN・差替えを拒否する。未実測のOS-only／署名／installer claimを出さない。 |
| AC-035 | 利用者別setting.txt（UTF-8 JSON、schema整数1）へ共通設定＋任意の採点定義1件を明示保存し（表示専用のmodel一覧cacheだけは§11.6の自動保存例外）、ID／decimal／Prompt／canonical hashを復元する。header由来の設問text・貼付内容は明示保存時に平文で含まれ、§11.6の非保存対象は保存しない。破損・未知schema・IO失敗は元fileとdraftを保持しoffline継続する。保存中再編集は未保存のまま、同時保存は最後の成功が優先する。明示出力先は再起動・入力変更後も復元し、初回の未指定または空欄への明示編集によるnullの場合だけ入力隣接resultを算出し、fallback・復元時directory作成をしない。 |
| AC-036 | 保存定義はExcel読込後の明示操作で、保存headerのmetadata・sheet・行・列・定義全体を検証してからInput／Designへ一括適用する。失敗・取消時は現在状態とfileを変更せず、成功時はID・順序・設問text・Prompt・配点を保持する。Imported Prompt一覧は不変、run中の一括適用は禁止し、既存checkpoint admissionを緩めない。 |
| AC-037 | 設定を第5ステップにせず、同じ対象へ1操作で移動し、値・対象ID・ページ・設定内の表示（設定は共通のみ。§11.23）・入力途中の編集を保持して戻れる。希望modelは明示確認後だけ実効選択にし、不在なら未選択・no fallback、確認失敗だけで保存希望を消さない。遷移・設定読込／保存／適用で認証確認／login／runを自動開始せず、login完了時は§11.3の6の認証状態・model一覧の自動再確認だけを行う（起動時の自動確認・自動loginだけは§11.10・AC-040に従う）、実行中の次回draft編集は現在snapshotへ混入しない。全画面で進捗・停止を保持し（実行画面では本文の進捗を保持とみなす。シェル帯の進捗を隠してもよい。FR-111）、設定中完了は結果通知だけとする。前回結果・override・未保存修正版を次回設定から分離する。 |
| AC-038 | 中断後は部分結果のpartial pathと再開準備を表示し、同一セッションでは明示操作で、再起動後はpartial選択またはpath指定で再開条件を開始前に項目別検証する。不一致時はcheckpointを変更せず開始しない。入力・modelだけは明示操作で合わせられ、採点設計・runtimeは自動変更しない。window close時は有限時間の中断待機後に閉じる。 |
| AC-039 | 実行・結果画面とジョブ単位のローカルJSON Linesログで、今回の開始操作に対応するAI使用量（入力・出力・推論・キャッシュtoken、`nano-AI units`、premium request消費量）を、AIを呼ぶ3 operation（参照回答・通常評価・固有評価）と再試行・失敗・取消・未保存行を含めて確認できる。未取得は0や推定値にせず理由とともに示し、明示0・未送信・送信状況不明・部分取得を区別する。項目別の取得元、試行番号・相関ID・終端結果、モデル内訳と総量の不一致を保持し、内訳を総量へ加算・配賦しない。SDK報告の原単位を保ち、通貨表示をしない。AIクレジットはnano-AI unitsから§11.21の規則で換算して示す（2026-10-01変更）。ログは数値・生成ID・閉じたコードだけを記録し、保存失敗でも観測済み表示と採点を壊さない。 |
| AC-040 | 事前条件: fake PATH resolver、認証境界、fake login processを注入した`ExecutionViewModel`（実CLI・実ブラウザー・実資格情報は使わない）。操作: `dotnet test tests/StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~CopilotLoginCommandTests"`。期待結果（exit code 0）: ①window表示（Opened）でPATH CLIによる認証確認が1回だけ実行される（FR-AL-01）。②`Available`ならlogin起動0回・状態文に「自動的にログインしました」（FR-AL-02）。③`AuthRequired`ならPATHで解決した同一CLIのlogin processがちょうど1回起動し、完了後に同CLIで認証を再確認し、2回目の起動時処理は何もしない（FR-AL-03、FR-AL-04）。④CLI不在／SDK非互換／`RuntimeFailed`／`Cancelled`ではlogin起動0回で適切なinstall／PATH案内またはruntime errorを表示する（FR-AL-03、FR-AL-07）。⑤自動login中の「ログインを取り消す」で所有processだけを`Kill(false)`で1回終了し、再自動起動しない（FR-AL-04、FR-AL-05）。⑥run呼出0回・model fallbackなし（FR-AL-06）。⑦環境変数値`0`／`false`（大文字小文字・前後空白を無視）でlogin起動0回、未設定・空・その他値は有効（FR-AL-08）。⑧状態文・`ToString()`にcanary（token、device code、path、例外名）を含まず、CLI version／hashは診断情報だけ（SEC-AL-01〜02）。⑨dispose後・手動確認中・確認失敗時に例外を漏らさずloginを開始しない（FR-AL-07、NFR-AL-01）。証跡: テスト結果（.trx）。実資格情報での確認はAC-040に含めず、§18外の本人確認とする。 |
| AC-041 | 事前条件: 設問文が`Question text Q1`（設問ID `Q1`、表示名`Question Q1`）等の合成runと、改行・連続空白・全角空白を含む設問文、空白のみの設問文を持つ合成run。操作: `dotnet test tests/StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~ResultsQuestionTextTests|FullyQualifiedName~ResultsOutputViewTests"`。期待結果（exit code 0）: ①`RowScores[].QuestionEarnedText`が`<正規化した設問文>: <獲得点>`を設問順に` · `連結した値と完全一致し、設問IDまたは表示名を含まない（FR-RS-01、FR-RS-02）。②改行・タブ・全角空白を含む設問文は単一半角空白へ正規化され前後空白が除かれる。未確定の設問は`—`（FR-RS-02）。③空白のみの設問文は表示名、表示名も空白のみならIDを使う（FR-RS-03）。④実viewの詳細の`ResultsQuestionEarnedFull`が選択行の`QuestionEarnedText`と同じ文字列で、詳細の基準編集領域の`ResultsCriterionQuestionText`が原文（改行保持）である（FR-RS-01、FR-RS-04）。⑤表示しただけで未保存overrideが生じず、snapshotの設問文が原文のまま（正規化されない）である（FR-RS-05）。⑥実viewの一覧（`RowScoreList`）の見出しのTextBlockが`元の行`・`最終点`・`固有点`・`類似減点`・`状態`の5個だけでこの順に並び、各行のGridが列定義5・子5で、一覧の見出し・行のどのTextBlockのTextとtooltipにも`設問別得点`と`QuestionEarnedText`の値（設問文）を含まない。10設問の合成runでも一覧の列は5のままで、詳細の`ResultsQuestionEarnedFull`には全10設問が出る（FR-RS-06、FR-RS-01）。証跡: テスト結果（.trx）。実画面のスクリーンショット確認は§18外とする。 |
| AC-042 | 事前条件: fake行source・fake AI runner・fake checkpoint storeと、fake run boundaryを注入した`ExecutionViewModel`／`ExecutionView`（実CLI・実AI・実資格情報は使わない）。操作: `dotnet test tests/StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~LivePreview"`。期待結果（exit code 0）: ①評価計画確定後、対象行と同数（先頭行〜末尾行、昇順、欠落・重複なし）の全行が「待機中」で並ぶ（FR-LP-02、FR-LP-03）。②1行の評価で、AIへ渡した`RenderedPrompt`と一致するPrompt、主回答・補助列のセル値、criterion別RawScore・理由・根拠、固有Score、類似度がFR-LP-04どおり詳細に出る。Prompt組立後・応答前は「AI評価中」で対象の文字列とPromptが出て、応答後に値が入る（FR-LP-04、FR-LP-05）。③空回答は`空回答: AIへ送信せず0点相当`、固有配点0は`未実行`、AI失敗は`失敗（AI_TIMEOUT）: 値は空欄`で、0や成功値を出さない（FR-LP-05）。④一覧の要約が`通常 8,6 / 固有 0.8 / 類似 0.42`形式になり、項目数・状態が更新される（FR-LP-03）。⑤checkpoint再開では再開済み行が「再開前に完了」でcheckpointの値を表示し、対象の文字列・Promptは保存されていない旨を示す（FR-LP-06）。⑥取消・失敗で終わると評価途中の行が「中断」、未着手が「未処理」になり、次のrun開始・入力変更で破棄される（FR-LP-07）。⑦2,000文字超の値は`先頭 2,000 文字`注記つきで切り詰められ、保持合計が32,000,000文字を超えると以後は200文字になる（FR-LP-08）。⑧見出しが`速報値（Excel 行ごと・{N} 行）`で、20,000行でも全件が一覧に入り、Controlは仮想化される（FR-LP-01、FR-LP-02、FR-LP-09、NFR-LP-01）。⑨canary文字列が`ToString()`・logに現れず、追加のAI呼出しが0件、checkpointに速報値用の追加フィールドがない（SEC-LP-01、SEC-LP-02）。⑩1024×720で「実測」表示・予約名・主要操作がviewport内に収まり、速報値の行一覧・詳細が日本語Accessible Nameを持つ（NFR-LP-01）。証跡: テスト結果（.trx）、`docs/images`の実行画面screenshot。 |
| AC-043 | 事前条件: 実CLI・実資格情報を使わない。SDKの`ModelInfo`をfakeとして与える。操作: `dotnet test tests/StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~CopilotModelEnumerationTests|FullyQualifiedName~ModelCatalogTests"`。期待結果（exit code 0）: ①`auto`、policy未設定、`unconfigured`、大文字の`ENABLED`を含む全modelがSDK順に選択肢となり、上限・reasoning effortが保持される（FR-MS-01、FR-MS-04）。②1000件でも切り捨てない（FR-MS-01）。③`disabled`（大文字小文字不問）は除外され他modelは残る（FR-MS-02、FR-MS-03）。④null・空・空白のみ・前後空白・制御文字・257文字のIDは除外され、例外なく他modelが残る。256文字は残る（FR-MS-02、FR-MS-03）。⑤null・空の列挙は空一覧になる（FR-MS-03）。⑥cache 4096件は保存・復元でき、4097件の保存は`InvalidSettings`で既存fileを変更しない（FR-MS-05）。⑦4097件の一覧は全件を選択でき、最後のmodelを選択でき、cacheと設定fileは作られない（FR-MS-01、FR-MS-05）。証跡: テスト結果（.trx）。実accountでの列挙件数の確認は§18外の本人確認とする。 |
| AC-044 | 【廃止（依頼 2026-11-02・統合→AC-059）】事前条件: リポジトリルート。操作: `dotnet test tests/StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~DocumentationContractTests"`。期待結果（exit code 0）: ①`docs/getting-started.md`で、`## 2. 採点設計`と`## 3. 実行`の間に、FR-DOC-01の見出しの小節が1つある（FR-DOC-01）。②その小節が`../images/02-input-mapping.png`を1回だけ参照し、参照先が存在する。`images/`のPNGは8枚のまま（FR-DOC-02）。③小節にFR-DOC-03の14個のラベルがすべて含まれる（FR-DOC-03）。④小節に`基本の使い方`と`注意してください`の見出し文言、FR-DOC-04の4つの注意点がある（FR-DOC-04）。証跡: テスト結果（.trx）。実画面との一致はAC-022の画像生成手順に従い、本ACは文書の構造だけを判定する。 |
| AC-045 | 対応要求: FR-XD-01〜06。事前条件: リポジトリのsource（実CLI・実AI・実資格情報は使わない）。操作: `dotnet test tests/StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~ResultExcelDescriptionTests"`、`--filter "FullyQualifiedName~DocumentationContractTests"`、`--filter "FullyQualifiedName~Packaging"`の3回（いずれも同じprojectで、`--filter`の値だけを替える）。期待結果（exit code 0）: ①`docs/result-excel-description.md`が存在し、`README.md`（本文と「ガイド」）と`docs/README.md`からリンクされ、全local linkと見出しanchorが解決する（FR-XD-04）。②文書に警告文の固定文言、公開`v0.8.6`、旧公開`0.8.1`との区別、clean-host試験CH-01〜06と本人loginの未実施、`Quantification_`で始まる4つのsheet名が含まれ（結果一覧`採点結果`の言及は許す）、かつ文書に`採点結果`の説明があり、列の見出し（`ID`・`質問Nの評価`・`質問NのPromptの評価`・`合計`）が結果workbookの実際の見出しと一致し、未リリース扱いの表記、`dev/docs`、実装の型名を含まない（FR-XD-03、FR-XD-05）。③`ResultsSheetWriter`のsuffix定数・列見出し定数の全値と、`ResultsStatusCodes`の12個の全値、`PRIMARY_ANSWER`／`SUPPORTING_COLUMN`／`NONE`が文書にある（FR-XD-01、FR-XD-02）。④実際に書いたConfig sheet（見出し33列の列記号と名前、全`RecordType`）、References sheet（8列）、Run sheet（`Field`26項目）の値がすべて文書の表にある（FR-XD-01、FR-MO-07）。⑤文書が単一EXE・ZIP・MSIXの公開文書allowlist（scripts・pubxml・試験の期待一覧）に含まれ、抽出後の全local linkが解決する（FR-XD-05）。証跡: テスト結果（.trx）。文章の平易さ（FR-XD-02）は自動判定できないため、レビュー者が教員の視点で通読し、専門用語が初出で説明されていることを確認する（許容差なし）。 |
| AC-046 | 対応要求: FR-PREP-01〜07、FR-STEP-01〜03。事前条件: リポジトリのsource（実CLI・実AI・実資格情報は使わない）、Avalonia headless。操作: `dotnet test tests/StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~PreparationOnInputTests|FullyQualifiedName~MainWindowTests|FullyQualifiedName~ExecutionViewTests"`。期待結果（exit code 0）: ①メインwindowの「1 入力」に§11.15の表の全Automation IDの部品が存在し、「3 実行」には0件である（FR-PREP-01、FR-PREP-02）。②入力画面の切替button（初期は閉）で実行準備の区画が開閉し、開閉で入力値が保持される（FR-PREP-01）。入力画面の「Copilot 状態を確認」「GitHubにログイン」「ログインを取り消す」「checkpoint から再開」がメインwindowの`ExecutionViewModel`のcommand・状態へ結び付き、操作結果が「3 実行」の判断に反映され、画面往復後も保持される（FR-PREP-03、FR-PREP-05）。③入力画面を単体表示すると実行準備部品は表示されず、既存の入力部品の配置・寸法試験が変わらず成功する（FR-PREP-07、FR-PREP-04）。④「1 入力」の初期focusがファイルpath入力、実行画面が実行画面内の部品である（FR-PREP-06）。⑤step状態文が、現在以外の過去表示済みstepで「設定済み」、現在は「現在・選択中」、未表示は「未着手」で、Accessible Nameにも同じ文言が入り、「訪問済み」がどのstepにも出ない（FR-STEP-01〜03）。証跡: テスト結果（.trx）。native Windowsの実画面確認は§18外とする。 |
| AC-047 | 対応要求: FR-SETLOGIN-01〜03。事前条件: Avalonia headless、fake認証境界。操作: `dotnet test tests/StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~Common_settings_hosts_copilot_login"`。期待結果（exit code 0）: 共通設定タブに`SettingsCopilotLoginPanel`と3つのボタン・2つの状態文が存在し、各ボタンのCommandが`ExecutionViewModel`の対応コマンドと同一で、状態文が`ExecutionViewModel`の文字列と一致し、設定画面を開いただけでは認証確認・loginが開始されない。証跡: テスト結果（.trx）。 |
| AC-048 | 対応要求: FR-QT-01〜05。事前条件: リポジトリのsource（実CLI・実AI・実資格情報は使わない）、Avalonia headless、設問が4件以上の採点定義。操作: `dotnet test tests/StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~EvaluatorSettingsViewTests|FullyQualifiedName~CompactWorkflowLayoutTests|FullyQualifiedName~MainWindowSettingsTests"`。期待結果（exit code 0）: ①Automation ID `EvaluatorSettingsQuestions`は`ComboBox`ではなく設問タブ部品で、各設問に1タブが設問の順に並び、タブ文字が設問の表示名である（FR-QT-01、FR-QT-02）。②タブを選ぶと`SelectedQuestion`が変わり、評価方法・評価項目・編集欄がその設問の内容へ切り替わる。`SelectedQuestion`を外部から変更するとタブの選択が追従する（FR-QT-03）。③null選択や入力の同期・並替え後も、ownerの選択は保持され、タブのitemsは`Questions`と同一である（FR-QT-03）。④設問0件では設問なしの案内が表示される（FR-QT-04）。⑤最小window（1024×720 DIP）と200%表示で、タブ領域が横へはみ出さず、各タブの高さが44 DIP以上である（FR-QT-05）。⑥タブ部品にkeyboard（Tab）で到達でき、「2 採点設計」の統合画面（FR-001）を開いた直後の初期focusが基礎配点入力`BasePointsTextBox`である（FR-QT-05）。証跡: テスト結果（.trx）。native Windowsの実画面確認は§18外とする。 |
| AC-049 | 対応要求: FR-MO-01〜05、NFR-MO-01。事前条件: fake認証・Avalonia headless、実通信なし。リポジトリルートで`dotnet test tests\StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~ModelOptionsTests|FullyQualifiedName~CopilotModelEnumerationTests|FullyQualifiedName~ModelCatalogTests|FullyQualifiedName~SettingsViewTests|FullyQualifiedName~SettingsAccessibilityTests" --logger trx`を実行。期待: exit 0、SDK順の全model（4097件目も選択可）、重複・disabled・不正ID境界、空一覧、認証失敗からの復旧、思考の順序・defaultラベル・未知値・非対応・auto、272K/1Mと未知容量・int境界、model切替と再取得での無効選択block、設定の旧形式・保存復元・不正値・保存失敗・自動cache保存の非混入・読込競合を検証。選択欄を実画面へbindし、44 DIP・Accessible Name・keyboard到達・局所scrollを検証。証跡: TRXとassertした選択値・型・順序・不変file bytes。 |
| AC-050 | 対応要求: FR-MO-06〜07。事前条件: fake transportと合成workbook、実通信なし。`dotnet test tests\StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~ModelOptionsTests|FullyQualifiedName~EphemeralEvaluationRunnerTests|FullyQualifiedName~AuxiliaryEvaluationRunnerTests|FullyQualifiedName~Resume|FullyQualifiedName~Checkpoint|FullyQualifiedName~ResultExcelDescriptionTests|FullyQualifiedName~ExecutionSettingsTests|FullyQualifiedName~ConfigAndRunSheetWriterTests|FullyQualifiedName~ResultsOutputViewTests" --logger trx`。期待: exit 0、全3種sessionとretryが選択effort/tierを保持、既定tierはnull、拡張prompt budgetへ容量検査が追従、実行中変更は次回だけ、checkpoint round-tripと追記のtier一致・不正tier拒否、旧schema2は既定tier、tier不一致は送信0で`CHECKPOINT_MODEL_MISMATCH`、復元操作でeffort/tierを復旧、Runの26項目と`ContextTier`出力・説明の一致、override出力へのtier継承、入力bytes不変。証跡: TRX、fake sessionのconfig、checkpointと出力assert。 |
| AC-051 | 対応要求: FR-RV-01〜06、SEC-RV-01、NFR-RV-01、FR-RS-04。事前条件: リポジトリのsource、Avalonia headless、合成definition（主回答列`A`・補助列`B`）と合成run、fakeの入力identity境界・行source、および一時directoryに作った合成`.xlsx`（実CLI・実AI・実資格情報・`sample/`は使わない）。操作: リポジトリルートで`dotnet test tests/StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~ResultsAnswerReviewTests|FullyQualifiedName~ResultsQuestionTextTests|FullyQualifiedName~ResultsOutputViewTests|FullyQualifiedName~ResponsiveLayoutTests|FullyQualifiedName~PrimaryJourneyAccessibilityTests"`。期待結果（exit code 0）: ①詳細を開くと選択行の基準の`StudentAnswerText`がFR-RV-02の形式（`[列 A · 主回答]\n{値}\n\n[列 B · 補助]\n{値}`、空セルは`（空欄）`、改行保持）と完全一致し、実viewの`ResultsCriterionStudentAnswer`に同じ文字列が出る（FR-RV-01、FR-RV-02）。②一覧表示中は読込0回、詳細表示で選択行の1行だけを読み、同じ行の再表示で読み直さない。入力identityの確認が読込の前後2回行われる（FR-RV-03）。③identity不一致・読込例外で、FR-RV-04の各文言になり、値を出さない。失敗後の再選択で再読込する（FR-RV-04）。④理由・引用・根拠の場所・説明が、成功・引用なし・`NONE`・`SUPPORTING_COLUMN`・空回答・取消・技術失敗の各場合にFR-RV-05の文字列と完全一致し、`ResultsCriterionReason`／`ResultsCriterionEvidence`／`ResultsCriterionEvidenceSource`／`ResultsCriterionDescription`に表示される（FR-RV-05）。⑤表示によって未保存overrideが生じず、出力境界の呼出し0回・`Results`の採点値不変、合成`.xlsx`のバイト列と更新日時が不変で、実ファイルから読んだ回答が表示される（FR-RV-06、FR-RV-03）。⑥読込中に別の行を選んだ・別の実行結果を読み込んだ場合、古い結果が反映されない（NFR-RV-01）。⑦回答・理由・根拠のcanary文字列とファイルパスが、`ToString()`、状態文言、Automation の Name に現れない（SEC-RV-01）。⑧1024×720 DIPの詳細で、override入力欄がviewport内、回答欄・評価欄が高さ44 DIP以上で横にはみ出さず、Automation IDが一意である（NFR-RV-01、FR-RV-01）。⑨設問文は回答欄の先頭に原文で出る（FR-RS-04）。既存のResults系UI試験が引き続き成功する。証跡: テスト結果（.trx）。native Windowsの実画面確認は§18外とする。 |
| AC-052 | 対応要求: FR-CD-01〜03。事前条件: リポジトリのsource（実CLI・実AI・実資格情報は使わない）、合成workbook。操作: リポジトリルートで`dotnet test tests/StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~DefaultCriterionDescriptionTests"`。期待結果（exit code 0）: ①`DefaultCriterionDescriptions.For`が§11.20の3本文（Knowledge・Prompt・Custom）と完全一致し、未対応の種類は`ArgumentOutOfRangeException`（FR-CD-01）。②新規の採点定義の最初の評価項目と、Knowledgeの評価項目の「追加」・評価方法の追加で作る評価項目がKnowledge本文、Prompt種類・Custom種類の評価方法への追加・その評価方法の最初の評価項目がそれぞれPrompt本文・Custom本文である（FR-CD-01）。③評価方法の種類のドロップダウンの選択肢が`Knowledge`・`Prompt`・`Custom`の3つで、保存・読込の種類名`KNOWLEDGE_COVERAGE`／`PROMPT_ANALYSIS`／`CUSTOM_PROMPT`が往復でき、既存の`CUSTOM_PROMPT`定義がCustomとして読める（FR-CD-01）。④評価方法の種類を変えても既存の説明は不変で、その後の追加だけ変更後の種類の本文になり、利用者が編集した説明は評価項目の追加で上書きされない（FR-CD-02）。⑤入力画面の提案設問は、学生のPrompt列が種類Promptの本文、それ以外がKnowledge本文で、検証エラーが0件である（FR-CD-01、FR-CD-03）。証跡: テスト結果（.trx）。native Windowsの実画面確認は§18外とする。 |
| AC-053 | 対応要求: FR-RT-01〜02、FR-CR-01〜04、NFR-CR-01。事前条件: リポジトリのsource、Avalonia headless、合成run結果と、`JobUsageTracker`へfakeの観測値を与えて作ったコスト記録（実CLI・実AI・実資格情報・実課金API・`sample/`は使わない）。操作: リポジトリルートで`dotnet test tests/StudyReportEvaluator.App.Tests --filter "FullyQualifiedName~ResultsRunMetricsTests|FullyQualifiedName~JobUsageTrackerTests|FullyQualifiedName~JobCostBackendTests|FullyQualifiedName~ResultsOutputViewTests|FullyQualifiedName~ResponsiveLayoutTests|FullyQualifiedName~MainWindowSettingsTests"`。期待結果（exit code 0）: ①時間の書式が0秒→`00:00:00`、129.8秒→`00:02:09`、3599.999秒→`00:59:59`、3600秒→`01:00:00`、360000秒→`100:00:00`、記録なし・終了時刻なし・負の差→`—（未計測）`と完全一致（FR-RT-02）。②AIクレジットの書式が0→`0.0000`、49999→`<0.0001`、50000→`0.0001`、12345678901→`12.3457`、1234567890000000→`1,234,567.8900`と完全一致し、型はstring（FR-CR-01）。③`ResultsRunMetrics`の本文がFR-CR-02の5分岐（記録なし・AI送信なし・未取得・一部取得・観測完了）で`総実行時間 {時間} · AIクレジット {クレジット}`と完全一致し、未読込で空・非表示、ToolTipとAccessible NameがFR-CR-04の注記を含み、別の実行結果の読込で更新される（FR-RT-01、FR-CR-02、FR-CR-04）。④コスト要約2行目・コスト詳細・ジョブログ表示のAIクレジットがFR-CR-03の文字列と一致し、旧文言`課金単位未確認`を含まない。通貨記号・`円`・`USD`・`ドル`を含まない（FR-CR-03、FR-CR-04）。⑤JSONLの各行がAIクレジットの項目を持たず、nano-AI unitsの記録値が変わらない（NFR-CR-01）。⑥1024×720・1180×800 DIPのメインwindow（`MainWindowSettingsTests`）と結果の内容領域950 DIP幅で最長例`総実行時間 100:00:00 · AIクレジット 1,234,567.8900（一部取得）`を含め、`ResultsRunMetrics`が省略記号なしに全文見える（FR-RT-01）。既存のResults・コスト系UI試験が引き続き成功する。証跡: テスト結果（.trx）。実account・実請求額との照合は§18外とする。 |

## 19. Test requirements

以下の番号は追跡ID `TR-01`〜`TR-50`に対応する。既存番号を維持し、中断・再開導線の37、ジョブコスト表示の38、起動時の自動Copilotログインの39、結果画面の設問文表示の40、実行中の速報値表示の41、利用できる全modelの選択の42、結果Excelの解説文書の43、実行準備部品の集約の44、通常評価の設問タブの45、結果画面での学生の回答と評価内容の表示の46、評価項目の説明の初期値の47、結果画面の総実行時間とAIクレジットの48、共通設定のCopilot login操作の49、思考レベル・Context Sizeの選択の50を末尾へ追加する。T01時点の未実装・試験NOT_RUNは履歴であり、現在の実装・局所検証は[traceability](../dev/docs/traceability.md)のVERIFIED_SCOPEDに限定する。過去の試験結果と現在の局所検証は同追跡表で区別し、異なる対象集合を合算して全体合格にしない。

**0.8.4での記録済み結果:** T35の対象文書試験は4/4成功・敵対的レビュー済み（`artifacts/test/ui-settings/t35/t35-reviewed.trx`、指摘0）。T36の文書・画像contract全体は別scopeで、独自の`artifacts/test/ui-settings/t36/t36-current.trx`が21/21成功・REVIEWED。T37は`artifacts/test/ui-settings/t37/t37.trx`の9/9（実ZIP＋MSIX静的契約）、T38は`artifacts/test/ui-settings/t38/t38.trx`の114/114とP06実EXE 7/7・P07 `PASS_DEVELOPMENT`。T39の自動回帰は`artifacts/test/ui-settings/t39/reviewed/`の2026-09-07の2 TRXでCore 190＋App 1702＝1892/1892、skip 0。初回1失敗→fixture修正→126/126・レビュー指摘0→全体再実行成功の履歴を保持する。MSIX実物は`artifacts/package/mechanism/StudyReportEvaluator-win-x64.unsigned.test.evidence.json`の`PASS_MECHANISM`（256 entries、0.8.4.0）で、install／公開の成功ではない。

これらをF02後の`0.8.6`や全受入のPASSへ流用しない。追加nativeは3試行で停止し、最新`artifacts/test/ui-settings/t39/native-final-attempt.json`は`CONTROL_ID_PREDICATE_NOT_UNIQUE`でFAIL。120 DPI・1475×1000 pixel＝1180×800 DIP・合成入力読込・入力／EXE不変・実利用者設定非作成は部分観測であり、4画面・5カテゴリ・1024×720・実keyboardの成功ではない。Narrator／本人walkthrough4項目／隔離利用者でのnative保存とCH-01〜06は`NOT_RUN_EXTERNAL_PREREQUISITE`。以下は受入に必要な試験契約であり、要求承認・局所検証・過去evidenceを未実施範囲の成功へ拡張しない。

1. Microsoft Forms型、Google Forms型、question row 1/2の匿名化synthetic workbook test。各rowで主回答列変更後の設問textが同列の交差セル値へ一致すること、および質問文行変更後・metadata再読込前は旧行の値を反映せず`HEADER_METADATA_MISMATCH`で再読込を要求することを含む。
2. sampleのcanonical path、標準形式、実ZIP／classification／metadataの件数整合性とrelationship安全境界、sheet、dimension、行列数、§4.4の現行D〜I role suggestion、実行前後のinput identity不変test。§4.4の履歴固定hash／bytes・11 entries／8 relationships一致はgateにしない。
3. native picker cancel/select、path direct input、unsupported format test。
4. base/special/question pointsの初期配分、最後の設問への端数、手動値保持、均等配分test。
5. 配点合計、range、similarity weight、enabled special minimumのvalidation test。
6. Knowledge/Prompt/Custom evaluatorとspecial Prompt placeholder／closed schema test。
7. FR-115の有限参照集合／旧単独集合、run modelとrun-level reasoning effortの共用、failure、resume reuse test。
8. normal/special/similarityの0/1境界、range外、empty-zero、failure-blank test。
9. QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、clampの独立手計算oracle。
10. Config参照formula、blank伝播、cached preview、formula allowlist/ref/DAG/length test。
11. References/Results/Run/Config sheetと元sheet保持、name collision test。
12. result path分単位命名、`-02` suffix、directory作成、no-overwrite、atomic fault test。
13. partial作成、reference checkpoint、学生行checkpoint、atomic replace、強制終了相当fault test。
14. checkpoint schema/input/definition/model（model ID・reasoning effort・Context Size tier）/runtime mismatchと完了行skip test。
15. auth、timeout、network（CLIが返す通信失敗のsession errorを含む）、rate limit（backoff・並列度縮退）、quota枯渇（非再試行・非保存）、schema（evaluator ID省略の受理と不一致の拒否を含む）、cleanup、cancel、no-send-after-cancel、reasoning effort指定条件とジョブログへの記録test。
16. selected-column／same-row isolation、literal string、no-content log test。
17. 4-step＋設定、warning exact text/nonblock、progress、resume、completion、keyboard、200% scale test。1024×720／1180×800の通常shellは実ClientSize、Extent／Viewport、主要Control完全包含を実測して外側scroll不要とhorizontal overflowなしを確認する。多数項目のページ切替・元行移動・空一覧・最終ページ・選択保持・リサイズとvirtualization、長文／path／dropdownの局所scroll、760×600 standalone／200%表示のreflow後の本文縦scroll例外を分離し、全操作への到達を検証する。44 DIP、focus復帰、一意Automation ID、native DPI／Narratorをheadlessと分けて記録する。主回答列ComboBox操作後の可視設問text同期も維持する。
18. CLI option parser、UTF-8 Prompt file、複数Prompt、明示適用、no-auto-run test。
19. Windows x64 legacy publish/package layout（同梱CLI／manifestがある場合のpackage integrityのみ）、clean GUI launch、およびPATH CLI runtime test。
20. unsigned ZIP、SHA-256 sidecar、safe layout、再現可能な再package regression test。
21. README、利用者文書、package契約が未実測platform、architecture、installer、signing、notarizationを対応済みと主張しないtest。
22. documentation link、screenshot provenance、unsupported claim、Prompt examples contract test。
23. fixed-seed 531-row synthetic end-to-end、resume end-to-end、input hash不変test。
24. optional authenticated synthetic Copilot smokeとexternal spreadsheet recalculation smoke。required deterministic testの代替にしない。
25. Windows development MSIXのmanifest/version/layout、unpack、block map、歴史的同梱CLI（存在する場合）のpackage integrity、sidecar、unsigned OID、policy negative、cleanup test。runtimeではPATH CLIだけを使う。
26. macOS RID別publish／bundle／sign／notary script、Info.plist、entitlement、secret boundaryのstatic contract test。
27. macOS scriptがhardened runtime、nested/outer signature、secure timestamp、notary log、staple、DMG integrity、quarantine launchをproduction evidenceとして要求することのcontract test。
28. Windows ZIPのclean extract/apphost/bundled CLI/input不変と、development MSIX packageに利用者workbookを含めないことのE2E／package test。
29. Windows ZIPをrequired publish row、development MSIXをnon-public mechanism rowとして扱うmatrix、secret isolation、public candidate re-download、未実測artifact非公開のrelease workflow test。
30. App限定single-file profile／locked restore／publishと最終EXEのpackage test。既定`CopilotSkipCliDownload=true`、歴史的publishで明示供給するCLI file／manifestのpackage-only検証、restore／publish条件一致、通常ZIP／Core／macOSへの非適用、AMD64／版、実行必須file1個、symbols除外、native integrity、docs／画像／LICENSE allowlist、禁止物非混入、sidecarのfinal bytes一致とsidecarなしGUI起動を検証する。runtimeがPATH CLIを選ぶことと同梱CLIを選ばないことも確認する（AC-029／030）。
31. 実EXEの初回／再起動／同時起動／cache欠落復元／抽出中断／容量・権限不足、移動・read-only配置、任意cwd・日本語／空白・相対path・複数Prompt・invalid引数・明示適用・no-auto-run test。synthetic input／既存final／partialのhash・size・時刻不変、default result位置、同版EXE／ZIPのcheckpoint互換を検証する。実施できないfaultをPASSにしない（AC-031／032）。
32. login専用service／Execution UIのdeterministic test。絶対PATH entryから解決したOS CLI／固定login引数だけの直接起動、空・相対PATHの無視、shell非使用、token／device code非読取・非収集・非log、bundled CLI／manifest非選択、SDK非互換・CLI不在の明示エラー、本人操作まで未開始、二重開始／評価中開始防止、失敗・取消後のGUI継続と再試行、取消／app close時の所有process限定終了、ブラウザー／他CLI／credential非干渉、既存buttonでの再確認、no-auto-run、login前後の観測CLI identityの診断記録、keyboard／Automation ID／200% scaleを検証する。本人loginの実測はCH-06として分離する（AC-033）。
33. exact candidate EXEのfresh OS試験CH-01〜06と公開境界test。matrix v2のclosed 3行／public 4 assets、candidate workflow／run／source／version／hash／sidecar拘束、metadata限定JSONの型・長さ・許可field、MSIXのcandidate検証記録のみ受渡し・本体非upload、再download照合後の最終matrix確定を検証する。unknown／duplicate／missing row、必須試験欠落・FAIL・NOT_RUN、CH-06の任意化、別EXE証跡、機微field、MSIX公開を拒否し、ADV-01／02のNOT_RUNは許容する。開発host／fake成功をclean-host／本人認証の代用にしない（AC-028／029／034）。
34. 一時absolute pathに限定した設定storeの明示保存・再読込test。fileなし、BOM、型／enum／範囲、schema欠損／不正型／未知版／未知項目、定義なし／不正draft、入力未読込時の保存定義保持、ID／decimal／Unicode／Prompt／canonical hash往復、保存中再編集、atomic置換と旧bytes保持・temp後始末を確認する。Windowsの排他file・file-valued親path等で実際のIO拒否を確認し、ReadOnlyディレクトリだけを拒否根拠にしない。合成header／貼付内容の平文保存、回答自動収集・credential／AI結果非保存・no-content logを検証する。未指定で入力A→Bのresult、明示先の再起動→入力Bでの復元、空欄→null、利用不可no fallback、復元時directory非作成を確認し、実利用者設定を使わない（AC-035）。
35. 保存定義の明示適用test。Excel未読込／run中の禁止、同入力／別header／sheet・列・行不一致、取消・失敗時のInput metadata／draft／Design／保存file無変更、成功時のID・順序・設問text・Prompt・配点・canonical hash保持を確認する。後続主列変更の既存同期、Imported Prompt一覧・本文・順序不変、AI送信0、既存checkpoint admission維持を検証する（AC-036）。
36. Input→Design→Settings→Input→Executionの往復と交互編集→保存、入力途中、選択ID／ページ／設定内の表示の保持、同じ入力・定義での再開指定非初期化と変更時の再確認、保存希望modelの有／無／欠落・確認失敗、no fallback／no-auto-login/runをfake境界で検証する。実行中の次回編集で現在request／snapshot不変、全画面の進捗・停止、設定中完了の非強制遷移、前回結果とoverrideの保持を確認する。save→新VM／store→read-only Excel明示適用→fake run→別名出力・resumeのdeterministic E2Eで元本、exact100、zero／blank、formula、checkpoint契約を維持する。本人walkthroughは別途実施し未確認を合格にしない（AC-035〜037、AC-016〜019）。

37. 中断後の再開準備、partial picker取消時の状態不変、開始前の項目別再開検証、入力・modelの明示適用、window close時の有限中断待機をdeterministicに確認する。

38. ジョブ使用量の集計・表示・JSONLログのdeterministic test。取得成功／一部欠落／全欠落、明示0と未取得、最後の呼び出しのみ、イベント重複・順序逆転・final複数通知、再試行と3 operation（類似度はAIを呼ばない）、取消・cleanup失敗、項目別の取得元、モデル内訳と総量の不一致、下方訂正、overflow／負数を確認する。JSONLは一時directoryだけを使い、回答・Prompt・path・credentialのcanary非記録、容量上限・保存失敗時の観測値保持、終端記録の整合を検証する。実AI・実課金照合・通貨換算は含めない（AC-039）。AIクレジットの換算・書式はTR-48で検証する。

39. 起動時の自動Copilotログインのdeterministic test。既存資格情報あり（login process起動0）、資格情報なし（自動login1回・完了後の再確認・以後の再実行なし）、CLI不在／SDK非互換／`RuntimeFailed`／`Cancelled`でのlogin非開始、利用者取消後の非再試行、dispose後・手動確認中の非実行、確認失敗の封じ込め、環境変数の解釈（未設定・空・`0`・`false`・大文字小文字・空白）、window表示（Opened）で1回だけ開始されること、AI送信0件、状態文・ToStringへのcanary非混入、version／hashがdiagnostic-onlyであることをfake境界で検証する。実CLI・実ブラウザー・実資格情報は使わない（AC-040）。

40. 結果画面の設問文表示のdeterministic test。詳細の設問別得点が正規化した設問文を使い、ID・表示名を含まないこと、空白正規化、空白のみの設問文の代替、未確定`—`、複数設問の順序と同一設問文の非統合、詳細の原文全文表示、表示によるsnapshot設問文・未保存overrideの不変、一覧が5列（元の行・最終点・固有点・類似減点・状態）だけで設問別得点の見出し・値・tooltipを含まないこと（設問数10でも同じ）を検証する（AC-041）。

41. 実行中の速報値表示のdeterministic test。scheduler（項目単位の更新順序、Prompt一致、空回答・固有配点0・AI失敗・取消、追加AI呼出し0）、orchestrator（全行の待機中一覧、再開前に完了した行、取消後の中断・未処理、checkpointへの追加保存なし）、ViewModel（要約形式、選択行と評価中の行の既定表示の詳細、2,000／200文字上限と注記、20,000行の全件保持、run間の破棄、canary非漏洩）、View（Automation ID、局所scroll、1024×720の収まり、仮想化）をfake境界で検証する。実AI・実課金・実資格情報は使わない（AC-042）。

42. 利用できる全modelの選択のdeterministic test。SDK `ModelInfo`のfakeから、全model（`auto`・policy未設定・`unconfigured`）のSDK順での列挙、1000件の非切捨て、`disabled`除外、不正ID（null・空・空白・前後空白・制御文字・257文字）の除外と他modelの継続、256文字ID保持、null／空列挙、cache件数境界（4096可・4097不可）、4097件一覧の全件選択とcache非保存を検証する。実CLI・実資格情報は使わない（AC-043）。

43. 結果Excelの解説文書のdeterministic test。文書の存在、README・利用者index・各ガイドからのリンク、警告文・版注記・sheet名、`ResultsSheetWriter`の列定数と`ResultsStatusCodes`の全値、実際に書いたConfig・References・Run sheetの見出し・列記号・`Field`名・`RecordType`の全値が文書にあること、公開文書allowlist（scripts・pubxml・package試験）への収録を検証する。実CLI・実AI・実資格情報は使わない（AC-045、FR-XD-01〜06）。
44. 実行準備部品の入力画面集約とstep状態文「設定済み」のheadless UI試験。メインwindowで入力・実行画面のAutomation ID所在、`ExecutionViewModel`への結び付き、単体表示時の非表示、focus、状態文とAccessible Nameを検証する。実CLI・実AI・実資格情報は使わない（AC-046、FR-PREP-01〜07、FR-STEP-01〜03）。
45. 通常評価の設問タブのheadless UI試験。設問名の表示、選択の双方向反映、null選択・入力同期での選択保持、最小window内の収まり、keyboard到達を検証する（AC-048、FR-QT-01〜05）。
46. 結果画面での学生の回答と評価内容の表示のdeterministic test。fakeの入力identity境界・行sourceで回答の書式・空欄・改行、詳細表示時だけの1行読込と行単位の記憶、読込前後のidentity確認、不一致・例外の文言と再試行、古い読込の破棄、理由・引用・根拠の場所・説明の全分岐、表示による採点値・override・出力の不変、canary非漏洩を検証し、一時directoryの合成`.xlsx`で実ファイル読込とバイト列・更新日時の不変を確認する。実CLI・実AI・実資格情報は使わない（AC-051、FR-RV-01〜06、SEC-RV-01、NFR-RV-01）。

47. 評価項目の説明の初期値のdeterministic test。評価方法の種類別の本文の完全一致、新規定義・評価項目の追加・評価方法の追加・入力画面の提案設問での初期値、種類変更と利用者編集で既存の説明が変わらないこと、検証エラー0件を検証する。実CLI・実AI・実資格情報は使わない（AC-052、FR-CD-01〜03）。

48. 結果画面の総実行時間とAIクレジットのdeterministic test。時間の書式（0秒・秒未満切捨て・時の繰上り・100時間以上・未計測の3条件）、AIクレジットの換算・丸め・3桁区切り・`<0.0001`・0、`ResultsRunMetrics`の5分岐とToolTip・Accessible Name・未読込時の非表示・再読込での更新、コスト要約・詳細・ジョブログ表示の文言、JSONL schemaの不変、1024×720・1180×800での全文表示をfakeの観測値で検証する。実CLI・実AI・実資格情報・実課金APIは使わない（AC-053、FR-RT-01〜02、FR-CR-01〜04、NFR-CR-01）。

49. 共通設定のCopilot login操作のheadless UI試験。共通設定タブのlogin区画とAutomation ID、3つのbuttonが入力画面と同じ`ExecutionViewModel`のcommandに結び付くこと、状態文の一致、設定画面の表示・遷移・保存・読込で認証確認・loginが自動開始されないこと、「通常モデル」のplaceholderを検証する。実CLI・実ブラウザー・実資格情報は使わない（AC-047、FR-SETLOGIN-01〜03）。

50. 思考レベル・Context Sizeの選択のdeterministic test。fakeの`ModelInfo`から、思考レベルの順序・表示名・`(Default)`・未知値・非対応／`auto`の未指定、Context Sizeの既定／拡張tier・`M`／`K`／整数の表示・未知容量・int境界、model別の希望保持と`modelPreferences`の保存・復元・旧形式・不正値・保存失敗、再取得での利用不能時の実行拒否と「既定に戻す」、run開始時のmodel・effort・tier・prompt budgetの固定、全session種別と再試行への同じ値の受渡し、checkpointの`contextTier`の保存・再開一致・旧checkpoint互換、Run sheetの`ContextTier`を検証する。実CLI・実AI・実課金は使わない（AC-049〜050、FR-MO-01〜07、NFR-MO-01）。

## 20. 外部仕様出典

本要求の実装時は、固定するversionの一次資料を再確認する。

### 出典台帳（2026-11-03追補）

| ID | 資料名・位置 | 版・日付 | 確認日 | 取得・確認範囲 |
|---|---|---|---|---|
| SRC-002 | 要求所有者の依頼原文（conductorからの引継ぎ、run 202611032044） | 2026-11-03 | 2026-11-03 | 本文確認。「設定画面の指定出力先と実行出力先は、1つのファイルの出力先に」「フォルダーピッカーの画面コンポーネントを使って、入力じゃなくて、画面操作のみで選択」。添付画像そのものは未取得、指定出力先と読取専用の実効出力先の二欄という要旨を確認。既存の出力workbookのフォルダーを単一欄にする変更として扱い、設定保存fileやcheckpointを統合する意味にはしない。 |
| SRC-004 | 要求所有者の依頼原文（conductorからの引継ぎ、run 202611040007）と結果workbookの既存構造（Workbooks/Writing、docs/result-excel-description.md §3・§5） | 2026-11-04 | 2026-11-04 | 依頼本文と背景の確認。シート`採点結果`の見出し例（ID・質問Nの評価・質問NのPromptの評価・合計）。 |
| SRC-003 | リポジトリの既存出力先UI・保存契約 | HEAD 91fe973、2026-11-03 | 2026-11-03 | PowerShell／rgで本文確認。SettingsView.axaml:103–116は明示指定のTwoWay入力と実効pathのOneWay読取専用表示。ExecutionViewModel.cs:658–680は空白→nullと入力隣接resultの算出、同:1078以降は設定復元。docs/settings.md:56–64は指定保持・null解除・算出値非保存。InputView.axaml.cs:29–55にネイティブfile pickerの既存実装があるが、出力用folder pickerの実装は未確認（OpenFolderPickerAsync等の検索は該当なし）。§9.1・§11.8・AC-013の出力・snapshot契約は維持。 |
| SRC-008 | 要求所有者の依頼原文（run 202611052355）、`ExecutionViewModel.ProgressText`、`ExecutionView.axaml`:128–132の`OperationProgressSummary`・ツールチップ・Accessible Name、`MainWindow.axaml`:242のAccessible Name、`DurableQuantificationOrchestrator`のcheckpoint、`RunSummary.OperationFailureCount` | 2026-11-05 | 2026-11-05 | 依頼本文の表示例と成功・失敗の定義、既存の「成功数とは別」「送信回数とは別」の文言の位置を確認（grepで実在を確認。コードは変更していない）。 |
| SRC-007 | 要求所有者の依頼原文（run 202611050055）、`src/StudyReportEvaluator.App/App.axaml`の`TechnicalBackgroundBrush`(#EAF5FF)・`TechnicalBorderBrush`(#62A9E8)・`TechnicalTextBrush`(#174F7C)、MainWindowの`Button.shell-action.cancel` | 2026-11-05 | 2026-11-05 | エラー表示の配色が青系で見落としやすいという依頼と現行ブラシ値を確認（依頼原文・App.axamlの記載）。Microsoft Design（Fluent 2）とWCAG 2.2 SC 1.4.3・1.4.11は依頼の引用であり、本担当は外部資料を取得していない（コントラスト比は計算で確認）。 |
| SRC-006 | 要求所有者の依頼原文と添付画像（run 202611042230）、`ResultsOutputViewModel.cs`の`RunSummaryText`・`ResultsRowStatus` | 2026-11-04 | 2026-11-04 | 結果画面に成功件数とエラー種別ごとの件数を足す依頼と、既存の行状態の分類（成功・回答空欄・取消・未処理・未確定・技術エラー）を確認。 |
| SRC-005 | 要求所有者の依頼・承認対象と関連する既存契約（run 202611040836） | 2026-11-04、HEAD bad7f3f | 2026-11-04 | 引継ぎ本文「履歴は入手ができない」「改善点を実装してください」と今回の明示承認対象を確認。未検証指標・新規既定減点ゼロ・既存係数保持、質問文字列のみ・参照集合の開始点5、ラベル付きローカル評価・設問holdout・失敗と互換性を承認。PowerShell／viewで§7.2・§7.5・§8・AC-004・AC-009〜012、src/StudyReportEvaluator.Core/Domain/QuantificationDefinition.cs:20–24の既定係数0.1、Similarity/SurfaceTextSimilarityCalculator.cs:1–112の既存指標、App/Copilot/AuxiliaryEvaluationRunner.cs:1–68のrun条件指定を事実確認。既存の保護境界は§14。実AI実験・外部書込・課金・pushは行わない。実学生の識別性能は未確認。 |

外部確認: Context7のAvalonia公式資料検索を試みたが実行環境のhookが参照ツールを拒否したため、外部確認未実施。迂回せず既存契約と依頼原文を根拠とした。社内情報未確認。ネイティブfolder pickerの固定版での適合とOS画面での操作確認は後続担当が行い、既存file pickerの存在をfolder pickerの実機成功証拠とはしない。

2026-09-06のdelivery追補ではsingle-file互換モードの非推奨注意、Windows保護、CLI本人認証の境界を確認した。一般仕様は本製品のclean-host／login成功証拠ではない。GitHubの最新main資料の構成・API・optionを固定SDK `1.0.11`／CLI `1.0.79`へそのまま適用せず、固定版のhelpと実測で確認する。

- GitHub Copilot SDK: [Getting started](https://github.com/github/copilot-sdk/blob/main/docs/getting-started.md)
- GitHub Copilot SDK: [Bundled CLI](https://github.com/github/copilot-sdk/blob/main/docs/setup/bundled-cli.md)
- GitHub Copilot CLI: [Authenticating GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/authenticate-copilot-cli)
- GitHub Copilot SDK: [Session persistence](https://github.com/github/copilot-sdk/blob/main/docs/features/session-persistence.md)
- GitHub Copilot SDK: [Usage and billing metrics](https://docs.github.com/en/copilot/how-tos/copilot-sdk/features/usage-and-billing)（固定版[v1.0.11](https://github.com/github/copilot-sdk/blob/v1.0.11/docs/features/usage-and-billing.md)、確認日2026-10-01、§11.21）
- GitHub Copilot: [Usage-based billing for individuals](https://docs.github.com/en/copilot/concepts/billing-and-usage/individuals/billing)（AIクレジットの定義、確認日2026-10-01、§11.21）
- Avalonia: [File dialogs](https://github.com/AvaloniaUI/avalonia-docs/blob/main/docs/services/file-dialogs.md)
- Microsoft: [.NET application publishing overview](https://learn.microsoft.com/dotnet/core/deploying/)
- Microsoft: [Single-file deployment overview](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview)
- Microsoft: [dotnet publish](https://learn.microsoft.com/dotnet/core/tools/dotnet-publish)
- Microsoft: [SmartScreen reputation](https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation)
- Microsoft: [.NET RID catalog](https://learn.microsoft.com/dotnet/core/rid-catalog)
- Microsoft: [Choose a Windows app distribution path](https://learn.microsoft.com/windows/apps/package-and-deploy/choose-distribution-path)
- Microsoft: [Sign an MSIX package](https://learn.microsoft.com/windows/msix/package/signing-package-overview)
- Microsoft: [Package a desktop app manually](https://learn.microsoft.com/windows/msix/desktop/desktop-to-uwp-manual-conversion)
- Avalonia: [macOS deployment](https://docs.avaloniaui.net/docs/deployment/macos)
- Avalonia: [Supported platforms](https://docs.avaloniaui.net/docs/supported-platforms)
- Apple: [Notarizing macOS software](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)
- Apple: [Customizing the notarization workflow](https://developer.apple.com/documentation/security/customizing-the-notarization-workflow)
- Apple: [Resolving common notarization issues](https://developer.apple.com/documentation/security/resolving-common-notarization-issues)
- Microsoft: [Working with formulas](https://learn.microsoft.com/office/open-xml/spreadsheet/working-with-formulas)
- Microsoft: [Excel specifications and limits](https://support.microsoft.com/office/excel-specifications-and-limits-1672b34d-7043-467e-8e27-269d656771c3)

## 21. Traceability summary

| Surface | Primary owner |
|---|---|
| Input/sample/mapping/picker | App workbook adapter + UI |
| Base/question/special allocation | Core domain/scoring + Design UI |
| Normal/special Prompt | Core prompting + Copilot adapter |
| Reference answer/similarity | Copilot adapter + workflow |
| Excel formulas/sheets | Core formula + App workbook writer |
| Checkpoint/resume | App workflow + workbook adapter |
| Prompt-file launch | App composition + Design UI |
| Warning/progress/results | App UI |
| 4-step／設定・通常／例外layout・往復 | MainWindow／SettingsViewと既存Input・Design draft／Execution snapshot（AC-016／017／037、TR-17／36）。MainWindowSettings／WorkflowState／ResponsiveLayout／CompactWorkflowLayout testsで局所検証済み（VERIFIED_SCOPED）。最小契約・測定条件は`dev/docs/ui-layout-contract.md`。T39追加nativeは部分観測でFAIL、Narrator・本人walkthroughはNOT_RUN_EXTERNAL_PREREQUISITE |
| 設定明示保存・出力先復元／保存定義明示適用 | ApplicationSettings／SettingsFileStore、SettingsViewModel、Inputのread-only明示適用、Executionの希望値と実効値（AC-035／036、TR-34／35）。一時pathのstore／VM試験とSettingsWorkflowSystemTestsの実file E2Eで局所検証済み（VERIFIED_SCOPED）。新instance復元を別process再起動とせず、既存checkpointは別責務 |
| Windows delivery | App限定single-file profile／EXE package（AC-030、TR-30）+ 既存public ZIP publish/package tests + development MSIX mechanism tests |
| 標準抽出・起動引数・data保護 | .NET標準host + 既存App起動契約 + 実EXE package tests（AC-031／032、TR-31） |
| Login開始・取消・終了 | bundled login専用service + Execution UI（AC-033、TR-32）。本人認証はCH-06 |
| macOS source foundation | macOS publish/package/sign/notary static contract tests |
| OS-only実測 | fresh Windows 11 x64標準userでのexact EXE CH-01〜06（AC-029／034、TR-33） |
| Platform release matrix | 既存protected CI/release workflow + candidate-bound v2 matrix／metadata evidence（AC-028／034、TR-29／33） |
| User/developer documentation | docs + dev/docs + screenshot tests |
| 実行コスト表示・ジョブログ | App `Usage`の集計と`Logging`のJSONL writer、Execution／Resultsが共有するコストView（AC-039、TR-38）。JobUsageTracker／SdkUsageAdapter／UsageProvenance／JobCostBackend／JobCostView testsで局所検証済み（VERIFIED_SCOPED）。実AI・実課金照合・native確認は未実施（AIクレジットの表示時換算は2026-10-01に§11.21・TR-48で追加） |
| 実行中の速報値表示 | `DurableEvaluationScheduler`／`DurableQuantificationOrchestrator`の`LivePreviewUpdate`通知、`LiveQuantificationPreviewViewModel`、`ExecutionView`の速報値領域（AC-042、TR-41、FR-LP-01〜09／SEC-LP-01〜02／NFR-LP-01）。LivePreview試験で局所検証。実AI・native表示は未実施 |
| 起動時の自動Copilotログイン | `ExecutionViewModel.RunStartupAuthenticationAsync`と`App.AttachStartupAuthentication`（AC-040、TR-39、FR-AL-01〜08／SEC-AL-01〜03）。CopilotLoginCommandTestsの起動時自動ログイン試験で局所検証。実CLI・実ブラウザー・実資格情報での確認は未実施（本人操作が必要） |
| 結果画面の設問文表示 | `ResultsOutputViewModel`（設問別得点・基準の設問文）と`ResultsOutputView`（AC-041、TR-40、FR-RS-01〜06。一覧は設問別得点を表示しない5列）。ResultsQuestionTextTestsと既存Results系UI試験で局所検証 |
| 利用できる全modelの選択 | `SdkCopilotAuthenticationRuntime.MapAvailableModels`、`ExecutionViewModel`のcache反映、`SettingsFileStore`のcache上限（AC-043、TR-42、FR-MS-01〜05）。CopilotModelEnumerationTests・ModelCatalogTestsで局所検証。実accountでの列挙確認は未実施（本人操作が必要） |
| 結果Excelの解説文書 | `docs/result-excel-description.md`、README・利用者indexからのリンク、公開文書allowlist（`WindowsSingleFile.pubxml`と各package／publish script）、`ResultExcelDescriptionTests`（AC-045、TR-43、FR-XD-01〜06）。実装の列定数・実際に書いたsheetとの一致とpackage収録を局所検証。文章の平易さはレビュー者の通読 |
| 実行準備部品の集約・step状態文 | `InputView`＋`ExecutionPreparationPanel`（左列の移設先）、`MainWindow`のExecutionViewModel結線、`MainWindowViewModel`のstep状態文（AC-046、TR-44、FR-PREP-01〜07／FR-STEP-01〜03）。PreparationOnInputTests・MainWindowTests・ExecutionViewTestsで局所検証 |
| 通常評価の設問タブ | `EvaluatorSettingsView`の`QuestionSelector`（ListBox、AC-048、TR-45、FR-QT-01〜05、§11.22）。EvaluatorSettingsViewTests・CompactWorkflowLayoutTests・MainWindowSettingsTestsで局所検証 |
| 結果画面の学生の回答と評価内容 | `ResultsAnswerSource`（入力identity確認つきの1行読込）、`ResultsOutputViewModel`（詳細表示時の読込・行単位の記憶・古い読込の破棄）、`ResultsCriterionViewModel`（回答・理由・引用・根拠の場所・説明の文言）、`ResultsOutputView`の回答欄・評価欄（AC-051、TR-46、FR-RV-01〜06／SEC-RV-01／NFR-RV-01）。ResultsAnswerReviewTestsと既存Results系UI試験で局所検証 |
| 評価項目の説明の初期値 | `DefaultCriterionDescriptions`（Core）、`QuantificationDesignViewModel`の評価項目・評価方法の追加、`InputViewModel`の提案設問（AC-052、TR-47、FR-CD-01〜03）。DefaultCriterionDescriptionTestsで局所検証 |
| 結果画面の総実行時間とAIクレジット | `RunMetricsFormatter`（Usage、時間・AIクレジットの書式）、`JobUsageTracker`のコスト要約・詳細、`JobCostLogReader.Render`、`ResultsOutputViewModel.RunMetricsText`と`ResultsOutputView`の`ResultsRunMetrics`（AC-053、TR-48、FR-RT-01〜02／FR-CR-01〜04／NFR-CR-01）。ResultsRunMetricsTests・JobUsageTrackerTests・既存Results系UI試験で局所検証。実請求額との照合は未実施 |
| 思考レベル・Context Sizeの選択 | `ModelOptionPolicy`（思考レベル・Context Sizeの選択肢と表示）、`ExecutionViewModel`のmodel別希望・run request固定、`SettingsFileStore`の`modelPreferences`、checkpointの`contextTier`、`RunSheetWriter`の`ContextTier`（AC-049〜050、TR-50、FR-MO-01〜07／NFR-MO-01）。ModelOptionsTests等で局所検証。実account・実課金での確認は未実施 |
| 共通設定のCopilot login操作 | `SettingsView`の共通設定タブの`SettingsCopilotLoginPanel`と、入力画面と共有する`ExecutionViewModel`のcommand（AC-047、TR-49、FR-SETLOGIN-01〜03）。headless UI試験で局所検証。実CLI・実ブラウザーでの本人loginは未実施（CH-06） |

## 22. Approval record

### 決定記録・変更履歴（2026-11-04、run 202611040836）

| 日付 | 決定者・種別 | 対象 | 決定・影響 |
|---|---|---|---|
| 2026-11-04 | 要求所有者・依頼承認（SRC-005） | FR-114、FR-115、FR-116 | 学生のAI履歴を入手できない前提の改善を、明示された今回範囲として承認。approval_policy=包括承認だが、利用者データ・AI送信条件・生成量に触れる要求は包括承認で代替せず依頼承認を根拠とする。§7.2の単独生成、§7.5の検出目的、§5.1・AC-004の新規係数0.1、§8・§16の係数ゼロでの診断blank伝播を今回の承認で変更する。旧データの明示値は維持し、無断の不可逆移行は行わない。 |
| 2026-11-04 | AI提案・包括承認（approval_policy、今回依頼のみ） | PARAM-004 | 短文注意の表示値を採用。表示上の可逆なPARAMであり、学生の点数・権限・保存本文・AI送信・課金・不正判定の条件にしない。根拠は見落としやすい短い共通正答への注意であり、性能校正済みという意味ではない。利用者の指定で変更できる。 |
| 2026-11-04 | 変更履歴・影響追跡 | FR-114、FR-115、FR-116、INT-001 | IDのなかった既存業務契約を構造化し、AC-004・AC-009・AC-010・AC-011・AC-012は新設せず変更して§7.7へ移動、検証レベルを明記。AC-077〜090を追加。AC-014・AC-015は既存の保存・再開境界として再利用し本文は変更しない。§1・§3・§5・§7・§8・§10・§16・§18・§19を依頼範囲だけ整合。カタログに要求と既存Copilot連携の16観点を記録。 |
| 2026-11-04 | test-designerへの影響 | AC-004、AC-009〜012、AC-077〜090 | 既存の新規係数・単独参照・全失敗blankのケースは条件変更が必要。追加分は通常・固有失敗の維持、係数ゼロ診断失敗、条件／slot保存、旧欠損、再開、不完全集合、ローカルholdoutのsystem試験へ対応させる。台帳・コード・System Testは本変更で編集しない。統合による要求・AC廃止なし。 |
| 2026-11-04 | 実装項目のまとまり | FR-114→FR-115→FR-116 | 最大の実装項目数内で、確認指標・採点保護、有限参照・保存互換、ローカル再現評価の順にまとめる。FR-115はFR-114の診断契約、FR-116は両者の比較契約を使う。FR-114/115はshared=yes・security=yes（既存の採点式・保存／送信境界）、FR-116はshared=yes・security=no（共通診断利用・合成データのみ）。scope縮小なし、実装完了や精度向上の主張なし。 |
| 2026-11-04 | AI提案・包括承認（approval_policy、工程2監査 20261104-0855-run1 のHIGH 3件・MEDIUM 1件） | AC-010、AC-011、AC-012、AC-079、AC-081、§7.5、§8.3、§17.2 | C1: 減点のSimilarityは完全な参照集合の参照別合成値の最大値（従来の単独参照と同じく保守的、AC-010と一致）。中央値・最小値・差は表示のみ。C2: 完全な診断＝新形式は予定slotすべて成功かつ各参照の合成値算出済み、旧形式（単独参照）は旧予定数1の成功で完全とし旧採点値を保持（AC-079と両立）。一部失敗・成功0は不完全で係数が正ならAC-011のblank伝播。C3: §17.2の対象外は採点値を決めるための投票・多数決・中央値と明確化し、AC-081の中央値等は記述統計の表示のみ。M1: 空回答は参照状態にかかわらずSimilarity 0・減点0（AC-012優先）。FR本文と承認済み要求の目的は変えず、条件の明確化に限る。新規ID・廃止なし。test-designerはAC-010・AC-011・AC-012・AC-079・AC-081のケースを見直す。残りのMEDIUM（M2〜M4）は§23 A-SC-01〜03へ。 |
| 2026-11-04 | AI提案・包括承認（包括承認 2026-11-04・202611040836、最終監査 A-02） | §11.19 境界・例外、A-RV-02、§7.7 前文 | 最終監査の確度高の直接矛盾（§11.19・A-RV-02「類似度の項目別を詳細に追加しない」⇔ FR-114 AC-077・AC-078・AC-081）を、実装・統合済みの FR-114 に合わせて解消。結果詳細に類似度の確認指標の詳細を「確認指標・未検証」として表示することを許し、固有評価の項目別は従来どおり追加しない。A-RV-02 の覆す条件が FR-114 の依頼で満たされたと扱う。AC 本文・FR-RV-01〜06 の表は変更しない。新規ID・廃止なし。 |
| 2026-11-04 | 包括承認の一覧（202611040836） | PARAM-004、AC-010・AC-011・AC-012・AC-079・AC-081（工程2監査 C1〜C3・M1）、§11.19・A-RV-02（最終監査 A-02） | この run で approval_policy により包括承認した項目は上の 3 行に記録したものだけ。利用者は次の Prompt の answers で却下・修正できる。FR-114〜116 は依頼承認であり包括承認ではない。 |
| 2026-11-05 | 要求所有者・明示依頼承認 | §3.2、§7.1、§11.3、§11.10、§13、§16、§17、AC-020／023／030／033／040、INT-001 | 必須bundled／fixed Copilot CLIを廃し、固定SDKとOS利用者がPATHに導入したCLIを認証・model列挙・評価に一貫使用する。Windowsは絶対PATH entry上の`copilot.exe`（空・相対PATH entryを無視）、Unix source foundationは実行可能な`copilot`。CLI identity version／SHA-256は診断・再開互換情報に限り信頼attestationにしない。CLI不在はOS install／PATH設定を案内（WindowsはWinGet推奨）、SDK非互換は明示runtime error。既定App buildは`CopilotSkipCliDownload=true`。歴史的publishは明示供給CLIを含めてもよいが、runtimeはCLI manifest／bundled fileを使わない。現hostで観測されたWinGet導入native `copilot.exe`のreported CLI version `1.0.99-2`、PE version `1.0.98`は観測値であり固定要件ではない。§13〜§18の過去release／package証跡は歴史的記録として維持し、新規質問・採番・包括承認なし。 |

調査記録: `rdcheck.py similar`と`similar --pairs`で既存を探索し、同じ目的のIDなし契約とACを再利用した。今回範囲で統合・廃止すべき別FRはない。originをfetchして管理データの未取込更新がないことを確認。今回の判断は依頼本文と既存契約で解決したため、社内情報未確認・外部仕様確認未実施。新たなSDKの仕様や実AIの挙動は主張しない。競合・新規質問・BLOCKEDなし。既存のbaseline警告と依頼外の監査指摘は維持する。

工程上の警告: 要求定義時点では今回の承認済み要求は未実装であり、構造化したsystem ACの台帳対応も後続工程で行う。docs-onlyのerrorsはゼロ、警告は既存の数値直書き4件と今回の未実装／台帳未対応22件を分けて扱う。警告を消すために検証レベルを下げたり、実装・台帳対応済みと偽って記録したりしない。

2026-11-05決定記録（run 202611050055、要求所有者の依頼）: FR-118・AC-093〜096・PARAM-005〜007（エラー表示の赤系配色。配色の変更だけで、データ・権限・保存・動作は変えない）を依頼により承認済み（依頼 2026-11-05）にした。包括承認はない。質問票の新規Qはない。
2026-11-03決定記録（run 202611032335、要求所有者の依頼）: FR-111・AC-068・AC-069（[実行]画面の同一情報の重複表示の解消。表示の出し分けだけで、データ・認証・保存・進捗計算は変えない）を依頼により承認済み（依頼 2026-11-03）にした。包括承認はない。質問票の新規Qはない。
2026-11-04決定記録（run 202611040021、要求所有者の回答）: FR-111を承認（承認済みのまま）。A-1の回答としてAC-037に「実行画面では本文の進捗を保持とみなす」を追記。A-2の回答としてFR-111・AC-069の条件を「Execution step表示中（設定画面を開いていない間）」に統一（実装は既にこの動作でコード変更なし）。A-1・A-2を解決済みにした。包括承認はない。
2026-11-04決定記録（run 202611040003、要求所有者の回答）: 必須設問が空のときの扱いは0点とする。任意設問が空の場合も既存どおり（§7.4）AIへ送らず0であり、設問の必須・任意で扱いは変わらない。空回答は常にAIへ送らず0点で、新しい要求・受入基準は作らない（§7.4の文面に明記。コード変更なし）。

2026-11-03決定記録（run 202611032044、要求所有者の依頼、SRC-002）: FR-002・AC-055の出力フォルダー欄統一とネイティブ選択・アプリ内手入力禁止を依頼により承認。既定への解除は空欄入力の代替となる画面操作に置換し、保存・入力変更・取消・実行・予約済みpathの既存契約を維持する。approval_policyは「安全範囲は推奨どおり」だが、新しいAI提案要求の包括承認はない。出力workbookの形式・命名・上書き禁止や保存データ体系を変更せず、実装済みの他要求は再queueしない。UIの読み違いと手入力を減らす既存機能変更として目的§1に整合し、効果の定量値は未測定。実機のネイティブ選択確認を受入条件とし、既存PASSを変更後の合格として扱わない。

| 項目 | 内容 |
|---|---|
| Approver | 本repositoryの要求所有者 |
| Initial requirement source | 2026-09-01の本セッションで提示されたアプリケーション要件 |
| Default-decision approval source | 同日の後続指示「不明点はデフォルトのプランを採用してください。全てのタスクを実行してください」 |
| Release-scope update source | 2026-09-02の正式公開README実装指示とADR-0013 |
| Sample consolidation source | 2026-09-03の`sample/SampleReport.xlsx`を唯一のsampleとして扱う要求所有者指示 |
| Setup simplification source | 2026-09-03の「WindowsとMac OSでのセットアップをシンプルに」「不明点はデフォルトのプラン」「全てのタスクを実行」指示 |
| Question text synchronization source | 2026-09-05の「主回答列を選択したら、その列のExcelのシートの値を設問 textに表示」要求所有者指示 |
| One-action startup approval source | 2026-09-06の[1操作起動プラン](../dev/docs/archive/work/20260906-0617-one-action-startup-plan.md)のデフォルト案・全タスク実行承認と[実行上書き](../dev/docs/archive/work/20260906-one-action-startup-execution.md)。D-06のlogin buttonは採用済み。元プランの未承認表記ではなく、この後続承認・最新指示を優先する |
| UI/settings approval source | 2026-09-07の要求所有者による[UI・設定保存プランv2](../dev/docs/archive/work/20260907-ui-settings-redesign-plan-v2.md)のD01〜D20デフォルト採用・全実装の明示承認。D18に従い要求版をv4.6／本日へ更新し、元プランのG0未通過・承認待ち表記より後続承認を優先する。D20は明示出力先を再起動・入力変更後も復元し、nullの場合だけ入力隣接result |
| Delivery decision | [ADR-0015](../dev/docs/adr/0015-windows-macos-installer-delivery.md)の履歴を保持し、[ADR-0016](../dev/docs/adr/0016-windows-one-action-startup.md)で単一EXE主配布・ZIP代替・標準全内容展開・login導線・matrix v2を追加 |
| Approved scope | 本書§1〜§21の要求。UI・設定保存／明示適用／出力先復元の差分と、既存業務・入力・採点・数式・checkpoint・privacy、Windows unsigned EXE／ZIPと各sidecar、既存non-public development MSIX回帰、exact EXE clean-host公開gateを含む。D19は非公開MSIXの同梱文書と検証リスト同期だけでinstall／署名／公開要件を拡張しない。macOS source/staticは変更せずproduction deliveryはscope外 |
| Delivery scope revision | 2026-09-04の要求所有者指示「開発用のMSIXでOKです」「外部ブロッカーの情報はないです」 |
| Previous version / changelog override（履歴） | D-14／R03の予定MINOR更新を取り消す最新指示を優先し、実装・文書task完了後のR03でKeep a Changelog形式のUnreleasedへ概要・実装済み変更を追加して、製品版をPATCH `0.8.3` → `0.8.4`とした。R03後の最終artifact再検証を要求し、版変更だけを公開成功へ読み替えない。要求文書v4.5を製品版や公開版と混同しない |
| Latest version / changelog override | UIプランD17の当初上書きは全タスク後のUnreleased追記 → PATCH `0.8.4` → `0.8.5`だった。さらに要求所有者の利用者不在時の自律続行指示により、T39をBLOCKEDのまま承認済みF01／F02を進める。F01はREVIEWED、親担当が製品版正本を0.8.5へ一度だけ更新済み。T01では製品版・CHANGELOGを変更しないという履歴は維持する。要求v4.6・製品版・公開版は独立し、最終bytes変更後の再検証・clean-host公開条件を省略しない |
| Implementation / validation status | 要求承認済み。T01は要求・契約・追跡・要求版metadataの同期で、当時のUI／設定は未実装・試験NOT_RUNだった。T01〜T35はREVIEWEDだった履歴を維持し、現在は2026-09-07の[実行記録](../dev/docs/archive/work/20260907-ui-settings-execution-record.md)と後続引継ぎでT01〜T38がREVIEWED、T39はBLOCKED。実装と局所試験の対応はVERIFIED_SCOPED。T35の対象文書試験は4/4成功・敵対的レビュー済みで、別scopeのT36は自身のt36-current.trxで21/21成功。製品`0.8.6`は2026-10-01に公開済みで、前回公開版は`v0.8.1` ZIP。0.8.4のT37実ZIP・T38実EXE・T39自動回帰／MSIX成功と追加native FAILを分離する。公開はrepository ownerの明示判断でCH-01〜06を実施せず、protected publish-release workflowではなくdraft解除で行った。本人確認／隔離利用者保存／CH-01〜06はNOT_RUN_EXTERNAL_PREREQUISITEで、G4・全タスク完了・clean-host PASSは付与しない |
| Auto model selection source | 2026-09-15の要求所有者指示「`auto`を通常評価modelとして選択できるように必要なら要求定義から変更」。同梱CLIを実測し、`auto`はrouterでtoken上限を公開しないことを確認した上で§7.1・§10.3・§10.4・§11・§15・§16を改訂した。上限不明modelを拒否せず、model相対のcontext budget検査だけを適用外とし、既定値の推定と別modelへのfallbackは行わない。要求版はv4.6のままで、製品版・公開版とは独立 |
| Timeout / reasoning effort source | 2026-09-24の要求所有者指示「7.6節のattempt timeoutはSDKの60秒に従う」「Thinking Effortはmedium。答案の定量化なのでHighは不要」。当初はattempt全体を60秒としたが、実機の通し実行で起動・session作成に約10〜25秒かかり、SDKの60秒の応答待ちより先にattempt側が満了してAI_TIMEOUTとなった（応答完了直前の打切りを含む）ため、§7.6は応答待ちをSDK既定の60秒とし、attempt全体の外側上限120秒は維持した。§7.1へreasoning effort `medium`を追加した。同梱CLIの実測で、`auto`と非対応modelへeffortを指定するとsession作成が失敗することを確認し、対応modelだけに指定する。2026-09-25の要求所有者指示「effortを記録する」により、attemptごとの指定値をジョブログへ記録する（§7.1、§11.9）。同日の指示「schema不正の根本原因を調査・修正する」により、claude-sonnet-5がtool引数から定数のevaluator IDを省略することが原因と確認し（`medium`で10/30、未指定で2/30）、§7.3でアプリが補う。要求版はv4.6のままで、製品版・公開版とは独立 |
| Similarity / effort / concurrency source | 2026-09-25の要求所有者指示「類似度判定を最適化する」「並列度をSDKが許容する最大値まで設定できるようにする（最速実行時間・LLM回答の高い一貫性・Tokenコスト最小化）」「全ての評価・回答で同じreasoning effortにする（採点の一貫性のため、最低でよい）」。§7.5を生成AI出力の貼り付け検出向けのローカル表層類似度へ改訂し、§7.1のreasoning effortを`medium`からrun-level統一（希望`low`）へ改訂、参照回答を固定`auto`から通常評価と同じmodelへ変更した。SDKには並列session数の上限がないため、並列度はapp判断として合成Promptの実測で頭打ちとなった8を既定、16を最大とし、rate limitは専用statusでbackoff付き再試行と有効並列度の縮退を行う。2026-09-27に、同指示と実装に合わせて§7.2・§7.6・§9.2・§11・§11.9・§14・§16・AC-009・AC-039・§19（7、15、36、38）に残っていた固定`auto`・LLM類似度・4 operationの記述を整合し、rate limit／quota枯渇の挙動を明記した。要求版はv4.6のままで、製品版・公開版とは独立 |
| Startup automatic login source | 2026-09-30の要求所有者依頼「アプリケーションの起動時に、ユーザーがPCやMacなどにログインしているアカウントで、GitHub Copilot CLIに自動的（可能な限りユーザーが何もしなくてもいいように）にログインしてください」。§3.2・§3.4・§11.3・§14・§17.1・AC-033・AC-037を改訂し、§11.10（FR-AL-01〜08、SEC-AL-01〜03、NFR-AL-01）、AC-040、TR-39を追加した。削除した要求IDはない。「起動時にloginやAI評価を自動開始しない」という旧規定のうちlogin部分だけを置き換え、AI評価の自動開始禁止は維持する。要求版はv4.6のままで、製品版・公開版とは独立 |
| Results question text source | 2026-09-30の要求所有者依頼「[4.結果・出力]の画面で、questionの文字列は、元のExcelのデータを表示してください。どの文字列が評価されたのかを私が理解しやすくするためです。元の学生のレポートを見ないと判断ができません。」。§11（結果の記述）を改訂し、§11.11（FR-RS-01〜05）、AC-041、TR-40を追加した。削除した要求IDはない。設問別得点の表示だけを設問IDから設問文へ変更し、採点・出力・checkpoint・設定の契約は変更しない。要求版はv4.6のままで、製品版・公開版とは独立 |
| Live preview source | 2026-09-30の要求所有者依頼「実行中の画面で、現在、どの文字列を、どんなPromptで、どう定量化したのかを、画面右側の中央あたりの{実測}の下に表示してください。元のExcelデータの1行と1:1になるように速報値として表示をしてください。全件を表示します。」。不明点は利用者不在のため§23のA-LPを採用し、§11.12・AC-042・TR-41として追加した |
| All-models selection source | 2026-09-30の要求所有者依頼「私がGitHub Copilotで利用できる全てのモデルを選択できるようにしてください。」。§7.1を改訂し、§11.13（FR-MS-01〜05）、AC-043、TR-42を追加した。変更した要求: cache件数上限512→4096（FR-MS-05、旧上限を規定するIDはなく本文の記載だけ）。削除した要求IDはない。不明点は利用者不在のため§23のA-MSを採用した。要求版はv4.6のままで、製品版・公開版とは独立 |
| Model options source | 2026-10-01: 機能追加・変更「ログイン後の全モデル表示、思考レベルとContext Sizeの選択」。追加FR-MO-01〜07、NFR-MO-01、AC-049〜050。変更FR-MS-04、FR-XD-01、AC-045（Run 26項目）、§7.1。削除IDなし。既存の全model・自動login・入力不変・採点・privacy境界を維持。製品版・公開版は変更しない。固定SDKでcontext tierを指定できることを確認し、実課金呼出しは行わずfakeで検証する。 |
| Question details guide source | 2026-09-30の要求所有者依頼「`## 2. 採点設計`に、{設問の詳細}を押した後の詳細設定の画面の使い方の説明についてもスクリーンショットもつけて記載をしてください。文章は大学や高校の教員が理解できるようにしてください。」。§11.14（FR-DOC-01〜04）、AC-044を追加した。削除した要求IDはない。アプリの挙動は変更しない。不明点は§23のA-DOCを採用した |
| Result Excel guide source | 2026-09-30の要求所有者依頼「実行結果のExcelシートの詳細な解説を、ITに詳しくない大学・高校の教授や先生が理解できるような説明用のドキュメントを作成して`/docs/result-excel-description.md`に保存をして、`/README.md`から適切な文章とリンクもつけてください。」。§9.4（FR-XD-01〜06）、AC-045、TR-43を追加した。削除した要求IDはない。アプリの挙動・出力Excelの形式は変更しない。不明点は利用者不在のため§23のA-XDを採用した。要求版はv4.6のままで、製品版・公開版とは独立 |
| Preparation relocation source | 2026-10-01の要求所有者依頼「[3.実行]画面の中の、左型の[GitHub Copilot CLIへのログイン]などの全ての画面コンポーネントを、「1.入力」の画面の中に移動させてください。」「画面上部の[訪問済み]の表現を[設定済み]に変更してください。」。§11.15（FR-PREP-01〜07）、§11.16（FR-STEP-01〜03）、AC-046、TR-44を追加し、§3.2・§11・§11.4の該当記述を更新した。削除した要求IDはない（「訪問済み」の表示文言をFR-STEP-01へ置換）。ViewModelの振る舞い、認証・再開の契約、永続化は変更しない。要求版はv4.6のままで、製品版・公開版とは独立 |
| Question tabs source | 2026-10-01の要求所有者依頼「画面の[2.定量化設定]の[通常評価]で、各設問がコンボボックスで切り替えられるようになっています。これをタブで切り替えられるようにしてください。タブには設問名を表示してください。」。§11.18（FR-QT-01〜05。2026-10-05に番号重複のため§11.22へ改番）、AC-048、TR-45を追加した。削除した要求IDはない。選択状態・編集内容・保存の契約は変更しない。不明点は利用者不在のため§23のA-QTを採用した。要求版はv4.6のままで、製品版・公開版とは独立 |
| Results answer review source | 2026-10-01: 機能変更・追加。依頼原文「添付の実行後の画面での評価状況で表示される文字や情報を以下にしてください。設問に対して、学生の回答と、その回答をどう評価したのか?これは作成するExcelで作成されている情報です。…自分の設定の確認をするという目的で表示させたいです。」。追加: §11.19（FR-RV-01〜06、SEC-RV-01、NFR-RV-01）、AC-051、TR-46、§21の追跡行、§23のA-RV-01〜05。変更: FR-RS-04（設問文の表示位置を回答欄の先頭へ）、§11.11の境界・例外、§11の結果の記述、§14（結果画面の1行再読込）、A-RS-01（回答本文を表示しない仮定を覆した）。削除した要求IDはない。採点・出力workbook・checkpoint・設定・AI送信の契約は変更しない。不明点は利用者不在のため§23のA-RVを採用した。要求版はv4.6のままで、製品版・公開版とは独立 |
| Criterion description defaults source | 2026-10-01の要求所有者依頼「[2.定量化設計]の画面の[通常評価]の中の、画面右側の[評価項目]の説明の初期値を、[評価方法]に対応して以下としてください。」（Knowledge Cover／Prompt 分析の各本文）。機能変更。追加: §11.20（FR-CD-01〜03）、AC-052、TR-47、§21の追跡行、§23のA-CD-01〜03。変更: 評価項目の説明の従来の初期値（「評価する知識ポイントまたは観点を記述してください。」、入力画面の提案設問の「回答内で説明・関係・適用を確認する知識ポイント」「必要な視点を引き出す具体性、論理性、実行可能性」）を§11.20の本文へ置換した。削除した要求IDはない。評価項目の名前・重み・range、AI Prompt、採点・出力・checkpoint・保存の契約は変更しない。不明点は利用者不在のため§23のA-CDを採用した。要求版はv4.6のままで、製品版・公開版とは独立。2026-11-03の依頼で、評価方法の種類を3つ（Knowledge・Prompt・Custom）へ拡張した。変更: §5.2、§11.20、FR-CD-01〜03、AC-052、A-CD-01。追加: A-CD-04、A-CD-05。包括承認（approval_policy）: 説明の既定値の文面と種類の追加。削除した要求IDはない。決定記録: FR-CD-01〜03の3種類（Knowledge・Prompt・Custom）への拡張を「包括承認 2026-11-03・202611032047」で承認済みにした（A-CD-04・A-CD-05は仮定のまま。監査指摘X-01〜X-05は§25へ転記）。2026-11-03の利用者回答 Q-002: a（Prompt・Custom既定本文を現状のまま承認）を反映した（承認済み（Q-002 2026-11-03））。A-CD-05を要求所有者承認済みに、X-04を解消にした。本文・ACは変更していない。 |
| Results list question score removal source | 2026-10-01: 機能削除・変更。依頼原文「[4.結果・出力]画面の、[設問別得点]は、削除してください。プレビューで表示するには情報量が多すぎるためです。」と、「4 結果・出力」の一覧の画面写真。追加: FR-RS-06（一覧は5列だけで設問別得点を表示しない）、§23のA-RS-04。変更: FR-RS-01（表示先を詳細だけに）、FR-RS-02（一覧の省略表示の記述を削除）、§11.11の例、§11の結果の記述、AC-041（操作のfilterと④⑥）、TR-40、§21の追跡行、A-RS-03（前提の一覧表示が無くなったため適用外と記録）。削除した要求IDはない（一覧の「設問別得点」列はFR-RS-01の表示先の一部だったため、IDを削除せず表示先を変更した）。`QuestionEarned`の計算、詳細の設問別得点、出力workbook、checkpoint、override、設定、AI送信の契約は変更しない。要求版はv4.6のままで、製品版・公開版とは独立 |
| Run metrics source | 2026-10-01: 機能追加・変更。依頼原文「実行結果の画面に以下の情報を付与してください。定量化のジョブの総実行時間(HH:MM:SS)/AI Credit (これは金額のコストに関わるので大変重要)」と「4 結果・出力」の画面写真。追加: §11.21（FR-RT-01〜02、FR-CR-01〜04、NFR-CR-01）、AC-053、TR-48、§21の追跡行、§23のA-RT-01〜02・A-CR-01〜03。変更: §11.9の単位の規定（旧「換算根拠を確認できるまでAIクレジットの数値を表示しない」を、SDK資料で換算根拠を確認したため§11.21の表示時換算へ置換）、§11の結果の記述、§17.1、AC-039、TR-38、§21のコスト行。削除した要求IDはない。JSONLログschema・Excel出力・checkpoint・設定・AI送信の契約は変更しない。要求版はv4.6のままで、製品版・公開版とは独立 |
| Merged design screen source | 2026-11-02の要求所有者依頼（採点配分と通常評価を1画面へ統合、設定は共通のみ、入力詳細・固有評価・読込Promptを設定から削除）。追加: §11.23（FR-001〜003、AC-054〜055）、A-MG-01〜02。変更（統合・読替え）: §11.4の5カテゴリ→共通のみ、§11の主画面一覧と§12.2の読込Prompt、§11.22の通常評価の置き場所（FR-QT-01〜05は統合画面で維持）。廃止した要求IDはない（この時点では機能は維持、画面だけ削除）。その後Q-001回答B（2026-11-02、run 202611021453）で3機能を廃止（FR-003、AC-057〜059、A-MG-02決定済み・A-MG-03、FR-DOC-01〜04・AC-044を廃止）。競合の記録: FR-DOC-01と§11.14・A-DOC-01の「設問の詳細→入力詳細の設定画面」、AC-048のtest名（EvaluatorSettingsViewTests等）、AC-037の「5カテゴリ」の記述は新しい画面構成と食い違うため、実装時に更新が必要（承認済み要求の文面は本依頼の範囲外のため変えず、競合として記録）。要求版はv4.6のままで、製品版・公開版とは独立 |
| Settings login source | 2026-10-01の要求所有者依頼「GitHub Copilotへのログイン画面がありません。…アプリケーションの起動時に私が介在しなくても自動的にログインを行うようにしているかを確認してください。もしそうでなければ、ログインする機能を添付の[設定]の[共通設定]の中に追加してください。」。§11.17（FR-SETLOGIN-01〜03）とAC-047を追加し、2026-10-05にTR-49と§21の追跡行を補った。削除した要求IDはない。認証・login・token非収集の契約（§11.3、§11.10）は変更しない。新しい仮定はない。要求版はv4.6のままで、製品版・公開版とは独立 |
| 0.8.6 publication exception | 製品`0.8.6`（2026-10-01公開）は、repository ownerの明示判断により、§13.6〜13.7・AC-028・AC-034が要求するexact EXEのclean-host試験CH-01〜06とprotected publish-release workflowを経ずに、draft解除で公開した。これは受入条件の充足ではなく、要求所有者による例外判断の記録である。受容したリスク: OS-only環境での起動・本人login・MOTW／SmartScreen下の動作が未実測のまま公開されていること。要求は緩和しない。CH-01〜06は`NOT_RUN_EXTERNAL_PREREQUISITE`のままとし、G4・clean-host PASSを付与しない。以後の公開は§13.6〜13.7に従う。例外を解消する条件: 公開済みexact EXEに対するCH-01〜06のPASS、または同条件を満たす後続版の公開 |
| Requirements review source | 2026-10-05の要求所有者依頼「レビュー結果を詳細に吟味して、要求定義書を更新してください。」。要求定義書レビューの指摘を、実装（`ScoringAllocationCalculator`、`OutputPathPlanner`、`RetryAndCleanupCoordinator`、`SettingsViewModel`のmodel一覧cache自動保存、`ModelOptionPolicy`）と照合して整合した。変更: §1の3（類似度はローカル計算）、§4.4（現行契約と履歴の区別）、§6.2（端数の算出規則）、§7.6（rate limitの再試行回数と総attempt上限）、§8.2（記号）、§8.3（W=0でも参照回答を生成）、§9.1（時刻はOSローカル）、§9.2（`.Similarity_AI_Raw`の名前）、§10.3・§10.4・AC-015・TR-14（Context Size tier）、§11.6・AC-035（`modelPreferences`・`cachedModels`と自動保存の例外）、§11.8・AC-037（login完了後の自動再確認）、§11.9、§11の実行画面の記述、FR-MO-03（表示規則）、§11.12の例、§11.16の例の位置、設問タブの§11.18→§11.22への改番と出典、§11の節順の整列、§17.1、§19（TR-49〜50）、§21、§22（本行・Settings login source・0.8.6 publication exception）、§23の前文、冒頭の同版内追補の方針。要求IDの削除・意味変更はない（記述を実装済み挙動へ一致させた）。要求版はv4.6のままで、製品版・公開版とは独立 |
| Legacy ID cleanup source | 2026-11-02の利用者承認。ADR・preflight・fixturesの旧ID参照は節番号などへ付け替え完了。旧番号体系のID（FR-0xx・NFR-xxx-00x）の廃止定義59個を持つ§24を削除した。要求の意味は変更しない。§24は欠番とする。 |
| Meaning | repository要求baselineの承認記録。実装完了・試験成功・release存在・tag／push／draft／公開操作の承認、組織の法務・教育・security承認または電子署名を意味しない |

## 23. 仮定・未解決事項

本節は2026-09-30の起動時自動login依頼（A-AL）、結果画面の設問文表示依頼（A-RS）、実行中の速報値表示依頼（A-LP）、利用できる全modelの選択依頼（A-MS）、結果Excelの解説文書依頼（A-XD）、通常評価の設問タブ依頼（A-QT）、2026-10-01の結果画面での学生の回答と評価内容の表示依頼（A-RV）、評価項目の説明の初期値依頼（A-CD）、結果一覧の設問別得点の削除依頼（A-RS-04）、結果画面の総実行時間とAIクレジットの表示依頼（A-RT、A-CR）、思考レベル・Context Sizeの選択依頼（A-MO）で選んだ仮定を記録する。本節の仮定には、未決（TBD）・仮定どうしの競合はない（ただし2026-11-02の統合画面依頼のA-MG-02は未決で承認待ち）。なお、作業taskのT39 BLOCKEDやCH-01〜06のNOT_RUNは§19・§22の検証状態であり、本節の仮定とは別である。

| ID | 種別 | 内容 |
|---|---|---|
| A-CR-01 | [ASSUMPTION] | AIクレジット＝nano-AI units ÷ 1,000,000,000とする。根拠: 固定SDK `1.0.11`とGitHub Docsの「Usage and billing metrics」が`totalNanoAiu`を「AI credit cost, in nano-AI units」と定義し`÷ 1e9`で換算する例を示し、SDKの`workflow.ts`が`NANO_AIU_PER_AIU = 1_000_000_000`でAIクレジット上限をnano-AI unitsへ変換している（確認日2026-10-01）。同文書は「課金上の換算はGitHubの課金文書を正とし、通貨のような値を出す前に確認すること」と注意しているため、通貨へは換算せず、請求確定額でない旨を常に添える（FR-CR-04）。影響: 表示値はSDKが報告した観測値の換算で、未報告の消費・プラン内の無料枠・請求の丸めを反映しない。覆す条件: GitHubの課金文書が異なる換算を定めた場合、または固定SDKの更新で単位が変わった場合（換算定数1か所の変更で対応する）。 |
| A-CR-02 | [ASSUMPTION] | 表示は小数4桁（0.0001 AIクレジット単位）とする。根拠: 1 AIクレジットは0.01米ドル（GitHub Docs、確認日2026-10-01）で、小数4桁は1回の小さな評価でも0と区別でき、画面の1行に収まる。正の値が丸めで0になる場合は`<0.0001`とし、無料と誤認させない。覆す条件: 桁数・丸め方の指定があった場合。 |
| A-CR-03 | [ASSUMPTION] | 依頼の「AI Credit」は、結果を作った同じジョブで観測できたAI使用量（再試行・失敗・未保存行を含む、§11.9）のAIクレジット換算とし、アカウント全体の当月利用量・残量・請求額は表示しない。根拠: アプリは課金APIへ問い合わせない境界（§11.9、§14）を持ち、追加の通信・権限を要しない。覆す条件: アカウント単位の利用量表示が求められた場合（課金APIの権限・privacy設計が別途必要）。 |
| A-RT-01 | [ASSUMPTION] | 「定量化のジョブの総実行時間」は、§11.9のジョブ（開始・再開の1操作）の開始から終了までの壁時計時間とし、最終workbook確定とcleanupを含める。再開した実行では中断前のジョブの時間を加算しない。根拠: AIクレジットと同じ集計範囲にすると、時間とコストを同じジョブについて比較できる。checkpointは中断前の各ジョブの所要時間を保存していない。影響: `ResultsRunIdentity`の開始・終了の差と一致しないことがある。覆す条件: 中断前を含む累計時間を求められた場合。 |
| A-RT-02 | [ASSUMPTION] | 表示位置は「4 結果」の見出し行の件数要約の右とし、「3 実行」の画面には総実行時間を追加しない（AIクレジットはコスト要約の既存行で両画面に出る）。根拠: 依頼は「実行結果の画面」を対象とし、画面写真で空いている見出し行なら既存の配置・44 DIP契約を変えずに常時見える。覆す条件: 実行画面での経過時間表示が求められた場合。 |
| A-MO-01 | [ASSUMPTION] | 添付のContext SizeはSDKの入力prompt枠（token budget）として表示する。根拠: 固定SDKはdefault/long-contextの2tierを指定でき、billing metadataにtier別prompt枠があるが拡張の総contextは公開されない。影響: 272K/1MはSDKがその値を返す場合だけ現れ、全modelで同じ2択を保証しない。覆す条件: tier別総contextの公式metadataが提供された場合。 |
| A-MO-02 | [ASSUMPTION] | 未編集の思考レベルは既存の希望low、contextはdefaultを維持する。明示選択はmodel別に保存する。根拠: 既存のコスト・一貫性方針を保ち、model切替で非対応設定を漏らさない。影響: SDKのDefault表示と初期のlow選択は異なる場合がある。覆す条件: 初期値をSDK defaultへ合わせる依頼。 |
| A-MO-03 | [ASSUMPTION] | context metadataの実験的SDK型は固定1.0.11に限定し、該当accessだけGHCP001を抑止する。根拠: 同versionで型とJSON契約を確認済み。影響: SDK更新時にmetadata・serialization回帰を再検証する。覆す条件: stable APIの提供。実accountの全model権限・請求額・Windows native本人loginの確認は外部前提で、本変更はfakeによるAC-049〜050の検証であり公開gateの解除ではない。 |
| A-MO-04 | [ASSUMPTION] | 共通設定の既存950×450 DIP slotを保つため、採点定義名・revision・丸めの3項目を同じ1行へまとめ、空いた高さに思考レベルとContext Sizeを配置する。根拠: 既存44 DIP・body340 DIP・footer内包のcontractを維持する。影響: 項目の役割・保存・keyboard既存順は変わらず、長い名前はTextBox内で横scrollする。覆す条件: 別の配置を求められた場合。 |
| A-AL-01 | [ASSUMPTION] | 「PCやMacにログインしているアカウント」は、OS利用者ごとにPATHから解決したCopilot CLIが利用する既存GitHub資格情報（§11.10）と解釈する。根拠: 2026-09-30に確認したGitHub Copilot CLI資料にWindows／macOSのサインインアカウントをCopilotへ直接連携する手段は記載されておらず、アプリがOSアカウントからGitHub資格情報を作る実装は独自認証providerとなり§17.2・SEC-AL-01に反する。影響: OSサインインだけではGitHubへログインしない。覆す条件: GitHubがOSサインイン連携を提供した場合。 |
| A-AL-02 | [ASSUMPTION] | 既存資格情報がない場合の「可能な限り利用者が何もしない」は、起動時にCLIのブラウザー認証を1回自動で開始し、利用者がブラウザーで承認するだけの状態にすることとする。根拠: 資格情報がない状態でCLIがlogin対話なしに認証することはできない。影響: 起動時にブラウザーが開く。覆す条件: 起動時のブラウザー自動起動が不適切と判断された場合、環境変数の既定値を無効へ変更する。 |
| A-AL-03 | [ASSUMPTION] | 自動loginは1アプリ起動につき1回とし、取消・失敗後は自動再試行しない。根拠: 取消した利用者へ認証画面を繰り返し提示しない安全側の動作。覆す条件: 再試行間隔・回数を利用者が指定した場合。 |
| A-AL-04 | [ASSUMPTION] | 任意の抑止手段として環境変数`STUDY_REPORT_EVALUATOR_AUTO_COPILOT_LOGIN`を採用し、設定画面・setting.txtの項目は追加しない。根拠: 既存のsetting.txt schema・§17.2「必須環境変数」を増やさず、後から低コストで変更できる。覆す条件: 利用者向けの設定UIが必要になった場合。 |
| A-AL-05 | [ASSUMPTION] | macOS source foundationでは同じ実装経路（Avalonia／PATHから解決したOS-installed CLI）が動く想定だが、macOSは現版の正式公開対象外（§17.2）であり、実測していない。影響: macOSでの動作は保証しない。 |
| A-RS-01 | [ASSUMPTION] | 依頼の「questionの文字列」は、結果画面の設問別得点に出ていた内部設問ID（`question-<hash>`）を指し、「元のExcelのデータ」はその設問に対応するExcelの質問文（質問文行×主回答列のセル、§4.3の9）と解釈する。学生の回答本文の表示は依頼文から一意に読み取れず、run後にExcelを再読込する必要が生じるため本版では実装しない。根拠: 画面の実例と依頼文の「どの文字列が評価されたのか」、privacy境界（§14）。影響: 設問の識別はできるが回答本文は画面に出ない。覆す条件: 回答本文の表示が必要と確認された場合は、別要求として入力の再読込とprivacy要求を定義する。（2026-10-01: 覆す条件が成立し、回答本文の表示は§11.19・A-RV-01〜05で定義した。設問IDを設問文へ置き換える解釈は維持する） |
| A-RS-02 | [ASSUMPTION] | 表示する設問文は、run開始時のsnapshotが持つ設問文（利用者が手動編集した場合はその値）とする。Excelセルの現在値を結果表示時に再読込しない。根拠: 前回の実行結果を現在のdraft・入力から再評価しない原則（§2.2の5、§11.8）。覆す条件: 元セルの現在値の表示が必要になった場合。 |
| A-RS-03 | [ASSUMPTION] | 一覧の設問別得点の文字列は、設問文を長さ制限せず連結し、列幅を超える分は既存の表示上の省略記号に任せる。根拠: 全文は詳細で確認でき、業務上の文字数上限を決める根拠がない。覆す条件: 一覧の列構成の変更要望。（2026-10-01: 覆す条件が成立し、一覧から設問別得点を除いた（FR-RS-06、A-RS-04）。本仮定は詳細の全文表示には不要となり、適用外） |
| A-RS-04 | [ASSUMPTION] | 依頼の「[4.結果・出力]画面の[設問別得点]」は、添付画面の一覧（プレビュー）にある見出し「設問別得点（詳細で全文）」の列を指すと解釈し、一覧の見出しと各行の値だけを削除する。詳細（「詳細・override」）の選択行1件分の設問別得点の全文（`ResultsQuestionEarnedFull`、FR-RS-01）は残す。削除した列の幅は「状態」列へ回す。根拠: 依頼の理由が「プレビューで表示するには情報量が多すぎる」であり、詳細は選択した1行だけを縦scroll付きで表示するため一覧の情報量の問題に当たらない。削除した見出し自体が「詳細で全文」と詳細での確認を案内していた。影響: 設問別の点は詳細を開くか出力workbookで確認する。覆す条件: 詳細の設問別得点の削除、または一覧への短い要約（例: 合計だけ）の表示を求められた場合。 |
| A-RV-01 | [ASSUMPTION] | 依頼の「学生の回答」は、選択した基準が属する設問の主回答列と補助列（AIへ送った列）の当該行のセル値と解釈する。根拠: 評価の根拠（`Evidence_Source`）が指しうるのはこの2種類の列だけで（`docs/result-excel-description.md` 5.3節）、§14は他の列・他の行をAIへ送らない。影響: 氏名等の非選択列は表示しない。覆す条件: 行の他の列の表示を求められた場合（privacyの再検討が必要）。 |
| A-RV-02 | [ASSUMPTION] | 「その回答をどう評価したのか」は、通常評価の基準ごとのAIの点・理由・根拠の引用・根拠の場所と、先生が設定した評価項目の説明（採点設計）・既存の計算previewと解釈する。固有評価の項目別の値・理由は詳細に追加しない。類似度は、FR-114（AC-077・AC-078・AC-081）の確認指標の詳細を「確認指標・未検証」として表示する（2026-11-04改訂。覆す条件「類似度の項目別表示を求められた場合」が FR-114 の依頼で満たされたため。包括承認 2026-11-04・202611040836）。根拠: 依頼の目的は「自分の設定（評価項目）の確認」で、既存の詳細は通常評価の基準単位の画面である。固有評価・類似度は行の固有点・類似減点として既に表示され、項目別の詳細は結果Excelにある。影響: 固有評価の理由は画面では確認できない。覆す条件: 固有評価の項目別表示を求められた場合。 |
| A-RV-03 | [ASSUMPTION] | 回答の読込元は、結果Excelではなくrunの入力Excelとし、実行時のSHA-256・サイズ・更新日時との一致を読込前後で確認する。根拠: 結果Excelの元のシートは入力の写しで内容は同じだが、取消・部分結果・出力失敗のrunにはfinal workbookが無い。入力は全runに存在し、実行時のidentity（`RunSummary.InputSnapshot`）で同一性を確かめられる。影響: 入力Excelを移動・変更した後は回答を表示できない（文言で結果Excelでの確認を案内する）。覆す条件: 結果Excelからの読込を求められた場合。 |
| A-RV-04 | [ASSUMPTION] | 読込は詳細表示中の選択行1行だけとし、成功した行の回答をその実行結果の表示中だけメモリに記憶する。根拠: 一覧の全行を読むと最大20,000行の再読込とメモリ保持が必要になり、必要な行だけに限る方が安全側である。影響: 初めて開く行は短時間「読み込んでいます…」になる。覆す条件: 一覧に回答の抜粋を出す要望。 |
| A-RV-05 | [ASSUMPTION] | 回答・理由・引用は文字数で切り詰めない。根拠: 1セルの上限はExcel仕様の32,767文字で、表示は選択した1行だけのため速報値（FR-LP-08）のような累積上限が不要。覆す条件: 実測で表示性能に問題が出た場合。 |
| A-LP-01 | [ASSUMPTION] | 依頼の「どの文字列を」は、各評価項目が使う選択済みの主回答列・補助列の当該行のセル値と解釈する。根拠: 評価Promptに実際に埋め込まれ、AIまたはローカル計算へ渡る文字列はこれだけである（§14）。影響: 選択されていない列や他行の値は出ない。覆す条件: 行全体のセル値の表示を求められた場合（プライバシー上の再検討が必要）。 |
| A-LP-02 | [ASSUMPTION] | 「速報値」は、AIまたはローカル計算が返した生の定量値（RawScore・Score・Similarity）と理由・根拠とし、QuestionEarned・SpecialEarned・SimilarityPenalty・FinalRaw・FinalScoreは含めない。根拠: これらはExcel数式の計算結果であり（§8）、アプリ内で数式を再実装すると出力workbookの値と食い違う恐れがある。影響: 速報値は最終評点の予測ではない。覆す条件: 仮点の表示を求められた場合。 |
| A-LP-03 | [ASSUMPTION] | 「元のExcelデータの1行と1:1」の対象は学生行だけとし、行に対応しない参照回答の生成は一覧に含めない。参照回答は類似度項目の比較文字列としてだけ表示する。根拠: 参照回答は設問単位で同一runの全学生に共有される。影響: 参照回答生成のPromptは画面に出ない（進捗は「実測: 参照 a / b」）。覆す条件: 参照回答のPrompt表示を求められた場合。 |
| A-LP-04 | [ASSUMPTION] | 各文字列の保持上限を2,000文字、保持合計の閾値を32,000,000文字（超過後は200文字）とする。根拠: 全件（最大20,000行×複数項目）を保持してもメモリを際限なく増やさないための安全側の値で、標準的な回答・Promptは上限内に収まる。行数は制限しない。影響: 極端に長い回答・Promptの末尾は画面に出ない（出力workbookには従来どおり保存される）。覆す条件: 実測で上限が不足する場合、定数を変更する。 |
| A-LP-05 | [ASSUMPTION] | 速報値の本文は画面とメモリだけに置き、ファイル・log・checkpointへ保存しない。根拠: 回答本文・Prompt・AI理由・根拠をlogへ記録しない既存方針（§2.2の6、AC-019）と、依頼に保存要求がないこと。影響: アプリ終了後・入力変更後は再表示できない（既存のcheckpointと出力workbookは変更しない）。覆す条件: 速報値のファイル保存を求められた場合。 |
| A-MS-01 | [ASSUMPTION] | 「利用できる全てのモデル」は、実行時にPATHから解決したCopilot CLI経由でSDK `ListModelsAsync`が当該accountへ返すmodelから、ポリシーで`disabled`のものと不正IDを除いた集合と解釈する。根拠: 2026-09-15の当時のCLI実測では`auto`を含む29 modelが返り、アプリ側の絞込み・件数上限は存在しなかったため、依頼の実質的な欠陥は、(1)1件の不正IDが列挙全体を失敗させ得ること、(2)cache 512件超がsetting.txt全体を無効にすること、(3)選択しても使えない`disabled` modelが選択肢に混ざり得ることと判断した。この件数は現行PATH CLIの返却数を保証しない。影響: SDKが返さないmodel（プラン外・組織ポリシーで未提供）は選べない。覆す条件: `disabled` modelも表示したい場合はFR-MS-02(b)を外す。 |
| A-MS-02 | [ASSUMPTION] | cache上限は4096件とする。根拠: 実測は29件で、旧512件は将来のmodel増加に対して余裕が乏しく、4096件でも設定fileは高々数百KBである。上限を超える一覧は保存せず選択のみ可能にする。覆す条件: 上限の変更は`ApplicationSettings.MaximumCachedModels`を変えるだけでよい。 |
| A-DOC-01 | [ASSUMPTION] | 「スクリーンショット」は、既存の実画面PNG `images/02-input-mapping.png`（設問の詳細から開く入力詳細画面を写す）の再利用とする。根拠: 同じ画面で、画像は8枚固定でpackaging・文書のcontract testが列挙している。影響: 新しいPNGを追加しない。画面の変更時は既存の再生成手順（AC-022）で更新する。覆す条件: 別の画面状態の画像が必要になった場合は9枚目の追加と全contractの更新を別依頼とする。 |
| A-XD-01 | [ASSUMPTION] | 依頼の「教授や先生」は、Excelを開いて列を見る・セルへ数値を入力する基本操作ができる大学・高校の教員と解釈する。根拠: 本アプリの対象利用者（§2.1）と、結果Excelを開く操作の前提。影響: Excelの基本操作の説明は含めない。覆す条件: Excelの操作自体の説明が必要と確認された場合。 |
| A-XD-02 | [ASSUMPTION] | 列名・sheet名・状態コードはExcelに書かれる英語の文字列のため、翻訳せず`コード書式`で示して日本語で意味を説明する。設問・評価方法・評価項目のコードは設計ごとの自動生成値のため値を列挙せず、「コード＋列の種類」の形で説明する。根拠: 利用者が実ファイルの見出しと照合できること。覆す条件: 列名自体を日本語化する別要求。 |
| A-XD-03 | [ASSUMPTION] | スクリーンショットは追加せず、表と計算例だけで説明する。根拠: 依頼に画像の指定がなく、画像は8枚固定でpackaging・文書のcontract testが列挙している（A-DOC-01）。覆す条件: Excelの実画面の画像が求められた場合は別依頼とし、生成手順と全contractの更新を伴う。 |
| A-XD-04 | [ASSUMPTION] | 新しい文書を公開文書allowlist（`WindowsSingleFile.pubxml`と各package／publish script、対応する試験）へ加える。根拠: 配布物内のREADMEが新文書へリンクするため、収録しないと抽出後のリンクが切れる（AC-027／030、`WindowsPublishPackageTests`の抽出後リンク検査）。影響: 配布物の文書が1件増える。文書だけの変更でも、公開する場合は最終EXE／ZIPとsidecarを再生成・再検証する（既存の運用）。覆す条件: 配布物へ文書を含めない方針にする場合は、READMEの当該リンクを配布物外の扱いにする別要求が必要。 |
| A-XD-05 | [ASSUMPTION] | 解説は公開`v0.8.6`の出力に基づく。旧公開`0.8.1`が同じ列を出力するかは確認していない。根拠: 本リポジトリのwriter実装だけを確認した。影響: 版注記で利用者へ明示する（FR-XD-03）。覆す条件: `0.8.1`の出力を実測した場合。 |
| A-PREP-01 | [ASSUMPTION] | 「左型」は「左側（左列）」と解釈し、対象を従来の「3 実行」画面の左列（Copilot認証、checkpoint再開、出力方針文、技術的な問題）の全部品とする。右列・上部の実行条件要約・下部の開始／中断・前後移動は移さない。根拠: 依頼文の「全ての画面コンポーネント」と提示画像の左列。影響: 技術的な問題の一覧も入力画面に移る。覆す条件: 技術的な問題を実行画面に残す指示があった場合は§11.15の表の「実行前の判断」行を分ける。
| A-PREP-02 | [ASSUMPTION] | 配置は「1 入力」の本文内に、ファイルpath行の切替buttonで開閉する区画として置く。根拠: 通常画面は外側スクロール不要（§11.5）で、最小window（1024×720 DIP）の本文に入力部品と実行準備部品を同時に置く余地がないため。影響: 実行準備部品は常時は見えない。覆す条件: 利用者が別の配置（常時表示の右列等）を指定した場合。 |
| A-STEP-01 | [ASSUMPTION] | 「訪問済み」を指す「設定済み」は表示文言だけの変更とし、内部の状態名（`WorkflowStepState.Visited`）・CSS class・判定は変えない。根拠: 依頼は「表現」の変更であり、意味の変更（設定の保存・検証成功）を求めていない。影響: 「設定済み」は設定値が有効であることを保証しない（FR-STEP-03）。覆す条件: 実際の設定完了を表す判定が必要な場合は別要求で判定を定義する。 |
| A-QT-01 | [ASSUMPTION] | 「タブ」は、設問ごとに1つのタブ項目（表示名のみ）を横に並べる選択部品と解釈し、選択した設問の内容を別の内容領域へ切り替える既存の構造（評価方法・評価項目の選択、基本／Promptの編集タブ）を変えない。根拠: 依頼は設問の切替手段の変更だけを求めている。影響: 評価方法・評価項目の選択はcomboboxのままである。覆す条件: それらもタブ化する指示があった場合。 |
| A-QT-02 | [ASSUMPTION] | タブが多く1行に収まらない場合は折り返して最大2行まで表示し、超える分は設問タブ領域の縦スクロールで到達させる。根拠: 最小window（1024×720 DIP）で外側スクロールを不要に保つ（§11.5）。覆す条件: 横スクロール等の別方式を指定された場合。 |
| A-MG-01 | [ASSUMPTION] | 「マージした1つの画面」は、既存の主画面「2 採点設計」（採点配分）へ、設定の「通常評価」の内容を取り込む形とし、step名・Automation IDは変えない。添付画像を参照できないため、既存の要求とsourceの画面から解釈した（2026-11-02、FR-001〜002）。覆す条件: 画像の配置・見た目が異なる場合は別依頼で修正する。 |
| A-MG-02 | [決定済み] | 設定から削除した「入力詳細」「固有評価」「読み込みPrompt」の機能は、Q-001回答B（2026-11-02）により機能ごと廃止した（FR-003）。 |
| A-MG-03 | [ASSUMPTION] | 廃止の範囲は「3機能の編集・一覧・適用のUIと、それを前提とする説明・操作」とし、採点の計算式（固有配点を含む）、既存定義・設定fileの読込・保存互換、command lineの`--prompt`の受理は維持する。根拠: 回答は機能の廃止を示すが、データの読込・保存・計算の廃止までは明示せず、最小の変更で既存の保存済みデータと配布済みの起動引数を壊さないため。影響: 固有評価・補助列を持つ既存定義は編集できないが計算は従来どおり。覆す条件: データ・計算・`--prompt`も廃止する指示があった場合（§6・§8・§12.1・§4の変更を伴う別依頼）。 |
| A-CD-01 | [ASSUMPTION] | 評価項目の説明の既定値は、評価方法の表示名ではなく種類で決める。根拠: 表示名は利用者が自由に変更できる。2026-11-03の依頼で種類は3つ（`KNOWLEDGE_COVERAGE`＝Knowledge、`PROMPT_ANALYSIS`＝Prompt、`CUSTOM_PROMPT`＝Custom）になり、旧版の「Prompt 分析＝`CUSTOM_PROMPT`」の対応は廃止した。影響: Custom評価方法の既定値は旧Prompt 分析本文ではなく短い雛形になる（既存の保存済みの説明は不変）。覆す条件: 表示名ごとに本文を変える指示があった場合。 |
| A-SR-01 | [ASSUMPTION] | `採点結果`シート（FR-113）は、完成workbookだけに入る5枚目のapp-owned sheetである。名前は`Quantification_`で始まらないため、FR-XD-01・§9.4・AC-045②の「4つのapp-owned sheet」を「`Quantification_`で始まる4つ」と「結果一覧`採点結果`1枚」の整合する文面に更新した。ID=SourceRow、Prompt列は有効なPROMPT_ANALYSIS評価方法がある設問だけ、値は数式参照、空欄・失敗は空欄のまま、無効な設問は列を作らない。根拠: 依頼文と確認済みの背景。 |
| A-CD-04 | [ASSUMPTION] | 新しい種類`PROMPT_ANALYSIS`（EvaluatorType.PromptAnalysis）を追加する。保存・読込・workbook設定シートの種類名は3値で、既存の`CUSTOM_PROMPT`はそのままCustomとして読む（後方互換、既存の説明は書き換えない）。Prompt種類は組込みの評価用Promptテンプレート（BuiltInTemplateVersionで固定）で評価し、採点・出力・AI契約はKnowledgeと同じ構造とする。利用者がテンプレートを編集する「Prompt」タブはCustomのときだけ使う。根拠: 2026-11-03の依頼と進行役の仮定。影響: §5.2・§11.20・AC-052を拡張。結果Excelの`EvaluatorType`列に新しい値が現れる。覆す条件: Prompt種類も利用者が編集できる指示があった場合。 |
| A-CD-05 | 要求所有者承認済み（Q-002 2026-11-03） | Prompt種類の既定値本文（§11.20）は、ベストプラクティスの8観点（役割・目的、具体的な指示、文脈、出力形式、例示、制約、段階的な思考、検証可能な基準）を並べた文面とし、Custom種類の本文は利用者が書き換える前提の短い雛形とする。文面はrd-authorが定めた。根拠: 依頼が趣旨だけを示し本文を委ねたため。影響: 文面の変更は`DefaultCriterionDescriptions`とAC-052の完全一致の更新になる。覆す条件: 要求所有者が文面を指定した場合。 |
| A-CD-06 | [ASSUMPTION] | 設定シートの読み戻し（FR-110）は本番のライブラリ機能（`ConfigSheetReader`相当）で、`CANONICAL_JSON`を正本として採点定義を復元する。壊れた・欠けた・未知の種類名は検証エラーの結果で返す（例外にしない）。UIへの接続（画面から読み込むボタン）は今回の範囲外とする。根拠: 書出し（FR-109）だけがあり往復を検証できないため、最小の変更で往復を成立させる。影響: 利用者の画面操作は変わらない。覆す条件: 画面からの読込を求められた場合（別依頼で§11.6を変更）。 |
| A-CD-02 | [ASSUMPTION] | 依頼文のKnowledge Cover本文の「行ってださい」は誤字と判断し、「行ってください」で実装する。それ以外の文字（`{論点}`、`論点:`、`- xxx`）は依頼どおりとする。根拠: 同じ依頼のPrompt 分析本文が「行ってください」であり、意図が明らかな誤記。影響: 依頼原文と1文字（「だ」→「く」）異なる。覆す条件: 原文どおりの綴りを求められた場合は`DefaultCriterionDescriptions.Knowledge`を直す。 |
| A-RR-01 | [ASSUMPTION] | FR-112 の再実行の比較元は、実装上、元の run の出力・RunSummary である（完了 run に checkpoint が残らないため）。model は元の run と同じとみなすが、画面で固定か選択か、`auto` の扱いは未定義。未解決（監査指摘 R112-01・R112-02、run 202611032347）。覆す条件: 要求所有者が比較元・保持 identity・`auto` の扱いを定めた場合。 |
| A-RR-02 | [ASSUMPTION] | 再実行（FR-112）は §10.5 の再開（完了済み行 skip）とは別の経路で、元の出力を上書きせず §9.1 の新名で出力する。再開の動作は変更しない。完了済み行の再検証・参照回答の再利用の細目は未定義（R112-03）。 |
| A-CD-03 | [ASSUMPTION] | 「初期値」は新規作成時の値であり、保存済み・読込済みの採点定義や既存の評価項目の説明は書き換えない。本文中の`{論点}`はAI Promptのplaceholderではなく説明文中の記載で、評価項目の説明はPromptへ値として挿入されるため再解釈されない（§5.4）。根拠: 既存データの暗黙の書換えは利用者の編集を失わせる。影響: 既存の定義の説明は従来のままである。覆す条件: 既存の説明の一括更新を求められた場合。 |

| A-FR111-01 | [ASSUMPTION] | FR-111の「同一」は、パス文字列の一致で判定すると仮定する（正規化・大文字小文字・別名パスの同一視はしない）。AC-037とFR-111の関係（実行画面では本文の進捗を保持とみなす）と、AC-069・FR-111の条件（Execution step 表示中、設定画面を開いていない間）は要求所有者が確定した（A-1・A-2は解決済み、2026-11-04）。未解決は「同一」の判定基準のみ（A-3）。覆す条件: 要求所有者が判定基準・扱いを指定した場合。 |
| A-SC-01 | [未解決] | 工程2監査M2（run 202611040836）: FR-116・AC-088の「固定のローカル評価専用ルール」の形（指標・比較・閾値）と決め方（調整群だけで定め、評価記録に版・値を残すか）が未定義。仮定: 調整群だけで定めて評価記録に残す。覆す条件: 要求所有者が別の決め方を指定した場合。 |
| A-SC-02 | [未解決] | 工程2監査M3（run 202611040836）: §17.1 required scopeにFR-114〜116が載っておらず、FR-116の成果物の種類（製品機能か保守用ツール・テストか）と置き場所が本文から特定できない。仮定: FR-116は製品外の保守用評価ツール・テストとする。覆す条件: 要求所有者が製品機能として求めた場合。 |
| A-SC-03 | [未解決] | 工程2監査M4（run 202611040836）: FR-114・FR-115の要求文が複数主題を含み変更時の影響範囲が広い。ACで分かれており矛盾ではないため今回は分割しない。次回以降の依頼で主題の分割を検討する。 |
| A-RR-03 | [未解決] | 最終監査 A-01（run 202611040836、確度高 HIGH）: FR-112 の選んだ行だけの再実行で、元 run の参照回答集合と比較条件（slot・status・比較方式版、旧形式は旧予定数）を再利用するか再生成するかが未定義。再生成すると AC-009（全学生が同じ集合）・AC-084（同じ snapshot）と両立しない恐れがある。仮定: 再利用し再生成しない。元の集合を取得できない・条件が一致しない場合は開始しない。決め方: FR-115 の実装時（次 run）に要求所有者が確定し、FR-112 本文と AC-072（参照生成呼出し 0 件・References 不変）の変更を R112-03 と統合して扱う。AC 本文は今回変更しない。 |
| A-SC-04 | [未解決] | FR-115・FR-116 は承認済み（依頼 2026-11-04）だが、run 202611040836 では時間予算のため未実装とし、次 run に回した（FR-114 だけを実装・統合）。関係する AC（AC-009・AC-080・AC-082〜090 など）の台帳のケースは未実行・未達のまま残る。実装完了・精度向上を主張しない。 |

## 25. 監査指摘

| ID | 日付 | カテゴリ | 重大度 | 位置 | 要約 | 状態 |
|---|---|---|---|---|---|---|
| C-1 | 2026-11-02 | 矛盾 | 中 | FR-DOC-01 | 「設問の詳細」が入力詳細画面を開くという記述が、FR-003と食い違っていた。Q-001回答BでFR-DOC-01〜04・AC-044を廃止し解消（AC-059）。 | 解決済み |
| C-2 | 2026-11-02 | 矛盾 | 低 | AC-048 ⑥ | 初期focusの記述が統合画面（FR-001）の構成と合わない。利用者承認（2026-11-02）で本文を直した（FR-QT-05、AC-048 ⑥）。 | 解決済み |
| C-3 | 2026-11-02 | 用語 | 低 | 行1184・行1281・§11.8・AC-037 | 「5 カテゴリ」「カテゴリ」の記述が統合後の画面構成と合わない。利用者承認（2026-11-02）で本文を直した（§11.5・§11.8・§11.22・AC-037・§18）。 | 解決済み |
| C-4 | 2026-11-02 | 競合 | 高 | FR-003 | 置き場所が未決のまま入口だけが除去されていた。Q-001回答Bで機能の廃止を決定し解消。実装の編集UI・読込Prompt適用のコードの除去はAC-057・058で確かめる。 | 解決済み |
| C-5 | 2026-11-03 | 不足 | 中 | §2.3、ペルソナ表 | 既存ui_policyの記述には利用者によるレイアウト変更・保存と、ペルソナごとの既定表現・キャンバス採否・代替手段の構造化記録が揃っていない。今回の出力先選択では従来の共通設定の読取専用テキストとキーボード選択操作を使い、承認済み全画面の設計変更は行わない。別途要求所有者の判断を要する既存の不足として記録し、今回の他要求再queueはしない。 | 未解決（依頼範囲外） |
| P-01 | 2026-11-03 | 目的整合・追跡性 | 中 | §1 / FR-001〜003 | 最終監査集計3/3。FR-001〜003のG-ID対応がなく目的ごとの追跡性が不足している。目的逸脱そのものの認定ではない。今回の依頼範囲外の既存課題として記録し、要求本文の再設計は行わない。 | 未解決（既存・依頼範囲外） |
| C-01 | 2026-11-03 | カタログ／台帳の追跡性 | 低 | docs/catalog.md FR-002 / E2E-002 | 最終監査集計2/3。E2E-002の実行コマンド `python -m pytest tests/system/test_fr002_output_folder.py -q` の対象パスがカタログ未記載だったため追記し解消。`tests/system/ui/OutputFolderSelectionTests.cs` は既に記載済み。 | 解決済み |
| X-01 | 2026-11-03 | 不足 | 高 | §5.2, A-CD-04 | Prompt種類の評価動作（組込みテンプレート、BuiltInTemplateVersion、採点・出力はKnowledgeと同構造）と結果Excel`EvaluatorType`列の新値にACがない。AC-052は説明の既定値と種類名の往復だけを検証する。次のrunへの課題（評価動作・結果Excel出力のFR・ACを追加するか、スコープ外と明記する）。 | 解消（FR-109・AC-064を追加。2026-11-03） |
| X-02 | 2026-11-03 | 矛盾の疑い | 中 | §5.4 | 「Knowledgeはapp-owned template…利用者は編集できない」とあり、同じapp-ownedのPrompt種類が書かれていない。6 placeholder制約の適用範囲が曖昧。「KnowledgeとPrompt」へ直す。 | 解消（2026-11-03） |
| X-03 | 2026-11-03 | 不足 | 中 | §6付近 | 「Knowledge / Custom evaluator」と2種類の記述が残り、3種類化と不整合の疑い。Promptを含めて更新する。 | 解消（2026-11-03） |
| X-04 | 2026-11-03 | 記述の質 | 中 | A-CD-05, §11.20 | Prompt・Custom本文はrd-authorが作文したASSUMPTIONで、AC-052が完全一致で固定する。根拠が要求所有者の指定でない。所有者の承認を取る（Q化）。 | 解消（Q-002回答 a、2026-11-03） |
| X-05 | 2026-11-03 | 記述の質 | 低 | FR-CD-01③, §11.20出典 | 「Knowledge Cover」と「Knowledge」の表記が混在。§11.20出典は2本文のままで11-03の追加を含まない。表記統一し出典に11-03を追記する。 | 解消（2026-11-03） |
| R112-01 | 2026-11-04 | 不足・矛盾の疑い | 高 | FR-112、§10.4、§10.5 | run 202611032347 の監査（旧C-01）。FR-112 の再実行前提検証「§10.4 と同じ検証」は checkpoint を比較元とするが、§10.5 は完成成功後に partial を削除するため、完了した run には比較元の checkpoint がない。実装は元の run の出力・RunSummary を比較元にした。完了後に保持する identity（input SHA-256・definition SHA-256・model・reasoning effort・Context tier）と比較元の明記、検証項目名の列挙が要求側に未反映。FR・AC本文は今回変更しない。 | 未解決（次のrunで要求を直す） |
| R112-02 | 2026-11-04 | 不足 | 中 | FR-112、AC-073 | 旧C-02。本文は「入力 workbook・採点定義・model など」の不一致で開始しないとするが、AC-073 は workbook と採点定義だけで、model・reasoning effort・Context tier の不一致がない。§10.4 は model 不一致を `CHECKPOINT_MODEL_MISMATCH` で拒否するため、`auto` の再実行が同一 model になる保証もない（仮定 A-RR-01）。 | 未解決（次のrunで要求を直す） |
| R112-03 | 2026-11-04 | トレードオフ | 中 | FR-112、§10.5、§9.1 | 旧C-03。§10.5 は完了済み行を skip するが再実行の対象は完了済み行で、再開機構の流用と境界が未記述。行単位マージ（元出力を上書きせず §9.1 の新名で出力）の可否、元 run の完了済み行内容の再検証（§10.4 末尾）との関係、新出力のパス予約・参照回答の再利用が未記述。「再開は変更しない」との境界を明記する（仮定 A-RR-02）。 | 未解決（要確認） |
| R112-04 | 2026-11-04 | 記述の質 | 低 | AC-070、AC-062 | 旧C-04。CHK-14 の数値直書き警告（PARAM 未参照）。AC-070 の合成データ（行 42〜51）は例示で実害は小さい。例示であることを明記するか PARAM 化する。 | 未解決（低・依頼範囲外） |
| A-1 | 2026-11-04 | 矛盾の疑い | 中 | AC-037 / FR-111 | AC-037「全画面で進捗・停止を保持」と、FR-111の実行画面でのシェル帯進捗の非表示との関係が曖昧。実行画面では進捗・停止は実行画面自身が持つのか、シェル帯が非表示でも保持と数えるのかを要求所有者が確定する。包括承認なし。 | 解決済み（回答 2026-11-04: AC-037に「実行画面では本文の進捗を保持とみなす」を追記） |
| A-2 | 2026-11-04 | 曖昧 | 中 | FR-111 / AC-069 | 要求は「Execution step 表示中」だが、AC-069は「実行中」と書く。run前・完了後に実行画面を表示している場合の扱いが未定。どちらかに文言をそろえる。包括承認なし。 | 解決済み（回答 2026-11-04: 条件をExecution step表示中（設定画面を開いていない間）に統一。実装は既にこの動作） |
| A-3 | 2026-11-04 | 記述の質 | 低 | FR-111 | 「同一」の判定基準が未記載。パス文字列の一致で判定すると仮定している（正規化・大文字小文字・別名パスの扱いは未定）。 | 未解決（仮定。仮定・未解決事項 A-FR111-01） |
| FA-A01 | 2026-11-04 | 条件不明による矛盾の疑い | 高 | FR-112・AC-072、A-RR-02、R112-03 ⇔ FR-115 AC-009・AC-082〜085 | 最終監査 202611040836 確度高3/3。選んだ行だけの再実行で参照回答集合を再利用するか再生成するかが未定義。仮定・未解決事項 A-RR-03 へ（FR-115 実装時＝次 run で決める）。 | 未解決（A-RR-03） |
| FA-A02 | 2026-11-04 | 直接矛盾 | 高 | §11.19 境界・例外、A-RV-02、§7.7 前文 ⇔ FR-114 AC-077・078・081 | 確度高2/3。結果詳細に類似度の項目別を追加しないとする記述が FR-114 と矛盾。 | 解決済み（2026-11-04 包括承認で §11.19・A-RV-02・§7.7 前文を改訂） |
| FA-A03 | 2026-11-04 | 不足 | 中 | §9.2、§10.3、§10.4、§9.4 FR-XD-01 ⇔ FR-114 診断列、FR-115 AC-080・082・083 | 確度高3/3。References 列・保存内容・FR-XD-01 の列数が単独参照のままで、FR-114 の診断列・FR-115 の slot・条件 metadata を含まない。§10.4 に参照集合の条件一致がない。FR-115 実装時に整合する。 | 未解決 |
| FA-A04 | 2026-11-04 | 条件不明による矛盾の疑い | 中 | §11.12 FR-LP-04 ②③・FR-LP-05 ⇔ FR-115 AC-081、FR-114 AC-078 | 確度高2/3。速報で複数参照のどれを比較文字列にするか、一部失敗の暫定値、「確認指標・未検証」の付記が未定。FR-115 実装前に決める。 | 未解決 |
| FA-A05 | 2026-11-04 | 目的整合 | 中 | §1、13 要求すべて | 確度高3/3。G-ID・成功指標・測定方法がなく目的ごとの追跡ができない（P-01 の拡大）。 | 未解決（既存・依頼範囲外） |
| FA-A06 | 2026-11-04 | 不足（範囲） | 中 | §17.1、FR-116、A-SC-02 | 確度高3/3。§17.1 に FR-114〜116 がなく、FR-116 の成果物の種類・置き場所が本文で決まらない。 | 未解決（A-SC-02） |
| FA-A07 | 2026-11-04 | 重複 | 中 | AC-064 ⇔ AC-065（similar 0.43） | 確度高2/3。AC-064 後半の往復の検証が AC-065 と重なる。統合案: AC-065 に寄せ AC-064 を書出し等に絞る（AC 本文の変更のため次の依頼で扱う）。 | 未解決 |
| FA-A08 | 2026-11-04 | 曖昧 | 中 | FR-107・FR-108「自動出力の result directory」 ⇔ FR-002・§9.1 | 確度高2/3。明示出力フォルダー指定時の解釈が読み分けられない。「§9.1 の実効出力先」に改める案。 | 未解決 |
| FA-A09 | 2026-11-04 | 記述の質 | 低 | §11 冒頭の 4 step 一覧、§11.7 ⇔ FR-003・AC-057・AC-058 | 確度高3/3。廃止した入口（詳細設定・読込Prompt）が本文に残る。削除か「（廃止、FR-003）」注記。 | 未解決 |
| FA-A10 | 2026-11-04 | 記述の質 | 低 | AC-004「旧既定値0.1」 | 確度高3/3。旧形式の補完係数が PARAM 未参照。 | 未解決 |
| FA-A11 | 2026-11-04 | 記述の質 | 低 | AC-058・AC-062・FR-112・AC-070 | 確度高3/3。CHK-14 WARN 4。例示の件数・行数が PARAM 未参照（R112-04 で扱う）。 | 未解決（R112-04） |
| FA-A12 | 2026-11-04 | 目的整合 | 低 | FR-110（ほか FR-108・FR-109・FR-113） | 確度高2/3。画面を持たない要求の上位が「既定制約（ui_policy）」。FA-A05 と合わせて直す。 | 未解決 |
| FA-A13 | 2026-11-04 | トレードオフ | 低 | §8.3 × PARAM-002=0 × PARAM-003=5 | 確度高2/3。W=0 でも参照を設問ごとに 5 件生成し AI 使用量が単独参照の約 5 倍。要求所有者の費用受入れの判断を記録する。 | 未解決（要求所有者の判断待ち） |
| FA-A14 | 2026-11-04 | 記述の質 | 低 | AC-013「4 app-owned sheets」 ⇔ FR-113 | 確度高2/3。完成 workbook の `採点結果` シートが反映されていない。 | 未解決 |
| FA-B01 | 2026-11-04 | 直接矛盾 | 高 | AC-018・§17.1・AC-031・AC-036・§11.8・§19 test 35 ⇔ FR-003・AC-058・§12.2 | 要確認1/3。`--prompt` 起動で AC-018 は GUI 事前入力、AC-058 は表示も適用もしない。FR-003 の優先範囲に §17.1・§18・§19 が含まれない。AC 本文の変更か優先範囲の拡大を要求所有者が決める。 | 未解決（要確認） |
| FA-B02 | 2026-11-04 | 直接矛盾 | 高 | §5.4 最終段落 ⇔ §7.5・§7.1・§14 | 要確認1/3。§5.4 は類似度を app-owned Prompt で学生回答と参照回答から組み立てると書くが、§7.5・§14 はローカル計算で AI へ送らない（送信境界）。§5.4 を「類似度は Prompt を使わないローカル計算」に直す案。 | 未解決（要確認） |
| FA-B03 | 2026-11-04 | 曖昧 | 中 | FR-116・AC-088、A-SC-01 | 要確認1/3。固定のローカル評価専用ルールの形と決め方が未定義。 | 未解決（A-SC-01） |
| FA-B04 | 2026-11-04 | カタログ | 中 | docs/catalog.md FR-109 行 | 要確認1/3。実装ファイル欄が「未実装」だが実装・テストがある。 | 未解決 |
| FA-B05 | 2026-11-04 | 条件不明による矛盾の疑い | 中 | FR-114 AC-004 ⇔ FR-110 AC-066 | 要確認1/3。係数の欠損が AC-066 の必須項目の欠損に当たるかが未定。 | 未解決 |
| FA-B06 | 2026-11-04 | 曖昧 | 中 | FR-114 AC-079 ⇔ §11.19 A-RV-03、FR-110 A-CD-06 | 要確認1/3。旧結果 workbook を読む経路が特定できない。 | 未解決 |
| FA-B07 | 2026-11-04 | 用語 | 低 | §3.0、§7.5、AC-078、AC-084 | 要確認1/3。類似度診断／類似度評価／確認指標の混在、AC-084 のシート名が実名でない。 | 未解決 |
| FA-B08 | 2026-11-04 | 記述の質 | 低 | §23 冒頭 | 要確認1/3。A-MG-02 を未決とし TBD なしと書く記述が現状と合わない。 | 未解決 |
| FA-B09 | 2026-11-04 | 外部連携 | 低 | docs/integrations.md INT-001 | 要確認1/3。関連する要求が FR-115 だけで、同じ Copilot SDK を使う通常・固有評価が未記録。 | 未解決 |
| FA-B10 | 2026-11-04 | 記述の質 | 低 | §8.3 例「W=0.1」、FR-XD-02 ④ | 要確認1/3。計算例の係数 0.1 が新規既定と誤読されうる。 | 未解決 |
| FA-B11 | 2026-11-04 | メトリクス | 低 | stats の ledger ⇔ ledger.json | 要確認1/3。集計時点で一致し解消。 | 解決済み（集計時に一致） |
| FA-B12 | 2026-11-04 | 不足 | 低 | §19 試験要求 | 要確認1/3。FR-114 の診断と FR-116 の対照評価の TR がない（AC・台帳では扱い済み）。 | 未解決 |
| F3-01 | 2026-11-04 | トレードオフ | 中 | AC-082 ⇔ AC-011 | 最終監査 202611041046（3/3 並行）。保存済みの終端失敗 slot は同一 run で再試行しない（AC-082）一方、W>0 では参照が 1 件でも失敗すると全学生の減点・総合点が blank になる（AC-011）。run 内に回復手段がなく、新規 run でないと総合点が出ない。意図（再 run のみ）かを明記するか、失敗 slot だけを教師が明示して再生成する条件を決める。 | 未解決（要求所有者の判断） |
| F3-02 | 2026-11-04 | 条件不明 | 中 | FR-112、AC-081 ⇔ AC-011、FA-A01 | 選択行の再実行（FR-112）での参照集合の再利用と、一部失敗集合での暫定集計が、採点に使う完全性の判定にどうつながるか未定義（FA-A01 と同根。実装後も未解決）。A-RR-03 を FR-115 の AC に反映するか FR-112 側で明記する。 | 未解決 |
| F3-03 | 2026-11-04 | 記述の質 | 中 | FR-116 AC-088・AC-089、A-SC-01 | 「固定のローカル評価専用ルール」の内容・出典（仕様の節または版）・置き場所と、評価記録の最小項目・形式が閉じておらず、AC-088 の検証可能性と AC-089 の再現性の検証対象が実装依存になる（FA-B03・FA-A06 と同根）。 | 未解決 |
| F3-04 | 2026-11-04 | 記述の質 | 低 | AC-009、AC-090 | system 指示・template 版への依存が比較結果に影響する点が、AC-090 の限界の列挙にない。限界に追記する。 | 未解決 |
| F3-05 | 2026-11-04 | 重複 | 中 | AC-064 ⇔ AC-065（similar 0.43） | 直接矛盾ではない。統合要否は依頼範囲外のため判断を保留（FA-A07 と同一）。 | 未解決（統合要否の判断待ち） |
| F3-06 | 2026-11-04 | 目的整合 | 低 | FR-114・FR-115・FR-116 | 上位が既定制約のみで G-ID がない（per_goal が空）。G-ID を付けるか、付けない理由を決定記録に書く（FA-A05・FA-A12 と同根）。 | 未解決（既存の構造） |
| F3-07 | 2026-11-04 | 不足 | 低 | AC-087、AC-090 | holdout の最小件数（設問数）の PARAM がなく、空の holdout だけが不成立で 1 件でも成立する。PARAM 化するか AC-090 に限定の説明を追記する。 | 未解決 |
| F3-08 | 2026-11-04 | トレードオフ | 中 | §8.3 × PARAM-002=0 × PARAM-003=5、FA-A13 | 係数 0 でも参照を設問ごとに 5 件生成するため、AI 使用量が約 5 倍になる。費用の受入れ判断が未解決（要求所有者が判断して記録する）。 | 未解決（要求所有者の判断） |
| F3-09 | 2026-11-04 | 記述の質 | 低 | AC-004「0.1」、FA-A10 | 旧形式の補完値 0.1 を直書き（PARAM 未参照。SRC-005 はコードの既定値を根拠にしている）。旧形式補完用 PARAM を新設するか、互換定数として A-ID に記録する。 | 未解決 |
| F3-10 | 2026-11-04 | 記述の質 | 低 | AC-077、PARAM-004 | 短文注意の値が未校正（表示専用で AC-078・AC-011 と整合）。変更時は PARAM のみを変更する。 | 未解決（現状のまま） |

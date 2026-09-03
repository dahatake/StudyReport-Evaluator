# StudyReport Evaluator v4.2 システムテスト Prompt集

| 項目 | 内容 |
|---|---|
| 文書用途 | ソフトウェアエンジニアまたはテスト支援AIが、ユースケース単位でコピーして実行するPrompt集 |
| 対象 | StudyReport Evaluator v4.2 |
| 基準日 | 2026-09-03 |
| 要求正本 | `docs/requirements-definition.md` v4.2 |
| 設計正本 | `dev/docs/detailed-design.md`、ADR-0012、ADR-0013、ADR-0015 |
| 対象環境 | functional baseline: Windows 11 x64。delivery candidate: Windows MSIX、macOS 14 / 15 / 26 x64・Arm64 |
| 必須決定的fixture | `tests/fixtures/system-test/SystemTest-10Students.xlsx` |
| 注意 | 本書はテスト結果ではない。未実施、過去結果、文書上の期待値を今回のPASSとして扱わない |

> **捏造禁止:** 期待結果は要求、実装、固定fixture、独立oracleから導出し、実測結果は今回の実行証跡だけから記録する。過去のtest件数、性能値、package hash、Live AI結果、未対応platformの結果を今回値として流用しない。

## 1. 正本と統合境界

本書は、旧24件版PromptのTest Requirement 1〜24、セッション添付の旧実データtechnical E2E資料、およびv4.2 deliveryのTR-25〜TR-29を統合した、repositoryで唯一のシステムテストPrompt正本である。旧Prompt fileは統合検証後に廃止済みであり、実行時は本書だけを使用する。

- repositoryに実在する要求正本は`docs/requirements-definition.md`である。
- `/hve-dev/requirement-definition.md`は本repositoryに存在しないため参照しない。
- 要求定義§19のTR-01〜TR-29を欠番なく網羅する。
- 10人fixture E2Eは、Promptを小規模かつ決定的に反復できるよう追加した補助scenarioであり、要求定義を改変しない。
- 531行synthetic E2E、10人synthetic E2E、canonical SampleReport technical E2Eを相互代用しない。
- optional Live Copilotとexternal recalculationをrequired deterministic testへ代用しない。
- 実データをLive AIへ送信しない。

### 1.1 統合した旧ユースケース

| 旧分類 | 統合後の扱い |
|---|---|
| shell / warning | 4-step UI、exact warning、keyboard、200% scaleへ統合 |
| input / mapping | Forms/Google row 1/2、canonical sample、10人fixture、pickerへ分離 |
| unsupported input | picker/direct path scenarioの形式・package安全境界へ統合 |
| dynamic design | 絶対Points、Base/Special/Similarity、均等配分、validationへ統合 |
| Knowledge / Custom Prompt | Knowledge、Custom、Special、closed tool schemaへ統合 |
| auth / execution / cancel | reference-first、retry、checkpoint、resume、cancelへ再編 |
| score / override | QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScoreへ統合 |
| output | 自動final、4 app-owned sheets、partial、atomic commitへ統合 |
| privacy / validation | reference／normal／special／similarityの4 operationへ拡張 |
| delivery | Windows publish、ZIP integrity、platform claimを別scenarioとして維持 |
| optional smoke | Live Copilotとexternal recalculationを2つの独立scenarioへ分離 |

## 2. Fixture契約

### 2.1 10人synthetic fixture（required）

| 項目 | 固定値 |
|---|---|
| path | `tests/fixtures/system-test/SystemTest-10Students.xlsx` |
| bytes | `7,652` |
| SHA-256 | `7F473879B3C43319D842881C5DFD1D01B2A60091A5B0C7B39B70B8D574D801C8` |
| worksheet | `SystemTestInput` 1件 |
| dimension | `A1:L11` |
| rows | header 1行 + 匿名synthetic data 10行 |
| formula | 0件。`= + - @`で始まるcanaryも文字列 |
| package安全境界 | macro、external link、DDE、defined nameなし |

このfixtureは`sample/SampleReport.xlsx`の**row 1 headerだけ**を転記し、data rowは全て新規のsynthetic値から作成した。実データのrow 2〜531をコピーしていない。各primary列F〜Jには空または空白primaryを1件ずつ置き、Jが空の行でもKを入力済みにして、primaryなしではsupportingだけでAI送信しない境界を検証する。

### 2.2 Canonical repository sample（TR-02専用）

要求定義§4.4のcanonical sample契約は次のとおりであり、10人fixtureで置換しない。

| 項目 | 固定値 |
|---|---|
| path | `sample/SampleReport.xlsx` |
| bytes | `470,806` |
| SHA-256 | `73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA` |
| package entries / relationships | 11 / 8 |
| worksheet / dimension | 1件（nameはhashのみ記録） / `A1:L531` |
| 初期候補 | F/I primary、G/J primary + student Prompt、H primary + supporting、K supporting、J→K support |
| 初期target / 対象外 | F〜K / A〜E、L |

fileが存在しない場合は期待値へ合わせて作成せず、当該scenarioを`BLOCKED`とする。F〜Kの役割はheader semantic由来の候補であり、固定mappingではない。
sample契約はこのexact pathだけを使用し、`sample` directoryの他fileを列挙、fallback、代用しない。

### 2.3 Canonical SampleReportのtechnical E2E利用

| 項目 | 固定baseline |
|---|---|
| logical path | `sample/SampleReport.xlsx` |
| bytes | `470,806` |
| local SHA-256 | `73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA` |
| identity binding | size、SHA-256、先頭128 bytesの一致 |
| package entries | 11 |
| worksheet / dimension | 1件 / `A1:L531` |
| rows | header 1行 + data 530行 |
| macro / external-link parts | 0 / 0 |

§2.2と同じ唯一のcanonical sampleを使用し、別のsample fileを列挙、作成、要求、代用しない。このworkbookのcell本文、header本文、worksheet名、学生情報をreportへ出力しない。Live AIへ送信せず、既存のlocal deterministic runnerだけでtechnical durable E2Eを行う。

### 2.4 Fixed-seed 531-row synthetic fixture

`tests/StudyReportEvaluator.App.Tests/E2E/SyntheticWorkbookFactory.cs`がseed `20260901`から実行時に作成する。`Original!A1:L531`、data 530行であり、repositoryへbinaryを追加しない。10人fixtureや実データのidentity oracleを流用しない。

## 3. Canonical SampleReport由来の設問とアプリ既定値

### 3.1 設問／source mapping

canonical `sample/SampleReport.xlsx`のrow 1 headerから得た現行の5 question definitionは次のとおりである。回答本文は抽出していない。

| Question | Primary | Supporting | Evaluator | Points |
|---|---|---|---|---:|
| ソフトウェアでの実装手段として、プログラミングと機械学習の違いについて述べる設問 | F | なし | KnowledgeCoverage | 8 |
| `[必須ではない] 設問(1) 作成のために、生成AIに入力したPrompt` | G | なし | CustomPrompt | 8 |
| `質問` | H | なし | KnowledgeCoverage | 8 |
| 実世界での機械学習（AI）の利用状況、技術、利用方法、採用理由を調査する設問 | I | なし | KnowledgeCoverage | 8 |
| `[必須ではない] 設問(4) 作成のために、生成AIに入力したPrompt` | J | K | CustomPrompt | 8 |

Kは、設問(4)(5)に関連してPrompt作成時に工夫した点、観点、論点を記すsupporting sourceである。

### 3.2 数値の出典

| 設定 | 既定値 | 出典 |
|---|---:|---|
| BasePoints | 60 | 要求定義§5.1、アプリの初期definition |
| SpecialPoints | 0 | 要求定義§5.1、アプリの初期definition |
| SimilarityPenaltyWeight | 0.1 | 要求定義§5.1、アプリの初期definition |
| RoundingDigits | 1 | 要求定義§5.1、アプリの初期definition |
| Question Points | 8 × 5 | 要求定義§6.2の`(100-60-0)/5`と現行mapping |

**これらの数値はExcel cellから抽出した値ではない。** Excelから抽出した5つのheader候補へ、要求とアプリの初期配分規則を適用した既定値である。

## 4. 使い方

1. 実行したい`ST-UC-01`〜`ST-UC-25`を1件選ぶ。
2. 直下の`text`コードブロック全体を1個だけコピーする。
3. コマンド実行可能なAI agentまたはテスト担当者へ渡す。
4. 各Promptはrepository root、fixture path、evidence directoryを実行時に解決する。原則として置換は不要。
5. Prompt内の報告順、privacy規則、status語彙を変更しない。
6. 複数Promptの一部だけを連結して1件のPASSにしない。
7. 任意testのopt-in、login、external spreadsheetはPromptが明示した場合だけ使用する。

## 5. 共通実行規則

各scenario Promptは次を自己完結的に内包する。

- `StudyReportEvaluator.slnx`を現在directoryから上位探索し、repository rootを確定する。
- Windows 11 x64、PowerShell 7+ Core、`global.json`が選択する.NET SDKを確認する。
- PowerShell処理は`pwsh.exe -NoLogo -NoProfile`だけを使い、Windows PowerShell 5.1へfallbackしない。
- 既存のsource変更をstash、reset、clean、commitしない。
- 長時間testを無出力だけを理由に重複起動しない。
- evidenceはrepository外の一意な一時directoryへ保存し、commitしない。
- 実行前後で対象inputのSHA-256、size、last-write UTCを比較する。
- workbookはread-onlyで読み、元本を変更、再保存、rename、移動、削除しない。
- cell本文、学生情報、Prompt、reference、reason、evidence、credential、token、private absolute pathをterminal、JSON、Markdown、chatへ出力しない。
- test名、技術code、件数、列記号、dimension、hash、basenameは記録できる。
- failure時に期待値を緩めず、初回結果と再実行結果を時系列で残す。
- 実行していないmanual UI、Live AI、external recalculation、package、platformをPASSにしない。
- test certificateまたはunsigned artifactの成功は`PASS_MECHANISM`、production trustとclean target OSの成功だけを`PASS_PRODUCTION`とする。
- macOS vendor support tier、cross-publish、Rosettaを本製品のnative production evidenceへ代用しない。

## 6. Status語彙

| Status | 使用条件 |
|---|---|
| `PASS` | Promptに書かれた必須確認を全て今回実測し、期待と一致 |
| `FAIL` | 実行できたが1件以上が期待と不一致 |
| `BLOCKED` | 必須fixture、tool、permission、対応環境等がなく開始不能 |
| `NOT_RUN` | 任意testを意図的に実行していない |
| `NOT_RUN_UI_AUTOMATION_UNAVAILABLE` | user-visible UIを観測する手段がない |
| `NOT_RUN_POLICY_REAL_DATA` | 実データのproduction AI送信をpolicyにより実施しない固定status |
| `SKIPPED_NOT_AUTHENTICATED` | optional synthetic Live Copilotだけ、既存loginがない |
| `FAILED_ADVISORY` | optional testを実行したが期待と不一致。required gateへ影響させない |
| `SKIPPED_NOT_INSTALLED` | optional external spreadsheetが未install |
| `NOT_RUN_EXTERNAL_PREREQUISITE` | external recalculationの別の任意前提がない |
| `PASS_MECHANISM` | test certificateまたはunsigned artifactでpackage mechanismだけを今回確認 |
| `PASS_PRODUCTION` | production identityとclean target OSでrequired trust/journeyを今回確認 |
| `BLOCKED_EXTERNAL` | signing identity、credential、native test host等がなくproduction scopeを開始不能 |

## 7. 共通報告契約

各scenarioは、Prompt内により厳密な形式がなければ次の順で報告する。

1. Test ID、External run ID、UTC開始／終了
2. 対象commit、開始時worktree status hash
3. OS／architecture、PowerShell、.NET
4. 前提とfixture identity
5. 実施内容と実行したtest名
6. 期待結果
7. 今回の実測結果
8. 本文を含まない証跡pathとSHA-256
9. scope別Status
10. 差異、再現手順、影響
11. 確認済み範囲、未確認範囲、非保証事項

## 8. Scenario一覧

| Scenario | 内容 | Test Requirement / Acceptance Criteria |
|---|---|---|
| ST-UC-01 | Forms/Google row 1/2、10人fixture identity・mapping | TR-01 / AC-001、AC-002 |
| ST-UC-02 | Canonical SampleReport identity・role候補 | TR-02 / AC-003 |
| ST-UC-03 | picker、direct path、unsupported形式 | TR-03 / AC-001、AC-002 |
| ST-UC-04 | 配点、均等化、range、validation | TR-04、TR-05 / AC-004〜AC-006、AC-008 |
| ST-UC-05 | Knowledge／Custom／Special Promptとclosed schema | TR-06 / AC-007、AC-008 |
| ST-UC-06 | reference／normal／special／similarity境界 | TR-07、TR-08 / AC-009、AC-010、AC-012 |
| ST-UC-07 | score hand oracleとConfig参照formula | TR-09、TR-10 / AC-006、AC-011、AC-012 |
| ST-UC-08 | final workbook、output path、atomic commit | TR-11、TR-12 / AC-013 |
| ST-UC-09 | checkpoint、fault、resume mismatch | TR-13、TR-14 / AC-014、AC-015 |
| ST-UC-10 | auth／timeout／network／schema／cleanup／cancel | TR-15 / AC-012、AC-016 |
| ST-UC-11 | same-row privacy、literal、no-content log | TR-16 / AC-019 |
| ST-UC-12 | 4-step UI、warning、progress、accessibility | TR-17 / AC-016、AC-017 |
| ST-UC-13 | command-line Prompt launch | TR-18 / AC-018 |
| ST-UC-14 | Windows publish、bundled CLI、clean launch | TR-19 / AC-020 |
| ST-UC-15 | ZIP integrity、sidecar、再現性、tamper | TR-20 / AC-020 |
| ST-UC-16 | Windows-only claim、docs、screenshots | TR-21、TR-22 / AC-021、AC-022 |
| ST-UC-17 | 10人fixture new→interrupt→resume E2E | 補助scenario / AC-009〜AC-016、AC-019 |
| ST-UC-18 | fixed-seed 531-row synthetic E2E | TR-23 / AC-009〜AC-016、AC-019 |
| ST-UC-19 | Canonical SampleReport no-network technical E2E | 旧実データE2E / AC-009〜AC-016、AC-019 |
| ST-UC-20 | optional authenticated synthetic Copilot | TR-24A |
| ST-UC-21 | optional external spreadsheet recalculation | TR-24B |
| ST-UC-22 | Windows MSIX mechanism、production trust、install lifecycle | TR-25 / AC-023、AC-026、AC-027 |
| ST-UC-23 | macOS RID別publish、`.app`／DMG structure | TR-26 / AC-024、AC-026 |
| ST-UC-24 | macOS Developer ID、notary、staple、quarantine | TR-27 / AC-025 |
| ST-UC-25 | package-installed application E2E、release matrix、secret boundary | TR-28、TR-29 / AC-027、AC-028 |

## 9. Scenario Prompt

以降の各コードブロックは単独でコピーして実行できる。

### ST-UC-01: Forms／Google question row 1/2と10人fixture mapping

```text
あなたはStudyReport Evaluator v4.2のシステムテスト担当者です。
Test ID: ST-UC-01
Requirement: TR-01 / AC-001 / AC-002

このPromptだけで最後まで実行してください。捏造禁止。未実施、過去結果、文書記載値を今回のPASSにしません。repository source、fixture、既存変更を修正、stash、reset、clean、commitしません。学生回答、header本文、Prompt、reason、evidence、credential、token、private absolute pathをterminal、report、chatへ出力しません。Windowsの処理はpwsh.exe -NoLogo -NoProfile、PowerShell 7+ Coreだけを使います。同じ長時間commandを重複起動しません。

実行準備:
1. 現在directoryから上位へ`StudyReportEvaluator.slnx`を探索してrepository rootを確定し、そこへ移動します。見つからなければBLOCKEDです。
2. Windows 11 x64、PowerShell Core 7+、`dotnet --version`が`global.json`の選択結果であることを確認します。
3. UTC開始時刻、HEAD、`git status --porcelain=v1 --untracked-files=all`のUTF-8 SHA-256と件数を記録します。status本文はreportへ転載しません。
4. repository外に一意なevidence directoryを作ります。
5. `dotnet restore .\StudyReportEvaluator.slnx --locked-mode`を1回実行します。

対象fixture:
- `tests/fixtures/system-test/SystemTest-10Students.xlsx`
- expected bytes: 7,652
- expected SHA-256: 7F473879B3C43319D842881C5DFD1D01B2A60091A5B0C7B39B70B8D574D801C8
- expected worksheet/dimension: SystemTestInput / A1:L11
- header row 1、data rows 2〜11

実行:
1. fixtureのSHA-256、size、last-write UTCを取得します。
2. 次のtest classをReleaseで実行します。test名と今回のpassed/failed/skippedだけを記録し、TRXはrepository外へ保存します。
	- `StudyReportEvaluator.App.Tests.E2E.TenPersonSystemSmokeTests.Fixture_has_fixed_identity_structure_and_literal_formula_canaries`
	- `WorkbookMetadataReaderTests`
	- `ColumnMappingSuggesterTests`
	- `ColumnMappingValidatorTests`
	完全修飾method名はその1件だけ、末尾が`Tests`のclass名は当該classでdiscoverされた全testを意味します。別classへ拡張しません。
3. 既存test factoryによるMicrosoft Forms型とGoogle Forms型について、question text row 1と2を個別に検証します。実在データを新規作成しません。
4. fixtureをproduction read-only loaderへ通し、初期definitionを本文非表示で検査します。
5. 実行後にfixture identityを再取得します。

必須oracle:
- 標準`.xlsx`としてread-only loadできる。
- question text rowは1または2だけを選択でき、first data rowはその後である。
- timestamp、email、氏名等の管理列は初期primary候補にならない。
- header semanticからnormal answer、student Prompt、Prompt considerations候補を作る。
- 10人fixtureではprimary F/G/H/I/J、Jのsupporting K、A〜E/Lは初期対象外となる。
- candidateは利用者が変更でき、列位置だけで固定mappingしない。
- fixtureにはformula、macro、external link、DDE、defined nameがなく、`= + - @` canaryはliteral stringである。
- F〜Jはそれぞれ空または空白primaryがexactly 1件ある。
- 読込前後のSHA-256、size、last-write UTCが一致する。

UIで未観測の内部候補はmetadata/mapping testの結果として報告し、画面で確認したと書きません。全必須oracleが今回一致した場合だけPASS、実行できた不一致はFAIL、環境・fixture不足はBLOCKEDです。

報告順: Test ID / External run ID・UTC開始終了 / commit・status hash・OS・architecture・PowerShell・.NET / fixture identity / 実行test / 期待 / 実測 / identity不変 / 非機密証跡 / Status / 差異と再現手順 / 未確認範囲。
```

### ST-UC-02: Canonical SampleReport identityとF〜K role候補

```text
あなたはStudyReport Evaluator v4.2のcanonical repository sampleシステムテスト担当者です。
Test ID: ST-UC-02
Requirement: TR-02 / AC-003 / docs/requirements-definition.md §4.4

捏造禁止。`sample/SampleReport.xlsx`が存在しない場合、10人fixtureで置換せずBLOCKEDと報告して終了します。期待値に合わせてfileを作成・変更しません。sampleの回答cell、氏名、email、Prompt本文、header本文を読み出し・表示・AI送信しません。構造metadata、列記号、file identityだけを扱います。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使います。sourceや既存変更をstash、reset、clean、commitしません。

準備:
1. `StudyReportEvaluator.slnx`を上位探索してrepository rootへ移動します。
2. Windows 11 x64、x64 process、PowerShell Core 7+、global.json選択SDKを確認します。
3. UTC開始、HEAD、worktree statusの件数とUTF-8 SHA-256を記録します。
4. `sample/SampleReport.xlsx`の存在を確認します。なければStatus=BLOCKED、code=`BLOCKED_CANONICAL_SAMPLE_MISSING`とし、存在すると偽りません。
5. repository外にevidence directoryを作ります。

存在する場合の期待identity:
- bytes: 470,806
- SHA-256: 73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA
- package entries: 11
- relationships: 8
- worksheet/dimension: 1件（nameはSHA-256だけを記録） / A1:L531
- initial source: single worksheet、question row 1、data rows 2〜531

期待role候補:
- A〜E/L: 初期対象外
- F/I: primary候補
- G/J: primary + student Prompt候補
- H: primary + supporting候補
- K: supporting候補、Jのinitial supporting候補
- F〜Kはheader semanticから得た候補であり固定mappingではない。

実行:
1. 検査前のSHA-256、size、last-write UTCを取得します。
2. productionの`FileFormatClassifier`、`WorkbookMetadataReader`、`ColumnMappingSuggester`をread-only経路で検証する既存testをReleaseで実行します。必要なlocked restoreは1回だけ行います。
3. package entry、relationship、sheet、dimension、row、候補roleを本文非表示で検査します。
4. 検査後のidentityを取得し、前後比較します。

判定:
- file不足はBLOCKEDでありFAILやPASSへ変換しない。
- fileが存在し、identityまたは構造が1項目でも異なる場合はFAIL。
- 全期待値と元本不変を今回確認した場合だけPASS。

報告順: Test ID / UTC開始終了 / commit・環境 / file存在 / identity / package構造 / role候補 / input不変 / test実測 / 非機密証跡 / Status / 差異。sample本文とprivate pathは掲載しません。
```

### ST-UC-03: Native picker、direct path、unsupported input

```text
あなたはStudyReport Evaluator v4.2のinput workflowシステムテスト担当者です。
Test ID: ST-UC-03
Requirement: TR-03 / AC-001 / AC-002

捏造禁止。実在データを使わず、既存test factoryが一時directoryへ作るsynthetic fixtureだけを使います。file本文、header本文、private pathをerrorやreportへ含めません。repository fileを変更、stash、reset、clean、commitしません。PowerShell処理はpwsh.exe -NoLogo -NoProfile、PowerShell 7+ Coreだけを使います。

準備:
1. `StudyReportEvaluator.slnx`を上位探索しrepository rootへ移動します。
2. Windows 11 x64、PowerShell Core 7+、global.json選択SDKを確認し、HEAD、UTC開始、worktree status hashを記録します。
3. repository外にevidence directoryを作り、locked restoreを1回行います。
4. `FileFormatClassifierTests`、`InputViewTests`、`MainWindowTests`、picker service testsをReleaseで実行します。既存の対象testがない確認項目はmanual UIまたはheadless deterministic testとして区別します。

単一原因case:
1. native picker cancel: 既存Input stateとpathが変わらず、buttonを再利用できる。
2. native picker select: 1件の`.xlsx`だけを同じread-only loaderへ渡す。
3. pickerがlocal pathを提供しない: cancelと区別したsafe案内を表示し、direct path入力を維持する。
4. direct path: 日本語と空白を含むabsolute pathを読める。
5. `.csv`、`.xls`、`.xlsb`、`.xlsm`、PDF、encrypted/protected、corrupt ZIP、external relationship等のunsafe packageを個別に拒否する。
6. file path編集時に旧metadata、snapshot、sheet、mappingをclearする。
7. picker filterをsecurity validationの代替にせず、direct pathも同じclassifierへ通す。

期待:
- unsupported fileはloaded stateにならない。
- errorはsafe technical codeだけを示し、pathや内容をechoしない。
- cancel caseはvalidation errorとして扱わない。
- 各caseは前caseのstateを戻してから実行し、原因を混在させない。
- fixtureの検査前後identityは一致する。

全caseを今回確認した場合だけPASS。不一致はFAIL。Desktop pickerを観測できず、対応headless testもない項目があれば、その項目をNOT_RUNとしてscenario全体をPASSにしません。必須tool不足はBLOCKEDです。

報告順: Test ID / 環境 / case別実施方法（manual/headless） / expected code・state / 実測 / input不変 / 証跡 / Status / 差異と再現手順。
```

### ST-UC-04: 絶対配点、均等化、range、run前validation

```text
あなたはStudyReport Evaluator v4.2の採点設計システムテスト担当者です。
Test ID: ST-UC-04
Requirement: TR-04 / TR-05 / AC-004 / AC-005 / AC-006 / AC-008

捏造禁止。決定的なCore/UI testだけを使い、Live AI scoreを使いません。入力workbookや既存成果物を変更しません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使用します。各invalid caseは単一原因にし、前caseの値をvalidへ戻してから次へ進みます。

準備:
1. repository rootを`StudyReportEvaluator.slnx`から上位探索します。
2. Windows 11 x64、PowerShell Core 7+、global.json選択SDK、HEAD、UTC開始、worktree status hashを記録します。
3. repository外にevidence directoryを作り、locked restoreを1回行います。
4. `ScoringAllocationCalculator`、`QuantificationDefinitionValidator`、`QuantificationDesignViewModel`、Design view testsに対応するCore/App testをReleaseで実行します。

既定値の出典:
- BasePoints=60、SpecialPoints=0、SimilarityPenaltyWeight=0.1、RoundingDigits=1は要求定義§5.1とアプリ初期definition。
- Excel cellに埋め込まれた値ではない。
- 添付header由来5問ではQuestion Pointsは各8で、60+0+5×8=100。

配分oracle:
- enabled question 2件、Base 60、Special 0: 20、20。
- enabled question 3件: 13.333333、13.333333、13.333334。6 decimal placesで最後のenabled questionへ差分を入れ、合計exact 100。
- question追加、Base/Special編集、enable/disableだけでは既存の手動Pointsを自動変更しない。
- 「設問配点を均等化」を明示実行した時だけenabled questionへ残点を再配分する。
- disabled questionのPointsは均等化で変更しない。
- snapshot作成後のdraft変更が既存snapshotを変更しない。

validation matrix:
- BasePointsとSpecialPointsは0〜100。
- SimilarityPenaltyWeightは0〜1。
- enabled Question Pointsは0以上の有限decimal。
- `Base + Special + Σ enabled Question Points`はdecimal exact 100。99.999999と100.000001を許容しない。
- SpecialPoints>0ならenabled special itemが1件以上必要。
- enabled evaluator/criterion weightは正数。
- minimum < maximum。
- rounding digitsは0〜6。
- question text rowは1または2。
- invalid中はsnapshot作成とrun開始をblockする。

期待code:
`BASE_POINTS_OUT_OF_RANGE`、`SPECIAL_POINTS_OUT_OF_RANGE`、`SIMILARITY_WEIGHT_OUT_OF_RANGE`、`QUESTION_POINTS_OUT_OF_RANGE`、`ALLOCATION_TOTAL_INVALID`、`SPECIAL_ITEMS_REQUIRED`。実装が追加の具体codeを返す場合は実測名を記録し、期待へ書換えません。

全配分・保持・snapshot・validation caseが今回一致した場合だけPASS。不一致はFAIL、実行不能はBLOCKEDです。

報告順: Test ID / 環境 / default出典 / case / deterministic oracle / expected code・field / 実測 / test件数 / 証跡 / Status / 差異。
```

### ST-UC-05: Knowledge／Custom／Special Promptとclosed tool schema

```text
あなたはStudyReport Evaluator v4.2のPrompt・structured resultシステムテスト担当者です。
Test ID: ST-UC-05
Requirement: TR-06 / AC-007 / AC-008

捏造禁止。合成Promptとfake transportだけを使い、Prompt本文をlog、error artifact、reportへ出しません。Live AIを呼びません。repositoryを変更、stash、reset、clean、commitしません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使います。

準備:
1. `StudyReportEvaluator.slnx`を上位探索してrepository rootへ移動します。
2. Windows 11 x64、PowerShell Core 7+、global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. repository外にevidence directoryを作り、locked restore後、Prompt renderer、definition validator、schema factory、tool collector、runnerのCore/App testをReleaseで実行します。

必須case:
1. Knowledge: app-owned semantic templateはread-onlyで、知識の「説明・関係・適用」を扱い、keyword存在だけで満点にしない。利用者templateへ置換できない。
2. Custom: `{回答}`と`{評価項目}`が必須。`{設問}`、`{補助情報}`、`{最小点}`、`{最大点}`は任意。
3. Special: `{回答}`が必須で、`{評価項目}`は必須ではない。
4. `{{`と`}}`はliteral brace。挿入値はsingle-passであり再展開しない。
5. unknown、unclosed、nested、unmatched braceをrun前に拒否する。
6. 6 placeholder制約は利用者編集可能なCustom／Specialだけに適用する。app-owned Referenceは固定`{設問}`だけを限定置換し、Similarityは固定sectionへ値を直接組み立てる。`{参照回答}`を第7placeholderとして許可しない。
7. 公開toolはoperationごとにexactly 1件: normal=`submit_quantification`、reference=`submit_reference_answer`、special=`submit_special_quantification`、similarity=`submit_similarity`。
8. schemaは`additionalProperties=false`、required IDはexact enum、normal raw rangeとspecial/similarity 0〜1を閉じる。
9. duplicate tool call、missing result、unknown ID、partial resultをoperation全体で拒否する。
10. invalid Promptではinput row read、Copilot session作成、runner callが全て0件である。
11. AIはcriterion raw／special score／similarityだけを返し、配点、最終点、合否を返さない。

各operationとinvalid原因を別fixtureで実行し、call/session/tool countはtest doubleから取得します。期待に合わせて証跡を書換えません。

全必須caseが今回一致した場合だけPASS。不一致はFAIL、必要test/tool不足はBLOCKEDです。

報告順: Test ID / 環境 / operation / case / expected schema・code・count / 実測 / Prompt本文非記録確認 / 証跡 / Status / 差異。
```

### ST-UC-06: Reference-firstとnormal／special／similarity境界

```text
あなたはStudyReport Evaluator v4.2の4 AI operationシステムテスト担当者です。
Test ID: ST-UC-06
Requirement: TR-07 / TR-08 / AC-009 / AC-010 / AC-012

捏造禁止。fake transportと制御fixtureだけを使い、repository sampleや実データをAIへ送りません。reference、回答、Prompt、reason、evidence本文を証跡へ記録しません。Live AI期待値を使いません。各operationとfailureを単一原因で検証します。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使います。

準備:
1. repository rootを`StudyReportEvaluator.slnx`から上位探索します。
2. Windows 11 x64、PowerShell Core 7+、global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. repository外へevidence directoryを作り、locked restore後、reference runner、special runner、similarity runner、durable orchestrator、result validatorのCore/App testsをReleaseで実行します。

Reference-first／resume:
- enabled question 2件のrunで、student rowを読む前にreference phaseを実行する。
- 1 question/runにつきreference operationはexactly 1回。
- reference/similarity model IDはliteral `auto`で、別modelへfallbackしない。
- 各reference完了後にpartial checkpointを更新する。
- SUCCESSは非空answerとexact QuestionIdを持つ。
- invalid/timeout/network/auth failureはanswer blankと技術statusを保存する。
- resumeでは保存済みreferenceを再生成せず、未生成questionだけを続行する。
- final References sheetへquestion、answer、`auto`、status、UTC timestampを保存する。

数値境界:
- normal rawはcriterion effective rangeのminimum/maximumを受理し、範囲外、NaN、Infinityをrejectする。
- special scoreとsimilarityは0と1を受理し、負数、1超、NaN、Infinityをrejectする。
- unknown ID、duplicate、partial resultをrejectする。

empty-zero:
- 空normal primaryはAI call 0、QuestionRate=0、QuestionEarned=0。
- 空special primaryはAI call 0、special score=0。
- 空student answerはsimilarity call 0、similarity=0、penalty=0。
- supportingだけが非空でもprimaryが空なら送信しない。
- SpecialPoints=0ではspecial runner call 0、status=`NOT_RUN_ZERO_BUDGET`、SpecialEarned=0。これは要求定義§8.2の「固有評価AIを実行しない」を表す既存実装`ResultsStatusCodes.NotRunZeroBudget`の技術statusであり、新しい要求や利用者設定ではない。

technical-failure-blank:
- nonempty入力のschema/timeout/network/auth/cleanup failureはraw blankで、0へ変換しない。
- reference failure時は当該similarityを送信せずblankにする。
- technical blankはQuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScoreへblank伝播する。
- empty-zeroとtechnical-blankを同じtest caseへ混在させない。

attempt/session/call countとcheckpoint順はtest double/store記録から取得します。各cellのtypeとcached previewも確認します。全必須case一致だけをPASSとし、不一致はFAIL、前提不足はBLOCKEDです。

報告順: Test ID / environment / operation / case / expected model・range・status・call count / checkpoint順 / 実測 / cell type・cache / 非機密証跡 / Status / 差異。
```

### ST-UC-07: Score hand oracleとConfig参照formula

```text
あなたはStudyReport Evaluator v4.2のscore・Excel formulaシステムテスト担当者です。
Test ID: ST-UC-07
Requirement: TR-09 / TR-10 / AC-006 / AC-011 / AC-012

捏造禁止。次の固定合成oracleだけを使い、Live AI scoreや過去workbookの値を期待値へ流用しません。Excel結果だけを正本にせず、独立手計算、Core計算、formula cached preview、read-only reopenを比較します。repository fileを変更、stash、reset、clean、commitしません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使用します。cell本文とprivate pathを証跡へ出しません。

準備:
1. `StudyReportEvaluator.slnx`を上位探索してrepository rootへ移動します。
2. Windows 11 x64、PowerShell Core 7+、global.json選択SDK、HEAD、UTC開始、worktree status hashを記録します。
3. repository外にevidence directoryを作り、locked restoreを1回行います。
4. `WeightedScoreCalculatorTests`、`FormulaSerializerTests`、`FormulaPreflightValidatorTests`、`FormulaCellWriterTests`、`ConfigAndRunSheetWriterTests`、`ResultsSheetWriterTests`、`OutputPackageValidatorTests`をReleaseで実行します。末尾が`Tests`の指定はそのclassでdiscoverされた全testを意味します。
5. 固定oracleのCore比較は`WeightedScoreCalculatorTests.V4_system_prompt_hand_oracle_produces_86_point_8`の実測を必須とします。別入力の87.86 oracle testを代用しません。

固定oracle:
- BasePoints=60、SpecialPoints=10、SimilarityPenaltyWeight=0.1、RoundingDigits=1。
- Q1: Points=20、normalized=80、similarity=0.5、special question rate=0.8。
- Q2: Points=10、normalized=50、similarity=0.2、special question rate=0.6。

独立期待値:
- Q1 QuestionRate=0.8、QuestionEarned=16.0、SimilarityPenalty=1.0。
- Q2 QuestionRate=0.5、QuestionEarned=5.0、SimilarityPenalty=0.2。
- SpecialEarned=10×average(0.8,0.6)=7.0。
- FinalRaw=60+16+5+7-1-0.2=86.8。
- FinalScore=86.8。
- FinalRaw<0ならFinalScore=0、FinalRaw>100ならFinalScore=100。
- midpoint roundingはAwayFromZero。各段階でroundした値を次段へ使う。

formula surface:
- Quantification_ConfigのAllocationTotalとAllocationValidだけがapp-owned formula。他のConfig cellはliteral。
- Quantification_ResultsのEffectiveRaw、Normalized、Evaluator、QuestionRate、QuestionEarned、SpecialQuestionRate、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScoreをclosed ASTから生成する。
- points、range、rounding、similarity weightをformulaへliteral埋込みせず、実際のConfig sheet cellへabsolute referenceする。
- formula storage textはleading `=`を持たない。
- allowlist外function、raw user formula、external workbook、DDE、defined-name injectionをrejectする。
- target重複、unknown reference、cycleをrejectする。
- Excel上限をwrite前に検査する: formula length 8191、function arguments 255、column 16384、cell 32767 characters。
- expected formula planはConfigとResults双方をexactly once含む。
- AllocationValid≠1または必要なtechnical rawがblankならFinalRaw/FinalScoreはformula付きblank cacheとなる。
- app-owned formula以外のtextはInlineStringでCellFormulaを持たない。

検証:
1. 手計算を式と途中値付きで独立に実施します。
2. Core calculatorの各段階を手計算と比較します。
3. synthetic workbookへformula planを書き、cached previewをCore値と比較します。
4. fileをcloseしread-only reopenして、formula text、Config reference、cached type/value、OpenXmlValidator error countを検査します。
5. hostile formula caseは専用一時fixtureで単一原因ごとに行います。

全oracle、preflight、writer、reopen結果が今回一致し、Open XML error 0の場合だけPASS。不一致はFAIL、必要tool不足はBLOCKEDです。

報告順: Test ID / 環境 / 固定oracle / 手計算 / Core / formula plan / workbook cache / hostile case別code / Open XML validation / 証跡 / Status / 差異。
```

### ST-UC-08: Final workbook、output path、no-overwrite atomic commit

```text
あなたはStudyReport Evaluator v4.2のfinal workbook・output commitシステムテスト担当者です。
Test ID: ST-UC-08
Requirement: TR-11 / TR-12 / AC-013

捏造禁止。一時synthetic workbookだけをwrite対象とし、canonical sampleと10人fixtureを変更しません。既存利用者fileを削除・上書きしません。cell本文とprivate absolute pathは報告しません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使い、repository変更をstash、reset、clean、commitしません。

準備:
1. `StudyReportEvaluator.slnx`を上位探索しrepository rootへ移動します。
2. Windows 11 x64、PowerShell Core 7+、global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. repository外に一意なtest/evidence directoryを作り、locked restoreを1回行います。
4. `ConfigAndRunSheetWriterTests`、`ReferenceAnswersSheetWriterTests`、`ResultsSheetWriterTests`、`OutputPathPlannerTests`、`AtomicOutputCommitterFaultTests`、`OutputPackageValidatorTests`、`WorkingPackageTests`をReleaseで実行します。

final workbook oracle:
- inputの全sheetが同じ順序、名前、part identityで保持される。
- app-owned sheetは`Quantification_Config`、`Quantification_References`、`Quantification_Results`、`Quantification_Run`のexact 4件。
- finalに`Quantification_Checkpoint`はない。
- ReferencesとRunはformula 0。
- Config formulaはAllocationTotal/AllocationValidの2件だけ。
- Results formulaはclosed expected planとexact一致する。
- untrusted textは先頭が`= + - @`でもInlineStringでありCellFormulaを持たない。
- inputに同名または大小文字違いのsheetがある場合、既存sheetを変更せず` (2)`、` (3)`の最小suffixを使う。
- Run sheetへ実際に解決した4 sheet namesを記録する。
- OpenXmlValidator error 0。

output path oracle（local minute 2026-09-02 14:35固定）:
- default directoryはinputの隣の`result`で、なければ作成する。
- first pairは`eval-20260902-1435.xlsx`と`eval-20260902-1435.partial.xlsx`。
- finalまたはpartialのどちらかが存在すれば、次は共通suffix`-02`。
- `-02`のどちらかが存在すれば`-03`。
- fileだけでなく同名directoryもoccupiedとして扱う。
- reserve後に第三者がfinalを作ったraceでは上書きせず`TARGET_EXISTS`。

atomic final oracle:
- target-local working copyを作成し、flush、close、read-only reopen、formula plan/Open XML validationを行う。
- input identity再確認後、same-volume no-overwrite moveでfinalを作る。
- move前のwrite、flush、validation、cancel、input drift、output invalid faultではfinalを作らず、既存targetを変えない。
- move後例外でもfinal存在・temp不存在・validを確認できた場合だけSUCCESS。
- test終了時にapp-owned tempをcleanupし、今回作ったtest directoryだけを削除する。

各faultは単一原因で実行し、input、既存target、final、partialのSHA-256と存在状態を前後比較します。pathはreportではbasenameだけを使用できます。

全caseが今回一致し、inputと既存targetが不変の場合だけPASS。不一致はFAIL、環境・tool不足はBLOCKEDです。

報告順: Test ID / 環境 / input identity / case / output basename・存在状態 / sheet set / formula counts / validation / atomic fault結果 / cleanup / Status / 差異。
```

### ST-UC-09: Checkpoint、atomic fault、resume mismatch

```text
あなたはStudyReport Evaluator v4.2のdurable checkpoint・resumeシステムテスト担当者です。
Test ID: ST-UC-09
Requirement: TR-13 / TR-14 / AC-014 / AC-015

捏造禁止。一時synthetic inputだけを使い、checkpoint payload、回答、reference、Prompt、reason、evidence本文をterminal、report、chatへ出しません。mismatchごとにcheckpoint copyを使い、正本partialを変更しません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使います。repositoryを変更、stash、reset、clean、commitしません。

準備:
1. `StudyReportEvaluator.slnx`を上位探索しrepository rootへ移動します。
2. Windows 11 x64、PowerShell Core 7+、global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. repository外に一意なtest/evidence directoryを作り、locked restoreを1回行います。
4. `CheckpointStoreTests`と`DurableQuantificationOrchestratorTests`をReleaseで実行します。

partial create／payload oracle:
- run開始時にinput byte-copyへ`Quantification_Checkpoint`だけを追加した`.partial.xlsx`をCreateNewする。
- inputに同名sheetが大小文字違いで存在する場合は作成を拒否する。
- METAはSchemaVersion、PayloadSha256、ChunkCountだけを持つ。
- PAYLOADはcanonical UTF-8 JSONを各30,000 characters以下、0-based連続indexで保存する。
- normal、special、similarity、token usageを1つのCheckpointCompletedRowとしてround-tripする。
- unknown row/column/property、duplicate key/chunk、missing chunk、index gap、hash mismatch、unsupported schemaを個別に拒否する。

atomic update oracle:
- reference完了およびcomplete student rowごとにtarget-local tempを作り、reopen validation後にFile.Replaceする。
- replace前のwrite、flush、validation、cancel、replace faultで旧partial hashが変わらない。
- replace後例外はnew payloadが実在・validな場合だけSUCCESS。
- tempはresume sourceにならず、通常fault後にcleanupされる。
- input SHA-256、size、mtime drift時は更新しない。

resume mismatch matrix（各copyへ単一mutation）:
- checkpoint schema
- input path、SHA-256、size、last-write UTC
- definition canonical JSON／SHA-256
- normal model ID
- reference/similarity model ID `auto`
- app major compatibility
- CLI version／SHA-256
- SDK version
- preserved source worksheet identity
- completed row number、order、expected IDs
- accepted normal range／evidence source
- special/similarity IDと0〜1 range

resume期待:
- 各mismatchを具体的safe codeで拒否し、AI call、checkpoint update、final createは0。
- rejected checkpointとinputのhashは変わらない。
- valid resumeは保存済みreferenceを再生成しない。
- complete rowだけskipし、最初の未完了rowから続ける。
- row途中の結果はskipせず、そのrow全体を再実行する。
- 別orchestrator/process相当でpartialをread-only reopenしても同じ動作。
- success後だけpartialを削除する。cleanup failureはvalid finalを無効にせずwarningを返す。

全構造、fault、mismatch、valid resume caseが今回一致した場合だけPASS。不一致はFAIL、前提不足はBLOCKEDです。

報告順: Test ID / 環境 / package structure / metadata dimensions / faultまたはmismatch dimension / expected safe code / AI call・update・final count / hash不変 / resume結果 / cleanup / 証跡 / Status / 差異。
```

### ST-UC-10: Auth／timeout／network／schema／cleanup／cancel

```text
あなたはStudyReport Evaluator v4.2のAI failure・cancelシステムテスト担当者です。
Test ID: ST-UC-10
Requirement: TR-15 / AC-012 / AC-016

捏造禁止。Live障害を人為的に起こさず、fake transportとcontrolled cancellationだけを使います。exception本文、session content、Prompt、response、reason、evidenceを証跡へ出しません。repositoryを変更、stash、reset、clean、commitしません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使います。

準備:
1. `StudyReportEvaluator.slnx`を上位探索してrepository rootへ移動します。
2. Windows 11 x64、PowerShell Core 7+、global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. repository外にevidence directoryを作り、locked restoreを1回行います。
4. retry/cleanup coordinator、normal/reference/special/similarity runner、durable scheduler/orchestrator testsをReleaseで実行します。

normal、reference、special、similarityの各operationで単一原因ごとに検証:
- schema invalidは新sessionで最大1回だけ再試行後、`AI_OUTPUT_INVALID`でblank。
- timeoutとtransient networkは新sessionで最大2回再試行後、`AI_TIMEOUT`／`NETWORK_FAILED`でblank。
- auth failureは無駄に再試行せず`AUTH_REQUIRED`でblank。
- cleanup failureは`CLEANUP_FAILED`とし、同一coordinatorで新規operationをinterlockする。
- start前cancelはattempt 0、session 0、send 0。
- in-flight cancelはabort→dispose/deleteをfinite timeoutで行う。
- cancel受付後、semaphore待機中operationはrunnerへ到達せず、新規AI sendは0。
- cancel後に新規sessionを開始しない。
- 完了済みreference/rowだけcheckpointへ残り、in-progress row全体はdurable completeにならない。
- observer/progress callback例外はrun data、status、checkpoint payloadを変更しない。
- cleanup failure後の追加retryは0。

既定attempt timeoutは120秒だが、testはvirtual/controlled timeoutを使い実時間待機を行いません。attempt、session、send、abort、dispose、delete、checkpoint countをtest doubleから取得します。期待値に合わせてcountを書換えません。

全operation×failure/cancel caseが今回一致した場合だけPASS。不一致はFAIL、test fixture/tool不足はBLOCKEDです。

報告順: Test ID / 環境 / operation kind / failure kind / expected status / attempt・session・send・cleanup count / durable row count / 実測 / 非機密証跡 / Status / 差異。
```

### ST-UC-11: Selected same-row privacy、literal text、no-content log

```text
あなたはStudyReport Evaluator v4.2のprivacy・formula injectionシステムテスト担当者です。
Test ID: ST-UC-11
Requirement: TR-16 / AC-019

捏造禁止。合成canaryだけを使い、実在学生本文を扱いません。canary値自体を最終reportへ掲載せず、source ID、存在／不在、件数だけを記録します。repositoryを変更、stash、reset、clean、commitしません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使います。

準備:
1. `StudyReportEvaluator.slnx`を上位探索してrepository rootへ移動します。
2. Windows 11 x64、PowerShell Core 7+、global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. repository外にevidence directoryを作り、locked restoreを1回行います。
4. Safe payload builder、row source、normal/reference/special/similarity runner、SafeLogger、UntrustedString/Config/References/Results/Checkpoint writer testsをReleaseで実行します。

payload isolation oracle:
- reference: current question textだけ。student row、workbook path、他questionを含めない。
- normal: current rowのselected primary/supporting + definition/schema metadataだけ。
- special: current rowのselected special primary/supportingだけ。
- similarity: current row primary + same question referenceだけ。
- 各payloadへ未選択列、別row、別question、workbook pathの異なるcanaryを置き、rendered Prompt、tool args、logsのいずれにもない。
- evidence sourceはsame-row selected stable source column IDだけを受理する。

literal／formula injection oracle:
- `=`, `+`, `-`, `@`で始まる合成textをConfig、References、Results、Checkpointへ渡す。
- app-owned closed formula以外はInlineStringでCellFormulaを持たない。
- formula allowlist外function、external workbook、DDE、defined name、raw formulaを拒否する。
- error、ToString、logへcanary本文やprivate pathをechoしない。

logging oracle:
- loggerのpublic surfaceにfree-textまたはException parameterがない。
- Prompt、response、reference、student value、reason、evidence、credential、path、token値をapplication logへ出さない。
- 技術code、operation kind、attempt count、elapsed、dimension等の非内容metadataだけを記録できる。
- output/partialは元本と同等以上に機密として扱う旨を確認し、test artifactをrepositoryへcommitしない。

各operationでsent source IDs、forbidden canary absent/present、cell type、formula有無、log resultをtest double/Open XMLから取得します。全必須oracle一致だけをPASS、不一致はFAIL、前提不足はBLOCKEDです。

報告順: Test ID / 環境 / operation / sent source IDs / forbidden canary absent/present（値なし） / logger surface / workbook cell type・formula count / input不変 / 証跡 / Status / 差異。
```

### ST-UC-12: 4-step UI、warning、progress、keyboard、200% scale

```text
あなたはStudyReport Evaluator v4.2のuser-visible UI・accessibilityシステムテスト担当者です。
Test ID: ST-UC-12
Requirement: TR-17 / AC-016 / AC-017

捏造禁止。manual UIで観測していない項目を目視PASSと書きません。headless deterministic testで補完した項目はその方法を明記します。実在回答やprivate pathをscreen captureへ含めません。repositoryを変更、stash、reset、clean、commitしません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使います。

準備:
1. `StudyReportEvaluator.slnx`を上位探索してrepository rootへ移動します。
2. Windows 11 x64、x64 process、PowerShell Core 7+、global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. `tests/fixtures/system-test/SystemTest-10Students.xlsx`のsize 7,652、SHA-256 `7F473879B3C43319D842881C5DFD1D01B2A60091A5B0C7B39B70B8D574D801C8`を確認します。
4. repository外にevidence directoryを作り、locked restore後、`EthicsWarningTests`、`MainWindowTests`、`InputViewTests`、`QuantificationDesignViewTests`、Execution/Results UI testsとaccessibility testsをReleaseで実行します。exact warningのsource/docs同期は`DocumentationContractTests.Ethics_warning_is_exact_in_readme_and_user_guides`でも確認します。
5. Desktop UIを観測できる場合だけRelease apphostを10人fixture付きで起動し、render scaling 200%、keyboard中心で確認します。観測手段がなければmanual scopeを`NOT_RUN_UI_AUTOMATION_UNAVAILABLE`とし、headless scopeと分離します。

全step共通のexact warning:
`生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません`

warning oracle:
- Input、Design、Execution、Resultsの全stepでshell rootのpersistent bannerとして常時表示する。
- non-focusable、non-modalで、checkbox、dismiss、consent、role、score gate、実行blockがない。
- warning操作をsnapshot、run、cancel、resume、formula、finalizationの条件にしない。

control／navigation oracle:
- Input: native picker、direct path、question row 1/2、sheet、row範囲、mappingへTabで到達する。
- Design: Base/Special/Similarity、Question Points、equalize、Knowledge/Custom、special editor、Imported Promptへ到達する。
- Execution: auth、model、`auto` availability、concurrency、新規/resume、output directory/partial path、start/cancelへ到達する。
- Results: row-level QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScore、final/partial path、cleanup warning、output folder操作を表示する。
- completed runはResultsへ自動遷移する。起動しただけでは遷移しない。

progress oracle:
- stageはPreparing、GeneratingReferences、EvaluatingRows、SavingCheckpoint、FinalizingWorkbook、Completed/Cancelling。
- reference、row、operationのcompleted/total、in-flight、final/partial pathを表示する。
- countは単調増加し、Completedのin-flightは0。
- partial failure/cancelとvalid final completionを同じ表示にしない。

accessibility oracle:
- 200%でも縦横scrollで全主要controlへ到達する。
- keyboardだけで主要操作へ移動でき、focus ringが視認できる。
- realized control内のautomation IDが重複しない。
- warningは読み上げ可能だがTab stopではない。
- synthetic content以外をcaptureしない。

apphost起動時に`Copilot 状態を確認`や`定量化を開始`を押す必要があるcaseは10人synthetic fixtureだけで行い、canonical sampleを使いません。Live AI送信はこのscenarioでは行いません。

判定は`Headless deterministic UI`と`Manual user-visible UI at 200%`を別statusで記録します。overall PASSには両scopeの実測PASSが必要です。headlessがPASSでもmanual観測手段がない場合、manualとoverallを`NOT_RUN_UI_AUTOMATION_UNAVAILABLE`とし、PASSやBLOCKEDへ読み替えません。不一致はFAIL、required build/test自体を開始できない環境不足はBLOCKEDです。

報告順: Test ID / 環境・scale / scope（manual/headless） / step / control・automation ID / 期待 / 実測 / warning exact match / capture provenance / scope別Status / 差異。
```

### ST-UC-13: `--input`／`--prompt`、explicit apply、no-auto-run

```text
あなたはStudyReport Evaluator v4.2のcommand-line Prompt launchシステムテスト担当者です。
Test ID: ST-UC-13
Requirement: TR-18 / AC-018

捏造禁止。合成`.txt`と10人synthetic XLSXだけを使い、Prompt本文をerror、log、reportへ出しません。アプリを起動しただけでAIを実行しません。repositoryを変更、stash、reset、clean、commitしません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使います。

準備:
1. `StudyReportEvaluator.slnx`を上位探索しrepository rootへ移動します。
2. Windows 11 x64、PowerShell Core 7+、global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. repository外にevidence directoryと、UTF-8 BOMあり／なし、invalid UTF-8、空、valid上限32,767 UTF-16 code units、invalid上限超過32,768 UTF-16 code units、複数Promptの一時`.txt` fixtureを作ります。本文はreportへ記録しません。
4. locked restore後、`LaunchOptionsTests`（PromptFileLoaderとstartup compositionのcaseを含む）とDesign imported Prompt UI testsをReleaseで実行します。存在しない別名のPromptFileLoader test classを推測しません。

parser oracle:
- option名はordinal-ignore-caseの`--input` 0/1回と`--prompt` 0回以上だけ。
- `--input`／`--INPUT`／`--Input`と`--prompt`／`--PROMPT`／`--Prompt`が同じoptionとして動作する。
- relative pathはstartup current directory基準でabsolute化する。
- Prompt指定順を維持し、canonical duplicate pathは最初の1件だけ残す。
- unknown option、missing value、2回目以降の`--input`（値が同じでも異なっても）、non-`.txt` Promptをsafe startup errorにする。
- custom URI、daemon、background server、filename暗黙mappingを追加しない。

Prompt loader oracle:
- UTF-8 BOMあり／なしを受理する。
- invalid UTF-8、0文字、32,768 UTF-16 code units以上、read failureを拒否する。
- basenameと本文をImported Promptsへ指定順に表示する。
- safe error windowへoption value、Prompt本文、private pathを表示しない。

explicit apply oracle:
- Promptを一覧で選択しただけではdraftを変更しない。
- selected Custom evaluatorまたはspecial itemをtargetにして「Promptを適用」した時だけtemplateをcopyする。
- 同じPromptを複数targetへ再利用できる。
- filename規則でquestionへ暗黙割当しない。
- applyはstep遷移やAI処理を開始しない。
- startup後のCopilot runner/session/send countは0。利用者がExecutionのstartを明示するまで0のまま。

各args caseは別process／startup stateで実行し、前caseのstateを引き継ぎません。10人fixtureのidentityを起動前後で比較し、result/final/partialが作られていないことも確認します。

全caseが今回一致した場合だけPASS。不一致はFAIL、tool不足はBLOCKEDです。

報告順: Test ID / 環境 / args case（値は秘匿） / parser result / Prompt loader result / UI prefill・apply対象 / AI runner・session・send count / input・result state不変 / 証跡 / Status / 差異。
```

### ST-UC-14: Windows x64 publish、bundled CLI、clean launch

```text
あなたはStudyReport Evaluator v4.2のWindows legacy deliveryシステムテスト担当者です。
Test ID: ST-UC-14
Requirement: TR-19 / AC-020

捏造禁止。Windows 11 x64とpwsh.exe -NoLogo -NoProfile、PowerShell 7+ Coreだけを使います。Windows PowerShell 5.1へfallbackしません。credentialを収集・表示しません。今回生成・検証していないpackage、install、launch、test件数をPASSにしません。sourceや既存変更をstash、reset、clean、commitしません。

準備:
1. `StudyReportEvaluator.slnx`を上位探索してrepository rootへ移動します。
2. OS build、OS/process architecture、PowerShell version/edition、`dotnet --version`、`global.json`を記録します。Windows 11 x64でなければBLOCKEDです。
3. UTC開始、HEAD、worktree status件数とUTF-8 SHA-256を記録します。
4. repository外にclean extract/evidence directoryを作ります。

build:
1. `global.json`のSDK feature bandとversion 2 lockを使い、operatorはsolution locked restoreを1回行います。後続のpublish script内ではapp projectのcanonical locked restoreとwin-x64 RID restoreを実行し、一時RID lockをcanonical Core/App lockへ照合してcanonical lock hashを変えないことも必須です。
2. Release buildを`--no-restore`で行い、exit code、warning、errorを今回出力から記録します。
3. solution testをRelease `--no-restore --no-build`で1回行い、今回のtotal/passed/failed/skippedを記録します。
4. restore/build/testのいずれかが失敗した場合、古いpackageを代用せずFAILまたはBLOCKEDとして停止します。

publish/package:
- `scripts/publish-windows.ps1`と`scripts/package-windows.ps1`をPowerShell 7+ Coreで実行する。
- RIDは`win-x64`、self-contained、non-trimmed、non-single-file。
- packageにapp、README/docs/images、`copilot-runtime.json`、`runtimes/win-x64/native/copilot.exe`を含む。
- manifestのschema、RID、CLI version、CLI SHA-256、SDK version、relative pathと実fileが一致する。
- Appはmanifestからpackage-relative absolute CLI pathを解決し、PATH fallbackしない。
- ZIPはsingle rootで、safe relative path、0-byte/reparse/source/test/sample/secretを含まない。
- SHA-256 sidecarが実ZIP hashと一致する。
- publish/package scriptはPowerShell 7+だけを受理し、5.1へfallbackしない。

clean launch:
1. 今回生成したZIPとsidecarを照合してからrepository外のclean directoryへ展開します。
2. installer、administrator権限、.NET Runtime/SDK追加installを要求しないことを確認します。
3. 外部.NETを利用できない環境変数境界で展開済みapphostを起動し、startup probe中に異常終了しないことを確認します。
4. 起動しただけでCopilot process、AI send、final/partial作成が発生しないことを確認し、今回起動したprocessだけを終了します。
5. bundled CLI、manifest、sidecar、必須文書の1つでも欠落すればFAILとし、旧packageをPASSにしません。
6. `PackageLockTests`、`WindowsPublishPackageTests`、`WindowsLocalApplicationTests`、`CopilotClientFactoryTests`を実行し、manifest欠落、CLI hash不一致、manifest path不正ではCopilot機能をsafe failureにしてPATH上の同名CLIへfallbackしないことを確認します。negative caseはrepository外のpackage copyまたは既存test fixtureだけを変更します。

署名済み、installer、SmartScreen reputation、非Windows対応を主張しません。package hash、size、entry count、test countは今回値だけを記録します。

報告順: Test ID / UTC開始終了 / commit・status hash / OS build・architecture・pwsh・SDK / restore・build・test結果 / publish identity / package basename・size・SHA-256 / manifest・CLI identity / clean extract rootはbasenameのみ / launch / process cleanup / Status / 差異。
```

### ST-UC-15: Unsigned ZIP integrity、sidecar、再現性、tamper

```text
あなたはStudyReport Evaluator v4.2のWindows legacy package integrityシステムテスト担当者です。
Test ID: ST-UC-15
Requirement: TR-20 / AC-020

捏造禁止。Windows 11 x64、pwsh.exe -NoLogo -NoProfile、PowerShell 7+ Coreだけを使います。既存利用者file、sample、source、test、secretをpackageへ混入させません。今回生成した実物だけを証跡とし、過去hashを流用しません。repository変更をstash、reset、clean、commitしません。

準備:
1. `StudyReportEvaluator.slnx`を上位探索してrepository rootへ移動します。
2. OS/architecture/PowerShell/global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. ST-UC-14相当のlocked restore、Release build、win-x64 publishが今回成功したことを確認します。失敗または未実施ならBLOCKEDとし、古いpublish inputを使用しません。
4. repository外にevidence directoryを作ります。
5. `WindowsPublishPackageTests`をReleaseで実行します。このclassの全testが、再package、tamper、zero-byte、reparse point、既存出力不変を担当します。

同じ今回publish inputから2回packageし、1回目のZIP/sidecarをevidence directoryへcopyしてから2回目を作ります。

各runの必須検査:
- ZIPは単一rootを持つ。
- entry名はordinal順で、重複、大小文字衝突、absolute path、parent traversal、symlink/reparse pointがない。
- source、test、sample、symbol、credential候補、0-byte fileを含まない。
- entry timestampはpackage契約の固定値である。
- `RELEASE-NOTES.txt`はunsigned、self-contained、bundled CLIを明記する。
- sidecarは大文字64桁SHA-256、2 spaces、ZIP basename、LF終端のexact形式で、実ZIP hashと一致する。
- 2回目のZIP sizeとSHA-256が1回目と一致する。

negative case:
- valid ZIPのEOFへbyte `0xA5`を1件appendしたrepository外のtampered copyは、original sidecar検証に失敗する。
- package input copyへ0-byte fileを1件追加したcaseは失敗し、既存ZIP/sidecarを置換しない。
- package input copyへreparse pointを1件追加できる環境では同様に失敗し、既存出力を置換しない。作成permissionがなければこのnegative caseだけBLOCKEDとし、PASSにしない。
- negative fixtureはrepository外だけに作り、test後にcleanupする。

1回目と2回目のbasename、size、SHA-256、entry count、sidecar exact match、tamper rejection、invalid input時の既存出力不変を記録します。署名、installer、SmartScreen reputationがあると報告しません。

全required caseが今回一致した場合だけPASS。不一致はFAIL、必要なpublish inputや環境不足はBLOCKEDです。

報告順: Test ID / 環境 / publish input identity / first ZIP identity / second ZIP identity / sidecar形式 / layout counts / reproducibility / tamper result / zero-byte・reparse case / cleanup / Status / 差異。
```

### ST-UC-16: Windows-only claim、documentation、screenshots

```text
あなたはStudyReport Evaluator v4.2の公開scope・documentation整合システムテスト担当者です。
Test ID: ST-UC-16
Requirement: TR-21 / TR-22 / AC-021 / AC-022

捏造禁止。Avalonia/.NETの一般的なcross-platform対応を本製品の動作証跡にしません。存在しないpackage、installer、署名、notarization、AI品質を作成済みと報告しません。文書と実装が異なる場合、テスト中に文書を期待へ書換えてPASSにせず差異をFAILとして記録します。画像へ実在学生データ、private path、credentialを含めません。repositoryを変更、stash、reset、clean、commitしません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使います。

準備:
1. `StudyReportEvaluator.slnx`を上位探索しrepository rootへ移動します。
2. HEAD、UTC開始、worktree status hash、OS/architecture/PowerShell/global.json SDKを記録します。
3. repository外にevidence directoryを作り、locked restore後、`DocumentationContractTests`、`DocumentationScreenshotTests`、`WindowsPublishPackageTests`をReleaseで実行します。

Delivery transition oracle:
- 要求v4.2とADR-0015はWindows MSIX／macOS RID別DMGをtargetにし、ADR-0013はcurrent public evidence境界として保持する。
- current READMEと利用者文書はproduction artifact実測前にWindows 11 x64 unsigned ZIPだけを対応済みとする。
- legacy scripts/package testsは`win-x64` ZIP regressionを維持し、新deliveryの未実装をPASSにしない。
- macOS、installer、code signing、notarization、SmartScreen／Gatekeeper結果を`PASS_PRODUCTION`前に対応済みと記載しない。
- packageに未生成のmacOS runtime、installer、署名済みmarkerを現在の必須成果物として要求しない。
- framework support、cross-publish、test certificateを現在のproduction PASSへ算入しない。

documentation inventory／link oracle:
- READMEとdocs indexからgetting-started、features、custom evaluator、Prompt launch、privacy、troubleshootingへ到達できる。
- `docs/prompt-launch.md`にCopilot起動依頼Promptとアプリ評価Promptの区別、sample構造、複数の教員scenario、explicit apply、no-auto-runがある。
- Input/Design/Execution/Resultsの記述がnative picker、absolute points、special/reference/similarity、checkpoint/resume、auto final、4 final sheetsと一致する。
- exact warning文がsource、test、docsで一致する。
- Markdown local link targetが存在する。
- package契約にuser docs/imagesが含まれる。

screenshot oracle:
- screenshotsはproduction XAML/ViewModelをsynthetic dataでrenderしたものだけを正式画像とする。
- 生成手順、fake authentication/row/AI/checkpoint/output境界、synthetic provenanceを`images/README.md`へ記録する。
- `DocumentationContractTests`で`images/README.md`の存在、synthetic data、fake authentication/row/AI/checkpoint/output境界、各正式PNGの列挙を検証する。
- captureにprivate path、real answer、credential、実在個人情報がない。
- current UIのInput、mapping、Knowledge、Custom Prompt、Execution、Results、output journeyと一致する。
- mockupや過去UI画像を実画面証跡として扱わない。

read-onlyでdocument inventory、local link、claim文字列、script/package entry contract、image metadata/provenanceを検査します。1箇所でも現在対応と誤読できるclaim、broken link、実装との不一致、provenance不足があればFAILです。

報告順: Test ID / 環境 / docs inventory / link check / UI/requirement comparison / warning exact match / screenshot provenance・privacy / package docs/images / platform claims / test結果 / Status / 差異。
```

### ST-UC-17: 10人fixture new→interrupt→resume E2E

```text
あなたはStudyReport Evaluator v4.2の10人synthetic durable E2Eシステムテスト担当者です。
Test ID: ST-UC-17
Requirement: supplemental deterministic scenario / AC-009〜AC-016 / AC-019

捏造禁止。repositoryの固定synthetic fixtureとlocal deterministic runnerだけを使い、Live AI、Web、外部APIを呼びません。fixtureの回答、Prompt、reference、reason、evidence本文をterminal、report、chatへ出しません。repository fileを変更、stash、reset、clean、commitしません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使い、長時間commandを重複起動しません。

対象fixture:
- `tests/fixtures/system-test/SystemTest-10Students.xlsx`
- bytes 7,652
- SHA-256 `7F473879B3C43319D842881C5DFD1D01B2A60091A5B0C7B39B70B8D574D801C8`
- `SystemTestInput!A1:L11`、header 1 + data 10
- primary F/G/H/I/J、J supporting K
- Base 60、Special 0、SimilarityPenaltyWeight 0.1、RoundingDigits 1、Question Points各8

準備:
1. `StudyReportEvaluator.slnx`を上位探索しrepository rootへ移動します。
2. Windows 11 x64、PowerShell Core 7+、global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. fixtureのSHA-256、size、last-write UTCを取得します。不一致なら変更せずFAILです。
4. repository外にevidence/results directoryを作ります。
5. solution locked restoreを1回行います。

実行:
1. 次の2 testをReleaseで名前指定して実行し、TRXをrepository外へ保存します。
	- `StudyReportEvaluator.App.Tests.E2E.TenPersonSystemSmokeTests.Fixture_has_fixed_identity_structure_and_literal_formula_canaries`
	- `StudyReportEvaluator.App.Tests.E2E.TenPersonSystemSmokeTests.Ten_person_uninterrupted_and_resumed_runs_have_identical_results_and_formula_cache`
2. 1件目でfixture identity、StandardXlsx、1 sheet、A1:L11、10 rows、formula/macro/external-link/DDE/defined-name 0、F〜Jの各blank 1、literal canaryを検証します。
3. 2件目でuninterrupted baselineを実行します。
4. 別output pairで、5行目のcheckpointがsuccessful updateとして保存された直後にcontrolled cancelします。
5. 新しいorchestratorとCheckpointStoreで実在partialをread-only loadし、resumeします。
6. baselineとresumed finalのaccepted status/raw、reference、similarity、Results formula text、cell type、cached valueを比較します。

固定oracle:
- planned normal evaluations: 10×5=50。
- blank primary: F〜Jに各1件、合計5。normal SUCCESS=45、EMPTY=5、failure/cancelled=0。
- reference calls=5、normal calls=45、similarity calls=45、special calls=0。
- SpecialPoints=0のためspecialは`NOT_RUN_ZERO_BUDGET`でdispatchしない。
- uninterrupted checkpoint: create 1、update 15（5 references + 10 rows）、load 0。
- interrupted phase: references 5、completed rows 5、normal/similarity calls各22、checkpoint create 1、update 10、finalなし、partialあり。
- resume phase: saved reference calls 0、remaining normal/similarity calls各23、checkpoint create 0、update 5、load 1。
- resume後のcompleted rows=10、valid finalあり、partialなし。
- finalはoriginal 1 + app-owned 4 sheets、Checkpointなし、Results data rows 10、OpenXmlValidator error 0。
- fixture identityは全phase後も不変で、test temp directoryはcleanupされる。

2 testの両方が今回PASSし、全固定oracleが一致した場合だけPASS。不一致はFAIL、fixture/environment不足はBLOCKEDです。

報告順: Test ID / UTC開始終了 / commit・環境 / fixture identity / targeted test結果 / baseline count / interruption point・count / resume count / result・formula equality / final構造 / input・temp不変 / 証跡 / Status / 差異 / 非保証事項。

最後に、deterministic midpoint/reference/similarityは配線、checkpoint、formula検証用であり、学生の採点結果やAI品質の証明ではないと明記します。
```

### ST-UC-18: Fixed-seed 531-row synthetic new/resume E2E

```text
あなたはStudyReport Evaluator v4.2の531-row fixed-seed synthetic E2Eシステムテスト担当者です。
Test ID: ST-UC-18
Requirement: TR-23 / AC-009〜AC-016 / AC-019

捏造禁止。`SyntheticWorkbookFactory`がseed 20260901から一時生成する匿名synthetic workbookとfake runnerだけを使います。canonical sample、Live AI、Web、外部APIを使いません。cell本文、Prompt、reference、reason、evidence、private pathを証跡へ出しません。repositoryを変更、stash、reset、clean、commitしません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使い、同じ長時間testを重複起動しません。

準備:
1. `StudyReportEvaluator.slnx`を上位探索してrepository rootへ移動します。
2. Windows 11 x64、PowerShell Core 7+、global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. repository外にevidence/results directoryを作り、solution locked restoreを1回行います。
4. 次の2 testをReleaseで完全修飾名指定して実行します。
	- `StudyReportEvaluator.App.Tests.E2E.SyntheticQuantificationJourneyTests.Fixed_seed_531_row_workbook_completes_the_local_atomic_quantification_journey`
	- `StudyReportEvaluator.App.Tests.E2E.SyntheticQuantificationJourneyTests.Fixed_seed_531_row_durable_new_and_resume_matches_uninterrupted_run`

fixture／definition:
- `Original!A1:L531`、header 1 + data 530、seed 20260901。
- 2件以上のenabled questions、normal evaluator、1件以上のenabled special item、reference、similarityを含む。
- `Base + Special + Σ enabled Question Points = 100`のexact definitionを使う。
- formula marker、blank primary、same-row evidence canaryを含むが全てsynthetic。

実行:
1. uninterrupted durable runでreference-first、row ascending、checkpoint after each reference/complete rowを確認します。
2. midwayのcomplete row checkpoint成功直後にprocess終了相当のcontrolled cancellationを発生させ、finalを作らずpartialを保持します。
3. 新しいorchestrator/CheckpointStoreでpartialをread-only loadし、saved referencesとcomplete rowsをskipしてresumeします。
4. uninterruptedとresumed runのaccepted normal/special/similarity raw、status、formula text/cache、FinalRaw/FinalScoreを比較します。
5. fixed-seed生成inputのSHA-256、size、mtimeがnew、cancel、resume、final後に一致することを確認します。

必須oracle:
- referencesはenabled questionにつき1回、resume時の保存済みreference再生成は0。
- rowsは2〜531のascending orderで、complete 530 rowsとなる。
- normal/special/similarity call countはblank primaryによるno-sendを考慮してdefinitionとinputから決定的に導出し、過去値を固定流用しない。
- uninterrupted baselineはcheckpoint create 1、successful update exactly 532（2 references + 530 complete rows）、load 0。
- interrupted phaseはcheckpoint create 1、successful update exactly 267（2 references + 265 complete rows）、load 0。
- resume phaseはcheckpoint create 0、successful update exactly 265（remaining complete rows）、load 1。
- interruptionより前のcomplete rowだけdurableで、in-progress rowはresumeで全体再実行する。
- finalはoriginal sheets + 4 app-owned sheets、Checkpointなし、Results data rows 530、formula/Open XML error 0。
- success後partialを削除する。cleanup fault caseではvalid final + warningを維持する。
- temp、partial、finalのbasenameと存在状態だけを記録し、本文を記録しない。

既存testがuninterruptedだけを検証し、531-row resumeを直接実測していない場合、その不足を別testのPASSで代用せずFAILとして記録します。性能値は本scenarioの保証にせず、測定する場合はAI待機を除く別evidenceとします。

全必須oracleを今回の531-row new/resume runで確認した場合だけPASS。不一致はFAIL、環境不足はBLOCKEDです。

報告順: Test ID / seed・rows・definition shape / commit・環境 / uninterrupted count / interruption checkpoint / resume skip・call count / final comparison / workbook構造・validation / input identity / cleanup / 証跡 / Status / 差異 / 非保証事項。
```

### ST-UC-19: Canonical SampleReport no-network technical durable E2E

```text
あなたはStudyReport Evaluator v4.2のcanonical SampleReport technical durable E2Eシステムテスト担当者です。
Test ID: ST-UC-19
Requirement: real-data technical E2E / AC-009〜AC-016 / AC-019

目的:
`sample/SampleReport.xlsx`を唯一のcanonical sampleとしてread-onlyで使用し、input load、initial mapping、definition snapshot、reference generation、row evaluation、checkpoint、formula workbook、validation、atomic final、cleanupのproduction technical pathをlocal deterministic runnerで検証します。実データをGitHub Copilot、その他の生成AI、Web、外部APIへ送信しません。これはfull user-facing Live AI E2Eでも教育的採点品質testでもありません。

絶対規則:
1. 捏造禁止。未実施、未観測、過去結果、文書値を今回のPASSへ流用しません。
2. workbook cell/header本文、worksheet名、学生情報、Prompt、reference、reason、evidence、credential、token、private absolute pathをterminal、JSON、Markdown、chatへ出しません。TRXはlocal private artifactとして扱います。
3. inputを編集、再保存、rename、移動、削除、匿名化しません。
4. Copilot認証確認、production model call、Execution UIの`定量化を開始`を実行しません。
5. repository source、test、文書、設定を修正、stash、reset、clean、commitしません。
6. PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使います。
7. 同じ長時間testを無出力だけを理由に重複起動しません。
8. evidenceはrepository外の一意なdirectoryへ保存します。

正本:
- `docs/requirements-definition.md` v4.2
- `SystemTest-prompt.md` v4.2
- `docs/getting-started.md`
- `docs/privacy-and-data-handling.md`
- `tests/StudyReportEvaluator.App.Tests/E2E/RealDataSystemSmokeTests.cs`
- `global.json`
`work/`、`test/`、`artifacts/`の過去report、JSON、TRX、hash、件数、時間、判定を今回値へ流用しません。

input baseline:
- logical path `sample/SampleReport.xlsx`
- size 470,806 bytes
- local SHA-256 `73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA`
- identity bindingはsize、SHA-256、先頭128 bytesの一致。
- Open XML entries 11、worksheet 1、dimension A1:L531、rows 531、macro 0、external-link 0。
1項目でも異なる場合、inputを変更せず`BLOCKED_INPUT_IDENTITY_MISMATCH`とします。

initial definition baseline（header/cell本文は非表示）:
- header row 1、data rows 2〜531、530 rows。
- Base 60、Special 0、SimilarityPenaltyWeight 0.1、RoundingDigits 1。
- question 5、primary F/G/H/I/J、Jだけsupporting K。
- evaluator types KnowledgeCoverage、CustomPrompt、KnowledgeCoverage、KnowledgeCoverage、CustomPrompt。
- points各8、allocation total 100。
- special operationはSpecial=0のためdispatch 0。
数値はworkbook cellではなく要求とアプリ初期definitionの既定値です。

環境・provenance:
1. `StudyReportEvaluator.slnx`を上位探索しrepository rootへ移動します。
2. Windows 11 x64、x64 process、PowerShell Core 7+、global.json選択SDKを確認します。違えば`BLOCKED_UNSUPPORTED_ENVIRONMENT`です。
3. External run ID、UTC開始、HEAD、worktree status件数とUTF-8 SHA-256、OS、architecture、.NET、PowerShellを記録します。
4. inputのSHA-256、size、last-write UTC、package構造をread-onlyで取得しbaselineへ照合します。worksheet名はhashだけをreportへ記録できます。

locked build:
1. `dotnet restore .\StudyReportEvaluator.slnx --locked-mode`を1回実行します。
2. `dotnet build .\StudyReportEvaluator.slnx -c Release --no-restore`を1回実行します。
3. exit code、warning、errorを今回出力から記録します。失敗時はE2Eを開始せず`BLOCKED_BUILD_FAILED`です。
4. build後、次のSHA-256、size、last-write UTCを取得します。
	- `src/StudyReportEvaluator.App/bin/Release/net10.0/StudyReportEvaluator.App.exe`
	- `tests/StudyReportEvaluator.App.Tests/bin/Release/net10.0/StudyReportEvaluator.App.Tests.dll`

targeted run:
同じPowerShell 7+ processで次を設定します。値をreportへ出力しません。
- `STUDY_REPORT_EVALUATOR_REALDATA_SYSTEM_SMOKE=1`
- `STUDY_REPORT_EVALUATOR_REALDATA_INPUT=<absolute sample/SampleReport.xlsx>`
- `STUDY_REPORT_EVALUATOR_REALDATA_EVIDENCE=<external evidence directory>/realdata-e2e-evidence.json`
- `STUDY_REPORT_EVALUATOR_SOURCE_COMMIT=<actual HEAD>`
- `STUDY_REPORT_EVALUATOR_SOURCE_STATUS_SHA256=<actual status SHA-256>`

次を1回だけ実行します。
`StudyReportEvaluator.App.Tests.E2E.RealDataSystemSmokeTests.Optional_real_workbook_default_values_complete_local_v4_system_test`
Release、`--no-restore --no-build`、repository外results directory、local TRX loggerを使います。

freshness必須条件:
- test開始後に指定JSONが新規作成され、0 byteでない。
- `measured_at_utc`が今回run開始／終了内。
- source commitとstatus hashが今回指定値。
- JSON SHA-256を取得する。
- test exit code 0、対象failed 0。
- test生成final/recalculation copyはcleanup済み。
- input identityが開始前と完全一致。
opt-in忘れによるtest runner上の見かけPASSを、JSON freshnessなしでPASSにしません。

technical oracle:
1. format=`StandardXlsx`、InputViewModel read-only load成功。
2. dimension、row範囲、5 questions、mapping、evaluator、points、allocationがbaseline一致。
3. startup parserが`--input`を受理し、in-process prefillをloadする。
4. Release apphostがstartup probe中に生存し、controlled cleanupされる。
5. probe前後でinput identityとinput隣接`result` stateが不変で、新規Copilot process signal 0。
6. production row source、snapshot boundary、CheckpointStore、OutputPathPlanner、WorkbookDurableRunFinalizer、partial cleanerを通る。
7. AI boundaryは`local-deterministic-midpoint-no-network-not-for-grading`でnetwork runnerを呼ばない。
8. references 5、completed rows 530、planned normal evaluations 2,650。
9. normal SUCCESS/EMPTYは今回JSONから記録し、合計2,650、failure 0、cancelled 0。
10. reference calls 5。normal/similarity callsは今回SUCCESS件数と一致、special calls 0。
11. checkpoint create 1、update 535（5 references + 530 rows）、load 0。
12. progressにPreparing、GeneratingReferences、SavingCheckpoint、EvaluatingRows、FinalizingWorkbook、Completedを含み、count単調増加、最後in-flight 0。
13. finalization SUCCESS、success後partialなし、working/temp残存なし。
14. finalは元1 + app-owned 4 = 5 sheets、Checkpointなし。
15. Config formula 2、References/Run formula 0、Results formula >0。
16. Results data rows 530、formula error 0、Open XML error 0。
17. deterministic runner範囲でFinalScore numeric 530、blank 0。
18. calculation mode automatic、full calculation on load true。
19. external recalculation statusはadvisoryとしてrequired pathから分離する。

evidence整合性:
- JSONのsource requirements=`docs/requirements-definition.md v4.2`、system_test_prompt=`SystemTest-prompt.md v4.2`。
- driver固定run IDを今回のunique external run IDと誤認せず、`NON_UNIQUE_DRIVER_RUN_ID`として記録する。
- runtime CLI identityがtest value／zero hashであるため、bundled production CLI検証済みと報告しない。
- JSON privacy flagだけを信用せず、input absolute path、user profile path、16文字以上のworkbook文字列が完全一致で混入していないことを値非表示で再確認する。
- TRXを共有privacy artifactとして扱わない。

manual UI scope:
Desktop観測可能な場合だけRelease apphostを`--input`付きで起動し、exact warning、4 steps、Inputのsheet/dimension/row/5 mappings、Designの60/0/0.1/各8/total100、Execution controls、起動だけではResults遷移・AI実行・final/partial作成なしを本文非表示で確認します。`Copilot 状態を確認`と`定量化を開始`は押しません。観測不能なら`NOT_RUN_UI_AUTOMATION_UNAVAILABLE`でありPASSにしません。Full production AI E2Eは`NOT_RUN_POLICY_REAL_DATA`固定です。

regression:
targeted run後、上記5環境変数を同じprocessから必ず解除します。その後`dotnet test .\StudyReportEvaluator.slnx -c Release --no-restore --no-build`を1回実行し、今回のtotal/passed/failed/skipped/durationを記録します。環境変数を残したままreal-data testを再実行しません。

最終不変条件:
- input SHA-256、size、last-write UTCが開始前とexact一致。
- EXE/DLL SHA-256がtargeted test開始前と一致。
- 開始時からのsource status hashが一致し、今回のrepository内成果物がない。
- input隣接`result`に今回由来final/partial/tempなし。
- 今回起動したapphost/testhost/Excel processなし。既存の無関係processを終了しない。
- opt-in環境変数なし。

scope別status:
- Input identity: PASS/FAIL/BLOCKED
- Locked restore・Release build: PASS/FAIL/BLOCKED
- Required real-data technical durable E2E: PASS/FAIL/BLOCKED
- Evidence provenance/privacy: PASS/FAIL
- Manual UI: PASS/FAIL/NOT_RUN_UI_AUTOMATION_UNAVAILABLE
- Full user-facing production AI E2E: NOT_RUN_POLICY_REAL_DATA
- Live AI educational quality: NOT_EVALUATED
- External recalculation: PASS/FAILED_ADVISORY/SKIPPED_NOT_INSTALLED/NOT_RUN_EXTERNAL_PREREQUISITE
- Full regression: PASS/FAIL/BLOCKED

Required technical E2Eは全technical oracleを今回確認した場合だけPASSです。manual、production AI、external recalculationを曖昧な「full E2E PASS」「完全に問題なし」「release ready」へまとめません。

報告順: External run ID・UTC / commit・status hash・環境 / EXE・DLL identity / input identity・attachment binding / restore・build / targeted freshness・test結果・evidence hash / mapping・allocation / durable counts・stage / final workbook・cleanup / evidence metadata・privacy / manual UI・production AI・external recalculation / regression / final invariants / scope別status / finding一覧 / 確認済み・未確認・非保証。

最後に、deterministic midpoint、固定technical reference、固定similarityは配線・永続化・数式検証用で学生の採点結果ではなく、実データのLive AI正確性、公平性、教育的妥当性、法的／組織policy適合性を評価していないこと、similarityは不正行為を証明しないことを明記します。
```

### ST-UC-20: Optional authenticated synthetic Copilot smoke

```text
あなたはStudyReport Evaluator v4.2のoptional authenticated synthetic Copilot smoke担当者です。
Test ID: ST-UC-20
Requirement: TR-24A advisory

このtestは任意であり、required deterministic acceptanceの代替ではありません。捏造禁止。canonical sampleや実在学生データを送信しません。credential、token、Prompt、response、reason、evidence本文をreportへ出しません。credentialを収集・入力・保存しません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使います。

準備:
1. `StudyReportEvaluator.slnx`を上位探索しrepository rootへ移動します。
2. Windows 11 x64、PowerShell Core 7+、global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. repository外にevidence directoryを作り、locked restoreを1回行います。
4. `AuthenticatedSyntheticSmokeTests.Advisory_status_set_is_exact`と`Default_policy_does_not_probe_login_or_start_network_even_if_login_would_be_available`を必ず実行し、defaultがauth probe 0、network 0、status NOT_RUNであることを確認します。

opt-in判定:
- 実行依頼にLive network testの明示許可がない場合、`STUDY_REPORT_EVALUATOR_COPILOT_LIVE_SMOKE`を設定せずoptional testを実行し、Status=`NOT_RUN`として終了します。
- 明示許可がある場合だけ同じPowerShell processで`STUDY_REPORT_EVALUATOR_COPILOT_LIVE_SMOKE=1`を設定し、既存loginを使用します。secret入力を要求された場合は入力せず`SKIPPED_NOT_AUTHENTICATED`です。

opt-in時に次の1件だけを実行します:
`StudyReportEvaluator.App.Tests.Copilot.AuthenticatedSyntheticSmokeTests.Optional_authenticated_synthetic_smoke_reports_advisory_status_honestly`

送信data classはtest codeに固定された1件のsynthetic payloadだけです。許容status:
- PASS
- SKIPPED_NOT_AUTHENTICATED
- FAILED_ADVISORY
opt-inなしはNOT_RUNです。

PASSはsession/tool/schema/cleanupの1件のtechnical smoke成功だけを意味し、教育的品質、全model互換性、real-data品質、production release readinessを証明しません。実行後にopt-in環境変数を解除します。test生成advisory JSONは本文非包含を確認し、repositoryへcommitしません。

報告順: Test ID / advisory kind / explicit opt-in有無 / environment / sent data class / auth probe・network count / observed safe status・rationale code / artifact basename・SHA-256 / env cleanup / Status / 非保証事項。
```

### ST-UC-21: Optional external spreadsheet recalculation

```text
あなたはStudyReport Evaluator v4.2のoptional external spreadsheet recalculation smoke担当者です。
Test ID: ST-UC-21
Requirement: TR-24B advisory

このtestは任意であり、required runtimeやdeterministic acceptanceの代替ではありません。捏造禁止。専用synthetic formula workbookだけを使用し、canonical sampleや10人fixtureを外部spreadsheetで保存しません。既存利用者fileを変更しません。cell本文、private path、credentialをreportへ出しません。PowerShellはpwsh.exe -NoLogo -NoProfile、7+ Coreだけを使います。

準備:
1. `StudyReportEvaluator.slnx`を上位探索してrepository rootへ移動します。
2. Windows 11 x64、PowerShell Core 7+、global.json SDK、HEAD、UTC開始、worktree status hashを記録します。
3. repository外にevidence directoryを作り、locked restoreを1回行います。
4. 実行依頼にexternal spreadsheet使用の明示許可がない場合、`STUDY_REPORT_EVALUATOR_EXTERNAL_RECALC_SMOKE`を設定せずStatus=`NOT_RUN_EXTERNAL_PREREQUISITE`として終了します。
5. 明示許可がある場合だけ同じPowerShell processで`STUDY_REPORT_EVALUATOR_EXTERNAL_RECALC_SMOKE=1`を設定します。

次をReleaseで1回実行します:
`StudyReportEvaluator.App.Tests.E2E.ExternalSpreadsheetRecalculationSmokeTests.Optional_external_spreadsheet_recalculation_records_advisory_status_honestly`

oracle:
- Excel等の登録済みsupported spreadsheetがなければ`SKIPPED_NOT_INSTALLED`。
- 利用可能ならtestが作るsynthetic output copyだけをfull recalculationして保存する。
- app-owned formula 11件の再計算cacheを独立expected valuesと比較する。
- formula error 0、Open XML validation error 0。
- input synthetic workbookはtest temp内だけで、test後にcleanupする。
- external processは今回起動したinstanceだけを終了する。
- 許容statusはPASS、SKIPPED_NOT_INSTALLED、FAILED_ADVISORY。失敗をrequired pathのFAILへ変換しない。

testが`artifacts/test/external-recalculation-smoke.json`を生成した場合、開始前の存在・hashを記録し、今回生成物をrepositoryへcommitしません。共有用にはstatus、safe rationale code、artifact SHA-256だけをrepository外evidenceへ転記し、本文を転載しません。実行後にopt-in環境変数を解除します。

PASSは登録済みspreadsheet 1環境でsynthetic formulaが再計算されたことだけを意味し、external spreadsheetをrequired runtimeにせず、Live AI、実データ、全version互換性を保証しません。

報告順: Test ID / advisory kind / explicit opt-in有無 / environment / spreadsheet存在・version / synthetic data class / formula count・error count / Open XML count / process・temp cleanup / artifact basename・SHA-256 / observed status・rationale / env cleanup / 非保証事項。
```

### ST-UC-22: Windows MSIX mechanism、production trust、install lifecycle

```text
あなたはStudyReport Evaluator v4.2のWindows MSIX deliveryテスト担当者です。
Test ID: ST-UC-22
Requirement: TR-25 / AC-023 / AC-026 / AC-027

捏造禁止。Windows 11 x64、PowerShell Core 7+だけを使用します。既存利用者file、certificate private key、password、tokenを表示・変更しません。test certificateまたはunsigned development packageの成功をproduction trustへ読み替えません。repositoryの既存差分をstash、reset、clean、commitしません。

準備:
1. repository root、HEAD、status hash、UTC開始、exact Windows edition/build、OS/process architecture、PowerShell、.NET SDKを記録します。
2. Windows SDKのMakeAppxをabsolute pathで解決します。signed-test modeだけはSignToolも必須です。取得元、version、SHA-256、Authenticode statusを記録し、見つからなければ当該mechanism modeを`BLOCKED`とします。tool pathを推測しません。
3. repository外に一意なpackage/install/evidence directoryと、入力不変確認用のsynthetic `.xlsx` copyを作ります。
4. locked restore、Release build、Windows legacy package regressionが今回PASSした後だけMSIXを作ります。

Mechanism scope:
1. `win-x64` self-contained publishからmanifestとvisual assetsを含むMSIX stagingを作ります。
2. Identity Versionが製品stable SemVerの`Major.Minor.Patch.0`、architectureがx64、Executableが実apphostであることを検証します。signed-test modeはPublisherがtest certificate subjectとexact一致し、unsigned-development modeは`CN=<test name>, OID.2.25.311729368913984317654407730594956997722=1`とし、fixed markerをPublisherの最終fieldに置いてsigned packageと別identityにします。
3. packageにsource、test、sample、PDB、secret、0-byte、root外pathがないことを展開前に検査します。
4. certificateが提供されたsigned-test modeではself-signed test certificateで署名し、signatureとtimestampのmechanismを検証します。証明書なしのunsigned-development modeではartifact名を`.unsigned.test.msix`、`AppxSignature.p7x`を0件とし、使い捨てWindows 11 VMまたは復元可能なsnapshot上で管理者PowerShellから`Add-AppxPackage -AllowUnsigned`による全ユーザーinstallだけを行います。unsigned modeをダブルクリック、標準App Installer UI、非管理者setupの成功として報告しません。
5. 実行したmodeを明記し、Start menu launch、close、restart、same-version repair、higher-version upgrade、uninstallを実行します。unsigned modeでは全ユーザーregistrationとcleanupも確認し、update／repair結果をproduction identity間updateの証拠へ代用しません。
6. package-local bundled CLI path/hash/version、child process start/cleanup、native picker、synthetic input read-only、partial/final outputを確認します。Live AIはこのscopeで必須にしません。
7. repair/uninstall前後でsynthetic input、final、partialのSHA-256を比較し、user workbookを削除・変更しないことを確認します。
8. unsigned-development modeでは`-AllowUnsigned`なし、fixed marker欠落、marker値1桁変更、markerがPublisher最終fieldでない、署名entry混入、tampered、wrong architectureを個別に拒否します。signed-test modeではtampered、wrong Publisher、wrong architecture、invalid signatureを個別に拒否します。

Production scope:
- trusted production signing identityがprotected environmentに存在し、clean target machineでchain、timestamp、install、launch、bundled CLI、uninstallを今回確認した場合だけ`PASS_PRODUCTION`。
- identityまたはclean hostがなければ`BLOCKED_EXTERNAL`。`PASS_MECHANISM`を昇格しません。

報告順: Test ID / UTC / source・environment / SDK tool identity / manifest / package bytes・SHA-256 / mechanism signature / install lifecycle / bundled CLI / user file identity / negative cases / mechanism status / production status / evidence / 未確認範囲。
```

### ST-UC-23: macOS RID別publishと`.app`／DMG structure

```text
あなたはStudyReport Evaluator v4.2のmacOS package structureテスト担当者です。
Test ID: ST-UC-23
Requirement: TR-26 / AC-024 / AC-026

捏造禁止。`osx-arm64`と`osx-x64`を独立artifactとして扱い、cross-publishをnative launchへ、Rosettaをnative architectureへ代用しません。credential、学生data、Prompt本文を扱いません。repositoryの既存差分を変更しません。

準備:
1. repository root、HEAD、status hash、UTC開始、host OS/build/architecture、.NET SDKを記録します。
2. locked restore後、各RIDをRelease/self-contained/non-trimmed/non-single-file/UseAppHostで別directoryへpublishします。
3. fixed GitHub.Copilot.SDK packageが指定するCLI version、RID mapping、npm package integrityを取得済みlock/targetsへ照合します。

各RIDのoracle:
- `.app/Contents/Info.plist`、`MacOS`、`Resources`、必要な`Frameworks`が存在し、bundle外escape linkがない。
- `CFBundleExecutable`、bundle ID `com.github.dahatake.study-report-evaluator`、short/build version、icon、minimum OSがpackage contractと一致する。
- apphostと`runtimes/<RID>/native/copilot`がnonzeroで、対象Mach-O architectureとexecute modeを持つ。
- .NET runtime、Avalonia native assets、Open XML、SDK、`copilot-runtime.json`が存在する。
- manifestのRID、CLI version、SDK version、relative path、SHA-256が実fileと一致し、PATH fallbackがない。
- source、test、sample、PDB、secret、0-byteを含まない。

同architecture macOS hostでunsigned development bundleをFinderまたは`open`相当から起動できた範囲だけ`PASS_MECHANISM`とします。cross-publish hostだけの場合はstructureとnative launchを分離し、launchを`NOT_RUN`または`BLOCKED_EXTERNAL`とします。DMG作成toolがないhostでDMG成功を主張しません。

報告順: Test ID / UTC / source・host / RID / publish result / app bundle tree / plist / Mach-O・mode / manifest・CLI / forbidden entry / launch scope / DMG scope / status / evidence / 未確認範囲。
```

### ST-UC-24: macOS Developer ID、notary、staple、quarantine

```text
あなたはStudyReport Evaluator v4.2のmacOS production trustテスト担当者です。
Test ID: ST-UC-24
Requirement: TR-27 / AC-025

捏造禁止。Developer ID Application identity、notarization credential、native test hostがprotected environmentにない場合は`BLOCKED_EXTERNAL`として終了し、ad-hoc署名やtest identityをproductionへ代用しません。secret値、device code、certificate bytes、password、tokenをterminal、artifact、reportへ出しません。

対象RIDごとに次の順序を1回だけ実行します。
1. unsigned source CLIのpackage/version/integrity/SHA-256を記録します。
2. `.app`の全resourceを配置し、entitlementを`plutil`で検査します。必要性を実測した最小集合だけを使い、`get-task-allow`を拒否します。
3. nested Mach-O、Copilot CLI、apphostをDeveloper ID Application、hardened runtime、secure timestamp付きで内側から署名します。signing shortcutとして`--deep`を使いません。
4. signed CLIのSHA-256をruntime manifestへ反映し、resource不変を確認後、outer `.app`を最後に署名します。
5. outer署名後はbundleを変更せず、strict `codesign`、entitlement、timestamp、`spctl`、runtime manifest、CLI handshakeを検証します。
6. appをnotarization用containerでsubmitし、Accepted statusとnotary logを確認し、appへticketをstaple/validateします。
7. stapled appをRID別DMGへ格納し、DMG integrity、notarization、staple、validationを確認します。
8. HTTPS相当で取得してquarantineが付いたclean native hostで、DMG open、Applicationsへdrag、Finder launch、close/restart、bundled CLIを確認します。
9. tamper、signature invalid、notary rejected、missing execute modeを単一原因で拒否します。

exact macOS build × native architectureの全oracleが今回一致した行だけ`PASS_PRODUCTION`。別OS major、別architectureへ一般化しません。

報告順: Test ID / UTC / source / exact macOS・arch / non-secret signer identity / source CLI identity / entitlement / nested sign / shipped hash manifest / outer sign / notary status・log issue count / app・DMG staple / quarantine journey / negative cases / Status / evidence / 未確認行。
```

### ST-UC-25: Package-installed application E2Eとrelease matrix

```text
あなたはStudyReport Evaluator v4.2のplatform release gate担当者です。
Test ID: ST-UC-25
Requirement: TR-28 / TR-29 / AC-027 / AC-028

捏造禁止。ST-UC-22〜24の今回evidenceだけを入力にし、過去PASS、framework tier、cross-publishをproduction statusへ代用しません。private sample、student content、Prompt、response、reason、evidence、credential、private pathを共有evidenceへ含めません。

1. machine-readable platform matrixを読み、claimed artifactごとのsource commit、version、RID、bytes、SHA-256、non-secret signer/notary identity、exact OS build、native architecture、test statusをclosed schemaで検証します。
2. required行に`NOT_RUN`、`BLOCKED`、`BLOCKED_EXTERNAL`、`FAIL`、`PASS_MECHANISM`が1件でもあれば、そのartifactとREADME support claimを公開対象から除外します。
3. 各`PASS_PRODUCTION` package-installed appで10人synthetic fixtureを使い、picker、input hash不変、reference/row checkpoint、process restart resume、final workbook、bundled CLI path/hash/version、no-content logを再実行します。
4. install／upgrade／repair／uninstallまたはapp removal前後でinput/final/partial identityを比較します。
5. protected release workflowがPR/forkへsecretを渡さず、temporary keychain/certificateをcleanupし、全required行成功前にReleaseを公開しないことを検証します。
6. draft candidateをfresh downloadし、asset set、hash、signature/notary、install/launch/uninstallを再確認します。公開URL、bytes、hash、run IDは今回実測値だけを記録します。
7. documentation contractでWindows/macOS primary setupが各3操作以内、未実測OS/archが非対応、terminal/Gatekeeper bypassを要求しないことを確認します。

overall `PASS_PRODUCTION`は、claimed artifactの全required行、package-installed E2E、secret boundary、fresh download、documentationが全て成功した場合だけです。それ以外は正確なscope別statusを維持します。

報告順: Test ID / UTC / source / matrix rows / rejected rows / package-installed E2E / user file identity / workflow secret boundary / public-like download / docs / scope別status / evidence / blockers / 非保証事項。
```

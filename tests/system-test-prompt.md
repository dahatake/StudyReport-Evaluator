# StudyReport Evaluator v4.0 システムテスト用Prompt集

| 項目 | 内容 |
|---|---|
| 文書用途 | システムテスト実行者またはテスト支援AIへ渡す、単独copy可能な実行Prompt集 |
| 対象 | StudyReport Evaluator v4.0 |
| 基準日 | 2026-09-02 |
| 要求正本 | `docs/requirements-definition.md` v4.0 |
| 設計正本 | `docs-dev/detailed-design.md` / ADR-0012 |
| 対象環境 | Windows 11 x64、macOS arm64 / x64、.NET 10、Avalonia |
| 注意 | 本書はテスト結果ではない。未実施をPASSとして扱わない |

> **捏造禁止:** 期待結果は要求・実装・決定的oracleから導出し、実測結果は今回の実行証跡だけから記録する。過去のtest件数、性能値、package hash、Live AI結果、macOS署名結果を今回値として流用しない。

## 1. 旧v3ユースケースのレビュー結果

旧版の12分類は、利用者journeyと安全境界を分ける分類としては有効だった。ただし期待値はv3実装に固定されており、そのまま実行するとv4の正常動作をFAILと誤判定する。次のとおり再編した。

| 旧UC | 分類の有効性 | v4での変更 |
|---|---|---|
| UC-01 shell / warning | 有効 | exact warning文を更新。native picker、new/resume、auto final、final/partial表示を追加 |
| UC-02 input / mapping | 有効 | question text row 1/2、通常回答F/I、Prompt関連G/J/K、special source mappingを追加 |
| UC-03 unsupported input | 有効 | 標準`.xlsx`だけという境界を維持 |
| UC-04 dynamic design | 有効 | question相対weightを絶対Pointsへ変更。Base/Special/Similarity、均等配分、special CRUDを追加 |
| UC-05 Knowledge | 有効 | criterion raw-onlyを維持。空通常回答はAI callなし、QuestionEarned 0へ更新 |
| UC-06 Custom Prompt | 有効 | special PromptとImported Prompt／command-line launchを追加 |
| UC-07 auth / execution / cancel | 有効 | reference-first、row単位、stage、checkpoint、resume、operation countへ更新 |
| UC-08 score / override | 有効 | QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScoreへ更新。empty-zeroとtechnical-blankを分離 |
| UC-09 output | 有効 | 手動3-sheet exportから、`result/eval-*` auto final、4 app-owned sheets、partial checkpointへ更新 |
| UC-10 privacy / validation | 有効 | normalだけでなくreference／special／similarityの4 operationへ拡張 |
| UC-11 Windows delivery | 有効 | bundled CLI、manifest/hash、user-local installerを必須化 |
| UC-12 optional smoke | 有効 | macOS deterministic／credentialed external gateを分離し、未実行を明示 |

再作成版は要求定義§19のTest Requirement 1〜24へ1対1で対応する。Prompt数を過去の42へ合わせず、**24件**を正本とする。

## 2. 使い方

1. `STP-TR-01`〜`STP-TR-24`から実行対象を選ぶ。
2. 直下の`text`コードブロック全体を1個だけcopyする。
3. `<REPO_ROOT>`、`<SYNTHETIC_XLSX>`、`<MODEL_ID>`等だけを実環境値へ置換する。
4. テスト担当者またはコマンド実行可能なAI agentへ渡す。
5. Prompt内の報告形式とstatus語彙を変更しない。

各コードブロックは共通規則を内包し、単独で完結する。複数Promptを連結して1件のPASSにしない。

## 3. 共通変数

| 変数 | 意味 | 規則 |
|---|---|---|
| `<REPO_ROOT>` | `StudyReportEvaluator.slnx`があるrepository root | 実行環境でabsolute pathへ解決 |
| `<SYNTHETIC_XLSX>` | 個人情報を含まない標準`.xlsx` | Live AIへ使用できる唯一の入力。なければBLOCKED |
| `<OUTPUT_DIR>` | 一時出力directory | inputと同一または配下でもよいが、既存fileを上書きしない |
| `<MODEL_ID>` | 認証確認後にUIへ実表示された通常評価model | 文書から推測しない |
| `<EVIDENCE_DIR>` | 機密本文を含まない証跡directory | repositoryへcommitしない |
| `<MAC_RID>` | `osx-arm64`または`osx-x64` | 実runner architectureと一致させる |

## 4. 判定語彙

| Status | 使用条件 |
|---|---|
| `PASS` | Promptに書かれた必須確認を全て実測し、期待と一致 |
| `FAIL` | 実行できたが1件以上が期待と不一致 |
| `BLOCKED` | 必須fixture、tool、permission等がなく開始不能 |
| `NOT_RUN` | 任意テストを意図的に実行していない |
| `SKIPPED_NOT_AUTHENTICATED` | optional Live Copilotだけ、login不足で未送信 |
| `NOT_RUN_EXTERNAL_PREREQUISITE` | macOS runner、Developer ID、notary credential等の外部前提がない |

---

## UC-01: Forms入力、sample構造、picker

### STP-TR-01: Forms型synthetic workbookとquestion row 1/2

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-01
Requirement: TR-01 / AC-001 / AC-002

厳守: 実測だけを報告し、未実施をPASSにしない。実在学生データ、回答、Prompt、reason、evidence、credential、token、private pathを証跡へ記録しない。repository sampleをLive AIへ送らない。入力原本と無関係な変更を変更・stash・commitしない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / 対象commit・OS・architecture・.NET / 前提 / 実施 / 期待 / 実測 / 非機密証跡 / Status / 差異と再現手順。StatusはPASS、FAIL、BLOCKED、NOT_RUN、SKIPPED_NOT_AUTHENTICATED、NOT_RUN_EXTERNAL_PREREQUISITEのいずれか。

既存test factoryで、Microsoft Forms型とGoogle Forms型の匿名化synthetic workbookを作り、question text rowが1のcaseと2のcaseを個別に検証してください。

必須確認:
- standard `.xlsx`としてread-only読込できる。
- question text rowは1または2だけを選択できる。
- first data rowはquestion text rowより後。
- timestamp、email、氏名等の管理列は初期primary候補にならない。
- normal answer、student Prompt、Prompt considerationsのheader semanticから候補を作る。
- candidateは利用者が変更でき、列位置だけで固定mappingしない。
- 読込前後のinput SHA-256、size、last-write timeが一致する。

UIで未観測の内部候補は対応するmetadata/mapping testを証跡にし、画面で確認したと偽らないでください。
```

### STP-TR-02: Repository sample identityとF〜K role

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-02
Requirement: TR-02 / AC-003

厳守: 実測だけを報告し、未実施をPASSにしない。sampleの回答cell、氏名、メール、Prompt本文を読み出し・表示・AI送信しない。構造metadataとfile identityだけを扱う。入力原本と無関係な変更を変更・stash・commitしない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / 対象commit・OS・architecture・.NET / 前提 / 実施 / 期待 / 実測 / 非機密証跡 / Status / 差異と再現手順。

`<REPO_ROOT>/sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx`をread-onlyで検査してください。

期待するrepository sample identityと構造:
- bytes: 661,189
- SHA-256: `446386E20BB4096561CB4AFD6D74B8EAA9D50EAE53C97F984BA7F70EBEAD0DE5`
- package entries: 18
- `Old!A1:AF531`、`Original!A1:L531`、`Final!A1:AD531`
- initial source: `Original`、question row 1、data rows 2〜531
- A〜E: managementで初期対象外
- F/I: normal report answer候補
- G/J: student Prompt候補
- H:別回答またはsupporting候補
- K: JのPrompt considerations/supporting候補
- L: PBL feedbackで初期対象外

F〜Kは候補であり固定mappingではありません。検査前後のSHA-256、size、mtimeを比較し、1つでも不一致ならFAILとしてください。sampleを期待値へ合わせて変更してはいけません。
```

### STP-TR-03: Native picker、direct path、unsupported format

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-03
Requirement: TR-03 / AC-001 / AC-002

厳守: 実測だけを報告し、未実施をPASSにしない。実在データを使わず一時fixtureだけを使う。file本文とprivate pathをerror reportへ含めない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / 環境 / 前提 / 実施 / 期待 / 実測 / 証跡 / Status / 差異。

次を単一原因ごとに検証してください。
1. native picker cancel: 既存Input stateとpathが変わらず、buttonを再利用できる。
2. native picker select: 1件の`.xlsx`だけを同じread-only loaderへ渡す。
3. pickerがlocal pathを提供しない: cancelと区別したsafe案内を表示し、direct path入力を維持する。
4. direct path: 日本語・空白を含むabsolute pathを読める。
5. `.csv`、`.xls`、`.xlsm`、PDF、encrypted/protected、corrupt ZIP、unsafe packageを拒否する。
6. file path編集時に旧metadata、snapshot、sheet、mappingをclearする。

期待結果は、unsupported fileがloaded状態にならず、技術codeだけを示し、内容をechoしないことです。picker filterだけをsecurity validationの代替にしないでください。
```

---

## UC-02: 絶対配点と設計validation

### STP-TR-04: 初期配分、端数、手動保持、均等配分

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-04
Requirement: TR-04 / AC-004 / AC-005

厳守: 実測またはdeterministic oracleだけを報告し、Live AI scoreを使わない。入力や既存workbookを変更しない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / 環境 / fixture / 実施 / 期待 / 実測 / Status / 差異。

Design UIとScoringAllocationCalculatorで次を検証してください。
- defaultはBasePoints=60、SpecialPoints=0、SimilarityPenaltyWeight=0.1。
- enabled question 2件の初期Pointsは20、20。
- enabled question 3件では13.333333、13.333333、13.333334となり、合計はexact 100。
- question追加、Base/Special編集、enable/disableだけでは既存の手動Pointsを自動変更しない。
- 「設問配点を均等化」を明示実行した時だけenabled questionへ残点を再配分する。
- disabled questionのPointsは均等化で変更しない。
- snapshot作成後のdraft変更が既存snapshotを変えない。

端数値は6 decimal places・最後のenabled questionへ残りを入れる現行oracleと比較してください。
```

### STP-TR-05: 配点、range、similarity、special minimum validation

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-05
Requirement: TR-05 / AC-006 / AC-008

厳守: 各caseを単一原因にし、前caseのinvalid値を戻してから次へ進む。errorへPrompt本文やdisplay bodyを転記しない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / 環境 / case / 期待code・field / 実測 / Status / 差異。

次をCore validatorとDesign UIで検証してください。
- BasePointsとSpecialPointsは0〜100。
- SimilarityPenaltyWeightは0〜1。
- enabled Question Pointsは0以上。
- `Base + Special + Σ enabled Question Points`はexact 100。99.999999や100.000001を許容しない。
- SpecialPoints>0ならenabled special itemが1件以上必要。
- evaluator/criterion weightはenabledなら正数。
- minimum < maximum。
- rounding digitsは0〜6。
- question text rowは1または2。
- invalid中はsnapshotとrun開始をblockする。

期待code例: `BASE_POINTS_OUT_OF_RANGE`、`SPECIAL_POINTS_OUT_OF_RANGE`、`SIMILARITY_WEIGHT_OUT_OF_RANGE`、`QUESTION_POINTS_OUT_OF_RANGE`、`ALLOCATION_TOTAL_INVALID`、`SPECIAL_ITEMS_REQUIRED`。
```

---

## UC-03: Knowledge、Custom、special Prompt

### STP-TR-06: Prompt placeholderとclosed tool schema

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-06
Requirement: TR-06 / AC-007 / AC-008

厳守: 合成Promptだけを使う。Prompt本文をlog/error artifactへ出さない。Live AI点数を固定しない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / 環境 / operation / 実施 / 期待 / 実測 / Status / 差異。

次を検証してください。
1. Knowledge: app-owned semantic templateはread-onlyで、「説明・関係・適用」を扱い、keyword存在だけで満点にしない。利用者templateへ置換できない。
2. Custom: `{回答}`と`{評価項目}`が必須。`{設問}`、`{補助情報}`、`{最小点}`、`{最大点}`は任意。
3. Special: `{回答}`が必須で、`{評価項目}`は必須ではない。
4. `{{` / `}}`はliteral brace、挿入値はsingle-passで再展開しない。
5. unknown、unclosed、nested、unmatched braceをrun前に拒否する。
6. 6 placeholder制約は利用者編集可能なCustom／Specialだけに適用する。app-owned Referenceは固定`{設問}`だけを限定置換し、Similarityは固定sectionへ値を直接組み立てる。`{参照回答}`を利用者placeholderとして許可しない。
7. normal=`submit_quantification`、reference=`submit_reference_answer`、special=`submit_special_quantification`、similarity=`submit_similarity`とし、該当operationのtoolだけを各sessionへ1件公開する。
8. schemaはadditionalProperties=false、required IDsはexact enum、score/rangeを閉じる。
9. duplicate tool call、missing result、unknown ID、partial resultを全体拒否する。

invalid Promptでinput row read、Copilot session、runner callが0件であることをdeterministic testで確認してください。
```

---

## UC-04: Reference、special、similarity operation

### STP-TR-07: Reference exactly once、auto、resume reuse

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-07
Requirement: TR-07 / AC-009

厳守: repository sampleをLive AIへ送らず、fake transportまたは明示許可されたsynthetic inputだけを使う。reference本文を証跡へ記録しない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / 環境 / fixture / 実施 / 期待 / 実測 / Status / 差異。

enabled questionが2件のrunで次を確認してください。
- student rowを読む前にreference phaseを実行する。
- 1 question/runにつきreference operationはexactly 1回。
- model IDはliteral `auto`で、別modelへfallbackしない。
- 各reference完了後にpartial checkpointを更新する。
- SUCCESSは非空answerとexact QuestionIdを持つ。
- invalid/timeout/network/auth failureはanswer blankと技術statusを保存する。
- resumeでは保存済みreferenceを再生成せず、未生成questionだけを続行する。
- finalの`Quantification_References`へquestion、answer、`auto`、status、UTC timestampを保存する。

attempt/session countとcheckpoint更新順をfake transport/storeの記録から取得し、推測しないでください。
```

### STP-TR-08: Normal/special/similarity境界、empty-zero、failure-blank

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-08
Requirement: TR-08 / AC-010 / AC-012

厳守: 制御fixtureだけを使い、実在回答やLive AI期待値を使わない。各operationを単一原因で検証する。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / operation / case / 期待 / 実測 / Status / 差異。

次を検証してください。
- normal rawはcriterion effective rangeのminimum/maximumを受理し、範囲外をrejectする。
- special scoreは0と1を受理し、負数と1超をrejectする。
- similarityは0と1を受理し、負数と1超をrejectする。
- 空normal primaryはAI callなし、QuestionRate=0、QuestionEarned=0。
- 空special primaryはAI callなし、special score=0。
- 空student answerはsimilarity callなし、similarity=0、penalty=0。
- SpecialPoints=0ではspecial runner callなし、`NOT_RUN_ZERO_BUDGET`。
- nonempty入力のschema/timeout/network/auth/cleanup failureはraw blankで、0へ変換しない。
- reference failure時は当該similarityを送信せずblankにする。
- technical blankはFinalRaw/FinalScoreへblank伝播する。

empty-zeroとtechnical-blankを同じtest dataへ混在させず、各cellのtypeとcached previewを確認してください。
```

---

## UC-05: Score oracleとExcel formula

### STP-TR-09: Question/Special/Penalty/Final hand oracle

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-09
Requirement: TR-09 / AC-011 / AC-012

厳守: 次の固定合成oracleだけを使用し、Live AIの期待点へ流用しない。Excel結果だけを正本にせず、独立Core計算と比較する。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / oracle / 手計算 / Core / workbook cache / Status / 差異。

固定値:
- BasePoints=60、SpecialPoints=10、SimilarityPenaltyWeight=0.1、rounding digits=1。
- Q1 Points=20、normalized=80、similarity=0.5、special question rate=0.8。
- Q2 Points=10、normalized=50、similarity=0.2、special question rate=0.6。

期待:
- Q1 QuestionRate=0.8、QuestionEarned=16.0、Penalty=1.0。
- Q2 QuestionRate=0.5、QuestionEarned=5.0、Penalty=0.2。
- SpecialEarned=10×average(0.8,0.6)=7.0。
- FinalRaw=60+16+5+7-1-0.2=86.8。
- FinalScore=86.8。
- FinalRaw<0ならFinalScore=0、FinalRaw>100なら100。
- midpoint roundingはAwayFromZero。

各段階でroundした値を次段へ使い、式cacheとCore oracleが一致することを確認してください。
```

### STP-TR-10: Config参照formula、blank、allowlist、DAG、limits

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-10
Requirement: TR-10 / AC-006 / AC-011 / AC-012

厳守: 合成workbookだけを使い、formula textへprivate pathや外部book名を正例として入れない。hostile caseは専用一時fixtureで行う。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / formula surface / 期待 / 実測 / Status / 差異。

検証対象:
- ConfigのAllocationTotalとAllocationValidはapp-owned formulaで、他のConfig cellはliteral。
- ResultsのEffectiveRaw、Normalized、Evaluator、QuestionRate、QuestionEarned、SpecialQuestionRate、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScoreはclosed ASTから生成する。
- points、range、rounding、similarity weightをformulaへliteral埋込みせず、actual Config sheetへabsolute referenceする。
- formula storage textはleading `=`を持たない。
- allowlist外function、raw formula、external workbook、DDE、defined-name injectionをrejectする。
- target重複、unknown reference、cycleをrejectする。
- Excel formula length 8191、function argument 255、column 16384、cell 32767をwrite前に検査する。
- expected formula planはConfigとResults双方をexactly once含み、formula cacheをpreviewと比較する。
- AllocationValid≠1またはtechnical blank時はFinalRaw/FinalScoreがblank。

Open XML reopen validationと独立FormulaPreflightValidatorの両方を実測してください。
```

---

## UC-06: Final workbookとoutput path

### STP-TR-11: 4 app-owned sheets、元sheet保持、name collision

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-11
Requirement: TR-11 / AC-013

厳守: 一時synthetic workbookだけをwrite対象にし、repository sampleを変更しない。cell本文は証跡へ出さない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / input identity / sheet set / formula counts / validation / Status / 差異。

正常runのfinal workbookをread-only reopenし、次を確認してください。
- inputの全sheetが同じ順序・名前・part identityで保持される。
- app-owned sheetはConfig、References、Results、Runのexact 4件。
- finalにはCheckpoint sheetがない。
- ReferencesとRunはformula-free。
- ConfigのformulaはAllocationTotal/AllocationValidの2件だけ。
- Results formulaはclosed expected planとexact一致。
- untrusted textは先頭が`= + - @`でもInlineString。
- inputに同名または大小文字違いのsheetがある場合、既存sheetを変更せず` (2)`、` (3)`の最小suffixを使う。
- Run sheetへactual 4 sheet namesを記録する。
- OpenXmlValidator error 0。

input/outputのSHA-256を混同せず、input identityが不変であることも確認してください。
```

### STP-TR-12: result naming、suffix、directory、race、atomic final

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-12
Requirement: TR-12 / AC-013

厳守: 一時directoryだけを使用し、既存利用者fileを削除・上書きしない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / case / pathsは必要ならbasenameだけ / 期待 / 実測 / Status / 差異。

固定local minute 2026-09-02 14:35でOutputPathPlannerを検証してください。
- default directoryは`<input>/../result`で、なければ作成する。
- firstは`eval-20260902-1435.xlsx`と`eval-20260902-1435.partial.xlsx`。
- finalまたはpartialのどちらかが存在すれば、次は共通suffix`-02`。
- `-02`のどちらかが存在すれば`-03`。
- fileだけでなく同名directoryもoccupiedとして扱う。
- reserve後に第三者がfinalを作った場合、上書きせず`TARGET_EXISTS`。
- target-local working copyをflush/close/reopen/validateし、input identity再確認後にsame-volume no-overwrite moveする。
- move前fault/cancel/input drift/output invalidではfinalを作らない。
- move後例外でもfinal存在・temp不存在を確認できた場合だけSUCCESS。

inputと既存targetのbytesが全caseで不変であることを確認してください。
```

---

## UC-07: Checkpointとresume

### STP-TR-13: Partial create、chunk/hash、atomic update fault

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-13
Requirement: TR-13 / AC-014

厳守: 一時synthetic inputだけを使う。checkpoint payload本文、reference、reason、evidenceを証跡へ出さない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / package structure / metadata dimensions / fault / 期待 / 実測 / Status / 差異。

CheckpointStoreで次を検証してください。
- run開始時にinput byte-copy + `Quantification_Checkpoint`だけの`.partial.xlsx`をCreateNewする。
- inputに同名sheetが大小文字違いで存在する場合は作成を拒否する。
- METAはSchemaVersion、PayloadSha256、ChunkCount。
- PAYLOADはcanonical UTF-8 JSONを各30,000 characters以下、0-based連続indexで保存する。
- unknown row/column/property、duplicate key/chunk、missing chunk、index gap、hash mismatch、unsupported schemaを拒否する。
- reference完了およびcomplete student rowごとにtarget-local tempを作り、reopen validation後にFile.Replaceする。
- replace前のwrite/flush/validation/cancel/replace faultで旧partialのhashが変わらない。
- replace後例外はnew payloadが実在・validな場合だけSUCCESS。
- tempはresume sourceにならず、通常faultではcleanupされる。
- input SHA-256/size/mtime drift時は更新しない。

normal/special/similarity/token usageを1つのCheckpointCompletedRowとしてround-tripしてください。
```

### STP-TR-14: Resume mismatch matrix、deep validation、completed-row skip

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-14
Requirement: TR-14 / AC-015

厳守: mismatchごとにcheckpoint copyを作り、正本partialを書き換えない。回答・reference・evidence本文をerrorへ出さない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / mismatch dimension / expected code / AI call count / checkpoint mutation有無 / Status / 差異。

有効partialから次を単一原因で変え、resumeを試してください。
- checkpoint schema
- input path/SHA-256/size/mtime
- definition canonical JSON/SHA-256
- normal model ID
- reference/similarity model `auto`
- app major compatibility
- CLI version/SHA-256
- SDK version
- preserved source worksheet
- completed row number/order/expected IDs
- accepted normal range/evidence source
- special/similarity IDと0〜1 range

期待:
- 各mismatchを具体的safe codeで拒否し、AI call、checkpoint update、final createは0。
- valid resumeはsaved referenceを再生成しない。
- complete rowだけskipし、最初の未完了rowから続行する。
- row途中の結果はskipせず、そのrow全体を再実行する。
- processを分けてpartialをread-only reopenしても同じ動作。
- success後だけpartialを削除。cleanup failureはfinalを無効にしない。
```

---

## UC-08: AI failure、retry、cancel

### STP-TR-15: Auth/timeout/network/schema/cleanup/cancel/no-send

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-15
Requirement: TR-15 / AC-012 / AC-016

厳守: Live障害を人為的に起こさず、fake transportとcontrolled cancellationを使う。exception本文やsession contentを証跡へ出さない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / operation kind / failure kind / attempt count / cleanup calls / result status / Status / 差異。

normal、reference、special、similarityの各operationで次を検証してください。
- schema invalidは規定回数だけ再試行後にoutput invalid。
- timeout/networkはfinite transient retry後に`AI_TIMEOUT`/`NETWORK_FAILED`。
- auth failureは無駄に再試行せず`AUTH_REQUIRED`。
- cleanup failureは`CLEANUP_FAILED`で新規operationをinterlockする。
- start前cancelはattempt 0。
- in-flight cancelはabort→dispose/deleteをfinite timeoutで行う。
- cancel受付後、semaphore待機中operationはrunnerへ到達せず、新規AI sendは0。
- 完了済みreference/rowだけcheckpointへ残り、in-progress row全体はdurable completeにならない。
- observer/progress callback例外はrun dataを変えない。

attempt/session/abort/dispose/delete countをtest doubleから取得し、期待に合わせて書き換えないでください。
```

---

## UC-09: Privacy、literal、no-content log

### STP-TR-16: Selected same-row isolationとformula injection防止

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-16
Requirement: TR-16 / AC-019

厳守: 合成canaryだけを使い、実在学生本文を扱わない。canary値自体を最終レポートへ掲載せず、一致/不一致だけを記録する。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / operation / sent source IDs / forbidden canary absent/present / log result / workbook cell type / Status / 差異。

4 operationのpayloadを確認してください。
- reference: question textだけ。student row、path、他questionを含めない。
- normal: current rowのselected primary/supporting + definition/schema metadataだけ。
- special: current rowのselected special primary/supportingだけ。
- similarity: current row primary + same question referenceだけ。

各payloadへ未選択列、別row、workbook pathのcanaryを置き、rendered Prompt、tool args、logsにないことを確認してください。

Results/References/Config/Checkpointへ`=`, `+`, `-`, `@`で始まる合成textを渡し、app-owned formula以外はInlineStringでCellFormulaを持たないことを確認してください。loggerのpublic surfaceにfree-text/Exception parameterがなく、Prompt、response、reference、reason、evidence、credential、path、token値を出力しないことも確認してください。
```

---

## UC-10: 4-step UIとaccessibility

### STP-TR-17: Warning、stage、resume、completion、keyboard、200%

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-17
Requirement: TR-17 / AC-016 / AC-017

厳守: manual UIで未観測の項目はheadless deterministic testで補完し、目視したと偽らない。実在回答を画面captureへ含めない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / environment・scale / step / control / 期待 / 実測 / Status / 差異。

4-step UIをkeyboard中心、render scaling 200%で確認してください。

全step共通のexact warning:
`生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません`

必須確認:
- warningは全stepで常時表示、non-focusable、non-modal、checkbox/dismiss/consentなし。
- Input: native picker/direct path、question row 1/2、mappingへTabで到達。
- Design: Base/Special/Similarity、Question Points、equalize、special editor、Imported Promptへ到達。
- Execution: auth、model、concurrency、新規/resume、output directory/partial path、start/cancelへ到達。
- progress stageはPreparing、GeneratingReferences、EvaluatingRows、SavingCheckpoint、FinalizingWorkbook、Completed/Cancelling。
- reference/row/operation counts、in-flight、final/partial pathを表示。
- completed runはResultsへ自動遷移する。
- Results: row-level QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScore、final/partial/cleanup warningを表示。
- 200%でも縦横scrollで全主要controlへ到達し、focus ringが視認可能。
- automation IDはrealized controls内で重複しない。
```

---

## UC-11: Command-line Prompt launch

### STP-TR-18: Parser、UTF-8 Prompt、explicit apply、no auto-run

```text
あなたはStudyReport Evaluator v4.0のシステムテスト担当者です。
Test ID: STP-TR-18
Requirement: TR-18 / AC-018

厳守: 合成`.txt`だけを使い、Prompt本文をerror/logへ出さない。起動しただけでAIを実行しない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / args case / parser result / UI prefill / AI call count / Status / 差異。

`LaunchOptions`とstartup compositionで次を検証してください。
- optionはordinal-ignore-caseの`--input` 0/1回と`--prompt` 0回以上だけ。
- relative pathはstartup current directory基準でabsolute化。
- Prompt順序を維持し、canonical duplicate pathは最初の1件だけ。
- unknown option、missing value、duplicate input、non-txtをsafe startup errorにする。
- Prompt loaderはUTF-8 BOMあり/なしを受理し、invalid UTF-8、0文字、32,768 UTF-16 code units以上、read failureを拒否する。
- basenameと本文をImported Promptsへ指定順に表示する。
- Promptを選択しただけではdraftを変更しない。
- selected Custom evaluatorまたはspecial itemをtargetにして「Promptを適用」した時だけtemplateをcopyする。
- 同じPromptを複数targetへ再利用できる。
- startup後のCopilot runner/session call countは0。利用者がExecutionのstartを押すまでAI送信しない。
- custom URI、daemon、background server、filename暗黙mappingを追加しない。

safe error windowへoption value、Prompt本文、private pathを表示しないことも確認してください。
```

---

## UC-12: Windows delivery

### STP-TR-19: Windows publish/package/install/bundled CLI/clean launch

```text
あなたはStudyReport Evaluator v4.0のWindows deliveryテスト担当者です。
Test ID: STP-TR-19
Requirement: TR-19 / AC-020

厳守: Windows 11 x64実環境とpwsh 7+ Coreだけを使う。Windows PowerShell 5.1へfallbackしない。credentialを収集・表示しない。今回実行していないpackage/install/launchをPASSにしない。
報告順: Test ID / OS build・architecture・pwsh・SDK / restore/build/test / package identity / install root / launch / Status / 差異。

次を順に実行・検証してください。
- global.jsonのSDK feature bandとversion 2 lockを使うlocked restore。
- Release build warning/error 0と今回のsolution test実測件数。
- RID`win-x64`、self-contained、non-trimmed、non-single-file publish。
- packageにapp、README/docs/images、install-windows.cmd/ps1、`copilot-runtime.json`、RID別bundled CLIを含む。
- manifestのschema/RID/CLI version/CLI SHA-256/SDK version/relative pathと実file hashが一致。
- Appはmanifestからpackage-relative absolute CLI pathを解決し、PATH fallbackしない。
- ZIP entriesはsingle root、safe relative path、0-byte/reparse/source/test/sample/secretなし、SHA-256 sidecar一致。
- installerはhash検証後、default `%LOCALAPPDATA%\Programs\StudyReportEvaluator`またはtest overrideへuser-local installし、Start Menu shortcutを作る。
- adminと.NET Runtime/SDK追加installを要求しない。
- ps1はpwsh 7+だけを受理し、5.1をfail closed。
- installed appを外部.NET無効化環境で起動し、startup中に異常終了しない。

bundle/install scriptsまたはCLI fileが欠落していれば、その事実をFAILとして報告し、旧v3 packageをPASSにしないでください。
```

---

## UC-13: macOS deterministic delivery

### STP-TR-20: arm64/x64 bundle、absolute CLI、sign/notary fail-closed

```text
あなたはStudyReport Evaluator v4.0のmacOS deterministic deliveryテスト担当者です。
Test ID: STP-TR-20
Requirement: TR-20 / AC-021

厳守: source/layout contractをWindowsで検査してもmacOS launch PASSと称しない。credentialなしでad-hoc署名をrelease署名と称しない。secret名や値をreportへ出さない。
報告順: Test ID / 実行platform / RID / source contract / bundle layout / architecture / gate behavior / Status / 差異。

repository sourceと、利用可能ならmacOS runnerで次をRID別に確認してください。
- `osx-arm64`と`osx-x64`は別package。universal binaryではない。
- `.app/Contents/Info.plist`、`Contents/MacOS/StudyReportEvaluator.App`、self-contained runtime、bundled CLI、manifestが存在。
- Info.plistのbundle identifier/executable/versionがclosed contractと一致。
- apphostとCLI Mach-O architectureがRIDと一致。
- GUIがshell PATHを継承しなくてもmanifest relative pathをAppContext baseからabsolute化する。
- package/sign/notary/install scriptsはmacOS/RID mismatchをfail closedする。
- Developer IDまたはnotary profileがなければpackage releaseを作らず`NOT_RUN_EXTERNAL_PREREQUISITE`を示す。
- codesign/notary/staple/spctlのどれかが失敗したらrelease可能statusにしない。
- install scriptはhash、signature、staple、architectureを検証後、default `~/Applications`へno-sudo copyする。

Windows上のsource-only検証はPASS_REQUIRED相当と分け、実macOS launch/sign/notaryを同じPASSへまとめないでください。
```

---

## UC-14: macOS external release gate

### STP-TR-21: Actual launch、codesign、notary、staple

```text
あなたはStudyReport Evaluator v4.0のcredentialed macOS releaseテスト担当者です。
Test ID: STP-TR-21
Requirement: TR-21 / AC-021

厳守: このPromptはmacOS runner、architecture一致、Developer ID Application certificate、Apple notary accessが全てある場合だけ実行する。credential値、keychain profile内容、Apple IDをreportへ出さない。どれか不足なら実行せずStatus=`NOT_RUN_EXTERNAL_PREREQUISITE`。未実行をPASSにしない。
報告順: Test ID / macOS version・architecture / RID / credential presenceはyes/noのみ / publish / codesign verify / notary status / staple / spctl / launch / install smoke / Status / 差異。

外部前提が揃う場合だけ、対象RIDの実packageで次を実行してください。
1. locked restore/build/test。
2. self-contained `.app` publish。
3. Developer ID Applicationでhardened runtime/timestamp付きcodesign。
4. `codesign --verify --deep --strict --verbose`成功。
5. packageを`xcrun notarytool submit --wait`しAcceptedを確認。
6. appへticketをstapleし、`xcrun stapler validate`成功。
7. `spctl --assess --type execute`成功。
8. package hashとarchitectureを検証し、test用`~/Applications`へinstall。
9. PATHを前提にせずapp launchし、startup livenessとbundled CLI identityを確認。

1件でも失敗したらFAIL。`codesign -`のad-hoc署名、未staple、notary未提出をPASSにしないでください。
```

---

## UC-15: Documentationとfull E2E

### STP-TR-22: Docs links、Prompt examples、screenshots、unsupported claims

```text
あなたはStudyReport Evaluator v4.0のdocumentation整合テスト担当者です。
Test ID: STP-TR-22
Requirement: TR-22 / AC-022

厳守: 文書に書かれたtest件数、hash、performance、platform PASSを実測の代わりにしない。画像に実在学生データを使わない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / docs inventory / link check / current UI comparison / screenshot provenance / platform claims / Status / 差異。

次を確認してください。
- READMEとdocs indexからgetting-started、features、custom evaluator、Prompt launch、privacy、troubleshootingへ到達できる。
- `docs/prompt-launch.md`にCopilot起動依頼Promptとアプリ評価Promptの区別、sample構造、複数の教員scenario、explicit apply/no-auto-runがある。
- input/design/execution/resultsの記述がnative picker、absolute points、special/reference/similarity、checkpoint/resume、auto final、4 final sheetsと一致。
- exact warning文がsource/test/docsで一致。
- screenshotsはproduction XAML/ViewModelをsynthetic dataでrenderし、生成手順とfake境界を記録する。
- screenshotにprivate path、real answer、credentialがない。
- unsupportedなLinux/Windows Arm64/universal macOS、未検証sign/notary、AI教育品質を対応済みと書かない。
- macOS credentialed evidenceがなければ`NOT_RUN_EXTERNAL_PREREQUISITE`と明示する。
- Markdown link targetが存在し、packageにuser docs/imagesが含まれる。

文書と実装が違う場合は文書を期待側へ書換えてPASSにせず、差異をFAILとして記録してください。
```

### STP-TR-23: Fixed-seed 531-row new/resume E2E

```text
あなたはStudyReport Evaluator v4.0のfull synthetic E2Eテスト担当者です。
Test ID: STP-TR-23
Requirement: TR-23 / AC-009〜AC-016 / AC-019

厳守: fixed-seed匿名synthetic workbookだけを使い、repository sampleをAIへ送らない。AI operationはfake transportを使い、期待rawをfixture oracleからだけ導出する。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / seed・rows・definition shape / new run / interruption point / resume / final comparison / input identity / Status / 差異。

531 total rows（header 1 + data 530）のsynthetic workbookで次を実行してください。
- 2以上のenabled questions、normal evaluator、special item、reference、similarityを含むexact 100-point definitionを作る。
- new runでreference-first、row ascending、checkpoint after each reference/rowを確認。
- midwayのcomplete row checkpoint後にprocess終了相当を発生させ、finalを作らない。
- 新しいorchestrator/process相当でpartialをread-only loadし、saved references/complete rowsをskipしてresume。
- uninterrupted runとresumed runのaccepted raw/status、formula cache、FinalRaw/FinalScoreを同じdeterministic oracleと比較。
- finalは4 app-owned sheets + original sheets、Checkpointなし。
- partialはsuccess後に削除。cleanup fault caseではfinal有効＋warning。
- input SHA-256/size/mtimeはnew、fault、resume、final後に全て一致。
- temp、partial、finalのbasenameと存在状態を記録し、cell本文は記録しない。

性能値はこのPromptで保証せず、実測する場合はAI待機を除いた別performance evidenceとして記録してください。
```

---

## UC-16: Optional advisory evidence

### STP-TR-24: Live Copilotとexternal recalculation

```text
あなたはStudyReport Evaluator v4.0のoptional advisoryテスト担当者です。
Test ID: STP-TR-24
Requirement: TR-24

厳守: required deterministic acceptanceの代替にしない。明示的な実行許可がない場合はenvironment opt-inを設定せずNOT_RUN。repository sampleや実在学生データを送らない。credential/token/Prompt/response/reason/evidence本文をreportへ出さない。WindowsのPowerShell処理はpwsh 7+ Coreだけを使う。
報告順: Test ID / advisory kind / opt-in有無 / environment / sent data class / observed safe status / artifact path / Status / 非保証事項。

A. Optional authenticated synthetic Copilot smoke:
- 明示許可と既存loginがある場合だけ、固定synthetic payload 1件で実行。
- 許容結果はPASS、SKIPPED_NOT_AUTHENTICATED、NOT_RUN、FAILED_ADVISORY。
- session/tool/schema/cleanupの成功を確認するが、教育的品質、全model互換性、real-data品質を証明しない。

B. Optional external spreadsheet recalculation:
- 登録済みExcel等と明示許可がある場合だけ、synthetic output copyをfull recalculation。
- app-owned formula cellの再計算値をCore oracle/cached previewと比較。
- inputやrepository sampleを保存しない。
- 外部spreadsheetはrequired runtimeではない。

A/Bを別statusで記録し、片方のPASSをもう片方またはrequired gateへ代用しないでください。
```

---

## 5. 推奨実行順序

1. STP-TR-01〜03でinputとsample境界を確認。
2. STP-TR-04〜06でallocation、definition、Promptを確認。
3. STP-TR-07〜10で4 AI operation、score、formulaを確認。
4. STP-TR-11〜14でfinal path、checkpoint、resumeを確認。
5. STP-TR-15〜18でfailure、privacy、UI、launchを確認。
6. STP-TR-19でWindows deliveryを確認。
7. STP-TR-20でmacOS deterministic contractを確認。
8. 外部前提が揃う場合だけSTP-TR-21を実行。それ以外は`NOT_RUN_EXTERNAL_PREREQUISITE`。
9. STP-TR-22〜23でdocsとfull E2Eを確認。
10. 明示許可がある場合だけSTP-TR-24を実行。

## 6. 最終レポート集約Prompt

```text
あなたはStudyReport Evaluator v4.0のシステムテスト結果集約担当者です。
Prompt ID: FINAL-REPORT-AGGREGATION-V4

実行記録に存在する事実だけを集約してください。未実施をPASSにせず、初回FAILと再実行結果を時系列で残してください。回答、Prompt、reference、reason、evidence、credential、token、private pathを掲載しないでください。

集約規則:
1. STP-TR-01〜24を欠番なく列挙し、各statusをPASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED / NOT_RUN_EXTERNAL_PREREQUISITEから選ぶ。
2. required deterministic、manual UI、Windows delivery、macOS deterministic、macOS external、optional advisoryを別集計する。
3. test総数、成功、失敗、skip、performance、hashは今回実測した値だけを記載する。
4. fixed oracleとLive AI結果を混同しない。
5. optional Live Copilot/external recalculationをrequired PASSへ算入しない。
6. macOS credential/runner不足はNOT_RUN_EXTERNAL_PREREQUISITEとし、signed/notarized PASSと書かない。
7. sampleはidentity/dimension/roleだけを記載し、回答本文を記載しない。
8. defectは期待、実測、再現手順、影響、source/testを記載する。
9. 未確認事項を「問題なし」と要約しない。
10. 最後に、確認済み範囲、残存未確認、unresolved blocker/high findingを分ける。
```

## 7. Test Requirement対応表

| Prompt | Primary evidence surface |
|---|---|
| STP-TR-01 | Forms/Google synthetic metadata・mapping |
| STP-TR-02 | repository sample structural profile |
| STP-TR-03 | picker・format classifier・Input UI |
| STP-TR-04 | ScoringAllocationCalculator・Design UI |
| STP-TR-05 | definition validator |
| STP-TR-06 | Prompt renderer・normal/special schemas |
| STP-TR-07 | reference runner・checkpoint/resume |
| STP-TR-08 | normal/special/similarity validators・empty/failure |
| STP-TR-09 | WeightedScoreCalculator hand oracle |
| STP-TR-10 | Formula AST/preflight/writer/reopen validator |
| STP-TR-11 | Config/References/Results/Run writers |
| STP-TR-12 | OutputPathPlanner・AtomicOutputCommitter |
| STP-TR-13 | CheckpointStore |
| STP-TR-14 | DurableQuantificationOrchestrator resume admission |
| STP-TR-15 | retry/cleanup/cancel scheduler |
| STP-TR-16 | payload isolation・literal strings・SafeLogger |
| STP-TR-17 | 4-step UI・accessibility・warning/progress |
| STP-TR-18 | LaunchOptions・PromptFileLoader・startup composition |
| STP-TR-19 | Windows publish/package/install |
| STP-TR-20 | macOS bundle/source gate |
| STP-TR-21 | credentialed macOS external release |
| STP-TR-22 | user/dev docs・screenshots・Prompt examples |
| STP-TR-23 | fixed-seed new/resume E2E |
| STP-TR-24 | optional Live/recalculation advisory |

## 8. 非保証事項

本Prompt集は次を保証または主張しない。

- AI scoreの教育的妥当性、公平性、法的適合性、組織方針適合性。
- repository sampleに含まれる個別回答の評価結果。
- high similarityによる不正行為の証明、low similarityによる回答品質保証。
- optional Live Copilotまたはexternal spreadsheet recalculationのPASS。
- credentialed macOS signing/notarizationのPASS。
- universal macOS binary、Linux、Windows Arm64 support。
- 未実施のUI操作、screenshot、package、performance、test件数。

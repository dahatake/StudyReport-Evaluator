# StudyReport Evaluator システムテスト用プロンプト集

| 項目 | 内容 |
|---|---|
| 文書用途 | システムテスト実行者またはテスト支援AIへ渡す実行プロンプト集 |
| 対象 | StudyReport Evaluator v3.0 実装 |
| 基準日 | 2026-09-01 |
| 要求正本 | `docs/requirements-definition.md` |
| 対象環境 | Windows 11 x64 / .NET 10 / Avalonia |
| 注意 | 本書はテスト結果ではない。実行していない項目をPASSとして扱わない |

> **捏造禁止:** 期待結果は実装・要求・既存の決定的テストから導出し、実測結果は実行時の証跡だけから記録する。期待結果と実測結果を混同しない。

## 1. この文書の読み方

本書の「Prompt」は、**システムテストを実行する担当者またはAIエージェントへの指示文**である。アプリの「Custom Prompt template」へ貼り付けるテストデータは、該当プロンプト内で明示的に区別する。

各テストPromptは、**コードブロック1個だけをコピーすれば使える完全版**である。共通ルール、Test ID、判定方法、報告形式を各コードブロックへ内包しているため、別の前置きとの連結は不要である。

本書は12ユースケース、42件のユースケース別テストプロンプトで構成し、末尾に補助的な最終レポート集約Promptを1件付ける。

| ユースケース | Prompt数 |
|---|---:|
| UC-01〜UC-05 | 各3 |
| UC-06 | 4 |
| UC-07 | 5 |
| UC-08〜UC-11 | 各4 |
| UC-12 | 2 |

## 2. コピー方法

1. `Ctrl+F` で実行したい `STP-xx-x` またはキーワードを検索する。
2. 直下の `text` コードブロック全体をコピーする。表示環境にコピーbuttonがある場合はそれを使い、ない場合はコードブロック内だけを選択する。
3. ブロック内に `<SYNTHETIC_XLSX>`、`<OUTPUT_DIR>`、`<MODEL_ID>` などがある場合だけ、実環境の値へ置換する。
4. コピーした1ブロックをそのままテスト担当AIまたはエージェントへ渡す。

> 各コードブロックは単独で完結する。複数ブロックを結合しない。

## 3. 共通変数

| 変数 | 意味 | 規則 |
|---|---|---|
| `<REPO_ROOT>` | repository root | 例示値を決め打ちせず、実行環境で解決する |
| `<SYNTHETIC_XLSX>` | 個人情報を含まない標準 `.xlsx` | Live AIへ使用できる唯一の入力。提供がなければ対象テストはBLOCKED |
| `<OUTPUT_DIR>` | 一時出力directory | 入力とは別の、新規fileを作成できる既存directory |
| `<MODEL_ID>` | 認証確認後にUIへ実表示されたmodel ID | 推測で入力しない |
| `<EVIDENCE_DIR>` | 機密内容を含まない証跡directory | repositoryへcommitしない |

## 4. 判定上の既知境界

- 標準 `.xlsx` 1ファイルだけが入力対象である。
- AI実行には既存のGitHub Copilot CLI loginが必要だが、Excel読込や定義編集には不要である。
- app-owned Knowledge templateとstructured-output contractは利用者が置換できない。
- Custom Promptで許可されるplaceholderは `{設問}`、`{回答}`、`{補助情報}`、`{評価項目}`、`{最小点}`、`{最大点}` の6個だけである。
- AIの有効なraw scoreはoverrideが空なら直接使う。有効なoverrideはAI値より優先する。
- 空回答、失敗、取消、欠損scoreは0ではなく空欄である。
- 出力は入力とは別の新規 `.xlsx` であり、既存fileを上書きしない。
- required pathはMicrosoft Excel、Office、LibreOffice、COM automationへ依存しない。
- optional Live Copilot smokeとoptional external spreadsheet recalculationは、repository上の既知証跡では `NOT_RUN` である。

---

## UC-01: アプリ起動と4ステップworkflow

### 説明

利用者がアプリを起動し、「入力」「定量化設計」「実行」「結果・出力」の4ステップを順に移動できることを確認する。未到達ステップへの飛び越し、前後移動、現在地表示も対象にする。これは採点結果ではなく、デスクトップshellの基本導線を検証するユースケースである。

### STP-01-A: 起動と順次navigation

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-01-A

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

現在の対象commitからWindows 11 x64向けStudyReport Evaluatorを起動し、起動直後のwindow title、初期ステップ、4個のstep button、現在・完了・未着手の文字表示を確認してください。

起動直後に「結果・出力」へ直接移動できるか試し、未到達ステップを飛び越せないことを確認してください。その後「次へ」で入力→定量化設計→実行→結果・出力の順に移動し、「前へ」で戻れることを確認してください。

期待結果:
- window titleは `StudyReport Evaluator`。
- 初期ステップは「入力」。
- 4ステップが番号と状態文で表示される。
- 起動直後に最終ステップへ直接飛び越せない。
- 一度到達したステップへは前後移動できる。

画面文言や状態が期待と違う場合は、実測文言をそのまま記録し、期待側を書き換えてPASSにしないでください。
```

### STP-01-B: Keyboard操作と200%表示

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-01-B

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

Windowsの表示倍率またはテストwindowのrender scalingを200%にして、4ステップworkflowをkeyboard中心で操作してください。Tabでfocusを移し、Enterでbuttonを実行し、各画面の縦横scrollで主要controlへ到達できることを確認してください。

最低確認対象:
- Input: file path、read-only読込、sheet、行範囲、質問mapping。
- Design: 定義名、質問navigator、evaluator、criterion、Prompt surface。
- Execution: 認証確認、model、concurrency、開始、cancel、progress。
- Results: result list、override、output path、出力、cancel output。

期待結果:
- 主要入力とbuttonにkeyboard focusが到達する。
- focusが色だけでなく視認可能な枠で分かる。
- 200%でもscrollにより操作対象へ到達できる。
- 画面外にあるという理由だけで操作不能にならない。

全controlを観測できなかった場合は、観測範囲だけを報告し、未確認部分をPASSに含めないでください。
```

### STP-01-C: 教育倫理warningの常時表示と非block性

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-01-C

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

起動後、画面上部の教育倫理warningを操作せずに4ステップを移動してください。warning内およびその周辺に同意checkbox、承認button、閉じるbutton、必須focus targetがないことを確認してください。

期待する固定文:
`AIによる定量値には誤りや偏りが含まれる可能性があります。利用目的に応じて結果を確認してください。`

期待結果:
- warningは入力画面と結果画面を含むshell上で継続表示される。
- warning自体へ操作またはfocusする必要がない。
- warning未操作のままmapping、navigation、実行、override、出力の各controlへ到達できる。
- warningはscore、weight、技術validationの値を変えない。

実行や出力が別の技術要因でblockされた場合、その要因とwarningを分けて報告してください。
```

### 根拠

- `src/StudyReportEvaluator.App/Navigation/WorkflowNavigator.cs`
- `src/StudyReportEvaluator.App/Views/MainWindow.axaml`
- `tests/StudyReportEvaluator.App.Tests/UI/MainWindowTests.cs`
- `tests/StudyReportEvaluator.App.Tests/UI/PrimaryJourneyAccessibilityTests.cs`
- `tests/StudyReportEvaluator.App.Tests/UI/EthicsWarningTests.cs`

---

## UC-02: 標準workbook読込と列mapping

### 説明

標準 `.xlsx` をread-onlyで読み込み、sheet、見出し行、回答行、主回答列、補助列を設定する。repository sampleでは回答本文を扱わず、既知のidentity、dimension、mapping候補だけを検証する。手動変更後も候補へ戻せること、file path変更時に古いmetadataが残らないことも確認する。

### STP-02-A: Repository sampleのread-only構造確認

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-02-A

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

repository sample `sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx` をInput画面からread-onlyで読み込んでください。読込前後にSHA-256、size、last-write timeを取得し、回答cell本文を出力せず比較してください。

期待する既知の構造:
- size: 661,189 bytes
- SHA-256: `446386E20BB4096561CB4AFD6D74B8EAA9D50EAE53C97F984BA7F70EBEAD0DE5`
- package parts: 18
- sheets: `Old!A1:AF531`、`Original!A1:L531`、`Final!A1:AD531`
- 初期sheet: `Original`
- header row: 1、first data row: 2、last data row: 531
- mapping候補列: F、G、H、I、J、K
- 初期未選択列: A、B、C、D、E、L
- Jは学生Promptの主回答候補で、Kが補助列候補
- 自動生成される主回答mappingはF、G、H、I、Jで、Kは単独の主回答mappingではない

期待結果:
- read-only読込に成功する。
- 候補は表示されるが、利用者が上書きできる。
- 読込前後の3要素が完全一致する。
- 回答本文を証跡へ含めない。

既知値と実測が異なる場合はFAILとし、sampleを修正して合わせないでください。
```

### STP-02-B: 手動mappingと候補再適用

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-02-B

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

個人情報を含まない `<SYNTHETIC_XLSX>` を読み込み、候補適用後に次の手動変更を行ってください。

1. 先頭の質問mappingの主回答列を別の存在する列へ変更する。
2. 主回答列とは異なる列を補助列として1件以上選ぶ。
3. 回答開始行と終了行をsheet範囲内で変更する。
4. 技術検証が通ることを確認する。
5. 「候補を再適用」を実行し、候補由来のmappingへ戻ることを確認する。
6. 選択済み補助列と同じ列を主回答へ変更し、その列が補助列から自動的に外れることを確認する。

期待結果:
- 手動変更後は手動mapping状態として表示される。
- 主回答列と同じ列は補助列として選択不能または自動解除される。
- 同じ列を別質問の主回答に使うことは妨げられない。
- 候補再適用後も利用者が再編集できる。

使用した合成header名だけを報告し、cell本文は報告しないでください。
```

### STP-02-C: File path変更による旧状態の破棄

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-02-C

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

有効な合成workbookを読み込み、sheet、snapshot、mapping候補、質問mappingが表示された状態にしてください。その後、file path欄を別の未読込pathへ編集し、読込buttonはまだ押さずに状態を確認してください。

期待結果:
- 直前workbookのmetadata、snapshot、sheet一覧、候補、質問mappingは無効化またはclearされる。
- 技術検証にはread-only確認済みworkbookが必要である旨が表示される。
- 旧workbookの候補を新pathへ誤適用できない。
- 画面やobjectの文字列表現へ旧pathやcell本文が漏れない。
```

### 根拠

- `src/StudyReportEvaluator.App/ViewModels/InputViewModel.cs`
- `src/StudyReportEvaluator.App/Workbooks/Reading/WorkbookMetadataReader.cs`
- `src/StudyReportEvaluator.App/Workbooks/Mapping/ColumnMappingSuggester.cs`
- `tests/StudyReportEvaluator.App.Tests/E2E/SampleWorkbookStructuralTests.cs`
- `tests/StudyReportEvaluator.App.Tests/UI/InputViewTests.cs`

---

## UC-03: 非対応fileと入力境界の拒否

### 説明

非対応形式、破損package、安全上の上限超過、行・列範囲不正を技術エラーとして拒否する。倫理warningとは独立した安全境界であり、拒否時にfile内容やprivate pathをerrorへechoしないことを確認する。

### STP-03-A: CSVのUI拒否

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-03-A

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

一時directoryに、実在データを含まない小さな `.csv` を作成し、Input画面で読み込んでください。

期待結果:
- workbookはloaded状態にならない。
- validation codeまたは分類は `CommaSeparatedValues`。
- 標準 `.xlsx` を使うよう案内される。
- 次の処理に使えるdesign definitionを作れない。
- error messageとobject表現にCSV本文やprivate pathが含まれない。

テスト後に一時fileだけを削除し、repository内fileは変更しないでください。
```

### STP-03-B: `.xls`、macro、protected、corrupt、unsafe packageの制御テスト

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-03-B

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

既存の決定的テスト `HostileInputAndFailureTests` と `FileFormatClassifierTests` を使い、次の単一原因caseを個別に実行してください。実在workbookを破損させず、既存test factoryが作る一時fixtureだけを使ってください。

確認対象と期待分類:
- legacy `.xls` / CFB: `LegacyBinaryWorkbook`
- CFBが`.xlsx`を装うcase: `EncryptedOrRightsProtected`
- macro-enabled package: `MacroEnabledWorkbook`
- corruptまたはtruncated ZIP: `CorruptPackage`
- 高い展開率のunsafe package: `UnsafePackage`

期待結果:
- 各caseが期待する1分類で拒否される。
- package内容はerror表現へ含まれない。
- testが実行できなければ結果を作らずBLOCKEDとする。
```

### STP-03-C: 行上限とmapping境界

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-03-C

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

既存validator testまたは個人情報を含まない制御fixtureで、次を単一原因ごとに検証してください。

1. first data row=2、last data row=20001（20,000行）は受理される。
2. first data row=2、last data row=20002（20,001行）は `SELECTED_ROW_LIMIT_EXCEEDED`。
3. 終了行が開始行より前なら `DATA_ROW_RANGE_REVERSED`。
4. Excel最大列を超える `XFE` は `INVALID_SOURCE_COLUMN`。
5. 同一質問で補助列重複は `DUPLICATE_SUPPORTING_COLUMN`。
6. 主回答列を補助列へ再利用すると `PRIMARY_COLUMN_REUSED`。

UIだけでは作れない値は境界testで検証し、UIで観測したと偽らないでください。
```

### 根拠

- `src/StudyReportEvaluator.App/Workbooks/Intake/FileFormatClassifier.cs`
- `src/StudyReportEvaluator.Core/Validation/QuantificationDefinitionValidator.cs`
- `src/StudyReportEvaluator.App/Workbooks/Mapping/ColumnMappingValidator.cs`
- `tests/StudyReportEvaluator.App.Tests/E2E/HostileInputAndFailureTests.cs`
- `tests/StudyReportEvaluator.Core.Tests/Validation/QuantificationDefinitionValidatorTests.cs`

---

## UC-04: 動的な質問・評価方法・評価項目の設計

### 説明

質問、evaluator、criterionを固定件数にせず、追加、複製、並べ替え、無効化、削除できることを検証する。3階層のweight、親rangeとcriterion固有range、enabled最小数、immutable snapshotも対象とする。

### STP-04-A: Tree編集操作

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-04-A

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

合成workbookのmappingを完了してDesign画面へ進み、質問、evaluator、criterionの各階層で次を実行してください。

- 追加
- 複製
- 上下への並べ替え
- 無効化と再有効化
- 削除

期待結果:
- 複製後のquestion/evaluator/criterion IDは元と異なる。
- 並べ替えても各nodeのIDと内容の対応は維持される。
- disabled nodeは一覧に監査用として残るが、実効weightは0%になる。
- disabled nodeと子孫は実行対象にならない。
- 編集のたびに新draftとなり、既に作成済みのsnapshotは変化しない。

IDを表示できない画面では、既存ViewModel testでID不変性を補完し、画面で確認したと記載しないでください。
```

### STP-04-B: 3階層weightとrange validation

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-04-B

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

Design画面で次の合成設定を作ってください。

- question 2件、weight 2と1
- 先頭question内のevaluator 2件、weight 3と1
- 先頭evaluator内のcriterion 2件、weight 1と3
- evaluator default range 0..10
- 先頭criterionだけ個別range 1..5

期待結果:
- questionの実効割合は約66.67%と33.33%。
- evaluatorの実効割合は75%と25%。
- criterionの実効割合は25%と75%。
- weight合計を100へ揃える入力は要求されない。
- 個別rangeを解除すると親range 0..10へ戻る。

続いて個別rangeを1..1、question weightを0、rounding digitsを7へ、それぞれ別々に変更してください。各caseでsnapshot作成がblockされ、原因fieldが示されることを確認し、case間で値を正常へ戻してください。
```

### STP-04-C: 動的件数とvirtualization

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-04-C

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

既存の合成fixture matrixとheadless UI testを使用し、次を確認してください。

- questions: 1 / 2 / 10
- evaluators per question: 1 / 2 / 5
- criteria per evaluator: 1 / 4 / 20
- 10 questions × 5 evaluators × 20 criteria = 1,000 criteriaの有効definition

期待結果:
- fixture matrixの27形状が有効なdeterministic snapshotになる。
- 大規模definitionのnavigatorはvirtualizeされ、全editorを同時realizeしない。
- 表示倍率については、既存testが確認する200%の結果だけを報告する。
- 実測した件数とtest結果だけを記録する。
```

### 根拠

- `src/StudyReportEvaluator.App/ViewModels/QuantificationDesignViewModel.cs`
- `tests/StudyReportEvaluator.App.Tests/UI/QuantificationDesignViewTests.cs`
- `tests/StudyReportEvaluator.Core.Tests/Fixtures/SyntheticSpecificationContractTests.cs`
- `tests/fixtures/v3/definition-matrix.json`

---

## UC-05: Knowledge coverage評価

### 説明

Knowledge evaluatorが利用者編集可能なkeyword-count Promptではなく、app-ownedのsemantic instructionを使うことを確認する。知識ポイントごとに説明・関係・適用の程度を評価し、AI出力はcriterion別rawだけとする。Live品質や教育的妥当性は保証対象にしない。

### STP-05-A: App-owned Knowledge Promptの不変性

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-05-A

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

Design画面でKnowledge evaluatorを選び、Prompt surfaceを確認してください。編集可能なCustom Prompt欄ではなくread-only previewであることを確認し、別の編集経路またはViewModel testからKnowledgeへcustom templateを設定しようとしてください。

期待結果:
- typeは `KNOWLEDGE_COVERAGE`。
- template versionは `knowledge-v1`。
- previewに「説明」「関係」「適用」「単語が存在するだけで満点にせず」の趣旨が含まれる。
- app-owned structured-output contractが付加される。
- 利用者の置換templateは採用されない。
- 別の分析方法にはCustom evaluatorを使う案内となる。

文言の存在を検証するのであり、Live AIが常に適切に意味評価するという品質保証へ拡張しないでください。
```

### STP-05-B: 合成回答に対する複数知識ポイント

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-05-B

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

明示的に合成データと確認できる `<SYNTHETIC_XLSX>` と認証済みCopilot CLIがある場合だけ実施してください。次のような合成回答を主回答に持つ1行を使い、実在学生データへ置換しないでください。

合成回答:
`過学習は訓練データに適合しすぎ、新しいデータで性能が下がる状態です。対策として検証データで性能を確認し、正則化を使います。`

Knowledge criterion:
- 過学習の定義
- 汎化性能との関係
- 具体的な対策

各criterionのrangeは0..10、weightは任意の正数としてください。

期待結果:
- 1 evaluatorにつき3 criterionが各1件だけ返る。
- rawは各criterionのeffective range内。
- evaluator/question/overall scoreや合否をAIが返す契約ではない。
- evidenceがある場合は同じ行の選択済み主回答または補助列の完全な連続substringで、source column IDが一致する。
- evidenceがない場合は空文字、`NONE`、空source column IDの組合せ。

期待点数を固定しないでください。認証または合成workbookがなければBLOCKEDまたはSKIPPED_NOT_AUTHENTICATEDとします。
```

### STP-05-C: 空主回答のdispatch抑止

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-05-C

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

合成workbook内に、主回答が空文字または空白だけで、補助列には値がある行を用意してください。その行をKnowledge evaluatorの対象にして実行経路を確認してください。

期待結果:
- 補助列に値があってもAI sessionを開始しない。
- statusは `EMPTY`。
- attempt countは0。
- Scorableは0。
- AI raw、effective raw、normalized、evaluator、question、overallは空欄。
- overrideは入力不能。

AI呼出し回数を画面だけで確認できない場合は、`EvaluationSchedulerTests` とpayload builder testの記録を証跡にし、推測値を報告しないでください。
```

### 根拠

- `src/StudyReportEvaluator.Core/Prompting/BuiltInPromptTemplates.cs`
- `src/StudyReportEvaluator.Core/Prompting/SafeEvaluationPayloadBuilder.cs`
- `tests/StudyReportEvaluator.Core.Tests/Prompting/BuiltInPromptTemplatesTests.cs`
- `tests/StudyReportEvaluator.Core.Tests/Prompting/SafeEvaluationPayloadBuilderTests.cs`
- `tests/StudyReportEvaluator.App.Tests/Workflow/EvaluationSchedulerTests.cs`

---

## UC-06: Custom Prompt評価

### 説明

Custom evaluatorで任意の選択列を主回答として使い、利用者が分析templateを編集できることを検証する。6 placeholder、literal brace、single-pass展開、補助列、validation errorを対象とする。app-owned structured-output contractはCustom templateの外側から必ず付加される。

### STP-06-A: 最小Custom template

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-06-A

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

Design画面でCustom evaluatorを追加し、アプリのCustom Prompt template欄へ次を入力してください。

[アプリ入力template開始]
次の回答を、指定された評価項目ごとに分析してください。

### 回答
{回答}

### 評価項目
{評価項目}
[アプリ入力template終了]

期待結果:
- typeは `CUSTOM_PROMPT`。
- template validationを通過する。
- previewでは `{回答}` と `{評価項目}` がpreview用値へ展開される。
- app-owned structured-output contractがpreview末尾へ付加される。
- snapshotを作成できる。

このテストはtemplate renderingの確認であり、Live AI scoreの固定値を要求しません。
```

### STP-06-B: 全placeholder、補助列、literal brace

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-06-B

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

Custom evaluatorの主回答を合成workbookのJ列、補助列をK列として、アプリのCustom Prompt template欄へ次を入力してください。

[アプリ入力template開始]
設問と同じ行の回答・補助情報だけを使い、根拠のない推測を避けてください。
説明用のliteral object例: {{"source":"selected-row-only"}}

### 設問
{設問}

### 回答
{回答}

### 補助情報
{補助情報}

### 評価項目
{評価項目}

親evaluatorの既定範囲: {最小点} から {最大点}
[アプリ入力template終了]

期待結果:
- 6個の許可placeholderが展開される。
- `{{` と `}}` はliteral braceとなり、previewに `{"source":"selected-row-only"}` が現れる。
- `{補助情報}` には選択済みK列のstable source column IDと同じ行の値だけが含まれる。
- `{最小点}` / `{最大点}` は親evaluator range、`{評価項目}` はcriterion effective rangeを表す。
- J、K以外の列、他行、workbook pathはpayloadへ含まれない。

Live実行する場合も固定点数は期待せず、result schemaとsource整合性だけを判定してください。
```

### STP-06-C: Invalid Custom templateの単一原因validation

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-06-C

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

Custom Prompt templateを次のcaseへ1件ずつ変更し、各caseの後で正常templateへ戻してから次へ進んでください。

- 空白だけ → `TEMPLATE_REQUIRED`
- `{評価項目}` だけ → `ANSWER_PLACEHOLDER_REQUIRED`
- `{回答}` だけ → `CRITERIA_PLACEHOLDER_REQUIRED`
- `{回答} {評価項目} {未知}` → `UNKNOWN_PLACEHOLDER`
- `{回答} {評価項目` → `UNCLOSED_PLACEHOLDER`
- `{回答 {評価項目}` → `MALFORMED_PLACEHOLDER`
- `{回答} {評価項目} }` → `UNMATCHED_CLOSING_BRACE`

期待結果:
- 各caseがrun開始前に該当codeで拒否される。
- invalid中はsnapshot作成またはrun開始がblockされる。
- error messageは入力したPrompt本文をechoしない。
- `{{回答}} {評価項目}` はliteral `{回答}` であり必須placeholderを満たさないため `ANSWER_PLACEHOLDER_REQUIRED`。
```

### STP-06-D: 挿入値のsingle-pass性

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-06-D

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

制御されたrenderer testを使い、主回答値へ `{設問} {{bad}} }`、criterion表示へ `{回答}` に似た文字列を含めてください。templateは `A={回答}; C={評価項目}` とします。

期待結果:
- 挿入値内部のbraceやplaceholder風文字列はそのまま残る。
- 挿入値を再走査して別placeholderへ展開しない。
- 挿入値のbraceをtemplate構文エラーとして誤判定しない。

このcaseは実在回答を使わず、既存 `PromptTemplateRendererTests.Inserted_values_are_opaque_and_never_rescanned` で検証してください。
```

### 根拠

- `docs-dev/custom-evaluator-guide.md`
- `src/StudyReportEvaluator.Core/Prompting/PromptTemplateRenderer.cs`
- `src/StudyReportEvaluator.Core/Prompting/SafeEvaluationPayloadBuilder.cs`
- `tests/StudyReportEvaluator.Core.Tests/Prompting/PromptTemplateRendererTests.cs`
- `tests/StudyReportEvaluator.Core.Tests/Prompting/SafeEvaluationPayloadBuilderTests.cs`

---

## UC-07: Copilot認証、実行、進捗、snapshot、cancel

### 説明

既存Copilot CLI loginを確認し、UIに実表示されたmodelと1〜3のconcurrencyで評価を開始する。runは開始時のimmutable snapshotへ固定し、progressとcancelを扱う。認証やnetworkがない環境ではUIの有限状態とrequired fake testsを検証し、Live成功を捏造しない。

### STP-07-A: 認証状態とsecret入力の不在

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-07-A

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

Execution画面で「Copilot 状態を確認」を実行し、実際の結果を記録してください。credential、PAT、client secretをアプリへ入力しないでください。

許容される観測状態:
- 既存login利用可能
- loginが必要
- CLI unavailable
- runtime確認失敗
- 確認取消

期待結果:
- 利用可能時だけmodel一覧とruntime identityが表示され、開始可能になる。
- 利用不可時は開始buttonが有効にならず、具体的な技術状態が表示される。
- アプリ固有OAuth app、client ID、client secret、PATの入力欄がない。
- private CLI pathやcredentialを画面・レポートへ表示しない。

認証できなかったこと自体をrequired機能のFAILにせず、Live実行部分をSKIPPED_NOT_AUTHENTICATEDとして分離してください。
```

### STP-07-B: Model、concurrency、評価単位、progress

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-07-B

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

認証済み環境と `<SYNTHETIC_XLSX>` がある場合、UIに列挙された `<MODEL_ID>` を選び、concurrencyを1、2、3へ順に変更して許可範囲だけが選べることを確認してください。小さな合成definitionでrunを1回開始し、評価単位数とprogressを記録してください。

期待結果:
- concurrency選択肢は1、2、3だけ。
- planned evaluation countは「選択行数 × enabled questionごとのenabled evaluator数」。criterion数では増えない。
- progressはtotal、completed、in-flightを表示し、completedは減少しない。
- 完了時はin-flightが0。
- model IDは実表示値を使い、文書から推測しない。

Live環境がなければ既存 `ExecutionViewTests` と `EvaluationSchedulerTests` を実行し、Liveとして報告しないでください。
```

### STP-07-C: Mid-run draft変更とsnapshot隔離

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-07-C

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

実行時間を制御できるfake boundary testでrunを開始し、最初のevaluation unitが進行中になった後、次回run用draftのPrompt、range、weight、mappingを変更してください。

期待結果:
- 現在runのdefinition hash、payload、result validation、override range、formula layoutは開始時snapshotのまま。
- 変更は次回runにだけ反映される。
- 現在runへ新旧definitionが混在しない。
- snapshot canonical JSONとSHA-256の整合性が維持される。

Live AIの応答速度に依存させず、`QuantificationOrchestratorTests.Mid_run_draft_replacement_cannot_change_current_payload_validation_override_or_formula_layout_contract` を主証跡にしてください。
```

### STP-07-D: Cancelと部分結果

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-07-D

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

複数行の合成runを開始し、少なくとも1 unitが進行中の間にcancelを1回実行してください。再現性が必要な場合はcontrolled run boundaryを使ってください。

期待結果:
- cancel受付後に新規evaluator送信を開始しない。
- 実行中unitは安全に終了またはcancelされる。
- 完了済みrawだけを保持する。
- 未完了unitは `CANCELLED` かつraw/score空欄。
- partial summaryは入力identityが一致していれば結果画面へ渡され、部分出力を許可する。
- cancel操作を複数回送信しない。
```

### STP-07-E: Schema、timeout、network、auth failureの再試行

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-07-E

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

Live障害を人為的に起こさず、`HostileInputAndFailureTests` と `RetryAndCleanupCoordinatorTests` の制御attemptを使用して、次を個別に検証してください。

- invalid schema/output: 初回+再試行1回の後 `AI_OUTPUT_INVALID`
- timeout: transient上限までの後 `AI_TIMEOUT`
- network failure: transient上限までの後 `NETWORK_FAILED`
- authentication failure: 再試行なしで `AUTH_REQUIRED`
- start前cancel: attempt 0で `CANCELLED`

期待結果:
- 不正または失敗結果にscoreがない。
- 必要なattemptごとにsession dispose/deleteを行う。
- timeout/network/cancelでは必要に応じabortしてからcleanupする。
- exception本文、Prompt、reason、evidence、tokenを結果表現へ含めない。

attempt数は実際のtest assertionから取得し、推測で報告しないでください。
```

### 根拠

- `src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs`
- `src/StudyReportEvaluator.App/Copilot/RetryAndCleanupCoordinator.cs`
- `src/StudyReportEvaluator.App/Workflow/EvaluationScheduler.cs`
- `tests/StudyReportEvaluator.App.Tests/UI/ExecutionViewTests.cs`
- `tests/StudyReportEvaluator.App.Tests/Workflow/QuantificationOrchestratorTests.cs`
- `tests/StudyReportEvaluator.App.Tests/E2E/HostileInputAndFailureTests.cs`

---

## UC-08: 結果確認、override、空欄伝播、加重計算

### 説明

AI raw、任意override、effective raw、normalized、evaluator、question、overall previewを確認する。有効overrideの優先、無効overrideのblock、空回答と失敗のblank-not-zero、rounding、3階層weightを検証する。固定数値は既存の合成oracleだけで使用する。

### STP-08-A: 有効overrideの優先

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-08-A

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

range 0..10、rounding digits 1、AI rawが存在する合成結果をResults画面へ読み込み、overrideへ `8` を入力してください。Live AIを使う場合、入力前のAI raw値は固定期待にしないでください。

期待結果:
- override errorはない。
- effective rawは8。
- normalizedは80.0。
- そのcriterionしかないevaluator/question/overallでは各80.0。
- exportは他の技術条件も満たす場合に有効。
- 出力workbookではoverrideはliteral、effective以降はformula cell。
```

### STP-08-B: 無効overrideはAIへfallbackしない

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-08-B

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

range 0..10で有効なAI rawがある合成結果に、override `11` を入力してください。

期待結果:
- `OVERRIDE_OUT_OF_RANGE` 相当のfield errorが表示される。
- effective raw、normalized、evaluator、question、overallは空欄になる。
- 有効なAI rawへ黙ってfallbackしない。
- exportはblockされ、output boundaryを呼ばない。

続いてoverrideを空欄へ戻し、有効AI rawが再びeffective rawになることを確認してください。
```

### STP-08-C: 空回答・欠損・取消のblank-not-zero

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-08-C

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

制御された結果で次の行を別々に確認してください。

1. 空主回答: `EMPTY`、Scorable=0。
2. 非空主回答だがAI rawとoverrideがない。
3. cancelされた未完了unit: `CANCELLED`。
4. AI output invalidだが有効override 5がある。

期待結果:
- case 1〜3のraw/effective/normalized/aggregateは0ではなく空欄。
- case 1はoverride不能。
- case 2と3は有効overrideがなければblank chainを維持する。
- case 4はrange 0..10ならeffective=5、normalized=50.0となり、status自体は元のAI failureを保持する。
```

### STP-08-D: 固定hand-calculated oracle

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-08-D

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

`tests/fixtures/v3/result-oracles.json`、`WeightedScoreCalculatorTests`、`ResultsSheetWriterTests` を使い、次の固定合成oracleだけを検証してください。

- criterion A: raw 25、range 0..30、weight 2 → normalized 83.3
- criterion B: raw 8、range 1..10、weight 1 → normalized 77.8
- evaluator weighted score → 81.5
- midpoint 81.45 → 81.5
- midpoint -81.45 → -81.5

期待結果:
- criterionを先にroundし、親はround済みchildを入力に使う。
- midpointはaway-from-zero。
- formula cached valueと独立したCore oracleが一致する。
- disabled childは分母・blank countから除外され、enabled blank childは親を空欄にする。

この固定値をLive AIの期待点へ流用しないでください。
```

### 根拠

- `src/StudyReportEvaluator.App/ViewModels/ResultsOutputViewModel.cs`
- `src/StudyReportEvaluator.Core/Scoring/WeightedScoreCalculator.cs`
- `tests/StudyReportEvaluator.App.Tests/UI/ResultsOutputViewTests.cs`
- `tests/StudyReportEvaluator.Core.Tests/Scoring/WeightedScoreCalculatorTests.cs`
- `tests/StudyReportEvaluator.App.Tests/Workbooks/Writing/ResultsSheetWriterTests.cs`
- `tests/fixtures/v3/result-oracles.json`

---

## UC-09: 別workbookへの安全な出力

### 説明

入力を変更せず、Config、Results、Runの3 sheetを持つ別の `.xlsx` を作る。working copyのwrite、flush、close、reopen、validate、入力identity再確認後にだけ完成名へatomic commitする。既存file、同一path、入力変更、sheet名衝突も対象とする。

### STP-09-A: 正常な別file出力

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-09-A

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

合成run完了後、入力とは異なる既存directory内の未使用path `<OUTPUT_DIR>\result.xlsx` を指定して出力してください。出力前後に入力のSHA-256、size、last-write timeを比較し、出力をOpen XMLでread-only再オープンしてください。

期待結果:
- output statusは成功。
- 入力3要素は完全一致。
- outputは入力と別path。
- 元sheetが保持される。
- `Quantification_Config`、`Quantification_Results`、`Quantification_Run` が追加される。
- ConfigとRunはformulaを持たず、Resultsのeffective/normalized/evaluator/question/overallはformulaを持つ。
- formula cellには利用可能なcached previewが保存される。
- calculation modeはautomaticで、full calculation on loadが有効。
- 完成後に一時working fileが残らない。

workbook内容をレポートへ転記せず、sheet名、cell type、formula数、hashだけを証跡にしてください。
```

### STP-09-B: 同一pathと既存targetの拒否

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-09-B

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

結果画面で次を別々に試してください。

1. output pathを入力workbookと同一pathにする。
2. output pathを既に存在する `.xlsx` にする。
3. output extensionを `.xlsx` 以外にする。
4. 存在しないdirectoryを指定する。

期待結果:
- すべて出力buttonがblockされるか、output boundaryが安全に拒否する。
- 既存targetでは `TARGET_EXISTS` 相当が表示され、上書きしない。
- 入力fileと既存targetのhashは変化しない。
- applicationが自動で別名上書きを決定しない。
```

### STP-09-C: Input changedとoutput validation failure

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-09-C

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

repository sampleではなく一時copyまたは既存atomic fixtureだけを使って、次の単一原因caseを検証してください。

A. run snapshot取得後からfinal rename直前までに入力の1 byteを変更し、sizeとmtimeは元へ戻す。
B. output validatorを制御して検証失敗を返す。

期待結果:
- AはSHA-256差異を検出して `INPUT_CHANGED`。
- Bは `OUTPUT_INVALID`。
- どちらも完成名を作らない。
- 一時fileをbest effortで削除する。
- Aでは変更後入力を元の正常inputと偽らない。テストfixtureのdisposeに任せるか、明示的に破棄する。

手動でrepository sampleを変更してはいけません。
```

### STP-09-D: App-owned sheet名衝突

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-09-D

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

一時合成workbookへ、既存の `Quantification_Config`、`Quantification_Results`、`Quantification_Run` sheetをあらかじめ作り、出力してください。

期待結果:
- 既存sheetを変更または削除しない。
- 新規sheetは `Quantification_Config (2)`、`Quantification_Results (2)`、`Quantification_Run (2)` のような最小の空きsuffixを使う。
- 大文字小文字だけが違う既存名も衝突として扱う。
- Run sheetへ実際に割り当てた3 sheet名を保存する。

suffixが連続していない複雑caseでは、`AppOwnedSheetNameResolverTests` の実測期待値を使用し、推測しないでください。
```

### 根拠

- `src/StudyReportEvaluator.App/Workbooks/Writing/WorkingPackage.cs`
- `src/StudyReportEvaluator.App/Workbooks/Writing/AtomicOutputCommitter.cs`
- `src/StudyReportEvaluator.App/Workbooks/Validation/OutputPackageValidator.cs`
- `tests/StudyReportEvaluator.App.Tests/UI/ResultsOutputViewTests.cs`
- `tests/StudyReportEvaluator.App.Tests/Workbooks/Writing/AppOwnedSheetNameResolverTests.cs`
- `tests/StudyReportEvaluator.App.Tests/E2E/HostileInputAndFailureTests.cs`

---

## UC-10: AI result validation、privacy、formula injection防止

### 説明

AIへ送るsourceを同じ行の選択済み列へ限定し、closed result schema、exact evidence、no-content logging、one-tool capabilityを検証する。AI由来文字列をExcel formulaとして実行しないことも確認する。

### STP-10-A: Selected same-row sources only

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-10-A

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

既存 `SafeEvaluationPayloadBuilderTests.Payload_contains_only_selected_same_row_sources_and_stable_column_ids` と同等の合成canaryを使ってください。

同じ行:
- 選択済みprimary G
- 選択済みsupport K、L
- 非選択Aに別canary
- fake file pathに別canary

別行:
- Gに別canary

期待結果:
- payload sourcesはG、K、Lだけ。
- source kindはprimary 1件、supporting 2件。
- 非選択A、別行G、file pathのcanaryはrendered Promptへ含まれない。
- objectの通常文字列表現は本文をredactする。

証跡にはcanaryの一致/不一致だけを記録し、実データを使用しないでください。
```

### STP-10-B: Closed AI resultとexact evidence

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-10-B

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

制御されたresult validatorへ、次を単一原因ごとに入力してください。

- evaluator IDの大文字小文字違い
- criterion欠落
- criterion重複
- unknown criterion
- range外raw
- primaryを名乗るがsource column IDが違う
- evidenceが指定sourceの連続substringではない
- 未送信supporting columnを名乗る
- `NONE`なのにevidenceまたはsource column IDが非空

期待結果:
- payload全体を拒否し、AcceptedResultはnull。
- 一部criterionだけを採用しない。
- clampまたは0点化しない。
- errorはreason/evidence/回答本文をechoしない。
- 正常な `NONE` は空evidenceと空source column IDの組合せだけ受理する。
```

### STP-10-C: Excel formula markerのliteral保存

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-10-C

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

reason/evidence相当の合成文字列として次の4値をResults writerへ渡してください。

- `=E02-FORMULA-CANARY`
- `+E02-FORMULA-CANARY`
- `-E02-FORMULA-CANARY`
- `@E02-FORMULA-CANARY`

期待結果:
- 各値はInlineStringとして保存される。
- CellFormulaとCellValue nodeを持たない。
- Excel式として評価されない。
- writerやresult objectのToStringへcanary本文を含めない。

実在回答ではなく既存hostile testの合成値だけを使ってください。
```

### STP-10-D: One-tool capabilityとno-content logging

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-10-D

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

`CapabilityBoundaryTests` と `SafeLoggerCanaryTests` を実行し、Copilot sessionとlogging surfaceを確認してください。

期待結果:
- 公開toolは `submit_quantification` 1件だけ。
- MCP server、command、custom agent、skill、plugin、instruction directory、GitHub write toolは0件。
- file tracking、host Git operation、memory、session store、tool search、config discoveryは無効。
- loggerのpublic write surfaceにfree textまたはException parameterがない。
- 全severityでcode、安全な数値、生成済みsession identity以外を記録しない。
- Prompt、response、reason、evidence、token、pathのcanaryはlogへ出ない。
```

### 根拠

- `src/StudyReportEvaluator.Core/Validation/QuantificationResultValidator.cs`
- `src/StudyReportEvaluator.App/Copilot/EvaluationSchemaFactory.cs`
- `src/StudyReportEvaluator.App/Logging/SafeLogger.cs`
- `tests/StudyReportEvaluator.Core.Tests/Validation/QuantificationResultValidatorTests.cs`
- `tests/StudyReportEvaluator.App.Tests/Copilot/CapabilityBoundaryTests.cs`
- `tests/StudyReportEvaluator.App.Tests/Logging/SafeLoggerCanaryTests.cs`
- `tests/StudyReportEvaluator.App.Tests/E2E/HostileInputAndFailureTests.cs`

---

## UC-11: Windows x64 build、起動、package、性能

### 説明

Windows 11 x64でlocked restore、Release build/test、framework-dependent起動、self-contained publish、unsigned ZIPとSHA-256 sidecarを検証する。Office/LibreOffice/COM不要、packageへのsource・test・sample・secret混入防止も対象とする。

### STP-11-A: Locked restore、Release build、required tests

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-11-A

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

PowerShell 7+ Coreを使用し、対象commitでlocked restore、Release build、solution testsを順に実行してください。開始前に `global.json` とpackage lockの存在を確認してください。

期待結果:
- `global.json`のSDK versionは10.0.400、rollForwardはlatestPatch、prereleaseは禁止。
- locked restoreが成功する。
- Release buildがwarningをerrorとして扱う構成で成功する。
- solution testsが成功する。
- test総数、成功、失敗、skipは今回の実測値を報告し、過去文書の件数をコピーしない。
- optional Live Copilot smokeがopt-inなしで `NOT_RUN` でも、required test失敗として偽装しない。

失敗時は最初の根本原因と失敗test名を記録し、再実行で成功した場合は両方のrunを残してください。
```

### STP-11-B: Framework-dependent app起動とOffice非依存

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-11-B

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

Release build済みの通常appをWindows 11 x64で起動し、少なくともstartup probe中にprocessが生存することを確認してください。process moduleとdependency contextを検査し、Office関連runtimeを使っていないことを確認してください。

期待結果:
- PE architectureはAMD64。
- framework-dependent targetはnet10.0。
- startup直後に異常終了しない。
- Excel、Office Interop、LibreOffice、soffice moduleをloadしない。
- production sourceにCOM automation markerがない。
- test終了時にprocessを安全に終了する。

windowが表示されたという未取得screenshotは作らず、processとmoduleの実測だけを報告してください。
```

### STP-11-C: Self-contained unsigned ZIP

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-11-C

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

PowerShell 7+ Coreでrepositoryのpublish/package scriptを使用し、Windows x64 self-contained folder、unsigned ZIP、SHA-256 sidecarを生成して検証してください。

期待結果:
- package rootは `StudyReportEvaluator-win-x64`。
- apphostはAMD64で、self-contained runtime fileを含む。
- `copilot.exe`を同梱しない。
- source、tests、sample workbook、PDB、key、credential、token名file、`.env`を含まない。
- ZIP内fileは0 byteでなく、reparse pointを含まない。
- release notesは `Signing: UNSIGNED` を明示する。
- sidecar hashとZIPの実SHA-256が一致する。
- 同じ入力からの再packageでZIP長とhashが一致する。
- 改ざんcopyはsidecar検証に失敗する。

生成物はignored local artifactとして扱い、commit済み成果物と報告しないでください。
```

### STP-11-D: 531行のAI待機除外性能

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-11-D

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

Windows 11 x64で `WindowsX64PerformanceEvidenceTests.Synthetic_531_row_read_write_validation_median_is_recorded_and_within_target` を実行してください。

期待する測定契約:
- 合成531行、data row 530。
- 3回測定のmedian。
- fixture生成時間とAI待機を除外。
- scopeはclassification、metadata、mapping、copy、write、reopen-validation、atomic-commit、input-recheck。
- 目標はmedian 30秒以下。

今回生成されたJSONからOS、architecture、.NET、3 raw measurements、median、resultを報告してください。過去の3.279264秒を今回値として再利用せず、実行しなければNOT_RUNとしてください。
```

### 根拠

- `global.json`
- `Directory.Packages.props`
- `scripts/publish-windows.ps1`
- `scripts/package-windows.ps1`
- `tests/StudyReportEvaluator.App.Tests/E2E/WindowsLocalApplicationTests.cs`
- `tests/StudyReportEvaluator.App.Tests/E2E/SampleWorkbookStructuralTests.cs`
- `tests/StudyReportEvaluator.App.Tests/Packaging/WindowsPublishPackageTests.cs`

---

## UC-12: Optional advisory smoke

### 説明

required acceptanceへ算入しない任意テストである。Live Copilot接続と外部spreadsheet再計算は環境依存の補助証跡であり、未実施をPASSにしない。実行してもAI品質や全model互換性の保証にはしない。

### STP-12-A: Optional authenticated synthetic Copilot smoke

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-12-A

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

このテストは明示的な実行許可と既存Copilot CLI loginがある場合だけ実施してください。許可がなければ環境変数を設定せずNOT_RUNとします。

許可された場合だけ `STUDY_REPORT_EVALUATOR_COPILOT_LIVE_SMOKE=1` を当該test processへ設定し、`AuthenticatedSyntheticSmokeTests.Optional_authenticated_synthetic_smoke_reports_advisory_status_honestly` を実行してください。

許容status:
- `PASS`
- `SKIPPED_NOT_AUTHENTICATED`
- `NOT_RUN`
- `FAILED_ADVISORY`

期待結果:
- 合成primaryだけを送る。
- rationaleへ合成本文、reason、model IDを含めない。
- default policyはlogin probeもnetwork開始もしない。
- advisory結果をrequired gateのPASS/FAILへ置換しない。
- PASSでも教育的妥当性、実学生データ品質、全model互換性を主張しない。
```

### STP-12-B: Optional external spreadsheet recalculation

```text
あなたは StudyReport Evaluator のシステムテスト担当者です。
Test ID: STP-12-B

厳守事項: 実測または機械検証した事実だけを報告し、未実施をPASSにしない。合成oracle以外でAIの点数を固定しない。実在学生データ、credential、token、回答・Prompt・reason・evidence本文を記録しない。repository sampleは構造確認だけに使い、Live AIへ送らない。入力原本と既存の無関係な変更を変更、stash、commitしない。`<...>` の変数は実環境値へ置換し、安全に解決できなければBLOCKEDとする。Windowsでコマンドを使う場合はPowerShell 7+ Coreを使用する。
報告は Test ID / 実行日時・OS・architecture・.NET・対象commit / 前提条件 / 実施内容 / 期待結果 / 実測結果 / 機密本文を含まない証跡 / 判定 / 差異と再現手順 の順とする。判定は PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATED から選ぶ。

Microsoft ExcelまたはLibreOfficeが明示的に利用可能で、optional testの実行許可がある場合だけ、合成出力workbookのcopyを外部spreadsheetで開いて再計算してください。利用不能または未許可ならNOT_RUNとします。

期待する確認:
- fileが警告なく開けるかを実測する。
- formula再計算後の対象cell値を、同じ合成入力のCore oracle/cached valueと比較する。
- 入力原本ではなくoutput copyだけを保存する。
- 外部spreadsheet名、version、再計算対象cell ID、比較結果を記録する。

禁止:
- 外部spreadsheetをrequired runtimeと表現する。
- 未実施なのに再計算PASSとする。
- Live AIの非決定的scoreを固定oracleとして使う。
- repository sampleの回答本文を表示・記録する。
```

### 根拠

- `tests/StudyReportEvaluator.App.Tests/Copilot/AuthenticatedSyntheticSmokeTests.cs`
- `docs-dev/traceability.md`
- `README.md`

---

## 5. 推奨実行順序

1. UC-11-Aで対象commitのrestore/build/required testsを確認する。
2. UC-01でshellと非block warningを確認する。
3. UC-02とUC-03で入力境界を確認する。
4. UC-04〜UC-06でdefinitionとPrompt contractを確認する。
5. UC-07で認証・実行・cancel・failure taxonomyを確認する。
6. UC-08とUC-09でpreview、formula、atomic outputを確認する。
7. UC-10でprivacy/security境界を確認する。
8. UC-11-B〜DでWindows deliveryと性能を確認する。
9. 明示許可がある場合だけUC-12を実施する。

## 6. 最終レポート集約Prompt

```text
あなたは StudyReport Evaluator のシステムテスト結果集約担当者です。
Prompt ID: FINAL-REPORT-AGGREGATION

厳守事項: 実行記録に存在する事実だけを集約し、未実施をPASSにしない。AI raw、reason、evidence、回答本文、Prompt本文、credential、token、private pathを掲載しない。固定oracleとLive AI結果を混同しない。

これまでに実行したStudyReport Evaluatorのシステムテスト結果だけを集約してください。

集約規則:
1. Test IDごとに最新runだけで上書きせず、初回失敗と再実行結果を時系列で残す。
2. PASS / FAIL / BLOCKED / NOT_RUN / SKIPPED_NOT_AUTHENTICATEDを別集計する。
3. required deterministic、manual UI、optional advisoryを別sectionにする。
4. AI raw、reason、evidence、回答本文、Prompt本文、credential、token、private pathを掲載しない。
5. 固定oracleはfixture由来であることを明記し、Live AI期待値と混同しない。
6. optional Live Copilotとexternal recalculationの未実施を明示し、required PASSへ算入しない。
7. sampleのSHA-256/size/dimensionは実測時だけ記載し、回答本文は記載しない。
8. defectごとに再現手順、期待、実測、影響範囲、関連source/testを記載する。
9. 未確認事項を「問題なし」と要約しない。
10. 最後に、受入可否ではなく「確認済み範囲」と「残存する未確認範囲」を列挙する。
```

## 7. 実装根拠一覧

| Surface | Primary implementation | Primary deterministic evidence |
|---|---|---|
| 4-step shell / warning | `src/StudyReportEvaluator.App/Views/MainWindow.axaml` | `tests/StudyReportEvaluator.App.Tests/UI/EthicsWarningTests.cs` |
| Input / mapping | `src/StudyReportEvaluator.App/ViewModels/InputViewModel.cs` | `tests/StudyReportEvaluator.App.Tests/UI/InputViewTests.cs` |
| Dynamic design | `src/StudyReportEvaluator.App/ViewModels/QuantificationDesignViewModel.cs` | `tests/StudyReportEvaluator.App.Tests/UI/QuantificationDesignViewTests.cs` |
| Prompt rendering | `src/StudyReportEvaluator.Core/Prompting/PromptTemplateRenderer.cs` | `tests/StudyReportEvaluator.Core.Tests/Prompting/PromptTemplateRendererTests.cs` |
| Safe payload | `src/StudyReportEvaluator.Core/Prompting/SafeEvaluationPayloadBuilder.cs` | `tests/StudyReportEvaluator.Core.Tests/Prompting/SafeEvaluationPayloadBuilderTests.cs` |
| Result validation | `src/StudyReportEvaluator.Core/Validation/QuantificationResultValidator.cs` | `tests/StudyReportEvaluator.Core.Tests/Validation/QuantificationResultValidatorTests.cs` |
| Execution / cancel | `src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs` | `tests/StudyReportEvaluator.App.Tests/UI/ExecutionViewTests.cs` |
| Score / override | `src/StudyReportEvaluator.Core/Scoring/WeightedScoreCalculator.cs` | `tests/StudyReportEvaluator.Core.Tests/Scoring/WeightedScoreCalculatorTests.cs` |
| Results workbook | `src/StudyReportEvaluator.App/Workbooks/Writing/ResultsSheetWriter.cs` | `tests/StudyReportEvaluator.App.Tests/Workbooks/Writing/ResultsSheetWriterTests.cs` |
| Atomic output | `src/StudyReportEvaluator.App/Workbooks/Writing/AtomicOutputCommitter.cs` | `tests/StudyReportEvaluator.App.Tests/E2E/HostileInputAndFailureTests.cs` |
| Full synthetic path | application workflow and workbook adapters | `tests/StudyReportEvaluator.App.Tests/E2E/SyntheticQuantificationJourneyTests.cs` |
| Windows delivery | publish/package scripts | `tests/StudyReportEvaluator.App.Tests/Packaging/WindowsPublishPackageTests.cs` |

## 8. 非保証事項

本プロンプト集は、次を保証または主張しない。

- AI scoreの教育的妥当性、公平性、法的適合性、組織方針適合性。
- repository sampleに含まれる個別回答の評価結果。
- optional Live Copilot smokeのPASS。
- optional Excel/LibreOffice再計算のPASS。
- macOS、Linux、Windows Arm64対応。
- signed installer、signed ZIP、notarization。
- 未実施の画面操作、screenshot、performance値、test件数。

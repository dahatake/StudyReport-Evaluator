# StudyReport-Evaluator

Excel の学習レポートの採点を数値化・定量化するツールです。

StudyReport Evaluator は、標準 `.xlsx` の回答を読み取り、動的に設定した Knowledge / Custom evaluator で評価項目ごとの数値を取得し、重み付き集計式を持つ別の `.xlsx` を作るローカルデスクトップアプリです。

## 対応範囲

| 項目 | 初版の契約 |
|---|---|
| OS / Architecture | Windows 11 x64 |
| Runtime / UI | .NET 10 / Avalonia |
| 入力 | 標準 `.xlsx` 1ファイルのみ |
| 出力 | 入力とは別パスの標準 `.xlsx` 1ファイル |
| AI | AI定量化を実行するときだけ、既存の GitHub Copilot CLI ログインを使用 |
| Spreadsheet runtime | Microsoft Excel / Office / LibreOffice / COM automation は不要 |

`.xls`、CSV、PDF、macro-enabled workbook、暗号化または権利保護された workbook は対象外です。AIを使わない読込、定義編集、既存結果の表示には Copilot CLI ログインを要求しません。

## 文書

- [要求定義書](docs/requirements-definition.md) — サンプル Excel の確認結果、機能・非機能要求、受入基準、調査出典をまとめています。
- [アーキテクチャ](docs-dev/architecture.md) — project 境界、依存方向、run と workbook のデータフローを説明します。
- [Excel / formula 契約](docs-dev/excel-contract.md) — Config / Results / Run sheet、式、空欄、丸め、atomic output の契約です。
- [Custom evaluator ガイド](docs-dev/custom-evaluator-guide.md) — 任意列、Prompt placeholder、range、weight、validation の利用方法です。

## 教育倫理上の注意

> AIによる定量値には誤りや偏りが含まれる可能性があります。利用目的に応じて結果を確認してください。

この注意は persistent non-modal banner として表示するだけの **nonblocking** な案内です。checkbox、同意、承認、必須の人手確認を要求せず、mapping、AI実行、cancel、override、Excel計算、出力を妨げたり、score や weight を変更したりしません。ファイル形式、数値範囲、式、出力先などの技術的検証は、破損・情報漏えい・計算不能を防ぐため別途適用されます。

## 4ステップの操作

1. **入力** — `.xlsx`、sheet、見出し行、データ行、質問ごとの主回答列と補助列を選びます。
2. **定量化設計** — Knowledge の知識ポイント、Custom Prompt、評価項目、range、criterion / evaluator / question の weight を設定します。
3. **実行** — Copilot CLI のログイン状態と model を確認し、immutable snapshot に固定した評価を開始します。進捗確認と cancel ができます。
4. **結果・出力** — AI raw、任意の override、effective / normalized / aggregate preview を確認し、入力とは別の `.xlsx` へ出力します。

## unsigned ZIP から実行

P-01 が生成する次の2ファイルは、リポジトリへ commit される成果物ではなく、ローカルの **generated ignored artifact** です。

- `artifacts/package/StudyReportEvaluator-win-x64.zip`
- `artifacts/package/StudyReportEvaluator-win-x64.zip.sha256`

ZIP は **unsigned** です。配布元を確認し、PowerShell 7 で sidecar の値と `Get-FileHash` の SHA-256 が一致することを確認してから展開してください。Windows の警告が表示された場合も、署名済みであるとは扱わないでください。

```powershell
Get-Content artifacts\package\StudyReportEvaluator-win-x64.zip.sha256
Get-FileHash -Algorithm SHA256 artifacts\package\StudyReportEvaluator-win-x64.zip
Expand-Archive -LiteralPath artifacts\package\StudyReportEvaluator-win-x64.zip -DestinationPath artifacts\package\run
.\artifacts\package\run\StudyReportEvaluator-win-x64\StudyReportEvaluator.App.exe
```

AI定量化を使う場合は、起動前に端末上の既存 Copilot CLI でログインを済ませてください。アプリ固有の OAuth app、client ID、client secret、PAT は入力しません。

## source から build / test / package

リポジトリルートで PowerShell 7（`pwsh.exe`、PSEdition Core）を使います。次の restore、build、test は順番に実行します。

```powershell
dotnet restore StudyReportEvaluator.slnx --locked-mode
dotnet build StudyReportEvaluator.slnx --no-restore -c Release
dotnet test StudyReportEvaluator.slnx --no-build --no-restore -c Release
```

Release build 済みのアプリを source から起動する場合は次を使います。

```powershell
dotnet run --project src/StudyReportEvaluator.App/StudyReportEvaluator.App.csproj --no-build -c Release
```

self-contained の Windows x64 folder と unsigned ZIP を生成するスクリプトは、それぞれ次のとおりです。package script は既定で publish script も実行します。

```powershell
pwsh.exe -NoLogo -NoProfile -File scripts/publish-windows.ps1
pwsh.exe -NoLogo -NoProfile -File scripts/package-windows.ps1
```

## 出力 workbook

入力 workbook 自体は変更しません。target directory 内の一意な一時 `.xlsx` へ入力をcopyし、write、flush、close、reopen、検証、入力identity再確認を終えた後だけ、既存fileを上書きしない atomic rename で完成名へ移します。

- `Quantification_Config`: immutable definition snapshot、source mapping、range、weight、rounding digits、definition SHA-256。
- `Quantification_Results`: literal の Scorable / AI raw / Override / Reason / Evidence / Status と、formula の Effective Raw / Normalized / Evaluator / Question / Overall score。
- `Quantification_Run`: input identity、definition hash、app / SDK / CLI / model identity、開始・終了時刻、件数、実際に割り当てたsheet名。

formula cell には式と Core preview の cached value を併記します。入力内に同名sheetがある場合は既存sheetを変更せず、`Quantification_Config (2)` のような一意名を使います。詳細は [Excel / formula 契約](docs-dev/excel-contract.md) を参照してください。

## privacy / security

**このリポジトリへ実在する学生の回答・氏名・メールアドレス等を追加しないでください。** sample workbook の検証では回答本文をartifactやlogへ出さず、構造metadataと入力identityだけを扱います。

- AIへ送るのは、利用者が選んだ同じ行の主回答列と補助列だけです。他行、非選択列、workbook path は送りません。
- 回答本文、Prompt、reason、evidence、token、credential をアプリlogへ記録しません。
- untrusted text は Excel の string cell として保存し、raw user formula として実行しません。
- app-owned database と cloud backend はありません。外部通信は、AI実行時に既存 Copilot CLI / GitHub Copilot を利用する経路だけです。
- 入力の SHA-256、size、last-write time を最終rename直前に完全一致で再確認します。

## 検証済み証跡

| 対象 | 状態 / 実測 |
|---|---|
| repository sample の構造 | `sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx` を read-only で確認。661,189 bytes、SHA-256 `446386E20BB4096561CB4AFD6D74B8EAA9D50EAE53C97F984BA7F70EBEAD0DE5`、`Old!A1:AF531`、`Original!A1:L531`、`Final!A1:AD531`、入力identity不変、回答値のartifact出力なし。 |
| P-01完了時点（D-01文書テスト追加前） | Release solution tests は 443件すべて成功、失敗0、skip 0。 |
| Windows x64 性能記録 | 本D-01作業セッションで提示された E-02 の `synthetic-531-row-read-write-validation` 3回の記録は中央値 **3.279264 秒**。AI待機とfixture生成を除外し、classification / metadata / mapping / copy / write / reopen-validation / atomic-commit / input-recheck を測定した値で、端末や再実行に依存するため保証値ではありません。 |
| optional live Copilot smoke | `NOT_RUN` — required fake-transport tests だけが GATE-AI の PASS を決定。 |
| optional external recalculation smoke | `NOT_RUN` — required formula oracle / cached-value reopen tests だけが GATE-EXCEL の PASS を決定。 |
| 実画面 | この作業では実画面を取得していないため掲載していません。 |

## 制限と非保証

初版は Windows 11 x64 のみを対象とします。macOS、Linux、Windows Arm64、installer、code signing / notarization はサポート対象外です。AI出力や本ツールについて、採点品質、教育的妥当性、公平性、組織方針への適合性、法的適合性を保証しません。warning は確認を促す案内であり、mandatory human review、倫理承認、合否判定のgateではありません。

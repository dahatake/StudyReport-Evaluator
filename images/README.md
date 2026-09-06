# Documentation screenshots

このフォルダーは、StudyReport Evaluatorの操作説明に使う実際のAvalonia viewのPNGを保存します。

> **対象版:** 7枚の画像は**UNRELEASED（未リリース）の`0.8.4`候補**のUI説明用で、公開`0.8.1`の画面を示すものではありません。設問text同期・固定表示の採点計算式・幅に応じたカード配置を`0.8.1`の機能として扱わないでください。既存PNGは生成後のcode/UI変更の検証証跡ではありません。

## Provenance

- 生成日: 2026-09-05
- renderer: Avalonia 12.1.1 Headless + Skia
- size: 1440 × 1050 pixels
- generator source: `tests/StudyReportEvaluator.App.Tests/UI/DocumentationScreenshotTests.cs`（repositoryでのみ利用。配布ZIPにはsourceを含めない）
- definition/question/evaluator/criterion IDs: fixture内で固定し、同一環境での2回描画が一致することを検証。本番のID生成は変更しない
- input state: header 1行 + 回答100行 × 2設問を模した合成workbook metadata
- displayed path: `C:\Synthetic\StudyReport-100x2.xlsx`
- design state: Base 60、Special 0、Question points 20/20、Similarity penalty weight 0.1
- Copilot state: bundled CLI 1.0.79を模したfake authentication boundary。live loginやnetwork requestは実行していない
- login UI state（05再生成時）: `NotChecked`（認証未確認）・ログイン未開始の合成状態。ログインボタンとstatusは認証済み／live loginの証跡ではない。password・token・device codeなどの資格情報は含めない。modelは未選択で、Autoの選択は06/07用のfake状態確認後のみ
- run state: production durable orchestratorをfake row/AI/checkpoint/output boundariesで実行。Reference 2 + 100行 × Normal 2 + Similarity 2 = 402 operations
- result state: fake runnerが返すraw 8、similarity 0.2、任意override 9。AI品質や実データ結果を表さない
- output state: synthetic final/partial pathと、別名override出力候補。実fileを作成した証跡ではない
- personal/student data: なし

画像はproduction XAML / ViewModelをheadless Skiaでrenderした画面です。物理ディスプレイを撮影したものではなく、OS window chromeも含みません。UI layoutの説明には使用できますが、Windowsのfont rendering、DPI、window decorationを保証する証跡ではありません。

## Image index

| File | 表示内容 | 主な確認項目 |
|---|---|---|
| [`01-input-workbook.png`](01-input-workbook.png) | 入力画面上部 | native picker、full path、read-only読込、sheet、質問文行、回答行、候補 |
| [`02-input-mapping.png`](02-input-mapping.png) | 入力mapping | question表示名、設問text、primary / supporting columns |
| [`03-design-knowledge.png`](03-design-knowledge.png) | 設問一覧とKnowledge選択 | 採点計算式、現在値、2設問カード、選択カード |
| [`04-design-custom-prompt.png`](04-design-custom-prompt.png) | Custom設計 | 採点計算式、適用先、editable Prompt、required placeholders、criterion |
| [`05-execution-auto.png`](05-execution-auto.png) | 実行準備（合成の認証未確認・ログイン未開始） | GitHubにログイン、状態確認、未開始status、concurrency 2、new/resume、output directory（Auto選択前） |
| [`06-results-review.png`](06-results-review.png) | 結果review | 402 operations、自動final path、row別配点・減点・Final score |
| [`07-output-export.png`](07-output-export.png) | override出力 | synthetic raw/overrideと未使用の別名output path |

## Regeneration

### 05のみ（一時生成・比較・review用）

05だけの更新では、次のtestを完全一致で選択します。既存の7枚生成testと再現性testはそのまま維持しています。

- test: `StudyReportEvaluator.App.Tests.UI.DocumentationScreenshotTests.Execution_documentation_screenshot_renders_are_repeatable`
- filter: `FullyQualifiedName=StudyReportEvaluator.App.Tests.UI.DocumentationScreenshotTests.Execution_documentation_screenshot_renders_are_repeatable`
- optional opt-in: `STUDY_REPORT_EVALUATOR_GENERATE_EXECUTION_DOC_IMAGE=1`（比較済みPNGをreview用artifactへ残す場合だけ設定）
- artifact: `artifacts/test/documentation-execution-05/first/05-execution-auto.png` と `artifacts/test/documentation-execution-05/second/05-execution-auto.png`（gitignore対象）

testは共有fixtureの画面遷移を再利用し、別々の一時directoryへ**05のみを2回**描画します。各directoryに05以外のfileがないこと、file size、1440 × 1050 pixels、SHA-256の一致、ログインボタン／statusの表示・位置と未確認状態を検証し、fake runへは進みません。一時画像は成功・失敗を問わず削除します。専用opt-inがある場合だけ、検証成功後に上記2枚のartifactを上書き保存します。

このtestは`images/`へコピーしません。親タスクがtest成功と今回の2枚の一致を確認し、画像をreviewしてから、**05のPNGだけ**を`images/05-execution-auto.png`へコピーしてください。コピーするまで既存PNGは更新されません。他6枚は変更しません。旧opt-in `STUDY_REPORT_EVALUATOR_GENERATE_DOC_IMAGES`は不要で、05だけの更新にclass全体のfilterや下記の7枚更新手順を使わないでください。

### 7枚すべて（既存の公開画像更新）

PowerShell 7でrepository rootからvisual documentation testを明示的にopt-inして再生成します。

```powershell
$previousGenerateImages = $env:STUDY_REPORT_EVALUATOR_GENERATE_DOC_IMAGES
try {
	$env:STUDY_REPORT_EVALUATOR_GENERATE_DOC_IMAGES = '1'
	dotnet test tests\StudyReportEvaluator.App.Tests\StudyReportEvaluator.App.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~DocumentationScreenshotTests'
	if ($LASTEXITCODE -ne 0) { throw 'Documentation screenshot validation failed.' }
}
finally {
	$env:STUDY_REPORT_EVALUATOR_GENERATE_DOC_IMAGES = $previousGenerateImages
}
```

通常のtest runも現行UIを一時directoryへ描画し、主要controlの位置、pixel dimensions、2回生成の一致を検証します。既存PNGの存在・file size・pixel dimensionsも検査しますが、repository画像を書き換えるのは上記opt-in時だけです。一時画像はtest後に削除します。同一環境での再現性を別OS／font／DPIのpixel一致へ一般化しません。

## Source

Avalonia Headlessのframe capture方法: Avalonia公式 [Setting up the headless platform](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform)（2026-09-01確認）。

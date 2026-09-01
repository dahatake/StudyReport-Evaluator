# Documentation screenshots

このフォルダーは、StudyReport Evaluatorの操作説明に使う実際のAvalonia viewのPNGを保存します。

## Provenance

- 生成日: 2026-09-01
- renderer: Avalonia 12.1.1 Headless + Skia
- size: 1440 × 1050 pixels
- generator: [`DocumentationScreenshotTests.cs`](../tests/StudyReportEvaluator.App.Tests/UI/DocumentationScreenshotTests.cs)
- input state: 100名 × 2設問を模した合成workbook metadata
- displayed path: `C:\Synthetic\StudyReport-100x2.xlsx`
- Copilot state: fake authentication boundary。live loginやnetwork requestは実行していない
- result state: fake runnerが返す合成raw score。AI品質や実データ結果を表さない
- personal/student data: なし

画像はproduction XAML / ViewModelをheadless Skiaでrenderした画面です。物理ディスプレイを撮影したものではなく、OS window chromeも含みません。UI layoutの説明には使用できますが、Windowsのfont rendering、DPI、window decorationを保証する証跡ではありません。

## Image index

| File | 表示内容 | 主な確認項目 |
|---|---|---|
| [`01-input-workbook.png`](01-input-workbook.png) | 入力画面上部 | full path、read-only読込、sheet、header/data rows、候補 |
| [`02-input-mapping.png`](02-input-mapping.png) | 入力mapping | question表示名、設問text、primary / supporting columns |
| [`03-design-knowledge.png`](03-design-knowledge.png) | Knowledge設計 | evaluator名、weight、range、read-only semantic Prompt |
| [`04-design-custom-prompt.png`](04-design-custom-prompt.png) | Custom設計 | editable Prompt、required placeholders、supporting column |
| [`05-execution-auto.png`](05-execution-auto.png) | 実行準備 | fake login state、Auto、concurrency 2、200 units、retry込み最大600 attempts |
| [`06-results-review.png`](06-results-review.png) | 結果review | synthetic raw、override、effective、normalized、aggregate |
| [`07-output-export.png`](07-output-export.png) | output | safe synthetic output path、atomic output section |

## Regeneration

PowerShell 7でrepository rootからvisual documentation testを明示的にopt-inして再生成します。

```powershell
$env:STUDY_REPORT_EVALUATOR_GENERATE_DOC_IMAGES = '1'
dotnet test tests\StudyReportEvaluator.App.Tests\StudyReportEvaluator.App.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~DocumentationScreenshotTests'
```

通常のtest runは既存PNGの存在、file size、pixel dimensionsだけを検証し、画像を書き換えません。

## Source

Avalonia Headlessのframe capture方法: Avalonia公式 [Setting up the headless platform](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform)（2026-09-01確認）。

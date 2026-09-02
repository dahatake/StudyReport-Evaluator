# Documentation screenshots

このフォルダーは、StudyReport Evaluatorの操作説明に使う実際のAvalonia viewのPNGを保存します。

## Provenance

- 生成日: 2026-09-02
- renderer: Avalonia 12.1.1 Headless + Skia
- size: 1440 × 1050 pixels
- generator source: `tests/StudyReportEvaluator.App.Tests/UI/DocumentationScreenshotTests.cs`（repositoryでのみ利用。配布ZIPにはsourceを含めない）
- input state: header 1行 + 回答100行 × 2設問を模した合成workbook metadata
- displayed path: `C:\Synthetic\StudyReport-100x2.xlsx`
- design state: Base 60、Special 0、Question points 20/20、Similarity penalty weight 0.1
- Copilot state: bundled CLI 1.0.79を模したfake authentication boundary。live loginやnetwork requestは実行していない
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
| [`03-design-knowledge.png`](03-design-knowledge.png) | Knowledge設計 | evaluator、range、criterion、read-only semantic Prompt |
| [`04-design-custom-prompt.png`](04-design-custom-prompt.png) | Custom設計 | editable Prompt、required placeholders、criterion |
| [`05-execution-auto.png`](05-execution-auto.png) | 実行準備 | fake login、Auto、concurrency 2、new/resume、output directory |
| [`06-results-review.png`](06-results-review.png) | 結果review | 402 operations、自動final path、row別配点・減点・Final score |
| [`07-output-export.png`](07-output-export.png) | override出力 | synthetic raw/overrideと未使用の別名output path |

## Regeneration

PowerShell 7でrepository rootからvisual documentation testを明示的にopt-inして再生成します。

```powershell
$env:STUDY_REPORT_EVALUATOR_GENERATE_DOC_IMAGES = '1'
dotnet test tests\StudyReportEvaluator.App.Tests\StudyReportEvaluator.App.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~DocumentationScreenshotTests'
```

通常のtest runは既存PNGの存在、file size、pixel dimensionsだけを検証し、画像を書き換えません。

## Source

Avalonia Headlessのframe capture方法: Avalonia公式 [Setting up the headless platform](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform)（2026-09-01確認）。

# StudyReport Evaluator 利用者ドキュメント

このディレクトリは、StudyReport Evaluatorを利用する教員・評価設計者・運用担当者向けの正本です。要求所有者・QA向けの規範は[要求定義書](requirements-definition.md)、実装・保守情報は[`docs-dev/`](../docs-dev/README.md)を参照してください。

## 読者別の入口

| 読者 | 最初に読む文書 | 目的 |
|---|---|---|
| 初めて利用する教員・採点者 | [はじめに](getting-started.md) | `.xlsx`の読込から別workbook出力までを順に実行する |
| 評価方法を設計する人 | [機能リファレンス](features.md)、[Custom evaluatorガイド](custom-evaluator-guide.md) | Knowledge / Custom、range、weight、blank、overrideを理解する |
| GitHub Copilotから起動したい教員・採点者 | [GitHub CopilotからPromptで起動する](prompt-launch.md) | copy-paste用の起動依頼、評価Prompt、sample mapping、pilot／resume手順を使う |
| 情報管理・運用担当 | [データとprivacy](privacy-and-data-handling.md) | Copilotへ送る情報とoutput workbookの機密性を確認する |
| 問題を解決したい利用者 | [トラブルシューティング](troubleshooting.md) | file、Prompt、認証、run、outputの技術エラーを切り分ける |
| 要求所有者・QA | [要求定義書](requirements-definition.md)、[実装状態](../docs-dev/implementation-status.md) | frozen規範baselineとcurrent conformanceを分けて確認する |
| 画面を一覧したい人 | [スクリーンショット一覧](../images/README.md) | 7枚の合成画面と生成条件を確認する |

> [!NOTE]
> `requirements-definition.md` v3.0はGATE-0でexact bytesとhashを固定したbaselineです。本文の「実装前」は作成時点を表すため変更しません。現在の実装状態、最終gate、post-gate gapは[`docs-dev/implementation-status.md`](../docs-dev/implementation-status.md)を正本とします。baseline identityの出典は[`requirements-baseline.md`](../docs-dev/preflight/requirements-baseline.md#artifact-identity)です。

## 現在の境界

- 初版対応はWindows 11 x64、標準 `.xlsx` 1ファイル、別pathの `.xlsx` 1ファイルです。macOS、Linux、Windows Arm64、installer、code signingは対象外です。根拠: [要求定義書 §15](requirements-definition.md#15-scope)。
- AI評価には別途導入され、`PATH`から解決できる`copilot.exe`と、既存のCopilot CLI loginが必要です。Excel読込・設計編集自体にはloginを要求しません。実装根拠: [`CopilotClientFactory.cs`](../src/StudyReportEvaluator.App/Copilot/CopilotClientFactory.cs#L15-L57)。
- authenticated live Copilot smokeと外部spreadsheet再計算smokeは未実行です。決定的なfake/oracle testのPASSをlive品質の証明へ読み替えません。証跡: [`traceability.md`](../docs-dev/traceability.md#optional-advisory-evidence--never-a-required-substitute)。
- GATE-ACCEPTANCEはHEAD `62581a3`に対してPASSを記録していますが、後続の文書監査でrun前validationの既知差分を2件確認しています。詳細: [実装状態](../docs-dev/implementation-status.md)。

## 証拠の読み方

1. 現在の動作はproduction sourceを第一根拠とします。
2. deterministic testを検証根拠とします。
3. `artifacts/`のgate、performance、packageは再生成されるGit管理外の証跡です。固定baselineとして扱いません。
4. 旧要求scopeのADR・preflightは履歴であり、現行機能の説明には使用しません。

根拠: [`docs-dev/traceability.md`](../docs-dev/traceability.md#evidence-integrity-and-storage)、[`docs-dev/adr/0011-dynamic-quantification-excel-formulas.md`](../docs-dev/adr/0011-dynamic-quantification-excel-formulas.md#supersession-semantics)。

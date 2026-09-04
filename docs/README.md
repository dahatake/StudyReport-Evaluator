# StudyReport Evaluator 利用者ガイド

StudyReport Evaluatorは、標準`.xlsx`の回答をGitHub Copilotで定量化し、入力を変更せず別の`.xlsx`へ結果を作成するWindowsデスクトップアプリです。

現在の公開版は`0.8.1`です。[GitHub Releases](https://github.com/dahatake/StudyReport-Evaluator/releases)に実在するWindows ZIPとSHA-256 sidecarだけを利用してください。development MSIXは検証専用で、一般配布しません。

> [!WARNING]
> 生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません

## 読者別ガイド

| 読者 | ガイド | 内容 |
|---|---|---|
| 初めて利用する教員・採点者 | [はじめに](getting-started.md) | ZIPの確認、起動、4stepの操作、出力と再開 |
| 評価方法を設計する人 | [機能と点数](features.md) | 配点、4つのAI処理、status、Excel出力 |
| 独自の評価観点を作る人 | [Custom evaluator](custom-evaluator-guide.md) | 通常Custom評価と固有評価のPrompt |
| Promptファイルで準備する人 | [Promptファイルから起動](prompt-launch.md) | `--input`、複数`--prompt`、明示適用 |
| 情報管理・運用担当 | [データとprivacy](privacy-and-data-handling.md) | AIへ送る情報、log、final/partialの機密性 |
| 問題を解決したい人 | [トラブルシューティング](troubleshooting.md) | 入力、設計、Copilot、checkpoint、出力 |
| 画面を確認したい人 | [画面一覧](../images/README.md) | synthetic dataで生成した現行画面 |

## 対応範囲

- **OS:** Windows 11 x64
- **配布:** .NET 10 self-containedのunsigned ZIPとSHA-256 sidecar
- **入力:** 標準Office Open XML `.xlsx` 1file
- **出力:** 入力を保持した別の標準`.xlsx`
- **AI runtime:** ZIPへ同梱したGitHub Copilot CLI。PATH上の別CLIへfallbackしません
- **Spreadsheet runtime:** Microsoft Excel、Office、LibreOfficeはアプリ実行の必須条件ではありません

AI処理には、利用者本人のGitHub Copilot loginが必要です。アプリはPAT、password、client secretを入力・保存しません。loginできない場合も、アプリ起動、workbook読込、mapping、設計編集は利用できますが、新しいAI処理は開始できません。

## 対応しないもの

- `.xls`、`.xlsb`、CSV、PDF、`.xlsm`等のmacro-enabled file
- password、暗号化、rights-protected、unsafe relationshipを含むworkbook
- macOS、Linux、Windows Arm64
- installer、code signing、notarization、development MSIXの一般利用
- 保存済みfinal workbookのアプリへの再import
- definition profileだけの独立save/load
- AI品質、公平性、法的・組織policy適合性、不正行為の判定

類似度は不正行為の証明ではなく、低い類似度も回答品質を保証しません。最終的な評点と利用判断は利用者が行います。

## ライセンス

本ソフトウェアは[MIT License](../LICENSE)で提供されます。

# Changelog

StudyReport Evaluatorの利用者に影響する変更をこのファイルへ記録します。版番号は[Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)に従い、公開済み版の内容は変更しません。

## [Unreleased]

### Added

- 定量化設計の上部に、Base・Special・類似度減点係数の現在値と短い採点計算式を常時表示。設問配点は各カード、詳しい式の説明は展開欄で確認可能。
- 定量化設計の入力欄、選択欄、主要操作に、設定内容・値の範囲・計算への影響を説明するホバーヘルプを追加。

### Fixed

- 主回答列を変更すると、選択中のExcel sheetで設問text行（1行目または2行目）とその列が交差するセルの値を設問textへ即時反映するよう修正。セルが空の場合は設問textを空欄にして必須入力を表示し、設問text行を変更した場合はExcelを再読込するまで既存textを保持。
- ウィンドウ幅に応じて各画面の項目と操作を再配置し、横方向の見切れを防止。項目や結果行が多い場合も縦スクロールで全内容へ到達できるよう修正。
- 定量化設計で入力済みの全設問を折り返しカードとして同時表示し、選択した設問の評価詳細を下段で編集できるよう修正。
- 見出し行の再読込で選択sheet、手入力の質問文、配点、評価設定が消えないよう修正。空見出しへの代替文生成と、質問追加時の古い見出しの反映を防止。
- 多数の設問でも固定式ガイドが編集領域を圧迫せず、長いsheet名・補助列名を折り返し表示で確認できるよう修正。

## [0.8.1] - 2026-09-04

### Added

- Windows 11 x64向けの.NET 10 self-containedデスクトップアプリ。
- GitHub Copilot CLIを同梱し、Knowledge／Custom evaluator、Reference、固有評価、Similarityによる動的な定量化。
- student row単位のatomic checkpoint、中断後の再開、4 sheet構成のfinal workbook、criterion overrideの別名出力。
- アプリケーション版の単一正本、開発者向け版管理手順、版操作・検証tool。
- 利用者ガイド、現行UI画像、Prompt起動ガイド。
- Windows 11 x64向けself-contained ZIPとSHA-256 sidecarを、clean checkoutの回帰検証を通過した場合だけ公開するdelivery pipeline。
- 公開前のdraft作成workflowと、承認必須のprotected publish workflowへの分離。

### Changed

- 標準`.xlsx`をread-onlyで扱い、入力を変更せず別fileへatomic出力する契約へ統一。
- AIへ送るworkbook値をcurrent rowの選択済みsourceへ限定し、application logへ回答・Prompt・reason・evidence・credentialを記録しない境界を明文化。
- 開発者向け文書を`dev/docs/`へ分離し、利用者向けREADMEと`docs/`を要求v4.3へ同期。
- 初回公開のWindows配布物をself-contained ZIPとsidecarへ確定し、開発用unsigned MSIXは一般配布しない検証専用の位置づけへ変更。
- macOSの署名・公証済みDMG提供は現版scope外とし、sourceと静的契約のみ維持。
- 初回公開版を`0.8.1`へ確定。公開前候補は`0.8.0`へ再baseline済み。

### Fixed

- Windows runnerの`PATH`に複数のGit実行pathがある場合も、版・tag・clean tree検証とpublish toolが単一のapplication commandを選択できるよう修正。

<!-- 公開時は Unreleased の項目を `## [X.Y.Z] - YYYY-MM-DD` へ移し、空の Unreleased を先頭へ残します。 -->

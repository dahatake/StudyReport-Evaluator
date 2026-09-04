# Changelog

StudyReport Evaluatorの利用者に影響する変更をこのファイルへ記録します。版番号は[Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)に従い、公開済み版の内容は変更しません。

## [Unreleased]

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

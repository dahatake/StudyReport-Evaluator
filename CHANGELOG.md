# Changelog

StudyReport Evaluatorの利用者に影響する変更をこのファイルへ記録します。版番号は[Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)に従い、公開済み版の内容は変更しません。

## [Unreleased]

### Added

- Windows 11 x64向けMSIXとmacOS ARM64/x64向けDMGを、署名・公証・clean-machine証跡が揃ったplatformだけ公開するdelivery pipelineを追加中。

### Changed

- end-user setupのprimary pathを、WindowsはMSIXのInstall、macOSはDMGからApplicationsへの配置という各3操作以内へ簡素化する。
- 対応platform追加をSemVer MINORとして、次のcandidate版を`1.1.0`へ更新。

## [1.0.1] - 2026-09-03

### Changed

- `v1.0.0`ではGitHub Releaseと配布assetが作成されなかったため、`v1.0.1`を初回公開packageとし、`1.0.0` sectionの利用者向け変更をすべて含める。

### Fixed

- Windows runnerの`PATH`に複数のGit実行pathがある場合も、版・tag・clean tree検証とpublish toolが単一のapplication commandを選択できるよう修正。

## [1.0.0] - 2026-09-03

### Added

- Windows 11 x64向けの.NET 10 self-containedデスクトップアプリを初回公開。
- GitHub Copilot CLIを同梱し、Knowledge／Custom evaluator、Reference、固有評価、Similarityによる動的な定量化を追加。
- student row単位のatomic checkpoint、中断後の再開、4 sheet構成のfinal workbook、criterion overrideの別名出力を追加。
- アプリケーション版の単一正本、開発者向け版管理手順、版操作・検証ツールを追加。
- 利用者ガイド、現行UI画像、Prompt起動ガイドを追加。

### Changed

- 標準`.xlsx`をread-onlyで扱い、入力を変更せず別fileへatomic出力する契約へ統一。
- AIへ送るworkbook値をcurrent rowの選択済みsourceへ限定し、application logへ回答・Prompt・reason・evidence・credentialを記録しない境界を明文化。
- 開発者向け文書を`dev/docs/`へ分離し、利用者向けREADMEと`docs/`を要求v4.1へ同期。

<!-- 公開時は Unreleased の項目を `## [X.Y.Z] - YYYY-MM-DD` へ移し、空の Unreleased を先頭へ残します。 -->

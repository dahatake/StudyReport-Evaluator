# アプリケーション版管理手順

| 項目 | 値 |
|---|---|
| 状態 | 現行手順 |
| 対象 | StudyReport Evaluatorの製品版、assembly、checkpoint、配布候補、Git tag、GitHub Release |
| 製品版の単一正本 | [`Directory.Build.props`](../../Directory.Build.props) の`VersionPrefix` / `VersionSuffix` |
| 版操作・検証tool | [`dev/version.ps1`](../version.ps1) |
| tool self-test | [`dev/version.tests.ps1`](../version.tests.ps1) |
| 変更履歴 | [`CHANGELOG.md`](../../CHANGELOG.md) |
| Decision | [ADR-0014](adr/0014-product-versioning.md) |
| 最終更新 | 2026-09-04 |

## 1. 目的と境界

この手順は、同じ製品版がsource、App/Core project、公開binary、Git tag、変更履歴で一貫するように管理するための正本です。製品版を変更しただけで、test済み、公開済み、署名済み、またはGitHub Release作成済みとは扱いません。

現在のsource candidate版は`0.8.1`です。初回公開前であるため、2026-09-04に製品候補を0.x系列へ再baselineし、公開task完了時にPATCHを1つ進めました。過去の`1.0.0`、`1.0.1`、`1.1.0`は公開済み版ではなく、経緯は[調査証拠](../../work/20260902-version-management-investigation-evidence.json)、[release recovery記録](../../work/20260903-v1.0.1-release-recovery-plan.md)、[ADR-0015](adr/0015-windows-macos-installer-delivery.md)に保持します。

2026-09-02の公開API調査では公開GitHub Releaseとremote tagはいずれも0件でした。この観測はprivate draftの不存在を証明しません。[調査レポート §2.2](../../work/20260902-version-management-investigation-report.md#22-実測した主なcommand)

## 2. SemVer policy

製品版は[Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)の`MAJOR.MINOR.PATCH[-PRERELEASE]`を使用します。

| 要素 | 更新条件 | 例 |
|---|---|---|
| `MAJOR` | 後方互換性を壊す公開契約変更 | `1.4.2` → `2.0.0` |
| `MINOR` | 後方互換性のある機能追加。公開機能のdeprecated化も含む | `1.4.2` → `1.5.0` |
| `PATCH` | 後方互換性のあるバグ修正 | `1.4.2` → `1.4.3` |

追加規則:

1. MINOR更新時はPATCHを0へ戻します。
2. MAJOR更新時はMINORとPATCHを0へ戻します。
3. prereleaseは`1.5.0-rc.1`のように表します。通常版よりprecedenceが低く、非stableであることを示します。
4. `+metadata`はtool入力で禁止します。.NET SDKがsource revisionを`AssemblyInformationalVersion`へ追加するためです。[Microsoft Learn: Source Link](https://learn.microsoft.com/dotnet/core/compatibility/sdk/8.0/source-link)
5. 一度公開した版のtag、binary、ZIP、sidecarを差し替えません。修正は新しい版で公開します。[SemVer §3](https://semver.org/spec/v2.0.0.html#spec-item-3)
6. Git tag名は`v<SemVer>`とします。`v`はtag名のprefixで、SemVer本体には含めません。[SemVer FAQ](https://semver.org/#is-v123-a-semantic-version)

## 3. 公開互換性契約

版の上げ幅は、C#の`public` keywordだけでなく、利用者や後続処理が依存する次のsurfaceで判断します。

- 対応OS / architecture、配布形式、package layout、起動file。[ADR-0013](adr/0013-windows-only-public-release.md)、[ADR-0015](adr/0015-windows-macos-installer-delivery.md)
- `--input` / `--prompt`等のcommand-line contract。
- 受理するworkbook形式、入力不変、no-overwrite。
- final/partialのfile名、sheet名、field、formula、status、blank/zero semantics。[Excel契約](excel-contract.md)
- checkpointの読込・再開互換性。
- AIへ送るdata、log、outputのprivacy boundary。[要求定義書](../../docs/requirements-definition.md)
- Run sheetへ記録するapplication/SDK/CLI/model identity。
- 公開利用者文書で保証する操作。

`StudyReportEvaluator.Core`を独立NuGet packageとして公開するまでは、CoreのC# APIを一般利用者向けpublic APIとしては扱いません。ただしApp内部のbinary compatibilityとtestは維持します。

### 3.1 このrepositoryでのbump例

| 変更 | 原則 |
|---|---|
| 文書化済み挙動へ戻すbug fix | PATCH |
| 公開挙動を変えないdependency security update | PATCH |
| optional機能・option・対応platformの追加 | MINOR |
| 既存CLI option、sheet、fieldの削除・rename | MAJOR |
| scoreやstatusの既存意味を非互換に変更 | MAJOR |
| 過去checkpointをmigrationなしで再開不能にする | MAJOR |
| AIへ送るworkbook由来dataの範囲を拡大する | MAJOR |
| 要求書だけを改版し、binaryをreleaseしない | 製品版は変更しない |

判断できない場合は小さいbumpへ丸めず、releaseを止めて製品所有者と互換性を確認します。

## 4. 製品版と他versionの分離

次の値は製品SemVerとは別に所有します。製品版を上げたという理由だけで同時更新しません。

| Version | 現在値 | 更新条件 | Source |
|---|---:|---|---|
| 製品版 | `0.8.1` candidate | 本手順の公開契約差分 | [`Directory.Build.props`](../../Directory.Build.props) |
| 要求文書版 | `4.3` | 要求baseline変更 | [`requirements-definition.md`](../../docs/requirements-definition.md) |
| QuantificationDefinition schema | `4.0` | canonical definition format変更 | [`CanonicalDefinitionSerializer.cs`](../../src/StudyReportEvaluator.Core/Serialization/CanonicalDefinitionSerializer.cs) |
| checkpoint schema | `1` | checkpoint payload format変更 | [`CheckpointEnvelope.cs`](../../src/StudyReportEvaluator.App/Workbooks/Checkpoint/CheckpointEnvelope.cs) |
| Copilot runtime manifest schema | `1` | `copilot-runtime.json` format変更 | [`StudyReportEvaluator.App.csproj`](../../src/StudyReportEvaluator.App/StudyReportEvaluator.App.csproj) |
| Knowledge template | `knowledge-v1` | built-in Prompt semantics変更 | [`BuiltInPromptTemplates.cs`](../../src/StudyReportEvaluator.Core/Prompting/BuiltInPromptTemplates.cs) |
| .NET SDK | `10.0.400`, `latestPatch` | toolchain update | [`global.json`](../../global.json) |
| GitHub.Copilot.SDK | central declarationを参照 | dependency update | [`Directory.Packages.props`](../../Directory.Packages.props) |

schemaのbreaking changeは製品MAJORの根拠になり得ますが、逆に製品MAJORを上げただけでschemaを変更してはいけません。

## 5. `version.ps1`の契約

### 5.1 前提

- Windows PowerShell 5.1へfallbackせず、PowerShell Core 7以上を使います。
- toolはrepository rootを`dev/`の親として解決します。
- `Directory.Build.props`に`VersionPrefix`と`VersionSuffix`がそれぞれ1件だけ存在しない場合は失敗します。
- numeric componentは、現在の.NET assembly version mappingに合わせて0〜65,534へ制限します。
- prereleaseのnumeric identifierにleading zeroを許しません。
- toolは`CHANGELOG.md`、commit、tag、Releaseを自動作成しません。

### 5.2 現在版を表示

```powershell
pwsh.exe -NoLogo -NoProfile -File .\dev\version.ps1 show
```

machine-readable output:

```powershell
pwsh.exe -NoLogo -NoProfile -File .\dev\version.ps1 show -Json
```

### 5.3 任意の版を設定

まずdry-runします。

```powershell
pwsh.exe -NoLogo -NoProfile -File .\dev\version.ps1 set -Version 0.8.0-rc.1 -DryRun
```

確認後に反映します。

```powershell
pwsh.exe -NoLogo -NoProfile -File .\dev\version.ps1 set -Version 0.8.0-rc.1
```

書換えは同じdirectoryの一時fileを経由して`Directory.Build.props`へ置換します。`CHANGELOG.md`は利用者影響を人が確認して更新するため、自動編集しません。

### 5.4 MAJOR / MINOR / PATCHを上げる

```powershell
pwsh.exe -NoLogo -NoProfile -File .\dev\version.ps1 bump -Part patch -DryRun
pwsh.exe -NoLogo -NoProfile -File .\dev\version.ps1 bump -Part minor
pwsh.exe -NoLogo -NoProfile -File .\dev\version.ps1 bump -Part major
```

`bump`は指定componentを1増やし、下位componentをSemVer規則どおり0へ戻します。suffixを指定しない場合はstable版になります。prereleaseを作る場合:

```powershell
pwsh.exe -NoLogo -NoProfile -File .\dev\version.ps1 bump -Part minor -Prerelease rc.1
```

現在版がprereleaseでも、`bump -Part patch`はcoreのPATCHを増やします。同じcoreの`rc.1`から`rc.2`へ進める場合は`set`を使います。

### 5.5 sourceとprojectを検証

```powershell
pwsh.exe -NoLogo -NoProfile -File .\dev\version.ps1 verify
```

検証対象:

- `CHANGELOG.md`に`[Unreleased]`が1件ある。
- App/CoreのMSBuild `Version`、`VersionPrefix`、`VersionSuffix`が単一正本と一致する。

publish後はbinaryも検証します。

```powershell
pwsh.exe -NoLogo -NoProfile -File .\dev\version.ps1 verify -PublishedDirectory .\artifacts\package\publish\win-x64
```

App/Core DLLについて、次を検証します。

- `AssemblyVersion == MAJOR.MINOR.PATCH.0`
- `FileVersion == MAJOR.MINOR.PATCH.0`
- `ProductVersion`の`+sourceRevision`より前が製品版と一致

### 5.6 release tagを検証

公開用commitとtagを作成した後に実行します。

```powershell
pwsh.exe -NoLogo -NoProfile -File .\dev\version.ps1 verify -Tag v0.8.0 -RequireClean
```

追加検証:

- `CHANGELOG.md`に`## [0.8.0] - YYYY-MM-DD`がちょうど1件ある。
- tag名が`v` + 製品版と完全一致する。
- tagがlightweightではなくannotated tagである。
- tagが現在のHEADを指す。
- working treeがcleanである。

## 6. 通常の版上げ手順

### 6.1 変更を分類

1. 前回公開版からの利用者影響を列挙します。
2. §3の公開契約ごとにcompatible / breakingを判定します。
3. §2と§3.1からMAJOR / MINOR / PATCHを選びます。
4. schema、checkpoint、template、dependencyのversion要否を別々に判定します。
5. bump根拠をpull requestまたは変更記録へ残します。

### 6.2 変更履歴を準備

1. `CHANGELOG.md`の`[Unreleased]`へ利用者影響を追加します。
2. `Added`、`Changed`、`Fixed`、`Deprecated`、`Removed`、`Security`から適切な分類を使います。
3. 内部refactorだけなら、利用者影響がないことを確認します。
4. 回答、Prompt、学生data、credentialを履歴へ含めません。

### 6.3 版を変更

1. `show`で現在版を確認します。
2. `bump ... -DryRun`または`set ... -DryRun`で次版を確認します。
3. dry-runの結果と合意した版が一致した場合だけ、`-DryRun`なしで反映します。
4. `verify`を実行します。
5. `git diff -- Directory.Build.props CHANGELOG.md`で、意図した差分だけか確認します。

### 6.4 buildとtest

現行repositoryは[CI workflow](../../.github/workflows/ci.yml)でsample非依存のlocked restore、Release build、決定的testを実行します。`sample/SampleReport.xlsx`はGit追跡しないprivate local inputなので、正式公開前はWindows 11 x64上でcanonical sampleを含むmanual final gateも実行し、CIで代用しません。[詳細設計 §13.3](detailed-design.md#133-自動化境界)

1. locked restore。
2. Release build。
3. required Core/App tests。
4. documentation contract tests。
5. Windows publish/package tests。
6. cleanな展開先からself-contained appを起動。
7. optional smokeをrequired testの代わりにしない。

具体的なrequired gateは[Traceability — Final gate prerequisites](traceability.md#final-gate-prerequisites)を使います。

### 6.5 publish/packageを検証

1. [`scripts/publish-windows.ps1`](../../scripts/publish-windows.ps1)で`win-x64`をpublishします。
2. [`scripts/package-windows.ps1`](../../scripts/package-windows.ps1)でpublic ZIPとsidecarを作ります。
3. `version.ps1 verify -PublishedDirectory ...`でApp/Core DLLを検証します。
4. package testでZIPのsafe layout、sidecar、bundled CLI、clean extract/launch、再現性を検証します。
5. development MSIXはmanifest/version/RID、unpack、block map、bundled CLI、sidecar、policy negative、cleanupを検証し、`PASS_MECHANISM`とします。install/launchはrequiredではなく、GitHub Release assetへ含めません。
6. macOS publish/sign/notary scriptはstatic contractだけをrequiredとし、DMGまたはsupport claimを公開しません。
7. public asset名は`StudyReportEvaluator-win-x64.zip`と`StudyReportEvaluator-win-x64.zip.sha256`です。製品版はGitHub Release tag、release notes、binary/package metadata、SHA-256の組で識別します。

### 6.6 release commitとtag

1. `CHANGELOG.md`の`[Unreleased]`内容を`## [X.Y.Z] - YYYY-MM-DD`へ移し、空の`[Unreleased]`を先頭へ残します。
2. version、履歴、code、testを含むrelease commitを作ります。
3. working treeがcleanであることを確認します。
4. exact release commitへannotated tag `vX.Y.Z`を作ります。[Pro Git: Tagging](https://git-scm.com/book/en/v2/Git-Basics-Tagging)
5. `version.ps1 verify -Tag vX.Y.Z -RequireClean`を実行します。
6. commitとtagをremoteへ明示的にpushします。通常の`git push`はtagを自動送信しないため、tagのpush結果を確認します。[Pro Git: Sharing Tags](https://git-scm.com/book/en/v2/Git-Basics-Tagging#_sharing_tags)

### 6.7 GitHub Release

GitHub Releaseは既存annotated tagを対象に[release workflow](../../.github/workflows/release.yml)のmanual dispatchで作成できます。workflowを利用できない場合も、以下と同じfail-closed手順で手動作成します。

1. verified tagを選び、draft Releaseを作ります。
2. ZIPと対応する`.sha256`をdraftへ添付します。
3. asset名、size、SHA-256、tag、release notesを確認します。
4. 現行production workflowはstable版だけを受理します。prerelease packageを作る場合は別workflow decisionを先に行います。
5. assetが揃うまで公開しません。
6. repositoryでImmutable Releasesを利用できる場合は、初回公開前に設定を確認します。公開後のtag移動とasset差替えを防ぎます。[GitHub Docs: Immutable releases](https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases)
7. 公開後、実在するdownload URLを確認してから利用者文書へ記載します。

GitHubはdraftへ全assetを添付してから公開する手順を推奨しています。[GitHub Docs: Managing releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)

## 7. prereleaseとcheckpoint

現在のcheckpoint互換判定はapplication identityを`System.Version`で解析し、解析できない場合は文字列完全一致へfallbackします。[`DurableQuantificationOrchestrator.cs`](../../src/StudyReportEvaluator.App/Workflow/DurableQuantificationOrchestrator.cs)

そのため`0.8.0-rc.1`と`0.8.0-rc.2`は同じMAJORでも、現在実装ではapplication identityが完全一致せず、同じcheckpointを再開できません。toolがprereleaseを受理することは、RC間checkpoint互換を保証しません。

prerelease間でresumeを必要とするreleaseでは、次のどちらかをrelease前に決定します。

1. 同じprerelease版のbinaryだけでcheckpointを再開する。
2. SemVer-awareなcompatibility判定とstable/prerelease test matrixを先に実装する。

checkpointを書換えて不一致を回避してはいけません。

## 8. failure / rollback

### 8.1 version変更後、commit前に失敗

- tagやReleaseを作りません。
- `git diff`で今回変更した`Directory.Build.props`と`CHANGELOG.md`だけを確認します。
- 他者または別taskのdirty変更を`reset --hard`、一括checkout、cleanで削除しません。
- 原因を修正して同じ候補版を再検証するか、今回の2ファイルだけを意図した前版へ戻します。

### 8.2 release commit後、tag前に失敗

- 公開済み履歴を書換えたことにはなりません。
- 原因修正を新しいcommitとして追加し、全gateを再実行します。
- release commitを指すtagは、gate成功後にだけ作ります。

### 8.3 tag後、公開前に失敗

- remoteへpushしていないlocal tagなら、原因と影響を確認してから作り直せます。
- remote tagまたはdraft Releaseが他者に共有済みなら、tagを黙って移動せず、新しいPATCHまたはprerelease版を作ります。
- stable Releaseを公開しません。

### 8.4 公開後に問題を発見

- 公開済みtag、asset、sidecarを差し替えません。
- 影響を記録し、修正を新しいPATCH/MINOR/MAJORとして作ります。
- breaking changeを誤ってMINOR/PATCHで公開した場合は、互換性回復版または新MAJORを作り、問題版をrelease notesで明示します。[SemVer FAQ](https://semver.org/#what-do-i-do-if-i-accidentally-release-a-backwards-incompatible-change-as-a-minor-version)

## 9. toolのself-test

```powershell
pwsh.exe -NoLogo -NoProfile -File .\dev\version.tests.ps1
```

self-testは実repositoryの`show/verify`と、一時copyに対するset、bump、dry-run、invalid SemVer拒否を検証します。実repositoryの版は変更しません。

## 10. 完了条件

版上げは次を全て満たした場合だけ完了です。

- [ ] bump根拠がMAJOR/MINOR/PATCH規則へ追跡できる。
- [ ] `Directory.Build.props`が合意した版を示す。
- [ ] `[Unreleased]`と対象版のCHANGELOG entryが正しい。
- [ ] App/CoreのMSBuild版が一致する。
- [ ] required build/test/package/clean launchが成功する。
- [ ] publish DLLのAssembly/File/Product versionが一致する。
- [ ] annotated tagが製品版と一致し、release commitを指す。
- [ ] working treeがcleanである。
- [ ] ZIPとsidecarが一致する。
- [ ] GitHub Releaseのtag、prerelease状態、asset、notesが一致する。
- [ ] 公開済み版を差し替えていない。
- [ ] schema/component versionを理由なく連動更新していない。

## 11. 出典

### Repository

- [`Directory.Build.props`](../../Directory.Build.props)
- [`Directory.Packages.props`](../../Directory.Packages.props)
- [`global.json`](../../global.json)
- [`scripts/publish-windows.ps1`](../../scripts/publish-windows.ps1)
- [`scripts/package-windows.ps1`](../../scripts/package-windows.ps1)
- [`WindowsPublishPackageTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsPublishPackageTests.cs)
- [`PackageLockTests.cs`](../../tests/StudyReportEvaluator.App.Tests/SupplyChain/PackageLockTests.cs)
- [版管理の事前調査](../../work/20260902-version-management-investigation-report.md)

### External specifications

- [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)
- [Microsoft Learn: Set assembly attributes in a project file](https://learn.microsoft.com/dotnet/standard/assembly/set-attributes-project-file)
- [Microsoft Learn: Source Link included in the .NET SDK](https://learn.microsoft.com/dotnet/core/compatibility/sdk/8.0/source-link)
- [Microsoft Learn: `global.json` overview](https://learn.microsoft.com/dotnet/core/tools/global-json)
- [NuGet: Locking dependencies](https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files#locking-dependencies)
- [Pro Git: Tagging](https://git-scm.com/book/en/v2/Git-Basics-Tagging)
- [GitHub Docs: About releases](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)
- [GitHub Docs: Immutable releases](https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases)

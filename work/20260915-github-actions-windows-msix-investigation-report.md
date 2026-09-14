# GitHub.com で Windows 用 MSIX を任意のタイミングで作成するための調査レポート

- 調査日: **2026-09-15 JST**（GitHub 設定の一次確認開始: 2026-09-14T16:49:39.0730746Z = 2026-09-15 01:49:39 JST）
- 対象: `dahatake/StudyReport-Evaluator`、Windows 11 x64 向け配布
- 調査対象コミット: **`77d1dd6eee858765a619e36b6ba5f27dd9255360`**
- 成果物の性質: 調査・設計提案。**ワークフロー実装、署名、インストール、公開の実行報告ではない。**
- 出典表記: `[Rxx]` は対象リポジトリ、`[Oxx]` は GitHub の実観測、`[Gxx]` は GitHub 公式資料、`[Mxx]` は Microsoft 公式資料、`[Cxx]` は証明書事業者の一次資料。末尾にリンク・確認範囲を掲載する。

## 1. 結論

**GitHub Actions を使って任意のタイミングで MSIX を作成する土台は、既にこのリポジトリに存在する。ただし、現状のままでは「手動実行して、利用者がインストールできる MSIX をダウンロードする」という目的は達成できない。** 主な不足は次のとおり。[R01][R02][R03][R04][R07]

1. **MSIX 本体の保存・配布を許可する方針変更。** 現行要求は、開発用 MSIX 本体を GitHub Releases にも Actions artifact にもアップロードしないと明記している。アップロード先を1行追加するだけでは、要求と矛盾する。[R07 §13.6、特に行723]
2. **目的に合った手動ワークフロー。** 既存 CI は手動実行できるが、先行する単一 EXE 検証に依存する。現在の `main` の直近 CI はそこで失敗し、MSIX 生成は実行されていない。[R01][O04]
3. **本番配布なら、対象 PC が信頼できるコード署名。** 現在の生成スクリプトは `UnsignedDevelopment` と `SignedTest` の2モードで、本番用の成果物・署名運用を提供していない。[R04][M01][M02]
4. **本番 Identity・画像・バージョン管理・公開検証。** テスト用 Identity／合成アイコンからの分離、署名後ハッシュ、タイムスタンプ、Windows 11 のインストール・更新・削除検証が必要になる。[R04][R05][R06][M01][M03][M04][M09]
5. **公開する場合は既存の公開契約も更新。** 現在の Release candidate／Publish release は EXE・ZIP とそれぞれのチェックサムの4ファイルだけを許可し、MSIX を拒否する。[R02][R03][R08][R09]

### 1.1 「作成」の到達点を分ける

| 到達点 | 現状 | 必要な対応 |
|---|---|---|
| A. GitHub 上で開発用 MSIX の生成可否を検証する | CI に実装済み。ただし直近 `main` 実行では先行工程が失敗 | 先行失敗を解消して再実行、または検証ジョブを適切に独立させる |
| B. 開発者が試験用 `.msix` 本体を取得する | 本体アップロードは実装・要求とも禁止 | 要求変更の承認、手動作成と本体 artifact 保存、開発専用の表示・運用 |
| C. 一般利用者が通常の導入手順で使える署名済み MSIX を取得する | 未実装・未検証 | B に加え、本番署名、Identity、正式アセット、導入検証、必要に応じ公開フロー改修 |

A/B と C は別の成果である。未署名開発 MSIX は Microsoft も広範な配布に使わないよう明記している。**GitHub アカウントだけで試験用 MSIX を生成することと、Windows が信頼する発行者として配布することは同義ではない。**[R01][R07][O04][M01][M03]

**推奨案（本調査の提案）:** 既存 EXE／ZIP 公開フローを不用意に変更せず、方針承認後に MSIX 専用の手動ワークフローを設ける。開発用の作成・取得を先に検証し、本番署名と Windows 11 実機検証を別の段階として追加する。一般配布の最終目標は C とし、B を完成品扱いしない。[R01–R09][G01][G05][M01][M03]

## 2. 調査方法と限界

### 2.1 今回確認したもの

- ローカルの `main` と GitHub 上の `main` の SHA が一致すること、調査開始時の作業ツリーが clean であること。[O01]
- 現行の PowerShell スクリプト、MSIX マニフェスト、CI／Release YAML、要求定義、関連する契約テストの本文。[R01–R14]
- 認証済み GitHub API によるリポジトリ情報、Actions 設定、Environment の保護設定、Secrets の**件数・名前だけ**、直近 CI と公開 Release。[O01–O04][O06]
- 過去の成功 CI の MSIX 検証 artifact を一時ディレクトリへ取得し、JSON 本文と含有ファイルを直接照合。[O05]
- GitHub、Microsoft、および証明書事業者の公開一次資料。[G01–G10][M01–M11][C01]

### 2.2 今回実施していないもの

新規の Actions 実行、ビルド／アプリのテスト、証明書発行・購入、Secrets 登録、署名、信頼ストア変更、MSIX インストール、GitHub Release 作成・公開、Store 申請は実施していない。実ユーザーの認証情報、学生の実データ、ローカルの秘密鍵も調べていない。

本書でいう「必要な変更」「推奨」「受け入れ条件」は**提案**であり、実装済み・成功済みとは主張しない。過去の成功を現行コミットの成功へ拡張しない。取得した MSIX 証跡は生成時の記録であり、MSIX 本体の再取得・再ハッシュではない。[O04][O05]

資料検索サービスの一つは利用上限に達したため、GitHub／Microsoft の公式本文を直接取得して補完した。DigiCert の旧説明 URL は Page not found、SSL.com の候補説明 URL は本文抽出不能だったため、そのページの内容や推測した価格・発行条件は根拠に採用していない。確認できた DigiCert の製品ページは鍵の提供形態の例に限定して使用する。[C01]

### 2.3 調査中の作業ツリー変更

最終確認では、調査開始時とは異なり、README／開発文書の更新、過去の `work/` ファイルの削除表示、`dev/docs/archive/` の新規表示を観測した。本調査ではこれらを変更しておらず、復元・上書きもしない。変更の実行者や経緯は調査していない。本書のソース上の「現行」は、冒頭の固定コミットを指し、並行して現れた未コミット文書変更を新しい承認済み仕様として取り込んでいない。[O08]

## 3. 現在の GitHub とアプリの状態

### 3.1 GitHub 設定の実観測

| 項目 | 確認結果 | 根拠 |
|---|---|---|
| リポジトリ | `dahatake/StudyReport-Evaluator`、public | [O01] |
| 既定ブランチ | `main` | [O01] |
| ローカル／リモート `main` | ともに `77d1dd6eee858765a619e36b6ba5f27dd9255360` | [O01] |
| 調査開始時のローカル変更 | `git status --short` の出力なし | [O01] |
| 調査に使った認証主体の権限 | API で `push: true`、`admin: true` | [O01] |
| Actions | 有効、allowed actions は `all` | [O02] |
| SHA 固定を強制する設定 | `sha_pinning_required: false` | [O02] |
| 既定の workflow token 権限 | `read`。PR レビュー承認許可は false | [O02] |
| 有効な workflow | `CI`、`Release candidate`、`Publish release` の3件 | [O02] |
| Environment | `publish` が1件 | [O03] |
| `publish` の保護 | required reviewers あり。`prevent_self_review: false`。`deployment_branch_policy: null` | [O03] |
| repository Secrets | `total_count: 0` | [O03] |
| `publish` Environment Secrets | `total_count: 0` | [O03] |
| 最新公開 Release | `v0.8.1`、2026-09-04T08:52:49Z 公開。ZIP と `.zip.sha256` の2件 | [O06] |
| 現行ソースの製品版 | `0.8.5`、README 上は UNRELEASED | [R10][R13] |

Secrets が0件という結果は、その2つの GitHub スコープでの観測に限る。「所有者はどこにも証明書を保有していない」「外部の署名サービス契約がない」とまでは分からない。また、`publish` は存在するが、**本番署名のための設定が済んでいることを意味しない。**[O03][R03][R04]

### 3.2 直近 CI は MSIX 生成前に停止している

対象 run: [34141745681](https://github.com/dahatake/StudyReport-Evaluator/actions/runs/34141745681)、2026-09-07T16:07:40Z 開始、SHA は調査対象 `77d1dd6...`。[O04]

Windows job の観測結果は以下のとおり。[O04]

| 工程 | API の結果 |
|---|---|
| Restore locked dependencies | success |
| Verify product version | success |
| Run product version tool self-tests | success |
| Build Release | success |
| Run deterministic tests | success |
| Build and validate Windows ZIP regression | success |
| Build and validate Windows development-host single-file package | **failure** |
| Build and validate unsigned MSIX mechanism | **skipped** |
| Upload unsigned MSIX mechanism evidence | **skipped** |

失敗ログには `P07_FAIL stage=p06-trx code=P07_TRX_TEST_NOT_PASSED` と、`P07 Windows single-file required validation failed.`、終了コード1を確認した。**この結果を「MSIX のビルド失敗」と表現するのは不正確。** ただし、直近の現行 CI をそのまま実行すれば必ず MSIX まで進む、ともいえない。[O04]

この調査では P06 の個々の失敗テスト名・根本原因まで確定していない。GUI 環境、テストコード、アプリのいずれが原因かは未確定であり、CI の既存ゲートを単に無効化してよいという結論にはしない。[O04][R01]

### 3.3 過去には GitHub 上で生成した記録がある

過去 run [33855982972](https://github.com/dahatake/StudyReport-Evaluator/actions/runs/33855982972) の Windows job では、MSIX 生成 step が success。取得した artifact `unsigned-msix-mechanism-33855982972` に含まれていたのは、検証 JSON と `.msix.sha256` の2ファイルであり、**MSIX 本体はなかった。**[O05]

JSON が記録する内容:[O05]

| 項目 | 記録値 |
|---|---|
| 測定日時 | `2026-09-04T09:12:16.8851683+00:00` |
| source commit | `f044c38f60bb6a0e48a344b2f11b052eae5631e1` |
| package version | `0.8.1.0` |
| status | `PASS_MECHANISM` |
| production status | `BLOCKED_EXTERNAL` |
| install status | `NOT_RUN_REQUIRES_ELEVATED_DISPOSABLE_WINDOWS_11_HOST` |
| host | OS build `26100`、X64、PowerShell `7.6.5`、.NET SDK `10.0.400` |
| tool | `Microsoft.Windows.SDK.BuildTools` `10.0.26100.4948` |
| package の記録サイズ | `155501079` bytes |
| package の記録 entry 数 | `253` |

この JSON の原本 bytes の SHA-256 は `08B6BE82D9180739697678C6B25CA767BCB027DF0D529A2F84035BD8EFCCB320`、サイズは2869 bytes。これは**検証 JSON のハッシュ**であり、MSIX のハッシュではない。GitHub API での artifact ID は `9930636795`、観測時の失効予定は `2026-09-18T09:12:21Z`。将来もダウンロード可能とは保証しない。[O05]

## 4. 既にある仕組みと、不足している仕組み

### 4.1 手動起動は既に定義されている

| Workflow | 手動入力 | 作成・公開内容 | MSIX 本体 |
|---|---|---|---|
| `CI` / `ci.yml` | 独自入力なし。実行ブランチを選択 | build、tests、ZIP、単一 EXE、開発 MSIX の検証。MSIX 証跡の保持は14日 | アップロードしない |
| `Release candidate` / `release.yml` | `tag` | 既存の stable annotated tag、版整合、clean checkout、日付付き CHANGELOG を確認し、EXE／ZIP の4 asset を draft Release に添付。artifact 保持は30日 | 検証するがアップロードしない |
| `Publish release` / `publish-release.yml` | `tag`、`candidate_run_id`、`clean_host_evidence_json` | `publish` 承認後、候補 run・SHA・成果物の同一性・受領した clean-host 証跡を照合して公開 | 明示拒否。ビルドもしない |

根拠: [R01][R02][R03]。現在公開済みの `v0.8.1` は旧版 workflow で作成された ZIP の2 asset であり、現行 workflow の4 asset 契約と区別する。[O06][R02]

`workflow_dispatch` は GitHub の手動実行イベントで、workflow ファイルが既定ブランチに存在する必要がある。実行者には repository の write access が必要。Web UI、GitHub CLI、REST API から起動できる。[G01][G02]

### 4.2 MSIX 生成スクリプト

`scripts/package-windows-msix.ps1` の現行契約:[R04]

| 項目 | 現行仕様 |
|---|---|
| モード | `SignedTest`（既定）、`UnsignedDevelopment` |
| 入力 | `IdentityName`、`Publisher`、`PublisherDisplayName`、3種類の PNG |
| 署名入力 | `SignedTest` では40桁16進の `TestCertificateThumbprint` |
| 証明書の所在 | runner 上の `Cert:\CurrentUser\My\<thumbprint>` |
| 証明書検証 | 秘密鍵あり、有効期間内、Code Signing EKU、Subject と Publisher の完全一致 |
| 署名・検証 | SignTool の SHA-256 署名、`verify /pa /v` |
| タイムスタンプ | 現行呼び出しに `/tr`／`/t`／`/td` はない |
| unsigned 出力 | `artifacts/package/mechanism/StudyReportEvaluator-win-x64.unsigned.test.msix` |
| signed test 出力 | `artifacts/package/mechanism/StudyReportEvaluator-win-x64.test.msix` |
| 成功表示 | `PASS_MECHANISM only`。本番署名成功とは表示しない |

`SignedTest` という名前でも自己署名証明書だけに型を限定しているわけではない。しかし、公開 CA の証明書を渡しただけで、タイムスタンプ、正式配布契約、導入検証が自動的に完成するわけではない。証明書のインポートや trust 設定をスクリプトが自動で行うこともない。[R04][R08]

### 4.3 MSIX は単一 EXE の包み直しではない

`PublishedDirectory` を省略すると、MSIX スクリプトは `publish-windows.ps1` を **`-SingleFile` なし**で呼び、`artifacts/package/publish/win-x64` を使う。入力にはアプリ EXE／DLL、runtimeconfig、deps、Core DLL、Copilot SDK、`coreclr.dll`、`copilot-runtime.json`、同梱 `runtimes/win-x64/native/copilot.exe` などを要求する。[R04][R11]

したがって、`win-x64-singlefile` の単一 EXE だけを `PublishedDirectory` に渡す案は現行契約に合わない。既存のフォルダー型 self-contained 出力を使う方が変更範囲を抑えられる。[R04][R11]

MSIX 生成は MakeAppx の `pack`／`unpack` を使う。Visual Studio の新しい `.wapproj`、WiX、Inno Setup、MSIX Packaging Tool による既存 installer のキャプチャは、**現行方式を再利用する限り追加の必須要件ではない**。これは既存実装と MakeAppx の仕様からの判断である。[R04][M05]

### 4.4 開発用の検証 driver

`scripts/test-windows-msix-unsigned.ps1` は次を実行する。[R05][R12]

- `Microsoft.Windows.SDK.BuildTools` **`10.0.26100.4948`** を専用 csproj／lock から locked restore。
- x64 MakeAppx の package hash 情報、ファイル版、Microsoft Authenticode 署名を確認。
- **44×44、150×150、50×50** の合成 PNG を生成。
- 固定の開発 Identity と unsigned OID marker を使って生成。
- unpack、Identity／version／architecture、SHA-256 block map、文書・画像20項目、同梱 CLI hash、禁止入力、負のポリシー試験を確認。
- `.msix.sha256` と `.evidence.json` を生成。
- **インストール、導入済みアプリの起動、更新、削除は行わない。**

注意: BuildTools の固定はこの driver が担当する。下位の `package-windows-msix.ps1` を単独で呼ぶ場合、明示パスを渡さなければインストール済み SDK の候補探索になる。本番でも固定済みツールのパスを渡す設計が望ましい。[R04][R05]

## 5. GitHub 上のビルド環境に必要なもの

| 要素 | 必要条件・推奨 | 根拠 |
|---|---|---|
| ソース | GitHub に存在する、特定 SHA に解決できるソース。正式版は版・tag・CHANGELOG と整合させる | [R02][R03] |
| runner | x64 Windows。新規専用 workflow は `windows-2025` を候補とし、OS build／architecture を記録することを推奨 | [R04][R05][G03][G04] |
| .NET | `global.json` は SDK `10.0.400`、`latestPatch`、preview 不許可。既存 Actions も `10.0.400` 指定 | [R01][R10] |
| PowerShell | MSIX scripts は Core 7以上。既存公開フロー全体は7.5以上。最新導入済みの互換 Core を使う | [R02][R03][R04][R05] |
| MSIX tools | x64 MakeAppx。署名する場合は SignTool と採用署名方式の client／provider | [R04][R05][M02] |
| 依存取得 | NuGet、.NET 配布元、固定 Copilot CLI 取得先へ接続可能であること | [R01][R05][R11] |
| Node.js／npm | Windows 発行スクリプトは npm があれば固定 `@github/copilot-win32-x64` を取得し検証。標準 runner に npm あり。明示的な環境確認を推奨 | [R11][G04] |
| ドキュメント等 | スクリプトの公開 payload allowlist の20ファイルを含む完全な checkout | [R04][R05][R08] |
| 一時領域 | publish、staging、pack、unpack の作業領域と空き容量。必要量の実測は今回未実施 | [R04][R11] |
| 本番署名時の外部接続 | 採用サービスの認証・署名・タイムスタンプ・証明書検証に必要な通信 | [M01][M02][M04][G08] |

npm が見つからない場合の分岐も実装されているため、「npm がないと必ず失敗する」とは断定しない。一方、任意時刻の再ビルドで取得経路が変わらないよう、選択した CLI 取得経路を事前確認するのは有用である。[R11]

### 5.1 `windows-latest` は Windows 11 ではない

公式 runner 表では、x64 の `windows-latest` は現在 Windows Server 2025 の README に対応する。参照した image は `20260907.255.1`、OS Version は `10.0.26100`。Windows Server 2022 の参照 image は OS Version `10.0.20348` だった。[G03][G04]

このアプリのホスト判定は、エラーメッセージでは Windows 11 と記述するが、実装上は `$IsWindows`、OS build **22000以上**、OS／process の X64 を検査している。よって:[R04][R05][R11][G04]

- Server 2025 の build 26100 は、この数値条件だけなら通る。
- Server 2022 の build 20348 は、現行スクリプトの条件で拒否される。
- Windows 11 ARM の runner に切り替えるだけでは X64 条件を満たさない。
- **Server 上で pack が成功しても、Windows 11 の App Installer／標準ユーザー導入が成功した証明にはならない。**

過去の生成証跡にも build 26100 が記録されている。ただし証跡 JSON は OS 製品名・runner image の完全名まで保持していないので、その JSON だけから特定の image 版を断定しない。[O05]

`windows-2025` 指定も image 内の全ソフトウェアを完全固定するものではない。SDK／BuildTools／依存 lock と実際の環境記録を併用することを提案する。[G03][G04][R05][R10]

## 6. 署名と発行者に必要なもの

### 6.1 配布先による違い

| 方式 | 作成側の必要物 | 利用者側の条件 | このアプリへの適用 |
|---|---|---|---|
| 未署名開発 MSIX | 開発用 OID を含む Publisher。証明書不要 | Windows 11、実行コード入りなので管理者による `Add-AppxPackage -AllowUnsigned` | 既存方式。試験専用、一般配布不可 |
| 自己署名の test MSIX | 開発用証明書と秘密鍵、署名できる runner | 対象 PC で当該証明書を信頼する試験準備 | 限定された使い捨て試験環境向け |
| 組織内の署名済み MSIX | IT 部門等が管理する署名鍵／証明書 | 組織側で信頼チェーンと導入ポリシーを管理 | 不特定多数向け公開とは別設計 |
| 一般配布の署名済み MSIX | 対象 PC の信頼チェーンにつながるコード署名証明書、または対応する署名サービス | 信頼・署名・OS／組織 policy が満たされること | GitHub Releases で直接配布する本番案 |
| Microsoft Store 配布 | Store 用 Identity／申請・認定 | Store 経由で配布 | Store が MSIX を再署名。今回の GitHub 直接配布とは別経路 |

根拠: [M01][M02][M03][M06][M08]。自己署名試験用に他者へ渡す場合は**公開証明書だけ**を渡し、秘密鍵入り PFX は配布物に含めないという運用を提案する。[G06][G07][R04]

未署名開発パッケージは署名済みパッケージと同じ Identity にならない。**既存 `.unsigned.test.msix` に署名を後付けして本番に昇格する構成ではなく、unsigned OID を除いた本番 Publisher で別途組み立てる必要がある。**[M03][R04]

### 6.2 本番署名の調達前に決める事項

以下は調達・設定のチェックリストであり、所有者が既に契約済みという意味ではない。[M01][M02][M06][C01]

1. 発行者を個人にするか法人にするか。
2. 申請者の国・所在地・本人／法人確認条件を満たせるか。
3. Windows で表示したい正式名と、実際に発行される証明書 Subject。
4. MSIX／Authenticode を採用するサービス・鍵アルゴリズムで署名できるか。
5. GitHub-hosted runner から、秘密鍵を取り出さず非対話で署名できるか。
6. コード署名証明書、鍵保管、署名回数、更新、タイムスタンプの費用と制約。
7. 証明書更新・失効・鍵事故時の停止と復旧方法。

**PFX ファイルの存在は必須条件ではない。** 現行の証明書ストア方式では、thumbprint が指す証明書から署名鍵を利用できる必要がある。クラウド signer は追加 client／provider や認証が必要で、既存 `/sha1` 呼び出しに値を入れれば必ず使えるとは限らない。[R04][M02][M04]

DigiCert の一次資料は、KeyLocker Cloud、ハードウェアトークン、適格な所有トークン、HSM の選択肢を示している。これを踏まえ、**「証明書を購入して秘密鍵ごと Base64 PFX を Secrets に置けば完了」という一律の設計は採用しない**。採用業者の保管条件と GitHub 連携方法を確認する。価格の確定見積もりや日本在住個人への発行可否は今回未確認。[C01][G07]

Microsoft の署名概要には、マネージド署名サービスについても個人／組織・地域の利用資格が記載されている。安価・自動化可能という理由だけで、所有者に利用資格があると推定しない。[M01]

### 6.3 本番化で追加する署名検証

**提案する必須ゲート:**[M01][M02][M04][M06][R04]

- `Identity.Publisher` と署名証明書 Subject の一致。
- 正しい Code Signing 用途、利用可能な秘密鍵、有効な証明書チェーン。
- 既存 block map に合わせた SHA-256 署名。
- RFC 3161 タイムスタンプ（SignTool なら `/tr` と `/td SHA256` 相当）を付け、存在・検証結果も確認。
- 署名検証に失敗したもの、タイムスタンプを確認できないものは一般配布しない。
- **署名完了後の** MSIX bytes に対して SHA-256 sidecar と検証記録を作る。
- その後は内容を変更せず、ダウンロードした最終ファイルでも照合する。

タイムスタンプは証明書期限後の受理に重要だが、失効、端末 policy、将来の全環境に対する永久保証ではない。Microsoft もタイムスタンプを強く推奨している。現行 `SignedTest` にはタイムスタンプ処理がない。[M01][M04][R04]

**署名と SmartScreen の無警告は別。** 有効な署名があっても新しいアプリに SmartScreen 警告が出る場合がある。EV なら必ず警告がなくなる、一定回数で必ず消える、といった保証はしない。保護機能を無効にして導入成功とする運用も採用しない。[M10][R07]

GitHub artifact attestations はビルドの来歴を検証する追加手段であって、Windows 用 MSIX のコード署名・信頼条件の代替ではない。この区別は GitHub の attestation の用途と Microsoft の MSIX 署名要件からの判断である。[G09][M01][M02]

## 7. Identity、画像、バージョンとアプリ動作

### 7.1 固定すべき識別情報

本番用に次を決定・管理することを提案する。[R04][R06][M06][M07]

| 値 | 決め方 |
|---|---|
| `IdentityName` | 本番用の一貫した package name。既存 `StudyReportEvaluator.UnsignedDev` と分離 |
| `Publisher` | 調達した証明書／署名サービスが指定する正確な Subject。架空の `CN=` を決め打ちしない |
| `PublisherDisplayName` | 利用者向け表示名。これを変えても証明書の本人確認の代わりにはならない |
| `DisplayName`／`Description` | 正式名称・説明。必要な日本語表示を確認 |
| `ProcessorArchitecture` | 現行は `x64` |
| 対象 Windows | 現行 manifest は `Windows.Desktop`、MinVersion `10.0.22000.0` |

現行 manifest は `packagedClassicApp`、`mediumIL`、`runFullTrust` を指定している。これは full-trust の packaged desktop app の形であり、`runFullTrust` を「常に管理者昇格する」という意味には解釈しない。[R06][M09]

### 7.2 正式な画像

現行 packer が要求する PNG は、`Square44x44Logo.png`（44×44）、`Square150x150Logo.png`（150×150）、`StoreLogo.png`（50×50）の3つ。寸法検査がある。[R04]

`eng/packaging/windows` の tracked inventory は manifest と tools の csproj／lock だけで、既存 test driver は単色の合成画像をその場で生成する。**既存の画面説明画像8枚はこの3種の installer アイコンの代わりではない。** 正式な使用許諾済みアイコンを保管し、その出典・承認と SHA-256 を記録することを提案する。[R05][R12][O07]

### 7.3 バージョンの注意点

- 製品版は現行 `0.8.5`。packer は安定版の `Major.Minor.Patch` を受け取り、`0.8.5.0` のような4要素へ変換する。各要素は65535以下を要求する。[R04][R10]
- prerelease suffix はこの MSIX 変換経路では受け付けない。[R04]
- `-ProductVersion` は manifest の版を指定するだけで、渡された発行済み DLL／EXE をその版に再ビルドする機能ではない。本番では DLL／EXE と manifest の整合性を追加確認する必要がある。[R04][R11]
- **提案:** 新しい内容を更新として配る場合は package version を進める。同一内容の再作成は別 run ID／artifact 名で識別し、公開済み版のファイルを黙って差し替えない。run number を無制限に16-bit要素へ入れる設計も避ける。[R02][R04][M07][M08]
- Store の仕様には、先頭要素を0にしない、第四要素を0にする、という追加の版ルールがある。将来 Store に提出するなら現在の製品版とのマッピングを改めて設計する。この Store 規則を根拠に、現行の開発用 `0.8.5.0` を一律に作成不能とは断定しない。[M08][R04][O05]

### 7.4 パッケージ化後の動作は別検証

Microsoft は、パッケージの導入先・読み取り専用ファイル・一部のファイルシステム動作が非パッケージ版と異なることを説明している。仮想化の説明には適用対象の限定もあるため、全挙動を本アプリへ一律適用しない。[M09]

このアプリでは通常の設定保存先が `%LOCALAPPDATA%\StudyReportEvaluator\setting.txt`、AI login は同梱 CLI／ブラウザーに委譲される。MSIX にした場合も、設定の保存・再起動・旧 ZIP 版との関係、同梱 CLI の起動と本人 login、出力先と入力不変を確認する必要がある。現在壊れているという意味ではなく、**MSIX 導入状態では未検証の受け入れ対象**である。[R13][R05][M09]

## 8. 推奨する手動ワークフロー設計

この章は**未実装の提案**。記載する workflow 名・設定名は既存の実在値ではない。

### 8.1 開発者向け作成・取得

仮称 `Build Windows MSIX (development)` を `workflow_dispatch` 専用として追加する。[G01][G02]

1. **先に承認:** 要求定義 §13.6 の本体アップロード禁止を、目的・取得対象・保持期間を限定した許可へ変更する。一般配布禁止は維持する。[R07]
2. **入力:** GitHub の実行ブランチ選択を利用。別ソースを扱う必要があれば `source_ref` を追加するが、解決後の SHA を検証・記録する。版は原則としてソースの正本から読む。[R04][G01][G02]
3. **事前確認:** x64 Windows／PowerShell Core／SDK、private sample 非追跡、ソースの clean 状態、lock を確認。[R01][R05][R10][R11]
4. **build／tests:** MSIX に必要な決定的テストとフォルダー型 publish を実施。既存単一 EXE の P06 が MSIX 作成の直接入力ではないことを踏まえて依存を分離し、必要なテストまで一緒に削除しない。[R01][R04][R11]
5. **生成・検証:** 既存 unsigned driver の Identity／OID／合成アセット／negative checks を再利用する。[R05]
6. **保存:** 成功した場合だけ `.msix`、`.msix.sha256`、`.evidence.json` の正確な3ファイルを artifact へ保存する。MSIX 本体が存在すること・サイズ・sidecar 一致をアップロード前に検証する。[R05][G10]
7. **表示:** artifact 名と job summary に「開発用・未署名・一般配布不可・インストール未検証」を明記する。[M03][R05]

保持期間は、例えば14日を運用上の候補とするが、これは提案値。公開 repository の artifact を **Environment 承認だけで秘密配布にできるとは考えない**。Environment は job 開始／Secrets 利用を制御するものであり、artifact の取得権限は別。限定メンバーだけへ渡したいなら非公開 repository／保管先の権限設計も必要になる。[G05][G10]

既存 CI の artifact path だけを変える方法も技術的な選択肢ではあるが、現行の本体アップロード禁止、先行 EXE 検証の失敗、一般 CI と試験配布の責務混在を同時に考慮する必要がある。[R01][R07][O04]

### 8.2 本番署名済み MSIX の作成・公開

仮称 `Build Windows MSIX (signed)` は、開発 unsigned と別の権限・Identity・成果物経路を用いることを推奨する。[G05][G06][M01][M03]

推奨順序:

1. 承認された ref を正確な SHA に解決し、版・tag・CHANGELOG を確認。
2. **署名権限なし**の build／テストジョブで発行物を作る。
3. 専用 Environment（仮称 `msix-signing`）の承認後、検証済みの同一成果物だけを署名ジョブへ渡す。
4. 本番 Identity と承認済み画像で MSIX を組み立て、採用した client／provider で署名・タイムスタンプを行う。
5. 署名・Identity・最終 hash・sidecar を照合し、署名済み candidate を保存する。
6. 同一 SHA-256 の MSIX を Windows 11 の使い捨て clean host で導入検証。
7. **一般公開が必要な場合のみ**、承認付き公開フローでその検証済み bytes を GitHub Release に添付・公開する。公開段階で再ビルド／再署名しない。

以上は既存の「候補と公開を分離し、最終 bytes を照合する」設計を MSIX へ拡張する提案である。[R02][R03][G05][G06][M01][M04]

本番用の「本番 Identity で pack するが、別 signer へ渡す前なのでまだ未署名」という中間状態は、現行 `UnsignedDevelopment` とは異なる。クラウド signer を採用するなら、この中間成果物を安全に扱うモード／スクリプトを別途設計する。未署名の中間物を完成品 artifact／Release に紛れ込ませない。[R04][M03]

### 8.3 GitHub の設定項目

| 区分 | 必要な設定・運用 | 備考／出典 |
|---|---|---|
| 実行者 | repository の write access | 現在の調査主体は権限あり。他の利用者にもあるとは限らない [O01][G01] |
| build job | 原則 `contents: read` | artifact 保存を理由に repository 全体を常時 write にしない [R01][G06] |
| Release を作成・更新する job | `contents: write` | 現行公開 workflow にも宣言あり [R02][R03] |
| 別 run の検証記録を読む job | 必要な `actions: read` と取得認証 | 現行 publish が使用 [R03] |
| OIDC を使う署名 job | `id-token: write` とサービス側の限定した trust／署名権限 | token 発行権限はコード署名資格そのものではない [G08] |
| 非秘密設定 | Identity、Publisher、表示名、署名プロファイル ID、タイムスタンプ先等を承認済み設定として管理 | 値を推測しない [M02][M06] |
| 秘密設定 | 採用サービスに必要な資格情報だけを Environment Secrets 等で供給 | OIDC が対応するなら長期 secret を減らす [G05–G08] |
| 保護 | required reviewers、許可 ref、署名対象の検証、action の commit SHA 固定を推奨 | 現在は署名専用保護なし。既存 `publish` は自己承認禁止が false [O02][O03][G05][G06] |
| 同時実行 | 同一版の衝突を防ぐ concurrency、run ごとの artifact 名、公開済み版の置換禁止を提案 | 既存 release は tag ごとの concurrency あり [R02][R03] |

OIDC を採用する場合、古い記事の `sub` の例を無確認でコピーしない。GitHub は2026-07-15以後に作成された repository 等について owner／repository ID を含む immutable subject を説明している。本 repository の実際の OIDC 設定・claim は今回取得していないため、文字列を捏造して掲載しない。[G08]

`workflow_dispatch` の自由入力を PowerShell の `run:` 本文へ直接埋め込まず、環境変数や引数で受け、許可値・解決済み SHA を検証する。任意 ref を許す build と、本番鍵を利用できる ref は同じ範囲にしない。[G06][G08]

署名 PFX／password／token／実 `setting.txt`／学生 workbook を repository、artifact、Release、ログへ含めない。GitHub のログ伏字は全変換に対する保証ではなく、Base64 も暗号化ではない。[R04][R05][R07][G06][G07]

## 9. GitHub.com の画面で行う操作

### 9.1 現在できる操作: 生成検証の手動起動

1. リポジトリの **Actions** を開く。
2. **CI** を選択する。
3. **Run workflow** を選び、実行ブランチを選択する。
4. **Run workflow** を押す。
5. Windows job で **Build and validate unsigned MSIX mechanism** の結果を確認する。
6. 成功して evidence upload まで進んだ場合、Artifacts の `unsigned-msix-mechanism-<run_id>` を取得する。

操作方法の出典は [G01][G10]、workflow の実在・名前・成果物名は [R01][O02]。**これは現在 MSIX 本体を得る手順ではない。** また、直近 `main` CI の失敗があるので、成功を保証する案内ではない。[O04]

### 9.2 提案実装後: MSIX 本体の取得

1. Actions で新しい MSIX 専用 workflow を選ぶ。
2. 許可された実行ブランチ／入力を選び、Run workflow。
3. 署名ありの場合は、対象 SHA・版・署名対象を確認して Environment を承認。
4. job の成否だけでなく、生成、署名、検証、アップロードの各結果を確認。
5. Artifacts から目的の `.msix` を取得し、最終 SHA-256 と署名を照合。
6. 一般利用者向けに公開する場合は、実機検証・公開承認を経た GitHub Release の asset を利用する。

これは [G01][G05][G10][M01][M04] に基づく**将来手順**であり、現在その workflow や本番 download URL が存在するとは主張しない。

既存の `Release candidate` は annotated tag と日付付き CHANGELOG、未作成の Release を要求するため、何度でも同じ版を一時作成する汎用ボタンには向かない。作成だけの workflow は Release の作成を副作用に持たせない設計を推奨する。[R02]

## 10. 変更対象の一覧

以下は実装する場合の影響範囲であり、本調査では変更していない。

| 対象 | 開発用本体を取得する段階 | 本番 MSIX 配布の段階 | 根拠 |
|---|---|---|---|
| `docs/requirements-definition.md` | 本体 artifact upload 禁止と非公開範囲を承認の上で改版 | production MSIX の範囲・受け入れ条件を追加 | [R07] |
| 関連 ADR／README | 開発者用試験と一般利用者導線を分離 | 配布・署名・対応状況の表示を更新 | [R13][R14] |
| `.github/workflows/` の新規 MSIX workflow | 手動起動、独立した build、厳密な本体 upload | build／sign／検証／承認を分離 | [R01][G01][G05] |
| `scripts/package-windows-msix.ps1` または別の本番 packer | 既存 unsigned contract を再利用 | production Identity、外部署名の接続、正式出力、timestamp、検証を実装 | [R04] |
| `eng/packaging/windows/` | 合成画像は試験用のまま | 使用許諾・承認済みの3種 PNG、必要な manifest 設定 | [R04][R06][O07] |
| `test-windows-msix-unsigned.ps1` | 開発専用 status を維持 | 本番署名の試験は別系統にする | [R05] |
| `release.yml`／`publish-release.yml` | 原則維持 | 既存フローに MSIX を統合するなら exact asset 集合と MSIX 拒否判定を改版 | [R02][R03] |
| release matrix builder／validator・関連 schema | 原則維持 | 開発 MSIX と本番 MSIX を別 artifact kind として検証する設計を推奨 | [R09] |
| Packaging／Release 契約テスト | 新しい本体取得ポリシーと既存禁止範囲を検査 | 本番署名・公開許可・負の試験を追加 | [R08] |
| Windows 11 clean-host 検証 | 任意の開発試験は証跡を別管理 | 署名済み exact package の導入・更新・削除を必須化 | [R05][M05][M09] |

既存テストは test-only ファイル名や MSIX 不在の exact artifact 集合まで検査する。正式 MSIX を追加するなら、テストを単に削除するのではなく、**開発版禁止と本番版許可を区別する契約**へ更新する。[R08]

## 11. 完了判定に必要な検証

以下は**提案する受け入れチェックリストであり、今回の PASS 実績ではない**。MakeAppx 公式資料も、pack の検証だけではインストール可能性を保証しないと明記している。[M05]

| ID | 確認項目 | 合格条件の案 | 根拠 |
|---|---|---|---|
| V01 | 手動起動 | 許可された利用者が GitHub UI から開始でき、対象 SHA が記録される | [G01][G02] |
| V02 | 環境・依存 | SDK／BuildTools／RID／lock と採用 CLI の identity が一致 | [R05][R10–R12] |
| V03 | package 構造 | manifest、20項目 payload、native runtime／CLI、禁止ファイル検査が成功 | [R04][R05] |
| V04 | 版・発行者 | manifest／バイナリ版、本番 Identity、証明書 Subject が一致 | [R04][M06][M07] |
| V05 | 署名・timestamp | 有効な署名とタイムスタンプを確認。失敗・欠落は停止 | [M01][M02][M04] |
| V06 | 取得した実物 | artifact／Release から取得した MSIX の hash が署名後記録と一致 | [R03][G10] |
| V07 | 新規導入 | Windows 11 x64 の clean host で、意図した発行者表示と導入結果を確認 | [M01][M05][M09] |
| V08 | 標準ユーザー動作 | Start menu 等から起動し、GUI・ファイル選択・Excel 読込が動く | [R13][M09] |
| V09 | 設定・出力 | `setting.txt` の明示保存・再起動、指定出力先、入力 workbook 不変を確認 | [R13][M09] |
| V10 | 同梱 CLI | パッケージ内 CLI の解決・hash・起動、別途本人 login を検証 | [R04][R05][R13] |
| V11 | 更新・削除 | 旧本番 MSIX→新版、実行中更新時の挙動、アンインストール、設定・利用者ファイルの扱いを確認 | [M09][R13] |
| V12 | 失敗時 | Subject 不一致、署名不能、timestamp 不能、hash 不一致、未承認、未知 ref／版を拒否 | [R04][G05][G06][M04] |
| V13 | 公開境界 | unsigned test／未検証 candidate／秘密値を本番 Release に含めない | [R07][R08][G06][G07] |
| V14 | 回帰 | 既存 EXE／ZIP 配布と関連 tests の結果を維持 | [R01][R02][R08] |

無人 CI の生成試験には、本人の Copilot login、実 AI 評価、学生の実データは持ち込まない。必要な実認証試験は、同意を得た本人が別環境で実施し、build 成功・login 成功・AI 利用可能を区別して記録する。[R01][R13]

署名済み MSIX の正常導入を確認するためにテスト証明書を事前 trust しては、一般利用者と同じ信頼条件の検証にならない。test-signed の限定試験と public-trust の試験を分けることを提案する。[M01][M06]

## 12. 費用・不要な前提・未確定事項

### 12.1 費用

| 費用要素 | 今回いえること |
|---|---|
| 標準 GitHub-hosted runner の実行時間 | 対象は public repository。GitHub 公式は public repository の標準 runner 利用を無料としている [O01][G03][G04b] |
| artifact／cache 保管 | 保持期間・容量・plan の対象枠と課金を確認する。今回は所有者の契約・使用量を取得していない [G04b][G10] |
| 大型 runner／自己管理ホスト | 標準 runner と同一の総費用ではない。大型 runner は public でも有料。自己管理は機器・運用費を別途見積もる [G04b][G06] |
| コード署名 | CA 証明書、クラウド保管／HSM、署名サービスの契約条件次第。確定金額は未見積もり [M01][C01] |
| clean-host 試験 | 実機／使い捨て VM と検証作業が必要。工数・総額は未測定 [M05][M09] |

「任意のタイミングで起動可能」は、即時完了・外部配布サイトの恒久可用性・署名サービスの無停止・費用上限なしを意味しない。依存取得や鍵の期限を含む運用監視が必要、というのが本調査の判断である。[R05][R11][G04b][M01]

### 12.2 初期の作成機能に必須ではないもの

- **Microsoft Store 登録:** GitHub 上で生成し、署名済み MSIX を直接配布するだけなら別経路。[M01][M08]
- **常時稼働の自前 Windows PC:** 既存には GitHub-hosted runner での生成成功記録がある。ただし Windows 11 の実機検証環境は別途必要。[O05][G03][M05]
- **利用者 PC の .NET SDK、Node.js、PowerShell 7、Git、Office:** ビルド側の依存と利用者側を分ける。現行は self-contained の設計だが、MSIX 導入後の最終確認は必要。[R11][R13]
- **ビルドのための本人 Copilot 認証情報:** CI は live smoke を無効化している。AI 利用条件は利用時に別途満たす。[R01][R13]
- **GitHub 操作用 PAT の新規作成:** 既存 Release は `github.token` を使っている。外部 signer の認証は別問題。[R02][R03]
- **`.appinstaller` と自動更新:** MSIX 単体の作成には必須ではない。自動更新を採用する場合に、更新先 URL・設定・配布方針を別設計する。[M11][R13]

`.appinstaller` を将来採用しても、Web ページからの `ms-appinstaller:` ワンクリック起動を全端末で使えると約束しない。Microsoft はこの URI protocol が2023-12以降既定で無効と説明している。[M11]

### 12.3 所有者による決定が必要な事項

1. 欲しいのは生成検証のみ、開発用本体の取得、一般配布のどれか。
2. 開発 MSIX の本体アップロード禁止を、どの範囲まで変更するか。
3. 発行者を個人／法人のどちらにし、どの証明書／サービスを使うか。
4. 正式な Identity・表示名・アイコンを何にするか。
5. 一時 artifact だけでよいか、GitHub Release まで公開するか。
6. 本番 MSIX の版番号・更新・設定移行・削除時の保持方針。
7. 承認者、許可 ref、clean-host 検証担当者と検証環境。

これらの値や承認は今回の依頼からは確定できないため、実在しない値を設定済みとして記載しない。[R04][R07][G05][M01][M06]

## 13. 推奨する実施順序

以下は優先順位の提案であり、時間見積もりではない。

1. **方針を確定:** 要求定義の本体アップロード禁止・production MSIX 対象外を、目的に合わせて承認の上で変更。[R07]
2. **作成経路を独立化:** MSIX 専用手動 workflow を実装。既存 CI の EXE 検証失敗は別途調査し、必要な回帰品質を下げない。[R01][O04]
3. **開発 package の取得まで実証:** exact SHA、version、tool、MSIX／sidecar／evidence を照合し、開発専用表示を維持。[R05][M03]
4. **本番発行者と署名方式を導入:** 資格確認・鍵管理・timestamp・検証・Environment を整える。[M01][M02][M04][G05–G08]
5. **本番 candidate を clean Windows 11 で検証:** 導入・起動・設定・CLI・更新・削除を実測。[M05][M09][R13]
6. **必要なら公開フローへ統合:** asset allowlist、matrix、契約テスト、README を同期し、承認後に検証済みの同じファイルを公開。[R02][R03][R07][R08][R09]

**最終回答:** 必要なのは MSIX の作成技術をゼロから導入することではなく、既存の生成基盤に対して、**配布方針の承認 → 専用の手動作成・保存 → 本番署名と Identity → 実機検証 → 公開契約**を追加することである。試験用作成と一般利用者向けインストーラーを混同しないことが最も重要である。[R01–R09][M01–M06]

## 14. 出典一覧

### 14.1 対象ソース（固定コミットへのリンク）

以下はすべて `77d1dd6eee858765a619e36b6ba5f27dd9255360` のファイル。調査開始時にローカルと GitHub の `main` が同じ SHA・clean 状態であることを照合し、関連本文を確認した。終了時の作業ツリーが clean という意味ではない（§2.3）。ファイル全体への固定リンクを用い、本文には関数名・節名等も付記した。[O01][O08]

- **[R01]** [`.github/workflows/ci.yml`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/.github/workflows/ci.yml) — manual trigger、runner、テスト順序、MSIX evidence-only upload、保持期間。
- **[R02]** [`.github/workflows/release.yml`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/.github/workflows/release.yml) — annotated stable tag、版・CHANGELOG、draft 作成、exact EXE／ZIP asset 集合。
- **[R03]** [`.github/workflows/publish-release.yml`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/.github/workflows/publish-release.yml) — publish Environment、入力、権限、MSIX 拒否、再取得と公開前後の同一性検証。
- **[R04]** [`scripts/package-windows-msix.ps1`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/scripts/package-windows-msix.ps1) — parameters、`Assert-SupportedHost`、`Get-PackageVersion`、SDK 探索、証明書検証、pack／sign／unpack、成果物・status。
- **[R05]** [`scripts/test-windows-msix-unsigned.ps1`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/scripts/test-windows-msix-unsigned.ps1) — pinned BuildTools、synthetic assets、OID、負の試験、証跡と未実施範囲。
- **[R06]** [`eng/packaging/windows/AppxManifest.xml`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/eng/packaging/windows/AppxManifest.xml) — x64、minimum OS、full-trust manifest、画像。
- **[R07]** [`docs/requirements-definition.md`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/docs/requirements-definition.md) — 行17、§13.6（特に行719–727）、行832／836、AC-023／026／028。本体は Actions artifact にも upload しないことを確認。
- **[R08]** [`WindowsInstallerPackageTests.cs`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/tests/StudyReportEvaluator.App.Tests/Packaging/WindowsInstallerPackageTests.cs)、[`ReleaseWorkflowContractTests.cs`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/tests/StudyReportEvaluator.App.Tests/Packaging/ReleaseWorkflowContractTests.cs) — test-only、tool lock、payload20件、exact artifact、MSIX 不在の静的契約。今回テスト実行はしていない。
- **[R09]** [`build-platform-release-matrix.ps1`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/scripts/build-platform-release-matrix.ps1)、[`validate-platform-release-matrix.ps1`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/scripts/validate-platform-release-matrix.ps1) — `windows-development-msix`、`publish = -not $isMsix`、MSIX evidence binding、candidate／final の区別。該当語の検索で関連箇所を確認。
- **[R10]** [`Directory.Build.props`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/Directory.Build.props)、[`global.json`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/global.json) — 版 `0.8.5`、net10.0、SDK `10.0.400`／latestPatch。
- **[R11]** [`scripts/publish-windows.ps1`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/scripts/publish-windows.ps1) — folder／single-file 分岐、self-contained、RID lock、npm CLI 取得、同梱 runtime 検証。
- **[R12]** [`WindowsSdkBuildTools.csproj`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/eng/packaging/windows/tools/WindowsSdkBuildTools.csproj) — exact package version。lock の契約は [R05][R08] でも照合。
- **[R13]** [`README.md`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/README.md) — 公開／候補版、設定先、CLI login、入力不変、非保証、自動更新対象外。
- **[R14]** [`ADR-0015`](https://github.com/dahatake/StudyReport-Evaluator/blob/77d1dd6eee858765a619e36b6ba5f27dd9255360/dev/docs/adr/0015-windows-macos-installer-delivery.md) — 初回配布時の開発 MSIX 境界。古い初回公開時点の文書なので、現行要求 [R07] を優先する。

### 14.2 GitHub 実観測の一次出典

取得は GitHub CLI の `gh api` による GET、および読み取り専用の `git` 操作。以下の API URL は認証／権限を要する場合がある。設定と API 応答は時間で変化し、永久に同じ値ではない。秘密値の取得は行っていない。

- **[O01]** [repository API](https://api.github.com/repos/dahatake/StudyReport-Evaluator)、[`commits/main`](https://api.github.com/repos/dahatake/StudyReport-Evaluator/commits/main)。確認フィールド: `full_name`、`visibility`、`default_branch`、`permissions`、`sha`。ローカルで `git rev-parse HEAD`、`git status --short` を照合。観測開始時刻は本書冒頭。
- **[O02]** [workflows](https://api.github.com/repos/dahatake/StudyReport-Evaluator/actions/workflows)、[Actions permissions](https://api.github.com/repos/dahatake/StudyReport-Evaluator/actions/permissions)、[workflow permissions](https://api.github.com/repos/dahatake/StudyReport-Evaluator/actions/permissions/workflow)。確認値は §3.1 に抜粋。
- **[O03]** [environments](https://api.github.com/repos/dahatake/StudyReport-Evaluator/environments)、[repository Secrets metadata](https://api.github.com/repos/dahatake/StudyReport-Evaluator/actions/secrets)、[`publish` Secrets metadata](https://api.github.com/repos/dahatake/StudyReport-Evaluator/environments/publish/secrets)。両 Secrets は `total_count=0`、名前配列は空。Environment の protection rules と branch policy を確認。
- **[O04]** [直近 run](https://github.com/dahatake/StudyReport-Evaluator/actions/runs/34141745681)、[jobs API](https://api.github.com/repos/dahatake/StudyReport-Evaluator/actions/runs/34141745681/jobs)、[失敗した Windows job](https://github.com/dahatake/StudyReport-Evaluator/actions/runs/34141745681/job/101805037516)。`gh run view 34141745681 --repo dahatake/StudyReport-Evaluator --log-failed` の関連行を抽出。失敗コードのログ時刻は `2026-09-07T16:27:30.8095624Z`。全ログを成果物へ転載していない。
- **[O05]** [過去成功 run](https://github.com/dahatake/StudyReport-Evaluator/actions/runs/33855982972)、[Windows job](https://github.com/dahatake/StudyReport-Evaluator/actions/runs/33855982972/job/100969346054)、[artifacts API](https://api.github.com/repos/dahatake/StudyReport-Evaluator/actions/runs/33855982972/artifacts)。`gh run download` で artifact 名 `unsigned-msix-mechanism-33855982972` を指定して取得し、`StudyReportEvaluator-win-x64.unsigned.test.evidence.json` と `.msix.sha256` の2件を確認。JSON 原本 hash と範囲は §3.3。MSIX 本体は未取得。
- **[O06]** [latest Release API](https://api.github.com/repos/dahatake/StudyReport-Evaluator/releases/latest)、[v0.8.1 公開ページ](https://github.com/dahatake/StudyReport-Evaluator/releases/tag/v0.8.1)。確認した asset は `StudyReportEvaluator-win-x64.zip`（156214373 bytes）、`StudyReportEvaluator-win-x64.zip.sha256`（99 bytes）。本調査で Release 本体の再ダウンロード検証はしていない。
- **[O07]** ローカル `git ls-files -- eng/packaging/windows` の結果: `AppxManifest.xml`、`tools/WindowsSdkBuildTools.csproj`、`tools/packages.lock.json` の3件。[同コミットのディレクトリ](https://github.com/dahatake/StudyReport-Evaluator/tree/77d1dd6eee858765a619e36b6ba5f27dd9255360/eng/packaging/windows)。未管理の所有者の画像保有までは調べていない。
- **[O08]** 2026-09-15 JST の最終ローカル確認: `git status --short`／`git diff --stat` に README／開発文書変更、過去 `work/` の削除表示、`dev/docs/archive/` と本レポートの新規表示あり。`git diff --check` は成功。アプリソース・workflow・packaging scripts の変更表示はなかった。本調査が作成・編集したリポジトリ内ファイルは本レポートだけ。ローカル観測であり、これらの未コミット変更に対応する GitHub 公開 URL はない。

### 14.3 GitHub 公式資料

全資料の閲覧日は **2026-09-15 JST**。ページ更新日を確認していない資料について、著作権年・検索表示・閲覧日を更新日として扱っていない。

- **[G01]** [Manually running a workflow](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow) — default branch、write access、Run workflow の Web UI 手順、CLI／API。
- **[G02]** [Events that trigger workflows: workflow_dispatch](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_dispatch) — event、ref、inputs、既定ブランチ条件。
- **[G03]** [GitHub-hosted runners reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners) — x64／ARM runner 表、`windows-latest`、public repository の標準 runner、image 更新・管理者実行。
- **[G04]** [Windows Server 2025 image](https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md)、[Windows Server 2022 image](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md) — 観測した image version はそれぞれ `20260907.255.1`／`20260907.297.1`。OS、PowerShell、npm、.NET、SDK の提供内容。動的な main ページであり、将来の版は変わり得る。
- **[G04b]** [GitHub Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions) — public standard runner、larger runner、artifact／cache 保管と契約枠。所有者の実請求ではない。
- **[G05]** [Managing environments for deployment](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments) — required reviewers、prevent self-review、許可 branch／tag、Secrets の利用条件。
- **[G06]** [Secure use reference](https://docs.github.com/en/actions/reference/security/secure-use) — 最小権限、入力 injection、action の SHA 固定、Secrets、self-hosted runner のリスク。
- **[G07]** [Using secrets in GitHub Actions](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets) — repository／Environment secrets、ログと引数、Base64 は暗号化ではないこと。
- **[G08]** [OpenID Connect](https://docs.github.com/en/actions/concepts/security/openid-connect)、[OpenID Connect reference](https://docs.github.com/en/actions/reference/security/oidc) — 短期 token、trust、`id-token: write`、immutable subject。対象 repository の実 claim を観測した資料ではない。
- **[G09]** [Artifact attestations](https://docs.github.com/en/actions/concepts/security/artifact-attestations) — provenance、Sigstore、検証用途と保証の限界。
- **[G10]** [Downloading workflow artifacts](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts) — read access、取得手順、保持期限。

### 14.4 Microsoft 公式資料

閲覧日はすべて **2026-09-15 JST**。英語 URL の一部は取得時に日本語版へリダイレクトされた。原文の対象が Store、unsigned 開発、virtualized app に限定される箇所を一般化せず使用した。

- **[M01]** [Sign an MSIX package](https://learn.microsoft.com/windows/msix/package/signing-package-overview) — valid／trusted な署名、署名方式、サービス利用資格、timestamp。概要の参考価格を所有者の見積もりとして流用していない。
- **[M02]** [Sign an app package using SignTool](https://learn.microsoft.com/windows/msix/package/sign-app-package-using-signtool) — 証明書、署名 tool、SHA-256 と block map、PFX／certificate store。
- **[M03]** [Create an unsigned MSIX package](https://learn.microsoft.com/windows/msix/package/unsigned-package) — Windows 11 開発限定、OID、実行コード入りパッケージの管理者導入、署名版と別 Identity。
- **[M04]** [SignTool reference](https://learn.microsoft.com/windows/win32/seccrypto/signtool) — `/sha1`、`/fd`、`/tr`、`/td`、`/pa`、`/tw`、終了コード。`/sha1` は証明書を選択する hash 指定であり、ファイル署名を SHA-1 にする指定ではない。
- **[M05]** [Create an app package with MakeAppx.exe](https://learn.microsoft.com/windows/msix/package/create-app-package-with-makeappx-tool) — pack／unpack、入力、限定的な検証でありインストール可能性を保証しないこと。
- **[M06]** [Create a certificate for package signing](https://learn.microsoft.com/windows/msix/package/create-certificate-package-signing) — Subject／Publisher、Code Signing EKU、自己署名の信頼・安全上の注意。
- **[M07]** [Identity schema](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-identity) — Identity／Publisher／4要素 version。取得時 canonical は `element-f-identity`。
- **[M08]** [App package requirements for MSIX app](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements) — Store 固有の Identity・版・認定、MSIX の再署名と CA 証明書購入不要。Store の規則を GitHub 直接配布に一律適用していない。
- **[M09]** [Understanding how packaged desktop apps run on Windows](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes) — packagedClassicApp／mediumIL、導入先、read-only、適用対象を限定したファイルシステム・削除の説明。
- **[M10]** [SmartScreen reputation for Windows app developers](https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation) — 有効な署名と reputation の区別、EV の非保証、端末 policy。
- **[M11]** [App Installer file overview](https://learn.microsoft.com/en-us/windows/msix/app-installer/app-installer-file-overview) — `.appinstaller` による更新と `ms-appinstaller:` の既定無効化。

### 14.5 証明書事業者の一次資料

- **[C01]** [DigiCert code signing certificates](https://www.digicert.com/signing/code-signing-certificates) — 2026-09-15 JST 閲覧。鍵保管方式に KeyLocker Cloud／token／HSM を示す。特定の契約、個人申請資格、当該アプリの MSIX 署名成功を証明する資料ではない。販売ページの一般的な警告抑止の表現より、Windows の挙動については Microsoft の [M10] を優先した。

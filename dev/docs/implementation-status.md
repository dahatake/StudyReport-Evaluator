# Current implementation status

## 2026-09-07 — F02文書同期時点の現在状態

現在の製品は**`0.8.5`未公開候補（UNRELEASED）**、要求はv4.6、公開済みは`v0.8.1`のunsigned ZIP／sidecarである。親担当が`Directory.Build.props`をPATCHし、F01のUnreleased追記はREVIEWED。以下は[親担当の実行記録](archive/work/20260907-ui-settings-execution-record.md)と後続引継ぎに基づく既存結果の同期で、この文書編集では端末・build・test・package・native操作を実行していない。

| 項目 | 状態・適用限界 |
|---|---|
| T01〜T38 | **REVIEWED**。実装・対象検証・敵対的レビュー・必要修正確認の範囲であり、全要件／全環境の受入完了ではない |
| T39 | **BLOCKED**。`0.8.4`の自動回帰とMSIX機構確認は成功したが、追加native観測のFAILと本人・Narrator・隔離利用者保存の外部前提が残る |
| F01／F02 | F01はREVIEWED、F02は版正本を`0.8.5`へ更新済み。本同期時点の最終版build／test／package再検証は親担当で未完了。以後の判定は実行記録の**最新F02欄**を参照し、この時点の保留を恒久状態にしない |
| 続行承認 | 利用者不在時の自律続行指示によりT39をBLOCKEDのままF01／F02を進める。G4・全タスクDONE・公開PASSを付与する承認ではない |
| native／本人確認 | 追加nativeは3試行で停止。Narrator、本人walkthrough4項目、隔離利用者環境のnative保存は`NOT_RUN_EXTERNAL_PREREQUISITE` |
| clean-host／公開 | CH-01〜06は`NOT_RUN_EXTERNAL_PREREQUISITE`、新EXEの公開gateは`BLOCKED_EXTERNAL`。candidate／protected publish・本人login・実AI・実学生data利用・公開操作の成功を主張しない |

### 記録済みの0.8.4検証（F02後の0.8.5へ流用しない）

| 対象 | 記録済み結果 | 証跡・範囲 |
|---|---|---|
| T35 | 4/4・敵対的レビュー済み、指摘0 | `artifacts/test/ui-settings/t35/t35-reviewed.trx`。T36とは別scopeの対象文書試験 |
| T36 | 21/21・REVIEWED | `artifacts/test/ui-settings/t36/t36-current.trx`。公開文書11件・画像8枚、設定境界、対象版、local links／anchorsのcontract。変更後のF02文書の再検証ではない |
| T37 | 9/9・REVIEWED | `artifacts/test/ui-settings/t37/t37.trx`。実ZIPのpublish／生成／再現性／展開起動とMSIX静的契約。MSIX実物の成功とは分離 |
| T38 | contract 114/114、P06 7/7、P07 `PASS_DEVELOPMENT`・REVIEWED | `artifacts/test/ui-settings/t38/t38.trx`と同`native/`配下。P02／P05のEXE生成と20ファイルの同梱、CLI、標準展開、移動・再起動・同時起動・Prompt設定を開発hostで確認 |
| T38／T39対象EXE | `0.8.4`、283,408,986 bytes | SHA-256 `4D80FA246EB64E6984B6D8B4A5EA37C1F62BCDD28C40F6D9A63F41522C89E507`。`0.8.5`のEXE identityではない |
| T39自動回帰 | Core 190/190＋App 1702/1702＝1892/1892、failed／error／notExecuted 0 | `artifacts/test/ui-settings/t39/reviewed/`の2 TRX（2026-09-07、+09:00、App終了23:06:45）。CI同等の3クラス除外（実学生sample構造・別実行ZIP・P06）と生成済みP02 artifact検査opt-inの集合。無効化されたLive AI等を実行済みとしない |
| T39 MSIX実物 | `PASS_MECHANISM`、256 entries、Identity Version `0.8.4.0` | `artifacts/test/ui-settings/f02/baseline-0.8.4/msix.evidence.json`。package／unpack／integrity／policy／cleanupのみ。install・署名・一般公開は未実施 |

T39の初回全体実行は1件FAILだった。設定済みExecution単体と未読込Inputを組み合わせたfixtureを、合成Excelの実読込から通常ナビゲーションでExecutionへ進む形へ修正し、期待を緩めず126/126・独立レビュー指摘0を確認後、上記の全体再実行が成功した。初回失敗は消さない。T18〜21の540/540、T23の275/275、T24の67/67、T25〜27の74/74、T28の216/216等を合算してfull gateを作らない。

P06の7/7は開発hostの限定証跡であり、disk-full、directory ACL、抽出中断、EXE／ZIP間checkpoint再開、fresh OS／本人loginの未実施範囲を埋めない。AC-032／TR-31はこの範囲だけの`VERIFIED_SCOPED`で、全faultの`PASS_REQUIRED`ではない。

追加nativeの最新記録は`artifacts/test/ui-settings/t39/native-final-attempt.json`。3試行とも成功せず、最新は`CONTROL_ID_PREDICATE_NOT_UNIQUE`でFAIL。120 DPI、実client 1475×1000 pixel＝1180×800 DIP、合成入力読込、正常終了、入力／EXE不変、実利用者setting.txtの開始前後不存在だけを確認した。4画面・設定5カテゴリ・1024×720・実keyboard／focusの追加検証は未完了で、再試行しない。T26のheadless測定をnative成功へ昇格させず、この部分観測で置き換えもしない。

本人walkthroughの4項目（入力／30・10配点、設定保存と復帰、再起動・明示適用、override未保存の識別）、Narrator、隔離利用者でのnative保存は未実施。実利用者設定・secretの読書／保存、本人login、実AI、実学生data、公開操作を追加しない。環境変数の差替えをOS利用者の隔離保証としない。

## 2026-09-07 — T34時点の0.8.4 UI／Settings状態（履歴）

以下のT34節の「現在」「未変更」「後続」は当時の`0.8.4`に限る。現行の版・進捗は上のF02節と実行記録を優先し、T34時点の未完了を現在の未実施根拠へ流用しない。

本節はT34の設計文書同期時点を記録する。実装はAppのSettings／ViewModel／View／Shell／composition、要求はv4.6、作業範囲は[UI・設定保存プランv2](archive/work/20260907-ui-settings-redesign-plan-v2.md)と[後続承認・実行記録](archive/work/20260907-ui-settings-execution-record.md)に基づく。計画書の承認前の「未着手」や途中の実行記録を現在の状態と混同せず、**T01〜T27のREVIEWEDは今回の親担当の完了引継ぎ**を採用する。

| 項目 | 現在の状態・適用限界 |
|---|---|
| 要求baseline | `docs/requirements-definition.md` v4.6 / 2026-09-07。UI／設定の所有・同期は[architecture](architecture.md)と[detailed-design](detailed-design.md)へ反映 |
| 製品版 | **`0.8.4` candidate（Unreleased）**。`Directory.Build.props`は未変更。F02の`0.8.5`へのPATCHは後続作業で、要求版v4.6や公開版とは独立 |
| UI／Settings実装 | **T01〜T27 REVIEWED（対象範囲）**。4ステップ＋独立設定5カテゴリ、明示保存／適用、最新draft同期、現在runと前回結果の分離を実装済み |
| 対象検証の性質 | 実Avalonia View／bindingのheadless操作、一時pathの実設定file／合成workbook、fake認証／AI。対象試験と親担当レビューの完了であり、全製品・全環境のPASSではない |
| UI変更後の全体Release build／full required gate | **NOT_RUN（今回差分）**。下の2026-09-06 V01の995/995等は当日のbaselineであり、新UI・設定・文書を含む現行treeの再検証ではない |
| UI変更後の配布実物 | **NOT_RUN（今回差分）**。最終EXE／ZIPの再build・package・payload／sidecar照合は未完了。旧版のexact bytes／hash証跡を転記しない |
| native Windows／本人walkthrough | **NOT_RUN**。実DPI／Narrator・隔離利用者環境での実保存・本人確認をheadless成功で代替しない |
| 本人login／実AI／Office再計算 | 今回は未実施。T27のreal-file成功もこれらの成功ではない。実学生sampleは今回のUI／設定fixtureに使わない |
| 新EXEの公開 | **未公開**。CH-01〜06、別途の公開指示、candidate／protected publishと最終bytes照合が必要。公開済み`v0.8.1` ZIPの履歴は維持 |
| T34の検証範囲 | 実装source・計画・既存文書・保存済みTRX集計の読取と、この3文書の同期のみ。ターミナル／build／test／package／アプリ起動は行っていない。独立レビュー・文書contract実行は後続担当で判定 |

### 親担当から受領した限定証跡

T34では以下の既存TRXの集計を読取確認しただけで、試験を再実行していない。各対象集合には重複し得る既存回帰があるため、合算して新たな全件数やfull gateを作らない。

| 対象 | 親担当の最終結果 | 証跡・範囲 |
|---|---|---|
| T18〜T21 | **540/540** | `artifacts/test/ui-settings/t18-21/t18-21-reviewed.trx`。主画面の対象集合。初回の失敗記録は履歴として保持 |
| T23 | **275/275** | `artifacts/test/ui-settings/t23/t23-fixed.trx`。composition／cached shellと関連回帰。T18〜21から分離した旧shell固定領域試験も含む |
| T24 | **関連67/67（うち新規12ケース）** | `artifacts/test/ui-settings/t24/t24.trx`。[WorkflowStateTests](../../tests/StudyReportEvaluator.App.Tests/UI/WorkflowStateTests.cs)の実shell往復・保存・run固定／停止・結果保持と関連回帰 |
| T25〜T27 | **合同74/74** | `artifacts/test/ui-settings/t25-27/t25-27-reviewed.trx`。keyboard／警告／認証回帰、通常／例外layout、設定E2E。途中の`t25-27-fixed.trx`は67/74であり最終成功記録ではない |
| T27内訳 | **上記74件に含まれるreal-file 7ケース** | [SettingsWorkflowSystemTests](../../tests/StudyReportEvaluator.App.Tests/E2E/SettingsWorkflowSystemTests.cs)。追加の7件として二重計上しない。4回答行の合成入力と実file処理で、native UI／実AIではない |

採用した4つの最終TRXは、集計上failed／error／notExecutedがいずれも0。`REVIEWED`の判定自体は親担当の実装・対象検証・敵対的レビュー・必要修正確認の引継ぎに基づき、T34がそのレビューを再実行したという意味ではない。

T27は実`InputWorkbookLoader`、`SettingsFileStore`、`DurableQuantificationOrchestrator`、checkpoint、4 sheet writer、validator、atomic commitを通す。設定save→新store／VM→load→保存定義の明示適用→fake run→実final／別名override出力、取消・新VMによるresume、適用拒否時の無変更、cached score改変時のcommit拒否を検証する。test専用adapterで認証・model／runtime identity・時刻・AI runnerを合成に置換しており、RunSummaryやfile成功receiptだけをfakeにして成功扱いにしたものではない。設定・遷移の呼出し0と、明示確認／実行後のfake呼出しを区別する。

### 実装された所有・安全境界

| 境界 | 確認した実装 |
|---|---|
| 保存共通値／任意定義 | `ApplicationSettings`はschema整数1、通常model希望ID・並列度1〜3・absoluteな明示出力先またはnull・任意定義1件。strict JSON／UTF-8、同directory tempのwrite／flush／close後の置換。自動保存／移行／mergeはなく、失敗時は旧fileとdraftを保持 |
| productionとtest構成 | `Program.BuildAvaloniaApp` → `ServiceRegistration.FromStartup`でOS LocalApplicationDataを解決し、`App.CreateMainWindow`経由のwindow構築後だけ初期読込を接続。従来の既定constructorはnull storeで設定I/Oなし。testのread／writeは一時absolute pathに限定 |
| 最新採点draft | Input／Designが既存draftを所有し、Settingsは`latestEditor`から一方向に同期して保存する。Design VMと残る子editorを再利用。共通値の変更で採点編集元を切り替えず、入力未読込時は保存済み定義を保持 |
| 保存値と適用値 | model希望と確認済み実効選択、明示出力先とnull時だけの入力隣接resultを分離。保存定義は起動時に保持するだけで、header metadata・入力identity・mapping検証後の明示操作でだけ適用。run中は一括適用不可 |
| 表示と編集 | ShellはVM参照ごとに5種類のViewをcacheし、SettingsもカテゴリごとのViewをoff-treeで保持。未確定text・選択・内部tabを不要に再生成せず、設定終了時は有効な元controlへfocusを戻す。主画面の編集値は詳細側で読取専用再表示し、入力欄を二重配置しない |
| 現在run／前回結果 | 開始request／snapshotを固定し、次回設定を混入させない。Settings中の完了で強制遷移せず、全画面の進捗入口／停止を維持。Resultsの元criterionにoverrideを保持し、元行／ページ外エラー移動・単一詳細・別名出力・修正版未保存状態を分ける |
| 機密性／自動処理 | 設定は明示保存時にheader由来設問文・貼付Prompt等を平文で含み得る。入力xlsx path／bytes、回答の自動収集、AI結果、credential／認証、run／checkpoint、未適用Prompt一覧、UI状態は非保存。設定・遷移から認証確認／login／AIを自動開始しない |
| 維持した既存契約 | Core／Appの2 project、4 final sheets・formula／cached preview、exact100・zero／blank、checkpoint schema／admission、bundled resolver・所有login process・SDK固定版は継承。新しい全体回帰の成功を意味しない |

### レビューと後続担当への依存

- **T24／T27 → T34**の前提は上記の完了引継ぎ。本3文書を実sourceと照合する独立レビューでは、設定の保存対象、最新editor、固定request／Results、no-auto-auth／AIの境界を優先する。
- **T26親担当／T35**: `ui-layout-contract.md`の実測欄や要求・追跡台帳に残る途中時点のNOT_RUN／未実装表記は、それぞれの所有担当が実証跡に基づいて同期する。T34は実ClientSize・表示件数等を推測せず、要求／traceability／claim ledger／SystemTest／実行記録を編集しない。
- **T35／T36**: 要求ID対応と文書・画像contractを、本設計の5カテゴリ／保存境界／現行状態へ同期して検証する。本3文書の静的編集だけでcontract PASSを付与しない。
- **T37／T38／T39**: 文書・画像の作成側／検証側allowlist同期、最終payload、全体Release／required試験、native Windowsと本人walkthroughが必要。headlessの限定成功と旧package証跡を代用しない。
- **F01／F02**: 全タスク後のUnreleased追記、続いてPATCH `0.8.4` → `0.8.5`と最終版の文書・期待値・artifact再検証を行う。T34ではCHANGELOG・版・公開物を変更しない。

## 2026-09-06以前の実装・配布記録（履歴を保持）

以下の既存表、Delivery status、実装済みsurface、Historical v3 gate recordは、それぞれに記された日付・source・artifactに限定した履歴である。「Current」「Latest」等の表記も当時の意味として残し、2026-09-07のUI／Settings変更後の全体合格や配布実物の再検証を表すものではない。過去ADR、試験件数・日付・hashは変更していない。

| 項目 | 値 |
|---|---|
| Current requirement | `docs/requirements-definition.md` v4.5 |
| Scope ADR | ADR-0012（機能）/ ADR-0015（Windows ZIP public / development MSIX）/ ADR-0013（Windows public evidence）/ ADR-0016（Windows単一EXE 1操作起動） |
| Product version | `0.8.4` candidate（UNRELEASED）— `Directory.Build.props`の明示`VersionPrefix` |
| One-action startup implementation | App限定single-file profile、EXE package、login専用service／UI、matrix v2、candidate／protected publish contractを実装済み。公開済みではない |
| One-action startup deterministic validation | P01〜P07／A01〜A03／C01〜C07の直接検証とレビューを実施。C03は18/18 PASS。V01は0.8.4最終artifactで実行済み |
| Latest development-host EXE evidence | `PASS_DEVELOPMENT` — V01再package後の0.8.4 EXE 282,949,002 bytes、SHA-256 `E03553BE…C382E`。開発hostのみの観測でありclean-host証跡ではない |
| V01 full opt-in gate | Core 190/190 + App 805/805 = 995/995 PASS（failed 0、skipped 0、opt-in artifact／package testを含む、2026-09-06実行） |
| R03 version status | `PASS` — `0.8.3`から`0.8.4`へPATCHし、Unreleasedへ変更を追記。0.8.4の最終EXE／ZIPをV01で再生成・再検証済み |
| V02 fresh-host validation | `SKIPPED_BY_INSTRUCTION` — 2026-09-06、要求所有者の明示指示によりfresh Windows clean-host試験を実施していない。実施済みやPASSへ読み替えない |
| Fresh Windows clean-host / personal login | `NOT_RUN_EXTERNAL_PREREQUISITE` — CH-01〜CH-06は未実施。開発host、fake、CLI help、contract testで代用しない |
| New single-file public release | `BLOCKED_EXTERNAL` — exact最終EXEのCH-01〜06、別途の公開指示、candidate／protected publish実行まで公開しない |
| Version management | [`version-management.md`](version-management.md) / [`dev/version.ps1`](../version.ps1) / ADR-0014 |
| Public release source | release commit `d0b03b9201d397b6c3333dafbb816b13b4dc003c`、annotated tag `v0.8.1` |
| Public release status | `PUBLISHED — v0.8.1、2026-09-04T08:52:49Z公開、ZIPとsidecarの2 asset` |
| Audited candidate source | `20c8121c2474a13f409c0d7f0fde9d4c41f74698`（`0.8.3`、UNRELEASED） |
| Audited v4.4 Release build | source `20c8121`でPASS、warning 0 / error 0（2026-09-05実行） |
| Previous v4.3 full required gate | 696/696 PASS（Core 190/190、App 506/506、failed 0、skipped 0、2026-09-04 V2-01実行） |
| Recorded v4.4 focused input validation | 46/46 PASS（InputViewTests 21、WorkbookMetadataReaderTests 11、ColumnMappingSuggesterTests 14。failed 0、skipped 0、2026-09-05実行） |
| Audited v4.4 full required gate | source `20c8121`のbaseline: Core 190 + App deterministic 524 + sample構造 1 + Windows ZIP 3 = 718/718 PASS（App計528、failed 0、skipped 0、2026-09-05実行） |
| Current review validation | [修正記録](archive/work/20260905-adversarial-review.md)と`artifacts/test/adversarial-review/final/summary.json`を参照。対象source一致かつ全体`PASSED`・TRX検査成功時だけ合格。未生成／`RUNNING`／`FAILED`は未完了 |
| Baseline focused validation | locked restore、version self-test 15 assertions、DocumentationContractTests 14/14、WindowsInstallerPackageTests 6/6、MacOsPublishPackageTests 2/2、`git diff --check`が全てPASS（2026-09-04実行） |
| Previous focused UI baseline | `MainWindowTests` 8/8 PASS（2026-09-02実行。current candidate full gateへ未算入） |
| Previous bundled CLI baseline | `CopilotClientFactoryTests` 14/14 PASS（2026-09-02実行。current candidate full gateへ未算入） |
| Previous documentation contract | 14/14 PASS（2026-09-04実行。root `SystemTest-prompt.md`、25 scenario、TR-01〜TR-29、v4.3 canonical sample契約を含む） |
| Recorded v4.4 documentation contract | 14/14 PASS（2026-09-05実行。requirements、利用者文書、root `SystemTest-prompt.md`、25 scenario、TR-01〜TR-29、input synchronization契約を含む） |
| Previous screenshot baseline | 7画像再生成、1440×1050、test 1/1 PASS、敵対review finding 0（2026-09-02実行。current candidate full gateへ未算入） |
| Recorded v4.4 screenshot validation | 7画像再生成1/1 PASS、全画像1440×1050、全7画像目視確認（2026-09-05実行） |
| Audited Windows ZIP package validation | source `20c8121`で3/3 PASS。publish、package、sidecar、safe layout、clean launch、再現性を検証し、App/CoreのAssembly/File/Product versionが0.8.3で一致（2026-09-05実行） |
| Previous Windows ZIP integration baseline | publish/package/展開/resolver/clean launch/repackage、全class 3/3 PASS（2026-09-02実行。current candidate gateへ流用しない） |
| B1-16 adversarial review | RealData evidence contractの曖昧性を計画へ反映。source identity assertionはPASS、opt-in E2E／Live AIは非実行。未解決の再現可能なblocker/high finding 0 |
| Previous canonical sample baseline | canonical `sample/SampleReport.xlsx` exact path・identity・structure・input不変 1/1 PASS、synthetic fixture isolation 1/1 PASS（2026-09-03実行。current candidate full gateへ未算入） |
| Previous canonical technical E2E baseline | 530行、2,650 normal evaluations、5 references、535 checkpoint updates、no-network primary application path 1/1 PASS（2026-09-03実行。current candidate full gateへ未算入） |
| Previous 10-person system smoke baseline | fixed 10-person fixtureのidentity/formula canaryとuninterrupted/interruption-resume同値性 2/2 PASS（2026-09-03 full run。current candidate full gateへ未算入） |
| Previous synthetic 531-row journey baseline | fixed-seed local atomic journeyとdurable new/resume同値性 2/2 PASS（2026-09-03 full run。current candidate full gateへ未算入） |
| Previous functional baseline | Release 670/670 PASS、Core 190/190・App 480/480、failed/error/not-executed 0（2026-09-03、v1.0.1 recovery tree。現在の0.8.0 delivery証跡へ流用しない） |
| Previous optional live Copilot evidence | `PASS` — fixed synthetic payload、required substitute false（2026-09-02実行。current required gateへ算入しない） |
| Previous optional external recalculation evidence | `MIXED_ADVISORY` — syntheticはMicrosoft Excel 16.0でopen/recalculate/save・process cleanupまで`PASS`。canonical output copyは20,672 formula error 0だが、Excel保存時に生成されたoptional `/xl/calcChain.xml`だけに`Sem_MissingIndexedElement`が発生して`FAILED_ADVISORY`（2026-09-03実行。current required gateへ算入しない） |
| Version tool validation | `0.8.3`でshow/verify/set/bump/dry-run/invalid rejection、複数Git pathでのtag/clean検証、15 assertions PASS（2026-09-05実行） |
| Development MSIX mechanism | `PASS_MECHANISM` — package/unpack/hash/policy/cleanup、0.8.3.0、install未実行・非required（2026-09-05実行） |
| macOS static source contract | 2/2 PASS — unsigned bundle最小contractとproduction trust順序のsource検証。production artifact／署名／公証の実測ではない（2026-09-04 B1-16実行） |
| Audit date | 2026-09-06 |

監査済みgateの集約記録は`artifacts/test/final-recovery-20c8121/phases.json`。718/718 PASSは`20c8121`に限定し、opt-in未指定で`NOT_RUN`としてreturnする経路やpolicy経路を含む。実際のLive AI、外部Excel再計算、RealData処理は`NOT_RUN`で、sample構造確認やfake/synthetic testとは区別する。focused／screenshot等の既存記録も各記録日時点のbaselineであり、今回reviewの修正を検証したものではない。証拠の保存・適用限界は[Evidence integrity and storage](traceability.md#evidence-integrity-and-storage)を参照。

公開済みdeliveryの履歴は[`20260904-publication-remediation-plan.md`](archive/work/20260904-publication-remediation-plan.md)と[ADR-0015](adr/0015-windows-macos-installer-delivery.md)に従い、`v0.8.1`のunsigned ZIP／sidecar 2件を維持する。現行v4.5 candidateは[ADR-0016](adr/0016-windows-one-action-startup.md)に従い、unsigned単一EXEを将来の主配布、ZIPを代替として実装した。単一EXEはfresh clean-host CH-01〜06とprotected publish完了まで未公開である。development MSIXはnon-public `PASS_MECHANISM`、macOS production deliveryは現版scope外のままである。

2026-09-03のdelivery変更前working treeを再buildしたfull solution testは670件中670件成功した。ユーザーが配置した`sample/SampleReport.xlsx`だけをcanonical sampleとし、exact path、470,806 bytes、SHA-256 `73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA`、read-only検査前後のidentity不変を確認した。opt-in technical E2Eはnetwork/live AIを使わず530行を完走した。これらは機能regressionの比較baselineであり、0.8.0のMSIX／macOS／signing／notarization／quarantine結果ではない。delivery変更後は全required regressionを再実行する。

2026-09-04のV2-01はrelease commitのclean treeでlocked restore、Release build、full required deterministic gateを実行し、696件中696件が成功した。Windows ZIP regressionはhosted CIのclean checkoutとcandidate workflowでも`PASS_REQUIRED` evidenceを生成している。optional Live Copilot、external recalculation、RealData system smokeはrequired gateへ算入していない。

## Delivery status — v0.8.1公開履歴 / v4.5 current candidate

| Surface | Current status | Completion evidence |
|---|---|---|
| Windows single-file profile／package／login deterministic contract | `PASS_REQUIRED`（実装・開発host範囲） | App限定profile、最終EXE／sidecar、標準抽出、CLI integrity、cwd／Prompt、cache fault、login process所有境界の直接test。本人loginやclean-hostを含まない |
| Windows single-file development-host evidence | `PASS_DEVELOPMENT`（再package待ち） | P07で実EXEの7 package testと9観測を照合。後続文書変更でbundle bytesが変わるため最終候補へ流用しない |
| Windows single-file clean-host CH-01〜06 | `NOT_RUN_EXTERNAL_PREREQUISITE` | fresh Windows 11 x64標準user、MOTW／保護状態、本人loginの実測待ち |
| Matrix v2／candidate／protected publish contract | `PASS_REQUIRED`（deterministic contractのみ） | closed 3 rows、public 4 assets、MSIX非公開、candidate identity、clean-host JSON、公開直前再検証をC01〜C07で検証。live workflowは未実行 |
| New single-file public release | `BLOCKED_EXTERNAL` | clean-host証跡、別途のtag／push／draft／公開指示、実workflow実行が未完了 |
| Windows public ZIP regression | `PASS_REQUIRED` | 公開`v0.8.1`の履歴: clean checkoutでpublish/package/hash/safe layout/bundled CLI/clean launch/入力不変を検証し、closed evidenceを生成（hosted CIとcandidate workflow） |
| Full required regression（公開v0.8.1履歴） | `PASS_REQUIRED` | Core 190/190、App 506/506、合計696/696（2026-09-04 V2-01） |
| Canonical technical E2E | `NOT_RUN_CURRENT_CANDIDATE` | opt-in no-network E2Eはrequired gate外。current candidateでは実行していない |
| Windows development MSIX mechanism | `PASS_MECHANISM` | `20c8121`（`0.8.3`）でmanifest/version/RID、unpack、block map、payload、CLI、sidecar、policy negative、cleanup |
| Development MSIX install/launch | `NOT_RUN_NOT_REQUIRED` | 現版のrequired gateではなく、一般配布しない |
| macOS source foundation static contract | `PASS_REQUIRED` | `MacOsPublishPackageTests` 2/2。production signing／notary evidenceではない |
| macOS production delivery | `EXCLUDED_CURRENT_SCOPE` | public artifact／support claimなし |
| Windows ZIP public setup docs | `PASS_REQUIRED`（baselineのみ） | `20c8121`のdocumentation contract 14/14はbaseline。今回の文書修正は上表のCurrent review validationで別に判定 |
| Release matrix/workflow | `PASS_REQUIRED`（local deterministic contractのみ） | `v0.8.1`は公開／public re-download確認済みの履歴。v4.5 matrix v2とcandidate／protected publish contractはlocal test済みだが、現行candidateのworkflow実行・公開後re-downloadを意味しない |

## 実装済みsurface

| Surface | Production owner | Required evidence |
|---|---|---|
| `.xlsx` classification / immutable input | [`FileFormatClassifier.cs`](../../src/StudyReportEvaluator.App/Workbooks/Intake/FileFormatClassifier.cs)、[`InputSnapshotService.cs`](../../src/StudyReportEvaluator.App/Workbooks/Intake/InputSnapshotService.cs) | [`FileFormatClassifierTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/Intake/FileFormatClassifierTests.cs)、[`InputSnapshotServiceTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/Intake/InputSnapshotServiceTests.cs) |
| picker / mapping / question text synchronization | [`InputViewModel.cs`](../../src/StudyReportEvaluator.App/ViewModels/InputViewModel.cs)、[`InputView.axaml.cs`](../../src/StudyReportEvaluator.App/Views/InputView.axaml.cs)、[`ColumnMappingSuggester.cs`](../../src/StudyReportEvaluator.App/Workbooks/Mapping/ColumnMappingSuggester.cs)、[`ColumnMappingValidator.cs`](../../src/StudyReportEvaluator.App/Workbooks/Mapping/ColumnMappingValidator.cs) | [`InputViewTests.cs`](../../tests/StudyReportEvaluator.App.Tests/UI/InputViewTests.cs)、[`WorkbookMetadataReaderTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/Reading/WorkbookMetadataReaderTests.cs)、[`ColumnMappingSuggesterTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/Mapping/ColumnMappingSuggesterTests.cs) |
| v4 domain / allocation / snapshot | [`Domain`](../../src/StudyReportEvaluator.Core/Domain/)、[`ScoringAllocationCalculator.cs`](../../src/StudyReportEvaluator.Core/Scoring/ScoringAllocationCalculator.cs)、[`QuantificationDefinitionValidator.cs`](../../src/StudyReportEvaluator.Core/Validation/QuantificationDefinitionValidator.cs) | [`Domain tests`](../../tests/StudyReportEvaluator.Core.Tests/Domain/)、[`Scoring tests`](../../tests/StudyReportEvaluator.Core.Tests/Scoring/)、[`QuantificationDefinitionValidatorTests.cs`](../../tests/StudyReportEvaluator.Core.Tests/Validation/QuantificationDefinitionValidatorTests.cs) |
| Prompt / selected-row payload | [`Prompting`](../../src/StudyReportEvaluator.Core/Prompting/) | [`Prompting tests`](../../tests/StudyReportEvaluator.Core.Tests/Prompting/) |
| reference / normal / special / similarity Copilot operations | [`Copilot`](../../src/StudyReportEvaluator.App/Copilot/) | [`Copilot fake-runtime tests`](../../tests/StudyReportEvaluator.App.Tests/Copilot/) |
| score / formula AST | [`Scoring`](../../src/StudyReportEvaluator.Core/Scoring/)、[`Formulas`](../../src/StudyReportEvaluator.Core/Formulas/) | [`Scoring tests`](../../tests/StudyReportEvaluator.Core.Tests/Scoring/)、[`Formula tests`](../../tests/StudyReportEvaluator.Core.Tests/Formulas/) |
| Config / References / Results / Run / partial checkpoint / atomic output | [`Workbooks`](../../src/StudyReportEvaluator.App/Workbooks/)、[`Workflow`](../../src/StudyReportEvaluator.App/Workflow/) | [`Workbook tests`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/)、[`Workflow tests`](../../tests/StudyReportEvaluator.App.Tests/Workflow/) |
| 4-step UI / keyboard / 200% | [`Views`](../../src/StudyReportEvaluator.App/Views/)、[`ViewModels`](../../src/StudyReportEvaluator.App/ViewModels/) | [`UI headless tests`](../../tests/StudyReportEvaluator.App.Tests/UI/) |
| Windows x64 unsigned single-file EXE | [`WindowsSingleFile.pubxml`](../../src/StudyReportEvaluator.App/Properties/PublishProfiles/WindowsSingleFile.pubxml)、[`publish-windows.ps1`](../../scripts/publish-windows.ps1)、[`package-windows-singlefile.ps1`](../../scripts/package-windows-singlefile.ps1) | [`WindowsSingleFileProfileTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsSingleFileProfileTests.cs)、[`WindowsSingleFilePublishTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsSingleFilePublishTests.cs)、[`WindowsSingleFileArtifactTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsSingleFileArtifactTests.cs)、[`WindowsSingleFilePackageTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsSingleFilePackageTests.cs) |
| Bundled CLI login initiation | [`BundledCopilotLoginService.cs`](../../src/StudyReportEvaluator.App/Copilot/BundledCopilotLoginService.cs)、[`ExecutionViewModel.cs`](../../src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs) | [`BundledCopilotLoginServiceTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Copilot/BundledCopilotLoginServiceTests.cs)、[`CopilotLoginCommandTests.cs`](../../tests/StudyReportEvaluator.App.Tests/UI/CopilotLoginCommandTests.cs)。本人loginはCH-06待ち |
| Windows x64 unsigned ZIP / bundled CLI regression | [`publish-windows.ps1`](../../scripts/publish-windows.ps1)、[`package-windows.ps1`](../../scripts/package-windows.ps1)、[`CopilotClientFactory.cs`](../../src/StudyReportEvaluator.App/Copilot/CopilotClientFactory.cs) | [`WindowsPublishPackageTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsPublishPackageTests.cs)、[`CopilotClientFactoryTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Copilot/CopilotClientFactoryTests.cs) |
| Windows development MSIX | [`eng/packaging/windows`](../../eng/packaging/windows/)、[`package-windows-msix.ps1`](../../scripts/package-windows-msix.ps1)、[`test-windows-msix-unsigned.ps1`](../../scripts/test-windows-msix-unsigned.ps1) | [`WindowsInstallerPackageTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsInstallerPackageTests.cs)、mechanism evidence |
| macOS source foundation | [`eng/packaging/macos`](../../eng/packaging/macos/)、macOS publish/package/sign/notary scripts | [`MacOsPublishPackageTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/MacOsPublishPackageTests.cs) static contract |

詳細なAC/TR対応は[`traceability.md`](traceability.md)を参照してください。

## Historical v3 gate record

以下はv3 gate後に閉じたgapの履歴であり、現在のv4.4 requirement acceptanceや今回のtest結果を表さない。

### IMPL-GAP-001 — CLOSED: Custom Promptのrun開始前再検査

### Normative requirement

未知placeholder、未閉鎖brace等は実行前に拒否し、invalid definitionはrun開始前にfield errorを表示する必要があります。現行対応節: [要求定義書 §5.4](../../docs/requirements-definition.md#54-prompt-placeholder)、[§16](../../docs/requirements-definition.md#16-failure-behavior)

### Closure evidence

1. Knowledge / Custom ownership、built-in version、placeholder構文を[`QuantificationDefinitionValidator.cs`](../../src/StudyReportEvaluator.Core/Validation/QuantificationDefinitionValidator.cs)へ統合しました。
2. Design、Execution `CanStart`、`QuantificationSnapshot.Create`が同じCore validation outcomeを使用します。
3. unknown / unclosed / malformed / unmatched brace、必須placeholder欠落、Knowledge / Custom ownershipをCore testで検証します。
4. [`QuantificationOrchestratorTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Workflow/QuantificationOrchestratorTests.cs)はinvalid Promptでinput capture、row read、runner callがすべて0件であることを検証します。sessionはrunnerより下流なので作成されません。

Closure commit: `69e4b992711c243fe7c70b0defff5e6abf03865c`。

### IMPL-GAP-002 — CLOSED: Excel / request capacityのrun前preflight

### Normative requirement

Results列数、formula長、function arguments、request budget等から実行可能上限をrun前に計算し、超過definitionを黙って切り詰めず拒否する必要があります。現行対応節: [要求定義書 §8.5](../../docs/requirements-definition.md#85-formula-ownership)、[§15](../../docs/requirements-definition.md#15-performancecapacity)

### Closure evidence

- [`WorkbookExecutionPreflight.cs`](../../src/StudyReportEvaluator.App/Workbooks/Writing/WorkbookExecutionPreflight.cs)はsnapshotとworkbook metadataからConfig address map、Results layout、最終rowの実formula ASTをI/Oなしで構築します。
- `ResultsSheetWriter.Preflight`とexportが同じlayout / `FormulaExpressions` / `FormulaPreflightValidator`を共有し、列16,384、cell 32,767、formula 8,191、function 255、reference、DAGを同じ判定にします。
- [`EvaluationRequestCapacityValidator.cs`](../../src/StudyReportEvaluator.App/Copilot/EvaluationRequestCapacityValidator.cs)は全selected rowの実payloadとclosed schemaをAI dispatch前に構築し、app-owned request 65,536 Unicode scalars、SDK model prompt/context上限の80%を保守的UTF-8境界で検査します。UTF-8 byte数をtoken実測値とは表記しません。
- evaluation units × 最大3 attemptsは20,000以下を要求します。Execution UIはfield、actual、limitだけを表示し、回答またはPrompt本文を含めません。
- inputはrequest preflight後にexact hash / size / last-write timeを再確認し、drift時はrunner call 0で停止します。

Closure commit: `69e4b992711c243fe7c70b0defff5e6abf03865c`。

### Historical evidence boundary

旧GATE-ACCEPTANCE artifactは上記2差分を検出する前に生成されたため、artifact自体を書き換えていません。2差分のcode/test closure、独立レビュー、文書同期後のlocked restore / Release build / 485 testsを評価HEAD `3f4227e`で再実行し、新しいgenerated recordは`PASS`です。optional 2件も固定合成データで`PASS`しましたがrequired判定へ算入していません。

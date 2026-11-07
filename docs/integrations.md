# 外部連携カタログ

このアプリが API・ファイル・DB・イベントなどで行う、外部のアプリ・データとの連携を、1 連携 1 項目で記録します。外部連携がないアプリは、このファイルを使わなくてかまいません（任意です）。
追加・変更・廃止は rd-author だけが行い、変更は要求定義書の変更履歴に記録します。ID は `python scripts/next-id.py INT` でだけ採番します。検査は `python scripts/verify.py --docs-only` です。

## 書式

見出しは `#### INT-001 <名前>` の形式で、その下に「欄名: 値」の箇条書きを書きます。

- 必ず値を書く欄: 連携先、方向（受信／送信／双方向）、方式、データ分類、個人情報（あり／なし）、認証認可、責任分界、状態（検討中／運用中／廃止）、関連する要求（FR-xxx、NFR-xxx-xxx）
- 次の欄は、値、`なし`、`TBD（Q-xxx）`（質問票の ID）のどれかで必ず埋めます: 頻度と量、可用性・SLA、レート制限、エラー・再試行・冪等性、契約・バージョン、変更通知、監視、データ保持、障害時の代替、法務・ライセンス
- 認証情報の値（トークン・パスワード・鍵・接続文字列）は、この文書に書きません。認証認可の欄には方式の名前を、「秘密の保管場所」の欄には保管場所の名前（例: Key Vault の名前）だけを書きます。値らしい文字列は検査が違反として報告します。
- チェックリストは `.github/skills/requirement-definition/integration-checklist.md` にあります。

  ```text
  #### INT-001 受注 API（例。実在する連携ではありません）
  - 連携先: 取引先 A の受注システム
  - 方向: 受信
  - 方式: REST（HTTPS）
  - データ分類: 取引データ（社外秘）
  - 個人情報: あり
  - 認証認可: OAuth 2.0 クライアントクレデンシャル
  - 秘密の保管場所: Key Vault（名前は運用手順書に記載）
  - 責任分界: 取引先 A が受注データの正確性、当社が受信後の保管と処理に責任を持つ
  - 状態: 検討中
  - 関連する要求: FR-012
  - 頻度と量: TBD（Q-010）
  - 可用性・SLA: なし
  - レート制限: なし
  - エラー・再試行・冪等性: なし
  - 契約・バージョン: なし
  - 変更通知: なし
  - 監視: なし
  - データ保持: なし
  - 障害時の代替: なし
  - 法務・ライセンス: なし
  ```

## 索引

| ID | 名前 | 連携先 | 方向 | 状態 | 関連する要求 |
|---|---|---|---|---|---|
| INT-002 | 既存GitHub Actions／Releases配布 | GitHubの当該repository | 双方向 | 運用中 | FR-121 |
| INT-001 | 既存Copilot参照回答生成 | GitHub Copilot SDK／§3.2の配布・開発別Copilot CLI | 双方向 | 運用中 | FR-115、FR-120、FR-121、FR-AL-01〜08 |

## 項目

#### INT-002 既存GitHub Actions／Releases配布
- 連携先: GitHubのdahatake/StudyReport-Evaluator。source・tag・Release metadataは当該repositoryが正本。対象はFR-121、出典は要求定義書SRC-010・SRC-011。
- 方向: 双方向
- 方式: 既存GitHub ActionsのCI・手動candidate・protected publishとGitHub ReleasesのAPI／ファイル配布。今回外部書込は行わない。
- 頻度と量: 既存の手動stable tag指定ごと。配布集合は要求定義書§13.6のclosed setで、新しい自動定期実行・配布対象は追加しない。
- データ分類: 公開予定のEXE／ZIPとSHA-256 sidecar、内部control evidence。sample・学生本文・利用者workbook・secret・MSIX本体は公開しない。
- 個人情報: なし
- 認証認可: 既存GITHUB_TOKENとprotected environment公開承認。通常CI／PR・build検証にRelease書込権限を付与しない。Release変更／他run証跡参照だけへ必要権限を限定する（AC-101）。新規PATは要求しない。
- 秘密の保管場所: GitHub Actionsの既存token管理。値をdocs・log・成果物へ転記しない。
- 可用性・SLA: なし（既存以上の可用性保証を追加しない。失敗時は非公開で停止）。
- レート制限: 追加の数値契約なし。GitHubの拒否・制限・通信失敗を公開成功へ変換せず停止理由を記録する。
- エラー・再試行・冪等性: AC-028・AC-102。同tag競合、既存asset不一致、部分失敗では上書きせず停止。同一検証済み結果は無変更成功。公開済みReleaseをdraftへ戻さない。
- 契約・バージョン: 既存stable annotated tag、source commit、製品版、最終bytesとmatrix v2の拘束（AC-100）。公開前のclean-host必須条件を維持する。
- 変更通知: 保守担当者が既存workflow／配布script変更時に既存Release契約テストの回帰を確認。新規の通知期限・通知基盤は追加しない。
- 監視: Actions run結果、tag・source commit・candidate run・停止段階を保守担当者が確認。新規監視サービス・secret出力は追加しない。
- データ保持: 公開assetと内部control artifactを区別し、既存の保持設定を変更しない。今回新しい保持期限・自動削除を追加しない。
- 障害時の代替: 公開せず停止。原因解消後、既存candidate／protected publishを承認範囲内で再実行する。検証・公開承認の迂回は禁止。
- 責任分界: GitHubはActions／Releaseサービス、保守担当者はsource・検証結果・公開承認・障害対応を管理。人のclean-host証跡をhashだけで実施済みと認定しない。
- 法務・ライセンス: 既存LICENSEと配布allowlistを維持し、新しい契約・再配布対象・公開先を追加しない。今回の公開は禁止。
- 状態: 運用中
- 関連する要求: FR-121

#### INT-001 既存Copilot参照回答生成
- 連携先: GitHub Copilot SDK／要求定義書§3.2の配布・開発別Copilot CLI。Windows配布EXE／ZIPは検証済みbundled CLI、開発／Unixは従来のOS PATH CLI。質問の正本は入力workbookとrun snapshot、比較用参照回答の正本は保存済みrun集合。
- 方向: 双方向
- 方式: 固定SDKのrestricted sessionによる質問送信とclosed schema結果収集。Start／Ping・認証確認・login・model列挙・参照回答生成・通常評価・固有評価に§3.2で解決した同じCLIを一貫使用する。Windows配布は既存bundled resolverの検証済み絶対pathのみ、PATH fallback禁止。開発／Unixは従来のPATH規則を維持しbundledへfallbackしない。
- 頻度と量: 有効設問ごとに{PARAM-003}の固定slotを新規runで生成。旧runは旧予定数を保持。各slotは§7.6の既存有限attempt上限、request容量・総attempt容量のpreflightに従う。AI実験は今回行わない。
- データ分類: 質問文字列・比較用参照回答・条件metadata。既存の出力と同等以上に機密として扱う（要求定義書§14）。
- 個人情報: あり
- 認証認可: OS利用者に紐づく既存CLI loginとrestricted sessionの範囲を維持。アプリはcredentialを読み取り・解析・収集・保存せず、新しい権限・独自認証・外部検索を追加しない。
- 秘密の保管場所: §3.2で解決したCopilot CLIが利用する既存credential store。アプリは値を読み取り・収集・保存・log出力しない。
- 可用性・SLA: なし（追加保証なし。既存timeout・失敗statusで扱う）。
- レート制限: 既存RATE_LIMITEDの有限retry・backoff・並列度縮退を再利用。providerの未観測上限を推定しない。
- エラー・再試行・冪等性: §7.6の既存timeout・有限retry・cancel・quota非保存を維持。slot単位atomic保存と保存済みslotの再利用。重複参照や一部失敗を隠さない（FR-115）。
- 契約・バージョン: SDK `1.0.11`は要求定義書冒頭の固定版。Windows配布はCLI `1.0.79`と既存manifest・RID・SDK版・CLI版・path・SHA-256 integrity検証を必須とする（SRC-012、FR-121）。開発／UnixのCLI版は固定せず観測version・hashは診断・再開互換情報のみ。両経路ともhashは発行者trust・認証成功の証明ではない。SDK非互換は明示runtime error。指定model・effort・tierと比較方式／template版を保存。旧単独参照・欠損metadataの非破壊読込を維持。
- 変更通知: SDK更新時は既存§20の一次資料確認と互換回帰。Windows配布は固定CLIとmanifestを既存供給経路で同梱し、更新・修正後はexact bytesで再検証する。開発／UnixのCLI更新はOS利用者管理、通常開発App buildの`CopilotSkipCliDownload=true`を維持する。
- 監視: 既存のmetadata／技術status／件数logを再利用。本文・学生Prompt・credentialを新規logへ追加しない。
- データ保持: 既存References sheet・checkpointへ参照集合と条件を保存。本文をsetting.txtへ追加保存せず、自動削除・保持期間変更なし。新診断の一致箇所は位置情報だけ。
- 障害時の代替: Windows配布のCLI／manifest欠損・不一致はsafe runtime errorで拒否し、PATH fallback・外部CLI導入案内をしない。開発／UnixのCLI不在はOS install／PATH設定を案内しbundledへfallbackしない。SDK非互換・起動失敗を未認証扱いせず、offline GUIを維持する。不完全集合の暫定表示、生成不能時の未取得／失敗表示。減点係数ゼロなら診断だけの失敗で採点を妨げず、正ならblank伝播（FR-114）。別modelへ無断fallbackしない。
- 責任分界: providerはAI応答とCLI認証、アプリは有限送信・保存・比較・状態表示、利用者は質問に含む機密情報と最終評点／利用判断。履歴入手・不正識別の責任をAIへ委譲しない。
- 法務・ライセンス: 既存Copilot利用前提（§3.2）と§14を維持。新しい契約・外部公開・提供先・法的判定を追加しない。実AI使用・実学生の識別性能は今回未確認。
- 状態: 運用中
- 関連する要求: FR-115、FR-120、FR-121

run 202611060630の通常評価差分はFR-120／AC-007に限定する。既存の通常評価送信経路でevaluator IDの生成・転記をモデルに求めず、送信時の要求へ束縛する。互換受信の明示的不一致は厳密拒否する。参照回答生成・固有評価のID契約、送信先・認証・送信データ範囲・保持・再試行上限は変更しない。16観点の差分走査は要求定義書§22に記録する。

16観点の走査: 1〜3は既存のCopilot SDK／§3.2の配布・開発別CLI、方向、restricted session方式を再利用。4は承認された固定件数、5〜6は依頼承認の質問のみ送信と既存保護境界（包括承認で権限・個人情報の変更を許可しない）、7〜9は既存失敗制御・slot保存とCLI不在／SDK非互換の明示、10は旧形式互換、11〜12はSDK版確認・CLI identityの診断記録、13は既存保存先での非破壊保持、14は係数条件付き採点保護、15〜16は既存責任分界・契約を維持。run 202611071752ではWindows配布のCLI選択・integrityとexact bytes再検証だけを変更し、要求定義書§22に16観点別の差分を記録した。開発buildのCLI download抑止は維持する。参照生成の比較入力に学生本文を追加しない。個人情報「あり」は質問に含まれ得るためであり、新規に収集する意味ではない。

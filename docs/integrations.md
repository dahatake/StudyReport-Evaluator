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
| INT-001 | 既存Copilot参照回答生成 | GitHub Copilot SDK／OS利用者がPATHに導入したCopilot CLI | 双方向 | 運用中 | FR-115、FR-AL-01〜08 |

## 項目

#### INT-001 既存Copilot参照回答生成
- 連携先: GitHub Copilot SDK／OS利用者がPATHに導入したCopilot CLI。質問の正本は入力workbookとrun snapshot、比較用参照回答の正本は保存済みrun集合。
- 方向: 双方向
- 方式: 固定SDKのrestricted sessionによる質問送信とclosed schema結果収集。認証確認・login・model列挙・参照回答生成・通常評価・固有評価に、PATHから解決した同じOS-installed CLIを一貫して使用する。Windowsは絶対PATH entry上の`copilot.exe`（空・相対PATH entryは無視）、Unix source foundationはPATH上の実行可能な`copilot`。bundled CLI／manifestへのfallbackはしない。
- 頻度と量: 有効設問ごとに{PARAM-003}の固定slotを新規runで生成。旧runは旧予定数を保持。各slotは§7.6の既存有限attempt上限、request容量・総attempt容量のpreflightに従う。AI実験は今回行わない。
- データ分類: 質問文字列・比較用参照回答・条件metadata。既存の出力と同等以上に機密として扱う（要求定義書§14）。
- 個人情報: あり
- 認証認可: OS利用者に紐づく既存CLI loginとrestricted sessionの範囲を維持。アプリはcredentialを読み取り・解析・収集・保存せず、新しい権限・独自認証・外部検索を追加しない。
- 秘密の保管場所: OS-installed Copilot CLIが利用する既存credential store。アプリは値を読み取り・収集・保存・log出力しない。
- 可用性・SLA: なし（追加保証なし。既存timeout・失敗statusで扱う）。
- レート制限: 既存RATE_LIMITEDの有限retry・backoff・並列度縮退を再利用。providerの未観測上限を推定しない。
- エラー・再試行・冪等性: §7.6の既存timeout・有限retry・cancel・quota非保存を維持。slot単位atomic保存と保存済みslotの再利用。重複参照や一部失敗を隠さない（FR-115）。
- 契約・バージョン: SDK `1.0.11`は要求定義書冒頭の固定版。CLI版は固定せず、実行時に観測したversionとfile SHA-256は診断・再開互換情報に限る（信頼attestationではない）。SDK非互換は明示runtime error。指定model・effort・tierと比較方式／template版を保存。旧単独参照・欠損metadataの非破壊読込を維持。
- 変更通知: SDK更新時は既存§20の一次資料確認と互換回帰。CLIはOS利用者が管理し、更新後のSDK互換性を実行時に明示する。通常App buildは`CopilotSkipCliDownload=true`でCLIをdownloadしない。歴史的publishはCLI fileを明示供給できるがruntimeでは使用しない。
- 監視: 既存のmetadata／技術status／件数logを再利用。本文・学生Prompt・credentialを新規logへ追加しない。
- データ保持: 既存References sheet・checkpointへ参照集合と条件を保存。本文をsetting.txtへ追加保存せず、自動削除・保持期間変更なし。新診断の一致箇所は位置情報だけ。
- 障害時の代替: CLI不在時はOS install／PATH設定を案内し、SDK非互換は明示runtime errorとする。bundled CLIへfallbackしない。不完全集合の暫定表示、生成不能時の未取得／失敗表示。減点係数ゼロなら診断だけの失敗で採点を妨げず、正ならblank伝播（FR-114）。別modelへ無断fallbackしない。
- 責任分界: providerはAI応答とCLI認証、アプリは有限送信・保存・比較・状態表示、利用者は質問に含む機密情報と最終評点／利用判断。履歴入手・不正識別の責任をAIへ委譲しない。
- 法務・ライセンス: 既存Copilot利用前提（§3.2）と§14を維持。新しい契約・外部公開・提供先・法的判定を追加しない。実AI使用・実学生の識別性能は今回未確認。
- 状態: 運用中
- 関連する要求: FR-115

16観点の走査: 1〜3は既存のCopilot SDK／PATH CLI、方向、restricted session方式を再利用（固定SDKとOS利用者管理CLI）。4は承認された固定件数、5〜6は依頼承認の質問のみ送信と既存保護境界（包括承認で権限・個人情報の変更を許可しない）、7〜9は既存失敗制御・slot保存とCLI不在／SDK非互換の明示、10は旧形式互換、11〜12はSDK版確認・CLI identityの診断記録、13は既存保存先での非破壊保持、14は係数条件付き採点保護、15〜16は既存責任分界・契約を維持。通常App buildのCLI downloadを無効化し、歴史的publish artifactの同梱CLI／manifestはruntimeで使用しない。参照生成の比較入力に学生本文を追加しない。個人情報「あり」は質問に含まれ得るためであり、新規に収集する意味ではない。

# StudyReport Evaluator 要求定義書

| 項目 | 内容 |
|---|---|
| 文書版 | 1.2 |
| 基準日 | 2026-08-31 |
| 状態 | 要求ベースライン（アプリ未実装。レポート定義は起動後に利用者が作成し、実データ運用には第14節・第17節のゲート充足が必要） |
| 対象 | Microsoft Forms / Google Forms 由来の Excel 学習レポートを、GitHub Copilot SDK と教員定義 Prompt で定量評価するローカルデスクトップアプリケーション |
| 想定読者 | 教員、教育機関の情報管理責任者、開発者、テスト担当者 |

> **重要**: 本書は製品・技術要求であり、法的助言ではない。実運用前に、所属機関の個人情報保護、情報セキュリティ、教育評価、生成 AI 利用に関する規程と、適用法・契約を責任者が確認すること。

## 1. 要約

本アプリケーションは、Forms からエクスポートされた `.xlsx` をローカルで読み取り、入力ブックには一切書き込まず、新しい出力ブック内に教員が選択した元シート（既定候補は `Original`）から変換済みコピー `Eval` を作成する。利用者はアプリケーション起動後、レポート種類ごとの `ReportDefinition` として Prompt、評価項目、点数アンカー、理由・根拠の出力規則、不足flag、対象言語、構造上限を作成・変更する。これを各設問・各回答行へ独立して実行し、GitHub Copilot SDK の項目別出力を**採点候補**として保存する。重み付け、設問小計、総合点、採否、類似度区分などの決定論的処理は Excel 数式にし、教員が後から自由に変更できるようにする。[S02][S04][S20]

生成 AI の出力は非決定的で、誤り・偏りを含み得る。教育上の判断は人の将来機会へ影響するため、AI 出力を最終成績として自動確定してはならない。教員による確認、上書き、採否、異議申立てへの説明可能性を必須とする。[S05][S09][S13]

また、「学習者が設問文を生成 AI にそのまま入力した可能性」は、AI 文章検出器ではなく、設問文と学習者提出 Prompt の**ローカルかつ決定論的な文字列類似度**として表示する。これは確認の手掛かりであり、不正や AI 利用の証明、減点根拠にはしない。[S10][S11][S12][S15]

## 2. 要求表現と優先度

- **必須（MUST）**: リリースに不可欠。未達なら本番利用不可。
- **推奨（SHOULD）**: 原則実装する。見送る場合は理由と代替統制を記録する。
- **任意（MAY）**: 利便性または将来拡張。
- 優先度は `P0`（必須）、`P1`（初回正式版で推奨）、`P2`（将来）で示す。
- 「採点候補」は AI が返した未承認値、「採用点」は教員が確認または上書きした値、「最終点」は Excel 数式で集計した値を指す。
- `ReportDefinition` は、利用者が起動後のレポート作成画面で定義するレポート種類別設定である。製品releaseや実装前外部入力ではなく、実行開始時にimmutable snapshotとSHA-256を作る。
- 「技術的hard ceiling」は安全性・媒体・SDK上の絶対上限、「レポート設定上限」は利用者が`ReportDefinition`内で変更する上限を指す。後者は前者を超えられないが、製品が教育用途ごとの値を決めない。
- アプリケーションを起動したローカル人物の本人性、所属、役割、承認権限の認証は対象外とする。GitHub OAuth accountの確認はCopilot service利用権限のためであり、ローカル起動者の本人確認ではない。
- P1 を見送る場合は、要件 ID、責任者、理由、影響、代替統制、再検討期限をリリース記録へ残し、リリース責任者が承認しなければならない。
- 本書の数値上限・時間目標は**製品の安全側の設計値**であり、外部標準の保証値ではない。変更にはベンチマーク、費用・セキュリティ評価、承認を必要とする。

## 3. 調査・サンプル解析から得た設計判断

| 根拠 | 確認できた事実 | 本書での判断 |
|---|---|---|
| サンプルブック [S01] | `Original` は 531 行（見出しを除き 530 回答）・12 列。主回答は F/I、学習者 Prompt は G/J、補助回答は H/K、L は対象外。`Old` は Copilot 関数と総合式の参考、`Final` は AI 値を固定し総合値のみ数式。 | 列記号を固定せず、設問単位の列マッピングを教員が確認する。サンプルの式・点数体系は既定値として流用しない。 |
| Forms 公式資料 [S02][S03] | Microsoft Forms は質問を列、回答を行として Excel へ出力する。Google Forms は回答を Google Sheets に表形式で保存できる。Microsoft Forms の「コピーをダウンロード」はフォームとの接続がないオフラインブック。 | 静的なローカル `.xlsx` を入力契約とし、フォームやクラウドシートへ直接接続しない。 |
| 教育測定標準 [S09] | 自動採点には理論・方法上の根拠、運用前の人間採点との一致、部分集団ごとの精度・妥当性・系統的偏りの検証が必要。 | 本番前に教員の二重採点済み基準データで校正し、Prompt・モデル変更時に再検証する。 |
| LLM 評価研究 [S06][S07][S08] | LLM 評価は人間判断と一定の整合を示す研究がある一方、位置・冗長性・自己強化・LLM 生成文への偏りが報告され、対象タスクも本システムとは異なる。 | 研究結果を本システムの精度として転用しない。複数回評価、順序固定、明示的ルーブリック、ローカル妥当性検証を行う。 |
| AI 文章検出研究 [S10][S11][S12] | 既存検出器の精度・頑健性・公平性には重大な限界があり、言い換えで検出率が低下し、非母語話者への偏りも報告されている。 | 「AI が書いた確率」は実装しない。設問文と提出 Prompt の表層的なコピー類似度のみを示す。 |
| Copilot 公式資料 [S04][S05] | SDK は Copilot CLI と JSON-RPC で通信し、.NET SDK は CLI を同梱する。Copilot は非決定的で、人による確認が必要。要求調査の API 基準版を tag `v1.0.11` とした。 | `.NET 10` と公式 .NET SDK を採用候補とし、実装開始時に採用版と同梱 CLI を再検証・固定する。`CopilotClientMode.Empty` と厳密な許可リストで、結果提出用カスタムツール 1 件だけを公開する。 |
| データと契約 [S05][S13][S14] | Business/Enterprise の顧客データはモデル学習に使われない。個人プランは設定により相互作用データが学習改善へ利用され得る。現行企業規約は入力・出力を学習に使わないとするが、機能により保持があり得る。教育等に重大な影響を与える判断には人の監督が必要。 | 実在学生データの本番運用は、組織管理の Copilot Business/Enterprise と機関承認を必須にする。個人プランは合成データによる試用に限定する。 |
| LLM セキュリティ [S16][S17] | 外部ファイルは間接 Prompt Injection、機微情報漏えい、過剰権限、不適切な出力処理、無制限消費のリスクを持つ。 | 回答を信頼しないデータとして境界表示し、ツールを無効化し、出力スキーマをコードで検証し、回数・長さ・並列度・再試行を制限する。 |
| 国際的 AI リスク管理 [S18][S19] | NIST は AI リスク管理を設計・開発・利用・評価へ組み込む枠組みを提供。EU AI Act の現行統合版は学習成果評価を Annex III の高リスク用途に列挙し、リスク管理、記録、透明性、人の監督、精度・堅牢性等を要求する。 | 地域を問わず、追跡性、人の監督、説明、停止、監視、苦情対応を設計原則にする。EU で提供・利用する場合は別途適合性評価を実施する。 |
| Excel / Open XML [S20][S21][S22][S32] | Open XML は ZIP/XML ベースの標準。Excel は 1,048,576 行、16,384 列、1 セル 32,767 文字、1 数式 8,192 文字等の物理上限を持つ。 | 非暗号化 `.xlsx` を Open XML の copy-on-write 方式で処理し、既存パーツを再生成しない。入力と生成物を物理上限より小さい製品上限で事前検査する。 |
| クロスプラットフォーム [S23][S24] | Avalonia は Windows/macOS/Linux を対象にできる。.NET 10 は LTS で 2028 年 11 月までサポート。自己完結配布は OS/CPU ごとに必要。 | `.NET 10 LTS + Avalonia` を基準構成とし、OS/CPU 別の署名済み自己完結パッケージを配布する。 |
| 保護済み Office [S25] | MIP は Office Open XML のネイティブ保護に対応し、保護ファイルは認証と権限確認を必要とする。本サンプルは Compound File 内に `EncryptedPackage` と DRM DataSpace を持つが、メタデータ解析だけで保護方式を MIP と断定していない。 | P0 は非暗号化 `.xlsx` に限定する。暗号化/権利保護ファイルは方式を推測せず安全に停止し、機関承認済み手順で処理可能なコピーを作るよう案内する。MIP 対応は追加認証・通信を許可する要件変更後の P2 とする。 |
| OAuth [S04][S31] | SDK `v1.0.11` は OAuth ユーザートークンを受け取れるが、OAuth の保存・更新・失効はアプリの責任である。GitHub Device Flow は client secret を端末へ置かずに実装できる。安定版 `GetAuthStatusAsync` は認証状態・方式・ホスト・login を返すが、契約プラン種別は返さない。 | アプリ所有の OAuth Device Flow と OS 資格情報ストアを採用し、SDK へ明示トークンを渡す。Business/Enterprise 契約は SDK で推測せず、組織管理者の承認記録で統制する。 |

## 4. 目的、成功条件、対象外

### 4.1 目的

1. **OBJ-001 / P0**: 元データを変更せず、繰り返し可能な評価作業を提供する。
2. **OBJ-002 / P0**: 複数設問・複数回答行を、教員作成 Prompt と明示的な評価項目で定量化する。
3. **OBJ-003 / P0**: AI の候補値と Excel の決定論的計算を分離し、重み・閾値・式を教員が変更可能にする。
4. **OBJ-004 / P0**: AI 利用の透明性、追跡性、公平性、人による最終判断を確保する。
5. **OBJ-005 / P0**: Windows、macOS、Linux で、クラウドバックエンドを設けずに動作させる。

### 4.2 リリース成功条件

- **AC-001 / P0**: 入力ファイルの処理前後 SHA-256 が一致し、入力の更新日時・サイズ・内容が変化しない。
- **AC-002 / P0**: 合成データによる全受入試験が通り、アプリ所有シート/数式に `#REF!`、`#DIV/0!`、`#VALUE!`、`#NAME?`、循環参照が新規発生しない。入力由来の既存数式・既存エラーは基準値として別報告する。
- **AC-003 / P0**: ネットワーク記録で、セットアップ・明示更新を除き、承認済み GitHub OAuth/Copilot 推論・制御プレーン以外へ通信しない。テレメトリ通信は 0 件とする。
- **AC-004 / P0**: 教員の基準採点との一致、再実行安定性、部分集団別誤差が、運用責任者が測定前に定義した基準を満たす。
- **AC-005 / P0**: AI 候補だけで最終点を確定できず、教員の確認または上書きを要する。
- **AC-006 / P0**: 実在学生データを使う前に、所属機関の個人情報・教育評価・AI 利用承認を記録する。
- **AC-007 / P0**: 出力パッケージの既存パーツについて、変更許可リスト外の URI、content type、relationship、内容ハッシュが入力と一致する。

### 4.3 対象外

- Microsoft Forms / Google Forms API への直接接続、回答の書き戻し、クラウド同期。
- Excel `COPILOT()` 関数、VBA、Office Scripts、Power Automate。
- 生成 AI が書いた文章かどうかの確率判定、不正行為の自動認定。
- AI 候補のみを根拠とする合否・単位・成績の自動決定。
- 手書き、画像、PDF、`.xls`、CSV、Google Sheets URL の直接入力。
- Web 検索、外部 RAG、MCP、シェル、ファイル編集等を Copilot に実行させること。
- 本アプリから元ブック、Forms、LMS、学生への通知を変更すること。

## 5. 利用者と責任

| 利用者 | 主な責任 |
|---|---|
| 教員（主利用者） | 入力選択、列マッピング、レポート種類別`ReportDefinition`、Prompt/ルーブリック、理由・根拠規則、不足flag、対象言語、構造上限、重み・閾値の設定、AI 候補の確認・上書き、最終決定 |
| 教育評価責任者 | 測定対象、妥当性・信頼性基準、公平性分析、採点運用の承認 |
| 情報管理・個人情報保護責任者 | 学生通知、法的根拠、利用目的、保存期間、Copilot プラン/モデル、越境処理、アクセス権の承認 |
| システム管理者 | 署名済み配布物、許可ドメイン、プロキシ/証明書、更新、インシデント対応 |
| 開発者 | 実装、テスト、依存関係、脆弱性、再現性、文書化 |
| 学習者（影響を受ける者） | AI 補助利用の通知、説明・訂正・再確認・異議申立てを受ける対象 |

## 6. 前提と制約

### 6.1 オフラインの定義

- アプリ本体、Excel 読み書き、類似度計算、数式生成、ログ、レビューは端末内だけで実行する。
- 実行時の外部通信は、利用者が明示的に開始した GitHub OAuth、組織確認、Copilot の推論とその動作に必須の GitHub 制御プレーンだけを例外とする。許可先はリリース時点の公式 allowlist から承認済みマニフェストへ固定する。[S28]
- テレメトリ、クラッシュ本文送信、更新確認、Web 検索、外部フォント/CDN は既定で無効とする。
- 初回セットアップと明示的な更新はインターネット接続を必要としてよい。これは実行時オフライン要件とは分けて表示する。
- Copilot 通信に失敗しても、既存結果の確認、設定編集、類似度計算、Excel 再出力は利用可能でなければならない。

### 6.2 サンプルファイルに関する確認事項

対象サンプル `sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx` は、拡張子は `.xlsx` だが、先頭が ZIP の `PK` ではなく Compound File Binary の `D0 CF 11 E0 A1 B1 1A E1` で、`EncryptedPackage` と DRM DataSpace ストリームを持つ暗号化/権利保護 Office ファイルだった。メタデータだけでは MIP、パスワード暗号化、その他の方式を一意に確定していない。解析は権限のある Microsoft Excel で読み取り専用に開き、学生回答本文を端末へ出力せず、構造・見出し・数式・件数だけを確認した。[S01]

- SHA-256: `446386E20BB4096561CB4AFD6D74B8EAA9D50EAE53C97F984BA7F70EBEAD0DE5`
- シート: `Old`、`Original`、`Final`
- `Original`: 531 行 × 12 列、数式なし
- `Old`: 531 行 × 32 列、数式 2,649 セル
- `Final`: 531 行 × 30 列、数式 530 セル

暗号化/権利保護ファイルの読み取りには追加の認証、権限、場合により通信が必要であり、「Copilot 関連だけを通信例外とする」「OS だけの端末」「全 OS 同一挙動」と同時には保証できない。[S25] したがって P0 では次を採用する。

1. `ENCRYPTED_COMPOUND_FILE`、確認できた場合の `MIP_PROTECTED`、`OOXML_WORKBOOK_PROTECTION`、`OOXML_SHEET_PROTECTION`、`CORRUPT_OR_MISMATCH` を区別し、確証がなければ保護方式を推測しない。
2. 暗号化/権利保護ファイルの読み取りや解除を試行せず、元ファイルに触れないまま停止する。OOXML の workbook/sheet protection は暗号化とは別に報告する。
3. 利用者へ「権限のある Office/Forms と機関承認済み手順を使用し、本アプリで処理可能なコピーを承認済み保存先へ作成し、元の分類・持出し・保存規則に従う」よう案内する。Forms の「コピーをダウンロード」が保護解除を保証するとは説明しない。[S02]
4. MIP ネイティブ対応は、GitHub Copilot 以外の認証・通信を許可し、Entra/MIP の運用責任を定義した将来要求 `EXT-001` とする。

## 7. 業務フロー

1. **BUS-001 / P0 — 起動・安全説明**: AI は採点補助であり最終決定者ではないこと、送信されるデータ、承認済み契約/モデルを表示する。
2. **BUS-002 / P0 — GitHub 認証**: アプリ所有の OAuth Device Flow を開始し、端末の安全な資格情報ストアを使う。トークンを画面・ログ・設定ファイルへ出さない。[S04][S31]
3. **BUS-003 / P0 — 入力選択**: ローカル `.xlsx` を選択し、暗号化/保護、破損、拡張子、資源上限、危険な外部関係、ハッシュを検査する。
4. **BUS-004 / P0 — シート・列マッピング**: `Original` を既定候補とし、識別列、主回答、学習者 Prompt、補助回答、無視列を設問ごとに設定する。
5. **BUS-005 / P0 — レポート定義・Prompt/評価項目設定**: 利用者がレポート種類を作成または選択し、Prompt、評価項目、各範囲、理由・根拠規則、不足flag、対象言語、構造上限、Excel 重み、類似度閾値を設定する。アプリは設定者の本人性・役割を認証せず、実行前に構造・資源上限と送信内容を検証する。
6. **BUS-006 / P0 — 送信内容プレビュー**: 代表 1 件について、Copilot に送る項目名と伏字後内容を確認する。氏名・メール等の識別列は常に除外する。
7. **BUS-007 / P0 — 試行**: 合成データまたは機関承認済み少数データで、項目別構造化出力、点数範囲、根拠、モデル、概算消費を確認する。
8. **BUS-008 / P0 — 実行計画確認**: 対象行数 × 評価器数の最大リクエスト数、選択モデル、再試行上限、出力先を表示する。
9. **BUS-009 / P0 — 評価実行**: 1 セル/行/評価器を独立処理し、進捗・成功・スキップ・要確認・失敗を表示する。停止、取消、再開ができる。
10. **BUS-010 / P0 — レビュー**: 空欄、失敗、範囲外、類似度警告、再実行差、内容フィルター等を優先表示し、教員が採用/上書き/保留/却下する。
11. **BUS-011 / P0 — Excel 出力**: 一時ファイルへ原子的に保存・検証し、完成後に別名 `.xlsx` として確定する。
12. **BUS-012 / P0 — 最終確認**: 入力ハッシュ不変、出力場所、処理件数、要確認件数、構造/式/E2E 検証範囲を表示する。

## 8. 機能要求

### 8.1 入力保全とブック処理

| ID | 優先度 | 要求 | 受入条件 |
|---|---:|---|---|
| FR-001 | P0 | 入力を常に読み取り専用で開き、同一パスへ保存しない。処理中は同一性を確認済みのファイルハンドルまたは不変ローカルスナップショットだけを参照する。 | OS のファイル監視、ファイル ID、SHA-256、サイズ、更新時刻が読取前後で一致し、パス差替え（TOCTOU）が起きても入力を取り違えない。 |
| FR-002 | P0 | 処理開始前と終了後に入力 SHA-256 を計算する。 | 不一致時は出力を完成扱いにせず、重大エラーを表示。 |
| FR-003 | P0 | 各実行は承認済み入力から新しい出力 workbook を作り、既定名を `{元名}_evaluated_{yyyyMMdd-HHmmss}.xlsx` とする。入力 package に本製品の `Evaluation_Run` schema marker または署名済み run manifest relationship があれば「評価済み出力」と判定する。 | 実データモードでは評価済み出力の再入力を禁止する。合成データモードだけは全 `Eval*`/監査/設定シートを除外する新規コピーとして明示 override 可能。ファイル名だけでは判定しない。既存出力を上書きせず、入力と同一パス/同一ファイル ID は選択不可。 |
| FR-004 | P0 | 標準 Office Open XML `.xlsx` のみを受け入れ、ZIP/package を streaming 走査する。 | `.xls`、CSV、PDF、マクロ有効形式、破損 ZIP、拡張子偽装、ZIP traversal、重複 URI、不正 relationship を明確に拒否。 |
| FR-005 | P0 | P0 は `ENCRYPTED_COMPOUND_FILE`、OOXML workbook/sheet protection、破損、拡張子偽装を区別する。MIP-aware API を持たない P0 parser は MIP とパスワード暗号化を確定分類せず、暗号化 compound file を一律拒否する。 | 判別不能は `UNKNOWN_PROTECTION` として fail-closed。方式を推測せず、認証回避・総当たり・解除を行わない。MIP 確定分類/復号は `EXT-001` の範囲。 |
| FR-006 | P0 | 入力上限を既定 100 MiB、展開後合計 1 GiB、ZIP entry 10,000、entry 単体 256 MiB、圧縮比 100:1、20,000 使用行、300 使用列、40 シート、共有文字列 2,000,000、一意セル書式 20,000、1 セル 32,767 文字とする。[S22][S32] | メモリ/時間/再帰深さ上限を設け、超過を処理前または検出時に安全停止する。管理者は上限を引き下げだけ可能。 |
| FR-007 | P0 | 外部リンク/外部参照式、DDE、OLE/ActiveX、マクロ、データ接続/クエリ、埋込みオブジェクト、VeryHidden/非表示シート、既存数式、定義名、カスタム XML、デジタル署名を走査する。 | 外部実行/取得を伴う関係、不正 package、外部参照式、data-table式は拒否する。ローカル式は警告・明示確認後に保存済みcached valueだけを静的値として投影できるが、式を継承せず、検査中に更新または実行しない。 |
| FR-008 | P0 | 入力 package を byte-for-byte コピーした作業ファイルを基礎に、Open XML SDK の low-level API でアプリ所有パーツだけを追加/置換する。 | 変更対象はcontent types、workbook、workbook relationships、stylesの必要最小要素と、新規`Eval`、`Evaluation_Config`、`Evaluation_Run` worksheet partsに閉じる。namespace familyを混在させず、ClosedXML 等による workbook 全体の load/save 再生成を production writer に使用しない。[S20][S21] |
| FR-009 | P0 | 入力に存在した全 package part と全シートを出力でも保持する。変更許可リスト外の part bytes、URI、content type、relationship は同一にする。 | 許可リスト外パーツの SHA-256 と関係グラフが完全一致。デジタル署名等、変更で無効化される要素は入力段階で拒否。 |
| FR-010 | P0 | 教員が選択した元シートから、値と必要な静的表示だけを投影した新規 `Eval` シートを作る。`Original` は既定候補にすぎず、非選択時は複製元にしない。 | 選択した sheet ID/name を実行計画、チェックポイント、監査へ記録し、元シートは不変。式・external relationship・rich textを投影先へ継承しない。 |
| FR-011 | P0 | `Eval` 名が Unicode case-insensitive に衝突する場合は `Eval (2)`…の一意名を提案し、Excel の禁止文字と 31 文字制約を満たす。 | 既存シートを置換/削除せず、生成実名を確認画面と監査へ記録。 |
| FR-012 | P0 | データ行が全て空の列、無視指定列、どの設問にも使わない列を新規 `Eval` から除外できる。 | 保持対象の識別・主回答・補助・学習者 Prompt 列は自動削除されず、元シートは列削除されない。 |
| FR-013 | P0 | 行1を既定見出し、行2以降をデータとするが、見出し行と最終行を設定可能にする。 | 空行、途中空行、末尾書式だけの行で件数を誤らない。 |
| FR-014 | P0 | 読み込み後、シート名、使用範囲、見出し、非空件数、数式/危険要素有無を表示する。 | 回答本文は明示操作なしにログや診断へ表示されない。 |
| FR-015 | P1 | 過去の列マッピングをローカルプロファイルとして保存・再利用する。 | ファイルハッシュではなく見出しと質問 ID で照合し、差分を再確認させる。 |
| FR-016 | P0 | 一時ファイルへ書込み、close、fsync 相当、再 open、Open XML 構造/関係/上限/許可差分検証、同一ボリュームでの原子的 rename の順に確定する。 | 強制終了、ディスク不足、rename 失敗で完成名を残さず、入力は不変。 |
| FR-017 | P0 | workbook内部には入力SHA-256、実行ID、生成日時、アプリ版、選択元/生成シート名、`ReportDefinition` snapshot hash、logical manifest digestを記録する。完成workbook全体のSHA-256、最終filename、実行ID、manifest digestは署名付きdetached completion recordへ記録する。 | workbook自身のSHA-256をworkbook内部へ格納する自己参照を作らない。workbookとcompletion recordをrun ID/manifest digestで相互拘束し、片方の欠落・不一致・早期final名では完成扱いにしない。本文・PII・tokenを監査へ含めない。 |

### 8.2 設問・列マッピング

| ID | 優先度 | 要求 | 受入条件 |
|---|---:|---|---|
| FR-020 | P0 | 設問数を固定せず、1 件以上の設問を追加・並べ替え・無効化できる。 | 2 設問以外の合成ブックでも処理可能。 |
| FR-021 | P0 | 各設問に一意な `question_id`、表示名、設問本文、主回答列を設定する。 | 同一列を複数主回答へ設定した場合は警告し、設定者の本人・role承認ではなく、その場の明示確認を要求する。 |
| FR-022 | P0 | 設問本文は既定で主回答列の見出しを使い、別テキストへ上書き可能にする。 | 実行マニフェストに最終設問本文の SHA-256 を記録。 |
| FR-023 | P0 | 各設問に 0 件以上の補助回答列を設定し、`重要`、`参考`、`送信しない` の役割を付けられる。 | サンプルでは H/K を `重要`、その他を教員確認後に設定可能。 |
| FR-024 | P0 | 各設問に 0 または 1 件の学習者 Prompt 列を設定する。 | サンプルでは G/J を候補表示。空欄は `対象なし` であり 0 点にしない。 |
| FR-025 | P0 | 氏名、メール、回答者 ID、時刻等を識別/管理列として設定する。 | 識別/管理列は出力へ保持できるが Copilot 送信対象へ指定できない。 |
| FR-026 | P0 | L1 のような対象外列を明示的に無視できる。 | 無視列は Prompt、類似度、数式の参照先にならない。 |
| FR-027 | P0 | 見出し文字列だけによる自動確定は禁止し、自動候補を利用者が確認する。 | 初回はマッピングの明示確認なしに実行画面へ進めない。この確認を本人・role承認として記録しない。 |
| FR-028 | P1 | Microsoft Forms の先頭管理列パターンを候補提示する。 | 列名が異なる Google Forms/手作業ブックでも手動設定可能。 |

### 8.3 Prompt・ルーブリック

| ID | 優先度 | 要求 | 受入条件 |
|---|---:|---|---|
| FR-030 | P0 | Prompt は利用者がアプリ起動後にレポート種類別`ReportDefinition`として作成・編集し、設問別または共通テンプレートを設定できる。 | アプリやrelease artifactが教育内容、理由文、根拠文、正解を事前生成・固定せず、実行中の学生回答を設定profileへ保存しない。 |
| FR-031 | P0 | 変数は `{{question}}`、`{{answer}}`、`{{student_prompt}}`、`{{supplementals}}`、`{{criteria}}` 等の明示プレースホルダーに限定する。 | 未定義・未使用の必須変数、再帰展開、テンプレート構文エラーを実行前に拒否。 |
| FR-032 | P0 | 評価器ごとに一意ID、目的、対象セル役割、1件以上の評価項目、各項目の最小/最大点を`ReportDefinition`へ定義する。利用者はレポート作成中に評価器ごとの最大rubric item数、evaluator ID最大Unicode scalar数、item ID最大Unicode scalar数を変更できる。 | 実数が設定上限を超える、最小>最大、重複ID、有限数でない値、または最悪tool argumentが技術的hard ceilingを超える定義を実行前に拒否する。総合候補点だけを返す評価器は受け付けない。 |
| FR-033 | P0 | 評価項目ごとに観察可能な記述と点数アンカーを持たせる。 | 「高度に評価」だけのように点数水準を識別できない Prompt へ警告。 |
| FR-034 | P0 | Prompt のプレビューと 1 件試行を実行前に提供する。 | 展開後 Prompt、送信項目、戻りスキーマを確認可能。個人情報候補は警告。 |
| FR-035 | P0 | `ReportDefinition`全体（Prompt、ルーブリック、設問本文、理由・根拠規則、不足flag、対象言語、構造上限、重み設定）をcanonical化し、版番号とSHA-256を記録する。 | 同じ名称でも内容変更を別版として識別し、実行開始後の変更は既存run/checkpoint/cacheへ適用せず、新しいrun IDを要求する。 |
| FR-036 | P0 | 学習者回答を「信頼しない引用データ」として明確な区切り内へ挿入し、その中の命令に従わないようシステム指示する。学生データをsystem instruction、後続命令、別sessionへ転送しない。唯一の例外として、外部I/Oを持たない`submit_evaluation`の`evidence`だけが同じ主対象回答からの短い逐語抜粋を受けられる。[S16] | Prompt Injectionテスト文がtool追加、秘密開示、採点規則変更を起こさず、evidence以外のtool propertyへ学生本文が入らない。検知だけでなくFR-047/FR-048の能力遮断でも防止。 |
| FR-037 | P0 | 最終Promptに「証拠のない推測をせず、不足は不足として返す」と、`ReportDefinition`で利用者が指定したreason/evidence出力規則・対象言語を含める。 | 空・無関係回答に架空の根拠を生成しない。target languageを自動推測・翻訳せず、evidenceは対象回答の原表記を保持する。 |
| FR-038 | P1 | 合成した良い/中間/不十分回答の few-shot 例を評価器ごとに設定可能にする。 | 実在学生回答を例として保存する場合は別途承認と保存期間を要求。 |
| FR-039 | P0 | 利用者は起動後、レポート種類ごとに`ReportDefinition`を作成・編集・保存できる。設定にはreason/evidence規則、利用者定義のclosed `missing_flags`、report全体で1つの`target_language`（BCP 47 tag）、`max_rubric_items_per_evaluator`、`evaluator_id_max_scalars`、`item_id_max_scalars`、`reason_max_scalars`、`evidence_max_scalars`、`max_missing_flags_per_item`、`reason_source_quote_rejection_threshold_scalars`を含める。最後の値以上の長さで主回答とexact contiguous一致する文字列をreason中に検出した場合は拒否する。 | 設定者の氏名・役割・承認を要求しない。各値はレポート作成中だけ変更でき、run開始時にimmutable snapshot/hashへ固定する。quote拒否閾値は`reason_max_scalars`以下、`max_missing_flags_per_item`は定義flag数以下とし、flag 0種類なら0だけを許す。生成schemaの最大instanceをproduction serializerで実測し、tool arguments 32,767 scalar以下、各string 32,767文字以下、request 65,536 scalar以下かつmodel contextの80%以下、投影列/式がExcel上限内である場合だけ実行可能。切詰め、暗黙default、別report schema再利用を禁止する。 |

### 8.4 GitHub Copilot SDK 実行

| ID | 優先度 | 要求 | 受入条件 |
|---|---:|---|---|
| FR-040 | P0 | 公式 GitHub Copilot SDK for .NET の固定版を使用し、Excel `COPILOT()` 関数を使用しない。API 要求の基準は tag `v1.0.11` とし、実装開始時に採用可能な版を再確認して NuGet package/同梱 CLI の exact version と SHA-256 を lock file・SBOM・実行監査へ固定する。[S04][S05] | 採用版で Empty mode、tool/permission、auth、telemetry、session 削除を contract test する。依存解決は floating range にせず、更新時に全項目を再検証。 |
| FR-041 | P0 | 実在学生データモードは、機関管理者が別途発行した**署名済みローカル運用ポリシーファイル**をアプリへ取り込んだ場合だけ有効にする。ファイルにはschema version、policy ID、発行者ID、機関/GitHub org、Business/Enterprise契約確認、許可model、保持/学習/データ所在条件、費用上限、発行/失効日時、公開鍵ID、署名を含める。`ReportDefinition`の教育内容や起動者個人IDは含めない。 | アプリは信頼済み公開鍵集合で署名、schema、期限、org/model対応を検証する。不正は`POLICY_SIGNATURE_INVALID`、期限切れは`POLICY_EXPIRED`、未知schema/keyは`POLICY_VERSION_MISMATCH`/`POLICY_KEY_UNTRUSTED`として`POLICY_BLOCKED`。current/next key、signed revocation、offline recoveryを支援する。SDK/tokenからplanやローカル起動者を推測しない。 |
| FR-042 | P0 | アプリ所有の GitHub OAuth App で Device Flow を実装する。Device Flow を登録時に有効化し、`/login/device/code` の `client_id` と最小 scope で user/device code を取得し、返却 `interval` 以上で `/login/oauth/access_token` を poll する。[S31] | client secret をデスクトップへ同梱しない。`authorization_pending`、`slow_down`（以後 interval +5 秒）、`expired_token`、`access_denied`、`incorrect_*`、`device_flow_disabled` を区別。 |
| FR-043 | P0 | access/refresh token と期限は Windows Credential Manager、macOS Keychain、Linux Secret Service の OS 資格情報ストアだけへ保存する。`offline_access` 使用時は token rotation を原子的に保存し、refresh 不可時は再認証する。[S31] | token を設定、Excel、ログ、クリップボード履歴、crash dump へ保存しない。logout 時はローカル削除し、GitHub の認可確認/失効リンクも提示。 |
| FR-044 | P0 | token取得/更新時にGitHub `/user`でOAuth accountのlogin/user IDを再確認し、各実データ評価実行の開始直前（長時間実行は30分ごと）に対象org membershipと署名済みポリシー期限を再検証する。これはCopilot service accountの認可確認であり、ローカル起動者の本人性・役割確認ではない。SDKにはexplicit `GitHubToken`と`UseLoggedInUser=false`を設定し、環境変数、Copilot CLI、`gh`の資格情報へfallbackしない。[S04][S31] | OAuth account変更、組織外、membership不明/取消、期限切れpolicy、SSO/IP policy拒否では新規送信を停止し`POLICY_BLOCKED`。進行中sessionを削除する。人間の氏名・roleへのmappingを作らない。 |
| FR-045 | P0 | SDK から利用可能モデル一覧を取得し、教員/管理者が承認済みの具体的 model ID を選択する。 | 本番では `auto` を使用不可。廃止/利用不能モデルは停止し、無断代替しない。実モデル ID と provider policy 版を記録。 |
| FR-046 | P0 | 1 回の評価単位を「1 行 × 1 対象回答セル × 1 評価器」とし、評価単位/再試行ごとに新規 ephemeral session を使う。 | 別学生・別設問・前試行の本文や会話履歴を共有しない。 |
| FR-047 | P0 | clientを`CopilotClientMode.Empty`で起動し、sessionのeffective tool集合をアプリ内`submit_evaluation` 1件へ固定する。当該result toolだけpermission promptをbypassでき、発生したその他のpermission requestは全てRejectする。[S04][S17] | shell、filesystem、Web fetch、検索、GitHub write、MCP、editor等が公開されず、悪意ある回答から外部I/Oが0件。result toolのcallback 0件と、negative-control permissionのReject/実行0件を別々に検証する。 |
| FR-048 | P0 | `submit_evaluation`は外部I/Oを持たず、評価器ID、全rubric itemの項目別点/confidence、reason、evidence、systemの`evidence_status`、利用者が`ReportDefinition`で定義した`missing_flags`だけを受ける。reason/evidenceの規則と長さ、flag集合・同時最大数、item/ID最大数、target languageはrun snapshotから評価器ごとのclosed schemaへ生成する。実際のreason/evidence本文を実装前corpusとして要求しない。通常assistant本文は採点値、理由、再試行指示として解釈・保存しない。 | 1回の検証済み提出後は追加tool call/本文を無視して終了。schemaはexpected evaluator/item IDをexactに閉じ、`missing_flags`は当該reportのenumだけを許す。総合点だけ、項目欠落/重複、未知ID/flag、NaN、範囲外、過長、複数提出を拒否する。 |
| FR-049 | P0 | LLM出力を不信データとして、`ReportDefinition` snapshotから生成したJSON schema、型、範囲、exact ID、利用者設定上限、技術的hard ceiling、有限数、全必須項目、呼出回数、根拠抜粋の入力内存在をコード検証する。[S16][S17] | 不正時は`AI_STATUS=SCHEMA_INVALID`または`TOOL_PROTOCOL_VIOLATION`。切り詰めや0点変換をしない。schema生成前に最大instanceを同じserializerで測定し、上限不成立なら送信前にreport設定エラーとする。 |
| FR-050 | P0 | 構造不正時は同一入力を新規 session の修正用 Prompt で最大 2 回まで再試行し、なお不正なら `SCHEMA_INVALID` とする。 | 再試行前 session を削除し、点数セルは空欄。 |
| FR-051 | P0 | 通信の一時障害だけを指数バックオフ+jitter 付きで最大 3 回再試行し、認証・契約・入力・内容フィルター・permission 違反は自動反復しない。 | リクエスト総上限を超えず、取消後に新規送信しない。 |
| FR-052 | P0 | 技術的hard ceilingは、既定並列度1/最大3、1 run最大20,000 outbound attempts、1文字列property 32,767文字、1 request 65,536 Unicode scalar以下かつ採用model context windowの80%以下、1 tool arguments全体32,767 Unicode scalar以下とする。[S17][S22] `ReportDefinition`の構造・field上限は利用者がレポート作成時にこの範囲内で変更できる。 | hard ceilingはruntime override不可。report設定を変更するたび、最大rubric item数、最大ID、reason/evidence、最大flag数と最長flag IDから最大tool instanceをproduction serializerで構築・測定し、Excel投影列/式上限、request/context、run attemptsを全て送信前に検査する。不成立なら設定を拒否し、黙って切らない。 |
| FR-053 | P0 | 実行前に`ReportDefinition`のreport type/revision/hash/target language、item/ID/reason/evidence/flag上限、生成schemaの最悪size、最大outbound attempts、選択model、対象行、再試行上限、並列度、文字/token budget、送信先を表示する。 | 利用者が実行確認するまで送信しない。これは利用者本人・役割の承認ではない。価格情報を信頼できるAPIから取得できる場合だけ概算を「見積」と表示する。 |
| FR-054 | P0 | SDK の usage event から提供された入力/出力 token、model、cost 情報だけを集計する。[S04] | 値が提供されない場合に推測値を実績として表示しない。 |
| FR-055 | P0 | 評価単位完了ごとに `DeleteSessionAsync` 相当で disk session data を削除し、client dispose を削除の代用にしない。取消/異常終了時は実行所有 session ID を列挙して削除し、次回起動時も残骸清掃する。 | 削除失敗時は完了済み結果を保全して新規送信を即時停止し、`RUN_STATUS=SESSION_CLEANUP_FAILED`、対象 session ID の秘匿表示、再試行操作を提示する。清掃確認まで実データモードをロック。canary 走査で本文断片 0 件。 |
| FR-056 | P0 | 停止、取消、失敗後の再開をサポートする。 | 入力 hash、Prompt/rubric hash、model ID、SDK/CLI/app 版、選択 sheet ID/name、生成 sheet name が一致する完了済み評価だけ再送しない。 |
| FR-057 | P0 | Copilot がオフライン/利用不可でも類似度計算と既存結果の Excel 出力を実行できる。 | `AI_STATUS=NOT_RUN`、`REVIEW_STATUS=PENDING` を出力し、点数は空欄。 |
| FR-058 | P1 | 同一回答を複数回評価する検証モードを持つ。 | 実行回数、各項目候補、中央値/範囲は分離保存し、単一値で変動を隠さない。 |

### 8.5 データ最小化と伏字

| ID | 優先度 | 要求 | 受入条件 |
|---|---:|---|---|
| FR-060 | P0 | Copilot へ送るのは、設問本文、評価基準、対象回答、明示選択された補助回答/学習者 Prompt だけとする。 | ID、氏名、メール、時刻、ファイルパス、他学生データが送信ペイロードにない。 |
| FR-061 | P0 | 回答本文に含まれるメール、電話番号、学生番号等の候補をローカル検出して警告・伏字可能にする。 | 自動検出は完全ではない旨を表示し、送信プレビューを提供。 |
| FR-062 | P0 | アプリログに回答本文、学習者 Prompt、氏名、メール、Copilot の理由/根拠を記録しない。 | ログ検査で合成 PII と秘密文字列が存在しない。 |
| FR-063 | P0 | 一時ファイル、OAuth state、checkpoint、SDK/CLI session data は出力先と同等以上のアクセス制御下に置き、正常/異常終了時に規定どおり消去する。 | 回復用暗号化 checkpoint と利用者が承認した `.partial.xlsx` 以外に本文を残さず、次回起動時に orphan を検出・清掃。 |
| FR-064 | P0 | SDK の `EnableSessionTelemetry=false`、session の `Streaming=false`（不要時）、アプリ独自テレメトリ無効、OpenTelemetry exporter 未構成、remote session 無効を明示する。`captureContent=false` だけを telemetry 無効化とみなさない。 | 構成検査とプロキシ/packet capture で OAuth/Copilot 必須通信以外の telemetry endpoint 接続が 0 件。 |
| FR-065 | P1 | 保存期間と出力の管理窓口を初回設定で記録し、READMEに端末/共有フォルダーのアクセス制御を説明する。アプリは起動者本人や責任者の本人性を認証しない。 | 期限到来時の削除は利用者/機関管理であり、アプリが原本を自動削除しない。入力値は連絡・表示用metadataで、認証済み責任者とは表示しない。 |

### 8.6 設問文コピー類似度

類似度は、設問文 $q$ と提出された学習者 Prompt $p$ の比較であり、回答本文との比較ではない。提出 Prompt がない場合は `対象なし` とする。

正規化はローカルで次の順に固定する。

1. 固定版 app-local ICU を同梱した `.NET 10` の `NormalizationForm.FormKC` で NFKC 正規化する。[S33]
2. CRLF/CR を LF に統一する。
3. Unicode White_Space の 1 個以上の連続を ASCII space 1 個に畳み、前後を除去する。
4. 大文字小文字、句読点、記号は変更しない。元表示文字列も変更しない。
5. 正規化後を UTF-16 code unit ではなく Unicode scalar value (`System.Text.Rune`) 列として列挙する。
6. 3 scalar 以上は連続 3-scalar shingle の**出現回数 multiset**を作る。3 scalar 未満は `TOO_SHORT` とし、疑似 shingle を作らない。

$c_q(g)$ と $c_p(g)$ を shingle $g$ の出現回数として、以下を保存する。[S15]

- 完全包含: 正規化した $p$ が正規化した $q$ を scalar 列の連続部分列として含むか。
- 設問包含率: $C(q,p)=\sum_g\min(c_q(g),c_p(g))/\sum_g c_q(g)$。
- multiset Jaccard: $J(q,p)=\sum_g\min(c_q(g),c_p(g))/\sum_g\max(c_q(g),c_p(g))$。

状態判定順は、`NOT_APPLICABLE`（両方空/欠損）→ `INSUFFICIENT_DATA`（片方だけ空/欠損）→ `TOO_SHORT`（一方でも 3 scalar 未満）→ 数値計算、と固定する。分母 0 は数値化しない。

| ID | 優先度 | 要求 | 受入条件 |
|---|---:|---|---|
| FR-070 | P0 | 上記アルゴリズムを AI なしで決定論的に実装し、Unicode/ICU 版を実行監査へ記録する。 | 日本語、結合文字、互換文字、絵文字/サロゲート、CRLF、各 Unicode 空白、重複 shingle の golden corpus が Windows/macOS/Linux で bit-exact 一致。 |
| FR-071 | P0 | 状態、完全包含、包含率、multiset Jaccard を別列へ出力する。 | 数値は 0〜1。`NOT_APPLICABLE`、`INSUFFICIENT_DATA`、`TOO_SHORT`、分母 0 は空欄と状態コード。 |
| FR-072 | P0 | 区分は Excel 数式で `完全包含`、`高類似`、`中類似`、`低類似`、`対象なし`、`情報不足`、`短すぎる`、`閾値未校正` を返す。 | `NOT_APPLICABLE`/`INSUFFICIENT_DATA`/`TOO_SHORT` を先に判定する。数値計算可能でも `threshold_approval_status<>"VALID"` なら区分セルは文字列 `閾値未校正` を返し、数値比較しない。 |
| FR-073 | P0 | 製品既定の業務判定閾値を埋め込まない。教員は対象コースの代表的な既知ペア集合で境界を校正し、false-positive/false-negative、標本、日付、承認者を記録する。閾値、校正データ hash、指標、course ID、承認者、日時を署名済み校正レコードにし、アプリ検証結果から `threshold_approval_status` を生成する。 | 初期の高/中閾値セルは空欄、status は `MISSING`。単純な編集可能 TRUE/FALSE cell だけで承認できない。記録欠落/署名不正/閾値との hash 不一致は `INVALID` となり区分は `閾値未校正`。Web 検索等の別用途の 0.90 を転用しない。[S15] |
| FR-074 | P0 | 類似度を減点、最終点、AI 利用認定へ自動接続しない。 | 既定の集計式が類似度列を参照しない。 |
| FR-075 | P0 | 「高類似は設問文が Prompt に含まれる表層的証拠であり、意図・不正・AI 生成を証明しない」と画面・Excel・README に表示する。 | 3 箇所で同趣旨を確認。 |
| FR-076 | P1 | 校正用データで閾値別の適合率/再現率をローカル算出できる。 | 正解ラベルは教員が作成し、アプリが捏造しない。 |

### 8.7 Excel 出力と計算

出力ブックの必須シートは次のとおり。

| シート | 用途 |
|---|---|
| `Eval`（衝突時は一意な実名） | 教員が選択した元シートの変換済みコピーに、項目別 AI 候補、根拠、類似度、教員確認、Excel 数式を追加した詳細表 |
| `Evaluation_Config` | `ReportDefinition`のreport type/revision/hash/target language、理由・根拠規則、不足flag、構造上限、Prompt/ルーブリック版、評価項目、点数範囲、重み、類似度閾値、採用規則。run snapshotとして出力し、次run用設定はアプリで編集可能 |
| `Evaluation_Run` | 入出力ハッシュ、アプリ/SDK/CLI/モデル版、実行日時、件数、状態、使用量、警告。回答本文・PII は含めない |

既存の `Old`、`Original`、`Final` その他は変更しない。既存 `Final` は参考資料として保持し、新しい結果の書込先にはしない。

`Eval` の**評価項目ごとの**論理列は、少なくとも次を含む。

- `{QID}.{EvaluatorID}.{ItemID}.AI候補点`
- `{QID}.{EvaluatorID}.{ItemID}.Confidence`
- `{QID}.{EvaluatorID}.{ItemID}.理由`
- `{QID}.{EvaluatorID}.{ItemID}.回答中の根拠`
- `{QID}.{EvaluatorID}.{ItemID}.不足項目`
- `{QID}.{EvaluatorID}.{ItemID}.教員上書き点`
- `{QID}.{EvaluatorID}.{ItemID}.教員判断`（`PENDING` / `ACCEPT_AI` / `OVERRIDE` / `REJECT`）
- `{QID}.{EvaluatorID}.{ItemID}.採用点`（数式）
- `{QID}.{EvaluatorID}.AI_STATUS`、`{QID}.{EvaluatorID}.VALIDATION_STATUS`、`{QID}.{EvaluatorID}.REVIEW_STATUS`
- `{QID}.類似度状態`、`{QID}.設問包含率`、`{QID}.MultisetJaccard`、`{QID}.類似度区分`
- 設問小計、総合点、要確認フラグ（数式）

| ID | 優先度 | 要求 | 受入条件 |
|---|---:|---|---|
| FR-080 | P0 | LLM が返した**全評価項目の個別値**は literal value として保存し、項目重み付け・設問小計・設問重み付け・総合点・採否は Excel 数式で計算する。 | 総合候補だけの応答は拒否。数式変更後に Excel で再計算される。 |
| FR-081 | P0 | 重み、最低/最高点、丸め桁数、空欄処理、総合式は`Evaluation_Config`のセル参照にする。丸めが必要な式はallowlistへ追加した`ROUND`だけを使用する。`ReportDefinition`のtarget language、reason/evidence/flag/構造上限も同sheetへsnapshot metadataとして出力する。 | 数式中に運用固有の点数・重み・丸め桁数を直書きしない。出力sheetのsnapshotを編集して進行中runのschemaや送信条件を変更できない。 |
| FR-082 | P0 | AI 候補点と教員上書き点を分離し、上書きは AI 候補を消さない。 | 変更履歴を比較可能。 |
| FR-083 | P0 | `教員判断=ACCEPT_AI` かつ候補が妥当なときだけ AI 候補を採用点へ、`OVERRIDE` かつ上書きが妥当なときだけ教員値を反映し、`PENDING`/`REJECT`/不正値では空欄にする。 | 未確認の AI 候補だけで設問小計/総合最終点が出ない。 |
| FR-084 | P0 | 空回答、通信失敗、構造失敗、内容フィルターを 0 点にしない。 | 点数セルは空欄、状態列に理由。 |
| FR-085 | P0 | アプリ生成式は`IF`、`IFERROR`、`AND`、`OR`、`ISNUMBER`、`SUM`、`SUMPRODUCT`、`COUNT`、`ROUND`、比較、四則演算、固定参照だけのallowlistとし、依存グラフをDAGにする。 | 式は8,192文字未満。重み合計0、空欄、欠落参照、範囲外でエラー値を出さず`VALIDATION_STATUS`に理由を出す。[S32] |
| FR-086 | P0 | 再計算モードを Automatic、full calculation on load にし、保存済み cached value を信頼済み結果として扱わない。 | 開く前は `RECALC_REQUIRED`。Windows/macOS Excel と Ubuntu LibreOffice の採用版で golden workbook を再計算・保存し、アプリ生成セルが期待値と一致。未試験環境を検証済みと表示しない。 |
| FR-087 | P0 | 不信な入力/LLM 文字列は内容にかかわらず Open XML の明示的な inline/shared **string cell** として書き、`CellFormula` node、external relationship、DDE、hyperlink を生成しない。危険表示の検知は literal 先頭および各 LF 後の先頭 Unicode scalar から Unicode White_Space/format controls を除いた最初の scalar を検査し、`= + - @`、全角 `＝＋－＠`（U+FF1D/U+FF0B/U+FF0D/U+FF20）、tab/CR/LF、U+200B/U+200C/U+200D/U+200E/U+200F/U+202A–U+202E/U+2066–U+2069/U+061C を flag する。[S17][S26] | 検知の成否にかかわらず cell type が string で formula node がないことを主要安全境界とする。各 vector、結合文字、改行後開始、format-control 挿入を package XML と Excel/LibreOffice E2E で検証。OWASP CSV の知見を Open XML の保証と誤認しない。 |
| FR-088 | P0 | アプリが追加する数式に外部ブック参照、DDE、WEBSERVICE、HYPERLINK、マクロを含めない。 | パッケージ XML と数式走査で 0 件。 |
| FR-089 | P0 | 一時ファイルへ保存後、再度開いて Open XML schema、relationship、変更許可リスト、数式 AST/参照、状態件数、物理上限を検証し、成功時だけ完成名へ原子的に変更する。 | 強制終了試験で元ファイル無傷、完成ファイル破損なし。既存入力由来の数式エラーと新規アプリ式エラーを分離。 |
| FR-090 | P0 | 1 行目を固定し、フィルター、折返し、列幅、条件付き書式で確認しやすくする。 | 530 行規模でエラー/要確認行へフィルター可能。 |
| FR-091 | P0 | 複雑な数式、閾値、Prompt ハッシュにセルコメントまたは隣接説明を付ける。 | 教員が Excel だけで意味と変更箇所を理解可能。 |
| FR-092 | P1 | `Evaluation_Config` に名前付き範囲または Excel テーブルを使う。 | 評価項目追加時に式範囲が追随し、参照切れしない。 |

### 8.8 レビュー、説明、異議申立て

| ID | 優先度 | 要求 | 受入条件 |
|---|---:|---|---|
| FR-100 | P0 | エラー、空欄、類似度警告、範囲端の点、再実行差が大きい結果をレビュー一覧へ出す。 | 各項目から該当行・根拠へ移動可能。 |
| FR-101 | P0 | 教員が候補点、理由、根拠、原回答を並べて確認し、採用/上書き/保留/却下できる。 | 操作は出力側だけに反映し、原本は不変。 |
| FR-102 | P0 | 根拠は対象回答からの短い抜粋または `根拠なし` とし、入力中の存在をコードで照合する。 | 存在しない引用は `UNVERIFIED_EVIDENCE` として要確認。 |
| FR-103 | P0 | 行ごとに、使用した設問、評価項目、Prompt版、model、候補点、reason、evidence状態、教員判断を説明可能にする。 | 学習者への説明用に、他学生データを含まない行単位表示とOS/platform print dialogからの印刷が可能。PDF/XLSX exportだけを印刷実装とみなさない。方式はG-17で実証する。 |
| FR-104 | P0 | 教員判断の変更時に任意commentを残せる。アプリはローカル起動者・判断者の本人性、所属、roleを認証・推測しない。 | AI候補と判断時刻を保持する。任意のreviewer labelを入力できるが、認証済みidentityとして表示せず、未入力でもreview操作を妨げない。 |
| FR-105 | P0 | README に、学習者への通知、説明、訂正、再確認、異議申立ての学内窓口を設定するよう案内する。[S13][S19] | 窓口未設定を初回運用チェックで警告。 |
| FR-106 | P1 | 教員間の二重レビュー用に、個人情報を最小化した確認用コピーを生成できる。 | 生成前に含有列をプレビューし、識別列を既定除外。 |

## 9. 非機能要求

### 9.1 セキュリティ

| ID | 優先度 | 要求・受入条件 |
|---|---:|---|
| NFR-SEC-001 | P0 | 最小権限: `CopilotClientMode.Empty`、`submit_evaluation` 1 件、全 permission deny とし、アプリは利用者が承認した入力/出力パス、OS 資格情報ストア、アクセス制御済み app state 以外へ本文を書かない。 |
| NFR-SEC-002 | P0 | 入力 package、Prompt、LLM 出力を不信データとして検証し、ZIP bomb/traversal、重複 part、危険 relationship、外部参照、数式注入、Prompt Injection、tool protocol 違反の攻撃試験を行う。[S16][S17][S26] |
| NFR-SEC-003 | P0 | 秘密はソース、設定、環境サンプル、Excel、ログへ格納しない。OAuth token は OS keychain/credential vault に委ね、明示 token 以外の環境変数/CLI/`gh` 資格情報 fallback を無効にする。 |
| NFR-SEC-004 | P0 | 依存パッケージをロックし、リリースごとに SBOM、ライセンス一覧、脆弱性走査結果、SHA-256 を公開する。 |
| NFR-SEC-005 | P0 | Windows 実行物へコード署名、macOS アプリへ署名・notarization、Linux パッケージへ署名/チェックサムを付ける。未署名配布を README で推奨しない。 |
| NFR-SEC-006 | P0 | TLS 証明書検証を無効化する選択肢をアプリへ設けない。企業プロキシでは正しいルート証明書の導入を案内する。[S27] |
| NFR-SEC-007 | P0 | GitHub 公式 allowlist の認証/Copilot エンドポイントだけを文書化し、固定一覧は公式更新に追随させる。[S28] |
| NFR-SEC-008 | P0 | 重大なセキュリティ問題の非公開報告先、対応手順、サポート期限を `SECURITY.md` に記載する。 |
| NFR-SEC-009 | P0 | OAuth client ID、redirect/device-flow 設定、requested scopes、組織承認 ID をリリース構成として固定し、client secret や personal token を desktop build へ同梱しない。 |

### 9.2 プライバシーとデータガバナンス

| ID | 優先度 | 要求・受入条件 |
|---|---:|---|
| NFR-PRI-001 | P0 | 利用目的、対象データ、送信先、保存先、保存期間、アクセス者、問い合わせ先を運用前に文書化する。[S29] |
| NFR-PRI-002 | P0 | 実在学生データ送信前に通知・同意またはその他の適法な根拠を機関が確認する。アプリは同意の法的有効性を自動判断しない。[S14] |
| NFR-PRI-003 | P0 | データ最小化を採用し、回答評価に不要な識別列・他設問を送信しない。 |
| NFR-PRI-004 | P0 | モデル提供者、保持例外、学習利用設定を運用承認記録へ残す。モデル切替時は再承認する。[S05][S13] |
| NFR-PRI-005 | P0 | 実データをテスト、スクリーンショット、Issue、CI ログへ使用しない。合成 fixture を用いる。 |
| NFR-PRI-006 | P1 | 出力先が同期フォルダー/共有フォルダーの可能性を検出できる範囲で警告し、OS のディスク暗号化と最小アクセス権を推奨する。 |

### 9.3 信頼性、再現性、性能

| ID | 優先度 | 要求・受入条件 |
|---|---:|---|
| NFR-REL-001 | P0 | 例外、通信切断、ディスク不足、取消、強制終了で入力を破損せず、暗号化/アクセス制御済み checkpoint から再開可能。完成前ファイルを完成品と誤認させない。 |
| NFR-REL-002 | P0 | 空欄、失敗、未実行を数値 0 と区別する。 |
| NFR-REL-003 | P0 | 同一 Prompt/モデルでも同一結果を保証しないことを表示し、再現用メタデータと複数回検証を提供する。[S05] |
| NFR-REL-004 | P0 | 入力/選択シート/生成シート名/Prompt/ルーブリック/モデル/SDK/CLI/アプリ/Unicode-ICU 版のいずれかが変われば結果キャッシュを流用しない。 |
| NFR-REL-005 | P0 | UI スレッドで Excel 全走査や Copilot 待機を行わず、処理中も停止/取消を操作可能にする。 |
| NFR-REL-006 | P0 | release candidate をクリーン起動し、warm-up 1 回後 5 回測定する。基準機（4 論理コア以上、RAM 8 GiB、空き 2 GiB、SSD、Windows 11/macOS 14/Ubuntu 22.04 各 x64 または対象 Arm64）で、合成 531 行 × 100 列/50 MiB 以下の非暗号化 workbook の読込→安全検査→`Eval` 生成→ローカル検証（Copilot 待機除外）の中央値 30 秒以下、p95 UI input latency 200 ms 以下、peak working set 1.5 GiB 以下とする。 |
| NFR-REL-007 | P0 | 進捗は総評価単位、完了、失敗、スキップ、再試行中を区別し、残時間は十分な実測がある場合だけ推定表示する。 |
| NFR-REL-008 | P0 | Copilot E2E 性能はモデル/ネットワーク依存のため固定 SLA を捏造せず、実行ごとに評価単位 latency p50/p95、429、retry、timeout、token usage を匿名集計し、RC ごとに承認済み環境で測定する。 |
| NFR-REL-009 | P0 | 正常終了、取消、強制終了 recovery 後に、アプリ所有 session ID が SDK/CLI disk storage に残らず、合成 canary 本文が logs/session/cache/crash artifacts にないことを検証する。 |

### 9.4 公平性と測定品質

| ID | 優先度 | 要求・受入条件 |
|---|---:|---|
| NFR-FAI-001 | P0 | 本番前に対象授業・言語・設問の人間採点基準データを用いて妥当性・信頼性を評価する。[S09] |
| NFR-FAI-002 | P0 | 人間採点との一致率、隣接一致率、重み付き Cohen の $\kappa$、点差分布、再実行一致を報告する。採用閾値は教育評価責任者が利用目的ごとに事前定義し、アプリが捏造しない。[S09] |
| NFR-FAI-003 | P0 | 適法かつ必要な範囲で、言語背景、回答長、支援利用等の関連部分集団ごとに誤差を確認する。属性は Copilot へ送信しない。[S09][S11] |
| NFR-FAI-004 | P0 | 簡潔さ、冗長さ、設問順序、自己生成文、表記揺れによる偏りを反例テストする。[S06][S07] |
| NFR-FAI-005 | P0 | Prompt、ルーブリック、モデルを変更した場合は旧検証結果を無効化し、再校正する。 |
| NFR-FAI-006 | P0 | 校正基準未達の場合、本番モードを有効にせず、「研究/試行」として候補値のみ出力する。 |

### 9.5 アクセシビリティとユーザビリティ

| ID | 優先度 | 要求・受入条件 |
|---|---:|---|
| NFR-ACC-001 | P0 | 全操作をキーボードだけで行え、論理的な Tab 順序、可視フォーカス、ショートカットを持つ。 |
| NFR-ACC-002 | P0 | 標準コントロールと `AutomationProperties` を使い、Windows Narrator/NVDA、macOS VoiceOver、Linux Orca で主要フローを試験する。[S23] |
| NFR-ACC-003 | P0 | 色だけで状態を表さず、アイコン、文字、状態コードを併記する。200% 拡大で情報・操作を失わない。 |
| NFR-ACC-004 | P0 | 専門用語を避けた日本語を既定表示とし、エラーに「何が起きたか」「データへの影響」「次の操作」を示す。 |
| NFR-ACC-005 | P1 | WCAG 2.2 AA と WCAG2ICT の考え方をデスクトップ UI へ適用し、適合チェック表を残す。 |

### 9.6 対応環境

P0 の検証対象を次に限定し、未検証 OS を「対応」と表示しない。

- Windows 11 x64 / Arm64
- macOS 14 以降 x64 / Arm64
- Ubuntu 22.04 LTS / 24.04 LTS x64 / Arm64（X11 / Wayland）
- `.NET 10 LTS` の自己完結配布。利用端末への .NET SDK 導入は不要。[S23][S24]
- GitHub Copilot .NET SDK の同梱 CLI 対応 RID: `win-x64`、`win-arm64`、`linux-x64`、`linux-arm64`、`linux-musl-x64`、`linux-musl-arm64`、`osx-x64`、`osx-arm64`。[S30]

Linuxの対応表示ではX11、Wayland session上のXWayland、native Waylandを別routeとして記録する。XWayland成功をnative Wayland成功と表示しない。native Avalonia Wayland backendがexperimentalである間はcharacterizationに限定し、正式対応へ数えない。未試験OS/build/arch/display routeを対応済みと表示しない。

## 10. 画面要求

| 画面 | 必須項目 |
|---|---|
| 1. はじめに | 目的、AI は補助であること、送信される情報、オフライン範囲、プライバシー確認、実データ/合成データモード |
| 2. GitHub 認証 | OAuth 状態、login/組織、管理者承認 ID/期限（SDK によるプラン推測ではない）、デバイスフロー開始、アカウント切替、ログアウト、ネットワーク診断 |
| 3. ファイル選択 | 入力パス、出力先、暗号化/保護分類、危険要素、資源上限、サイズ、ハッシュ、シート一覧、原本不変表示 |
| 4. 設問マッピング | 設問追加、主回答、学習者 Prompt、補助、識別、無視、非空件数、サンプル値の伏字プレビュー |
| 5. 評価設計 | report type/revision、target language、Prompt、変数、評価項目、点数アンカー、reason/evidence規則、missing flags、item/ID/field上限、重み、類似度閾値、版/hash、最悪schema size |
| 6. 試行・検証 | 送信プレビュー、構造化結果、エラー、再実行差、人間基準との比較 |
| 7. 実行確認 | report snapshot/hash、model、対象件数、最大outbound attempts、schema/request budget、利用量見積、出力名、実行確認。起動者identity/roleの承認checkは設けない |
| 8. 実行中 | 進捗、成功/失敗/スキップ、現在の設問（学生名は既定非表示）、停止、取消、再開 |
| 9. レビュー | 原回答、候補点、理由、根拠、状態、類似度、採用、上書き、保留、フィルター |
| 10. 完了 | 入力不変、出力場所、件数、要確認、利用量、式検証、フォルダーを開く |
| 11. 設定/診断 | ログ場所、保持、プロキシ案内、許可ドメイン、版情報、SBOM、診断エクスポート（本文なし） |

すべての破壊的または費用を生む操作には、対象と影響を示す確認を置く。評価中断は常に可能にし、画面を閉じても入力へ書き込まない。

## 11. アーキテクチャ要求

### 11.1 基準構成

- UI: Avalonia UI + MVVM。[S23]
- Runtime: .NET 10 LTS。[S24]
- AI: 要求調査基準 `GitHub.Copilot.SDK v1.0.11`。実装時の exact SDK/NuGet/同梱 CLI を lock file、SBOM、ハッシュで固定し、`CopilotClientMode.Empty` を使用。[S04]
- Excel: Open XML SDK low-level API を production reader/writer の基準とし、package byte copy 後に許可パーツだけを変更する。ClosedXML は production workbook save path では使用せず、採用する場合も検証/fixture 補助に限定する。[S20][S21]
- 状態: 出力 `.partial.xlsx`、回答本文を含まない実行マニフェスト、必要最小限の暗号化 checkpoint。SDK/CLI session は評価単位後に明示削除する。サーバー/クラウド DB を使用しない。
- Unicode: runtime image に固定版 app-local ICU を同梱し、類似度正規化の OS 差を golden corpus で封じる。[S33]
- DI、ログ、設定は標準 .NET 抽象を用い、UI、Excel、Copilot、類似度、数式生成を分離する。

### 11.2 コンポーネント境界

1. **Desktop UI**: 入力、確認、進捗、レビューだけを担当。
2. **Application Orchestrator**: 状態機械、取消、再開、件数上限、エラー分類。
3. **Workbook Reader/Writer**: 読み取り専用入力、複製、値/式/書式、原子的保存。
4. **Mapping & Prompt Engine**: 設問定義、テンプレート検証、データ最小化、境界付与。
5. **Similarity Engine**: 固定 ICU の NFKC、Unicode scalar 3-shingle multiset、包含率、Jaccard。ネットワーク/AI 依存なし。
6. **Copilot Gateway**: アプリ所有 OAuth、管理者承認 gate、モデル一覧、Empty mode、一時 session、tool/permission 拒否、明示 session 削除、usage、再試行。
7. **Output Validator**: スキーマ、範囲、引用存在、文字列安全化。
8. **Formula Builder**: 設定参照式、採用点、集計、エラー回避。
9. **Run Manifest & Safe Logger**: ハッシュ、版、件数、状態だけを保存。

Copilot Gateway から Workbook Writer を直接呼び出してはならない。LLM はファイルパスを受け取らず、書込権限を持たない。

## 12. セットアップ・配布要求

### 12.1 配布物

- `scripts/setup-windows.cmd`: OS 標準環境から署名済み Windows パッケージを取得・検証し、必要なら PowerShell 7 LTS を正規配布元から導入して `setup-windows.ps1` へ委譲する最小 bootstrap。
- `scripts/setup-windows.ps1`: PowerShell 7+ だけで実処理し、Windows PowerShell 5.1 では自己停止または上記 bootstrap へ案内する。
- `scripts/setup-macos.sh`: CPU 判定、署名/チェックサム検証、アプリ配置、Gatekeeper/notarization 確認。
- `scripts/setup-linux.sh`: 対応ディストリビューション/CPU 判定、署名/チェックサム検証、ユーザー領域または承認済みパッケージ導入。
- OS/CPU 別の自己完結アプリパッケージ、`SHA256SUMS`、署名、SBOM、ライセンス一覧。

### 12.2 セットアップ要件

| ID | 優先度 | 要求・受入条件 |
|---|---:|---|
| SET-001 | P0 | OS の新規対応状態から、開発 SDK や IDE を手作業で入れずに完了する。アプリは .NET と Copilot CLI を自己完結で含む。 |
| SET-002 | P0 | CPU/OS に合う artifact だけを取得し、HTTPS、署名、公開 SHA-256 の全てを検証してから実行する。 |
| SET-003 | P0 | 管理者権限を既定で要求せず、必要な場合は理由・変更先・取消方法を事前表示する。 |
| SET-004 | P0 | 冪等であり、再実行は修復/同版確認/明示更新になり、設定や出力を削除しない。同版の正常 install は再 download せず検証し、不完全 artifact は hash/signature 検査後に安全除去して再取得する。異版は upgrade/downgrade と変更点を確認後に binaries だけを置換する。 |
| SET-005 | P0 | インストール後にアプリ起動、版、Excel ローカル自己診断、Copilot 接続診断を実行できる。認証は利用者がアプリで行う。 |
| SET-006 | P0 | プロキシ資格情報を引数、ログ、URL 平文へ保存しない。証明書検証無効化を自動実施しない。[S27] |
| SET-007 | P0 | uninstall 手順を提供し、アプリと任意ログだけを削除する。入力/出力 Excel と OS keychain 資格情報は別確認にする。 |
| SET-008 | P0 | 開発用 SDK 導入は `docs-dev` に分離し、教員向けセットアップへ混在させない。 |

## 13. ドキュメント要求

ルートの利用者文書は **`/README.md`**、利用者画像は **`/images/`**、開発者文書は **`/docs-dev/`**、開発者画像は **`/docs-dev/images/`** に置く（`/doc-dev` は使用しない）。アーキテクチャ図は標準 **SVG** 形式とする。

### 13.1 `/README.md`（コンピューターに詳しくない教員向け）

必須内容:

1. 何ができるか、できないか、AI は最終採点者ではないこと。
2. 入力を変更しない仕組み、毎回新しい出力を作る理由、評価済み出力を次回入力へ使わない注意、出力ファイル名/保存先。
3. Microsoft Forms/Google Sheets から機関承認済みの処理用 `.xlsx` コピーを作る手順と、コピー操作が暗号化/権利保護解除を保証しない注意。[S02][S03]
4. Windows/macOS/Linux 別セットアップを、1 手順 1 操作で説明。
5. GitHub Copilot Business/Enterprise の必要性、アプリ内 OAuth、ログアウト、管理者承認の確認方法。SDK が契約プランを自動判定すると説明しない。
6. 初回チュートリアル: ファイル選択 → マッピング → Prompt → 試行 → 実行 → レビュー → Excel。
7. 全画面の実画面スクリーンショット、番号付き吹き出し、各項目説明、代替テキスト。
8. スクリーンショットは実在学生データを使わず、合成データを使用。
9. 全 README 用画像を `/images` に保存。外部画像 URL に依存しない。
10. 暗号化/権利保護ファイルの分類限界、P0 が暗号化 compound file を処理しないこと、方式を推測せず機関承認済み手順で処理用コピーを作る案内。リポジトリの `/sample` は要求分析用で、実行テスト入力ではないこと。
11. 類似度が不正/AI 使用の証明ではない注意。
12. Excel の AI 候補、教員上書き、採用点、数式、重み、閾値の変更方法。
13. 停止・取消・再開、認証、プロキシ、証明書、内容フィルター、ディスク不足、数式エラーのトラブルシュート。
14. プライバシー、保存期間、共有時の注意、学生への説明/異議申立て窓口。
15. 対応版、更新、アンインストール、問い合わせ、セキュリティ報告。
16. 参考資料と「参照日」。

スクリーンショットはアプリ実装後に実物から取得する。モックを実画面と称したり、未実装画面を作成済みとして掲載してはならない。

### 13.2 `/docs-dev`（Vibe Coding でカスタマイズする開発者向け）

必須成果物:

- `/docs-dev/architecture.md`: 信頼境界、データフロー、オフライン境界、Copilot 境界、失敗/再開。
- `/docs-dev/components.md`: 上記 9 コンポーネントの責務、公開インターフェイス、依存方向。
- `/docs-dev/excel-contract.md`: 入力/出力シート、列命名、セル型、数式、状態コード、32,767 文字、保全対象。
- `/docs-dev/prompt-authoring.md`: 変数、スキーマ、評価アンカー、Prompt Injection 対策、良い/悪い例。
- `/docs-dev/evaluation.md`: 人間基準データ、反復、$\kappa$、差分、部分集団、公平性、リリース判定。
- `/docs-dev/security-privacy.md`: 脅威モデル、データ最小化、ログ、資格情報、モデル許可、Formula Injection、依存管理。
- `/docs-dev/build-test-release.md`: .NET SDK、ビルド、単体/統合/E2E、OS マトリクス、署名、SBOM、セットアップ生成。
- `/docs-dev/customization.md`: 設問追加、Prompt/重み/数式/UI の変更例と再検証条件。
- `/docs-dev/adr/`: 技術・リスク判断記録。
- `/docs-dev/images/architecture.svg`: アーキテクチャ図。
- `/docs-dev/images/components.svg`: コンポーネント図。
- その他の開発者画像も `/docs-dev/images` だけに保存。

SVG はテキスト要素、矢印、凡例、信頼境界を持ち、ダーク/ライト背景で判読でき、画像内情報を本文でも説明する。

## 14. テスト・検証要求

### 14.1 自動テスト

1. **Workbook golden tests**: 値、式、日付、改行、Unicode、結合、表、filter、style、chart、image、custom XML、external link、hidden/VeryHidden、digital signature fixture を持つ合成 `.xlsx`。許可リスト外 part bytes/URI/content type/relationship が一致することを検査。
2. **原本不変/TOCTOU テスト**: 読取成功、失敗、取消、クラッシュ、ディスク不足、入力パス差替えの全経路で原本 SHA-256/ファイル ID 不変。
3. **保護分類テスト**: 本サンプルを `ENCRYPTED_COMPOUND_FILE` と判定し、MIP と断定せず、解除試行なしで安全停止。OOXML workbook/sheet protection との区別も確認。
4. **package hostile-input tests**: ZIP bomb、圧縮比/entry/展開量超過、traversal、重複 part、破損 relationship、DDE/OLE/ActiveX/connection、macro、式長/行列/セル長上限。
5. **マッピング/衝突テスト**: 1/2/10 設問、列順変更、同名見出し、空列、途中空行、Google/Microsoft 形式、選択元が `Original` 以外、既存 `Eval`/`eval`/31 文字名。
6. **ReportDefinition/Promptテスト**: 未定義変数、巨大入力、命令注入、多言語、学習者本文中の区切り文字、report別reason/evidence規則・missing flag enum・target language・item/ID上限、0/負/過大値、最大instance serialization、別report schema再利用、run開始後変更を検査する。
7. **Policy/OAuth/Copilot contract tests**: 運用policyのvalid/expired/truncated/wrong issuer/unknown schema/current-next key/revoked key/offline recovery、Device Flow interval/slow_down/expiry/cancel/refresh rotation、OAuth account/org再確認、local launcher identity非依存、資格情報fallback禁止、Empty mode、result tool 1件、その他permission全Reject、通常本文無視、動的schemaの項目欠落/重複/未知flag/総合値だけ、不正JSON、timeout、429/5xx、認証切れ、内容filter。
8. **session/telemetry tests**: 正常/取消/crash recovery/一時的削除失敗後の `DeleteSessionAsync`、次回起動時 orphan cleanup、cleanup retry UI、session storage/log/cache の canary 走査、`EnableSessionTelemetry=false`、OpenTelemetry/remote session 無効、許可外通信 0 件。
9. **類似度テスト**: 完全コピー、前後文追加、Unicode 空白/改行、互換文字、結合文字、絵文字/サロゲート、重複 shingle、片空、両空、3 scalar 未満、日本語、英語。全 OS bit-exact。
10. **Formula Injection テスト**: `=`, `+`, `-`, `@`, tab、CR/LF、全角 `＝＋－＠`、U+200B/U+200C/U+200D/U+200E/U+200F/U+202A–U+202E/U+2066–U+2069/U+061C、結合文字を literal 先頭/Unicode 空白後/format control 後/各改行後に置き、すべて string cell、formula/外部関係 0 件となることを検査。[S26]
11. **数式テスト**: `AI_STATUS`/`VALIDATION_STATUS`/`REVIEW_STATUS` の直積を 500 行以上で生成し、`SUCCESS + VALID + (ACCEPTED|OVERRIDDEN)` 以外の採用点が必ず空欄になる不変条件を検査する。未確認、採用、上書き、却下、空欄、zero/negative/non-numeric weight、項目/評価器追加、DAG/循環/式長、入力由来既存エラーと新規エラーも検査。
12. **数式 E2E**: Windows/macOS Excel と Ubuntu LibreOffice の固定採用版で golden workbook を再計算・保存し、期待値、新規 error 0、式保持を確認。
13. **再開テスト**: 完了単位を二重送信せず、入力/`ReportDefinition` snapshot/hash/設定/版/選択元/生成名のいずれか変更時は再利用しない。
14. **ログ検査**: PII、回答本文、Prompt 本文、token、通常 assistant 本文がログにない。
15. **ネットワーク E2E**: 承認済み GitHub OAuth/Copilot/必須 control plane 以外の外向き通信がない。
16. **アクセシビリティ E2E**: キーボード、focus、screen reader、拡大、高 contrast。
17. **クロス OS E2E**: 対応 OS/CPU ごとに setup、認証、合成 2 行評価、Excel 出力、uninstall。
18. **性能/資源テスト**: NFR-REL-006 の基準機・回数・fixture で時間、UI latency、peak memory を測定し、生結果と環境を release artifact に保存。
19. **類似度校正 gate テスト**: 閾値空欄、校正 record 欠落/署名不正/hash 不一致/期限切れ/valid の各状態で、無効時は常に `閾値未校正`、valid 時だけ数値区分になることを Excel/LibreOffice で確認。

### 14.2 教育評価のリリースゲート

実在学生データで運用する前に、次を満たす。

1. 対象授業から、教員 2 名以上が独立採点し、相違を調停した基準セットを機関手続に従って準備する。
2. Prompt とモデルを固定し、各回答を複数回評価して非決定性を測る。
3. 人間基準との一致率、隣接一致率、重み付き $\kappa$、平均/絶対点差、順位相関、再実行差を報告する。
4. 利用可能で適法な属性について、部分集団別の誤差と欠測を確認する。
5. 冗長/簡潔、丁寧/非定型、日本語表記揺れ、Prompt Injection、設問文コピー等の反例を含める。
6. 合格基準と重大差の定義は測定前に教育評価責任者が定める。結果を見て後付けしない。
7. 不合格なら Prompt、ルーブリック、用途を見直し、再検証する。自動採点へ格上げしない。
8. モデル、SDK、Prompt、評価範囲、授業対象が変わるたびに再実施する。
9. 類似度閾値は採点妥当性とは別に、対象授業の既知ペア集合で false-positive/false-negative を測り、測定前の判定基準と承認者を記録する。
10. Copilot契約/組織/model/保持条件の管理者承認はSDK推測に置き換えず、期限切れ時に再承認する。これはGitHub service利用条件であり、local launcherの本人・role承認ではない。

### 14.3 Definition of Done

- 全 P0 要求にテストまたは承認証跡が紐付く。
- 対応 OS の CI/E2E が成功。
- 0 件の既知 Critical/High 脆弱性（例外は期限・軽減策・承認を記録）。
- 数式エラー 0、外部リンク/マクロ追加 0、入力差分 0。
- 変更許可リスト外 package part/relationship 差分 0、アプリ生成数式エラー 0、外部リンク/マクロ追加 0、入力差分 0。
- 本文を含まないログ/session/cache とネットワーク検査が成功。
- 実画面スクリーンショットを含む README と全 `docs-dev` 成果物が現行版に一致。
- 教育評価、個人情報、情報セキュリティの各責任者が運用を承認。
- 要求作成者とは別の reviewer が P0 要件と受入証跡を確認し、未解決 blocker/high 0 件、medium/low の受容理由・owner・期限を署名付き release record に残す。

## 15. 状態・エラーコード

状態を 1 コードへ畳み込まず、次の直交する軸で保存する。採用点が非空であるための必要十分条件は `AI_STATUS=SUCCESS`、`VALIDATION_STATUS=VALID`、かつ `REVIEW_STATUS∈{ACCEPTED, OVERRIDDEN}` である。論理式では `(AI_STATUS != SUCCESS) OR (VALIDATION_STATUS != VALID) OR (REVIEW_STATUS NOT IN {ACCEPTED, OVERRIDDEN})` なら採用点を必ず空欄にする。

| 軸 | コード | 意味 |
|---|---|---|
| `AI_STATUS` | `NOT_RUN` / `SUCCESS` / `SKIPPED_EMPTY` / `FAILED_RETRYABLE` / `FAILED_FINAL` / `CANCELLED` / `POLICY_BLOCKED` / `SCHEMA_INVALID` / `TOOL_PROTOCOL_VIOLATION` | Copilot 評価単位の実行結果 |
| `VALIDATION_STATUS` | `VALID` / `RECALC_REQUIRED` / `OUT_OF_RANGE` / `UNVERIFIED_EVIDENCE` / `FORMULA_ERROR` / `UNSAFE_INPUT` / `INCOMPLETE` | 候補、根拠、数式、入力のコード検証結果 |
| `REVIEW_STATUS` | `PENDING` / `ACCEPTED` / `OVERRIDDEN` / `REJECTED` / `ESCALATED` | 教員の判断。`PENDING`/`REJECTED`/`ESCALATED` は採用点空欄 |
| `SIMILARITY_STATUS` | `NOT_APPLICABLE` / `INSUFFICIENT_DATA` / `TOO_SHORT` / `COMPUTED` / `UNCONFIGURED` | 類似度計算/校正の状態 |
| `RUN_STATUS` | `READY` / `RUNNING` / `PAUSED` / `CANCELLED` / `INPUT_CHANGED` / `PROTECTED_INPUT` / `UNSAFE_PACKAGE` / `POLICY_BLOCKED` / `SESSION_CLEANUP_FAILED` / `OUTPUT_VALIDATION_FAILED` / `COMPLETED` | workbook 実行全体の状態 |

低レベル原因は別の `ERROR_CODE`（例: `AUTH_EXPIRED`、`ORG_APPROVAL_MISSING`、`MODEL_UNAVAILABLE`、`NETWORK_TIMEOUT`、`CONTENT_FILTERED`、`SESSION_DELETE_FAILED`）へ記録し、状態コードを原因文字列の寄せ集めにしない。

## 16. 将来拡張

| ID | 優先度 | 条件 |
|---|---:|---|
| EXT-001 | P2 | MIP SDK による保護 `.xlsx` 読み取り。Entra アプリ登録、追加通信、利用者権限、出力保護、macOS/Linux 配布、DPIA、ライセンスを別要求として承認した場合のみ。 |
| EXT-002 | P2 | LMS 連携。書戻し・通知・API 認証を別信頼境界として設計し、既定無効。 |
| EXT-003 | P2 | 組織内ローカルモデル/BYOK。提供者ごとの保持・学習・費用・規約を再評価し、Copilot と同じであると仮定しない。 |
| EXT-004 | P2 | 他言語 UI。翻訳ごとに評価 Prompt の同等性を再検証。 |
| EXT-005 | P2 | 大規模ブックのストリーミング処理。Open XML SAX 方式を検討。[S20] |

AI 文章検出器による不正判定は将来拡張にも含めない。採用するには、対象言語・対象集団で独立に検証された新たな根拠、異議申立て、誤検知統制、機関承認を伴う別プロジェクトとする。[S10][S11][S12]

## 17. 実装開始前の外部リリースゲート

次は実装漏れではなく、利用組織・教員が所有する必須入力/承認である。

1. 実装前には、任意のレポート種類を起動後に作成できるgeneric `ReportDefinition` schema、動的tool schema生成、最大instance/resource検証、snapshot/hash/new-run規則を合成fixtureで確定する。実際のPrompt、rubric、anchor、reason/evidence本文、missing flags、対象言語、教育用途ごとの上限は実装前外部入力にせず、利用者がレポート作成時に設定する。
2. サンプルと同じ列構造を持つ、個人情報を除いた**非暗号化の合成 `.xlsx` fixture** と、各危険 package/数式/Unicode edge case fixture を作る。
3. Copilot Business/Enterpriseの組織、許可model、保持/学習/データ所在条件、費用上限、OAuth App client ID/scope/device-flow設定を管理者が期限付きで承認し、FR-041の署名済みlocal policyを発行する。署名鍵の保管、公開鍵配布、失効、更新を機関運用で定める。SDKによるplan推測やlocal launcher identityで代替しない。
4. 学生への通知文、問い合わせ/訂正/異議申立て窓口、保存期間を機関が承認する。
5. 教育評価責任者が人間基準データ、採用指標、合格閾値を事前登録する。
6. EU で提供・利用する場合、現行 EU AI Act の分類、提供者/導入者責任、適用開始日、適合性評価を法務担当が再確認する。[S19]
7. 採用する GitHub Copilot SDK/NuGet/同梱 CLI exact version、SHA-256、通信 allowlist、session 保存先/削除 API の動作を source と合成 canary で再確認する。
8. Excel/LibreOffice の採用版、Open XML 変更許可リスト、app-local ICU exact version、対応 RID を固定し、cross-platform golden tests を実行する。

## 18. 出典

すべて 2026-08-31 参照。数値・結論は、対象の異なる研究から本システムへ直接外挿しない。

- **[S01] 内部一次資料**: `sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx`。Microsoft Excel 読み取り専用 COM 解析。回答本文・氏名・メールは抽出/表示せず、シート、見出し、非空件数、数式、ファイルシグネチャ、SHA-256 のみ確認。
- **[S02] Microsoft Support**, 「フォームの結果を確認して共有する」: https://support.microsoft.com/office/check-and-share-your-form-results-02859424-341d-406f-b32a-9a0fbaf357af
- **[S03] Google Docs Editors Help**, “Choose where to save form responses”: https://support.google.com/docs/answer/2917686
- **[S04] GitHub**, GitHub Copilot SDK tag `v1.0.11`: Authentication, GitHub OAuth setup, Getting Started, .NET README/source（`CopilotClientMode.Empty`、`GetAuthStatusAsync`、`DeleteSessionAsync`、`EnableSessionTelemetry`、permission handler を含む）: https://github.com/github/copilot-sdk/tree/v1.0.11 ; https://github.com/github/copilot-sdk/blob/v1.0.11/docs/auth/authenticate.md ; https://github.com/github/copilot-sdk/blob/v1.0.11/docs/setup/github-oauth.md ; https://github.com/github/copilot-sdk/blob/v1.0.11/docs/getting-started.md ; https://github.com/github/copilot-sdk/blob/v1.0.11/dotnet/README.md
- **[S05] GitHub Docs**, “Application card: GitHub Copilot Chat,” “About customizing GitHub Copilot responses,” “Hosting of models for GitHub Copilot”: https://docs.github.com/en/copilot/responsible-use/chat ; https://docs.github.com/en/copilot/concepts/prompting/response-customization ; https://docs.github.com/en/copilot/reference/ai-models/model-hosting
- **[S06] Zheng, L. et al. (2023)**, “Judging LLM-as-a-Judge with MT-Bench and Chatbot Arena,” NeurIPS 2023 Datasets and Benchmarks Track: https://doi.org/10.48550/arXiv.2306.05685
- **[S07] Liu, Y. et al. (2023)**, “G-Eval: NLG Evaluation using GPT-4 with Better Human Alignment,” EMNLP 2023, pp. 2511–2522: https://doi.org/10.18653/v1/2023.emnlp-main.153
- **[S08] Mizumoto, A. & Eguchi, M. (2023)**, “Exploring the potential of using an AI language model for automated essay scoring,” *Research Methods in Applied Linguistics*, 2(2), 100050: https://doi.org/10.1016/j.rmal.2023.100050
- **[S09] AERA, APA, NCME (2014)**, *Standards for Educational and Psychological Testing*, とくに Standards 2.0, 3.8–3.9, 4.19–4.20, 12.6（印刷頁 42, 66–67, 91–92, 197）: https://www.testingstandards.net/open-access-files.html ; https://www.testingstandards.net/uploads/7/6/6/4/76643089/standards_2014edition.pdf
- **[S10] Weber-Wulff, D. et al. (2023)**, “Testing of detection tools for AI-generated text,” *International Journal for Educational Integrity*, 19, 26: https://doi.org/10.1007/s40979-023-00146-z
- **[S11] Liang, W. et al. (2023)**, “GPT detectors are biased against non-native English writers,” *Patterns*, 4(7), 100779: https://doi.org/10.1016/j.patter.2023.100779 ; preprint: https://arxiv.org/abs/2304.02819
- **[S12] Sadasivan, V. S. et al.**, “Can AI-Generated Text be Reliably Detected?”, arXiv record `2303.11156`（版履歴はリンク先 record を正とする）: https://doi.org/10.48550/arXiv.2303.11156
- **[S13] GitHub**, “GitHub Generative AI Services Terms,” version 2026-03; “Models and pricing for GitHub Copilot”: https://github.com/customer-terms/github-generative-ai-services-terms ; https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing
- **[S14] Microsoft**, “Code of Conduct for Microsoft AI Services,” version 4.0, 2026-05-01: https://learn.microsoft.com/legal/ai-code-of-conduct
- **[S15] Manning, C. D., Raghavan, P., Schütze, H. (2008/2009 web edition)**, *Introduction to Information Retrieval*, “Near-duplicates and shingling”: https://nlp.stanford.edu/IR-book/html/htmledition/near-duplicates-and-shingling-1.html
- **[S16] OWASP GenAI Security Project**, “LLM01:2025 Prompt Injection”: https://genai.owasp.org/llmrisk/llm01-prompt-injection/
- **[S17] OWASP GenAI Security Project**, “LLM02 Sensitive Information Disclosure,” “LLM05 Improper Output Handling,” “LLM06 Excessive Agency,” “LLM10 Unbounded Consumption”: https://genai.owasp.org/llmrisk/llm022025-sensitive-information-disclosure/ ; https://genai.owasp.org/llmrisk/llm052025-improper-output-handling/ ; https://genai.owasp.org/llmrisk/llm062025-excessive-agency/ ; https://genai.owasp.org/llmrisk/llm102025-unbounded-consumption/
- **[S18] NIST**, *AI Risk Management Framework 1.0* and *Generative AI Profile (NIST AI 600-1)*: https://doi.org/10.6028/NIST.AI.100-1 ; https://doi.org/10.6028/NIST.AI.600-1
- **[S19] European Union**, Regulation (EU) 2024/1689, consolidated text 2026-07-27, in particular Articles 6, 9–15, 26–27, 85–86 and Annex III 3(b): https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:02024R1689-20260727
- **[S20] Microsoft Learn**, “Open XML SDK,” read-only spreadsheet access, large spreadsheet parsing: https://learn.microsoft.com/office/open-xml/open-xml-sdk ; https://learn.microsoft.com/office/open-xml/spreadsheet/how-to-open-a-spreadsheet-document-for-read-only-access ; https://learn.microsoft.com/office/open-xml/spreadsheet/how-to-parse-and-read-a-large-spreadsheet
- **[S21] ClosedXML**, repository and documentation (“Copying Worksheets,” “Formula calculation”): https://github.com/ClosedXML/ClosedXML ; https://github.com/closedxml/closedxml/wiki/Copying-Worksheets ; https://docs.closedxml.io/en/latest/concepts/formula-calculation.html
- **[S22] Microsoft Learn**, “Excel Worksheet and Expression Evaluation — Long Unicode Strings”: https://learn.microsoft.com/office/client-developer/excel/excel-worksheet-and-expression-evaluation#long-unicode-strings
- **[S23] Avalonia UI**, supported platforms and accessibility: https://docs.avaloniaui.net/docs/supported-platforms ; https://docs.avaloniaui.net/docs/app-development/accessibility
- **[S24] Microsoft Learn**, “Releases and support for .NET,” “Single-file deployment”: https://learn.microsoft.com/dotnet/core/releases-and-support ; https://learn.microsoft.com/dotnet/core/deploying/single-file/overview
- **[S25] Microsoft Learn**, Microsoft Information Protection SDK overview and supported file types: https://learn.microsoft.com/information-protection/develop/overview ; https://learn.microsoft.com/information-protection/develop/concept-supported-filetypes
- **[S26] OWASP**, “CSV Injection” (Excel の数式開始文字と再保存上の注意を含む): https://owasp.org/www-community/attacks/CSV_Injection
- **[S27] GitHub Docs**, “Troubleshooting network errors for GitHub Copilot,” “Configuring network settings”: https://docs.github.com/en/copilot/how-tos/troubleshoot-copilot/troubleshoot-network-errors ; https://docs.github.com/en/copilot/how-tos/configure-personal-settings/configure-network-settings
- **[S28] GitHub Docs**, “Copilot allowlist reference”: https://docs.github.com/en/copilot/reference/copilot-allowlist-reference
- **[S29] Personal Information Protection Commission, Japan**, “Laws and Policies — Act on the Protection of Personal Information”: https://www.ppc.go.jp/en/legal/ （法的効力を持つのは日本語原文である旨を同ページが明記）
- **[S30] GitHub Copilot SDK**, `.NET` supported portable RIDs: https://github.com/github/copilot-sdk/blob/main/dotnet/src/build/GitHub.Copilot.SDK.targets
- **[S31] GitHub Docs**, “Authorizing OAuth apps” — Device Flow、poll interval/error、expiring/refresh token、identity revalidation、認可確認/失効: https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow
- **[S32] Microsoft Support**, 「Excel の仕様と制限」— 1,048,576 行、16,384 列、32,767 文字/セル、8,192 文字/数式等: https://support.microsoft.com/office/excel-specifications-and-limits-1672b34d-7043-467e-8e27-269d656771c3
- **[S33] Microsoft Learn**, “Globalization and ICU” — app-local ICU の固定構成: https://learn.microsoft.com/dotnet/core/extensions/globalization-icu#app-local-icu

## 19. 要求トレーサビリティ要約

| リスク/目的 | 主要求 |
|---|---|
| 元データ不変・package 保全 | FR-001〜017、NFR-REL-001、自動テスト 1〜4 |
| 複数設問・行単位 | FR-020〜028、FR-045〜046 |
| 起動後のレポート種類・教員作成Prompt | FR-030〜039 |
| Copilot SDK・OAuth・非決定性 | FR-040〜058、NFR-SEC-001/003/009、NFR-REL-003/004/009 |
| 個人情報・ローカル優先 | FR-060〜065、NFR-PRI-* |
| 設問文コピー類似度 | FR-070〜076、S33 |
| Excel で自由に計算 | FR-080〜092 |
| 人の最終判断・説明 | FR-100〜106、NFR-FAI-* |
| Prompt/Formula Injection | FR-036、FR-047〜050、FR-087〜088、NFR-SEC-* |
| 状態の曖昧さ防止 | FR-057、FR-083〜086、第15節 |
| 性能・資源上限 | FR-006、FR-052〜053、NFR-REL-006〜008 |
| 第三者保証 | 第14.3節、実装開始ゲート 7〜8 |
| Windows/macOS/Linux | NFR-ACC-*、NFR 9.6、SET-* |
| 教員/開発者文書 | 13.1、13.2、Definition of Done |
| MIP 保護サンプル | 6.2、FR-005、EXT-001 |

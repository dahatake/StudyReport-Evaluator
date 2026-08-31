# StudyReport Evaluator 要求定義書

| 項目 | 内容 |
|---|---|
| 文書版 | 2.0 |
| 基準日 | 2026-08-31 |
| 状態 | 単一入力・ローカル実行版（実装開始前） |
| 唯一の利用者提供ファイル | Microsoft Forms等から出力した回答Excel（標準 `.xlsx`） |
| 初版対応環境 | Windows 11 x64 |
| UI / Runtime | Avalonia / .NET 10 |
| AI | GitHub Copilot SDK for .NET。既存のCopilot CLIログインを使用 |
| 旧版 | v1.2は本版により全面的にsupersede |

> 本版は、2026-08-31の要求所有者指示「Formsの回答のExcelのファイルだけで実行できるようにする」を反映する。機関policy、署名鍵、教育評価baseline、法務判断、cross-platform runner等を実装開始条件または利用者提供情報にしない。
>
> 未回答だった設計選択には、安全かつ実装可能な既定を採用した。AI点は候補点として人が確認する、送信前に内容を表示して確認する、初版はWindows 11 x64だけを対象とする、提示されたPrompt例は意味を保ちながら誤字と技術表現を校正する、という既定である。

## 1. 目的

Microsoft Forms等から出力した回答 `.xlsx` を読み込み、複数のレポート設問について、利用者が画面で自由に編集できる設問・論点・Promptを用いてGitHub Copilotへ評価を依頼し、定量的な候補点、論点別判定、理由、回答中の根拠を新しい `.xlsx` へ出力するWindowsデスクトップアプリケーションを提供する。

初版の目的は次の4点である。

1. 入力Excelを変更しない。
2. レポート本文を設問ごとに0〜30点で定量化する。
3. 任意の学生PromptおよびPrompt考慮事項を1〜10点で定量化する。
4. AI候補、根拠、人による確認結果を分離して出力する。

## 2. 提供情報と実行前提

### 2.1 利用者が提供する唯一の情報

利用者が本プロジェクトへ提供する必須情報は、次の1ファイルだけである。

- Microsoft Forms等の回答を含む、標準Office Open XML形式の非暗号化 `.xlsx`

ファイルには複数の回答行を含められる。アプリは見出しを読み取り、利用者が画面で列を割り当てる。具体的な設問、論点、Prompt、採点範囲は初期プリセットから開始でき、すべて画面で編集できるため、実装前に別文書として提供する必要はない。

### 2.2 利用者が提供しなくてよいもの

次は実装開始条件でも、開発者へ提供する情報でもない。

- GitHub OAuth App client ID、client secret、PAT、access token
- GitHub organization、Business/Enterprise承認記録、署名済み運用policy
- production署名鍵、certificate、notarization情報
- privacy／法務approval artifact
- 教育評価baseline、事前登録threshold、部分集団policy
- macOS／Linux／Arm64 runner
- local launcherの氏名、所属、role、本人確認
- 特定授業のPrompt、rubric、reason/evidence例

### 2.3 実行時の前提

AI評価を実行する端末では、GitHub Copilotを利用できるGitHub accountでCopilot CLIへログイン済みである必要がある。これは開発者へ提供する設定情報ではなく、利用者がGitHubの画面で直接行う対話的な認証である。

- SDKの標準動作で既存のlogged-in user credentialsを使用する。
- 未認証時はアプリが認証不足を表示し、Copilot CLIの対話的loginを案内または起動する。
- アプリはpassword、token、client secretを入力・保存・表示しない。
- 未認証でもExcel読込、列mapping、評価設定編集、過去結果確認は利用できる。

## 3. 初版scope

### 3.1 対象

- Windows 11 x64
- 1回答者または1提出を1行とする表形式 `.xlsx`
- 複数設問
- 設問ごとの必須レポート回答列
- 設問ごとの任意学生Prompt列
- 設問ごとの任意Prompt考慮事項列
- 既存Copilot CLI認証を利用した評価
- 新規 `.xlsx` への結果出力
- ローカルfolderから起動するself-contained build

### 3.2 初版の対象外

- macOS、Linux、Windows Arm64の正式対応
- installerのproduction code signing、Apple notarization、Linux package signing
- 機関policyをアプリが検証する仕組み
- 教育評価の統計的妥当性をproduction release条件にすること
- AI候補だけによる成績の自動確定
- Forms API、LMS API、回答の書戻し
- 暗号化、MIP、password保護ファイルの復号
- PDF、CSV、`.xls`、Google Sheets URLの直接入力
- 行単位印刷、AI文章検出、不正行為認定
- 設問文と学生Promptの類似度による減点

これらは将来追加できるが、初版実装をblockしない。

## 4. Excel入力契約

### 4.1 行と列

1. 既定では1行目を見出し、2行目以降を回答とする。
2. 1回答者または1提出を1行として扱う。
3. 見出し行と最終行は画面で変更できる。
4. 設問数は固定しない。
5. 各設問は次の列mappingを持つ。

| 列role | 必須 | 内容 |
|---|---:|---|
| `REPORT_ANSWER` | Yes | 設問に対する学生レポート本文 |
| `STUDENT_PROMPT` | No | 学生がレポート作成に使用したPrompt |
| `PROMPT_CONSIDERATIONS` | No | Prompt作成時に学生が意識した視点・論点・考慮事項 |
| `IDENTIFIER` | No | 氏名、メール、回答ID等。出力へ残せるがCopilotへ送らない |
| `IGNORE` | No | 評価にも出力追加列にも使用しない列 |

6. 同じExcel列を複数roleへ割り当てる場合は警告し、明示確認を要求する。
7. アプリは見出し文字列から候補を提示できるが、自動確定しない。
8. `STUDENT_PROMPT`と`PROMPT_CONSIDERATIONS`は独立して省略可能である。
9. `STUDENT_PROMPT`がなく`PROMPT_CONSIDERATIONS`だけがある場合は、実Promptの品質とは称さず、`CONSIDERATIONS_ONLY`として考慮事項の品質だけを評価する。
10. 両方空欄の場合、Prompt候補点は空欄、状態は`NOT_APPLICABLE`とする。

### 4.2 ファイル安全性

- 入力をread-onlyで開き、同一pathへ保存しない。
- 処理前後のSHA-256、size、last-write timeを比較する。
- 出力は別pathの一時ファイルへ保存し、再open・検証後に完成名へrenameする。
- ZIPではない `.xlsx`、破損ZIP、macro-enabled形式、暗号化compound fileは安全に拒否する。
- 入力本文をlogへ記録しない。
- 暗号化sampleの方式を推測したり、復号を試みたりしない。

## 5. 評価設定

### 5.1 設問設定

利用者は設問ごとに次を作成・編集・並べ替え・無効化できる。

- 設問IDと表示名
- 設問本文
- レポート評価Prompt
- レポート評価の視点・論点・観点
- レポート候補点の最小値・最大値（初期値0〜30）
- Prompt評価Prompt
- Prompt評価の視点・論点・観点
- Prompt候補点の最小値・最大値（初期値1〜10）
- 出力理由の最大長

設定はアプリ内で自由に変更でき、local launcherの本人性・role・approvalを要求しない。実行開始時に設定snapshotとSHA-256を作り、そのrun中は変更しない。

Prompt考慮事項が存在する場合は、それをPrompt能力評価の最優先基準とし、学生Promptがその考慮事項を実際に引き出せる構造になっているかを評価する。ただし、考慮事項に記載された内容が実際の学生Promptへ反映されているとは推測しない。この規則は初期Promptへ明記し、利用者はPrompt全文を自由に編集できる。独立したBoolean設定や非公開の数値weightは設けない。

`MET`または`PARTIAL`と判定する論点には根拠を必須とする。`NOT_FOUND`では根拠を空にし、存在しない引用を生成しない。この規則も独立した切替設定にせず、schemaとvalidatorで一貫して強制する。

### 5.2 共通placeholder

初版は次のplaceholderだけを許す。

- `{設問}`
- `{論点}`
- `{学生のレポート}`
- `{学生のPrompt}`
- `{学生のPrompt考慮事項}`
- `{最小点}`
- `{最大点}`

未知placeholder、未閉鎖brace、再帰展開は実行前に拒否する。学生入力をinstructionとして解釈せず、明示的な引用data区画へ挿入する。

## 6. 初期プリセット

以下は初期表示用の編集可能なプリセットであり、固定された正解や最終判定ではない。利用者は削除、追加、修正できる。

### 6.1 レポート評価Prompt

```text
次の設問に対する学生レポートを分析し、{最小点}〜{最大点}の範囲で候補点を付けてください。
各論点が説明されているかを主要な評価基準とし、論点ごとに「説明あり」「一部あり」「確認できず」を判定してください。
根拠のない内容を推測せず、根拠を示す場合は学生レポート内の短い連続した抜粋だけを使用してください。

### 設問
{設問}

### 論点
{論点}

### 学生レポート
{学生のレポート}
```

### 6.2 設問1プリセット

**設問**

ソフトウェアでの実装手段として、プログラミングと機械学習の違いを述べてください。なぜ機械学習という手法が必要になったのか、実現できることやテスト方法の違いなどを含めて説明してください。生成AIを使用した場合は、その結果を踏まえたレポートを記述してください。

**レポート評価の論点**

- 従来のプログラミングでは人が明示的なロジックを設計するため、ルール化が難しい複雑な課題に限界がある一方、機械学習はデータからパターンを学習できること。
- 一般的な整理として、ルールから結果を導くプログラミングは演繹的、例からモデルを学習する機械学習は帰納的な側面を持つこと。
- テストと品質評価の考え方が異なり、機械学習では一般に精度100%を前提にできないこと。
- 追加の考察や、自ら調査したことが分かる具体的な根拠があること。

### 6.3 設問2プリセット

**設問**

実世界（社会、企業、組織など）における機械学習またはAIの利用事例を1つ調査してください。どの場面で、どの技術が、どのように使われ、なぜ機械学習が選ばれたのかを説明してください。

**レポート評価の論点**

- 機械学習が社会またはビジネス上の課題解決へどう貢献したか。特に投資対効果に触れているか。
- 具体的な企業名または組織名があるか。
- 具体的な技術、ツール、製品名があるか。
- 追加の考察や、自ら調査したことが分かる具体的な根拠があるか。

### 6.4 Prompt能力評価Prompt

```text
次の設問に対して学生が作成したPromptと、その考慮事項を分析し、{最小点}〜{最大点}の範囲で候補点を付けてください。
そのPromptが、指定された視点・観点・論点をレポート生成時に引き出せるほど具体的か、論理的か、実行可能かを評価してください。
1は論理性、具体性、必要な視点が大きく不足する状態、10は論理的かつ具体的で、必要な視点を十分に引き出せる状態の初期アンカーです。
Prompt考慮事項が存在する場合は、それを最優先の評価基準とし、学生のPromptが各考慮事項を実際に引き出せる構造になっているかを評価してください。ただし、考慮事項に書かれただけで実際のPromptに反映されているとは推測しないでください。

### 視点・観点・論点
{論点}

### 設問
{設問}

### 学生のPrompt
{学生のPrompt}

### 学生のPrompt考慮事項
{学生のPrompt考慮事項}
```

設問1のPrompt評価では、設問1の論点を初期値として使用する。設問2および追加設問も、各設問の論点を初期値にできる。利用者はレポート評価用とPrompt評価用の論点を別々に編集できる。

## 7. AI評価契約

### 7.1 認証とclient

- GitHub Copilot SDK for .NETの固定versionを使用する。
- 既定のlogged-in user credentialsを使用し、アプリ固有OAuth Appを要求しない。
- 実行時に認証状態とGitHub loginを表示するが、local launcher本人との同一性を主張しない。
- token、password、credential valueをlogまたはExcelへ保存しない。

### 7.2 送信内容

各評価単位で送る内容は次だけとする。

- 設問本文
- 編集済み評価Prompt
- 評価論点
- 対象行のレポート本文
- mappedされている場合だけ、対象行の学生PromptとPrompt考慮事項

氏名、メール、回答ID、時刻、file path、他の回答行、他の設問回答を送らない。実行前に代表1件の送信previewを表示し、利用者の明示確認後だけ送信する。

### 7.3 toolと出力

Copilot sessionは評価用途に限定し、filesystem、shell、Web検索、GitHub write、MCPを公開しない。構造化出力toolは1件だけとする。

レポート評価結果:

- `overall_score`: 設定range内の有限数
- `criterion_results[]`: 論点ID、`MET` / `PARTIAL` / `NOT_FOUND`、短い説明、根拠、`evidence_source`
- `reason`: 総合理由

Prompt評価結果:

- `prompt_score`: 設定range内の有限数
- `mode`: `PROMPT` / `PROMPT_WITH_CONSIDERATIONS` / `CONSIDERATIONS_ONLY`
- `criterion_results[]`: 論点ID、判定、短い説明、根拠、`evidence_source`
- `reason`

`evidence_source`は`REPORT_ANSWER`、`STUDENT_PROMPT`、`PROMPT_CONSIDERATIONS`、`NONE`のclosed enumとする。レポート評価の根拠は、Copilotへ送った同じ行・同じ設問の`REPORT_ANSWER`内に存在する短い連続substringだけを受理する。Prompt評価の根拠は、同じ評価単位で実際に送った`STUDENT_PROMPT`または`PROMPT_CONSIDERATIONS`のうち、`evidence_source`が示すfield内に存在する短い連続substringだけを受理する。他行、他設問、送信していないfieldからの引用は拒否する。暗黙の正規化、翻訳、case-foldで一致を推測しない。

`MET`または`PARTIAL`では`evidence_source`を`NONE`以外にし、実在する非空根拠を必須とする。`NOT_FOUND`では根拠を空文字、`evidence_source=NONE`とする。不正JSON、未知field、欠落論点、範囲外、NaN／Infinity、過長文字列、根拠不一致、複数提出は失敗として扱い、0点へ変換しない。

### 7.4 sessionと再試行

- 1行×1設問×1評価種別を1評価単位とする。
- 評価単位ごとに新しいsessionを使用する。
- 構造不正は新sessionで最大1回再試行する。
- transient network errorは最大2回再試行する。
- cancel後に新規送信しない。
- sessionを明示削除し、削除失敗時は後続送信を停止する。
- 既定並列度は1、最大3とする。

## 8. 候補点と人の確認

1. AIが返す点数は常に`AI候補点`である。
2. AI候補だけで`確認済み点`や最終点を自動確定しない。
3. 利用者は設問ごとに`採用`、`上書き`、`保留`、`却下`を選べる。
4. `採用`時だけ候補点を確認済み点へ反映する。
5. `上書き`時は利用者入力値を反映し、AI候補を残す。
6. `保留`、`却下`、未実行、失敗では確認済み点を空欄にする。
7. Prompt列がない場合、Prompt候補点と確認済み点を空欄にする。

## 9. Excel出力契約

入力をbyte-copyした作業copyを基礎にし、アプリ所有sheetだけを追加する。入力workbook自体は変更しない。

### 9.1 必須sheet

| Sheet | 内容 |
|---|---|
| 元の全sheet | 入力から保持し、変更しない |
| `Evaluation_Config` | 設問、Prompt、論点、score range、設定snapshot hash |
| `Evaluation_Results` | 行ごとのAI候補、論点判定、理由、根拠、人の確認結果、状態 |
| `Evaluation_Run` | 入力hash、実行日時、app/SDK/CLI/model version、件数、状態。本文・tokenなし |

### 9.2 結果列

各設問について少なくとも次を出力する。

- レポートAI候補点
- レポート論点別判定
- レポート理由
- レポート根拠
- レポート確認状態
- レポート確認済み点
- Prompt評価mode
- Prompt AI候補点
- Prompt論点別判定
- Prompt理由
- Prompt確認状態
- Prompt確認済み点
- AI／validation error code

不信文字列は内容にかかわらずOpen XML string cellとして書き、formulaとして解釈させない。

## 10. Privacy・security

- 回答本文、学生Prompt、考慮事項、reason、evidence、tokenをlogへ記録しない。
- identifier列はCopilot payloadへ入れない。
- 回答本文内のメール、電話番号等を簡易検知し、previewで警告・伏字できるようにする。ただし完全検出とは表示しない。
- 利用者は送信前に、選択したfieldがGitHub Copilotへ送信されることを毎run確認する。
- アプリは利用者の法的権限や機関承認の有効性を判定しない。入力と送信の権限確認は利用者の責任として画面とREADMEに明記する。
- TLS検証を無効化しない。
- telemetryを可能な範囲で無効化し、アプリ独自telemetryを実装しない。
- workbook、Prompt、AI出力を不信入力として扱う。

外部privacy／legal／educational approval artifactはアプリの実装・起動条件にしない。この簡素化は法的助言や機関内手続の免除を意味しない。

## 11. UI要求

初版は次の7画面または同等のstep UIを持つ。

1. **開始**: 入力不変、AI候補、人の確認、データ送信の説明。
2. **ファイル選択**: `.xlsx`選択、hash、sheet、見出し、保護／破損判定。
3. **列mapping**: 設問追加、必須回答列、任意Prompt／考慮事項列、identifier／ignore列。
4. **評価設定**: プリセット選択、設問、Prompt、論点、点数rangeの自由編集。
5. **送信preview・実行確認**: 代表payload、除外列、件数、model、最大request数。
6. **実行・レビュー**: 進捗、cancel、候補点、論点、理由、根拠、採用／上書き／保留／却下。
7. **出力完了**: 入力hash不変、出力path、成功／失敗／要確認件数。

全操作をkeyboardで実行でき、状態を色だけで表さず、日本語で「何が起きたか」「入力への影響」「次の操作」を表示する。

## 12. 非機能要求

### 12.1 Reliability

- 取消、通信失敗、disk full、process異常で入力を変更しない。
- 完成前fileを完成名にしない。
- 同じ設定とmodelでもAI結果が一致するとは表示しない。
- UI threadでworkbook全走査やAI待機をしない。
- 20,000行を上限とし、超過は実行前に拒否する。
- 1 cellはExcel上限32,767文字以内とする。

### 12.2 Performance

この開発端末上の合成531行workbookについて、Copilot待機を除く読込、mapping解析、出力生成、再検証を30秒以内に完了することを目標とする。実測環境と結果を記録し、未測定値を保証として表示しない。

### 12.3 Platform

- Windows 11 x64のみを初版の検証済み対象とする。
- .NET 10 self-contained folder publishを生成する。
- 開発SDKなしで起動できることをlocal clean-user相当環境で確認する。
- code signing済みinstallerを初版必須成果物にしない。
- macOS、Linux、Arm64はsource上のportable性を妨げないが、実測なしに対応済みと表示しない。

## 13. Acceptance criteria

| ID | 条件 |
|---|---|
| AC-001 | 利用者が提供する必須情報は回答 `.xlsx` 1ファイルだけである。 |
| AC-002 | 1回答者1行、複数設問、必須回答列、任意Prompt／考慮事項列を画面でmappingできる。 |
| AC-003 | 提示された2設問と2種類のPromptが編集可能な初期プリセットとして表示される。 |
| AC-004 | レポートは既定0〜30、Prompt能力は既定1〜10の候補点として出力される。 |
| AC-005 | 各論点の判定、理由、検証済み根拠が候補点とともに出力される。 |
| AC-006 | Prompt／考慮事項がない行でもレポート評価を継続し、Prompt点を空欄にする。 |
| AC-007 | identifier列と他行データをCopilotへ送信せず、送信前previewを毎run確認する。 |
| AC-008 | AI候補だけで確認済み点を確定しない。 |
| AC-009 | 入力SHA-256、size、last-write timeが処理前後で一致する。 |
| AC-010 | 出力 `.xlsx` を一時保存、再open検証、atomic renameの順で確定する。 |
| AC-011 | 既存Copilot CLI認証で動作し、アプリ固有client ID、secret、組織policyを要求しない。 |
| AC-012 | Windows 11 x64でbuild、unit test、synthetic E2E、self-contained起動確認が成功する。 |

## 14. Test requirements

1. 1／2／10設問、列順変更、同名見出し、空列、途中空行のmapping test。
2. Prompt列なし、考慮事項列なし、両方なし、考慮事項だけのtest。
3. 初期プリセット内容、自由編集、snapshot/hash、run開始後不変test。
4. 0〜30、1〜10の境界、範囲外、NaN、欠落論点、未知field、複数提出test。
5. Prompt Injection、formula marker、長文、Unicode、改行を含む不信入力test。
6. identifier除外、回答本文内のemail／電話番号候補検知、warning、伏字preview、検知限界表示、回答本文を含まないlog test。
7. fake Copilot clientによる正常、schema不正、network失敗、cancel、cleanup失敗test。
8. 認証済み環境がある場合のsynthetic live smoke test。未認証環境ではskip可能で、build gateをblockしない。
9. Open XML package preservation、string cell、atomic output、disk full／rename失敗test。
10. Windows 11 x64のsynthetic end-to-end test。
11. keyboard navigation、focus、200% scaleのbasic accessibility test。
12. `src/`、test、docsのrequirement traceability検査。

## 15. Implementation start gate

GATE-0は次だけを要求する。

1. 本要求v2.0と対応実装計画v3.0がcommitされている。
2. 単一Excel入力contract、初期Promptプリセット、AI候補／人確認境界が明文化されている。
3. 合成fixture仕様が存在する。
4. production `src/` fileがgate判定前に0件である。
5. 独立した文書reviewでblocker/high defectが0件である。

機関policy、法務判断、教育評価baseline、署名、cross-platform実機、外部owner approvalはGATE-0条件ではない。GATE-0 PASS後は、実装計画v3.0を依存順に全て実行する。

## 16. Traceability summary

| Requirement surface | Primary implementation lane |
|---|---|
| Excel intake/mapping | Core + Workbooks + Desktop |
| Editable presets/config | Core + Desktop |
| Copilot auth/evaluation | Platform + Core |
| Candidate/review | Core + Desktop |
| XLSX output/preservation | Workbooks |
| Privacy/security | Core + Platform + tests |
| Windows build/E2E | Desktop + E2E |

## 17. Source note

- GitHub Copilot SDKの現行authentication contractでは、`.NET`の`new CopilotClient()`は既定でlogged-in user credentialsを使用し、未ログイン時はCopilot CLIの対話的loginが必要である。
- 旧v1.2のADR、signed policy、cross-platform、教育評価gate文書は設計参考としてGit履歴に残すが、本v2.0の初版completion条件ではない。
- 本書は法的助言ではない。実データを処理する権限と所属組織の規則確認は利用者が行う。

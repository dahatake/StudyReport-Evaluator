# E2E-002 — FR-002 / AC-055（202611032044）

期待値の正本: `rdcheck.py show FR-002` 全文、要求定義書 §9.1、§11.6、§11.8。
既存 E2E-002 を拡張し、FR-001/FR-003 のケースは変更しない。

| AC | 検証 |
|---|---|
| 共通のみ・旧入口・model/login存続 | 既存 `FR002_AC055_*` カテゴリ/入口テストを保持 |
| ① 単一・全文コピー・readonly | 旧実効欄不在、1欄、Ctrl+A/C、文字入力・Backspace・Ctrl+V不変 |
| ② 選択・保存・復元・入力変更 | real SettingsView の mouse/Enter → Avalonia OpenFolderPickerAsync、長いUnicode絶対path、明示保存、別インスタンス復元 |
| ③ 取消・失敗・非local | 空結果・IOException・HTTPS URI、理由表示、表示/override/保存bytes/FS inventory不変、編集fallbackなし |
| ④ reset/null/default | UI「既定に戻す」、null保存→再起動、input-a/b隣接result追従、未入力「入力後に決定」、算出先非保存 |
| ⑤ 実出力・invalid・snapshot/checkpoint | 表示とExecution.OutputDirectory一致、pending run中のUI選択/取消/resetでrequest/snapshot/予約final/partial不変 + 既存durable契約 |

`test_fr002_output_folder.py` はUIと既存Appテストの両方をbuild/discoverしてから
選択実行する。既存テストは期待値の逆生成に使わず、変更禁止の旧出力・run契約の
回帰検証にのみ再利用する。対象は実final workbook（明示/既定）、利用不可先拒否、
実partialと予約済みfinal/partialでの再開。新UIのrunテストはDIでrunをpendingにし、
実際のUI選択・取消・reset後にimmutable requestと予約済みpathを比較する。
重複台帳ケースは追加しない。選択/取消/復元のFS inventoryは設定・入力用test fixture
の初期作成後に比較し、アプリによるmkdir/file作成がないことを確認する。

## 組み込み面と限界

- `SettingsView.axaml/.cs`: 既存 `ExecutionOutputDirectory` を単一readonlyに保ち、
  旧 `SettingsEffectiveOutputDirectory` を除去。出力フォルダー選択・既定解除の
  visible/focusableなButtonと、フォルダーopen失敗/ローカルpath不可の理由表示を提供する。
  新Buttonは表示/automation名の「フォルダー」「選択/変更」「既定に戻す」で検索する。
  理由は`Output`と`Status`/`Message`を含むautomation IDのTextBlockで確認する。
  理由の文面は固定しない（例の組み込みID: `OutputFolderPickerStatusMessage`）。
- 選択は `TopLevel.StorageProvider.OpenFolderPickerAsync`、単一選択、
  local path取得を使用。テストはアプリの選択処理をmockしない。
- `ExecutionViewModel` / `SettingsViewModel` / `SettingsFileStore`:
  override/null draft・明示保存・復元、次回実効path、run immutable snapshotを維持。
  writer/planner/checkpointの予約path・再開契約を変更しない。
- Avalonia 12がlocator登録を非公開にしたため、test-only reflectionで
  scoped `IStorageProviderFactory` を差し替える（アプリ内部状態のreflectionはなし）。
  UI内のmouse/keyboardイベントとnative storage API呼出は自動検証するが、
  **OSネイティブdialog実表示・dialog内のmouse/keyboard操作・OS clipboard連携は
  headlessでは証明しない**。Windows製品構成で同操作の実機確認が必要。
- 現在run中のpicker操作はDIのpending runで検証するが、durable writerまでを
  1つのOS E2Eで通してはいない。既存DIによる実file/checkpoint契約を併用する。
- 台帳は `not_run`。preflightはpytest collection、C#は別途
  `dotnet test tests/system/ui --list-tests --filter FullyQualifiedName~FR002_AC055` で確認。

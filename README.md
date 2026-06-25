# ai-harness-baselib

ai-harness の**拡張プラグイン契約**を定義するライブラリ。プラグインはこの DLL を参照し、`PluginBase` を継承して実装する。

## 位置づけ

コア（`ai-harness-main`）と拡張（プラグイン）を分離する。依存は一方向。

```
ai-harness-main  ──┐
                   ├──▶  ai-harness-baselib
各プラグイン DLL  ──┘
```

- プラグインは baselib のみ参照。`ai-harness-main` は参照しない。
- プラグイン API の境界を越える型は全て baselib に置く（main に置くと依存が逆流するため）。

## 提供する型

| 型 | 役割 |
|---|---|
| `PluginBase` | 全プラグインの抽象基底 |
| `HookData` | hook の全フィールド統合モデル（欠落フィールドは `null`） |
| `PluginResult` | プラグイン1実行の結果ホルダ（`ExitCode` / `Reason`） |
| `LogEntry` / `LogLevel` | ログ専用型（レベル・メッセージ・source） |
| `HookEvent` | hook イベント種別の列挙 |

## プラグイン契約（`PluginBase`）

```csharp
public abstract class PluginBase
{
    // プラグインのキー。ログの source として使われ、claude（ハーネス由来）と区別する。
    public abstract string PluginName { get; }

    // ロード直後に1度。ログを yield で逐次返す。
    public abstract IEnumerable<LogEntry> Init();

    // hook 発火本体。ログを yield で逐次返し、列挙完了時に result が確定。
    public abstract IEnumerable<LogEntry> Action(HookData data, PluginResult result);

    // 設定ファイル読み込みヘルパ。
    public string ReadConfigFile(string configPath);
}
```

### 発火モデル

各プラグインは**発火条件を宣言**する。`ai-harness-main` は hook ごとに `ShouldFire` で対象を選別し、マッチしたプラグインのみ並列発火する。条件は以下の **OR**（いずれかにマッチで発火）。全条件 `null` のプラグインは発火しない。

| プロパティ | マッチ対象 | 形式 |
|---|---|---|
| `Tools` | `tool_name` | 完全一致。`"*"` で全ツール |
| `Events` | `hook_event_name` | 完全一致。`"*"` で全イベント |
| `FileNames` | file_path（`tool_input.file_path` 優先、無ければトップレベル `file_path`） | glob（`*`／`?`）。大文字小文字を無視 |
| `BashCommands` | `tool_input.command` | glob（`*`／`?`）。大文字小文字を区別 |

発火後はさらに `Action` 内で `HookData` を見て**自己フィルタ**でき、対象外なら `yield break`（`ExitCode` は 0 のまま＝許可）。

`Init` / `Action` は常駐しないプロセスのため、**hook 発火のたびに実行**される（「セッションで1回」ではない）。重い初期化は毎回コストになる点に注意。

### ログの返し方

`Init` / `Action` は `IEnumerable<LogEntry>` を **yield でログのストリーム**として返す（処理を止めずに逐次出力）。

`LogEntry` はレベルとメッセージを持つ。ファクトリで生成する。

```csharp
yield return LogEntry.Info("処理開始");
yield return LogEntry.Warning("想定外の入力");
yield return LogEntry.Error("失敗");
```

`source` はプラグインが設定しなくてよい。main が `PluginName` を打刻する。

`LogLevel` は `Trace < Debug < Info < Warning < Error`。main が `config/main.yml` の閾値以上のみ出力する。

### 結果の返し方（int / deny 先勝ち）

iterator は戻り型が固定で int を別途返せないため、結果は引数の `PluginResult` に書く。`Action` の最終 yield 後に `result.ExitCode` を設定すると、**列挙が完了した瞬間（`MoveNext()` が false を返すとき）に確定**する。

- `ExitCode` … `0` = 正常／許可、非 `0` = deny（ブロック）
- `Reason` … deny 時の理由

`ai-harness-main` は全プラグインの `PluginResult` を集約する。**1つでも非 0 なら全体 deny**（deny 先勝ち）。Claude Code への最終出力では非 0 を block（終了コード 2）へマップする。

### 実装例

```csharp
public sealed class BlockDangerousBashPlugin : PluginBase
{
    public override string PluginName => "BlockDangerousBash";

    public override IEnumerable<LogEntry> Init()
    {
        yield return LogEntry.Info("初期化");
    }

    public override IEnumerable<LogEntry> Action(HookData data, PluginResult result)
    {
        if (data.Event != HookEvent.PreToolUse || data.ToolName != "Bash")
            yield break;                       // 対象外 → ExitCode 0（許可）

        var command = data.ToolInput?["command"]?.GetValue<string>();
        yield return LogEntry.Debug($"検査: {command}");

        if (command?.Contains("rm -rf") == true)
        {
            yield return LogEntry.Warning("破壊的コマンドを検出。deny。");
            result.ExitCode = 2;               // 列挙完了時に確定
            result.Reason = $"破壊的コマンドをブロック: {command}";
        }
    }
}
```

## HookData

Claude Code が hook の stdin に渡す JSON を 1 型へ統合。当該イベントに存在しないフィールドは `null`。プラグインは `Event` や `ToolName` で必要なフィールドのみ参照する。未知フィールドは `[JsonExtensionData]` で前方互換に受ける。

- 共通: `SessionId` / `TranscriptPath` / `Cwd` / `PermissionMode` / `Effort` / `HookEventName` / `AgentId` / `AgentType`
- ツール系: `ToolName` / `ToolInput`(`JsonNode`) / `ToolOutput` / `PermissionType`
- その他イベント固有: `Prompt` / `Source` / `Trigger` / `StopHookActive` / `NotificationType` / `FilePath` ほか

ヘルパ:

- `HookData.Parse(string)` / `ParseAsync(Stream)` … stdin JSON → `HookData`（main が使用）
- `HookData.ToNonNullJson()` … 非 null フィールドのみの 1 行 JSON（ログ表示用）
- `HookData.ToNonNullJson(params string[] excludeKeys)` … 指定したトップレベルキー（`Extra` 展開後の未知キー含む）を除外して出力。巨大フィールドや会話本文をログから外す用途
- `HookData.Event` … `HookEventName` を `HookEvent` 列挙へ解釈（未知は null）

hook 仕様の典拠: code.claude.com/docs/en/hooks.md（2026-06 時点）。

## ビルド

```sh
dotnet build ai-harness-baselib/ai-harness-baselib/ai-harness-baselib.csproj
```

- ターゲット: net10.0 / nullable 有効 / 暗黙 usings 有効

## ディレクトリ

```
ai-harness-baselib/
├── README.md
└── ai-harness-baselib/
    ├── ai-harness-baselib.csproj
    ├── PluginBase.cs
    ├── HookData.cs
    ├── HookEvent.cs
    ├── PluginResult.cs
    ├── LogEntry.cs
    ├── LogLevel.cs
    └── Examples/
        └── BlockDangerousBashPlugin.cs   実装例
```

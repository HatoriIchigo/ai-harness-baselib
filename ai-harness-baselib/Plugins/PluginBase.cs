using System.IO.Enumeration;
using System.Text.Json.Nodes;

namespace ai_harness_baselib;

/// <summary>
/// 全拡張プラグインの基底クラス。各プラグインはこれを継承（オーバーライド）して実装する。
/// このアセンブリ（baselib）はオーバーライド契約のみを定義し、ロード・発火・集約は ai-harness-main が担う。
///
/// 動作モデル:
///   1. ai-harness-main が特定フォルダ内の全プラグイン DLL をロード。
///   2. hook 発火時、main は hook JSON を <see cref="HookData"/> へ組み立て、全プラグインを並列起動（上限あり）。
///   3. 各プラグインは <see cref="Action"/> 内で <see cref="HookData"/> を見て自己フィルタ。
///      無関係なら何も yield せず <see cref="PluginResult.ExitCode"/> を 0 のまま返す。
///   4. main が全プラグインの <see cref="PluginResult"/> を deny 先勝ちで集約し、Claude Code へ整形して返す。
/// </summary>
public abstract class PluginBase
{
    /// <summary>
    /// プラグインのキー。ログの source として使われ、claude（ハーネス由来）と区別する。
    /// 各プラグインで一意な値を override する。
    /// </summary>
    public abstract string PluginName { get; }

    /// <summary>
    /// プラグインが何を強制するかの 1 行説明。人間向けの一覧表示（<c>ai-harness-main --plugin</c>）に使う。
    /// 既定は空文字＝説明なし。ハーネスの動作には影響しない。
    /// </summary>
    public virtual string Description => "";

    /// <summary>
    /// このプラグインが対象とするツール名の配列。未使用なら <c>null</c>（既定）。
    /// hook の <c>tool_name</c> がこの配列に含まれるイベントで <see cref="Action"/> が発火する。
    /// 全ツールを対象にするには <c>"*"</c>（<see cref="ToolCatalog.Wildcard"/>）。
    /// 値は <see cref="ToolCatalog.ValidateTools"/> でロード時に検証され、不正なら当該プラグインは無効化される。
    /// <c>tool_name</c> を持たないイベント（UserPromptSubmit 等）は <see cref="Events"/> で対象指定する。
    /// </summary>
    public virtual IReadOnlyList<string>? Tools => null;

    /// <summary>
    /// このプラグインが対象とする hook イベント名の配列。未使用なら <c>null</c>（既定）。
    /// hook の <c>hook_event_name</c> がこの配列に含まれるとき <see cref="Action"/> が発火する。
    /// 全イベントを対象にするには <c>"*"</c>（<see cref="EventCatalog.Wildcard"/>）。
    /// 値は <see cref="EventCatalog.ValidateEvents"/> でロード時に検証される。
    /// <see cref="Tools"/> と併せて評価され、いずれかにマッチすれば発火（OR）。両方 null のプラグインは発火しない。
    /// </summary>
    public virtual IReadOnlyList<string>? Events => null;

    /// <summary>
    /// このプラグインが対象とするファイルパスのパターン配列。未使用なら <c>null</c>（既定）。
    /// hook の file_path（<c>tool_input.file_path</c> を優先、無ければトップレベル <c>file_path</c>）が
    /// いずれかのパターンに glob 一致するとき <see cref="Action"/> が発火する。
    /// パターンは <c>*</c>（任意長）と <c>?</c>（任意1文字）のワイルドカードを解釈（例: <c>"*.cs"</c>, <c>"src/*"</c>）。
    /// 全ファイル対象は <c>"*"</c>。大文字小文字は無視。
    /// </summary>
    public virtual IReadOnlyList<string>? FileNames => null;

    /// <summary>
    /// このプラグインが対象とする Bash コマンドのパターン配列。未使用なら <c>null</c>（既定）。
    /// hook の <c>tool_input.command</c> がいずれかのパターンに glob 一致するとき <see cref="Action"/> が発火する。
    /// パターンは <c>*</c>（任意長）と <c>?</c>（任意1文字）のワイルドカードを解釈（例: <c>"git push*"</c>, <c>"*rm *"</c>）。
    /// 全コマンド対象は <c>"*"</c>。大文字小文字は区別する。
    /// </summary>
    public virtual IReadOnlyList<string>? BashCommands => null;

    /// <summary>
    /// この hook データに対して発火すべきか。<see cref="Tools"/>（tool_name 完全一致）・
    /// <see cref="Events"/>（hook_event_name 完全一致）・<see cref="FileNames"/>（file_path glob）・
    /// <see cref="BashCommands"/>（command glob）の <b>OR</b>。各配列の <c>"*"</c> は全マッチ。
    /// null の系統は評価対象外。全系統 null のプラグインは発火しない。ai-harness-main が発火前に呼ぶ。
    /// </summary>
    public bool ShouldFire(HookData data) =>
        MatchesTool(data.ToolName)
        || MatchesEvent(data.HookEventName)
        || MatchesPattern(FileNames, ExtractFilePath(data), ignoreCase: true)
        || MatchesPattern(BashCommands, ExtractBashCommand(data), ignoreCase: false);

    private bool MatchesTool(string? toolName)
    {
        if (Tools is null)
        {
            return false;
        }
        if (Tools.Contains(ToolCatalog.Wildcard))
        {
            return true;
        }
        return !string.IsNullOrEmpty(toolName) && Tools.Contains(toolName);
    }

    private bool MatchesEvent(string? eventName)
    {
        if (Events is null)
        {
            return false;
        }
        if (Events.Contains(EventCatalog.Wildcard))
        {
            return true;
        }
        return !string.IsNullOrEmpty(eventName) && Events.Contains(eventName);
    }

    /// <summary>
    /// パターン配列に対する glob マッチ（<c>*</c>＝任意長 / <c>?</c>＝任意1文字）。
    /// null 配列は対象外（false）。<c>"*"</c> 単体を含む場合は値の有無に依らず全マッチ
    /// （<see cref="Tools"/>/<see cref="Events"/> の <c>"*"</c> と整合）。空配列はマッチなし。
    /// </summary>
    private static bool MatchesPattern(IReadOnlyList<string>? patterns, string? value, bool ignoreCase)
    {
        if (patterns is null)
        {
            return false;
        }
        if (patterns.Contains("*"))
        {
            return true;
        }
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        foreach (var pattern in patterns)
        {
            if (FileSystemName.MatchesSimpleExpression(pattern, value, ignoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>マッチ対象のファイルパス。tool_input.file_path 優先、無ければトップレベル file_path。</summary>
    private static string? ExtractFilePath(HookData data) =>
        AsString(GetMember(data.ToolInput, "file_path")) ?? data.FilePath;

    /// <summary>マッチ対象の Bash コマンド（tool_input.command）。</summary>
    private static string? ExtractBashCommand(HookData data) =>
        AsString(GetMember(data.ToolInput, "command"));

    /// <summary>JsonObject のメンバを安全に取得（オブジェクト以外・不在は null）。</summary>
    private static JsonNode? GetMember(JsonNode? node, string name) =>
        node is JsonObject obj && obj.TryGetPropertyValue(name, out var v) ? v : null;

    /// <summary>JsonNode が文字列値なら取り出す。型不一致・null は null。</summary>
    private static string? AsString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>
    /// ロード直後に 1 度だけ呼ばれる初期化。
    /// ログを <c>yield</c> で逐次返す（処理を止めずに出力させる）。
    /// </summary>
    public abstract IEnumerable<LogEntry> Init();

    /// <summary>
    /// hook 発火本体。main が全フィールドを含む <see cref="HookData"/>（欠落フィールドは null）を渡す。
    /// ログを <c>yield</c> で逐次返し、<b>列挙が完了した時点で</b> <paramref name="result"/> に判定が確定する。
    /// 列挙最後の yield の後に <c>result.ExitCode = ...</c> を書くこと（MoveNext が false を返す瞬間に実行される）。
    /// </summary>
    /// <param name="data">hook データ（全フィールド・欠落は null）。</param>
    /// <param name="result">列挙完了時に書き込む結果ホルダ。main が読む。</param>
    public abstract IEnumerable<LogEntry> Action(HookData data, PluginResult result);

    /// <summary>
    /// 能動スキャン本体。hook イベントには紐づかず、CLI（<c>ai-harness-main --fire</c>）から
    /// <b>手動で</b>起動される点が <see cref="Action"/> と異なる。<paramref name="projectRoot"/> 配下を
    /// 能動的に点検し、ログを <c>yield</c> で逐次返す。列挙が完了した時点で <paramref name="result"/> に
    /// 結果が確定する（<see cref="Action"/> と同じ規約。最後の yield の後に <c>result.ExitCode = ...</c> を書く）。
    ///
    /// <see cref="Action"/> のような <see cref="ShouldFire"/> フィルタは通らず、有効化されている（common.yml の
    /// tools で on の）プラグインが一律に呼ばれる。<see cref="LoadConfig"/> 済みで呼ばれるため
    /// <see cref="Config"/> を参照できる。hook のゲートではないため、ここでの非 0 <see cref="PluginResult.ExitCode"/>
    /// は何かをブロックするのではなく、スキャンの検出結果としてレポートに表示されるだけ。
    ///
    /// <paramref name="projectRoot"/> はスキャン対象のプロジェクトルート（絶対パス）。daemon は常駐ゆえ
    /// 各 hook/CLI プロセスの cwd を持たないため、走査対象は引数で受け取る（<see cref="Config"/> の所在と同様に
    /// プロジェクトごとに異なる）。
    ///
    /// 既定は何もしない（no-op）。スキャンを実装したいプラグインのみ override する。
    /// </summary>
    /// <param name="projectRoot">スキャン対象のプロジェクトルート（絶対パス）。</param>
    /// <param name="result">列挙完了時に書き込む結果ホルダ。main が読み、レポートへ整形する。</param>
    public virtual IEnumerable<LogEntry> Fire(string projectRoot, PluginResult result)
    {
        yield break;
    }

    /// <summary>
    /// このプラグインの設定ファイル名。<b>必須</b>。プロジェクトの設定ディレクトリ
    /// （<c>&lt;プロジェクトルート&gt;/.claude/harness/config</c>）からの相対名。
    /// 未設定（null/空）の場合 <see cref="LoadConfig"/> がエラーを投げ、ai-harness-main は当該プラグインを無効化する。
    /// </summary>
    public virtual string? ConfigName => null;

    private IReadOnlyDictionary<string, object>? _config;

    /// <summary>
    /// <see cref="LoadConfig"/> でロード済みの設定（YAML をパースしたマッピング）。未ロードで参照すると例外。
    /// 値はスカラ＝<c>string</c>、ネストマップ＝<c>Dictionary&lt;object, object&gt;</c>、配列＝<c>List&lt;object&gt;</c>
    /// （YamlDotNet の既定デシリアライズ）。プラグインは標準型のみ参照し YamlDotNet には依存しない。
    /// </summary>
    protected IReadOnlyDictionary<string, object> Config =>
        _config ?? throw new InvalidOperationException(
            $"{PluginName}: 設定が未ロード。LoadConfig の呼び出しが先に必要。");

    /// <summary>
    /// <see cref="ConfigName"/> が指す YAML 設定ファイルを、<paramref name="configDir"/>（プロジェクト個別の
    /// 設定ディレクトリ <c>&lt;プロジェクトルート&gt;/.claude/harness/config</c>）からロード・パースし、内部変数に保持する。
    /// ConfigName が未設定（null/空）の場合はエラー（必須）。ファイル不在も例外。空ファイルは空マッピング。
    /// ai-harness-main がプラグインのインスタンス生成直後（Init / Action の前）に呼ぶ。
    /// 単一 daemon が複数プロジェクトをさばくため、設定の所在は実行体ではなくプロジェクトごとに異なる。
    /// </summary>
    /// <param name="configDir">プロジェクトの設定ディレクトリ（絶対パス）。</param>
    public void LoadConfig(string configDir)
    {
        if (string.IsNullOrWhiteSpace(ConfigName))
        {
            throw new InvalidOperationException(
                $"{PluginName}: ConfigName が未設定。設定ファイル名の宣言は必須。");
        }
        var path = Path.Combine(configDir, ConfigName);
        var text = ReadConfigFile(path);
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
        // 空ファイル/コメントのみは null デシリアライズ → 空マッピングへフォールバック。
        _config = deserializer.Deserialize<Dictionary<string, object>>(text)
                  ?? new Dictionary<string, object>();
    }

    /// <summary>プラグインが自身の設定ファイルを読む共通ヘルパ。</summary>
    public string ReadConfigFile(string configPath) => File.ReadAllText(configPath);
}

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
    /// この hook データに対して発火すべきか。<see cref="Tools"/>（tool_name マッチ）と
    /// <see cref="Events"/>（hook_event_name マッチ）の <b>OR</b>。各配列の <c>"*"</c> は全マッチ。
    /// null の系統は評価対象外。ai-harness-main が発火前に呼ぶ。
    /// </summary>
    public bool ShouldFire(HookData data) =>
        MatchesTool(data.ToolName) || MatchesEvent(data.HookEventName);

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
    /// このプラグインの設定ファイル名。<b>必須</b>。設定ディレクトリ（<c>&lt;実行体&gt;/config</c>）からの相対名。
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
    /// <see cref="ConfigName"/> が指す YAML 設定ファイルを設定ディレクトリからロード・パースし、内部変数に保持する。
    /// ConfigName が未設定（null/空）の場合はエラー（必須）。ファイル不在も例外。空ファイルは空マッピング。
    /// ai-harness-main がプラグインのインスタンス生成直後（Init / Action の前）に呼ぶ。
    /// </summary>
    public void LoadConfig()
    {
        if (string.IsNullOrWhiteSpace(ConfigName))
        {
            throw new InvalidOperationException(
                $"{PluginName}: ConfigName が未設定。設定ファイル名の宣言は必須。");
        }
        var path = Path.Combine(AppContext.BaseDirectory, "config", ConfigName);
        var text = ReadConfigFile(path);
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
        // 空ファイル/コメントのみは null デシリアライズ → 空マッピングへフォールバック。
        _config = deserializer.Deserialize<Dictionary<string, object>>(text)
                  ?? new Dictionary<string, object>();
    }

    /// <summary>プラグインが自身の設定ファイルを読む共通ヘルパ。</summary>
    public string ReadConfigFile(string configPath) => File.ReadAllText(configPath);
}

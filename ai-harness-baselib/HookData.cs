using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ai_harness_baselib;

/// <summary>
/// effort レベル。Claude Code から <c>{ "level": "..." }</c> 形式で渡る。
/// </summary>
public sealed class Effort
{
    [JsonPropertyName("level")]
    public string? Level { get; init; }
}

/// <summary>
/// Claude Code が hook の stdin に渡す JSON を表す統合モデル。
/// 全イベントのフィールドを 1 つの型に集約し、当該イベントに存在しないフィールドは <c>null</c>。
/// プラグインは <see cref="Event"/> や <see cref="ToolName"/> を見て自己フィルタし、必要なフィールドのみ参照する。
///
/// 型定義は baselib に置く（プラグイン API の引数型 = 境界を越える型のため）。
/// JSON → 本型の組み立て（デシリアライズ）は ai-harness-main が <see cref="Parse"/> 等で行う。
/// </summary>
public sealed class HookData
{
    // ---- 共通フィールド（全イベント） ----

    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    [JsonPropertyName("transcript_path")]
    public string? TranscriptPath { get; init; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    [JsonPropertyName("permission_mode")]
    public string? PermissionMode { get; init; }

    [JsonPropertyName("effort")]
    public Effort? Effort { get; init; }

    /// <summary>Claude Code が渡す生のイベント名文字列。未知の値もそのまま保持。</summary>
    [JsonPropertyName("hook_event_name")]
    public string? HookEventName { get; init; }

    [JsonPropertyName("agent_id")]
    public string? AgentId { get; init; }

    [JsonPropertyName("agent_type")]
    public string? AgentType { get; init; }

    // ---- ツール系（PreToolUse / PostToolUse / *Failure / PermissionRequest / PermissionDenied） ----

    [JsonPropertyName("tool_name")]
    public string? ToolName { get; init; }

    /// <summary>ツールごとに構造が異なるため生 JSON ノードとして保持。</summary>
    [JsonPropertyName("tool_input")]
    public JsonNode? ToolInput { get; init; }

    [JsonPropertyName("tool_output")]
    public string? ToolOutput { get; init; }

    [JsonPropertyName("permission_type")]
    public string? PermissionType { get; init; }

    // ---- プロンプト系 ----

    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    [JsonPropertyName("command_name")]
    public string? CommandName { get; init; }

    [JsonPropertyName("expanded_prompt")]
    public string? ExpandedPrompt { get; init; }

    // ---- セッション / コンパクション / 設定の source・trigger 系 ----

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("trigger")]
    public string? Trigger { get; init; }

    [JsonPropertyName("load_reason")]
    public string? LoadReason { get; init; }

    // ---- Stop / StopFailure ----

    [JsonPropertyName("stop_hook_active")]
    public bool? StopHookActive { get; init; }

    [JsonPropertyName("error_type")]
    public string? ErrorType { get; init; }

    // ---- Notification ----

    [JsonPropertyName("notification_type")]
    public string? NotificationType { get; init; }

    // ---- Subagent ----

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    // ---- Cwd / File 変更 ----

    [JsonPropertyName("old_cwd")]
    public string? OldCwd { get; init; }

    [JsonPropertyName("new_cwd")]
    public string? NewCwd { get; init; }

    [JsonPropertyName("file_path")]
    public string? FilePath { get; init; }

    [JsonPropertyName("change_type")]
    public string? ChangeType { get; init; }

    // ---- MCP elicitation ----

    [JsonPropertyName("mcp_server")]
    public string? McpServer { get; init; }

    [JsonPropertyName("elicitation_data")]
    public JsonNode? ElicitationData { get; init; }

    // ---- Worktree ----

    [JsonPropertyName("base_branch")]
    public string? BaseBranch { get; init; }

    [JsonPropertyName("worktree_path")]
    public string? WorktreePath { get; init; }

    // ---- MessageDisplay / Task / Teammate ----

    [JsonPropertyName("message_type")]
    public string? MessageType { get; init; }

    [JsonPropertyName("message_text")]
    public string? MessageText { get; init; }

    [JsonPropertyName("task_description")]
    public string? TaskDescription { get; init; }

    [JsonPropertyName("teammate_id")]
    public string? TeammateId { get; init; }

    /// <summary>
    /// 上記プロパティに明示マップされなかった未知フィールド。
    /// Claude Code のイベント追加に前方互換で追従するための受け皿。
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>
    /// <see cref="HookEventName"/> を列挙型へ解釈。未知・未設定なら <c>null</c>。
    /// </summary>
    [JsonIgnore]
    public HookEvent? Event =>
        System.Enum.TryParse<HookEvent>(HookEventName, ignoreCase: false, out var e) ? e : null;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions NonNullSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    /// <summary>
    /// 非 null フィールドのみを 1 行 JSON で出力する。null フィールドはキーごと省略。
    /// ログ表示用（「null以外は全て表示」）。
    /// </summary>
    public string ToNonNullJson() => JsonSerializer.Serialize(this, NonNullSerializerOptions);

    /// <summary>
    /// <see cref="ToNonNullJson()"/> から指定したトップレベルキーを除外して出力する。
    /// <paramref name="excludeKeys"/> には JSON キー名（<c>tool_input</c> 等。<see cref="Extra"/> 展開後の
    /// 未知キー <c>tool_response</c> も対象）を渡す。巨大フィールドをログから外す用途。
    /// </summary>
    public string ToNonNullJson(params string[] excludeKeys)
    {
        if (excludeKeys is null || excludeKeys.Length == 0)
        {
            return ToNonNullJson();
        }
        if (JsonSerializer.SerializeToNode(this, NonNullSerializerOptions) is not JsonObject obj)
        {
            return ToNonNullJson();
        }
        foreach (var key in excludeKeys)
        {
            obj.Remove(key);
        }
        return obj.ToJsonString(NonNullSerializerOptions);
    }

    /// <summary>stdin から読んだ JSON 文字列を <see cref="HookData"/> へデシリアライズ。</summary>
    public static HookData Parse(string json) =>
        JsonSerializer.Deserialize<HookData>(json, SerializerOptions)
        ?? throw new JsonException("hook データの JSON が null にデシリアライズされた。");

    /// <summary>stdin ストリームから直接読み取り。</summary>
    public static async Task<HookData> ParseAsync(Stream stdin, CancellationToken ct = default)
    {
        var data = await JsonSerializer.DeserializeAsync<HookData>(stdin, SerializerOptions, ct)
                     .ConfigureAwait(false);
        return data ?? throw new JsonException("hook データの JSON が null にデシリアライズされた。");
    }
}

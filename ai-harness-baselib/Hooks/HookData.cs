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
    /// <summary>effort レベル名（<c>"low"</c> / <c>"medium"</c> / <c>"high"</c> 等）。</summary>
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

    /// <summary>セッション識別子。</summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>会話トランスクリプト（JSONL）の絶対パス。</summary>
    [JsonPropertyName("transcript_path")]
    public string? TranscriptPath { get; init; }

    /// <summary>hook 実行時のカレントディレクトリ。</summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    /// <summary>現在の権限モード（<c>"default"</c> / <c>"plan"</c> / <c>"acceptEdits"</c> 等）。</summary>
    [JsonPropertyName("permission_mode")]
    public string? PermissionMode { get; init; }

    /// <summary>effort 設定（レベルを内包）。</summary>
    [JsonPropertyName("effort")]
    public Effort? Effort { get; init; }

    /// <summary>Claude Code が渡す生のイベント名文字列。未知の値もそのまま保持。</summary>
    [JsonPropertyName("hook_event_name")]
    public string? HookEventName { get; init; }

    /// <summary>発火元エージェントの ID（サブエージェント発火時）。</summary>
    [JsonPropertyName("agent_id")]
    public string? AgentId { get; init; }

    /// <summary>発火元エージェントの種別。</summary>
    [JsonPropertyName("agent_type")]
    public string? AgentType { get; init; }

    // ---- ツール系（PreToolUse / PostToolUse / *Failure / PermissionRequest / PermissionDenied） ----

    /// <summary>対象ツール名（PreToolUse / PostToolUse 等）。</summary>
    [JsonPropertyName("tool_name")]
    public string? ToolName { get; init; }

    /// <summary>ツールごとに構造が異なるため生 JSON ノードとして保持。</summary>
    [JsonPropertyName("tool_input")]
    public JsonNode? ToolInput { get; init; }

    /// <summary>ツールの出力（PostToolUse 等）。文字列化された結果。</summary>
    [JsonPropertyName("tool_output")]
    public string? ToolOutput { get; init; }

    /// <summary>権限イベントの種別（PermissionRequest / PermissionDenied 等）。</summary>
    [JsonPropertyName("permission_type")]
    public string? PermissionType { get; init; }

    // ---- プロンプト系 ----

    /// <summary>ユーザーが送信したプロンプト（UserPromptSubmit）。</summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    /// <summary>スラッシュコマンド名（該当時）。</summary>
    [JsonPropertyName("command_name")]
    public string? CommandName { get; init; }

    /// <summary>展開後のプロンプト（UserPromptExpansion）。</summary>
    [JsonPropertyName("expanded_prompt")]
    public string? ExpandedPrompt { get; init; }

    // ---- セッション / コンパクション / 設定の source・trigger 系 ----

    /// <summary>イベントの発生源（SessionStart の <c>"startup"</c> / <c>"resume"</c> 等）。</summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>トリガ種別（PreCompact の <c>"manual"</c> / <c>"auto"</c> 等）。</summary>
    [JsonPropertyName("trigger")]
    public string? Trigger { get; init; }

    /// <summary>読み込み理由（InstructionsLoaded 等）。</summary>
    [JsonPropertyName("load_reason")]
    public string? LoadReason { get; init; }

    // ---- Stop / StopFailure ----

    /// <summary>Stop hook が既に稼働中か（無限ループ防止のフラグ）。</summary>
    [JsonPropertyName("stop_hook_active")]
    public bool? StopHookActive { get; init; }

    /// <summary>失敗イベントのエラー種別（StopFailure 等）。</summary>
    [JsonPropertyName("error_type")]
    public string? ErrorType { get; init; }

    // ---- Notification ----

    /// <summary>通知種別（Notification）。</summary>
    [JsonPropertyName("notification_type")]
    public string? NotificationType { get; init; }

    // ---- Subagent ----

    /// <summary>停止理由（SubagentStop 等）。</summary>
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    // ---- Cwd / File 変更 ----

    /// <summary>変更前のカレントディレクトリ（CwdChanged）。</summary>
    [JsonPropertyName("old_cwd")]
    public string? OldCwd { get; init; }

    /// <summary>変更後のカレントディレクトリ（CwdChanged）。</summary>
    [JsonPropertyName("new_cwd")]
    public string? NewCwd { get; init; }

    /// <summary>対象ファイルパス（FileChanged 等・トップレベル）。</summary>
    [JsonPropertyName("file_path")]
    public string? FilePath { get; init; }

    /// <summary>ファイル変更の種別（<c>"created"</c> / <c>"modified"</c> / <c>"deleted"</c> 等）。</summary>
    [JsonPropertyName("change_type")]
    public string? ChangeType { get; init; }

    // ---- MCP elicitation ----

    /// <summary>対象 MCP サーバ名（Elicitation）。</summary>
    [JsonPropertyName("mcp_server")]
    public string? McpServer { get; init; }

    /// <summary>elicitation の生データ（構造はサーバ依存のため JSON ノードで保持）。</summary>
    [JsonPropertyName("elicitation_data")]
    public JsonNode? ElicitationData { get; init; }

    // ---- Worktree ----

    /// <summary>worktree のベースブランチ（WorktreeCreate）。</summary>
    [JsonPropertyName("base_branch")]
    public string? BaseBranch { get; init; }

    /// <summary>worktree のパス（WorktreeCreate / WorktreeRemove）。</summary>
    [JsonPropertyName("worktree_path")]
    public string? WorktreePath { get; init; }

    // ---- MessageDisplay / Task / Teammate ----

    /// <summary>表示メッセージの種別（MessageDisplay）。</summary>
    [JsonPropertyName("message_type")]
    public string? MessageType { get; init; }

    /// <summary>表示メッセージの本文（MessageDisplay）。</summary>
    [JsonPropertyName("message_text")]
    public string? MessageText { get; init; }

    /// <summary>タスクの説明（TaskCreated / TaskCompleted 等）。</summary>
    [JsonPropertyName("task_description")]
    public string? TaskDescription { get; init; }

    /// <summary>対象 teammate の ID（TeammateIdle）。</summary>
    [JsonPropertyName("teammate_id")]
    public string? TeammateId { get; init; }

    /// <summary>
    /// 上記プロパティに明示マップされなかった未知フィールド。
    /// Claude Code のイベント追加に前方互換で追従するための受け皿。
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    // ---- ハーネス内部 state（hook JSON 外・main が注入） ----

    /// <summary>
    /// ai-harness-main が保持する永続 state（<c>&lt;projectRoot&gt;/.claude/harness/state.json</c>）の全体。
    /// hook の stdin JSON には含まれず、main が発火直前に注入する（<see cref="JsonIgnoreAttribute"/> で入出力から除外）。
    /// プラグインは規約として自分の <see cref="PluginBase.PluginName"/> をトップレベルキーとする配下を参照する
    /// （全体を読めるので他プラグインの state も参照可）。
    /// <b>共有参照のため書き換えないこと</b>（並列発火で競合する）。state の変更は
    /// <see cref="PluginResult.State"/> に自分の新しいスライスを返して行う。
    /// </summary>
    [JsonIgnore]
    public JsonNode? State { get; set; }

    /// <summary>
    /// LSP サーバの状態スナップショット（言語名 → <see cref="LspLanguageState"/>）。
    /// hook の stdin JSON には含まれず、main が発火直前に注入する（<see cref="JsonIgnoreAttribute"/> で入出力から除外）。
    /// キーは <c>common.yml</c> の <c>lsp:</c> に列挙され、かつ既に一度でもインストール・起動が試みられた言語のみ
    /// （未対応言語や、まだ一度もトリガーされていない言語はキーごと存在しない）。
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, LspLanguageState>? Lsp { get; set; }

    /// <summary>
    /// LSP の診断キャッシュ（絶対ファイルパス → 診断一覧）。hook の stdin JSON には含まれず、
    /// main が発火直前に注入する（<see cref="JsonIgnoreAttribute"/> で入出力から除外）。
    /// main がファイル変更のたびに裏で LSP へ通知し、サーバから非同期に届く
    /// <c>textDocument/publishDiagnostics</c> をキャッシュしたもののスナップショット。
    /// <b>編集直後の hook では、まだサーバの解析が終わっておらず古い（または無い）診断しか無いことがある</b>
    /// （サーバ側の完了を待ってブロックすることはしない）。診断が無いファイルはキーごと存在しない。
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, IReadOnlyList<LspDiagnostic>>? LspDiagnostics { get; set; }

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

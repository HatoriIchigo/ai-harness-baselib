namespace ai_harness_baselib;

/// <summary>
/// プラグインが宣言する <see cref="PluginBase.Tools"/> / <see cref="PluginBase.Events"/> の検証結果。
/// </summary>
/// <param name="IsValid">全要素が有効なら true。</param>
/// <param name="Errors">無効理由（複数）。<see cref="IsValid"/> が true のとき空。</param>
public sealed record FilterValidation(bool IsValid, IReadOnlyList<string> Errors)
{
    /// <summary>検証成功（エラーなし）。null 宣言（フィルタ無視）もこれを返す。</summary>
    public static FilterValidation Ok { get; } = new(true, Array.Empty<string>());
}

/// <summary>
/// Claude Code 組み込みツール名のカタログと、プラグインの <see cref="PluginBase.Tools"/> 宣言の検証。
/// 「正しいツール名を宣言しているか」の検証は baselib の責務（プラグイン契約の一部）。
/// ai-harness-main はロード時に <see cref="ValidateTools"/> を呼び、不正なプラグインを除外する。
/// </summary>
public static class ToolCatalog
{
    /// <summary>全ツールにマッチするワイルドカード。個別ツール名との併用は不可。</summary>
    public const string Wildcard = "*";

    /// <summary>MCP ツール名の接頭辞（<c>mcp__&lt;server&gt;__&lt;tool&gt;</c> 形式）。</summary>
    public const string McpPrefix = "mcp__";

    /// <summary>
    /// Claude Code 組み込みツール名。hook の <c>tool_name</c> と一致する。
    /// 出典: code.claude.com/docs/en/hooks.md（2026-06 時点）。ツール追加時は要更新。
    /// MCP ツールは <see cref="McpPrefix"/> パターンで別途許容するため列挙しない。
    /// </summary>
    public static readonly IReadOnlySet<string> KnownTools = new HashSet<string>(StringComparer.Ordinal)
    {
        "Task", "Bash", "BashOutput", "KillShell",
        "Glob", "Grep", "Read", "Edit", "Write", "NotebookEdit",
        "WebFetch", "WebSearch", "TodoWrite", "ExitPlanMode", "SlashCommand",
    };

    /// <summary>
    /// 単一ツール名が有効か。ワイルドカード <c>*</c>／既知の組み込みツール／<c>mcp__</c> パターンを許容。
    /// </summary>
    public static bool IsValidTool(string tool) =>
        tool == Wildcard
        || KnownTools.Contains(tool)
        || (tool.StartsWith(McpPrefix, StringComparison.Ordinal) && tool.Length > McpPrefix.Length);

    /// <summary>
    /// プラグインの <see cref="PluginBase.Tools"/> 宣言を検証する。
    /// <c>null</c>＝ツールフィルタ未使用として無視（成功扱い）。空配列・空白・重複・未知ツール名・
    /// ワイルドカードと個別名の併用は不正とする。
    /// </summary>
    public static FilterValidation ValidateTools(IReadOnlyList<string>? tools)
    {
        if (tools is null)
        {
            return FilterValidation.Ok; // 未宣言＝無視
        }

        var errors = new List<string>();
        if (tools.Count == 0)
        {
            errors.Add("Tools が空配列。ツールフィルタ未使用なら null にすること。");
            return new FilterValidation(false, errors);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in tools)
        {
            if (string.IsNullOrWhiteSpace(tool))
            {
                errors.Add("空または空白のみのツール名は不可。");
                continue;
            }
            if (tool != tool.Trim())
            {
                errors.Add($"前後に空白を含むツール名は不可: '{tool}'");
            }
            if (!seen.Add(tool))
            {
                errors.Add($"重複したツール名: '{tool}'");
            }
            if (!IsValidTool(tool))
            {
                errors.Add($"未知のツール名: '{tool}'（組み込み一覧にも mcp__ パターンにも該当しない）");
            }
        }

        if (tools.Contains(Wildcard) && tools.Count > 1)
        {
            errors.Add("\"*\"（全ツール）と個別ツール名は併用不可。");
        }

        return errors.Count == 0 ? FilterValidation.Ok : new FilterValidation(false, errors);
    }
}

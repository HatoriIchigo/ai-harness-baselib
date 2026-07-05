namespace ai_harness_baselib.Examples;

/// <summary>
/// 実装例: Bash の破壊的コマンド（rm -rf 等）を PreToolUse で deny するプラグイン。
/// yield でログを逐次返し、列挙完了時に <see cref="PluginResult.ExitCode"/> を確定させる形を示す。
/// 実運用ではプラグイン側 DLL に置く。
/// </summary>
public sealed class BlockDangerousBashPlugin : PluginBase
{
    /// <inheritdoc/>
    public override string PluginName => "BlockDangerousBash";

    /// <summary>Bash のみ対象。tool_name=Bash の PreToolUse だけ発火する。</summary>
    public override IReadOnlyList<string> Tools => new[] { "Bash" };

    /// <inheritdoc/>
    public override IEnumerable<LogEntry> Init()
    {
        yield return LogEntry.Info("BlockDangerousBashPlugin 初期化");
    }

    /// <inheritdoc/>
    public override IEnumerable<LogEntry> Action(HookData data, PluginResult result)
    {
        // 自己フィルタ: 対象外イベント／ツールなら何もせず正常終了。
        if (data.Event != HookEvent.PreToolUse || data.ToolName != "Bash")
        {
            yield break; // ExitCode は 0 のまま（許可）
        }

        var command = data.ToolInput?["command"]?.GetValue<string>();
        yield return LogEntry.Debug($"検査: {command}");

        if (command is not null && IsDestructive(command))
        {
            yield return LogEntry.Warning("破壊的コマンドを検出。deny。");
            // 列挙完了時に確定。main が deny 先勝ちで集約。
            result.ExitCode = 2;
            result.Reason = $"破壊的コマンドをブロック: {command}";
            yield break;
        }

        yield return LogEntry.Debug("問題なし。");
        // ここに到達 = ExitCode 0（許可）のまま終了。
    }

    private static bool IsDestructive(string command) =>
        command.Contains("rm -rf", StringComparison.Ordinal) ||
        command.Contains("rm -fr", StringComparison.Ordinal);
}

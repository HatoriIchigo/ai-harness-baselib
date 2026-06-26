namespace ai_harness_baselib;

/// <summary>
/// プラグイン 1 実行の結果ホルダ。
/// <see cref="PluginBase.Action"/> のログ列挙が完了した時点で値が確定する。
/// </summary>
public sealed class PluginResult
{
    /// <summary>
    /// 終了コード。<c>0</c> = 正常／許可。<c>0 以外</c> = deny（ブロック）。
    /// main は全プラグインの結果を <b>deny 先勝ち</b>（1 つでも非 0 なら全体 deny）で集約する。
    /// Claude Code への最終出力では非 0 を block（終了コード 2 相当）へマップする。
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>deny 時などに判定へ添える理由。main が集約して Claude へ渡す。</summary>
    public string? Reason { get; set; }

    /// <summary>正常（許可）を表す既定状態か。</summary>
    public bool IsOk => ExitCode == 0;
}

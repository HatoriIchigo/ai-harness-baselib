using System.Text.Json.Nodes;

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

    /// <summary>
    /// Claude のコンテキストへ注入する追加テキスト（非ブロック）。<see cref="ExitCode"/> を 0（許可）に
    /// 保ったまま添えられる。<see cref="Reason"/>（deny 理由）とは独立した系統。
    /// main は全プラグインのこの値を連結し、client が Claude Code の hook 出力
    /// （<c>hookSpecificOutput.additionalContext</c>）へマップする。PreToolUse では
    /// <c>permissionDecision=allow</c> と併せて出力され、ツール実行をブロックせずに文脈へ反映される。
    /// 注入テキストが Claude に見えるのはツール実行後（次のモデル呼び出し）である点に注意。
    /// </summary>
    public string? AdditionalContext { get; set; }

    /// <summary>
    /// このプラグインが返す新しい state スライス。<c>null</c>（既定）＝変更なし。
    /// main はこの値を <c>state.json</c> のトップレベル <see cref="PluginBase.PluginName"/> キー配下へ
    /// 上書きし、state 全体に差分があれば書き戻す。
    /// 規約として各プラグインは<b>自分の名前空間のみ</b>を返す（返した値がそのキー配下を丸ごと置換する）。
    /// <see cref="HookData.State"/>（共有参照）を直接書き換えず、新しいノードを生成して返すこと。
    /// </summary>
    public JsonNode? State { get; set; }

    /// <summary>正常（許可）を表す既定状態か。</summary>
    public bool IsOk => ExitCode == 0;
}

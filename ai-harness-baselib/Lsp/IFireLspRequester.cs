namespace ai_harness_baselib;

/// <summary>
/// <see cref="PluginBase.Fire"/>（能動スキャン）専用の LSP 診断リクエスタ。host が <c>Fire</c> 呼び出し直前に
/// <see cref="PluginBase.FireLsp"/> へ注入する。
///
/// <see cref="PluginBase.Action"/> は <see cref="HookData.LspDiagnostics"/>（非同期に届いたものの
/// キャッシュ読み取りのみ・待たない）だが、こちらは逆に<b>応答が届くまでブロックしてよい</b>。
/// <c>Fire</c> は hook のようにレスポンス速度を問われない同期のバッチスキャンのため。
/// </summary>
public interface IFireLspRequester
{
    /// <summary>
    /// <paramref name="filePath"/> を <paramref name="content"/> の内容で対応する言語の LSP へ同期し
    /// （未起動なら起動を待つことも含む）、新しい診断が届くまで、または <paramref name="timeout"/> 経過まで待つ。
    /// 対応する LSP が無い（未対応拡張子／<c>common.yml</c> の <c>lsp:</c> 未設定／起動失敗）場合や
    /// タイムアウトした場合は空を返す（例外は投げない）。
    /// </summary>
    IReadOnlyList<LspDiagnostic> RequestDiagnostics(string filePath, string content, TimeSpan timeout);
}

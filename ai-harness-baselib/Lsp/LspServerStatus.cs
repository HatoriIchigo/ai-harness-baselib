namespace ai_harness_baselib;

/// <summary>
/// LSP サーバの状態。<c>ai-harness-main</c> の LSP マネージャが遅延インストール・起動する各言語について、
/// hook 発火時点のスナップショットとして <see cref="HookData.Lsp"/> 経由でプラグインへ渡る。
/// </summary>
public enum LspServerStatus
{
    /// <summary>インストール中（ダウンロード／npm・pip・go・dotnet install の実行中）、または起動処理中。</summary>
    Installing,

    /// <summary>起動して生存中。</summary>
    Running,

    /// <summary>直近の試行（インストールまたは起動）が失敗した状態のまま。</summary>
    Failed,
}

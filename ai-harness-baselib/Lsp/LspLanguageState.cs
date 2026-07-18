using System.Text.Json.Serialization;

namespace ai_harness_baselib;

/// <summary>1 言語ぶんの LSP 状態スナップショット。<see cref="HookData.Lsp"/> の値。</summary>
public sealed class LspLanguageState
{
    /// <summary>言語名（<c>common.yml</c> の <c>lsp:</c> と対応。例: <c>"python"</c>）。</summary>
    [JsonPropertyName("language")]
    public required string Language { get; init; }

    /// <summary>実際に使われているサーバ名（例: <c>"pyright"</c>／<c>"pylsp"</c>）。</summary>
    [JsonPropertyName("server")]
    public required string Server { get; init; }

    /// <summary>現在の状態。</summary>
    [JsonPropertyName("status")]
    public required LspServerStatus Status { get; init; }

    /// <summary><see cref="Status"/> が <see cref="LspServerStatus.Failed"/> のときの直近のエラー内容。それ以外は <c>null</c>。</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

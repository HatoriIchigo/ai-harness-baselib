using System.Text.Json.Serialization;

namespace ai_harness_baselib;

/// <summary>
/// LSP の 1 件の診断（<c>textDocument/publishDiagnostics</c> 通知に由来）。<see cref="HookData.LspDiagnostics"/> の値。
/// <c>ai-harness-main</c> がファイル変更のたびに裏で <c>textDocument/didOpen</c>／<c>didChange</c> を送り、
/// サーバから非同期に届く通知をキャッシュしたもののスナップショット。編集直後の hook では
/// まだ更新が届いておらず、古い（または無い）診断が見えることがある（サーバ側の解析完了を待たないため）。
/// </summary>
public sealed class LspDiagnostic
{
    /// <summary><c>"error"</c> / <c>"warning"</c> / <c>"information"</c> / <c>"hint"</c> のいずれか。</summary>
    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    /// <summary>開始行（0始まり、LSP準拠）。</summary>
    [JsonPropertyName("start_line")]
    public required int StartLine { get; init; }

    /// <summary>開始列（0始まり、LSP準拠）。</summary>
    [JsonPropertyName("start_column")]
    public required int StartColumn { get; init; }

    /// <summary>終了行（0始まり、LSP準拠）。</summary>
    [JsonPropertyName("end_line")]
    public required int EndLine { get; init; }

    /// <summary>終了列（0始まり、LSP準拠）。</summary>
    [JsonPropertyName("end_column")]
    public required int EndColumn { get; init; }

    /// <summary>診断メッセージ本文。</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>診断の発生元（例: <c>"pyright"</c>）。サーバが返さなければ <c>null</c>。</summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>診断コード（サーバ・ルール固有）。無ければ <c>null</c>。</summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }
}

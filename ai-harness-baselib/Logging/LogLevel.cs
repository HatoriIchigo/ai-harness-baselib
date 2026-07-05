namespace ai_harness_baselib;

/// <summary>
/// ログの重大度。数値が小さいほど詳細（冗長）。
/// main はプロジェクトの <c>common.yml</c> の <c>logLevel</c> 閾値以上のレベルのみ出力する。
/// </summary>
public enum LogLevel
{
    /// <summary>最も詳細。逐次のトレース向け。</summary>
    Trace,

    /// <summary>デバッグ用の詳細情報。</summary>
    Debug,

    /// <summary>通常の情報ログ（既定の閾値）。</summary>
    Info,

    /// <summary>想定外だが処理は継続する警告。</summary>
    Warning,

    /// <summary>失敗・エラー。</summary>
    Error,
}

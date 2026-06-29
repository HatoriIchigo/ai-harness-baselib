namespace ai_harness_baselib;

/// <summary>
/// ログの重大度。数値が小さいほど詳細（冗長）。
/// main はプロジェクトの <c>common.yml</c> の <c>logLevel</c> 閾値以上のレベルのみ出力する。
/// </summary>
public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error,
}

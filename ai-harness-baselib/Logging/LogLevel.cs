namespace ai_harness_baselib;

/// <summary>
/// ログの重大度。数値が小さいほど詳細（冗長）。
/// main は <c>config/main.yml</c> の閾値以上のレベルのみ出力する。
/// </summary>
public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error,
}

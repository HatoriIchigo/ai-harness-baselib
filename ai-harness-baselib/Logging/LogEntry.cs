namespace ai_harness_baselib;

/// <summary>
/// プラグインが返すログ専用型。レベル・メッセージ・発生源を持つ。
/// <see cref="PluginBase.Init"/> / <see cref="PluginBase.Action"/> が <c>yield</c> で逐次返す。
/// <see cref="Source"/> はプラグインが設定しなくてよい（main が plugin_name を打刻する）。
/// main 由来のログは source=claude として扱われる。
/// </summary>
public sealed record LogEntry(LogLevel Level, string Message, string? Source = null)
{
    /// <summary><see cref="LogLevel.Trace"/> のログを生成する。</summary>
    public static LogEntry Trace(string message) => new(LogLevel.Trace, message);

    /// <summary><see cref="LogLevel.Debug"/> のログを生成する。</summary>
    public static LogEntry Debug(string message) => new(LogLevel.Debug, message);

    /// <summary><see cref="LogLevel.Info"/> のログを生成する。</summary>
    public static LogEntry Info(string message) => new(LogLevel.Info, message);

    /// <summary><see cref="LogLevel.Warning"/> のログを生成する。</summary>
    public static LogEntry Warning(string message) => new(LogLevel.Warning, message);

    /// <summary><see cref="LogLevel.Error"/> のログを生成する。</summary>
    public static LogEntry Error(string message) => new(LogLevel.Error, message);
}

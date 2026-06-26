namespace ai_harness_baselib;

/// <summary>
/// Claude Code が発火させる hook イベント種別。
/// 値は Claude Code が <c>hook_event_name</c> に渡す文字列と一致。
/// 一覧は code.claude.com/docs/en/hooks.md（2026-06 時点）に準拠。
/// </summary>
public enum HookEvent
{
    // セッション
    SessionStart,
    Setup,
    SessionEnd,

    // ターン
    UserPromptSubmit,
    UserPromptExpansion,
    Stop,
    StopFailure,

    // ツール
    PreToolUse,
    PostToolUse,
    PostToolUseFailure,
    PostToolBatch,
    PermissionRequest,
    PermissionDenied,

    // 通知・表示
    Notification,
    MessageDisplay,

    // サブエージェント・タスク
    SubagentStart,
    SubagentStop,
    TaskCreated,
    TaskCompleted,
    TeammateIdle,

    // コンテキスト・設定
    InstructionsLoaded,
    ConfigChange,
    CwdChanged,
    FileChanged,
    PreCompact,
    PostCompact,

    // MCP・worktree
    Elicitation,
    ElicitationResult,
    WorktreeCreate,
    WorktreeRemove,
}

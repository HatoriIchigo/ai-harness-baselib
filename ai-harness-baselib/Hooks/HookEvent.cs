namespace ai_harness_baselib;

/// <summary>
/// Claude Code が発火させる hook イベント種別。
/// 値は Claude Code が <c>hook_event_name</c> に渡す文字列と一致。
/// 一覧は code.claude.com/docs/en/hooks.md（2026-06 時点）に準拠。
/// </summary>
public enum HookEvent
{
    // セッション

    /// <summary>セッション開始時。</summary>
    SessionStart,

    /// <summary>セットアップ時。</summary>
    Setup,

    /// <summary>セッション終了時。</summary>
    SessionEnd,

    // ターン

    /// <summary>ユーザーがプロンプトを送信した時。</summary>
    UserPromptSubmit,

    /// <summary>ユーザープロンプトの展開時。</summary>
    UserPromptExpansion,

    /// <summary>応答（ターン）が停止した時。</summary>
    Stop,

    /// <summary>停止処理が失敗した時。</summary>
    StopFailure,

    // ツール

    /// <summary>ツール実行の直前。deny すると実行をブロックできる。</summary>
    PreToolUse,

    /// <summary>ツール実行の直後。</summary>
    PostToolUse,

    /// <summary>ツール実行が失敗した後。</summary>
    PostToolUseFailure,

    /// <summary>ツールのバッチ実行後。</summary>
    PostToolBatch,

    /// <summary>権限（許可）要求時。</summary>
    PermissionRequest,

    /// <summary>権限が拒否された時。</summary>
    PermissionDenied,

    // 通知・表示

    /// <summary>通知の発生時。</summary>
    Notification,

    /// <summary>メッセージ表示時。</summary>
    MessageDisplay,

    // サブエージェント・タスク

    /// <summary>サブエージェント開始時。</summary>
    SubagentStart,

    /// <summary>サブエージェント停止時。</summary>
    SubagentStop,

    /// <summary>タスク作成時。</summary>
    TaskCreated,

    /// <summary>タスク完了時。</summary>
    TaskCompleted,

    /// <summary>teammate がアイドルになった時。</summary>
    TeammateIdle,

    // コンテキスト・設定

    /// <summary>指示（instructions）読み込み時。</summary>
    InstructionsLoaded,

    /// <summary>設定変更時。</summary>
    ConfigChange,

    /// <summary>カレントディレクトリ変更時。</summary>
    CwdChanged,

    /// <summary>ファイル変更検知時。</summary>
    FileChanged,

    /// <summary>コンテキスト圧縮（compact）の直前。</summary>
    PreCompact,

    /// <summary>コンテキスト圧縮（compact）の直後。</summary>
    PostCompact,

    // MCP・worktree

    /// <summary>MCP の elicitation 要求時。</summary>
    Elicitation,

    /// <summary>MCP の elicitation 応答時。</summary>
    ElicitationResult,

    /// <summary>worktree 作成時。</summary>
    WorktreeCreate,

    /// <summary>worktree 削除時。</summary>
    WorktreeRemove,
}

namespace ai_harness_baselib;

/// <summary>
/// hook イベント名の検証。プラグインの <see cref="PluginBase.Events"/> 宣言が
/// 有効な <see cref="HookEvent"/> 名（または <c>*</c>）かを検証する。
/// ツール名を持たないイベント（UserPromptSubmit / SessionStart 等）を対象にするための系統。
/// 検証は baselib の責務（<see cref="ToolCatalog"/> と対称）。
/// </summary>
public static class EventCatalog
{
    /// <summary>全イベントにマッチするワイルドカード。個別イベント名との併用は不可。</summary>
    public const string Wildcard = "*";

    /// <summary>単一イベント名が有効か。<c>*</c> または <see cref="HookEvent"/> に解釈可能な名前。</summary>
    public static bool IsValidEvent(string ev) =>
        ev == Wildcard || System.Enum.TryParse<HookEvent>(ev, ignoreCase: false, out _);

    /// <summary>
    /// プラグインの <see cref="PluginBase.Events"/> 宣言を検証する。
    /// <c>null</c>＝イベントフィルタ未使用として無視（成功扱い）。空配列・空白・重複・未知イベント名・
    /// ワイルドカードと個別名の併用は不正とする。
    /// </summary>
    public static FilterValidation ValidateEvents(IReadOnlyList<string>? events)
    {
        if (events is null)
        {
            return FilterValidation.Ok; // 未宣言＝無視
        }

        var errors = new List<string>();
        if (events.Count == 0)
        {
            errors.Add("Events が空配列。イベントフィルタ未使用なら null にすること。");
            return new FilterValidation(false, errors);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ev in events)
        {
            if (string.IsNullOrWhiteSpace(ev))
            {
                errors.Add("空または空白のみのイベント名は不可。");
                continue;
            }
            if (ev != ev.Trim())
            {
                errors.Add($"前後に空白を含むイベント名は不可: '{ev}'");
            }
            if (!seen.Add(ev))
            {
                errors.Add($"重複したイベント名: '{ev}'");
            }
            if (!IsValidEvent(ev))
            {
                errors.Add($"未知のイベント名: '{ev}'（HookEvent に存在しない）");
            }
        }

        if (events.Contains(Wildcard) && events.Count > 1)
        {
            errors.Add("\"*\"（全イベント）と個別イベント名は併用不可。");
        }

        return errors.Count == 0 ? FilterValidation.Ok : new FilterValidation(false, errors);
    }
}

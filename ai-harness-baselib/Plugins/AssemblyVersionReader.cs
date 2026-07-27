using System.Reflection;

namespace ai_harness_baselib;

/// <summary>
/// アセンブリに焼かれた版を表示用の文字列で読む。
///
/// 版の記述場所は csproj の <c>InformationalVersion</c>（無ければ <c>Version</c> 由来の数値版）だけとし、
/// コード側に版を書かない＝ DLL の実体と表示が食い違わないようにする。
/// <see cref="PluginBase.Version"/>（各プラグイン DLL）と ai-harness-main の <c>--version</c>（本体）が
/// 同じ規則で読むための共通実装。
/// </summary>
public static class AssemblyVersionReader
{
    /// <summary>版が全く読めなかったときの表示。</summary>
    public const string Unknown = "(unknown)";

    /// <summary>コミット sha は先頭からこの長さだけ見せる。</summary>
    private const int ShaLength = 7;

    /// <summary>
    /// <paramref name="assembly"/> の表示用の版。<c>InformationalVersion</c> を優先し、
    /// 無ければアセンブリの数値版（<c>1.0.0.0</c> 形式）へ倒す。どちらも無ければ <see cref="Unknown"/>。
    /// </summary>
    public static string Read(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (informational is { InformationalVersion.Length: > 0 })
        {
            return ShortenSha(informational.InformationalVersion);
        }
        return assembly.GetName().Version?.ToString() ?? Unknown;
    }

    /// <summary>
    /// <c>0.0.3α+&lt;sha&gt;</c> の sha を短縮する。SDK は git リポジトリからビルドすると
    /// 40 桁のフル sha を埋めるため、そのままでは読みにくい。
    /// </summary>
    private static string ShortenSha(string version)
    {
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0 || version.Length - plus - 1 <= ShaLength)
        {
            return version;
        }
        return version[..(plus + 1 + ShaLength)];
    }
}

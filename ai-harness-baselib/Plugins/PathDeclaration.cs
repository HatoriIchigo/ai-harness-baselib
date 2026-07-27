namespace ai_harness_baselib;

/// <summary>
/// あるプラグインが「このプロジェクトに存在する必要がある」と宣言したファイル配置。
/// <see cref="PluginBase.RequiredPaths"/> が返した glob に、host が宣言元の
/// <see cref="PluginBase.PluginName"/> を打刻したもの。
///
/// 宣言は<b>許可を広げない</b>。配置を検査するプラグイン（ai-harness-directory-checker 等）が
/// <see cref="PluginBase.ValidatePeers"/> で自分の設定と突き合わせ、覆えていなければ起動エラーを返す
/// ための材料に過ぎない。宣言を根拠に検査を緩めると、プラグインを 1 つ有効化しただけで
/// 別のガードが黙って緩むため、加算方向には一切効かせない。
/// </summary>
/// <param name="Source">宣言元の <see cref="PluginBase.PluginName"/>。</param>
/// <param name="Pattern">宣言されたファイル配置の glob。</param>
public readonly record struct PathDeclaration(string Source, string Pattern);

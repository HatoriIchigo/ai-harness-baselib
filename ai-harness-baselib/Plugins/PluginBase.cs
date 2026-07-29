using System.IO.Enumeration;
using System.Text.Json.Nodes;

namespace ai_harness_baselib;

/// <summary>
/// 全拡張プラグインの基底クラス。各プラグインはこれを継承（オーバーライド）して実装する。
/// このアセンブリ（baselib）はオーバーライド契約のみを定義し、ロード・発火・集約は ai-harness-main が担う。
///
/// 動作モデル:
///   1. ai-harness-main が特定フォルダ内の全プラグイン DLL をロード。
///   2. hook 発火時、main は hook JSON を <see cref="HookData"/> へ組み立て、全プラグインを並列起動（上限あり）。
///   3. 各プラグインは <see cref="Action"/> 内で <see cref="HookData"/> を見て自己フィルタ。
///      無関係なら何も yield せず <see cref="PluginResult.ExitCode"/> を 0 のまま返す。
///   4. main が全プラグインの <see cref="PluginResult"/> を deny 先勝ちで集約し、Claude Code へ整形して返す。
/// </summary>
public abstract class PluginBase
{
    /// <summary>
    /// プラグインのキー。ログの source として使われ、claude（ハーネス由来）と区別する。
    /// 各プラグインで一意な値を override する。
    /// </summary>
    public abstract string PluginName { get; }

    /// <summary>
    /// プラグインが何を強制するかの 1 行説明。人間向けの一覧表示（<c>ai-harness-main --plugin</c>）に使う。
    /// 既定は空文字＝説明なし。ハーネスの動作には影響しない。
    /// </summary>
    public virtual string Description => "";

    /// <summary>
    /// このプラグイン DLL の版。人間向けの一覧表示（<c>ai-harness-main --plugin</c>）に使う。
    /// ハーネスの動作には影響せず、更新判定にも使わない（表示専用）。
    ///
    /// override 不可＝版は各プラグインの csproj（<c>InformationalVersion</c>）にのみ書く。コード側にも版を
    /// 置くと DLL の実体と食い違ったまま気付けないため、実体から読む一方向に固定する。
    /// <see cref="object.GetType"/> は派生プラグインの型を返すため、参照先はプラグイン自身の DLL
    /// （baselib ではない）。csproj に版を書いていないプラグインは SDK 既定の <c>1.0.0</c> になる。
    /// </summary>
    public string Version => AssemblyVersionReader.Read(GetType().Assembly);

    /// <summary>
    /// このプラグインが各プロジェクトの <c>.claude/rules</c> へ配布する rule を持つか。既定 <c>false</c>＝配布しない。
    /// <c>true</c> にしたプラグインは、末尾が <c>.rule.md</c> の埋め込みリソースを 1 つ以上同梱する
    /// （<see cref="CopyRule"/> がそれを配置する。ちょうど 1 件なら <c>&lt;PluginName&gt;.md</c>、
    /// 2 件以上ならファイル名ごとに分けて配置する）。用途が異なる rule（例: 自身の設定ファイル向けと
    /// 検証対象ファイル向け）を 1 つにまとめず複数へ分けてよい。Claude Code 側への案内文の配布であり、
    /// ハーネスの発火判定には影響しない。
    /// </summary>
    public virtual bool ProvidesRule => false;

    /// <summary>
    /// このプラグインが各プロジェクトの <c>.claude/skills</c> へ配布する skill を持つか。既定 <c>false</c>＝配布しない。
    /// <c>true</c> にしたプラグインは、埋め込みリソースの論理名を <c>skills/&lt;スキル名&gt;/...</c>
    /// （csproj 側で <c>LogicalName</c> を明示し、実スラッシュ区切りで固定すること）で 1 つ以上同梱する
    /// （<see cref="CopySkill"/> がそれを配置する。<c>SKILL.md</c> と補助ファイル一式を想定するため
    /// 相対パスをそのまま複製する点が <see cref="CopyRule"/> と異なる）。Claude が能動的に読む案内文の
    /// 配布であり、ハーネスの発火判定には影響しない。
    /// </summary>
    public virtual bool ProvidesSkill => false;

    /// <summary>
    /// このプラグインが対象とするツール名の配列。未使用なら <c>null</c>（既定）。
    /// hook の <c>tool_name</c> がこの配列に含まれるイベントで <see cref="Action"/> が発火する。
    /// 全ツールを対象にするには <c>"*"</c>（<see cref="ToolCatalog.Wildcard"/>）。
    /// 値は <see cref="ToolCatalog.ValidateTools"/> でロード時に検証され、不正なら当該プラグインは無効化される。
    /// <c>tool_name</c> を持たないイベント（UserPromptSubmit 等）は <see cref="Events"/> で対象指定する。
    /// </summary>
    public virtual IReadOnlyList<string>? Tools => null;

    /// <summary>
    /// このプラグインが対象とする hook イベント名の配列。未使用なら <c>null</c>（既定）。
    /// hook の <c>hook_event_name</c> がこの配列に含まれるとき <see cref="Action"/> が発火する。
    /// 全イベントを対象にするには <c>"*"</c>（<see cref="EventCatalog.Wildcard"/>）。
    /// 値は <see cref="EventCatalog.ValidateEvents"/> でロード時に検証される。
    /// <see cref="Tools"/> と併せて評価され、いずれかにマッチすれば発火（OR）。両方 null のプラグインは発火しない。
    /// </summary>
    public virtual IReadOnlyList<string>? Events => null;

    /// <summary>
    /// このプラグインが対象とするファイルパスのパターン配列。未使用なら <c>null</c>（既定）。
    /// hook の file_path（<c>tool_input.file_path</c> を優先、無ければトップレベル <c>file_path</c>）が
    /// いずれかのパターンに glob 一致するとき <see cref="Action"/> が発火する。
    /// パターンは <c>*</c>（任意長）と <c>?</c>（任意1文字）のワイルドカードを解釈（例: <c>"*.cs"</c>, <c>"src/*"</c>）。
    /// 全ファイル対象は <c>"*"</c>。大文字小文字は無視。
    /// </summary>
    public virtual IReadOnlyList<string>? FileNames => null;

    /// <summary>
    /// このプラグインが対象とする Bash コマンドのパターン配列。未使用なら <c>null</c>（既定）。
    /// hook の <c>tool_input.command</c> がいずれかのパターンに glob 一致するとき <see cref="Action"/> が発火する。
    /// パターンは <c>*</c>（任意長）と <c>?</c>（任意1文字）のワイルドカードを解釈（例: <c>"git push*"</c>, <c>"*rm *"</c>）。
    /// 全コマンド対象は <c>"*"</c>。大文字小文字は区別する。
    /// </summary>
    public virtual IReadOnlyList<string>? BashCommands => null;

    /// <summary>
    /// この hook データに対して発火すべきか。<see cref="Tools"/>（tool_name 完全一致）・
    /// <see cref="Events"/>（hook_event_name 完全一致）・<see cref="FileNames"/>（file_path glob）・
    /// <see cref="BashCommands"/>（command glob）の <b>OR</b>。各配列の <c>"*"</c> は全マッチ。
    /// null の系統は評価対象外。全系統 null のプラグインは発火しない。ai-harness-main が発火前に呼ぶ。
    /// </summary>
    public bool ShouldFire(HookData data) =>
        MatchesTool(data.ToolName)
        || MatchesEvent(data.HookEventName)
        || MatchesPattern(FileNames, ExtractFilePath(data), ignoreCase: true)
        || MatchesPattern(BashCommands, ExtractBashCommand(data), ignoreCase: false);

    private bool MatchesTool(string? toolName)
    {
        if (Tools is null)
        {
            return false;
        }
        if (Tools.Contains(ToolCatalog.Wildcard))
        {
            return true;
        }
        return !string.IsNullOrEmpty(toolName) && Tools.Contains(toolName);
    }

    private bool MatchesEvent(string? eventName)
    {
        if (Events is null)
        {
            return false;
        }
        if (Events.Contains(EventCatalog.Wildcard))
        {
            return true;
        }
        return !string.IsNullOrEmpty(eventName) && Events.Contains(eventName);
    }

    /// <summary>
    /// パターン配列に対する glob マッチ（<c>*</c>＝任意長 / <c>?</c>＝任意1文字）。
    /// null 配列は対象外（false）。<c>"*"</c> 単体を含む場合は値の有無に依らず全マッチ
    /// （<see cref="Tools"/>/<see cref="Events"/> の <c>"*"</c> と整合）。空配列はマッチなし。
    /// </summary>
    private static bool MatchesPattern(IReadOnlyList<string>? patterns, string? value, bool ignoreCase)
    {
        if (patterns is null)
        {
            return false;
        }
        if (patterns.Contains("*"))
        {
            return true;
        }
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        foreach (var pattern in patterns)
        {
            if (FileSystemName.MatchesSimpleExpression(pattern, value, ignoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>マッチ対象のファイルパス。tool_input.file_path 優先、無ければトップレベル file_path。</summary>
    private static string? ExtractFilePath(HookData data) =>
        AsString(GetMember(data.ToolInput, "file_path")) ?? data.FilePath;

    /// <summary>マッチ対象の Bash コマンド（tool_input.command）。</summary>
    private static string? ExtractBashCommand(HookData data) =>
        AsString(GetMember(data.ToolInput, "command"));

    /// <summary>JsonObject のメンバを安全に取得（オブジェクト以外・不在は null）。</summary>
    private static JsonNode? GetMember(JsonNode? node, string name) =>
        node is JsonObject obj && obj.TryGetPropertyValue(name, out var v) ? v : null;

    /// <summary>JsonNode が文字列値なら取り出す。型不一致・null は null。</summary>
    private static string? AsString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>
    /// ロード直後に 1 度だけ呼ばれる初期化。
    /// ログを <c>yield</c> で逐次返す（処理を止めずに出力させる）。
    /// </summary>
    public abstract IEnumerable<LogEntry> Init();

    /// <summary>
    /// hook 発火本体。main が全フィールドを含む <see cref="HookData"/>（欠落フィールドは null）を渡す。
    /// ログを <c>yield</c> で逐次返し、<b>列挙が完了した時点で</b> <paramref name="result"/> に判定が確定する。
    /// 列挙最後の yield の後に <c>result.ExitCode = ...</c> を書くこと（MoveNext が false を返す瞬間に実行される）。
    /// </summary>
    /// <param name="data">hook データ（全フィールド・欠落は null）。</param>
    /// <param name="result">列挙完了時に書き込む結果ホルダ。main が読む。</param>
    public abstract IEnumerable<LogEntry> Action(HookData data, PluginResult result);

    /// <summary>
    /// 能動スキャン本体。hook イベントには紐づかず、CLI（<c>ai-harness-main --fire</c>）から
    /// <b>手動で</b>起動される点が <see cref="Action"/> と異なる。<paramref name="projectRoot"/> 配下を
    /// 能動的に点検し、ログを <c>yield</c> で逐次返す。列挙が完了した時点で <paramref name="result"/> に
    /// 結果が確定する（<see cref="Action"/> と同じ規約。最後の yield の後に <c>result.ExitCode = ...</c> を書く）。
    ///
    /// <see cref="Action"/> のような <see cref="ShouldFire"/> フィルタは通らず、有効化されている（common.yml の
    /// tools で on の）プラグインが一律に呼ばれる。<see cref="LoadConfig"/> 済みで呼ばれるため
    /// <see cref="Config"/> を参照できる。hook のゲートではないため、ここでの非 0 <see cref="PluginResult.ExitCode"/>
    /// は何かをブロックするのではなく、スキャンの検出結果としてレポートに表示されるだけ。
    ///
    /// <paramref name="projectRoot"/> はスキャン対象のプロジェクトルート（絶対パス）。daemon は常駐ゆえ
    /// 各 hook/CLI プロセスの cwd を持たないため、走査対象は引数で受け取る（<see cref="Config"/> の所在と同様に
    /// プロジェクトごとに異なる）。
    ///
    /// 既定は何もしない（no-op）。スキャンを実装したいプラグインのみ override する。
    /// </summary>
    /// <param name="projectRoot">スキャン対象のプロジェクトルート（絶対パス）。</param>
    /// <param name="result">列挙完了時に書き込む結果ホルダ。main が読み、レポートへ整形する。</param>
    public virtual IEnumerable<LogEntry> Fire(string projectRoot, PluginResult result)
    {
        yield break;
    }

    /// <summary>
    /// このプラグインが「このプロジェクトに存在する必要がある」と要求するファイル配置の glob 配列。
    /// 既定は空＝何も要求しない。自分の <see cref="Config"/> からのみ導出すること
    /// （他プラグインの宣言を参照して導出すると相互依存で循環する）。
    ///
    /// host は起動検証時に、有効化された全プラグインのこの値を集めて
    /// <see cref="ValidatePeers"/> へ渡す。宣言は<b>他プラグインの許可を広げない</b>
    /// （詳細は <see cref="PathDeclaration"/>）。要求が他のガードと両立しないことを
    /// 起動時に露見させるためだけに使う。
    ///
    /// 未設定を意味する雛形値（<see cref="CopyDefaultConfig"/> が置くプレースホルダ等）は
    /// 宣言しないこと。設定が済んでいないだけの状態で、他プラグインの起動エラーを誘発するため。
    /// </summary>
    public virtual IReadOnlyList<string> RequiredPaths => [];

    /// <summary>
    /// 他プラグインの <see cref="RequiredPaths"/> 宣言と自分の設定の矛盾を検証する。
    /// host は起動検証で全プラグインの <see cref="LoadConfig"/> が済んだ後に 1 度呼ぶ
    /// （<see cref="Init"/> の時点では宣言が出揃っていないため、そこでは検証できない）。
    ///
    /// 返した文字列は起動エラーとして積まれ、そのプロジェクトの hook は
    /// <b>フェイルクローズで全てブロックされる</b>（設定を直せばホットリロードで解除）。
    /// 「両立しない 2 つのガードを有効化したまま作業を続けさせない」ための強度であり、
    /// 警告に留めたい内容を返してはならない。
    ///
    /// <paramref name="declarations"/> には自分自身の宣言も含まれる。要求元を区別する必要があれば
    /// <see cref="PathDeclaration.Source"/> を見ること。宣言が 0 件（要求元プラグインが無効・未導入）なら
    /// 検証対象が無い＝エラー無しで返し、自分の設定のみで動作する。
    ///
    /// 既定は何も検証しない（no-op）。配置を検査するプラグインのみ override する。
    /// </summary>
    /// <param name="declarations">有効化された全プラグインの配置要求（宣言元の名前つき）。</param>
    /// <returns>矛盾の説明（利用者向け・修正方法を含めること）。矛盾が無ければ空。</returns>
    public virtual IEnumerable<string> ValidatePeers(IReadOnlyList<PathDeclaration> declarations) => [];

    /// <summary>
    /// <see cref="Fire"/> 専用の LSP 診断リクエスタ。host が <see cref="Fire"/> 呼び出し直前に設定する
    /// （<see cref="Action"/> の実行時は設定されない＝常に <c>null</c>。<see cref="Action"/> は
    /// <see cref="HookData.LspDiagnostics"/> のキャッシュ読み取りのみを使う）。
    /// LSP 連携機能自体が無い・未起動などで使えない状況では <c>null</c> のままのことがある
    /// （<see cref="Fire"/> 実装側で null チェックすること）。
    /// </summary>
    public IFireLspRequester? FireLsp { get; set; }

    /// <summary>
    /// このプラグインの設定ファイル名。<b>必須</b>。プロジェクトの設定ディレクトリ
    /// （<c>&lt;プロジェクトルート&gt;/.claude/harness/config</c>）からの相対名。
    /// 未設定（null/空）の場合 <see cref="LoadConfig"/> がエラーを投げ、ai-harness-main は当該プラグインを無効化する。
    /// </summary>
    public virtual string? ConfigName => null;

    private IReadOnlyDictionary<string, object>? _config;

    /// <summary>
    /// <see cref="LoadConfig"/> でロード済みの設定（YAML をパースしたマッピング）。未ロードで参照すると例外。
    /// 値はスカラ＝<c>string</c>、ネストマップ＝<c>Dictionary&lt;object, object&gt;</c>、配列＝<c>List&lt;object&gt;</c>
    /// （YamlDotNet の既定デシリアライズ）。プラグインは標準型のみ参照し YamlDotNet には依存しない。
    /// </summary>
    protected IReadOnlyDictionary<string, object> Config =>
        _config ?? throw new InvalidOperationException(
            $"{PluginName}: 設定が未ロード。LoadConfig の呼び出しが先に必要。");

    /// <summary>
    /// <see cref="ConfigName"/> が指す YAML 設定ファイルを、<paramref name="configDir"/>（プロジェクト個別の
    /// 設定ディレクトリ <c>&lt;プロジェクトルート&gt;/.claude/harness/config</c>）からロード・パースし、内部変数に保持する。
    /// ConfigName が未設定（null/空）の場合はエラー（必須）。ファイル不在も例外。空ファイルは空マッピング。
    /// ai-harness-main がプラグインのインスタンス生成直後（Init / Action の前）に呼ぶ。
    /// 単一 daemon が複数プロジェクトをさばくため、設定の所在は実行体ではなくプロジェクトごとに異なる。
    /// </summary>
    /// <param name="configDir">プロジェクトの設定ディレクトリ（絶対パス）。</param>
    public void LoadConfig(string configDir)
    {
        if (string.IsNullOrWhiteSpace(ConfigName))
        {
            throw new InvalidOperationException(
                $"{PluginName}: ConfigName が未設定。設定ファイル名の宣言は必須。");
        }
        var path = Path.Combine(configDir, ConfigName);
        var text = ReadConfigFile(path);
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
        // 空ファイル/コメントのみは null デシリアライズ → 空マッピングへフォールバック。
        _config = deserializer.Deserialize<Dictionary<string, object>>(text)
                  ?? new Dictionary<string, object>();
    }

    /// <summary>プラグインが自身の設定ファイルを読む共通ヘルパ。</summary>
    public string ReadConfigFile(string configPath) => File.ReadAllText(configPath);

    /// <summary>
    /// <see cref="ProvidesRule"/> が <c>true</c> のとき、このプラグインが同梱する埋め込み rule
    /// （末尾 <c>.rule.md</c> のリソース）を <paramref name="rulesDir"/> 配下へ上書きコピーする。
    /// <c>false</c> のときは何もしない。
    ///
    /// 1 プラグインが複数の rule（例: 自身の設定ファイル向けと、検証対象ファイル向けで内容を分ける）を
    /// 同梱してもよい。ちょうど 1 件のときは既存互換の <c>&lt;PluginName&gt;.md</c> 単一ファイルへ配置する
    /// （名前が変わらないため、既存プラグインは無変更で動く）。2 件以上のときは配置先が一意になるよう、
    /// 各リソースの論理名の末尾（<c>.rule.md</c> を除いたファイル名部分）を <c>&lt;PluginName&gt;-&lt;末尾&gt;.md</c>
    /// として使う。
    ///
    /// 宛先ディレクトリは host が渡す（baselib はプロジェクトルートを知らない＝<see cref="LoadConfig"/> と同じ流儀）。
    /// hook のゲートではなく Claude Code への案内文の配布なので、host は Create 時のみ呼び、失敗しても block しない。
    /// </summary>
    /// <param name="rulesDir">配置先ディレクトリ（<c>&lt;プロジェクトルート&gt;/.claude/rules</c> の絶対パス）。</param>
    /// <returns>書き込んだファイルの絶対パスの一覧。<see cref="ProvidesRule"/> が <c>false</c> のときは空。</returns>
    public IReadOnlyList<string> CopyRule(string rulesDir)
    {
        if (!ProvidesRule)
        {
            return Array.Empty<string>();
        }

        var asm = GetType().Assembly;
        var resources = Array.FindAll(
            asm.GetManifestResourceNames(),
            n => n.EndsWith(".rule.md", StringComparison.OrdinalIgnoreCase));
        if (resources.Length == 0)
        {
            throw new InvalidOperationException(
                $"{PluginName}: ProvidesRule=true だが埋め込みリソース *.rule.md が見つからない。");
        }

        Directory.CreateDirectory(rulesDir);
        var written = new List<string>();
        foreach (var resource in resources)
        {
            using var reader = new StreamReader(asm.GetManifestResourceStream(resource)!);
            var content = reader.ReadToEnd();

            var fileName = resources.Length == 1
                ? $"{PluginName}.md"
                : $"{PluginName}-{ExtractRuleBasename(resource)}.md";
            var path = Path.Combine(rulesDir, fileName);
            File.WriteAllText(path, content);
            written.Add(path);
        }
        return written;
    }

    /// <summary>
    /// 埋め込みリソース名から rule のファイル名部分を取り出す（末尾セグメント・<c>.rule.md</c> を除く）。
    /// 既定のドット連結命名／<c>LogicalName</c> によるスラッシュ区切りのどちらでも、区切り文字の後の
    /// 最後のセグメントが元のファイル名に対応するため、区切り文字は問わず最後のセグメントを取る。
    /// </summary>
    private static string ExtractRuleBasename(string resourceName)
    {
        var normalized = resourceName.Replace('\\', '/');
        var lastSegment = normalized[(normalized.LastIndexOf('/') + 1)..];
        return lastSegment.EndsWith(".rule.md", StringComparison.OrdinalIgnoreCase)
            ? lastSegment[..^".rule.md".Length]
            : lastSegment;
    }

    /// <summary>埋め込みリソースの論理名が持つディレクトリ区切り。csproj 側の <c>LogicalName</c> は実スラッシュ
    /// で書く規約だが、MSBuild の <c>%(RecursiveDir)</c> は Windows ビルドでバックスラッシュを混ぜて出すことが
    /// あるため、比較・分解の前に両方をスラッシュへ正規化する。</summary>
    private const string SkillResourcePrefix = "skills/";

    /// <summary>
    /// <see cref="ProvidesSkill"/> が <c>true</c> のとき、このプラグインが同梱する埋め込み skill 一式
    /// （論理名が <c>skills/</c> で始まる全リソース）を、そのプレフィックスを除いた相対パスのまま
    /// <paramref name="skillsDir"/> 配下へ複製する（既存ファイルは上書き）。<c>false</c> のときは何もしない。
    ///
    /// <see cref="CopyRule"/> と異なり 1 プラグインが複数ファイル（<c>SKILL.md</c> と参考資料など）を
    /// 同梱できる。相対パスの復元を一意にするため、csproj 側は既定のドット連結命名に頼らず
    /// <c>LogicalName</c> で <c>skills/&lt;相対パス&gt;</c> を明示する規約とする。
    ///
    /// 宛先ディレクトリは host が渡す（<see cref="CopyRule"/> と同じ流儀）。hook のゲートではなく
    /// Claude Code への案内文の配布なので、host は Create 時のみ呼び、失敗しても block しない。
    /// </summary>
    /// <param name="skillsDir">配置先ディレクトリ（<c>&lt;プロジェクトルート&gt;/.claude/skills</c> の絶対パス）。</param>
    /// <returns>書き込んだファイルの絶対パスの一覧。<see cref="ProvidesSkill"/> が <c>false</c> のときは空。</returns>
    public IReadOnlyList<string> CopySkill(string skillsDir)
    {
        if (!ProvidesSkill)
        {
            return Array.Empty<string>();
        }

        var asm = GetType().Assembly;
        var resources = Array.FindAll(
            asm.GetManifestResourceNames(),
            n => n.Replace('\\', '/').StartsWith(SkillResourcePrefix, StringComparison.Ordinal));
        if (resources.Length == 0)
        {
            throw new InvalidOperationException(
                $"{PluginName}: ProvidesSkill=true だが論理名が '{SkillResourcePrefix}' で始まる埋め込みリソースが見つからない。");
        }

        var written = new List<string>();
        foreach (var resource in resources)
        {
            var relative = resource.Replace('\\', '/')[SkillResourcePrefix.Length..];
            var destPath = Path.Combine(skillsDir, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            using var reader = new StreamReader(asm.GetManifestResourceStream(resource)!);
            var content = reader.ReadToEnd();
            File.WriteAllText(destPath, content);
            written.Add(destPath);
        }
        return written;
    }

    /// <summary>
    /// このプラグインが同梱するデフォルト設定（末尾 <c>.config.yml</c> の埋め込みリソース）を、
    /// <paramref name="configDir"/>/<see cref="ConfigName"/> が<b>存在しないときだけ</b>書き出す。
    /// 既存のユーザー設定は絶対に上書きしない。<see cref="ConfigName"/> 未設定・埋め込み無し・既存ありは何もしない。
    ///
    /// <c>--enable</c> 時に host が呼び、有効化直後のフェイルクローズ（設定 YAML 不在で <see cref="LoadConfig"/> が
    /// 失敗する）を避けるための雛形配置。中身は「有効化しても即 deny せず・フェイルクローズもしない安全既定」を
    /// 各プラグインが用意する。宛先ディレクトリは host が渡す（<see cref="LoadConfig"/> と同じ流儀）。
    /// </summary>
    /// <param name="configDir">プロジェクトの設定ディレクトリ（絶対パス）。</param>
    /// <returns>書き出したファイルの絶対パス。既存・埋め込み無し・ConfigName 未設定なら <c>null</c>。</returns>
    public string? CopyDefaultConfig(string configDir)
    {
        if (string.IsNullOrWhiteSpace(ConfigName))
        {
            return null;
        }
        var path = Path.Combine(configDir, ConfigName);
        if (File.Exists(path))
        {
            return null; // 既存のユーザー設定を尊重（上書きしない）。
        }

        var asm = GetType().Assembly;
        var resource = Array.Find(
            asm.GetManifestResourceNames(),
            n => n.EndsWith(".config.yml", StringComparison.OrdinalIgnoreCase));
        if (resource is null)
        {
            return null; // デフォルト設定を同梱していない。
        }

        using var reader = new StreamReader(asm.GetManifestResourceStream(resource)!);
        var content = reader.ReadToEnd();

        Directory.CreateDirectory(configDir);
        File.WriteAllText(path, content);
        return path;
    }
}

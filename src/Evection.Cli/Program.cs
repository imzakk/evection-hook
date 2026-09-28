using Evection.Cli;
using Evection.Detector;
using Evection.Inspector;
using Evection.Installer;

var args0 = new Args(args);
var command = args0[0]?.ToLowerInvariant();

try
{
    return command switch
    {
        null or "help" or "-h" or "--help" => Help(),
        "version" or "--version" => Version(),
        "scan" => Scan(),
        "detect" or "info" => Detect(args0),
        "install" => Install(args0),
        "play" or "launch" => Play(args0),
        "profiles" or "profile" => Profiles(args0),
        "uninstall" => Uninstall(args0),
        "mods" => Mods(args0),
        "types" => Types(args0),
        "search" => Search(args0),
        "inspect" => Inspect(args0),
        "source" => Source(args0),
        "dump" => Dump(args0),
        _ => throw new UsageException($"Unknown command '{command}'."),
    };
}
catch (UsageException e)
{
    Output.Error(e.Message);
    Output.Line("Run 'evection help' for usage.");
    return 2;
}
catch (Exception e) when (e is GameDetectionException or InstallException or InspectorException)
{
    Output.Error(e.Message);
    return 1;
}

static int Help()
{
    Output.Info($"Evection Hook {ModLoaderInstaller.Version} — a free, universal Unity mod loader");
    Output.Line("""

    <game> can be a game folder, the game's .exe, or the name of an installed Steam game.

    Players
      scan                              List Unity games in your Steam libraries
      detect <game>                     Show engine, backend (Mono/IL2CPP), anti-cheat...
      install <game>                    Install Evection Hook into a game
      uninstall <game> [--remove-mods]  Remove it (keeps your mods unless --remove-mods)
      mods <game>                       List installed mods
      mods <game> add <file.dll>        Install a mod
      mods <game> enable|disable <mod>  Turn a mod on or off
      play <game> [--profile name]      Start the game with a mod profile
      profiles <game>                   List mod profiles (* = active)
      profiles <game> new <name> [--copy name]
      profiles <game> use|delete <name>

    Modders
      types <game> [filter]             List the game's classes (optionally filtered)
      search <game> <term>              Find classes/fields/methods by name, e.g. "health"
      inspect <game> <Type>             Show every field, property and method of a class
      source <game> <Type>              Print the decompiled C# of a class
      dump <game> [--out dir]           Decompile the game's code into a folder you can
                                        open in VS Code / Rider (--all includes libraries,
                                        --assembly Name picks one assembly)

    Options
      --libs                            types/search/inspect/source: include Unity & library code
      --payload <dir>                   install: use a custom payload folder
    """);
    return 0;
}

static int Version()
{
    Output.Line(ModLoaderInstaller.Version);
    return 0;
}

static GameInfo ResolveGame(Args a)
{
    var query = a.Require(1, "game (a folder, .exe, or Steam game name)");
    if (File.Exists(query) || Directory.Exists(query))
        return GameDetector.Detect(query);

    var matches = SteamLibrary.FindUnityGames()
        .Where(g => g.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(g.GameDirectory).Contains(query, StringComparison.OrdinalIgnoreCase))
        .ToList();
    return matches.Count switch
    {
        1 => matches[0],
        0 => throw new GameDetectionException($"No folder or installed Unity game matches '{query}'. Try 'evection scan'."),
        _ => matches.FirstOrDefault(g => string.Equals(g.Name, query, StringComparison.OrdinalIgnoreCase))
             ?? throw new GameDetectionException(
                 $"'{query}' matches several games: {string.Join(", ", matches.Select(g => g.Name))}. Be more specific."),
    };
}

static int Scan()
{
    var games = SteamLibrary.FindUnityGames();
    if (games.Count == 0)
    {
        Output.Warn("No Unity games found in your Steam libraries.");
        return 0;
    }
    Output.Info($"Found {games.Count} Unity game(s):");
    foreach (var g in games)
    {
        Output.Write($"  {g.Name,-40} ", ConsoleColor.White);
        Output.Write($"{g.Backend,-7} ", g.Backend == ScriptingBackend.Mono ? ConsoleColor.Green : ConsoleColor.Magenta);
        Output.Write($"{g.UnityVersion ?? "?",-14}", ConsoleColor.DarkGray);
        if (g.HasAntiCheat) Output.Write(" anti-cheat", ConsoleColor.Red);
        if (g.EvectionInstalled) Output.Write(" installed", ConsoleColor.Cyan);
        Output.Line();
    }
    return 0;
}

static int Detect(Args a)
{
    var g = ResolveGame(a);
    Output.Info(g.Name);
    Output.Field("Company", g.Company);
    Output.Field("Folder", g.GameDirectory);
    Output.Field("Executable", Path.GetFileName(g.ExecutablePath));
    Output.Field("Platform", g.Platform.ToString());
    Output.Field("Backend", g.Backend.ToString());
    Output.Field("Architecture", g.Architecture.ToString());
    Output.Field("Unity", g.UnityVersion);
    Output.Field("Anti-cheat", g.HasAntiCheat ? string.Join(", ", g.AntiCheats.Select(x => $"{x.Name} ({x.Evidence})")) : "none found");
    Output.Field("Other loaders", g.OtherModLoaders.Count > 0 ? string.Join(", ", g.OtherModLoaders.Select(x => $"{x.Name} ({x.Evidence})")) : "none");
    Output.Field("Evection Hook", g.EvectionInstalled ? "installed" : "not installed");

    var problems = ModLoaderInstaller.CheckCompatibility(g);
    Output.Line();
    if (problems.Count == 0)
        Output.Ok("Compatible — run 'evection install' to set it up.");
    else
        foreach (var p in problems) Output.Warn(p);
    return 0;
}

static int Install(Args a)
{
    var game = ResolveGame(a);
    var payload = Payload.Locate(a.Option("payload"));
    var result = ModLoaderInstaller.Install(game, payload);
    Output.Ok($"Installed Evection Hook {result.Manifest.Version} into {game.Name} ({game.Backend}, {game.Architecture}).");
    Output.Field("Mods folder", result.ModsDirectory);
    Output.Field("Logs", Path.Combine(game.EvectionDirectory, "Logs", "latest.log"));
    foreach (var note in result.Notes)
        Output.Warn(note);
    return 0;
}

static int Play(Args a)
{
    var game = ResolveGame(a);
    var profile = a.Option("profile") is { } name ? ModProfiles.Find(game, name).Id : null;
    GameLauncher.Launch(game, profile);
    Output.Ok($"Starting {game.Name} with mods.");
    return 0;
}

static int Profiles(Args a)
{
    var game = ResolveGame(a);
    switch (a[2]?.ToLowerInvariant())
    {
        case null or "list":
            var active = ModProfiles.GetActive(game).Id;
            foreach (var p in ModProfiles.List(game))
            {
                Output.Write(p.Id == active ? "  * " : "    ", ConsoleColor.Green);
                Output.Line($"{p.Name}  ({new ModsFolder(p).List().Count} mods)");
            }
            return 0;
        case "new" or "create":
            var copy = a.Option("copy") is { } from ? ModProfiles.Find(game, from).Id : null;
            Output.Ok($"Created profile {ModProfiles.Create(game, a.Require(3, "profile name"), copy).Name}.");
            return 0;
        case "use":
            var use = ModProfiles.Find(game, a.Require(3, "profile name"));
            ModProfiles.SetActive(game, use.Id);
            Output.Ok($"{use.Name} is now the active profile.");
            return 0;
        case "delete":
            var del = ModProfiles.Find(game, a.Require(3, "profile name"));
            ModProfiles.Delete(game, del.Id);
            Output.Ok($"Deleted {del.Name}.");
            return 0;
        default:
            throw new UsageException($"Unknown profiles action '{a[2]}'.");
    }
}

static int Uninstall(Args a)
{
    var game = ResolveGame(a);
    var removeMods = a.Has("remove-mods");
    ModLoaderInstaller.Uninstall(game, keepUserData: !removeMods);
    Output.Ok($"Removed Evection Hook from {game.Name}." + (removeMods ? "" : " Your mods were kept in EvectionHook/Mods."));
    return 0;
}

static int Mods(Args a)
{
    var game = ResolveGame(a);
    var folder = new ModsFolder(game);
    switch (a[2]?.ToLowerInvariant())
    {
        case null or "list":
            var mods = folder.List();
            Output.Info($"{mods.Count} mod(s) in {folder.Directory}");
            foreach (var m in mods)
            {
                Output.Write(m.Enabled ? "  [on]  " : "  [off] ", m.Enabled ? ConsoleColor.Green : ConsoleColor.DarkGray);
                Output.Line(m.Name);
            }
            return 0;
        case "add":
            Output.Ok($"Added {folder.Add(a.Require(3, "path to the mod .dll")).Name}.");
            return 0;
        case "enable":
        case "disable":
            var enable = a[2]!.Equals("enable", StringComparison.OrdinalIgnoreCase);
            var mod = folder.SetEnabled(a.Require(3, "mod name"), enable);
            Output.Ok($"{mod.Name} is now {(enable ? "enabled" : "disabled")}.");
            return 0;
        default:
            throw new UsageException($"Unknown mods action '{a[2]}'.");
    }
}

static CodeBrowser Browser(Args a, out GameInfo game)
{
    game = ResolveGame(a);
    var code = GameCode.Load(game, Output.Info);
    return new CodeBrowser(code, includeLibraries: a.Has("libs"));
}

static int Types(Args a)
{
    var browser = Browser(a, out _);
    var count = 0;
    foreach (var group in browser.FindTypes(a[2]).GroupBy(t => t.Assembly))
    {
        Output.Info($"{group.Key}.dll");
        foreach (var (_, type) in group.OrderBy(t => t.Type.FullName, StringComparer.Ordinal))
        {
            Output.Line("  " + type.FullName);
            count++;
        }
    }
    Output.Write($"{count} type(s)", ConsoleColor.DarkGray);
    Output.Line();
    return 0;
}

static int Search(Args a)
{
    var browser = Browser(a, out _);
    var term = a.Require(2, "search term");
    var hits = browser.Search(term).ToList();
    foreach (var group in hits.GroupBy(h => h.TypeName))
    {
        Output.Write(group.Key, ConsoleColor.Cyan);
        Output.Line();
        foreach (var hit in group)
        {
            Output.Write($"  {hit.Kind,-9}", ConsoleColor.DarkGray);
            Output.Line(hit.Signature);
        }
    }
    Output.Write($"{hits.Count} match(es) for '{term}'", ConsoleColor.DarkGray);
    Output.Line();
    return 0;
}

static int Inspect(Args a)
{
    var browser = Browser(a, out _);
    Output.Line(browser.Describe(a.Require(2, "type name")));
    return 0;
}

static int Source(Args a)
{
    var browser = Browser(a, out _);
    Output.Line(browser.Source(a.Require(2, "type name")));
    return 0;
}

static int Dump(Args a)
{
    var game = ResolveGame(a);
    var code = GameCode.Load(game, Output.Info);
    var assemblies = a.Option("assembly") is { } name ? [code.Resolve(name)]
        : a.Has("all") ? code.AllAssemblies
        : code.GameAssemblies;
    var outDir = Path.GetFullPath(a.Option("out") ?? Path.Combine("dumps", string.Concat(game.Name.Split(Path.GetInvalidFileNameChars()))));
    ProjectExporter.Export(code, assemblies, outDir, Output.Info);
    Output.Ok($"Decompiled {assemblies.Count} assembly(ies) to {outDir}");
    Output.Line("  Open that folder in VS Code or Rider to browse the game's code.");
    return 0;
}

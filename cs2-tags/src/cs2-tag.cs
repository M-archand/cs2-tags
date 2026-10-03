using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using TagsApi;
using static Tags.TagExtensions;
using static TagsApi.Tags;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Tags;

// 313 = uses toml
[MinimumApiVersion(313)]
public class Tags : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Tags";
    public override string ModuleVersion => "1.2.0";
    public override string ModuleAuthor => "schwarper, Marchand";

    internal static readonly ConcurrentDictionary<ulong, Tag> PlayerTagsList = new();
    // Attribute types set through ITagApi, kept across tag refreshes until ResetAttribute or disconnect
    internal static readonly ConcurrentDictionary<ulong, TagType> PlayerTagOverrides = new();
    internal static readonly TagsAPI Api = new();
    private static Tags? _instance;
    internal static Tags Instance
    {
        get => _instance ?? throw new InvalidOperationException("Tags.Instance accessed before Load() completed.");
        private set => _instance = value;
    }
    public Config Config { get; set; } = new();

    private readonly List<string> _tagsReloadCommands = [];
    private readonly List<string> _visibilityCommands = [];

    // Minimum interval between command-triggered reloads. Each reload is a synchronous
    // config file read plus O(players) admin lookups, clan-tag writes and client events.
    private const long ReloadCooldownMs = 2000;
    private long _lastReloadTick = -ReloadCooldownMs;

    public override void Load(bool hotReload)
    {
        Instance = this;
        TagsApiHost.Attach(Api);

        foreach (string command in Config.Commands.TagsReload)
        {
            AddCommand(command, "Tags Reload", Command_Tags_Reload);
            _tagsReloadCommands.Add(command);
        }

        foreach (string command in Config.Commands.Visibility)
        {
            AddCommand(command, "Visibility", Command_Visibility);
            _visibilityCommands.Add(command);
        }
        
        AddCommandListener("say", OnSayCommand, HookMode.Pre);
        AddCommandListener("say_team", OnSayTeamCommand, HookMode.Pre);
        AddCommandListener("css_admins_reload", Command_Admins_Reloads, HookMode.Pre);

        RegisterListener<Listeners.OnMapStart>(OnMapStart);

        if (hotReload)
            ReloadTags();
    }

    public override void Unload(bool hotReload)
    {
        foreach (string command in _tagsReloadCommands)
            RemoveCommand(command, Command_Tags_Reload);
        _tagsReloadCommands.Clear();

        foreach (string command in _visibilityCommands)
            RemoveCommand(command, Command_Visibility);
        _visibilityCommands.Clear();

        RemoveCommandListener("css_admins_reload", Command_Admins_Reloads, HookMode.Pre);
        RemoveCommandListener("say", OnSayCommand, HookMode.Pre);
        RemoveCommandListener("say_team", OnSayTeamCommand, HookMode.Pre);

        RemoveListener<Listeners.OnMapStart>(OnMapStart);

        TagsApiHost.Detach(Api);
        Api.ClearSubscribers();
        PlayerTagsList.Clear();
        PlayerTagOverrides.Clear();
        _instance = null;
    }

    public void OnConfigParsed(Config config)
    {
        config.Settings.Init();
        config.BuildIndex();
        Config = config;

        if (config.Tags.Count == 0)
            Logger.LogWarning("No [[Tags]] entries in cs2-tags.toml; every player gets the [Default] tag.");
    }

    private void OnMapStart(string mapName)
    {
        ReloadTags();
    }

    public HookResult Command_Admins_Reloads(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && !AdminManager.PlayerHasPermissions(player, "@css/generic"))
            return HookResult.Continue;

        if (TryBeginReload())
        {
            ReloadConfig();
            ReloadTags();
        }

        return HookResult.Continue;
    }

    [RequiresPermissions("@css/root")]
    public void Command_Tags_Reload(CCSPlayerController? player, CommandInfo info)
    {
        if (!TryBeginReload())
        {
            info.ReplyToCommand(Config.Settings.Tag + Localizer.ForPlayer(player, "Tags were reloaded a moment ago, try again shortly."));
            return;
        }

        ReloadConfig();
        ReloadTags();
    }

    private bool TryBeginReload()
    {
        long now = Environment.TickCount64;
        if (now - _lastReloadTick < ReloadCooldownMs)
            return false;

        _lastReloadTick = now;
        return true;
    }

    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void Command_Visibility(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.HasTagIdentity())
        {
            return;
        }

        if (!player.HasVisibilityPermission())
        {
            info.ReplyToCommand(Config.Settings.Tag + Localizer.ForPlayer(player, "Missing required permission to use this command."));
            return;
        }

        if (player.GetVisibility())
        {
            player.SetVisibility(false);
            info.ReplyToCommand(Config.Settings.Tag + Localizer.ForPlayer(player, "Tags are now hidden"));
        }
        else
        {
            player.SetVisibility(true);
            info.ReplyToCommand(Config.Settings.Tag + Localizer.ForPlayer(player, "Tags are now visible"));
        }
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (@event.Userid is not CCSPlayerController player || !player.HasTagIdentity())
            return HookResult.Continue;

        // Fires again after a map change, RefreshPlayerTag keeps state of players who stayed connected
        RefreshPlayerTag(player);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid is not CCSPlayerController player || !player.HasTagIdentity())
            return HookResult.Continue;

        PlayerTagsList.TryRemove(player.SteamID, out _);
        PlayerTagOverrides.TryRemove(player.SteamID, out _);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event.Userid is not CCSPlayerController player || !player.HasTagIdentity())
            return HookResult.Continue;

        var tag = GetOrCreatePlayerTag(player, false);
        player.SetScoreTag(player.GetVisibility() ? tag.ScoreTag : string.Empty, force: true);
        return HookResult.Continue;
    }

    public HookResult OnSayCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.HasTagIdentity())
            return HookResult.Continue;

        string message = info.GetArg(1);
        if (string.IsNullOrEmpty(message))
            return HookResult.Continue;

        return ProcessChatCommand(player, message, false);
    }
    
    public HookResult OnSayTeamCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.HasTagIdentity())
            return HookResult.Continue;

        string message = info.GetArg(1);
        if (string.IsNullOrEmpty(message))
            return HookResult.Continue;

        return ProcessChatCommand(player, message, true);
    }

    private static bool IsCssChatCommand(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        return StartsWithAny(text, CoreConfig.PublicChatTrigger) || StartsWithAny(text, CoreConfig.SilentChatTrigger);
    }

    private static bool StartsWithAny(string text, IEnumerable<string> prefixes)
    {
        foreach (string prefix in prefixes)
        {
            if (prefix.Length > 0 && text.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private HookResult ProcessChatCommand(CCSPlayerController player, string message, bool teamMessage)
    {
        if (!player.IsValid)
            return HookResult.Continue;

        if (IsCssChatCommand(message))
            return HookResult.Continue;

        var tag = GetOrCreatePlayerTag(player, false);

        MessageProcess messageProcess = new()
        {
            Player = player,
            Tag = !player.GetVisibility() ? Config.Default.Clone() : tag.Clone(),
            Message = message.RemoveCurlyBraceContent(),
            PlayerName = player.PlayerName.SanitizeName(),
            ChatSound = tag.ChatSound,
            TeamMessage = teamMessage
        };

        HookResult hookResult = Api.MessageProcessPre(messageProcess);

        if (hookResult >= HookResult.Handled)
            return hookResult;

        string deadname = player.PawnIsAlive ? string.Empty : Config.Settings.DeadName;
        string teamname = messageProcess.TeamMessage ? player.Team.Name() : string.Empty;

        Tag playerData = messageProcess.Tag;

        CsTeam team = player.Team;
        messageProcess.PlayerName = FormatMessage(team, deadname, teamname, playerData.ChatTag ?? string.Empty, playerData.NameColor ?? string.Empty, messageProcess.PlayerName);
        messageProcess.Message = FormatMessage(team, playerData.ChatColor ?? string.Empty, messageProcess.Message);

        hookResult = Api.MessageProcess(messageProcess);

        if (hookResult >= HookResult.Handled)
            return hookResult;

        // Send the formatted message
        string formattedMessage = $"{messageProcess.PlayerName}{ChatColors.White}: {messageProcess.Message}";

        if (messageProcess.TeamMessage)
        {
            // Send to team only
            foreach (var p in Utilities.GetPlayers())
            {
                if (p.IsValid && p.Team == player.Team)
                    p.PrintToChat(formattedMessage);
            }
        }
        else
        {
            // Send to all players
            Server.PrintToChatAll(formattedMessage);
        }

        Api.MessageProcessPost(messageProcess);

        return HookResult.Handled; // Block the original message
    }
}
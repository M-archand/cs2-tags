using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using TagsApi;
using static Tags.TagExtensions;
using static TagsApi.Tags;
using System.Collections.Concurrent;

namespace Tags;

public class Tags : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Tags";
    public override string ModuleVersion => "1.2.0";
    public override string ModuleAuthor => "schwarper, Marchand";

    public static readonly ConcurrentDictionary<ulong, Tag> PlayerTagsList = new();
    public static readonly TagsAPI Api = new();
    private static Tags? _instance;
    public static Tags Instance
    {
        get => _instance ?? throw new InvalidOperationException("Tags.Instance accessed before Load() completed.");
        private set => _instance = value;
    }
    public Config Config { get; set; } = new();

    private readonly List<string> _tagsReloadCommands = [];
    private readonly List<string> _visibilityCommands = [];

    public override void Load(bool hotReload)
    {
        Instance = this;
        Capabilities.RegisterPluginCapability(ITagApi.Capability, () => Api);

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
    }

    public void OnConfigParsed(Config config)
    {
        config.Settings.Init();
        Config = config;
    }

    public static HookResult Command_Admins_Reloads(CCSPlayerController? player, CommandInfo info)
    {
        ReloadConfig();
        ReloadTags();
        return HookResult.Continue;
    }

    [RequiresPermissions("@css/root")]
    public void Command_Tags_Reload(CCSPlayerController? player, CommandInfo info)
    {
        ReloadConfig();
        ReloadTags();
    }

    [RequiresPermissions("@css/admin")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void Command_Visibility(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null)
        {
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
        if (@event.Userid is not CCSPlayerController player || player.IsBot || player.SteamID == 0)
            return HookResult.Continue;

        PlayerTagsList[player.SteamID] = player.GetTag();
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid is not CCSPlayerController player || player.IsBot || player.SteamID == 0)
            return HookResult.Continue;

        PlayerTagsList.TryRemove(player.SteamID, out _);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event.Userid is not CCSPlayerController player || player.IsBot || player.SteamID == 0)
            return HookResult.Continue;

        var tag = GetOrCreatePlayerTag(player, false);
        player.SetScoreTag(player.GetVisibility() ? tag.ScoreTag : string.Empty, force: true);
        return HookResult.Continue;
    }

    public HookResult OnSayCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || player.IsBot)
            return HookResult.Continue;

        string message = info.GetArg(1);
        if (string.IsNullOrEmpty(message))
            return HookResult.Continue;

        return ProcessChatCommand(player, message, false);
    }
    
    public HookResult OnSayTeamCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || player.IsBot)
            return HookResult.Continue;

        string message = info.GetArg(1);
        if (string.IsNullOrEmpty(message))
            return HookResult.Continue;

        return ProcessChatCommand(player, message, true);
    }

    private static bool IsCssChatCommand(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var span = text.AsSpan();
        int i = 0;
        while (i < span.Length && char.IsWhiteSpace(span[i])) i++;
        if (i >= span.Length) return false;
        char c = span[i];
        return c == '!' || c == '/' || c == '.';
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
            PlayerName = player.PlayerName,
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
# cs2-tags
A tag plugin for CounterStrikeSharp that gives players a scoreboard tag, a chat tag, a name color and a chat color. Tags are assigned from a TOML config by SteamID64, admin group or permission flag, and other plugins can change them at runtime through the shared `TagsApi` assembly.

cs2-tags replaces the stock chat line with its own formatted line: `[dead marker][team label]<ChatTag><NameColor>Name: <ChatColor>Message`. The team label is only added to team chat (`say_team`), and the dead marker only for dead T and CT players. Messages that start with a CounterStrikeSharp chat trigger (`!` and `/` by default, from `core.json`) are left to CounterStrikeSharp. Players can hide their own tags with a toggle command.

## Requirements
- CounterStrikeSharp **v1.0.369** or newer, the first .NET 10 build. The plugin is built against **v1.0.381**. It declares `MinimumApiVersion(313)` for TOML config support, but the `net10.0` target does not load on older CounterStrikeSharp builds.
- .NET 10 runtime (the plugin targets `net10.0`).
- `#group` and `@permission` roles use the CounterStrikeSharp admin system (`admins.json` / `admin_groups.json`).

## Installation
1. Copy `plugins/cs2-tags/`, `shared/TagsApi/` and `configs/plugins/cs2-tags/` from the release zip into `addons/counterstrikesharp/`.
2. Start the server once. CounterStrikeSharp copies `cs2-tags.example.toml` to `cs2-tags.toml` in the same folder; edit that file.
3. Run `css_tags_reload` (needs `@css/root`) after editing. `css_admins_reload` also re-reads the config and refreshes tags after admin changes.

If `cs2-tags.toml` has no `[[Tags]]` entries the plugin logs a warning and every player gets the `[Default]` tag.

Upgrading from v1.2.0 or older requires a full server restart, not a hot reload. The old shared `TagsApi.dll` and its capability supplier stay in memory until then. The old `cs2-tags-example.toml` file can be deleted; your `cs2-tags.toml` is not touched.

## Example
```c#
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using TagsApi;
using static TagsApi.Tags;

namespace TagsExample;

public class TagsExample : BasePlugin
{
    public override string ModuleName => "Tags Example";
    public override string ModuleVersion => "1.0.0";

    private ITagApi? TagApi;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        TagApi = ITagApi.Capability.Get();

        if (TagApi == null)
        {
            Logger.LogWarning("[Tags-Example] cs2-tags is not loaded");
            return;
        }

        TagApi.OnMessageProcessPre += OnMessageProcessPre;
        TagApi.OnMessageProcessPost += OnMessageProcessPost;
        TagApi.OnTagsUpdatedPost += OnTagsUpdatedPost;
    }

    public override void Unload(bool hotReload)
    {
        if (TagApi == null) return;

        TagApi.OnMessageProcessPre -= OnMessageProcessPre;
        TagApi.OnMessageProcessPost -= OnMessageProcessPost;
        TagApi.OnTagsUpdatedPost -= OnTagsUpdatedPost;
    }

    // Runs before the chat line is formatted. message.Tag is a copy, so changes apply to this line only.
    private HookResult OnMessageProcessPre(MessageProcess message)
    {
        // Handled or Stop drops the line: neither the cs2-tags line nor the stock chat line is sent
        if (message.Message.Contains("discord.gg", StringComparison.OrdinalIgnoreCase))
            return HookResult.Handled;

        // Show team chat in light blue without changing the player's saved tag
        if (message.TeamMessage)
            message.Tag.ChatColor = "{LightBlue}";

        return HookResult.Continue;
    }

    // Runs after the line was sent. PlayerName and Message now hold the formatted text with color codes.
    private void OnMessageProcessPost(MessageProcess message)
    {
        Logger.LogInformation("[Tags-Example] {Player} sent a {Scope} message", message.Player.PlayerName, message.TeamMessage ? "team" : "public");
    }

    // Runs after every ITagApi change to a player's tag and after the visibility toggle
    private void OnTagsUpdatedPost(CCSPlayerController player, Tag tag)
    {
        Logger.LogInformation("[Tags-Example] {Player} chat tag: '{ChatTag}', visible: {Visible}", player.PlayerName, tag.ChatTag, tag.Visibility);
    }

    [ConsoleCommand("css_viptag", "Gives the caller a VIP tag")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    [RequiresPermissions("@css/vip")]
    public void OnVipTagCommand(CCSPlayerController? caller, CommandInfo _)
    {
        if (caller == null || !caller.IsValid || TagApi == null) return;

        // SetAttribute replaces the value. The score tag is plain text, chat values take color tokens.
        TagApi.SetAttribute(caller, TagType.ScoreTag, "[VIP]");
        TagApi.SetAttribute(caller, TagType.NameColor, "{Gold}");

        // AddAttribute puts text before (Pre) or after (Post) the current value, and adds it again on every call
        string chatTag = TagApi.GetAttribute(caller, TagType.ChatTag) ?? string.Empty;
        if (!chatTag.Contains("[VIP]"))
            TagApi.AddAttribute(caller, TagType.ChatTag, TagPrePost.Pre, "{Gold}[VIP] ");
    }

    [ConsoleCommand("css_viptag_reset", "Restores the caller's tag from cs2-tags.toml")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnVipTagResetCommand(CCSPlayerController? caller, CommandInfo _)
    {
        if (caller == null || !caller.IsValid || TagApi == null) return;

        // TagType is a flags enum, so one call can reset several attributes
        TagApi.ResetAttribute(caller, TagType.ScoreTag | TagType.ChatTag | TagType.NameColor);
    }
}
```

Reference `TagsApi.dll` (shipped in `shared/TagsApi/`) from your plugin project and do not copy it into your plugin folder; CounterStrikeSharp loads the shared copy.

If your plugin changes a player's admin groups or permissions, refresh that player's config tag so a `#group` or `@permission` entry applies at once:
```c#
AdminManager.AddPlayerPermissions(target, "@css/vip");
TagApi?.ReloadTags(target);
```

## API
cs2-tags exposes `ITagApi` through the CounterStrikeSharp plugin capability `tags:api` (`ITagApi.Capability`). `ITagApi.Capability.Get()` returns `null` while cs2-tags is not loaded.

The `ITagApi` interface exposes:
- `void SetAttribute(player, types, value)` - replaces every attribute in `types` with `value`. A score tag change is sent to the scoreboard at once.
- `void AddAttribute(player, types, prePost, value)` - puts `value` before (`TagPrePost.Pre`) or after (`TagPrePost.Post`) the current value of every attribute in `types`. Each call adds it again.
- `string? GetAttribute(player, type)` - returns the current value of one attribute. Returns `null` for a combined flag value.
- `void ResetAttribute(player, types)` - restores the attributes in `types` to the player's config tag and stops keeping the API value.
- `bool GetPlayerVisibility(player)` / `void SetPlayerVisibility(player, value)` - same as the visibility command. A hidden player chats with the `[Default]` tag and has no score tag.
- `bool GetPlayerChatSound(player)` / `void SetPlayerChatSound(player, value)` - stores the value only, see [Known limitations](#known-limitations).
- `void ReloadTags()` - resolves the config tag again for every connected player. It does not re-read `cs2-tags.toml`.
- `void ReloadTags(player)` - same for one player, for example after your plugin changed their groups or permissions.
- Events: `OnMessageProcessPre`, `OnMessageProcess`, `OnMessageProcessPost`, `OnTagsUpdatedPre`, `OnTagsUpdatedPost`.

Values set with `SetAttribute` / `AddAttribute` and the visibility state stay through `css_tags_reload`, admin reloads, `ReloadTags` and map changes. They are cleared by `ResetAttribute` or when the player disconnects.

Events:
- `HookResult OnMessageProcessPre(MessageProcess)` - before the line is formatted. `Tag` is a copy of the player's tag, `PlayerName` and `Message` have braces (`Message`: whole `{...}` tokens) and control characters removed. Change `Tag`, `PlayerName`, `Message` or `TeamMessage` to change this line. Return `Handled` or `Stop` to drop the line.
- `HookResult OnMessageProcess(MessageProcess)` - after formatting, before the line is sent. `PlayerName` and `Message` hold the final text with color codes. Return `Handled` or `Stop` to drop the line.
- `void OnMessageProcessPost(MessageProcess)` - after the line was sent.
- `void OnTagsUpdatedPre(player, tag)` / `void OnTagsUpdatedPost(player, tag)` - before and after each `SetAttribute`, `AddAttribute`, `ResetAttribute`, `SetPlayerVisibility` and `SetPlayerChatSound` call, and the visibility command. They are not raised for config reloads or map changes.

With several subscribers on a message event, the highest `HookResult` wins. A subscriber that throws is logged with its type, method and event name, and the other subscribers still run.

Types (`using static TagsApi.Tags;`):
- `TagType` (flags) - `None`, `ScoreTag`, `ChatTag`, `NameColor`, `ChatColor`.
- `TagPrePost` - `Pre`, `Post`.
- `Tag` - `Role`, `ScoreTag`, `ChatTag`, `ChatColor`, `NameColor`, `Visibility`, `ChatSound`, `Clone()`.
- `MessageProcess` - `Player`, `Tag`, `PlayerName`, `Message`, `ChatSound`, `TeamMessage`.

Rules for API consumers:
- Call every `ITagApi` member and subscribe to events from the game thread only.
- Bots and players without a SteamID (not yet authorized) are skipped: setters do nothing, `GetAttribute` returns `null` and the `bool` getters return `false`. No message events are raised for them, and their chat stays stock.
- Resolve the API in `OnAllPluginsLoaded` or on each use, and always null-check the result.
- Do not keep the `ITagApi` instance across a cs2-tags reload. When cs2-tags is hot reloaded it unloads the old instance, drops every event subscription and publishes a fresh instance through `TagsApiHost`. Resolve the API again and subscribe again; reloading your plugin after cs2-tags is the simplest way.


## Commands
- `css_tags_reload` - re-reads `cs2-tags.toml` and refreshes every player's tag. Needs `@css/root` or the server console.
- `css_toggletags` / `css_visibility` (`!toggletags` in chat) - hides or shows the caller's own tags. Players only. Needs one of the `VisibilityPermissions` entries.
- `css_admins_reload` (CounterStrikeSharp) - cs2-tags also re-reads `cs2-tags.toml` and refreshes tags, when run from the console or by a player with `@css/generic`.
- `css_groups_reload` (CounterStrikeSharp) - cs2-tags refreshes tags without re-reading its config, with the same permission check.

Command-triggered reloads run at most once every 2 seconds. The names of the cs2-tags commands are set in the `[Commands]` section.

## Config
`addons/counterstrikesharp/configs/plugins/cs2-tags/cs2-tags.toml`:
```toml
[Settings]
# Prefix for replies from cs2-tags commands
Tag = "{Red}[CSS] "
# Shown before the name of dead T and CT players
DeadName = "☠"
# Team labels, shown before the name in team chat
NoneName = "{White}(NONE)"
SpecName = "{Purple}(SPEC)"
TName = "{Yellow}(T)"
CTName = "{Blue}(CT)"
# Who may use the visibility toggle. '#' prefix = admin group, otherwise a permission flag. Empty list = everyone.
VisibilityPermissions = [ "@css/admin" ]

[Commands]
TagsReload = [ "css_tags_reload" ]
Visibility = [ "css_toggletags", "css_visibility" ]

# Used for players who match no [[Tags]] entry and for players who hid their tags
[Default]
ScoreTag = ""
ChatTag = "{Grey}[Player] "
ChatColor = "{White}"
NameColor = "{TeamColor}"

# Matched by SteamID64
[[Tags]]
Role = "76561198000000000"
ScoreTag = "[DEV]"
ChatTag = "{Red}[DEV] "
ChatColor = "{Green}"
NameColor = "{TeamColor}"

# Matched by admin group
[[Tags]]
Role = "#OWNER"
ScoreTag = "[OWNER]"
ChatTag = "{Red}[OWNER] "
ChatColor = "{Green}"
NameColor = "{TeamColor}"

# Matched by permission flag
[[Tags]]
Role = "@css/vip"
ScoreTag = "[VIP]"
ChatTag = "{Gold}[VIP] "
ChatColor = "{White}"
NameColor = "{Gold}"
```
Each player gets one tag, picked in this order:
1. The `[[Tags]]` entry whose `Role` is the player's SteamID64.
2. The first `#group` entry, in file order, for a group the player is in.
3. The first `@permission` entry, in file order, for a permission the player has.
4. `[Default]`.

A SteamID64 entry always wins over group entries, and a group entry always wins over permission entries, wherever they are in the file. Each `[[Tags]]` entry stands alone: a key left out is empty, it is not taken from `[Default]`.

`ScoreTag` is the clan tag on the scoreboard and is plain text; color tokens do not work there. `ChatTag`, `ChatColor`, `NameColor` and the `[Settings]` strings take the color tokens listed below, in any letter case. Put a trailing space at the end of `ChatTag` to separate it from the name.

Tags are resolved again when a player connects, when the player is authorized, on every map start and on every reload command. Keys left out of `[Settings]` and `[Commands]` fall back to the values above, except `VisibilityPermissions`, which falls back to `[ "@css/admin", "@css/root" ]`.

## Colors
**Please do not use Default, use White instead**.
```
White
TeamColor
DarkRed
Green
LightYellow
LightBlue
Olive
Lime
Red
LightPurple
Purple
Grey
Yellow
Gold
Silver
Blue
DarkBlue
BlueGrey
Magenta
LightRed
Orange
```
`{TeamColor}` is the color of the player's team.

## Known limitations
`ChatSound` currently has no effect. The key is still accepted on `[Default]` and `[[Tags]]`, and `ITagApi.GetPlayerChatSound` / `SetPlayerChatSound` still exist, so existing configs and plugins keep working, but no chat sound is played or muted. It is kept for a future fix.

## Screenshots
![tag1](https://github.com/user-attachments/assets/93a333b4-55e0-4582-8f09-8ec3010724d3)

![tag2](https://github.com/user-attachments/assets/9066cb2f-2b6d-4268-9db3-b824de28d05e)

## Credits
Original plugin by [schwarper](https://github.com/schwarper/).

[Hextags plugin for CSGO](https://github.com/Hexer10/HexTags)

[@daffyyyy](https://github.com/daffyyyy/)

[@Yarukon](https://github.com/Yarukon)


## Changelog

See [CHANGELOG.md](CHANGELOG.md).

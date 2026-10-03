using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;
using static TagsApi.Tags;
using System.Text.Json.Serialization;

namespace Tags;

public class Config : BasePluginConfig
{
    public Settings Settings { get; set; } = new();
    public Commands Commands { get; set; } = new();
    public Tag Default { get; set; } = new() { ScoreTag = string.Empty, ChatTag = "{Grey}[Player]", ChatColor = "{White}", NameColor = "{TeamColor}" };
    public List<Tag> Tags { get; set; } = [];

    // Compiled index, rebuilt only when the config changes (load + reload)
    // Avoids re-scanning the full Tags list on every player join
    [JsonIgnore]
    public Dictionary<string, Tag> SteamIdTags { get; private set; } = [];
    [JsonIgnore]
    public List<Tag> GroupTags { get; private set; } = [];
    [JsonIgnore]
    public List<Tag> PermissionTags { get; private set; } = [];

    public void BuildIndex()
    {
        SteamIdTags = new Dictionary<string, Tag>(StringComparer.Ordinal);
        GroupTags = [];
        PermissionTags = [];

        foreach (Tag tag in Tags)
        {
            if (tag.Role is not { Length: > 0 })
                continue;

            switch (tag.Role[0])
            {
                case '#':
                    GroupTags.Add(tag);
                    break;
                case '@':
                    PermissionTags.Add(tag);
                    break;
                default:
                    SteamIdTags[tag.Role] = tag;
                    break;
            }
        }
    }
}

public class Settings
{
    public string Tag { get; set; } = "{Red}[CSS] ";
    public string DeadName { get; set; } = "☠";
    public string NoneName { get; set; } = "{White}(NONE)";
    public string SpecName { get; set; } = "{Purple}(SPEC)";
    public string TName { get; set; } = "{Yellow}(T)";
    public string CTName { get; set; } = "{Blue}(CT)";
    public List<string> VisibilityPermissions { get; set; } = ["@css/admin", "@css/root"];
    public Dictionary<CsTeam, string> TeamNames = [];

    public void Init()
    {
        Tag = Tag.ReplaceColorTags();

        TeamNames = new Dictionary<CsTeam, string>
        {
            [CsTeam.None] = NoneName,
            [CsTeam.Spectator] = SpecName,
            [CsTeam.Terrorist] = TName,
            [CsTeam.CounterTerrorist] = CTName,
        };
    }
}

public class Commands
{
    public string[] TagsReload { get; set; } = ["css_tags_reload"];
    public string[] Visibility { get; set; } = ["css_toggletags", "css_visibility"];
}
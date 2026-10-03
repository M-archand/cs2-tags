# cs2-tags

A tag plugin designed to enhance your CS2 experience with a dynamic tagging system. Customise and manage player tags effortlessly for a more interactive and engaging game environment.

## Installation

1. Copy `plugins/cs2-tags/`, `shared/TagsApi/` and `configs/plugins/cs2-tags/` from the release zip into `addons/counterstrikesharp/`.
2. Start the server once. CounterStrikeSharp copies `cs2-tags.example.toml` to `cs2-tags.toml` in the same folder; edit that file.
3. Run `css_tags_reload` (needs `@css/root`) after editing. `css_admins_reload` also refreshes tags after admin changes.

If `cs2-tags.toml` has no `[[Tags]]` entries the plugin logs a warning and every player gets the `[Default]` tag.

## Credits

[Hextags plugin for CSGO](https://github.com/Hexer10/HexTags)

[@daffyyyy](https://github.com/daffyyyy/)

[@Yarukon](https://github.com/Yarukon)

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

## Screenshots
![tag1](https://github.com/user-attachments/assets/93a333b4-55e0-4582-8f09-8ec3010724d3)

![tag2](https://github.com/user-attachments/assets/9066cb2f-2b6d-4268-9db3-b824de28d05e)

## API (for plugin developers)

cs2-tags exposes `ITagApi` through the CounterStrikeSharp plugin capability `tags:api`. Reference `TagsApi.dll` (shipped in `shared/TagsApi/`) from your plugin project and resolve the API when you need it:

```csharp
using TagsApi;

ITagApi? tagApi = ITagApi.Capability.Get();
if (tagApi is null)
    return; // cs2-tags is not loaded

tagApi.SetAttribute(player, Tags.TagType.ChatTag, "{Red}[VIP] ");
```

- Resolve the API in `OnAllPluginsLoaded` or on each use, and always null-check the result.
- Do not cache the `ITagApi` instance across plugin reloads. When cs2-tags is hot reloaded it unloads the old instance, drops every event subscription, and publishes a fresh instance through `TagsApiHost`; re-resolve the API and subscribe to its events again (reloading your plugin after cs2-tags is the simplest way).
- All `ITagApi` members must be called from the game thread.
- Upgrading from a cs2-tags version without `TagsApiHost` requires a full server restart; the old shared `TagsApi.dll` and its capability supplier stay in memory until then.

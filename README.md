# Stratum UI

A Vintage Story client/server mod that adds a proper online player list, command
autocomplete, a chat history overlay, and a few staff quality-of-life tools.

Built primarily for Stratum-powered servers but works fully against vanilla
servers and singleplayer worlds - on vanilla the staff features simply
stay dormant.

## Features

- Online player list with search, role colouring, and live ping (`Tab` by default).
- Right-click a player (staff only) for View Profile / Kick / Ban / Mute /
  Jail / Warn / Report / Freeze actions.
- Full player profile dialog with the in-game character layout, hotbar +
  backpack snapshot, and a moderation records popout (warnings & violations).
- Chat history overlay with scrollback.
- Command autocomplete popup while typing in chat.

## Install

1. Download the latest release zip (or build from source, see below).
2. Drop it into your `Mods/` folder (`%APPDATA%/VintagestoryData/Mods/` on
   Windows).
3. Launch the game. The mod is marked as not required, so it can be used on
   any server without forcing other players to install it.

## Build from source

The project currently builds inside my local Vintage Story server source tree
(the `.csproj` has relative `ProjectReference`s to `VintagestoryAPI` and the
Cairo wrapper). If you want to build this standalone, swap those for
`Reference` entries pointing at `VintagestoryAPI.dll` and `cairo-sharp.dll`
from your Vintage Story install.

Requires the .NET 10 SDK.

```
dotnet build StratumUI.csproj -c Release
```

The resulting `bin/Release/net10.0/StratumUI.dll` plus `modinfo.json` go into
a folder named `StratumUI` inside your `Mods` directory.

## Compatibility

- Game version: 1.20+ (NetworkVersion 1.22.6).
- Works on vanilla servers (read-only player list, no staff actions).
- Works on dedicated Stratum servers (full feature set).
- Works in singleplayer.

## License

See [LICENSE](LICENSE). Short version: read it, learn from it, build your own
stuff with it, just don't repackage and sell it.

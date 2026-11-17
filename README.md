# Hallow Mixer

A per-app volume mixer for Windows 10/11 - one fader per kind of sound, a game/chat balance, your mic, presets and hotkeys. Free, tiny (under 100 KB), nothing else to install.

## Features

- **Channels:** Game, Chat, Music, Media, System & other - each with a fader, mute, live level meter and its own output device (game on your headset, music on your speakers).
- **Apps sort themselves:** games from your Steam, Xbox, Epic, Riot... folders go to Game; Discord, TeamSpeak and Mumble to Chat; Spotify to Music; browsers and video players to Media. Right-click any app to move it - it's remembered. Every app also has its own slider inside its channel.
- **Game / chat balance:** one slider - lean toward CHAT and the game gets quieter while you talk.
- **Mic:** level, mute, live meter, and which mic Windows uses.
- **Master output:** pick your default speakers/headset and their volume.
- **Presets:** Gaming, Movie, Chill - click to use, right-click to save your current levels.
- **Hotkeys:** `Ctrl+Alt+PageUp` / `PageDown` = game/chat balance, `Ctrl+Alt+M` = mute mic.
- **Tray app:** close the window and it keeps running by the clock; optional start with Windows. Exit puts every app back to full volume and its normal output.

## Install

1. Download **Hallow Mixer.zip** from [Releases](../../releases/latest)
2. Right-click the zip > Properties > tick **Unblock** > OK, then Extract All
3. Double-click **INSTALL.bat** - no admin needed, it installs to your user folder

Runs on the .NET Framework that comes with Windows 10 and 11. The app isn't code-signed, so Windows may say "Windows protected your PC" - click **More info > Run anyway**.

## Good to know

- Hallow Mixer changes each app's own volume (the same thing Windows' volume mixer does), so it works with any headset or speakers, and alongside other audio software.
- It can't create virtual sound devices or add EQ/noise removal - those need an audio driver.
- Settings live in `%APPDATA%\HallowMixer`.

## Build it yourself

```powershell
.\build.ps1
```

Uses the C# compiler that ships with Windows (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) - no SDK or Visual Studio needed. Source is in `src/`: `Native.cs` (Windows Core Audio), `Engine.cs` (channels, volume math, settings), `App.cs` (window, tray, hotkeys), `Theme.xaml` (look).

Part of the Hallow family alongside [Hallow Toolkit](https://github.com/Dom428/hallow-toolkit).

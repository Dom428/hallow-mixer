# Hallow Mixer

A per-app volume mixer for Windows 10/11 - one fader per kind of sound, a game/chat balance, your mic, presets and hotkeys. Free, tiny (under 100 KB), nothing else to install.

## Features

- **Channels:** Game, Chat, Music, Media, System & other - each with a fader, mute, live level meter and its own output device (game on your headset, music on your speakers).
- **Apps sort themselves:** games from your Steam, Xbox, Epic, Riot... folders go to Game; Discord, TeamSpeak and Mumble to Chat; Spotify to Music; browsers and video players to Media. Right-click any app to move it - it's remembered. Every app also has its own slider inside its channel.
- **Game / chat balance:** one slider - lean toward CHAT and the game gets quieter while you talk.
- **Mic:** level, mute, live meter, and which mic Windows uses.
- **Master output:** pick your default speakers/headset and their volume.
- **Presets:** Gaming, Movie, Chill and up to 8 of your own - click to use, right-click to save, rename or delete, + to add.
- **Push-to-talk / push-to-mute:** hold a key or mouse side button; works in every app at once.
- **Auto-ducking:** when someone talks in Chat, game, music and media dip (by an amount you pick) and fade back after. Optionally music dips while *you* talk.
- **Levels per game:** each game gets its own channel levels - they come back when it starts, and your normal levels return when it closes.
- **Per-app mute:** click an app's icon. Scroll over any channel to change it.
- **EQ:** 10-band EQ for each headset, speaker and mic with presets (Bass boost, Footsteps (FPS), Voice clarity, Movie, Music; mic: Clear voice, Warm, Bright, Cut rumble). Uses the free, open-source [Equalizer APO](https://sourceforge.net/projects/equalizerapo/) - Hallow Mixer controls it for you.
- **App in front:** `Ctrl+Alt+Up` / `Down` changes the volume of just the game or app you're in.
- **Per-app output:** right-click an app > Play on, to send one app somewhere other than its channel.
- **Mic test:** records 5 seconds and plays it back - exactly what Discord and games hear - and tells you if you're too quiet or clipping.
- **Mini mixer:** a small always-on-top panel with every channel, your mic and the game/chat balance.
- **Headset first:** when your headset turns on, sound goes to it; when it turns off, it falls back to your speakers - and back again.
- **Hotkeys:** `Ctrl+Alt+PageUp` / `PageDown` = game/chat balance, `Ctrl+Alt+M` = mute mic - with an on-screen pop-up that shows over borderless games.
- **Noise removal:** one click to switch to NVIDIA Broadcast's noise-removed mic (free, RTX cards).
- **Discord-ready mic:** one click unmutes, sets a good level that stays put, and adds voice EQ + an 80 Hz rumble cut.
- **Keep my mic level:** if Discord's automatic gain, a game or Windows moves your mic level, it's put back.
- **Parametric EQ:** drag a curve like Sonar's - peaks, shelves, low/high cut, up to 10 bands, with presets for Valorant, CS2, Apex, Fortnite, CoD and more, plus mic presets (incl. QuadCast 2S).
- **Clean chat voices:** with an RTX card and NVIDIA Broadcast, one click on the Chat strip removes background noise and echo from your friends' voices.
- **First-run setup:** pick "With Sonar", "Replacing Sonar" or "Just a mixer" - it guesses your headset and mic, shows exactly what will change, then sets it up. Run it again from Settings.
- **Background mute:** an app can go quiet while you're in another app, and comes back when you switch to it.
- **Loud-sound guard:** sudden blasts (a flashbang, a scream in Chat) are turned down for a moment - off until you turn it on.
- **You're
- **Stream Deck and shortcuts:** `HallowMixer.exe --preset Gaming`, `--toggle-mic`, `--channel game 60`, `--media next` - Settings copies ready-made lines.
- **Quiet hours:** at night the volume is capped and Windows sounds are turned down, on your schedule.
- **Profiles:** whole mixer setups for different people or uses on one PC - switch from Settings, the tray or `--profile Name`.
- **Split headset:** on headsets with game and chat outputs (A50, Arctis), Chat goes to the chat side with a voice EQ so the headset's own dial balances them.
- **Auto mic level:** talk for 5 seconds and Hallow Mixer sets your mic so you're loud and clear, never distorted.
- **Keep my headset and mic:** if Windows moves your sound or mic to a monitor or a new USB device, Hallow Mixer moves it straight back (Settings).
- **Modern look:** Font Awesome icons on every button and channel, rounder cards, icon-only header buttons (hover for a tooltip).
- **Mic muted warning:** a clear bar at the top of the mixer when your mic is muted in Windows, with one click to unmute; and a reminder (one click to fix) when Hallow Mixer isn't set to start with Windows.
- **Mic presets per game:** in the Games window, give a game its own mic preset (QuadCast 2S, Discord-ready...) - it goes on when the game starts and your own mic EQ comes back when it closes. Hallow Overlay and Hallow Remote hear about mic, preset and profile changes right away.
- **One Hallow icon:** with Hallow Hub 2.4, Hallow Mixer's tray menu (mute mic, presets, Mini mixer, EQ) lives in Hub's icon and its own icon hides; it comes back by itself if Hub closes (Settings > Use the Hallow icon).
- **Mic chain:** a noise gate, compressor and limiter for your mic (EQ window) - Light, Streamer or Loud room, or your own settings; per game too. Hallow Mixer's own plugin, run by Equalizer APO.
- **Hallow accent:** the accent colour you pick in Hallow Toolkit colours Hallow Mixer's buttons and highlights (channel colours stay). Crash reports stay on your PC, and you're told next time if it ever closed unexpectedly.
- **Sidetone:** hear your own mic in your headset (Settings), about 25 ms behind and silent while you're muted.
- **Your EQ presets:** save your own, export them for a friend, import or paste AutoEQ / Equalizer APO text, and A/B compare (EQ window).
- **Quick output switching:** tray menu > Output or Ctrl+Alt+O, and each output remembers where every channel plays.
- **Smart Volume and loudness meter:** even out Game, Chat, Music and Media (each on its own), and see how loud what you hear is in LUFS.
- **App rules:** the Apps window (top bar) lists every app, running or not - pick its channel, output and mute ahead of time, add one by name, and make it always start at the same level (also right-click an app > When it starts).
- **Light and dark mode:** Hallow Mixer follows the Light / Dark / Same as Windows choice in Hallow Toolkit, live and without a restart - or pick its own in Settings > Look (Follow Hallow / Light / Dark). Channel colours stay; tooltips, right-click menus, scrollbars and the title bar follow too. In light mode every bit of text - status, accent-coloured text and text on accent buttons - reads at 4.5:1 or better, with any accent (3.26).
- **A tidy main window:** each channel keeps room for its apps (with real names like "Delta Force" and a slim slider each), the song that's playing sits in its own channel with play/pause and skip, and everything fits from a small window up to full screen.
- **Headphone space:** crossfeed for headphones (EQ window) - sound feels like speakers in front of you instead of inside your head.
- **Game profiles:** the Games window shows every game's saved levels and EQ - change them or forget a game.
- **Preset hotkeys:** Ctrl+Alt+1 to 9 switch presets from inside a game, Ctrl+Alt+0 goes to the next one.
- **Discord posts (with Hallow Toolkit):** if you turn them on, the songs you play in Music apps and your mic muted / back on can be posted to your Discord channel.
- **Smarter ducking:** only real talking in Chat turns your game down - join/leave sounds, pings and background hum are ignored.
- **Now playing:** the song or video that's playing, with previous / play-pause / next - as a card in its channel (Spotify in Music), in the Mini mixer and on your phone.
- **Mute Spotify ads (3.27):** while Spotify plays an ad, Hallow Mixer mutes Spotify - only Spotify - and unmutes it the moment the music is back; Spotify's card says "Ad muted". On by default (Settings > Mute Spotify ads), or lower ads to a level you pick instead. A Spotify you muted yourself stays muted. See [Spotify ads](#spotify-ads).
- **Quick presets (3.28):** Hallow Hub's menu ticks the preset you're using; Hub, Remote, Hallow Keys or a script can switch presets over the family pipe with `preset Chill`, `preset 2`, `preset next` or `preset prev` (`HallowMixer.exe --cmd "preset next"`). `/preset?name=` takes the same (any case).
- **Why the volume changed (3.29):** the **Why?** button by the status line lists everything Mixer turned down by itself since it started, newest first, and which setting did it: ducking for Chat (with who was talking) or for your mic, Smart Volume catching a loud moment, the loud-sound guard, an app going to the background, a game's saved levels coming back, quiet hours, a Spotify ad. The status line shows each one as it happens. The family pipe answers `why` (the last 60) and status.json carries `autoRecent` (the last 5) - channel and app names only, never what played.
- **Share your setup:** Hallow Toolkit's "Share your setup" file carries your presets, app choices, ducking, mic mode and EQ - a friend imports it in one click.
- **Hallow Toolkit + phone:** Hallow Toolkit installs and opens it, and its phone dashboard (over Tailscale) changes your levels, mutes and presets. Control is local to your PC and needs a key only your account can read.
- **Apps on your phone:** Hallow Remote and Hallow Hub can see every app in the mixer and change its level, mute it or move it to another channel - the same as its card in the window (see [Local API](#local-api)).
- **Stream (OBS):** a separate mix for your stream - each channel's level on stream, or keep it off stream; one click makes OBS sources for your apps; presets switch with your OBS scenes.
- **EQ follows the game:** change the headset EQ while a game runs and it comes back every time that game starts.
- **Even out Media:** loud ads and explosions in videos turn down, quiet talking comes up.
- **Updates:** checks GitHub when it starts and asks before installing; the download's checksum is verified.
- **Settings backup:** save and restore all levels, presets, EQ and app choices.
- **Tray app:** close the window and it keeps running by the clock; optional start with Windows. Exit puts every app back to full volume and its normal output.

## Install

1. Download **Hallow Mixer.zip** from [Releases](../../releases/latest)
2. Right-click the zip > Properties > tick **Unblock** > OK, then Extract All
3. Double-click **INSTALL.bat** - no admin needed, it installs to your user folder

Runs on the .NET Framework that comes with Windows 10 and 11. The app isn't code-signed, so Windows may say "Windows protected your PC" - click **More info > Run anyway**.

## Uninstall

Windows Settings > Apps > Hallow Mixer > Uninstall. Every app goes back to full volume and its normal output first.

## Good to know

- Hallow Mixer changes each app's own volume (the same thing Windows' volume mixer does), so it works with any headset or speakers, and alongside other audio software.
- EQ goes through Equalizer APO (install it once and tick your devices). Hallow Mixer can't create virtual sound devices.
- Settings live in `%APPDATA%\HallowMixer` (3.27: plus `status.json` for the other Hallow apps - the same as the pipe's `status`).

## Spotify ads

Hallow Mixer watches what Windows already knows about Spotify - its media session (the song info the volume pop-up shows) and the title of Spotify's window ("Artist - Song" while music plays, "Advertisement" or "Spotify" during an ad). An ad has to be seen for about a second before Spotify is muted, and real music (an artist and a normal track - even a song called "Advertisement") is never treated as one.

- **Only your own volume:** it changes Spotify's level in the Windows mixer, the same as clicking Spotify's icon in Hallow Mixer. No other app and not your system volume.
- **Spotify isn't modified:** no patching or changing Spotify's files, no blocking Spotify's network or ad servers, nothing to do with your Spotify account. The ads still play - you just don't hear them (or hear them quietly).
- `status` over the family pipe, `/state` and `%APPDATA%\HallowMixer\status.json` show `spotifyAdMuted` (true / false) and `adsMutedToday` (a count) - never what played.

## Local API

For Hallow Toolkit, Hallow Remote, Hallow Hub and Hallow Overlay. It listens on `127.0.0.1` only (port 5191-5199) and every request needs the `X-Hallow-Key` header; port and key are in `%APPDATA%\HallowMixer\api.json`, which only your Windows account can read. Requests from browsers (an `Origin` header) are refused.

| Request | What it does |
|---|---|
| `GET /state` | Everything at once (below) |
| `GET /wait?rev=<n>` | Long poll: answers with the state as soon as `rev` differs from `<n>`, or after 25 s |
| `GET /set?ch=game&v=60` (or `&d=-10`), `/mute?ch=game&m=toggle\|1\|0`, `/mic?mute=toggle\|1\|0`, `/preset?name=Chill` (3.28: any case, a number, `next` or `prev`), `/profile?name=..`, `/media?do=playpause\|next\|prev` | Channels, mic, presets, profiles, media keys |
| `POST /app` | One app (3.24) - JSON body, below |

Every path also works with `/api/` in front (`/api/state`, `/api/wait`, `/api/app`).

`/state` (and `/wait`) answer with `rev`, `version`, `channels`, `mic`, `chatMix`, `presets`, `preset`, `game`, `obs`, `nowPlaying`, `profile`, `profiles`, since 3.24 `apps`, since 3.27 `spotifyAdMuted` / `adsMutedToday`, and since 3.28 `gameMic` / `gameMicChain` (the running game's mic EQ preset / mic chain on your mic right now, `null` = your own) - `apps` is every app in the mixer window:

```json
"apps":[{"key":"spotify","name":"Spotify","channel":"music","volume":100,"muted":false,"playing":true}]
```

`key` is the program name in lower case (`system` = Windows sounds), `name` the friendly name the window shows, `channel` one of `game`, `chat`, `music`, `media`, `other`, `volume` the app's own level inside its channel (0-100), `muted` its own mute, `playing` whether one of its sound streams is playing right now (a paused player has a stream that isn't playing).

`POST /app` changes one app exactly like its card in the window - one or more of:

```
{"key":"spotify","volume":40}       // its slider, 0-100
{"key":"spotify","muted":true}      // clicking its icon (true or false)
{"key":"spotify","channel":"chat"}  // right-click > Move to (remembered)
```

It answers `{"ok":true,"msg":"Spotify 40%","rev":123}` and `rev` goes up, so long polls wake at once. A bad value changes nothing and answers 400 (`"ok":false` with the reason); an app that isn't in the mixer answers 404.

## Roadmap

See [ROADMAP.md](ROADMAP.md) - what's done and what's coming next.

## Build it yourself

```powershell
.\build.ps1
```

Uses the C# compiler that ships with Windows (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) - no SDK or Visual Studio needed. Source is in `src/`: `Native.cs` (Windows Core Audio), `Engine.cs` (channels, volume math, settings), `App.cs` (window, tray, hotkeys), `Theme.xaml` (look).

Part of the Hallow family alongside [Hallow Toolkit](https://github.com/Dom428/hallow-toolkit).

## Credits

Icons: [Font Awesome Free](https://fontawesome.com) 7.3.1 by Fonticons, Inc., licensed [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) (see FontAwesome-LICENSE.txt). Only the icon outlines Hallow Mixer uses are included.

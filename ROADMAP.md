# Hallow Mixer roadmap

The goal: everything SteelSeries Sonar does for a gamer - and the things it doesn't - in a tiny, free app that works with any headset.

## Done

| Version | What it added |
|---|---|
| **1.0** | Channels (Game, Chat, Music, Media, System) with faders, mute, meters and output device; apps sort themselves; game/chat balance; mic strip; master output; presets; hotkeys; tray |
| **1.1** | Auto-ducking (chat talks -> game and music dip); levels per game; per-app mute; app icons; mouse wheel; on-screen pop-up; settings panel |
| **1.2** | Mini mixer (always on top); headset first (auto-switch when the headset turns on/off) |
| **1.3** | 10-band EQ for every headset, speaker and mic with presets, through Equalizer APO |
| **1.4** | Volume of the app in front (Ctrl+Alt+Up/Down); per-app output (Play on); mic test (record + play back) |
| **1.5** | Push-to-talk / push-to-mute; window remembers size and place; presets you can rename, add and delete |
| **1.6** | Noise removal via NVIDIA Broadcast (one click); auto-update from GitHub with checksum check; uninstall entry in Windows Settings > Apps (resets every app first); settings backup and restore |
| **1.7** | Discord-ready mic (one click); keep my mic level; mic low cut (real high-pass filter); Discord-ready mic EQ preset |

## Next


### 1.8 - Hear better
- EQ follows the game: each game remembers its EQ like it remembers its levels
- Quick switch for Windows spatial sound (Windows Sonic / Dolby Atmos if installed)
- Loudness levelling for the Media channel (quiet dialogue, loud explosions)
- Mic noise gate and limiter: Equalizer APO has none built in, so this needs its own audio filter (NVIDIA Broadcast covers background noise until then)

### 1.9 - Streamers
- OBS link (obs-websocket, built into OBS): Hallow Mixer channels drive OBS audio sources
- Stream mix vs. your mix: keep music out of the stream, or chat at a different level
- Scene-aware presets (Just Chatting / Gaming / BRB)

### 2.0 - Hallow family
- Hallow Toolkit: install and open Hallow Mixer from the Apps tab; settings travel with "Share your setup"
- Phone dashboard (over Tailscale): see and change channel levels from your phone
- Hallow Discord bot: post "now playing" and mic status if you want it

### 2.1 - Ready for everyone
- First-run setup: "Using Sonar / replacing Sonar / just a mixer" - sets channels, outputs and mic for you

## How it's built
- Everything you see and control is Hallow Mixer's own code. The one outside piece is Equalizer APO, the free open-source hook that lets EQ run inside Windows' audio engine; noise removal uses NVIDIA Broadcast if you have an RTX card.

## Not possible without a driver
- New virtual sound devices (like Sonar's Game/Chat/Media devices). Hallow Mixer sends each channel to a real device instead - e.g. A50 Game and A50 Voice for its hardware game/voice balance.

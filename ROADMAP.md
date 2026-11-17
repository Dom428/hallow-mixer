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

## Next

### 1.5 - Controls
- **Push-to-talk / push-to-mute** for every app at once (hold a key or a mouse side button)
- Window remembers its size and place
- Rename presets and add more of them

### 1.6 - Mic pro (with Equalizer APO)
- **Noise removal** for the mic (open-source RNNoise, Sonar's ClearCast equivalent)
- Noise gate (silence between words) and a limiter (no blown-out shouts)
- One-click "Discord-ready mic" chain: rumble cut + noise removal + clear-voice EQ

### 1.7 - Hear better
- EQ follows the game: each game remembers its EQ like it remembers its levels
- Quick switch for Windows spatial sound (Windows Sonic / Dolby Atmos if installed)
- Loudness levelling for the Media channel (quiet dialogue, loud explosions)

### 1.8 - Streamers
- OBS link (obs-websocket, built into OBS): Hallow Mixer channels drive OBS audio sources
- Stream mix vs. your mix: keep music out of the stream, or chat at a different level
- Scene-aware presets (Just Chatting / Gaming / BRB)

### 1.9 - Hallow family
- Hallow Toolkit: install and open Hallow Mixer from the Apps tab; settings travel with "Share your setup"
- Phone dashboard (over Tailscale): see and change channel levels from your phone
- Hallow Discord bot: post "now playing" and mic status if you want it

### 2.0 - Ready for everyone
- Auto update from GitHub releases (asks first)
- First-run setup: "Using Sonar / replacing Sonar / just a mixer" - sets channels, outputs and mic for you
- Proper uninstall entry in Windows Settings > Apps
- Settings backup and restore

## Not possible without a driver
- New virtual sound devices (like Sonar's Game/Chat/Media devices). Hallow Mixer sends each channel to a real device instead - e.g. A50 Game and A50 Voice for its hardware game/voice balance.

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
| **1.8** | EQ follows the game; even out Media (loudness levelling); settings panel scrolls |
| **1.9** | Parametric EQ with a curve you drag (peak, shelves, low/high cut; up to 10 bands); 14 output presets incl. per-game (Valorant, CS2, Apex, Fortnite, CoD, R6, PUBG, Tarkov, OW2...) and 5 mic presets (incl. QuadCast 2S) |

| **2.0** | Streamers: OBS link (obs-websocket v5, password stored encrypted); stream mix per channel (level + on/off stream); "Set up OBS for me" makes an Application Audio Capture source per app; presets per OBS scene; connects by itself when OBS opens |
| **2.1** | Hallow family: Hallow Toolkit 4.5 installs/opens Hallow Mixer; its phone dashboard changes channel levels, mute, mic and presets; local control API (127.0.0.1 only, with a key) |
| **2.2** | Ready for everyone: first-run setup - "With Sonar / Replacing Sonar / Just a mixer" - guesses your headset, fallback and mic, shows what will change, then sets channels, outputs and mic; "Run setup again" in Settings |

## Next

### 2.3 - Clean chat voices
- One click sends the Chat channel through NVIDIA Broadcast's speaker effects (background noise and echo removed from your friends' voices), when you have an RTX card

### Later - Hallow family, part 2
- Hallow Mixer settings travel with the Toolkit's "Share your setup"
- Hallow Discord bot: post "now playing" and mic status if you want it

### Later - closing the gap to Sonar
- 360 spatial audio (virtual surround) through Equalizer APO's convolution filter - needs 3D sound profiles we're allowed to ship

- Mic noise gate, compressor and limiter: Equalizer APO has none built in, so this needs our own audio plugin
- Quick switch for Windows spatial sound (Windows Sonic / Dolby Atmos): Windows has no public way for apps to change it, so this needs research first

## How it's built
- Everything you see and control is Hallow Mixer's own code. The one outside piece is Equalizer APO, the free open-source hook that lets EQ run inside Windows' audio engine; noise removal uses NVIDIA Broadcast if you have an RTX card.

## Not possible without a driver
- New virtual sound devices (like Sonar's Game/Chat/Media devices). Hallow Mixer sends each channel to a real device instead - e.g. A50 Game and A50 Voice for its hardware game/voice balance.

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
| **2.3** | Clean chat voices: "Clean voices" on the Chat strip sends Chat through NVIDIA Broadcast's speaker effects (noise and echo removed from friends' voices); turns itself off and puts Chat back if NVIDIA Broadcast goes away |
| **2.4** | Hallow family, part 2: settings travel with Hallow Toolkit 4.6's "Share your setup" - presets, app choices, ducking, mic mode and headset/mic EQ (checked on import; EQ lands on your own real headset and mic, never Sonar's virtual devices) |
| **2.5** | Now playing: the song or video that's playing (from Windows' media sessions) with previous / play-pause / next in the mixer and the Mini mixer, coloured by the app's channel; on the phone dashboard through Hallow Toolkit 4.7; Settings switch |
| **2.6** | Smarter ducking: only real talking ducks (half a second of sound with three or more syllables); join/leave chimes, pings, key clicks and steady background hum are ignored; stays down between words, fades back about a second after talking stops; Settings switch to go back to the old way |
| **2.7** | Hallow family, part 3: now playing tells Hallow Toolkit which channel the app is in, so Toolkit 4.8's Discord posts can share songs from Music apps (never browser videos) and mic muted / back on - both off until you turn them on |
| **2.8** | Preset hotkeys: Ctrl+Alt+1 to 9 for your presets, Ctrl+Alt+0 for the next one, with the on-screen pop-up; skips keys that type a character on AltGr keyboards (German, French...) or that another app uses; Settings switch |
| **2.9** | Game profiles: a Games window lists every game Hallow Mixer remembers, with its five channel levels (change them, or take the mixer's current ones) and its EQ; forget a game's EQ or the whole game; the running game moves right away |
| **3.0** | Headphone space: crossfeed for headphones in the EQ window (Light / Medium / Strong, the bs2b levels) - bass and low-mids from one side reach both ears like speakers in a room, treble keeps its width, centre sounds untouched; mid/side through Equalizer APO's own Copy and shelf filters (only the device's L and R) |
| **3.1** | Background mute: right-click an app > "When I'm in another app" > keep playing / turn down to 20% / mute; it comes back the moment you switch to it (checks the window in front 4 times a second, only when an app uses it) |
| **3.2** | Loud-sound guard (Settings, off by default): a sudden blast far above what a channel was just playing (3.5x its recent level, out of a quiet moment) turns that channel down to 35% at once, holds 0.6 s and eases back over ~2 s; loud gameplay and music swells are left alone |
| **3.3** | "You're muted" warning: while the mic is muted in Windows (not push-to-talk), every mic is listened to - the real one behind Sonar too - with the same voice detector as ducking; real talking shows "You're muted - Ctrl+Alt+M to unmute" on screen (at most every 20 s); Settings switch |
| **3.4** | Shortcuts for Stream Deck and scripts: `--preset`, `--channel <ch> 60 / +10`, `--mute / --unmute / --toggle-mute <ch>`, `--mute-mic / --unmute-mic / --toggle-mic`, `--media playpause / next / prev`; sent to the running mixer through its local API, or run at start; Settings > "Shortcuts for Stream Deck..." copies ready-made lines |

## Next

### 3.5 - Quiet hours
- At night: a cap on master volume and Windows sounds turned down, on a schedule you pick

### Fixes from testing at home
- Whatever comes up on the real A50, QuadCast 2S, Equalizer APO, OBS and Discord


### Later - closing the gap to Sonar
- 360 spatial audio (true virtual surround) through Equalizer APO's convolution filter - needs 3D sound profiles (HRTFs) we're allowed to ship, and Equalizer APO's convolution can't mix one ear into the other on its own. Headphone space (3.0) covers the "speakers in a room" part
- Mic noise gate, compressor and limiter: Equalizer APO has none built in, so this needs our own audio plugin
- Quick switch for Windows spatial sound (Windows Sonic / Dolby Atmos): Windows has no public way for apps to change it, so this needs research first

## How it's built
- Everything you see and control is Hallow Mixer's own code. The one outside piece is Equalizer APO, the free open-source hook that lets EQ run inside Windows' audio engine; noise removal uses NVIDIA Broadcast if you have an RTX card.

## Not possible without a driver
- New virtual sound devices (like Sonar's Game/Chat/Media devices). Hallow Mixer sends each channel to a real device instead - e.g. A50 Game and A50 Voice for its hardware game/voice balance.

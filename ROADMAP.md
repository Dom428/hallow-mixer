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
| **3.5** | Quiet hours (Settings, off by default): pick from / to (wraps past midnight) and a cap (20-70%); while on, the master volume can't go above the cap (lower is fine) and Windows sounds are halved; pop-up when they start and end |
| **3.6** | Profiles: whole mixer setups for different people or uses on one PC (Settings and tray menu; New / Delete; `--profile Name` shortcut); switching restarts Hallow Mixer cleanly - fixes a race where a restart (also Restore settings) could exit instead of reopening |
| **3.7** | Split headset: finds headsets that show up as two outputs ("A50 Game" + "A50 Voice", "Arctis ... Game" + "... Chat"); one click on the Chat strip sends Chat to the voice/chat output with a Voice clarity EQ (keeps yours if you have one), so the headset's own game/chat dial balances games against Discord; one at a time with Clean voices |
| **3.8** | Auto mic level: "Set my level for me" on the Mic strip records 5 s of talking, finds where your voice really peaks (90th percentile of 50 ms blocks, so a knock can't fool it) and sets the mic in dB so speech peaks around -7 dB; tells you to use Mic Boost if it's still too quiet at 100% |
| **3.9** | What's new: a short pop-up once after each update with the versions you skipped (never on a new install); the local API reports profiles, so Hallow Toolkit 4.9's phone dashboard can switch them |
| **3.10** | New icon: three channel faders in the mixer's colours, drawn separately for every size (16 px tray = plain level bars) |
| **3.11** | Keep my headset and mic: Headset first now also undoes Windows switching sound or the mic to a device that just appeared (monitor waking up, USB plugged in), and keeps your mic as the default |
| **3.12** | Modern look: Font Awesome icons on every button (picked from the label, so "Mute"/"Muted" swap icons too), icon badges and a colour glow on each channel card, icon-only header buttons with tooltips, rounder controls |
| **3.13** | Start with Windows notice: the main window says so when Hallow Mixer isn't in your start-up list (after a restart the phone controls and the Overlay's mic warning were gone), with one-click Start with Windows or Don't show again; mic muted warning: a clear bar at the top when your mic is muted in Windows, with Unmute mic - the Mic strip's button now follows mutes from Windows or other apps |
| **3.14** | Hallow family, part 4: `GET /wait?rev=<n>` (also `/api/wait`) long poll - answers the moment the mic, a mute, a channel, the preset, profile, game or now playing changes (or after 25 s), so Hallow Overlay and Hallow Remote stop asking every second; requests run side by side; family pipe `HallowFamily.Mixer.<user>` (current user only) answers `ping` / `status`, and `--cmd "<line>"`; mic presets per game in the Games window (any of the 11 mic EQ presets while the game runs, your own mic EQ back when it closes - also after a restart or exit) |
| **3.15** | One Hallow icon: family pipe `open`, `menu` (mute mic, top presets, Mini mixer, Equalizer, next profile), `invoke <id>` and `quit`, so Hallow Hub 2.4 shows Hallow Mixer's menu in its own tray icon; "Use the Hallow icon" (Settings, on by default) hides Mixer's icon while Hub answers `tray-host` with on:true (checked every 5 s) and shows it again if Hub stops, says no or doesn't answer; balloons go through Hub's `notify` while hidden (own icon for a moment if Hub fails); pipe `status` reports `hallowIcon` |
| **3.16** | Mic chain: noise gate (threshold, attack, hold, release, range) -> compressor (threshold, ratio, attack, release, makeup, soft knee) -> lookahead limiter (1.5 ms, ceiling, release), per channel, in our own x64 VST2 plugin HallowMicChain (C, own ABI header, no allocations or locks while processing, any sample rate, denormals flushed) that Equalizer APO runs after the mic's EQ; built into HallowMixer.exe and copied to C:\ProgramData\HallowMixer\plugins (no admin; only your account can change it, the audio service can read it); EQ window > your mic: on/off, Off / Light / Streamer / Loud room, sliders per stage; per-game mic chain in the Games window; travels with Share your setup |
| **3.17** | Hallow accent and crash reports: the family accent colour from Hallow Toolkit (`%APPDATA%\Hallow\theme.json`) on buttons, titles, highlights, meters and EQ curves - live (file watcher, 300 ms), channel colours stay, a bad or missing file means Hallow Mixer's own orange; Settings "Use the Hallow accent"; crash reports kept on this PC (`%APPDATA%\HallowMixer\crashes`, newest 20; app, version, Windows, exception and stack only), a notice on the next start (Show details / Open folder / OK), `lastCrash` in the family pipe's status |
| **3.18** | Sidetone: hear your own mic in your headset - WASAPI shared mode, event-driven capture (one ~10 ms engine period, raw mode where the mic allows it, so no EQ or mic chain) written straight to the default output with a 5 ms cushion and old audio dropped so the delay can't grow (measured: 10 + 5 + 10 = ~25 ms, no underruns); Settings: on/off (off by default), level, which mic; silent while the mic is muted or push-to-talk is up; restarts when the default devices change; not Windows' "Listen to this device" |
| **3.19** | EQ presets you can share: "Mine" row in the EQ window - Save as, Export (Hallow preset .json or Equalizer APO text), Import (Hallow preset, AutoEQ ParametricEQ.txt / GraphicEQ.txt, Equalizer APO Filter: lines) and Paste the same text from the clipboard (peak, shelves, low/high cut; first 10 filters; values checked; the preamp is set for you); right-click a preset to export or delete it; A/B switches between your changes and how the EQ was when you opened it |
| **3.20** | Quick output switching: tray menu > Output lists every output (the one in use ticked) and Ctrl+Alt+O goes to the next one, with the on-screen pop-up (skipped on keyboards where it types a character; Settings switch); each output remembers which output every channel plays on (A50: Chat on A50 Voice; speakers: everything on the default) and puts it back when Windows' default output changes, by you, Headset first or anything else; Chat is left to Split headset / Clean voices while they're on |
| **3.21** | Smart Volume everywhere and a loudness meter: Settings > Smart Volume per channel - Game (x0.45-1.25, keeps its punch), Chat (x0.4-1.6, lifts quiet friends), Music and Media - the same levelling Media had; loudness meter in the status line: LUFS (ITU-R BS.1770 K-weighting, 0.4 s momentary, 3 s short-term in the tooltip) of what you hear, read from Windows' loopback of the default output only while the window is open (Settings switch) |
| **3.22** | App rules: an Apps window (top bar) lists every app Hallow Mixer remembers - running or not - with its channel (or Automatic), "Starts at" (where you left it, or always 0-100%), its own output, what it does while you're in another app, and mute; add an app by program name before it ever plays; Forget puts it back to automatic; right-click an app > When it starts; the start level is set when the app appears while Hallow Mixer runs, or at Hallow Mixer's start only for apps that started with it (not after a restart or profile switch) |
| **3.23** | Main window redesign: each column is header / fader with its level meter beside it (grows up to 230 px) / % / mute + output on one row / the app list, which always keeps room for about three apps and scrolls with a thin styled scrollbar; apps as compact cards (icon - click to mute, name with ellipsis and tooltip, level, slim slider, greyed and struck through when muted); friendly names from the program's description, else the file name cleaned up ("DeltaForceClient-Win64-Shipping" -> "Delta Force"); now playing as a card in the channel its app plays in (by name when the app has no sound stream, e.g. paused Spotify -> Music, and the channel says "Spotify paused" instead of "No apps playing"); Chat's Clean voices / Split headset as icon buttons, the mic's Test / Auto level / Discord-ready / Noise removal in the mic column's list area; the start-with-Windows and muted-mic notices are one slim line; the header's output list narrows in a small window; apps whose path Windows won't give (anti-cheat games, admin apps) are named by their process instead of "pid1234"; --snap uses made-up apps and adds main.png at 974x733 and main-1280.png, plus client, smallest and maximized sizes |
| **3.24** | Per-app volumes for your phone: the local API's `GET /state` and `/wait` list `apps` - every app in the mixer with key, friendly name, channel, its own level, mute and whether it's playing (Windows session state "active"); `POST /app` with `{"key","volume"}`, `{"key","muted"}` or `{"key","channel"}` does exactly what the app's card does (slider, icon click, Move to), checks every value before changing anything (400 / 404 with the reason), and bumps `rev` so long polls wake at once; same key and 127.0.0.1-only rules; README documents the whole API; for Hallow Remote and Hallow Hub |

## Next

### Fixes from testing at home
- Whatever comes up on the real A50, QuadCast 2S, Equalizer APO, OBS, Stream Deck and Discord

### Later
- Volume schedule for kids' accounts: quiet hours (3.5) and profiles (3.6) cover most of it; a PIN lock would need care so it can't lock you out


### Later - closing the gap to Sonar
- Soundboard into the mic: needs a virtual mic device (a driver) or a live, connected Equalizer APO to mix clips in safely
- Automatic headset EQ profiles: needs a downloaded database of headphone measurements
- 360 spatial audio (true virtual surround) through Equalizer APO's convolution filter - needs 3D sound profiles (HRTFs) we're allowed to ship, and Equalizer APO's convolution can't mix one ear into the other on its own. Headphone space (3.0) covers the "speakers in a room" part
- Quick switch for Windows spatial sound (Windows Sonic / Dolby Atmos): Windows has no public way for apps to change it, so this needs research first

## Gaps vs Sonar
What SteelSeries Sonar (and Voicemeeter / EarTrumpet for parts) do that Hallow Mixer doesn't yet - buildable here or not, and why.

| Gap | From | Buildable here? |
|---|---|---|
| Save, import and share your own EQ presets (Sonar's preset sharing; AutoEQ text) | Sonar | Yes - 3.19 |
| A/B compare in the EQ | Sonar | Yes - 3.19 |
| Switch the output device from the tray or a hotkey | EarTrumpet, GG | Yes - 3.20 |
| Channel outputs remembered per headset | Sonar | Yes - 3.20 |
| Smart Volume on every channel (Game, Chat, Music - not only Media) | Sonar | Yes - 3.21 |
| Loudness meter (LUFS) | Voicemeeter-style meters | Yes - 3.21 |
| App rules: every app's channel, output and start level in one place, also for apps that aren't running (an app always starts at X%) | Sonar, EarTrumpet | Yes - 3.22 |
| Per-app volumes from your phone | Sonar mobile app | Yes - 3.24 |
| Virtual Game / Chat / Media / Aux / Mic devices and buses | Sonar, Voicemeeter | No - needs a signed audio driver |
| ClearCast AI noise cancellation (mic and chat) without an RTX card | Sonar | No - needs a trained AI model; NVIDIA Broadcast covers RTX cards |
| 360 spatial audio | Sonar | No - needs licensed HRTFs and cross-ear convolution Equalizer APO can't do (Headphone space covers part) |
| Mic chain gain-reduction meter | Sonar | Not safely - the plugin runs inside the audio service (session 0), so it needs a cross-session channel that can't be tested without a connected Equalizer APO |
| Network audio (VBAN), recorder, macro buttons | Voicemeeter | VBAN: no (network audio is out of scope); recorder: OBS covers it; macros: Stream Deck shortcuts (3.4) cover it |
| Soundboard into the mic | Voicemeeter, others | No - needs a virtual mic or a live, connected Equalizer APO (see Later) |
## How it's built
- Everything you see and control is Hallow Mixer's own code. The one outside piece is Equalizer APO, the free open-source hook that lets EQ run inside Windows' audio engine; noise removal uses NVIDIA Broadcast if you have an RTX card.

## Not possible without a driver
- New virtual sound devices (like Sonar's Game/Chat/Media devices). Hallow Mixer sends each channel to a real device instead - e.g. A50 Game and A50 Voice for its hardware game/voice balance.

HALLOW MIXER - one fader per kind of sound: Game, Chat, Music, Media, System, plus your mic

HOW TO INSTALL
1. Right-click the zip > Properties > tick "Unblock" > OK
2. Right-click the zip > Extract All
3. Double-click INSTALL.bat (no admin needed - it installs to your user folder)
If Windows shows "Windows protected your PC", click "More info" > "Run anyway".
Nothing else to install: it runs on the .NET Framework that comes with Windows 10 and 11.

WHAT IT DOES
- Every app that plays sound lands in a channel by itself: games (from your Steam, Xbox, Epic,
  Riot... folders), Discord/TeamSpeak in Chat, Spotify in Music, browsers and video players in Media.
  Right-click an app to move it - it's remembered.
- Each channel: a fader, mute, a live level meter, and which speakers/headset it plays on.
  Each app also has its own small slider inside the channel.
- Game / chat balance: drag toward CHAT to turn the game down while you talk.
- Mic: level, mute and live meter, and which mic Windows uses.
- Presets: click to use; right-click to save, rename or delete; + to add your own (up to 8).
- Push-to-talk / push-to-mute (Settings > Mic): hold a key or mouse side button - works for every app.
- Auto-ducking: when someone talks in Chat, game, music and media dip and fade back after.
  Optionally music dips while you talk. (Settings button, top right.)
- Levels per game: each game gets its own levels - they come back when it starts.
- Click an app's icon to mute just that app. Scroll over a channel to change it.
- Ctrl+Alt+Up / Down: volume of just the game or app you're in.
- Right-click an app > Play on: send one app somewhere other than its channel.
- Mic > Test mic: records 5 seconds and plays it back so you hear what others hear.
- Mini mixer (button at the top, or the tray menu): small always-on-top panel with every channel.
- EQ (button at the top): 10-band EQ for each headset, speaker and mic, with presets (Bass boost,
  Footsteps, Voice clarity, Movie, Music; mic: Clear voice, Warm...). Needs the free Equalizer APO.
- Headset first (Settings): headset on = sound goes to it, headset off = your speakers.
- Hotkeys: Ctrl+Alt+PageUp / PageDown = game/chat balance, Ctrl+Alt+M = mute mic,
  with an on-screen pop-up that shows over borderless games.
- Mic > Get noise removal / Use NVIDIA Broadcast mic: noise removal with NVIDIA Broadcast (RTX cards).
- Mic > Discord-ready mic: one click for a good, steady level plus voice EQ and rumble cut.
- Settings > Keep my mic level: puts your mic level back if Discord or a game changes it.
- EQ > a mic > Low cut: removes rumble, desk bumps and fan hum.
- EQ > Parametric EQ: drag the dots on the curve; presets for many games and for mics (QuadCast 2S).
- Clean voices (Chat strip): with an RTX card, sends Chat through NVIDIA Broadcast to remove noise from your friends' voices.
- Setup: the first time it opens, pick With Sonar / Replacing Sonar / Just a mixer (Settings > Run setup again to redo it).
- Right-click an app > When I'm in another app: keep playing, turn down to 20% or mute.
- Settings > Loud-sound guard: sudden blasts are turned down for a moment.
- Talk while muted and a pop-up tells you (Settings to turn off).
- Stream Deck / shortcuts: HallowMixer.exe --preset Gaming, --toggle-mic, --channel game 60 (Settings > Shortcuts for Stream Deck).
- Settings > Quiet hours: a volume cap and quieter Windows sounds at night.
- Profiles (Settings or tray): whole setups for different people or uses on one PC.
- EQ > Headphone space: crossfeed for headphones (Light / Medium / Strong).
- Games (top bar): every game's saved levels and EQ - change them or forget a game.
- Ctrl+Alt+1 to 9 = your presets, Ctrl+Alt+0 = next preset.
- Ducking only reacts to real talking (Settings > "Only for real talking" to change it).
- Now playing: the song or video that's playing appears under the mixer (and in Mini) with play/pause and skip.
- Share: Hallow Toolkit's "Export my setup" includes your mixer presets, app choices and EQ; "Import a setup" brings a friend's in.
- Phone: Hallow Toolkit's phone dashboard shows Mixer controls while Hallow Mixer runs (turn it off in Settings if you like).
- Stream (top bar): link OBS (OBS > Tools > WebSocket Server Settings > Enable), then set each channel's stream level, or click Set up OBS for me. Pick a preset for each OBS scene.
- Settings > EQ follows the game: each game remembers its headset EQ.
- Settings > Even out Media: loud ads and explosions down, quiet talking up.
- Updates: it checks GitHub when it starts and asks first. Settings > Back up / Restore settings.
- Closing the window keeps it running by the clock (your levels stay on). Tray menu:
  presets, mute mic, Start with Windows, Exit.
- Exit puts every app back to full volume and its normal output.

USING STEELSERIES SONAR?
Hallow Mixer works alongside it: it controls each app's volume even when the app plays through
Sonar. EQ works through Equalizer APO instead of Sonar's own. Hallow Mixer can't make virtual
sound devices - it sends each channel to a real device instead.

UNINSTALL
Windows Settings > Apps > Hallow Mixer > Uninstall. Every app goes back to full volume and its
normal output first. You can keep or delete your settings.
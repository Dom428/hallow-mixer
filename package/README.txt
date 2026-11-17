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
- Presets: Gaming, Movie, Chill. Click to use, right-click to save your current levels into one.
- Auto-ducking: when someone talks in Chat, game, music and media dip and fade back after.
  Optionally music dips while you talk. (Settings button, top right.)
- Levels per game: each game gets its own levels - they come back when it starts.
- Click an app's icon to mute just that app. Scroll over a channel to change it.
- Mini mixer (button at the top, or the tray menu): small always-on-top panel with every channel.
- Headset first (Settings): headset on = sound goes to it, headset off = your speakers.
- Hotkeys: Ctrl+Alt+PageUp / PageDown = game/chat balance, Ctrl+Alt+M = mute mic,
  with an on-screen pop-up that shows over borderless games.
- Closing the window keeps it running by the clock (your levels stay on). Tray menu:
  presets, mute mic, Start with Windows, Exit.
- Exit puts every app back to full volume and its normal output.

USING STEELSERIES SONAR?
Hallow Mixer works alongside it: it controls each app's volume even when the app plays through
Sonar. Hallow Mixer can't make virtual sound devices or EQ (that needs a driver), so keep Sonar
for those.

UNINSTALL
Exit it from the tray, untick "Start with Windows" first if you turned it on, then delete
%LOCALAPPDATA%\Programs\Hallow Mixer and the two "Hallow Mixer" shortcuts.
Your settings are in %APPDATA%\HallowMixer.

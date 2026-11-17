# Hallow Mixer installer: copies the app to your user folder (no admin needed) and adds Start menu + Desktop shortcuts.
$ErrorActionPreference = 'Stop'
$dest = Join-Path $env:LOCALAPPDATA 'Programs\Hallow Mixer'
New-Item -ItemType Directory -Force $dest | Out-Null
Get-Process HallowMixer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Copy-Item (Join-Path $PSScriptRoot 'HallowMixer.exe') $dest -Force
Copy-Item (Join-Path $PSScriptRoot 'README.txt') $dest -Force -ErrorAction SilentlyContinue
$exe = Join-Path $dest 'HallowMixer.exe'
$w = New-Object -ComObject WScript.Shell
foreach ($dir in [Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('Desktop')) {
    $s = $w.CreateShortcut((Join-Path $dir 'Hallow Mixer.lnk'))
    $s.TargetPath = $exe; $s.WorkingDirectory = $dest; $s.Description = 'Per-app volume mixer: game, chat, music, media'; $s.Save()
}
Start-Process $exe
Write-Host "Hallow Mixer is installed and open. It's in your Start menu and on your Desktop."

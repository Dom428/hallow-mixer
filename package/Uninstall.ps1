# Hallow Mixer uninstaller (run from Windows Settings > Apps, or directly).
# Puts every app back to full volume and Windows' normal output first, then removes the app.
$dest = Join-Path $env:LOCALAPPDATA 'Programs\Hallow Mixer'
$exe = Join-Path $dest 'HallowMixer.exe'
Add-Type -AssemblyName System.Windows.Forms
$keep = [System.Windows.Forms.MessageBox]::Show("Uninstall Hallow Mixer?`n`nEvery app goes back to full volume and its normal output.`n`nKeep your settings (levels, presets, EQ) in case you reinstall?", 'Uninstall Hallow Mixer', 'YesNoCancel', 'Question')
if ($keep -eq 'Cancel') { return }
Get-Process HallowMixer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600
if (Test-Path $exe) { Start-Process $exe -ArgumentList '--reset' -Wait }
# EQ file for Equalizer APO: empty it so no EQ stays on
$eq = Join-Path $env:ProgramData 'HallowMixer\eq.txt'
if (Test-Path $eq) { Set-Content $eq '# Hallow Mixer was uninstalled - no EQ.' -Encoding UTF8 }
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'HallowMixer' -ErrorAction SilentlyContinue
foreach ($dir in [Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('Desktop')) { Remove-Item (Join-Path $dir 'Hallow Mixer.lnk') -ErrorAction SilentlyContinue }
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HallowMixer' -Recurse -ErrorAction SilentlyContinue
if ($keep -eq 'No') { Remove-Item (Join-Path $env:APPDATA 'HallowMixer') -Recurse -Force -ErrorAction SilentlyContinue }
# the folder holds this script, so remove it a moment after we exit
Start-Process cmd.exe -ArgumentList "/c timeout /t 2 /nobreak >nul & rmdir /s /q `"$dest`"" -WindowStyle Hidden
[System.Windows.Forms.MessageBox]::Show('Hallow Mixer is uninstalled.', 'Hallow Mixer') | Out-Null

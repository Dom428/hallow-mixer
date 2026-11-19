# Builds plugin\bin\HallowMicChain.dll (x64 VST2 for Equalizer APO) and the offline test host, with LLVM-MinGW.
#   .\plugin\build-plugin.ps1 [-Test]      (-Test also runs the offline tests)
param([switch]$Test, [string]$Llvm = "$env:LOCALAPPDATA\HallowDev\llvm-mingw-20261117-ucrt-x86_64")
$ErrorActionPreference = 'Stop'
$cc = Join-Path $Llvm 'bin\x86_64-w64-mingw32-clang.exe'
if (-not (Test-Path $cc)) { throw "LLVM-MinGW not found at $Llvm" }
$src = $PSScriptRoot; $bin = Join-Path $src 'bin'; New-Item -ItemType Directory -Force $bin | Out-Null
$dll = Join-Path $bin 'HallowMicChain.dll'
# static: no libgcc/libunwind/libc++ DLLs; only Windows' own KERNEL32 and the Universal CRT (part of Windows 10/11)
& $cc -O2 -std=c11 -Wall -Wextra -Wno-unused-parameter -msse2 -shared -static -s `
    -o $dll (Join-Path $src 'HallowMicChain.c') '-Wl,--no-insert-timestamp'
if ($LASTEXITCODE -ne 0) { throw 'plugin build failed' }
"Built $dll ($([math]::Round((Get-Item $dll).Length / 1KB)) KB)"
$testExe = Join-Path $bin 'chaintest.exe'
& $cc -O2 -std=c11 -Wall -static -o $testExe (Join-Path $src 'chaintest.c')
if ($LASTEXITCODE -ne 0) { throw 'test host build failed' }
"Imports:"; & (Join-Path $Llvm 'bin\llvm-readobj.exe') --coff-imports $dll | Select-String 'Name:' | Sort-Object -Unique
"Exports:"; & (Join-Path $Llvm 'bin\llvm-readobj.exe') --coff-exports $dll | Select-String 'Name:'
if ($Test) { & $testExe $dll; if ($LASTEXITCODE -ne 0) { throw "offline tests failed ($LASTEXITCODE)" } }

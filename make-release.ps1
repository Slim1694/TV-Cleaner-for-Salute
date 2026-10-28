# Builds, runs the tests and packs release\TVCleaner.zip (exe + packages.json + README + LICENSE + devmenu.apk).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& "$root\build.ps1"
& "$root\bin\TVCleaner.Tests.exe"
if ($LASTEXITCODE) { throw 'tests failed' }
$stage = "$root\release\TVCleaner"
if (Test-Path "$root\release") { Remove-Item "$root\release" -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item "$root\bin\TVCleaner.exe", "$root\packages.json", "$root\README.md", "$root\LICENSE", "$root\devmenu\devmenu.apk" $stage
Compress-Archive -Path "$stage\*" -DestinationPath "$root\release\TVCleaner.zip"
Get-Item "$root\release\TVCleaner.zip" | Select-Object Name, Length

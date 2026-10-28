# Builds TVCleaner.exe, TVCleaner.Tests.exe and TVCleaner.Probe.exe with the csc.exe built into Windows.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$out  = "$root\bin"
New-Item -ItemType Directory -Force $out | Out-Null
$refs = '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
        '/r:System.Web.Extensions.dll', '/r:System.Numerics.dll'

$all = @(Get-ChildItem "$root\src" -Recurse -Filter *.cs | % FullName)
$lib = @($all | ? { $_ -notmatch '\\src\\UI\\' -and $_ -notmatch '\\src\\Program\.cs$' })

& $csc /nologo /codepage:65001 /target:winexe /optimize+ "/out:$out\TVCleaner.exe" $refs $all
if ($LASTEXITCODE) { throw 'app build failed' }

$tests = @(Get-ChildItem "$root\tests" -Filter *.cs | % FullName)
& $csc /nologo /codepage:65001 /target:exe "/out:$out\TVCleaner.Tests.exe" $refs $lib $tests
if ($LASTEXITCODE) { throw 'tests build failed' }

if (Test-Path "$root\tools\AdbProbe.cs") {
    & $csc /nologo /codepage:65001 /target:exe "/out:$out\TVCleaner.Probe.exe" $refs $lib "$root\tools\AdbProbe.cs"
    if ($LASTEXITCODE) { throw 'probe build failed' }
}
if (Test-Path "$root\packages.json") { Copy-Item "$root\packages.json" $out -Force }
'build OK'

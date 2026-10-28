# Regenerates tests\fixtures\*.apk (tiny resource-only APKs used by the label tests).
# Needs Android build-tools; the generated APKs are committed, so normal builds do not need the SDK.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$sdk  = 'C:\TV\tools\android-sdk'
$aapt = "$sdk\build-tools\34.0.0\aapt2.exe"
$jar  = "$sdk\platforms\android-28\android.jar"
$src  = "$root\tests\fixtures\src"
$out  = "$root\tests\fixtures"
$tmp  = "$out\_tmp"
New-Item -ItemType Directory -Force $tmp | Out-Null

& $aapt link -o "$out\label-literal.apk" -I $jar --manifest "$src\literal\AndroidManifest.xml"
if ($LASTEXITCODE) { throw 'literal fixture failed' }

& $aapt compile --dir "$src\reference\res" -o "$tmp\res.zip"
if ($LASTEXITCODE) { throw 'compile failed' }
& $aapt link -o "$out\label-reference.apk" -I $jar --manifest "$src\reference\AndroidManifest.xml" "$tmp\res.zip"
if ($LASTEXITCODE) { throw 'reference fixture failed' }

Remove-Item $tmp -Recurse -Force
Get-ChildItem "$out\*.apk" | Select-Object Name, Length

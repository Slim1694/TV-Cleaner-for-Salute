# Builds devmenu.apk (shortcut to developer options) without Gradle: aapt2 -> javac -> d8 -> zipalign -> apksigner.
# Needs JDK 17 and Android build-tools; the built APK is shipped in the release, so users do not need them.
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$jdk  = 'C:\Program Files\Microsoft\jdk-17.0.21.21-hotspot'
$sdk  = 'C:\TV\tools\android-sdk'
$bt   = "$sdk\build-tools\34.0.0"
$jar  = "$sdk\platforms\android-28\android.jar"
$env:JAVA_HOME = $jdk; $env:Path = "$jdk\bin;$env:Path"

$out = "$here\build"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force "$out\classes", "$out\dex" | Out-Null

& "$bt\aapt2.exe" link -o "$out\unsigned.apk" -I $jar --manifest "$here\AndroidManifest.xml" --min-sdk-version 21 --target-sdk-version 28
if ($LASTEXITCODE) { throw 'aapt2 link failed' }

$src = Get-ChildItem "$here\src" -Recurse -Filter *.java | % FullName
& "$jdk\bin\javac.exe" --release 8 -classpath $jar -d "$out\classes" -encoding UTF-8 -Xlint:-options $src
if ($LASTEXITCODE) { throw 'javac failed' }

$cls = Get-ChildItem "$out\classes" -Recurse -Filter *.class | % FullName
& "$bt\d8.bat" --release --min-api 21 --lib $jar --output "$out\dex" $cls
if ($LASTEXITCODE) { throw 'd8 failed' }

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open("$out\unsigned.apk", 'Update')
[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, "$out\dex\classes.dex", 'classes.dex') | Out-Null
$zip.Dispose()

& "$bt\zipalign.exe" -f -p 4 "$out\unsigned.apk" "$out\aligned.apk"
if ($LASTEXITCODE) { throw 'zipalign failed' }
$ks = "$here\keystore.jks"
if (-not (Test-Path $ks)) {
    & "$jdk\bin\keytool.exe" -genkeypair -keystore $ks -alias devmenu -keyalg RSA -keysize 2048 -validity 10000 `
        -storepass devmenu -keypass devmenu -dname 'CN=TV Cleaner for Salute devmenu' | Out-Null
}
& "$bt\apksigner.bat" sign --ks $ks --ks-pass pass:devmenu --key-pass pass:devmenu --out "$here\devmenu.apk" "$out\aligned.apk"
if ($LASTEXITCODE) { throw 'apksigner failed' }
& "$bt\apksigner.bat" verify "$here\devmenu.apk"
if ($LASTEXITCODE) { throw 'verify failed' }
"OK: $here\devmenu.apk ({0:N1} KB)" -f ((Get-Item "$here\devmenu.apk").Length / 1KB)

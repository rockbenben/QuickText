#Requires -Version 7.4
# Called by release.ps1 locally and in CI after publishing and installer compilation.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9._-]+$')][string]$AppName,
    [Parameter(Mandatory)][ValidatePattern('^v\d+\.\d+\.\d+$')][string]$Tag,
    [Parameter(Mandatory)][string]$PublishRoot,
    [Parameter(Mandatory)][string]$Installer,
    [string]$OutputDirectory = '.',
    [string[]]$RequiredFiles = @(), # e.g. e_sqlite3.dll and resources/defaults.json
    [hashtable]$SmokeEnvironment = @{}, # app-specific data / shell-integration overrides
    [string]$SmokeMarker = 'quicktext-smoke.txt',
    [switch]$RequireSigning
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $PublishRoot -PathType Container)) { throw "Publish root does not exist: $PublishRoot" }
$PublishRoot = (Resolve-Path -LiteralPath $PublishRoot).Path
if (-not (Test-Path -LiteralPath $Installer -PathType Leaf)) { throw "Installer does not exist: $Installer" }
$Installer = (Resolve-Path -LiteralPath $Installer).Path
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
$installerAsset = Join-Path $outputRoot "$AppName-$Tag-setup.exe"
$sameBytes = (Test-Path -LiteralPath $installerAsset -PathType Leaf) -and ((Get-FileHash -LiteralPath $Installer).Hash -eq (Get-FileHash -LiteralPath $installerAsset).Hash)
if (-not $sameBytes) { Copy-Item -LiteralPath $Installer -Destination $installerAsset -Force }
$Installer = $installerAsset
$assets = @($Installer)
$executables = @($Installer)
foreach ($arch in 'x64', 'arm64') {
    foreach ($form in '', '-needs-dotnet10') {
        $profileDirectory = if ($arch -eq 'x64') { "publish$form" } else { "publish-$arch$form" }
        $directory = Join-Path $PublishRoot $profileDirectory
        $exe = Join-Path $directory "$AppName.exe"
        foreach ($relative in @("$AppName.exe") + $RequiredFiles) {
            $file = [IO.Path]::GetFullPath((Join-Path $directory $relative))
            if (-not $file.StartsWith([IO.Path]::GetFullPath($directory) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Required file outside profile: $relative" }
            if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing published file: $file" }
        }
        $stream = [IO.File]::OpenRead($exe)
        try {
            $reader = [IO.BinaryReader]::new($stream)
            if ($reader.ReadUInt16() -ne 0x5a4d) { throw "Invalid PE: $exe" }
            $stream.Position = 0x3c
            $offset = $reader.ReadInt32()
            $stream.Position = $offset
            if ($reader.ReadUInt32() -ne 0x4550) { throw "Invalid PE signature: $exe" }
            $machine = $reader.ReadUInt16()
            $expected = if ($arch -eq 'x64') { 0x8664 } else { 0xaa64 }
            if ($machine -ne $expected) { throw "Wrong PE architecture: $exe" }
        } finally { $stream.Dispose() }
        $executables += $exe
    }
}
# Check the exact four applications and installer before packing or hashing.
$signatures = @($executables | Get-AuthenticodeSignature)
$signedCount = @($signatures | Where-Object Status -eq Valid).Count
if ($RequireSigning -and $signedCount -ne $executables.Count) { throw 'Configured signing did not produce valid signatures for every executable' }

foreach ($arch in 'x64', 'arm64') {
    foreach ($form in '', '-needs-dotnet10') {
        $profileDirectory = if ($arch -eq 'x64') { "publish$form" } else { "publish-$arch$form" }
        $directory = Join-Path $PublishRoot $profileDirectory
        $zip = Join-Path $outputRoot "$AppName-$Tag-win-$arch$form.zip"
        $sandbox = Join-Path ([IO.Path]::GetTempPath()) ("wpf-package-$([guid]::NewGuid())")
        $stage = Join-Path $sandbox 'stage'
        New-Item -ItemType Directory -Path $stage -Force | Out-Null
        try {
            Get-ChildItem -LiteralPath $directory -Force | Where-Object { $_.Extension -ne '.pdb' } |
                Copy-Item -Destination $stage -Recurse -Force
            tar -a -c -f $zip -C $stage .
            if ($LASTEXITCODE) { throw "Archive failed: $zip" }
            $unpacked = Join-Path $sandbox "verify-$arch$form"
            New-Item -ItemType Directory -Path $unpacked -Force | Out-Null
            tar -x -f $zip -C $unpacked
            if ($LASTEXITCODE) { throw "Unpack failed: $zip" }
            # Verify every file, including satellite resources and native libraries, on both architectures.
            $expectedFiles = @(Get-ChildItem -LiteralPath $stage -File -Recurse -Force)
            $actualFiles = @(Get-ChildItem -LiteralPath $unpacked -File -Recurse -Force)
            if ($expectedFiles.Count -ne $actualFiles.Count) { throw "ZIP inventory mismatch: $zip" }
            foreach ($file in $expectedFiles) {
                $relative = [IO.Path]::GetRelativePath($stage, $file.FullName)
                $copy = Join-Path $unpacked $relative
                if (-not (Test-Path -LiteralPath $copy -PathType Leaf) -or
                    (Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $copy).Hash) {
                    throw "ZIP content mismatch: $relative in $zip"
                }
            }
            if ($arch -eq 'x64') {
                # APPDATA alone does not redirect QuickText's default Documents library.
                New-Item -ItemType File -Path (Join-Path $unpacked 'QuickText.portable') -Force | Out-Null
                $temp = Join-Path $sandbox 'temp'
                $data = Join-Path $sandbox 'data'
                New-Item -ItemType Directory -Path $unpacked,$temp,$data -Force | Out-Null
                # Process-local overrides prevent touching real settings or sharing smoke markers.
                $environment = @{} + $SmokeEnvironment
                $environment.TEMP = $temp; $environment.TMP = $temp
                $environment.APPDATA = $data; $environment.LOCALAPPDATA = $data
                $process = Start-Process -FilePath "$unpacked/$AppName.exe" -WorkingDirectory $unpacked -ArgumentList '--smoke' -Environment $environment -WindowStyle Hidden -PassThru
                if (-not $process.WaitForExit(60000)) { $process.Kill(); $process.WaitForExit(); throw "Packaged smoke timed out: $zip" }
                $marker = Join-Path $temp $SmokeMarker
                if (-not (Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw).Trim() -ne 'OK' -or $process.ExitCode) { throw "Packaged smoke failed: $zip" }
            }
            $assets += $zip
        } finally {
            $resolved = [IO.Path]::GetFullPath($sandbox)
            if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')) { throw 'Unexpected package cleanup directory' }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
$checksums = Join-Path $outputRoot 'SHA256SUMS.txt'
# Explicit inventory excludes old packages, unrelated ZIPs and the checksum itself.
$lines = $assets | Get-FileHash -Algorithm SHA256 | ForEach-Object { "{0}  {1}" -f $_.Hash.ToLowerInvariant(), (Split-Path $_.Path -Leaf) }
[IO.File]::WriteAllText($checksums, ($lines -join "`n") + "`n")
if ($env:GITHUB_OUTPUT) {
    $files = (@($assets) + $checksums) | ForEach-Object { $_.Replace('\', '/') }
    [IO.File]::AppendAllText($env:GITHUB_OUTPUT, ((@('files<<END_ASSETS') + $files + 'END_ASSETS') -join "`n") + "`n")
    "signing=$signedCount/$($executables.Count) executable Authenticode signatures verified valid" >> $env:GITHUB_OUTPUT
}
Write-Host "Packaged $($assets.Count) assets. Checksums: $checksums"

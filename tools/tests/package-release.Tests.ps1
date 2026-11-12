#Requires -Version 7.4
$ErrorActionPreference = 'Stop'
$package = Join-Path $PSScriptRoot '../package-release.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ("QuickText packaging test " + [guid]::NewGuid())
New-Item -ItemType Directory -Path $root | Out-Null
try {
    $publish = Join-Path $root 'publish'
    foreach ($arch in 'x64', 'arm64') {
        foreach ($form in '', '-needs-dotnet10') {
            $directory = if ($arch -eq 'x64') { "publish$form" } else { "publish-$arch$form" }
            $directory = Join-Path $publish $directory
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
            # A minimal PE fixture reaches validation without starting any application.
            $bytes = [byte[]]::new(256)
            $bytes[0] = 0x4d; $bytes[1] = 0x5a; $bytes[0x3c] = 0x80
            $bytes[0x80] = 0x50; $bytes[0x81] = 0x45
            $machine = if ($arch -eq 'x64') { [byte[]](0x64, 0x86) } else { [byte[]](0x64, 0xaa) }
            [Array]::Copy($machine, 0, $bytes, 0x84, 2)
            [IO.File]::WriteAllBytes((Join-Path $directory 'QuickText.exe'), $bytes)
            New-Item -ItemType Directory -Path (Join-Path $directory 'resources') | Out-Null
            [IO.File]::WriteAllText((Join-Path $directory 'resources/defaults.json'), '{"fixture":true}')
        }
    }
    $installer = Join-Path $root 'installer fixture.exe'
    Copy-Item (Join-Path $publish 'publish/QuickText.exe') $installer
    $arguments = @{ AppName='QuickText'; Tag='v0.0.0'; PublishRoot=$publish; Installer=$installer; OutputDirectory=(Join-Path $root 'output') }
    function ExpectFailure([string]$message, [hashtable]$extra = @{}) {
        try { & $package @arguments @extra }
        catch {
            if ($_.Exception.Message -notlike "*$message*") { throw }
            Write-Host "PASS: $message"
            return
        }
        throw "Expected failure: $message"
    }
    $arguments.Installer = Join-Path $root 'missing installer.exe'
    ExpectFailure 'does not exist'
    $arguments.Installer = $installer
    ExpectFailure 'Missing published file' @{ RequiredFiles=@('resources/missing.dll') }
    ExpectFailure 'Required file outside profile' @{ RequiredFiles=@('../outside.dll') }
    $armExe = Join-Path $publish 'publish-arm64/QuickText.exe'
    $original = [IO.File]::ReadAllBytes($armExe)
    Copy-Item (Join-Path $publish 'publish/QuickText.exe') $armExe -Force
    ExpectFailure 'Wrong PE architecture'
    [IO.File]::WriteAllBytes($armExe, $original)
    ExpectFailure 'Configured signing did not produce valid signatures' @{ RequireSigning=$true }
    $tarExecutable = (Get-Command tar -CommandType Application | Select-Object -First 1).Source
    function tar {
        & $tarExecutable @args
        if ($LASTEXITCODE) { throw 'Fixture archive command failed' }
        if ($args[0] -eq '-x') {
            $unpacked = $args[[Array]::IndexOf($args, '-C') + 1]
            $resource = Join-Path $unpacked 'resources/defaults.json'
            if ($corruption -eq 'content') { [IO.File]::WriteAllText($resource, 'corrupted') }
            else { Remove-Item -LiteralPath $resource }
        }
    }
    $corruption = 'content'
    ExpectFailure 'ZIP content mismatch'
    $corruption = 'inventory'
    ExpectFailure 'ZIP inventory mismatch'
    if (Test-Path (Join-Path $arguments.OutputDirectory 'SHA256SUMS.txt')) { throw 'Failed validation generated checksums' }
} finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')) { throw 'Unexpected test cleanup directory' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

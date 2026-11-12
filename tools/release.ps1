[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$InnoCompiler = "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe"
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repoRoot
try {
    $output = Join-Path $repoRoot 'artifacts/release'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    foreach ($arch in 'x64', 'arm64') {
        foreach ($form in '', '-needs-dotnet10') {
            $profile = "win-$arch$form"
            dotnet publish src/QuickText.App -c Release -p:PublishProfile=$profile -p:Version=$Version --nologo
            if ($LASTEXITCODE) { throw "Publish failed: $profile" }
        }
    }
    if (-not (Test-Path -LiteralPath $InnoCompiler)) { throw 'Pass -InnoCompiler with the path to Inno Setup 6 ISCC.exe' }
    & $InnoCompiler "/DAppVersion=$Version" "/O$output" "/FQuickText-v$Version-setup" 'tools/installer/QuickText.iss'
    if ($LASTEXITCODE) { throw 'Installer compilation failed' }
    $installer = Join-Path $output "QuickText-v$Version-setup.exe"
    if (-not (Test-Path $installer)) { throw 'Installer was not generated' }
    & (Join-Path $PSScriptRoot 'package-release.ps1') -AppName QuickText -Tag "v$Version" -PublishRoot (Join-Path $repoRoot 'src/QuickText.App/bin/Release') -Installer $installer -OutputDirectory $output
    Write-Host "Release files: $output"
} finally { Pop-Location }

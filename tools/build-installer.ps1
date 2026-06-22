[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts"
}

$tmpRoot = Join-Path $repoRoot ".tmp\installer"
$payloadDirectory = Join-Path $tmpRoot "payload"
$setupPublishDirectory = Join-Path $tmpRoot "setup-publish"
$installerDirectory = Join-Path $repoRoot "Installer"
$payloadZip = Join-Path $installerDirectory "payload.zip"
$installerProject = Join-Path $installerDirectory "SuperNoNo.Installer.csproj"
$mainProject = Join-Path $repoRoot "SuperNoNo.csproj"

function Assert-UnderRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd([System.IO.Path]::DirectorySeparatorChar)

    if ($fullPath -ne $fullRoot -and -not $fullPath.StartsWith($fullRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify path outside workspace: $fullPath"
    }
}

function Reset-Directory {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-UnderRoot -Path $Path -Root $repoRoot
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

Write-Host "Preparing installer workspace..."
Reset-Directory -Path $tmpRoot
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Write-Host "Publishing SuperNoNo payload..."
Invoke-DotNet -Arguments @(
    "publish",
    $mainProject,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=false",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-o", $payloadDirectory
)

$distSource = Join-Path $repoRoot "Live2DPlayer\dist"
$modelSource = Join-Path $repoRoot "VIP nono"
if (-not (Test-Path -LiteralPath $distSource)) {
    throw "Live2D player dist was not generated: $distSource"
}
if (-not (Test-Path -LiteralPath $modelSource)) {
    throw "Live2D model directory is missing: $modelSource"
}

$distTarget = Join-Path $payloadDirectory "Live2DPlayer\dist"
$modelTarget = Join-Path $payloadDirectory "VIP nono"
New-Item -ItemType Directory -Path (Split-Path -Parent $distTarget) -Force | Out-Null
Copy-Item -LiteralPath $distSource -Destination $distTarget -Recurse -Force
Copy-Item -LiteralPath $modelSource -Destination $modelTarget -Recurse -Force

Get-ChildItem -LiteralPath $payloadDirectory -Recurse -Filter "*.pdb" | Remove-Item -Force

Write-Host "Packing embedded payload..."
if (Test-Path -LiteralPath $payloadZip) {
    Assert-UnderRoot -Path $payloadZip -Root $repoRoot
    Remove-Item -LiteralPath $payloadZip -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $payloadDirectory,
    $payloadZip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false
)

Write-Host "Publishing setup executable..."
Reset-Directory -Path $setupPublishDirectory
Invoke-DotNet -Arguments @(
    "publish",
    $installerProject,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-o", $setupPublishDirectory
)

$setupExe = Join-Path $setupPublishDirectory "SuperNoNoSetup.exe"
if (-not (Test-Path -LiteralPath $setupExe)) {
    throw "Setup executable was not generated: $setupExe"
}

$finalSetupExe = Join-Path $OutputDirectory "SuperNoNoSetup.exe"
Copy-Item -LiteralPath $setupExe -Destination $finalSetupExe -Force

Remove-Item -LiteralPath $payloadZip -Force

$setupInfo = Get-Item -LiteralPath $finalSetupExe
$sizeMb = [Math]::Round($setupInfo.Length / 1MB, 1)
Write-Host ""
Write-Host "Installer ready:"
Write-Host "  $finalSetupExe"
Write-Host "  Size: $sizeMb MB"

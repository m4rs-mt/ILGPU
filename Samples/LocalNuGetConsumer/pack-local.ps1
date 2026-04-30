## ---------------------------------------------------------------------------------------
##                                    ILGPU Samples
##                           Copyright (c) 2026 ILGPU Project
##                                    www.ilgpu.net
##
## File: pack-local.ps1
##
## This file is part of ILGPU and is distributed under the University of Illinois Open
## Source License. See LICENSE.txt for details.
## ---------------------------------------------------------------------------------------

#requires -Version 7.0
<#
.SYNOPSIS
    Pack ILGPU + ILGPUC from your local clone into .\feed\ so
    Consumer\Consumer.csproj can restore against them via PackageReference.

.DESCRIPTION
    PowerShell-native equivalent of pack-local.sh for Windows users.
    Internally mirrors what Src/scripts/pack-ilgpuc.sh does for a single
    host RID, plus a one-shot `dotnet pack` for ILGPU. Read the script —
    it does exactly what the README walkthrough describes.

.PARAMETER Version
    NuGet package version to stamp on both packages. Defaults to
    "0.0.0-local" (override with $env:VERSION or -Version).

.EXAMPLE
    PS> .\pack-local.ps1
    PS> .\pack-local.ps1 -Version 0.0.1-local
    PS> $env:VERSION = "0.0.1-local"; .\pack-local.ps1
#>
[CmdletBinding()]
param(
    [string]$Version = $(if ($env:VERSION) { $env:VERSION } else { "0.0.0-local" })
)

$ErrorActionPreference = "Stop"

$SampleDir = $PSScriptRoot
$RepoRoot  = (Resolve-Path (Join-Path $SampleDir "..\..")).Path
$Feed      = Join-Path $SampleDir "feed"
$Csproj    = Join-Path $RepoRoot "Src\ILGPUC\ILGPUC.csproj"
$Staging   = Join-Path $RepoRoot "Bin\Packaging\ILGPUC-staging"
$Config    = "Release"

function Get-HostRid {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    switch ($arch) {
        "X64"   { return "win-x64" }
        "Arm64" { return "win-arm64" }
        "X86"   { return "win-x86" }
        default { throw "unsupported host architecture: $arch" }
    }
}

$Rid = Get-HostRid

# Wipe the feed and previous Consumer state so each run starts fresh.
foreach ($p in @($Feed,
                 (Join-Path $SampleDir "Consumer\packages"),
                 (Join-Path $SampleDir "Consumer\bin"),
                 (Join-Path $SampleDir "Consumer\obj"),
                 $Staging)) {
    if (Test-Path $p) { Remove-Item -Recurse -Force $p }
}
New-Item -ItemType Directory -Path $Feed    | Out-Null
New-Item -ItemType Directory -Path (Join-Path $Staging "tools\net10.0") -Force | Out-Null

Write-Host "==> packing ILGPU into $Feed ..." -ForegroundColor Cyan
& dotnet pack (Join-Path $RepoRoot "Src\ILGPU\ILGPU.csproj") `
    -c $Config `
    -o $Feed `
    -p:Version=$Version `
    -p:PackageVersion=$Version `
    -p:IncludeSymbols=false `
    --nologo --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet pack ILGPU failed" }

# --- Inline of Src/scripts/pack-ilgpuc.sh, host-RID-only flow. ----------
# Per-RID R2R publish. --self-contained false keeps the output framework-
# dependent. PublishReadyToRunComposite=false produces per-DLL R2R images
# so the Roslyn deps that R2R skips can be deduped against the JIT
# fallback dir at MSBuild resolution time.
Write-Host "==> publish R2R for $Rid" -ForegroundColor Cyan
& dotnet publish $Csproj `
    --configuration $Config `
    --framework net10.0 `
    --runtime $Rid `
    --self-contained false `
    -p:PublishReadyToRun=true `
    -p:PublishReadyToRunComposite=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:_ILGPUCToolDepPrivateAssets= `
    --output (Join-Path $Staging "tools\net10.0\$Rid") `
    --nologo --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (R2R, $Rid) failed" }

# Strip xml-doc + pdb noise from the per-RID dir.
Get-ChildItem (Join-Path $Staging "tools\net10.0\$Rid") -File `
    -Include *.xml,*.pdb | Remove-Item -Force

Write-Host "==> publish JIT fallback (RID-agnostic)" -ForegroundColor Cyan
& dotnet publish $Csproj `
    --configuration $Config `
    --framework net10.0 `
    --self-contained false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:_ILGPUCToolDepPrivateAssets= `
    --output (Join-Path $Staging "tools\net10.0") `
    --nologo --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (JIT fallback) failed" }

# XML doc files help IDEs but bring nothing to a tool package.
Get-ChildItem (Join-Path $Staging "tools\net10.0") -File `
    -Filter *.xml | Remove-Item -Force

# Strip per-RID dir of any DLL byte-identical to the JIT-fallback copy.
# Roslyn deps that crossgen2 skipped fall through to the parent dir during
# normal MSBuild assembly resolution.
Write-Host "==> dedupe per-RID payload against JIT fallback" -ForegroundColor Cyan
$jitDir = Join-Path $Staging "tools\net10.0"
$ridDir = Join-Path $jitDir $Rid
Get-ChildItem $ridDir -File -Filter *.dll | ForEach-Object {
    $twin = Join-Path $jitDir $_.Name
    if ((Test-Path $twin) -and
        ((Get-FileHash $_.FullName -Algorithm SHA256).Hash -eq
         (Get-FileHash $twin       -Algorithm SHA256).Hash)) {
        Remove-Item -Force $_.FullName
    }
}

Write-Host "==> packing ILGPUC ($Rid) into $Feed ..." -ForegroundColor Cyan
& dotnet pack $Csproj `
    --configuration $Config `
    --output $Feed `
    -p:PackageVersion=$Version `
    -p:Version=$Version `
    -p:ILGPUCPackStagingDir=$Staging `
    -p:IncludeSymbols=false `
    --nologo --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet pack ILGPUC failed" }

Write-Host ""
Write-Host "Done. Now build the consumer:" -ForegroundColor Green
Write-Host "    cd Consumer; dotnet build -p:ILGPUC_PACKAGE_VERSION=$Version"
Write-Host "    dotnet run --project Consumer -p:ILGPUC_PACKAGE_VERSION=$Version"

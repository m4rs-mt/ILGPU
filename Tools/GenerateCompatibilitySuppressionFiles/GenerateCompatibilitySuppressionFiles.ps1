## ---------------------------------------------------------------------------------------
##                                        ILGPU
##                        Copyright (c) 2022-2023 ILGPU Project
##                                    www.ilgpu.net
##
## File: GenerateCompatibilitySuppressionFiles.ps1
##
## This file is part of ILGPU and is distributed under the University of Illinois Open
## Source License. See LICENSE.txt for details.
## ---------------------------------------------------------------------------------------

using namespace System.IO

$basePath = [Path]::Combine($PSScriptRoot, '../..')
$srcPath = [Path]::Combine($basePath, 'Src')

# Reset by deleting existing compatibility suppression files.
# Otherwise, the existing suppression rules are never removed.
Write-Host `
     "Removing Existing Compatibility Suppression Files:" `
    -ForegroundColor Yellow

$files = `
    Get-ChildItem `
    -path $srcPath `
    -Filter "CompatibilitySuppressions.xml" `
    -Recurse `
    -File
ForEach ($file in $files) {
    Write-Host " - $($file.FullName)" -ForegroundColor Red
    Remove-Item $file.FullName
}

# Regenerate compatibility suppression files.
# Set GitHub Actions environment variable to enable building all
# configurations.
$propsFilePath = [Path]::Combine($srcPath, 'Directory.Build.props')
$xml = New-Object XML
$xml.Load($propsFilePath)
$node = $xml.SelectSingleNode(
    '/Project/*/LibraryPackageValidationBaselineVersion');

Write-Host `
    "Generating Compatibility Suppression Files for:" `
    "$($node.InnerText)" `
    -ForegroundColor Yellow

$env:GITHUB_ACTIONS = 'true'
dotnet pack /p:GenerateCompatibilitySuppressionFile=true $srcPath

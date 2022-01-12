## ---------------------------------------------------------------------------------------
##                                        ILGPU
##                           Copyright (c) 2021 ILGPU Project
##                                    www.ilgpu.net
##
## File: FixNugetSymbolPackages.ps1
##
## This file is part of ILGPU and is distributed under the University of Illinois Open
## Source License. See LICENSE.txt for details.
## ---------------------------------------------------------------------------------------

[CmdletBinding()]
Param (
    [Parameter(Mandatory=$True, HelpMessage="The ILGPU package version")]
    [string]$version
)

# WORKAROUND: The Symbols packages should only contain Portable
# PDBs (no Windows PDBs allowed). Transfer net471 pdb from Symbols
# packages to Main NuGet packages. Can be removed after updating
# ILGPU from net471 to net472.
ForEach ($library in "ILGPU", "ILGPU.Algorithms") {
  # Get path to the Main and Symbols NuGet packages
  $releaseDir = './Bin/Release'
  $mainPkgPath = Join-Path $releaseDir "$library.$version.nupkg"
  $symbolsPkgPath = Join-Path $releaseDir "$library.$version.snupkg"

  # Transfer net471 pdb from the Symbols to Main NuGet package
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $pdbEntryPath = "lib/net471/$library.pdb"

  $mainPkgZip = [System.IO.Compression.ZipFile]::Open(
    $mainPkgPath,
    'Update')
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
    $mainPkgZip,
    "$releaseDir/net471/$library.pdb",
    $pdbEntryPath);
  $mainPkgZip.Dispose()

  $symbolsPkgZip = [System.IO.Compression.ZipFile]::Open(
    $symbolsPkgPath,
    'Update')
  $symbolsPkgZip.GetEntry($pdbEntryPath).Delete();
  $symbolsPkgZip.Dispose()
}

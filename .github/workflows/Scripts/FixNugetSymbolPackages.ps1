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
ForEach ($library in "ILGPU.Core", "ILGPU.Algorithms") {
  # Get path to the Main and Symbols NuGet packages
  $releaseDir = './Bin/Release'
  $mainPkgPath = Join-Path $releaseDir "$library.$version.nupkg"
  $symbolsPkgPath = Join-Path $releaseDir "$library.$version.snupkg"

  # Transfer net471 pdb from the Symbols to Main NuGet package
  Add-Type -AssemblyName System.IO.Compression.FileSystem

  $mainPkgZip = [System.IO.Compression.ZipFile]::Open(
    $mainPkgPath,
    'Update')
  $symbolsPkgZip = [System.IO.Compression.ZipFile]::Open(
    $symbolsPkgPath,
    'Update')

  $pdbEntries = $symbolsPkgZip.Entries | Where-Object { $_.FullName -like 'lib/net471/*.pdb' }
  ForEach ($oldEntry in $pdbEntries) {
    $newEntry = $mainPkgZip.CreateEntry($oldEntry.FullName);

    $oldStream = $oldEntry.Open();
    $newStream = $newEntry.Open();
    $oldStream.CopyTo($newStream);
    $newStream.Dispose();
    $oldStream.Dispose();

    $oldEntry.Delete();
  }

  $mainPkgZip.Dispose()
  $symbolsPkgZip.Dispose()
}

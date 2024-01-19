## ---------------------------------------------------------------------------------------
##                                        ILGPU
##                        Copyright (c) 2021-2024 ILGPU Project
##                                    www.ilgpu.net
##
## File: UpdateSampleReferences.ps1
##
## This file is part of ILGPU and is distributed under the University of Illinois Open
## Source License. See LICENSE.txt for details.
## ---------------------------------------------------------------------------------------

[CmdletBinding()]
Param (
    [Parameter(Mandatory=$True, HelpMessage="The ILGPU package version")]
    [string]$version,

    [Parameter(HelpMessage="The ILGPU package suffix")]
    [string]$suffix
)

# NB: Perform operations in the Samples folder, so that the relative paths returned by
# the dotnet CLI are correct.
pushd Samples

# Remove ILGPU libraries from Samples solution.
$projectPaths = dotnet sln list | Select-String -Pattern "\.\.\\Src\\"
dotnet sln remove $projectPaths

# Update each sample project individually.
$projectPaths = dotnet sln list | Select-String -Pattern "\.[cf]sproj$"

ForEach ($projectPath in $projectPaths) {
  # Replace existing ILGPU libaries with NuGet package reference.
  $referencePaths = dotnet list $projectPath reference | Select-String -Pattern "\.\.\\Src\\"
  ForEach ($referencePath in $referencePaths) {
    dotnet remove $projectPath reference $referencePath

    $found = $referencePath -match '\\([^\\]*).csproj$'
    if ($found) {
      $libraryName = $matches[1]

      # Skip ILGPU.Analyzers because we bundle it with ILGPU.
      if ($libraryName -eq "ILGPU.Analyzers") {
        continue
      }

      dotnet add $projectPath package $libraryName -v $version --no-restore
    }
  }
}

# Create a local nuget configuration to FeedzIO if the version contains a suffix.
if ([bool]$suffix) {
  dotnet new nugetconfig
  dotnet nuget add source "https://f.feedz.io/ilgpu/preview/nuget/index.json" --name "ILGPU Preview NuGet Packages"
}

popd

## ---------------------------------------------------------------------------------------
##                                        ILGPU
##                           Copyright (c) 2021 ILGPU Project
##                                    www.ilgpu.net
##
## File: CheckLineLength.ps1
##
## This file is part of ILGPU and is distributed under the University of Illinois Open
## Source License. See LICENSE.txt for details.
## ---------------------------------------------------------------------------------------

[CmdletBinding()]
Param (
    [Parameter(Mandatory=$True, HelpMessage="The base path to check")]
    [string]$path
)

$found = $false
Foreach ($pattern in "*.cs","*.tt") {
  Foreach ($file in Get-ChildItem -Path $path -Filter $pattern -Recurse -File) {
    If (-Not (($file.Directory.Name -Eq "Resources") -Or (Select-String -Path $file -Pattern "^// disable: max_line_length" -Quiet))) {
      $index = 1
      Foreach ($line in Get-Content $file) {
        If ($line.Length -gt 90) {
          Write-Host "##[error]${file}:${index}: line too long ($($line.Length) > 90 characters)"
          $found = $true
        }
        $index++
      }
    }
  }
}

If ($found) {
  Exit 1
}

## ---------------------------------------------------------------------------------------
##                                        ILGPU
##                           Copyright (c) 2021 ILGPU Project
##                                    www.ilgpu.net
##
## File: CheckT4LineEndings.ps1
##
## This file is part of ILGPU and is distributed under the University of Illinois Open
## Source License. See LICENSE.txt for details.
## ---------------------------------------------------------------------------------------

[CmdletBinding()]
Param (
    [Parameter(Mandatory=$True, HelpMessage="The base path to check")]
    [string]$path
)

# WORKAROUND: The TextTransform tool fails when the T4 template ends with a newline.
$found = $false
Foreach ($pattern in "*.tt","*.ttinclude") {
  Foreach ($file in Get-ChildItem -path $path -Filter $pattern -Recurse -File) {
    If ((Get-Content -Raw $file) -match "\r\n$") {
      Write-Host "##[error]${file}: Bad T4 line ending"
      $found = $true
    }
  }
}

If ($found) {
  Exit 1
}

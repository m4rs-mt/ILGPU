$vsArchs = "Win32", "X64"

pushd .\ILGPU
. .\BuildNativeLibs.ps1
popd

git submodule update --init --recursive

pushd .\Src\ILGPU.Lightning.Native
$mainDir = (pwd).Path;
popd

# X86 build is disabled due to the restrictions of the Cuda 9.0 SDK

for ($i = 1; $i -lt $archs.Length; $i++)
{
    $vsArch = $vsArchs[$i];
    $command = "pushd $mainDir; msbuild /p:Configuration=Release /p:Platform=$vsArch; popd"
    BuildCommandInNativeEnv $archs[$i] $command
}

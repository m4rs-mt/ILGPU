pushd .\ILGPU
. .\BuildNativeLibs.ps1
popd

git submodule update --init --recursive

pushd .\Src\ILGPU.Lightning.Native
for ($i = 0; $i -lt $configs.Length; $i++)
{
    BuildExtensionLib ($configs[$i])
}
popd

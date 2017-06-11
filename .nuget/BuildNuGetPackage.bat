pushd ..\Src
msbuild ILGPU.Lightning.sln /p:Configuration=Debug
msbuild ILGPU.Lightning.sln /p:Configuration=Release
popd
nuget pack ILGPU.Lightning.nuspec

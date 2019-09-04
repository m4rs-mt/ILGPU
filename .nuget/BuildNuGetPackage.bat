pushd ..\Src
msbuild ILGPU.Algorithms.sln /p:Configuration=Debug
msbuild ILGPU.Algorithms.sln /p:Configuration=Release
popd
nuget pack ILGPU.Algorithms.nuspec

$configs = "X86", "X64"
$generators = "Visual Studio 15 2017", "Visual Studio 15 2017 Win64"
$pathMapping = @{ "X86" = "Win32"; "X64" = "X64" }
$numCores = $env:NUMBER_OF_PROCESSORS

# Based on the blog post: https://blogs.msdn.microsoft.com/vcblog/2017/03/06/finding-the-visual-c-compiler-tools-in-visual-studio-2017/
Install-Module VSSetup -Scope CurrentUser
$toolsPath = (Get-VSSetupInstance | Select-VSSetupInstance -Latest -Require Microsoft.VisualStudio.Component.VC.Tools.x86.x64).InstallationPath
$buildToolsPath = "$toolsPath/VC/Auxiliary/Build"
$buildEnvMapping = @{ "X86" = "$buildToolsPath/vcvars32.bat"; "X64" = "$buildToolsPath/vcvars64.bat" }

function BuildCommandInNativeEnv($config, $command)
{
    $envMap = $buildEnvMapping.Get_Item($config)
    cmd /c """"$envMap""" & powershell -ExecutionPolicy Unrestricted -command ""$command"""
}

function GetBuildDir($config) { return "Build/" + ($pathMapping.Get_Item($config)) }

function GetInstallDir($config) { return "Install/" + ($pathMapping.Get_Item($config)) }

function EnsureDirs($paths)
{
    foreach ($path in $paths)
    {
        if (-Not (Test-Path $path)) { mkdir $path }
    }
}

function ConfigureLLVM($generator, $config)
{
    $installDir = GetInstallDir $config
    $buildDir = GetBuildDir $config
    (EnsureDirs $installDir, $buildDir) > $null
    pushd $buildDir
    $cmakeBaseDir = "../../"
    $cmakeInstallDir = $cmakeBaseDir + $installDir
    cmake $cmakeBaseDir -G"$generator" -DLLVM_ENABLE_RTTI=1 -DCMAKE_INSTALL_PREFIX="$cmakeInstallDir" -DLLVM_TARGETS_TO_BUILD="NVPTX;X86"
    popd
}

function BuildLLVM($config)
{
    $buildDir = GetBuildDir $config
    pushd $buildDir
    cmake --build . --config Release
    popd
}

function BuildLLVMLib($config)
{
    pushd "../Src"
    if (-Not (Test-Path $config)) { mkdir $config }
    popd
    $buildCommand = "$PSScriptRoot/LLVMSharp/tools/GenLLVMDLL.ps1 -arch $config"
    pushd ((GetBuildDir $config) + "/Release/lib")
    BuildCommandInNativeEnv $config $buildCommand
    mv libLLVM.dll "../../../../../Src/$config/libLLVM.dll" -Force
    popd
}

function BuildExtensionLib($config)
{
    $arch = $pathMapping.Get_Item($config)
    $buildCommand = "msbuild /p:Configuration=Release /p:Platform=$arch /m:$numCores"
    BuildCommandInNativeEnv $config $buildCommand
}

git submodule update --init --recursive
        
pushd LLVM
for ($i = 0; $i -lt $configs.Length; $i++)
{
    $config = $configs[$i]
    ConfigureLLVM $generators[$i] $config
    BuildLLVM $config
    BuildLLVMLib $config
}
popd

pushd .\Src\ILGPU.LLVM
for ($i = 0; $i -lt $configs.Length; $i++)
{
    BuildExtensionLib ($configs[$i])
}
popd

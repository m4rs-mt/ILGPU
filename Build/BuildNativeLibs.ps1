$archs = "X86", "X64"
$generators = "Visual Studio 15 2017", "Visual Studio 15 2017 Win64"

# Based on the blog post: https://blogs.msdn.microsoft.com/vcblog/2017/03/06/finding-the-visual-c-compiler-tools-in-visual-studio-2017/
Install-Module VSSetup -Scope CurrentUser
$toolsPath = (Get-VSSetupInstance | Select-VSSetupInstance -Latest -Require Microsoft.VisualStudio.Component.VC.Tools.x86.x64).InstallationPath
$buildToolsPath = "$toolsPath/VC/Auxiliary/Build"
$buildEnvMapping = @{ "X86" = "$buildToolsPath/vcvars32.bat"; "X64" = "$buildToolsPath/vcvars64.bat" }

function BuildCommandInNativeEnv($arch, $command)
{
    $envMap = $buildEnvMapping.Get_Item($arch)
    cmd /c """"$envMap""" & powershell -ExecutionPolicy Unrestricted -command ""$command"""
}

# Build LLVM
git submodule update --init --recursive

pushd LLVM
mkdir Install -Force
mkdir Build -Force
$cmakeBaseDir = "../../"
for ($i = 0; $i -lt $archs.Length; $i++)
{
    $arch = $archs[$i]
    $generator = $generators[$i]
    pushd Install; mkdir $arch -Force; popd
    pushd Build; mkdir $arch -Force; pushd $arch
    $cmakeInstallDir = "../../Install/$arch"

    echo "Building LLVM: $arch"
    cmake $cmakeBaseDir -G"$generator" -DLLVM_ENABLE_RTTI=1 -DCMAKE_BUILD_TYPE="Release" -DCMAKE_INSTALL_PREFIX="$cmakeInstallDir" -DLLVM_TARGETS_TO_BUILD="NVPTX;X86"
    cmake --build . --config Release --target install

    popd; popd
}
popd

# Build ILGPU LLVM libs
$mainDir = (pwd).Path;
$linkCommand = $mainDir + "/Build/LinkLLVM.ps1"
$sourceDir = $mainDir + "/Src/ILGPU.LLVM"
mkdir ILGPU.LLVM -Force; pushd ILGPU.LLVM

for ($i = 0; $i -lt $archs.Length; $i++)
{
    $arch = $archs[$i]
    $generator = $generators[$i]
    $llvmLibDir = $mainDir + "/LLVM/Install/" + $arch + "/lib"
    $llvmDir = $llvmLibDir + "/cmake/llvm"
    mkdir $arch -Force; pushd $arch
    echo "Building ILGPU LLVM Extensions: $arch"
    cmake "$sourceDir" -G"$generator" -DLLVM_DIR="$llvmDir" -DCMAKE_BUILD_TYPE="Release"
    cmake --build . --config Release
    pushd Release

    $buildCommand = "$linkCommand $llvmLibDir $arch"
    echo "Linking LLVM Library: $arch"
    BuildCommandInNativeEnv $arch $buildCommand
    popd; popd
}

popd

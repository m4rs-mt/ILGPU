#!/bin/bash
archs=( "X86" "X64" )
generator="Unix Makefiles"

mainDir=$( pwd )

distro=$( uname -rv | grep -oi 'Ubuntu\|Fedora\|CentOS\|Debian\|Darwin' )
if [ -z $distro ]; then
    echo "Not suppored linux distribution"
    exit 1
fi

if [ "$distro" == "Darwin" ]; then
    dllExtension=".dylib"
else
    dllExtension=".so"
fi

echo "Distribution '$distro' detected"

# Build LLVM 
git submodule update --init --recursive 

pushd LLVM
mkdir -p Install
mkdir -p Build
for i in `seq 0 $((${#archs[*]}-1))`;
do
    arch=${archs[$i]}
    cmakeInstallDir="$mainDir/LLVM/Install/$arch"
    cd Install
    mkdir -p $arch
    cd ../Build
    mkdir -p $arch
    cd $arch
    cmake "../../" -G"$generator" -DLLVM_ENABLE_RTTI=1 -DCMAKE_BUILD_TYPE="Release" -DCMAKE_INSTALL_PREFIX="$cmakeInstallDir" -DLLVM_TARGETS_TO_BUILD="NVPTX;X86"
    cmake --build . --config Release --target install
    cd ../..
done
popd

# Build ILGPU LLVM libs
sourceDir="$mainDir/Src/ILGPU.LLVM"
mkdir -p ILGPU.LLVM
pushd ILGPU.LLVM
for i in `seq 0 $((${#archs[*]}-1))`;
do
    arch=${archs[$i]}
    mkdir -p $arch
    cd $arch
    llvmLibDir="$mainDir/LLVM/Install/$arch/lib"
    llvmDir="$llvmLibDir/cmake/llvm"
    cmake "$sourceDir" -G"$generator" -DLLVM_DIR="$llvmDir" -DCMAKE_BUILD_TYPE="Release"
    cmake --build . --config Release
    mkdir -p "$mainDir/$arch/$distro"
    cp "libILGPU_LLVM$dllExtension" "$mainDir/$arch/$distro/libILGPU_LLVM$dllExtension" -f
    cd ..
done
popd

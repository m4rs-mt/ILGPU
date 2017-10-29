param(
    [string] $llvmPath,
    [string] $targetArch
 )

$baseName = "LLVM"
$dllName = "$baseName.dll"
$pdbName = "$baseName.pdb"
$defName = "symbols.def"
$libName = "ILGPU_LLVM.lib"
cp $libName $llvmPath/$libName
pushd $llvmPath

if (Test-Path $defName) { del $defName -Force }
if (Test-Path LLVM.lib) { del LLVM.lib -Force }

$libs = ls LLVM*.lib, $libName
foreach ($lib in $libs)
{
    dumpbin /linkermember:1 $lib |
        select-string -Pattern "^(.*)\s+_?(LLVM.*|ILGPU_.*)" |
        select -expand Matches |
        foreach { "EXPORTS " + $_.groups[2].value } >> $defName
}

link /dll /def:$defName /machine:$targetArch /out:$dllName $libs /debug:full
popd
$targetDllPath = "../../../$targetArch"
mkdir $targetDllPath -Force
$targetDllPath = "$targetDllPath/Windows"
mkdir $targetDllPath -Force
cp "$llvmPath/$dllName" "$targetDllPath/ILGPU_$dllName" -Force
cp "$llvmPath/$pdbName" "$targetDllPath/ILGPU_$pdbName" -Force

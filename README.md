# ILGPU Programming Examples

The sample projects demonstrate the basic usage of ILGPU  to help you get started with 
high performance GPU programming.

One key feature of ILGPU is the ability to execute the code on the CPU in a way that 
emulates how the GPU works. 
Therefore, instead of having to resort to Graphics/CUDA debugging facilities,
you can directly use all of Visual Studio's CPU debugging features.

Note that this is not possible when your code is executed on the GPU;
in order to execute your code on the CPU, you have to create a CPU context instead of a GPU context
(e.g. by replacing **new CudaAccelerator** with **new CPUAccelerator**).

# Build Instructions


After cloning the repository, the folder structure should look as follows:
- .git
- Src
- LICENSE.txt
- ...

Just cloning the repository should be sufficient as all dependencies are usually restored by NuGet.
Please refer to the ILGPU readme for further dependencies, like the CUDA runtime.

# Samples

## Simple and Advanced 

These samples explain the basic and more advanced capabilities of ILGPU, respectively.

## ILGPU.Lightning

These samples show how to leverage ILGPU.Lightning for even more comfortable GPU programming.

## Remarks

There are a few settings that you should remember to change for your own projects:

- In the project properties, set the target framework to .NET Framework **4.6**.
- Make sure that **Prefer 32-bit** is disabled in the project settings and/or that the target platform is set to X64.

# Build instructions

ILGPU requires Visual Studio 2017 and several extensions:
* Visual Studio 2017 (Community edition or higher) with C++ support
* CUDA 8.0 SDK or higher (https://developer.nvidia.com/cuda-toolkit) 
  in order to execute kernels on NVIDIA GPUs
* CMAKE (https://cmake.org/, Windows requires CMAKE >= 3.9.1)
  ensure that cmake is added to your path
* Python 2.7 (required for LLVM build, https://www.python.org/download/releases/2.7/)

# Build native libraries

We have to build all native libraries first. Use the PowerShell to execute the
"BuildNativeLibs.ps1" script in the root directory. This script will download
all required dependencies and build the required libraries in the correct
configuration.

# Build ILGPU

Use the provided Visual Studio solution to build the ILGPU libs
in the desired configurations (Debug/Release).

Note: ILGPU uses the build configuration "Any CPU" (which simplifies an
integration into other projects).

# Use ILGPU

Build a nuget package in order to include the native libraries in the correct configuration.
Execute "BuildNuGetPackage.bat" in the .nuget directory from a Visual Studio command prompt 
to build a custom nuget package.

# License information

ILGPU is licensed under the University of Illinois/NCSA Open Source License.
Detailed license information can be found in LICENSE.txt.

Copyright (c) 2016-2017 Marcel Koester (www.ilgpu.net). All rights reserved.

## License information of required dependencies

Different parts of ILGPU require different third-party libraries.
* ILGPU Dependencies
    - LLVM (http://www.llvm.org/)
    - System.Runtime.CompilerServices.Unsafe (https://www.nuget.org/packages/system.runtime.CompilerServices.Unsafe/)

Detailed copyright and license information of these dependencies can be found in
LICENSE-3RD-PARTY.txt.

Note that ILGPU uses automatically generated bindings for the LLVM C-API.
These bindings where generated using the ClangSharpPInvokeGenerator project by
Mukul Sabharwal: https://github.com/Microsoft/ClangSharp.

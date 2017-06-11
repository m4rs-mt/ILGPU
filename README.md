# Build instructions

ILGPU requires Visual Studio 2017 and several extensions:
* Visual Studio 2017 (Community edition or higher) with C++ support
* CUDA 8.0 SDK (https://developer.nvidia.com/cuda-toolkit) 
  in order to execute kernels on NVIDIA gpus
* CMAKE (https://cmake.org/)
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

There are two possibilities to use ILGPU in your project without building a
Nuget package:
* Include the desired ILGPU projects in your solution by adding them as
  additional projects. Add references to these included solution projects to
  your derived project. This will automatically copy all required native
  libraries to your output directory.
* Reference the built files manually. You can also make explicit references
  to the built libraries. In this case, however, you have to ensure that the
  required native libraries (in the X86 and X64 directories) are in directory of
  your library/application. Otherwise, the ILGPU runtime cannot load the
  required libraries at runtime.

# License information

ILGPU is licensed under the University of Illinois/NCSA Open Source License.
Detailed license information can be found in LICENSE.txt.

Copyright (c) 2016-2017 Marcel Koester (www.ilgpu.net). All rights reserved.

## License information of required dependencies

Different parts of ILGPU require different third-party libraries.
* ILGPU Dependencies
    - LLVM (http://www.llvm.org/)
    - LLVMSharp (http://www.llvmsharp.org/)
    - System.Runtime.CompilerServices.Unsafe (https://www.nuget.org/packages/system.runtime.CompilerServices.Unsafe/)

Detailed copyright and license information of these dependencies can be found in
LICENSE-3RD-PARTY.txt.

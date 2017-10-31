# Build instructions

ILGPU requires Visual Studio 2017 and several extensions:
* Visual Studio 2017 (Community edition or higher) with C++ support
* CUDA 9.0 SDK (https://developer.nvidia.com/cuda-toolkit) 

# Build ILGPU.Lightning

Use the provided Visual Studio solution to build the ILGPU.Lightning libs
in the desired configurations (Debug/Release).

Note: ILGPU.Lightning uses the build configuration "Any CPU" (which simplifies
an integration into other projects).

# License information

ILGPU.Lightning is licensed under the University of Illinois/NCSA Open Source License.
Detailed license information can be found in LICENSE.txt.

Copyright (c) 2016-2017 ILGPU Lightning Project. All rights reserved.

## License information of required dependencies

Different parts of ILGPU.Lightning require different third-party libraries.
* ILGPU.Lightning Dependencies
    - ILGPU (http://www.ilgpu.net)
    - CUB (https://nvlabs.github.io/cub/)

Detailed copyright and license information of these dependencies can be found in
LICENSE-3RD-PARTY.txt.

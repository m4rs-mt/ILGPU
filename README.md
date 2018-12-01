# ILGPU.Lightning

Real-world applications typically require a standard library and a set of standard algorithms that "simply work".
The ILGPU Lightning library meets these requirements by offering a set of auxiliary functions and high-level algorithms (e.g. sorting or prefix sum).
All algorithms can be run on all supported accelerator types.
The CPU accelerator support is especially useful for kernel debugging.

# Build instructions

ILGPU.Lightning requires Visual Studio 2017.

# Build ILGPU.Lightning

Use the provided Visual Studio solution to build the ILGPU.Lightning libs
in the desired configurations (Debug/Release).

Note: ILGPU.Lightning uses the build configuration "Any CPU" (which simplifies
an integration into other projects).

# License information

ILGPU.Lightning is licensed under the University of Illinois/NCSA Open Source License.
Detailed license information can be found in LICENSE.txt.

Copyright (c) 2016-2018 ILGPU Lightning Project. All rights reserved.

## License information of required dependencies

Different parts of ILGPU.Lightning require different third-party libraries.
* ILGPU.Lightning Dependencies
    - ILGPU (http://www.ilgpu.net)

Detailed copyright and license information of these dependencies can be found in
LICENSE-3RD-PARTY.txt.

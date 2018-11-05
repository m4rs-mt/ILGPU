# Build instructions

ILGPU requires Visual Studio 2017 (Community edition or higher).

# Build ILGPU

Use the provided Visual Studio solution to build the ILGPU libs
in the desired configurations (Debug/Release).

Note: ILGPU uses the build configuration "Any CPU" (which simplifies an
integration into other projects).

# References

ILGPU currently uses an experimental IR inspired by the following papers:
* A Graph-Based Higher-Order Intermediate Representation
    - Roland Leissa, Marcel Koester, and Sebastian Hack
* Target-Specific Refinement of Multigrid Codes
    - Richard Membarth, Philipp Slusallek, Marcel Koester, Roland Leissa, and Sebastian Hack
* Code Refinement of Stencil Codes
    - Marcel Koester, Roland Leissa, Sebastian Hack, Richard Membarth, and Philipp Slusallek

# License information

ILGPU is licensed under the University of Illinois/NCSA Open Source License.
Detailed license information can be found in LICENSE.txt.

Copyright (c) 2016-2018 Marcel Koester (www.ilgpu.net). All rights reserved.

## License information of required dependencies

Different parts of ILGPU require different third-party libraries.
* ILGPU Dependencies
    - System.Collections.Immutable
    (https://www.nuget.org/packages/System.Collections.Immutable)
    - System.Reflection.Emit.ILGeneration
    (https://www.nuget.org/packages/System.Reflection.Emit.ILGeneration)
    - System.Reflection.Metadata
    (https://www.nuget.org/packages/System.Reflection.Metadata)
    - System.Runtime.CompilerServices.Unsafe
    (https://www.nuget.org/packages/system.runtime.CompilerServices.Unsafe/)

Detailed copyright and license information of these dependencies can be found in
LICENSE-3RD-PARTY.txt.

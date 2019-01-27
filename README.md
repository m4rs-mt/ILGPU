# ILGPU

ILGPU is a JIT (just-in-time) compiler for high-performance GPU programs written in .Net-based languages.
ILGPU is entirely written in C# without any native dependencies.
It offers the flexibility and the convenience of C++ AMP on the one hand and the high performance of Cuda programs on the other hand.
Functions in the scope of kernels do not have to be annotated (default C# functions) and are allowed to work on value types.
All kernels (including all hardware features like shared memory and atomics) can be executed and debugged on the CPU using the integrated multi-threaded CPU accelerator.

# Build instructions

ILGPU requires Visual Studio 2017 (Community edition or higher).

Use the provided Visual Studio solution to build the ILGPU libs
in the desired configurations (Debug/Release).

Note: T4 (*.tt) text templates must be converted manually depending on the Visual Studio version.
To transform them, right-click a text template and select `Run Custom Tool`.
Alternatively, you can open and save any text template in Visual Studio.

# General Contribution Guidelines

* Make sure that you agree with the general coding style (in terms of braces, whitespaces etc.).
* Make sure that ILGPU compiles without warnings in all build modes (Debug, DebugVerification and Release).

# References

* Parallel Thread Execution ISA 6.3
    - NVIDIA
* A Graph-Based Higher-Order Intermediate Representation
    - Roland Leissa, Marcel Koester, and Sebastian Hack
* Target-Specific Refinement of Multigrid Codes
    - Richard Membarth, Philipp Slusallek, Marcel Koester, Roland Leissa, and Sebastian Hack
* Code Refinement of Stencil Codes
    - Marcel Koester, Roland Leissa, Sebastian Hack, Richard Membarth, and Philipp Slusallek
* Simple and Efficient Construction of Static Single Assignment Form
    - Matthias Braun, Sebastian Buchwald, Sebastian Hack, Roland Leissa, Christoph Mallon and Andreas Zwinkau
* A Simple, Fast Dominance Algorithm
    - Keith D. Cooper, Timothy J. Harvey and Ken Kennedy

# License information

ILGPU is licensed under the University of Illinois/NCSA Open Source License.
Detailed license information can be found in LICENSE.txt.

Copyright (c) 2016-2019 Marcel Koester (www.ilgpu.net). All rights reserved.

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

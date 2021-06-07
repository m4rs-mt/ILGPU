# ILGPU

ILGPU is a JIT (just-in-time) compiler for high-performance GPU programs written in .Net-based languages.
ILGPU is entirely written in C# without any native dependencies.
It offers the flexibility and the convenience of C++ AMP on the one hand and the high performance of Cuda programs on the other hand.
Functions in the scope of kernels do not have to be annotated (default C# functions) and are allowed to work on value types.
All kernels (including all hardware features like shared memory and atomics) can be executed and debugged on the CPU using the integrated multi-threaded CPU accelerator.

# ILGPU.Algorithms

Real-world applications typically require a standard library and a set of standard algorithms that "simply work".
The ILGPU Algorithms library meets these requirements by offering a set of auxiliary functions and high-level algorithms (e.g. sorting or prefix sum).
All algorithms can be run on all supported accelerator types.

# Build Instructions

ILGPU requires Visual Studio 2019 (Community edition or higher).

Use the provided Visual Studio solution to build the ILGPU libs
in the desired configurations (Debug/Release).

# Tests

Sometimes the XUnit test runner stops execution when all tests are run in parallel.
This is not a problem related to the internal tests, but a known XUnit/Visual Studio problem.
If the tests stop unexpectedly, you can simply run the remaining tests again to continue working.

Note: You can unload ILGPU.Tests.Cuda (for example) if you do not have a Cuda-capable device to
execute the Cuda test cases.

# Related Information
* ILGPU Homepage (www.ilgpu.net)
* ILGPU Algorithms (https://github.com/m4rs-mt/ILGPU.Algorithms)
* ILGPU Samples (https://github.com/m4rs-mt/ILGPU.Samples)
* ILGPU Class Reference (http://www.ilgpu.net/Documentation/LatestReference)
* ILGPU Documentation (http://www.ilgpu.net/Documentation)
* Nuget (https://www.nuget.org/packages/ILGPU)
* Release Notes (http://www.ilgpu.net/ReleaseVersions)

# General Contribution Guidelines

* Make sure that you agree with the general coding style (in terms of braces, whitespaces etc.).
* Make sure that ILGPU compiles without warnings in all build modes (Debug, DebugVerification and Release).

# References

* Parallel Thread Execution ISA 7.0
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
* Fast Half Float Conversions
    - Jeroen van der Zijp
* Identifying Loops In Almost Linear Time
    - G. Ramalingam

# License information

ILGPU is licensed under the University of Illinois/NCSA Open Source License.
Detailed license information can be found in LICENSE.txt.

Copyright (c) 2016-2020 Marcel Koester (www.ilgpu.net). All rights reserved.

## License information of required dependencies

Different parts of ILGPU require different third-party libraries.
* ILGPU Dependencies
    - System.Collections.Immutable
    (https://www.nuget.org/packages/System.Collections.Immutable)
    - System.Memory
    (https://www.nuget.org/packages/System.Memory)
    - System.Reflection.Metadata
    (https://www.nuget.org/packages/System.Reflection.Metadata)
    - System.Runtime.CompilerServices.Unsafe
    (https://www.nuget.org/packages/system.runtime.CompilerServices.Unsafe/)

Detailed copyright and license information of these dependencies can be found in
LICENSE-3RD-PARTY.txt.

# Credits

This work was supported by the [Deutsches Forschungszentrum für Künstliche Intelligenz GmbH](https://www.dfki.de/) (DFKI; German Research Center for Artificial Intelligence).
<p><img src="https://www.dfki.de/fileadmin/user_upload/DFKI/Medien/Logos/Logos_DFKI/DFKI_Logo.png" alt="DFKI Logo" width="250"></p>


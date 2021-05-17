# ILGPU.Algorithms

Real-world applications typically require a standard library and a set of standard algorithms that "simply work".
The ILGPU Algorithms library meets these requirements by offering a set of auxiliary functions and high-level algorithms (e.g. sorting or prefix sum).
All algorithms can be run on all supported accelerator types.
The CPU accelerator support is especially useful for kernel debugging.

# Build instructions

ILGPU.Algorithms requires Visual Studio 2019 or higher.

Make sure to init/update the ILGPU git submodule using `git submodule update --init` before building the algorithms library.

# License information

ILGPU.Algorithms is licensed under the University of Illinois/NCSA Open Source License.
Detailed license information can be found in LICENSE.txt.

Copyright (c) 2019-2020 ILGPU Algorithms Project. All rights reserved.
Copyright (c) 2016-2018 ILGPU Lightning Project. All rights reserved.

## License information of required dependencies

Different parts of ILGPU.Algorithms require different third-party libraries.
* ILGPU.Algorithms Dependencies
    - ILGPU (http://www.ilgpu.net)

Detailed copyright and license information of these dependencies can be found in
LICENSE-3RD-PARTY.txt.

# Credits

This work was supported by the [Deutsches Forschungszentrum für Künstliche Intelligenz GmbH](https://www.dfki.de/) (DFKI; German Research Center for Artificial Intelligence).
<p><img src="https://www.dfki.de/fileadmin/user_upload/DFKI/Medien/Logos/Logos_DFKI/DFKI_Logo.png" alt="DFKI Logo" width="250"></p>


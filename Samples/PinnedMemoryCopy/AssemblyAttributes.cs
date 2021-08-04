using System;
using System.Runtime.CompilerServices;

[assembly: CLSCompliant(true)]

// Mark all internals to be visible to the ILGPU runtime
[assembly: InternalsVisibleTo(ILGPU.Context.RuntimeAssemblyName)]

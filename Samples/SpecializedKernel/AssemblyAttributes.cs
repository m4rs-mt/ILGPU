using System.Runtime.CompilerServices;

// Mark all internals to be visible to the ILGPU runtime
[assembly: InternalsVisibleTo(ILGPU.Context.RuntimeAssemblyName)]

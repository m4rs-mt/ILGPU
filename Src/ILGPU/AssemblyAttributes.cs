using ILGPU;
using System.Runtime.CompilerServices;

// WORKAROUND: There is an issue with net8.0 that causes the compiler to crash.
// https://github.com/dotnet/roslyn/issues/71039

[assembly: InternalsVisibleTo("ILGPURuntime")]
// [assembly: InternalsVisibleTo(Context.RuntimeAssemblyName)]

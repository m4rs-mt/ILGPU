// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilerManager.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Compilers;

/// <summary>
/// Default implementation of <see cref="ICompilerManager"/>. Routes compilation
/// requests to the appropriate <see cref="ICompiler"/> based on the requested
/// <see cref="CompilationTarget"/>.
/// </summary>
/// <remarks>
/// Initializes a new <see cref="CompilerManager"/> from an explicit collection of
/// compiler instances.
/// </remarks>
/// <param name="compilers">
/// The compilers to register, one per <see cref="CompilationTarget"/>.
/// </param>
public sealed class CompilerManager(IEnumerable<ICompiler> compilers) : ICompilerManager
{
    private readonly Dictionary<CompilationTarget, ICompiler> _compilers =
        compilers.ToDictionary(c => c.Target);

    /// <summary>
    /// Initializes a new <see cref="CompilerManager"/> with default compiler paths.
    /// </summary>
    public CompilerManager()
        : this(new CompilerOptions())
    { }

    /// <summary>
    /// Initializes a new <see cref="CompilerManager"/> using the paths specified in
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Configurable paths for each GPU compiler executable.</param>
    public CompilerManager(CompilerOptions options)
        : this(
        [
            new NvccCompiler(options.NvccPath),
            new HipccCompiler(options.HipccPath),
            new MetalCompiler(options.XcrunPath, options.MetalSdk),
            new OclocCompiler(options.OclocPath),
            new ClangOpenCLCompiler(options.ClangPath),
        ])
    { }


    /// <summary>
    /// Dispatches the compilation request to the compiler registered for the requested
    /// target, returning a failure result if the target is unknown or unavailable.
    /// </summary>
    /// <param name="request">The compilation request to fulfill.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A <see cref="CompilationResult"/> describing the outcome.</returns>
    public async Task<CompilationResult> CompileAsync(
        CompileRequest request,
        CancellationToken ct)
    {
        if (!_compilers.TryGetValue(request.Target, out var compiler))
        {
            return new CompilationResult
            {
                Success = false,
                StdErr = $"Unknown compilation target: {request.Target}",
                ExitCode = -1,
                Target = request.Target,
                OutputType = request.OutputType,
            };
        }

        if (!compiler.IsAvailable)
        {
            return new CompilationResult
            {
                Success = false,
                StdErr = $"Compiler for {request.Target} is not available on this system",
                ExitCode = -1,
                Target = request.Target,
                OutputType = request.OutputType,
            };
        }

        return await compiler.CompileAsync(request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Queries every registered compiler for its availability and version, returning an
    /// aggregate capability report.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="CompilationCapabilities"/> containing one entry per registered
    /// compiler.
    /// </returns>
    public async Task<CompilationCapabilities> GetCapabilitiesAsync(CancellationToken ct)
    {
        var capabilities = new List<CompilerCapability>();

        foreach (var compiler in _compilers.Values)
        {
            string? version = null;
            if (compiler.IsAvailable)
                version = await compiler.GetVersionAsync(ct).ConfigureAwait(false);

            capabilities.Add(new CompilerCapability
            {
                Target = compiler.Target,
                Available = compiler.IsAvailable,
                Version = version,
                ToolchainComponents = compiler.ToolchainComponents,
            });
        }

        return new CompilationCapabilities { Compilers = [.. capabilities] };
    }
}

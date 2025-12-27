// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: HipccCompiler.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Compilers;

/// <summary>
/// Compiler wrapper for AMD HIP, invoking <c>hipcc</c> to produce an object file from
/// HIP source code.
/// </summary>
public sealed class HipccCompiler : CompilerBase
{
    private const string DefaultPath = "/opt/rocm/bin/hipcc";

    /// <summary>
    /// Initializes a new <see cref="HipccCompiler"/> with a custom executable path.
    /// </summary>
    /// <param name="hipccPath">The absolute path to the <c>hipcc</c> executable.</param>
    public HipccCompiler(string hipccPath = DefaultPath)
        : base(hipccPath)
    { }

    /// <inheritdoc/>
    public override CompilationTarget Target => CompilationTarget.Hip;

    /// <summary>
    /// Compiles the HIP source code in <paramref name="request"/> to an object file and
    /// returns the base-64-encoded output on success.
    /// </summary>
    /// <param name="request">
    /// The compilation request containing source and options.
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A <see cref="CompilationResult"/> describing the outcome.</returns>
    public override async Task<CompilationResult> CompileAsync(
        CompileRequest request,
        CancellationToken ct)
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var inputFile = Path.Combine(tempDir, "input.hip");
            await File.WriteAllTextAsync(inputFile, request.SourceCode, ct)
                .ConfigureAwait(false);

            var outputFile = Path.Combine(tempDir, "output.o");
            var includes = BuildIncludeArgs(request);
            var directives = BuildDirectiveArgs(request);
            var compilerFlags = SanitizeCompilerFlags(request.CudaFlags);
            var args =
                $"-c -o \"{outputFile}\" \"{inputFile}\"" +
                $" {compilerFlags}{includes}{directives}";

            var (exitCode, stdout, stderr) = await RunProcessAsync(
                ExecutablePath,
                args.Trim(),
                tempDir,
                ct).ConfigureAwait(false);

            if (exitCode != 0)
            {
                return new CompilationResult
                {
                    Success = false,
                    StdOut = stdout,
                    StdErr = stderr,
                    ExitCode = exitCode,
                    Target = CompilationTarget.Hip,
                    OutputType = request.OutputType,
                };
            }

            var output = Base64EncodeFile(outputFile);

            return new CompilationResult
            {
                Success = true,
                Output = output,
                StdOut = stdout,
                StdErr = stderr,
                ExitCode = exitCode,
                Target = CompilationTarget.Hip,
                OutputType = request.OutputType,
            };
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }
}

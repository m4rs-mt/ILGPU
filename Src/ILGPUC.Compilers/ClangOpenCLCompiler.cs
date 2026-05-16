// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ClangOpenCLCompiler.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Compilers;

/// <summary>
/// Compiler wrapper for AMD/generic OpenCL, invoking <c>clang</c> to compile OpenCL C
/// source code to SPIR-V bytecode using the <c>spirv64-unknown-unknown</c> target.
/// </summary>
public sealed class ClangOpenCLCompiler : CompilerBase
{
    private const string DefaultPath = "/usr/bin/clang";

    private readonly string _clStandard;

    /// <summary>
    /// Initializes a new <see cref="ClangOpenCLCompiler"/> with a custom executable path
    /// and OpenCL C standard version.
    /// </summary>
    /// <param name="clangPath">The absolute path to the <c>clang</c> executable.</param>
    /// <param name="clStandard">
    /// The OpenCL C standard version to compile against (e.g. <c>CL2.0</c>).
    /// </param>
    public ClangOpenCLCompiler(
        string clangPath = DefaultPath,
        string clStandard = "CL2.0")
        : base(clangPath)
    {
        _clStandard = clStandard;
    }

    /// <inheritdoc/>
    public override CompilationTarget Target => CompilationTarget.OpenCLAmd;

    /// <inheritdoc/>
    protected override ToolchainComponent[] GetToolchainComponents()
    {
        return [CheckTool("clang", ExecutablePath)];
    }

    /// <summary>
    /// Compiles the OpenCL C source code in <paramref name="request"/> to SPIR-V
    /// bytecode using <c>clang -target spirv64-unknown-unknown</c> and returns the
    /// base-64-encoded output on success.
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
            var inputFile = Path.Combine(tempDir, "input.cl");
            var outputFile = Path.Combine(tempDir, "output.spv");
            await File.WriteAllTextAsync(inputFile, request.SourceCode, ct)
                .ConfigureAwait(false);

            var includes = BuildIncludeArgs(request);
            var directives = BuildDirectiveArgs(request);
            var flags = SanitizeCompilerFlags(request.CudaFlags);

            var args =
                $"-target spirv64-unknown-unknown -cl-std={_clStandard} -x cl" +
                $" -o \"{outputFile}\" \"{inputFile}\"" +
                $" {flags}{includes}{directives}";

            var (exitCode, stdout, stderr) = await RunProcessAsync(
                ExecutablePath,
                args.Trim(),
                tempDir,
                ct).ConfigureAwait(false);

            if (exitCode != 0)
                return Failure(stdout, stderr, exitCode, request.OutputType);

            return new CompilationResult
            {
                Success = true,
                Output = Base64EncodeFile(outputFile),
                StdOut = stdout,
                StdErr = stderr,
                ExitCode = 0,
                Target = CompilationTarget.OpenCLAmd,
                OutputType = OutputType.SpirV,
            };
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }
}

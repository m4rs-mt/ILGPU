// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: OclocCompiler.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Compilers;

/// <summary>
/// Compiler wrapper for Intel OpenCL, invoking <c>ocloc</c> to compile OpenCL C source
/// code to SPIR-V bytecode.
/// </summary>
public sealed class OclocCompiler : CompilerBase
{
    private const string DefaultPath = "/usr/bin/ocloc";

    /// <summary>
    /// Initializes a new <see cref="OclocCompiler"/> with a custom executable path.
    /// </summary>
    /// <param name="oclocPath">The absolute path to the <c>ocloc</c> executable.</param>
    public OclocCompiler(string oclocPath = DefaultPath)
        : base(oclocPath)
    { }

    /// <inheritdoc/>
    public override CompilationTarget Target => CompilationTarget.OpenCLIntel;

    /// <inheritdoc/>
    protected override ToolchainComponent[] GetToolchainComponents()
    {
        // ocloc rejects "--version" / "-version" with "Invalid option" and
        // exits non-zero. It does accept "--help" (exit 0) and prints its
        // own usage banner including the build identifier, which is
        // sufficient for an availability probe.
        return [CheckTool("ocloc", ExecutablePath, arguments: "--help")];
    }

    /// <summary>
    /// Compiles the OpenCL C source code in <paramref name="request"/> to SPIR-V
    /// bytecode using <c>ocloc compile</c> and returns the base-64-encoded output
    /// on success.
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
            await File.WriteAllTextAsync(inputFile, request.SourceCode, ct)
                .ConfigureAwait(false);

            // ocloc requires -device <device_type> to compile, otherwise it
            // falls back to a minimal default target that rejects modern
            // features (notably double-precision: cl_khr_fp64 is reported
            // as "unsupported and ignored" without a target). We pick `pvc`
            // (Intel Data Center GPU Max / Ponte Vecchio) because it has
            // the broadest feature set — full FP64, sub-groups, atomics,
            // etc. — so any kernel ILGPU emits will validate. The
            // resulting SPIR-V is portable across Intel devices (the
            // device choice affects validation, not the IR output).
            // ocloc compile -file input.cl -spv_only -device pvc \
            //     -options "-cl-std=CL2.0" -out_dir <tempdir>
            var args =
                $"compile -file \"{inputFile}\" -spv_only -device pvc" +
                $" -options \"-cl-std=CL2.0\" -out_dir \"{tempDir}\"";

            var (exitCode, stdout, stderr) = await RunProcessAsync(
                ExecutablePath,
                args,
                tempDir,
                ct).ConfigureAwait(false);

            if (exitCode != 0)
                return Failure(stdout, stderr, exitCode, request.OutputType);

            // ocloc writes output as *.spv in out_dir
            var spvFile = Directory.GetFiles(tempDir, "*.spv").FirstOrDefault();
            if (spvFile is null)
            {
                return Failure(
                    stdout,
                    stderr + "\nNo .spv output found",
                    -1,
                    request.OutputType);
            }

            return new CompilationResult
            {
                Success = true,
                Output = Base64EncodeFile(spvFile),
                StdOut = stdout,
                StdErr = stderr,
                ExitCode = 0,
                Target = CompilationTarget.OpenCLIntel,
                OutputType = OutputType.SpirV,
            };
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }
}

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvccCompiler.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Compilers;

/// <summary>
/// Compiler wrapper for NVIDIA CUDA, invoking <c>nvcc</c> to produce either a PTX
/// text file or a cubin binary from CUDA source code.
/// </summary>
public sealed class NvccCompiler : CompilerBase
{
    private const string DefaultPath = "/usr/local/cuda/bin/nvcc";

    /// <summary>
    /// Initializes a new <see cref="NvccCompiler"/> with a custom executable path.
    /// </summary>
    /// <param name="nvccPath">The absolute path to the <c>nvcc</c> executable.</param>
    public NvccCompiler(string nvccPath = DefaultPath)
        : base(nvccPath)
    { }

    /// <inheritdoc/>
    public override CompilationTarget Target => CompilationTarget.Cuda;

    /// <inheritdoc/>
    protected override ToolchainComponent[] GetToolchainComponents()
    {
        var nvccDir = Path.GetDirectoryName(ExecutablePath);
        var ptxasPath = nvccDir is not null
            ? Path.Combine(nvccDir, "ptxas")
            : "ptxas";
        return
        [
            CheckTool("nvcc", ExecutablePath),
            CheckTool("ptxas", ptxasPath),
        ];
    }

    /// <summary>
    /// Compiles the CUDA source code in <paramref name="request"/> to either PTX text
    /// or a base-64-encoded cubin binary, depending on the requested output type.
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
            var inputFile = Path.Combine(tempDir, "input.cu");
            await File.WriteAllTextAsync(inputFile, request.SourceCode, ct)
                .ConfigureAwait(false);

            string outputFile;
            string outputFlag;
            switch (request.OutputType)
            {
                case OutputType.Ptx:
                    outputFile = Path.Combine(tempDir, "output.ptx");
                    outputFlag = "-ptx";
                    break;
                default:
                    outputFile = Path.Combine(tempDir, "output.cubin");
                    outputFlag = "-cubin";
                    break;
            }

            var cudaFlags = SanitizeCompilerFlags(request.CudaFlags);
            var includes = BuildIncludeArgs(request);
            var directives = BuildDirectiveArgs(request);
            var args =
                $"{outputFlag} -o \"{outputFile}\" \"{inputFile}\"" +
                $" {cudaFlags}{includes}{directives}";

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
                    Target = CompilationTarget.Cuda,
                    OutputType = request.OutputType,
                };
            }

            var output = request.OutputType == OutputType.Ptx
                ? await File.ReadAllTextAsync(outputFile, ct).ConfigureAwait(false)
                : Base64EncodeFile(outputFile);

            return new CompilationResult
            {
                Success = true,
                Output = output,
                StdOut = stdout,
                StdErr = stderr,
                ExitCode = exitCode,
                Target = CompilationTarget.Cuda,
                OutputType = request.OutputType,
            };
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }
}

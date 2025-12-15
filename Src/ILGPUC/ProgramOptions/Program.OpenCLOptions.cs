// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.OpenCLOptions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.OpenCL;
using ILGPUC.Backends;
using ILGPUC.Backends.OpenCL;
using System.Collections.Generic;
using System.CommandLine;

namespace ILGPUC;

sealed partial class Program
{
    /// <summary>
    /// Represents options for the OpenCL backend.
    /// </summary>
    sealed class OpenCLOptions : BackendOptions
    {
        /// <summary>
        /// OpenCL C language version string (e.g. <c>2.0</c>, <c>1.2</c>).
        /// </summary>
        readonly Option<string> _openclVersionOption = new("--opencl-version")
        {
            Description = "OpenCL C version to use (e.g. 2.0, 1.2)",
            DefaultValueFactory = _ => "2.0"
        };

        /// <summary>
        /// One or more OpenCL compilation targets
        /// (<c>intel</c>, <c>amd</c>, or <c>source</c>).
        /// </summary>
        readonly Option<string[]> _openclTargetOption = new("--opencl-target")
        {
            Description =
                "OpenCL compilation target: intel, amd, source (default: source)",
            DefaultValueFactory = _ => ["source"],
            AllowMultipleArgumentsPerToken = true,
        };

        /// <summary>
        /// Registers all options to control OpenCL compilation properties
        /// with the command.
        /// </summary>
        /// <param name="command">The command to attach options to.</param>
        public OpenCLOptions(Command command) : base(BackendType.OpenCL)
        {
            command.Add(_openclVersionOption);
            command.Add(_openclTargetOption);
        }

        /// <summary>
        /// Creates OpenCL backend instances with desired configurations.
        /// </summary>
        public override IEnumerable<Backend> CreateBackends(
            CompilationProperties properties,
            ParseResult parseResult)
        {
            var versionStr = parseResult.GetRequiredValue(_openclVersionOption);
            if (!CLCVersion.TryParse(versionStr, out var version))
                version = CLCVersion.CL20;

            var targets = parseResult.GetRequiredValue(_openclTargetOption);
            foreach (var target in targets)
            {
                yield return target.ToLowerInvariant() switch
                {
                    "intel" => new OpenCLIntelBackend(version.Major, version.Minor),
                    "amd" => new OpenCLAmdBackend(version.Major, version.Minor),
                    _ => new OpenCLBackend(version.Major, version.Minor),
                };
            }
        }
    }
}

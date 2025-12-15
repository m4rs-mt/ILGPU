// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.CPUOptions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Backends.CPU;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace ILGPUC;

sealed partial class Program
{
    /// <summary>
    /// Represents options for the CPU backend
    /// </summary>
    sealed class CPUOptions : BackendOptions
    {
        /// <summary>
        /// One or more SIMD lane-width values (e.g. <c>8</c>) to generate backends for.
        /// </summary>
        readonly Option<int[]> _simdWidthOption = new("--simd-width")
        {
            Description = "SIMD width",
            DefaultValueFactory = _ => [8],
            AllowMultipleArgumentsPerToken = true
        };

        /// <summary>
        /// When <see langword="true"/>, disables vector buffer pooling so that buffers
        /// are allocated inside the kernel instead of pre-allocated in the launcher.
        /// </summary>
        readonly Option<bool> _noPoolBuffersOption = new("--no-pool-buffers")
        {
            Description =
                "Disable vector buffer pooling (allocate buffers inside " +
                "the kernel instead of pre-allocating in the launcher)",
            DefaultValueFactory = _ => false
        };

        /// <summary>
        /// Registers all options to control CPU compilation properties with the command.
        /// </summary>
        /// <param name="command">The command to attach options to.</param>
        public CPUOptions(Command command) : base(BackendType.CPU)
        {
            command.Add(_simdWidthOption);
            command.Add(_noPoolBuffersOption);
        }

        /// <summary>
        /// Creates CPU backend instances with desired configurations.
        /// </summary>
        public override IEnumerable<Backend> CreateBackends(
            CompilationProperties properties,
            ParseResult parseResult)
        {
            var simdWidths = parseResult
                .GetRequiredValue(_simdWidthOption)
                .ToArray();

            var noPoolBuffers = parseResult
                .GetValue(_noPoolBuffersOption);

            foreach (var simdWidth in simdWidths)
            {
                yield return new CPUBackend(
                    simdWidth,
                    properties.DebugSymbolsMode == DebugSymbolsMode.Default,
                    poolBuffers: !noPoolBuffers);
            }
        }
    }
}

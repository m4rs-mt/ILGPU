// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.ROCmOptions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Backends.ROCm;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace ILGPUC;

sealed partial class Program
{
    /// <summary>
    /// Represents options for the Hip backend
    /// </summary>
    sealed class ROCmOptions : BackendOptions
    {
        /// <summary>
        /// One or more ROCm GPU architecture strings (e.g. <c>gfx1100</c>) to target.
        /// </summary>
        readonly Option<string[]> _rocmArchitecturesOption = new("--rocm-arch")
        {
            Description = "ROCm architecture to use",
            DefaultValueFactory = _ => ["gfx1100"],
            AllowMultipleArgumentsPerToken = true
        };

        /// <summary>
        /// Registers all options to control ROCm compilation properties with the command.
        /// </summary>
        /// <param name="command">The command to attach options to.</param>
        public ROCmOptions(Command command) : base(BackendType.ROCm)
        {
            command.Add(_rocmArchitecturesOption);
        }

        /// <summary>
        /// Creates ROCm backend instances with desired configurations.
        /// </summary>
        public override IEnumerable<Backend> CreateBackends(
            CompilationProperties properties,
            ParseResult parseResult)
        {
            var architectures = parseResult
                .GetRequiredValue(_rocmArchitecturesOption)
                .ToArray();

            foreach (var arch in architectures)
                yield return new ROCmBackend(arch);
        }
    }
}

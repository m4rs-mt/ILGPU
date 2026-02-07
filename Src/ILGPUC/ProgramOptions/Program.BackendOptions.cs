// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.BackendOptions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using System.Collections.Generic;
using System.CommandLine;

namespace ILGPUC;

sealed partial class Program
{
    /// <summary>
    /// Abstract base class for backend-specific command-line options.
    /// Each concrete subclass registers its own options with the command and
    /// creates the corresponding <see cref="Backend"/> instances.
    /// </summary>
    /// <param name="backendType">
    /// The backend type represented by this options object.
    /// </param>
    abstract class BackendOptions(BackendType backendType)
    {
        /// <summary>The backend type represented by this options object.</summary>
        public BackendType BackendType { get; } = backendType;

        /// <summary>
        /// Creates one or more <see cref="Backend"/> instances configured from the
        /// parsed command-line values.
        /// </summary>
        /// <param name="properties">
        /// Compilation properties resolved from CLI options.
        /// </param>
        /// <param name="parseResult">The command-line parse result.</param>
        /// <returns>
        /// A sequence of <see cref="Backend"/> instances ready for code generation.
        /// </returns>
        public abstract IEnumerable<Backend> CreateBackends(
            CompilationProperties properties,
            ParseResult parseResult);
    }
}

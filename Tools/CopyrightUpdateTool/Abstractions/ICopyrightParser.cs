// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: ICopyrightParser.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CopyrightUpdateTool.Abstractions
{

    /// <summary>
    /// Represents a file parser to extract copyright information.
    /// </summary>
    public interface ICopyrightParser
    {
        /// <summary>
        /// Returns true if the file can be parsed by this parser.
        /// </summary>
        public Task<bool> CanParseAsync(
            FileInfo file,
            CancellationToken cancellationToken);

        /// <summary>
        /// Parses the file and updates the existing copyright, or adds copyright
        /// information to the file.
        /// </summary>
        public Task AddOrUpdateCopyrightAsync(
            FileInfo file,
            CancellationToken cancellationToken);
    }
}

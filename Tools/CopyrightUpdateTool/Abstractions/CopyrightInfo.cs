// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: CopyrightInfo.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace CopyrightUpdateTool.Abstractions
{
    /// <summary>
    /// Represents the extracted copyright information.
    /// </summary>
    public record CopyrightInfo
    {
        /// <summary>
        /// File contents before the copyright.
        /// </summary>
        public string Prefix { get; init; } = string.Empty;

        /// <summary>
        /// The copyright string.
        /// </summary>
        public string Copyright { get; init; } = string.Empty;

        /// <summary>
        /// File contents after the copyright.
        /// </summary>
        public string Suffix { get; init; } = string.Empty;

        /// <summary>
        /// The initial copyright year (if any), extracted from the file.
        /// </summary>
        public int? StartingYear { get; init; }
    }
}

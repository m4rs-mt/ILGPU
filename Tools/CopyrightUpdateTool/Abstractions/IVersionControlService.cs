// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: IVersionControlService.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.IO;
using System.Threading.Tasks;

namespace CopyrightUpdateTool.Abstractions
{
    enum CopyrightYearEndType
    {
        /// <summary>
        /// Use the last commit to the file to determine the ending copyright year.
        /// </summary>
        LastCommitToFile,

        /// <summary>
        /// Use the last commit to the repository to determine the ending copyright year.
        /// </summary>
        LastCommitToRepostory
    }

    /// <summary>
    /// Provides functionality to retrieve copyright information from version control.
    /// </summary>
    interface IVersionControlService
    {
        /// <summary>
        /// Returns the relative path of this file, to the repository.
        /// </summary>
        Task<string?> GetRelativePathAsync(FileInfo file);

        /// <summary>
        /// Returns the copyright year for a file.
        /// </summary>
        Task<int> GetCopyrightYearEndAsync(FileInfo file, CopyrightYearEndType type);
    }
}

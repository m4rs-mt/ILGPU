// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: NuspecCopyrightParser.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using CopyrightUpdateTool.Abstractions;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CopyrightUpdateTool.Parsers
{
    /// <summary>
    /// Updates copyright for Nuspec targets.
    /// </summary>
    class NuspecCopyrightParser : BaseCopyrightParser
    {
        #region Instance

        /// <summary>
        /// Constructs a new parser instance.
        /// </summary>
        /// <param name="versionControlService">The version control service.</param>
        public NuspecCopyrightParser(IVersionControlService versionControlService)
            : base(versionControlService)
        { }

        #endregion

        #region Methods

        /// <inheritdoc cref="BaseCopyrightParser.CanParseAsync(
        ///     FileInfo,
        ///     CancellationToken)" />
        public override Task<bool> CanParseAsync(
            FileInfo file,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                file.Name.EndsWith(
                    ".nuspec.targets",
                    StringComparison.OrdinalIgnoreCase));

        /// <inheritdoc cref="BaseCopyrightParser.ParseAsync(
        ///     FileInfo,
        ///     CancellationToken)" />
        protected async override Task<CopyrightInfo> ParseAsync(
            FileInfo file,
            CancellationToken cancellationToken)
        {
            // Search for the copyright text between <Copyright> and </Copyright> tags.
            var copyrightText = Regex.Escape(Config.CopyrightText);

            var pattern =
                $"^(?<{CaptureGroups.Prefix}>.*<Copyright\\>)"
                + $"(?<{CaptureGroups.Copyright}>{copyrightText}\\s*"
                + $"(?<{CaptureGroups.StartingYear}>[\\d]*).+"
                + $")"
                + $"(?<{CaptureGroups.Suffix}>\\</Copyright\\>.*)$";
            return await ParseUsingRegexAsync(file, pattern, cancellationToken);
        }

        /// <inheritdoc cref="BaseCopyrightParser.GenerateCopyrightAsync(
        ///     FileInfo,
        ///     int?,
        ///     CancellationToken)" />
        protected async override Task<string> GenerateCopyrightAsync(
            FileInfo file,
            int? startingYear,
            CancellationToken cancellationToken)
        {
            var endingYear = await VersionControlService.GetCopyrightYearEndAsync(
                  file,
                  CopyrightYearEndType.LastCommitToRepostory);
            var copyrightYear =
                startingYear.HasValue && startingYear.Value != endingYear
                ? $"{startingYear.Value}-{endingYear}"
                : $"{endingYear}";

            return $"{Config.CopyrightText} {copyrightYear} {Config.CopyrightOwner}" +
                $". All rights reserved.";
        }

        #endregion
    }
}

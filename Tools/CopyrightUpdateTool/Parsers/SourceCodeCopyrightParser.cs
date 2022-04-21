// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: SourceCodeCopyrightParser.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using CopyrightUpdateTool.Abstractions;
using CopyrightUpdateTool.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CopyrightUpdateTool.Parsers
{
    /// <summary>
    /// Updates copyright for source code files.
    /// </summary>
    class SourceCodeCopyrightParser : BaseCopyrightParser
    {
        #region Constants

        /// <summary>
        /// The list of supported file extensions.
        /// </summary>
        private readonly ImmutableHashSet<string> SupportedExtensions =
            ImmutableHashSet.CreateRange(
                StringComparer.OrdinalIgnoreCase,
                new[] { ".cs", ".fs", ".tt", ".ttinclude", ".ps1", ".js" });

        /// <summary>
        /// The delimeter used for padding the line.
        /// </summary>
        private const string LineDelimiter = "-";

        #endregion

        #region Static

        /// <summary>
        /// Returns the comment prefix for the given file.
        /// </summary>
        private static string GetLinePrefix(FileInfo file)
        {
            if (file.Extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
                return "## ";
            else
                return "// ";
        }

        /// <summary>
        /// Returns the filename line of the copyright header.
        /// </summary>
        private static string MakeFilenameLine(FileInfo file)
        {
            string fileName;
            if (file.Extension.Equals(".tt", StringComparison.OrdinalIgnoreCase))
            {
                var baseName = Path.GetFileNameWithoutExtension(file.FullName);
                fileName = $"{file.Name}/{baseName}.cs";
            }
            else
                fileName = file.Name;

            return $"File: {fileName}";
        }

        /// <summary>
        /// Returns the name of the project to which this file belongs.
        /// </summary>
        private static async Task<string> MakeProjectLine(
            IVersionControlService versionControlService,
            FileInfo file)
        {
            var relativePath = await versionControlService.GetRelativePathAsync(file);
            if (relativePath != null)
            {
                var parts = relativePath.DecomposePath();

                if (parts.Count > 1 &&
                    parts[0].Equals("Samples", StringComparison.OrdinalIgnoreCase))
                    return "ILGPU Samples";
                else if (parts.Count > 2 &&
                    parts[0].Equals("Src", StringComparison.OrdinalIgnoreCase) &&
                    parts[1].StartsWith(
                        "ILGPU.Algorithms",
                        StringComparison.OrdinalIgnoreCase))
                    return "ILGPU Algorithms";
            }

            return "ILGPU";
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new parser instance.
        /// </summary>
        /// <param name="versionControlService">The version control service.</param>
        public SourceCodeCopyrightParser(IVersionControlService versionControlService)
            : base(versionControlService)
        { }

        #endregion

        #region Methods

        /// <inheritdoc cref="BaseCopyrightParser.CanParseAsync(
        ///     FileInfo,
        ///     CancellationToken)" />
        public override Task<bool> CanParseAsync(
            FileInfo file,
            CancellationToken cancellationToken)
        {
            // Ignore unsupported file extensions.
            if (!SupportedExtensions.Contains(file.Extension))
                return Task.FromResult(false);

            // Ignore files that do not have a directory.
            if (file.Directory == null)
                return Task.FromResult(false);

            // Ignore files in the Resources directory.
            if (file.Directory.Name.Equals(
                "Resources",
                StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(false);

            // Ignore files in the obj directory.
            if (file.FullName.Contains($"\\obj\\"))
                return Task.FromResult(false);

            // Ignore generated C# files.
            if (file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                var templatePath = Path.ChangeExtension(file.FullName, ".tt");
                if (File.Exists(templatePath))
                    return Task.FromResult(false);
            }

            // Ignore .test.tt file (.runsettings)
            if (file.Name.Equals(".test.tt", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(false);

            // Ignore AssemblyAttributes.cs file.
            if (file.Name.Equals(
                "AssemblyAttributes.cs",
                StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        /// <inheritdoc cref="BaseCopyrightParser.ParseAsync(
        ///     FileInfo,
        ///     CancellationToken)" />
        protected async override Task<CopyrightInfo> ParseAsync(
            FileInfo file,
            CancellationToken cancellationToken)
        {
            // Search for the copyright block between two lines of "// ----------".
            var linePrefix = GetLinePrefix(file);
            var prefix = Regex.Escape(linePrefix + LineDelimiter.Repeat(10));
            var delim = Regex.Escape(LineDelimiter);
            var copyrightText = Regex.Escape(Config.CopyrightText);

            var pattern =
                $"^(?<{CaptureGroups.Prefix}>.*)"
                + $"(?<{CaptureGroups.Copyright}>{prefix}.*?{copyrightText}\\s*"
                + $"(?<{CaptureGroups.StartingYear}>[\\d]*).*?{prefix}[{delim}]+"
                + $")"
                + $"(?<{CaptureGroups.Suffix}>.*)$";
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
            // Helper function to align text in the middle of a line.
            static string CenterAlignString(string text, int lineLength)
            {
                var paddingLength = lineLength - text.Length;
                var leftPad = " ".Repeat((paddingLength + 1) / 2);
                return leftPad + text;
            }

            var linePrefix = GetLinePrefix(file);
            var lineLength = Config.LineLength - linePrefix.Length;
            var alignmentLength = Config.LineLength - (linePrefix.Length * 2) - 1;
            var dashLine = LineDelimiter.Repeat(lineLength);

            var projectLine = await MakeProjectLine(VersionControlService, file);
            var endingYear = await VersionControlService.GetCopyrightYearEndAsync(
                file,
                CopyrightYearEndType.LastCommitToFile);
            var copyrightYear =
                startingYear.HasValue && startingYear.Value != endingYear
                ? $"{startingYear.Value}-{endingYear}"
                : $"{endingYear}";
            var copyrightLine =
                $"{Config.CopyrightText} {copyrightYear} {Config.CopyrightOwner}";
            var websiteLine = Config.CopyrightOwnerWebsite;
            var fileLine = MakeFilenameLine(file);

            var lines = new List<string>();
            lines.Add(dashLine);
            lines.Add(CenterAlignString(projectLine, alignmentLength));
            lines.Add(CenterAlignString(copyrightLine, alignmentLength));
            lines.Add(CenterAlignString(websiteLine, alignmentLength));
            lines.Add(string.Empty);
            lines.Add(fileLine);
            lines.Add(string.Empty);
            lines.AddRange(Config.CopyrightLicenseText);
            lines.Add(dashLine);

            // Append the comment prefix to each line.
            var commentLines = lines.Select(line => $"{linePrefix}{line}".TrimEnd());
            var copyrightHeader = string.Join(Environment.NewLine, commentLines);

            var hasExistingCopyrightHeader = startingYear.HasValue;
            if (!hasExistingCopyrightHeader)
                copyrightHeader += Environment.NewLine + Environment.NewLine;
            return copyrightHeader;
        }

        #endregion
    }
}

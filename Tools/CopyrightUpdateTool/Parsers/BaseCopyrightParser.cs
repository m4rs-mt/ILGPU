// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: BaseCopyrightParser.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using CopyrightUpdateTool.Abstractions;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CopyrightUpdateTool.Parsers
{
    /// <summary>
    /// Helper class that can simplify the implementation of copright parsers.
    /// </summary>
    abstract class BaseCopyrightParser : ICopyrightParser
    {
        #region Constants

        /// <summary>
        /// Known capture groups to use with <see cref="ParseUsingRegexAsync(
        ///     FileInfo,
        ///     string,
        ///     CancellationToken)"/>
        /// </summary>
        public static class CaptureGroups
        {
            public const string Prefix = "prefix";
            public const string Copyright = "copyright";
            public const string Suffix = "suffix";
            public const string StartingYear = "startingYear";
        }

        #endregion

        #region Static

        /// <summary>
        /// Implementation of parsing a file using regular expression with known capture
        /// groups (prefix, copyright, suffix, startingYear).
        /// </summary>
        /// <param name="file">The file to process.</param>
        /// <param name="regexPattern">The regex pattern to use.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public static async Task<CopyrightInfo> ParseUsingRegexAsync(
            FileInfo file,
            string regexPattern,
            CancellationToken cancellationToken)
        {
            // Local function
            static int? ToNullableInt(string s)
            {
                if (int.TryParse(s, out int i))
                    return i;
                return null;
            }

            // Read existing file contents.
            var filePath = file.FullName;
            var fileContents = await File.ReadAllTextAsync(filePath, cancellationToken);

            var matches = Regex.Match(
                fileContents,
                regexPattern,
                RegexOptions.Singleline);
            if (matches.Success)
            {
                var prefix = matches.Groups[CaptureGroups.Prefix].Value;
                var copyright = matches.Groups[CaptureGroups.Copyright].Value;
                var suffix = matches.Groups[CaptureGroups.Suffix].Value;
                var startingYear = ToNullableInt(
                    matches.Groups[CaptureGroups.StartingYear].Value);

                return new CopyrightInfo()
                {
                    Prefix = prefix,
                    Copyright = copyright,
                    Suffix = suffix,
                    StartingYear = startingYear
                };
            }
            else
            {
                return new CopyrightInfo() { Suffix = fileContents };
            }
        }

        /// <summary>
        /// Detects if the file contains a UTF8 BOM.
        /// </summary>
        /// <param name="file">The file to check.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns true if the file contains a UTF8 BOM.</returns>
        private static async Task<bool> HasUTF8BomAsync(
            FileInfo file,
            CancellationToken cancellationToken)
        {
            using var fs = new FileStream(file.FullName, FileMode.Open);

            var bytes = new byte[3];
            var numBytesRead = await fs.ReadAsync(bytes, cancellationToken);
            if (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return true;

            return false;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs the base copyright parser.
        /// </summary>
        /// <param name="versionControlService">The version control service.</param>
        public BaseCopyrightParser(IVersionControlService versionControlService)
        {
            VersionControlService = versionControlService;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The version control service instance, used to provide copyright information.
        /// </summary>
        protected IVersionControlService VersionControlService { get; }

        #endregion

        #region Methods

        /// <inheritdoc cref="ICopyrightParser.CanParseAsync(
        ///     FileInfo,
        ///     CancellationToken)" />
        public abstract Task<bool> CanParseAsync(
            FileInfo file,
            CancellationToken cancellationToken);

        /// <inheritdoc cref="ICopyrightParser.AddOrUpdateCopyrightAsync(
        ///     FileInfo,
        ///     CancellationToken)" />
        public async Task AddOrUpdateCopyrightAsync(
            FileInfo file,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Parse the copyright information from the file.
            var copyrightInfo = await ParseAsync(file, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Build the new copyright.
            var newCopyright = await GenerateCopyrightAsync(
                file,
                copyrightInfo.StartingYear,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Write the updated copyright and file contents.
            var newFileContents =
                copyrightInfo.Prefix
                + newCopyright
                + copyrightInfo.Suffix;

            if (!newCopyright.Equals(copyrightInfo.Copyright))
                await WriteContentsToFileAsync(file, newFileContents, cancellationToken);
        }

        /// <summary>
        /// Parses the file and extracts the copyright information.
        /// </summary>
        protected abstract Task<CopyrightInfo> ParseAsync(
            FileInfo file,
            CancellationToken cancellationToken);

        /// <summary>
        /// Builds the new copyright string.
        /// </summary>
        /// <param name="file">The file to process.</param>
        /// <param name="startingYear">The starting year for the existing file.</param>
        /// <returns>The new copyright string.</returns>
        protected abstract Task<string> GenerateCopyrightAsync(
            FileInfo file,
            int? startingYear,
            CancellationToken cancellationToken);

        protected async virtual Task WriteContentsToFileAsync(
            FileInfo file,
            string fileContents,
            CancellationToken cancellationToken)
        {
            var hasUTF8Bom = await HasUTF8BomAsync(file, cancellationToken);
            var utf8encoding = new UTF8Encoding(hasUTF8Bom);

            await File.WriteAllTextAsync(
                file.FullName,
                fileContents,
                utf8encoding,
                cancellationToken);
        }

        #endregion
    }
}

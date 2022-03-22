// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: Runner.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using CopyrightUpdateTool.Abstractions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CopyrightUpdateTool.Util
{
    class Runner
    {
        /// <summary>
        /// List of copyright parsers to use.
        /// </summary>
        private readonly IEnumerable<ICopyrightParser> Parsers;

        /// <summary>
        /// Constructs a new runner instance.
        /// </summary>
        /// <param name="parsers"></param>
        public Runner(IEnumerable<ICopyrightParser> parsers)
        {
            Parsers = parsers.ToList();
        }

        /// <summary>
        /// Updates the copright of files in the supplied path.
        /// </summary>
        /// <param name="path">The base path to process.</param>
        /// <param name="cancellationToken">The cancellation token to use.</param>
        public async Task UpdateCopyrightAsync(
            string path,
            CancellationToken cancellationToken)
        {
            // Recursively enumerate all files from the base directory.
            var filePaths =
                Directory.EnumerateFiles(
                    path,
                    "*",
                    SearchOption.AllDirectories);

            // Update the copyright information in each file.
            await Parallel.ForEachAsync(
                filePaths,
                cancellationToken,
                async (filePath, token) =>
                {
                    // Find the first parser that can parse the file.
                    var file = new FileInfo(filePath);
                    ICopyrightParser? foundParser = null;

                    foreach (var parser in Parsers)
                    {
                        token.ThrowIfCancellationRequested();

                        var canParse = await parser.CanParseAsync(
                            file,
                            token);
                        if (!canParse)
                            continue;
                        foundParser = parser;
                        break;
                    }

                    // Update the copyright.
                    if (foundParser == null)
                        return;
                    token.ThrowIfCancellationRequested();
                    await foundParser.AddOrUpdateCopyrightAsync(file, token);
                });
        }
    }
}

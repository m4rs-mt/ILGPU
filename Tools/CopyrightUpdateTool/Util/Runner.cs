// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Runner.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using CopyrightUpdateTool.Abstractions;
using System;
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
        /// Directories that never contain hand-authored source we need to update.
        /// Skipping at enumeration time avoids descending into multi-thousand-file
        /// trees (the Snapshots submodule, build output, package caches, etc.).
        /// </summary>
        private static readonly HashSet<string> ExcludedDirectoryNames =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".git",
                "bin",
                "obj",
                "node_modules",
                "Snapshots",
            };

        /// <summary>
        /// List of copyright parsers to use.
        /// </summary>
        private readonly IReadOnlyList<ICopyrightParser> Parsers;

        /// <summary>
        /// Constructs a new runner instance.
        /// </summary>
        public Runner(IEnumerable<ICopyrightParser> parsers)
        {
            Parsers = parsers.ToList();
        }

        /// <summary>
        /// Updates the copyright of files in the supplied path.
        /// </summary>
        public async Task UpdateCopyrightAsync(
            string path,
            CancellationToken cancellationToken)
        {
            // Update the copyright information in each file.
            await Parallel.ForEachAsync(
                EnumerateCandidateFiles(path),
                cancellationToken,
                async (filePath, token) =>
                {
                    var file = new FileInfo(filePath);
                    foreach (var parser in Parsers)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!await parser.CanParseAsync(file, token))
                            continue;
                        token.ThrowIfCancellationRequested();
                        await parser.AddOrUpdateCopyrightAsync(file, token);
                        return;
                    }
                });
        }

        /// <summary>
        /// Recursively yields every file under <paramref name="root"/>, skipping
        /// directories whose name is in <see cref="ExcludedDirectoryNames"/>. The
        /// blanket <see cref="Directory.EnumerateFiles(string, string, SearchOption)"/>
        /// previously used here descended into the snapshot submodule, build output
        /// and the .git database — surfacing thousands of files that no parser will
        /// ever match but each cost a per-file dispatch.
        /// </summary>
        private static IEnumerable<string> EnumerateCandidateFiles(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.TryPop(out var dir))
            {
                IEnumerable<string> subdirs;
                IEnumerable<string> files;
                try
                {
                    subdirs = Directory.EnumerateDirectories(dir);
                    files = Directory.EnumerateFiles(dir);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                foreach (var subdir in subdirs)
                {
                    var name = Path.GetFileName(subdir);
                    if (!ExcludedDirectoryNames.Contains(name))
                        stack.Push(subdir);
                }

                foreach (var file in files)
                    yield return file;
            }
        }
    }
}

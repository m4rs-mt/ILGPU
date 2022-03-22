// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: VersionControlService.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using CopyrightUpdateTool.Abstractions;
using LibGit2Sharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CopyrightUpdateTool.Util
{
    /// <summary>
    /// Provides information from Git repositories. Can be used by plugin parsers to
    /// determine the ending copyright year.
    /// </summary>
    class VersionControlService : IVersionControlService
    {
        #region Static

        /// <summary>
        /// Return the git repository for the file.
        /// </summary>
        private static Repository? GetRepositoryFromFile(FileInfo file)
        {
            var repositoryPath = Repository.Discover(file.FullName);
            if (repositoryPath != null)
                return new Repository(repositoryPath);
            else
                return default;
        }

        /// <summary>
        /// Returns the relative path of the file to the repository.
        /// </summary>
        private static string GetRelativePath(Repository repository, FileInfo file)
        {
            return Path.GetRelativePath(
                repository.Info.WorkingDirectory,
                file.FullName);
        }

        /// <summary>
        /// Returns the ending copyright year from the list of commits.
        /// </summary>
        private static int GetCopyrightYearEndFromCommits(
            IEnumerable<Commit> commits) =>
                commits
                .Select(commit =>
                {
                    return Math.Max(commit.Author.When.Year, commit.Committer.When.Year);
                })
                .Max();

        #endregion

        #region Methods

        /// <inheritdoc cref="IVersionControlService.GetRelativePathAsync(string?)" />
        public Task<string?> GetRelativePathAsync(FileInfo file)
        {
            var repository = GetRepositoryFromFile(file);
            if (repository != null)
            {
                return Task.FromResult<string?>(GetRelativePath(repository, file));
            }

            return Task.FromResult<string?>(default);
        }

        /// <inheritdoc cref="IVersionControlService.GetCopyrightYearEndAsync(
        ///     FileInfo,
        ///     CopyrightYearEndType)" />
        public Task<int> GetCopyrightYearEndAsync(
            FileInfo file,
            CopyrightYearEndType type)
        {
            int fallbackYear = DateTime.Now.Year;
            int copyrightYear;

            var repository = GetRepositoryFromFile(file);
            if (repository != null)
            {
                var fileRelativePath = GetRelativePath(repository, file);
                var fileGitPath = fileRelativePath.Replace('\\', '/');
                IEnumerable<Commit>? commits = null;

                if (type == CopyrightYearEndType.LastCommitToFile)
                {
                    var logEntries = repository.Commits.QueryBy(
                        fileGitPath,
                        new CommitFilter()
                        {
                            SortBy = CommitSortStrategies.Topological
                        });
                    if (logEntries != null)
                        commits = logEntries.Select(logEntry => logEntry.Commit);
                }

                if (commits == null || !commits.Any())
                    commits = new[] { repository.Head.Tip };

                copyrightYear = GetCopyrightYearEndFromCommits(commits);
            }
            else
            {
                copyrightYear = fallbackYear;
            }

            return Task.FromResult(copyrightYear);
        }

        #endregion
    }
}

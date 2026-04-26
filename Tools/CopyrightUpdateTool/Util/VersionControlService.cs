// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2026 ILGPU Project
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
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CopyrightUpdateTool.Util
{
    /// <summary>
    /// Provides information from Git repositories. Can be used by plugin parsers to
    /// determine the ending copyright year. The service holds a single shared
    /// <see cref="Repository"/> handle and amortizes per-file commit lookups by
    /// walking history once and caching the latest year that touched each path.
    /// </summary>
    class VersionControlService : IVersionControlService
    {
        #region Instance

        private readonly Repository? _repository;
        private readonly int _fallbackYear = DateTime.Now.Year;
        private readonly int _headTipYear;
        private readonly object _buildLock = new();

        // Built lazily on first LastCommitToFile lookup. Keyed by repo-relative
        // POSIX path; value is max(author, committer) year over all commits that
        // touched the path (matching the original per-file QueryBy semantics).
        private Dictionary<string, int>? _lastYearByPath;

        public VersionControlService(Repository? repository)
        {
            _repository = repository;
            if (repository?.Head?.Tip is { } tip)
            {
                WorkingDirectory = repository.Info.WorkingDirectory;
                _headTipYear = Math.Max(
                    tip.Author.When.Year,
                    tip.Committer.When.Year);
            }
            else
            {
                _headTipYear = _fallbackYear;
            }
        }

        #endregion

        #region Properties

        public string? WorkingDirectory { get; }

        #endregion

        #region Methods

        public Task<string?> GetRelativePathAsync(FileInfo file)
        {
            if (WorkingDirectory == null)
                return Task.FromResult<string?>(null);

            return Task.FromResult<string?>(
                Path.GetRelativePath(WorkingDirectory, file.FullName));
        }

        public Task<int> GetCopyrightYearEndAsync(
            FileInfo file,
            CopyrightYearEndType type)
        {
            if (_repository == null || WorkingDirectory == null)
                return Task.FromResult(_fallbackYear);

            if (type == CopyrightYearEndType.LastCommitToRepostory)
                return Task.FromResult(_headTipYear);

            var relative = Path.GetRelativePath(WorkingDirectory, file.FullName);
            var gitPath = relative.Replace('\\', '/');

            var byPath = EnsureLastYearByPathBuilt();
            return Task.FromResult(
                byPath.TryGetValue(gitPath, out var year) ? year : _headTipYear);
        }

        /// <summary>
        /// Walks the repository's commit history once, building a path → max-year
        /// dictionary. Subsequent <see cref="CopyrightYearEndType.LastCommitToFile"/>
        /// queries are O(1) lookups. Thread-safe under double-checked lock; LibGit2Sharp
        /// <see cref="Repository"/> is not safe for concurrent use, so we serialize
        /// the walk and let the post-build read-only Dictionary be consumed in parallel.
        /// </summary>
        private Dictionary<string, int> EnsureLastYearByPathBuilt()
        {
            if (_lastYearByPath is { } cached)
                return cached;

            lock (_buildLock)
            {
                if (_lastYearByPath is { } cached2)
                    return cached2;

                var dict = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var commit in _repository!.Commits.QueryBy(
                    new CommitFilter { SortBy = CommitSortStrategies.Topological }))
                {
                    int year = Math.Max(
                        commit.Author.When.Year,
                        commit.Committer.When.Year);
                    var parent = commit.Parents.FirstOrDefaultCommit();
                    var changes = _repository.Diff.Compare<TreeChanges>(
                        parent?.Tree,
                        commit.Tree);
                    foreach (var change in changes)
                    {
                        Update(dict, change.Path, year);
                        if (!string.IsNullOrEmpty(change.OldPath) &&
                            !string.Equals(
                                change.OldPath,
                                change.Path,
                                StringComparison.Ordinal))
                        {
                            Update(dict, change.OldPath, year);
                        }
                    }
                }
                _lastYearByPath = dict;
                return dict;
            }

            static void Update(Dictionary<string, int> d, string path, int y)
            {
                d[path] = d.TryGetValue(path, out var cur) ? Math.Max(cur, y) : y;
            }
        }

        #endregion
    }

    internal static class CommitParentsExtensions
    {
        /// <summary>
        /// Returns the first parent of a commit, or null for the root commit.
        /// </summary>
        public static Commit? FirstOrDefaultCommit(this IEnumerable<Commit> parents)
        {
            foreach (var parent in parents)
                return parent;
            return null;
        }
    }
}

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXDebugInfoGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.Util;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents a debug information scope.
    /// </summary>
    public sealed class PTXDebugInfoGeneratorScope
    {
        #region Instance

        internal PTXDebugInfoGeneratorScope(PTXDebugInfoGenerator parent)
        {
            Parent = parent;
            ResetLocation();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent debug information generator.
        /// </summary>
        public PTXDebugInfoGenerator Parent { get; }

        /// <summary>
        /// Returns the current location.
        /// </summary>
        public FileLocation Current { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Generates debug information for the given node.
        /// </summary>
        /// <param name="builder">The target string builder to write to.</param>
        /// <param name="node">The node.</param>
        public void GenerateDebugInfo(StringBuilder builder, Node node)
        {
            if (!(node.Location is FileLocation location) || Current == location)
                return;
            Parent.GenerateDebugInfo(builder, node, location);
            Current = location;
        }

        /// <summary>
        /// Reset all location information.
        /// </summary>
        public void ResetLocation() => Current = null;

        #endregion
    }

    /// <summary>
    /// A general debug info generator for PTX kernels.
    /// </summary>
    public abstract class PTXDebugInfoGenerator : DisposeBase
    {
        #region Instance

        /// <summary>
        /// Constructs a new generic debug info generator.
        /// </summary>
        protected PTXDebugInfoGenerator() { }

        #endregion

        #region Methods

        /// <summary>
        /// Begins a new debug information scope.
        /// </summary>
        public PTXDebugInfoGeneratorScope BeginScope() =>
            new PTXDebugInfoGeneratorScope(this);

        /// <summary>
        /// Generates debug information for the given node.
        /// </summary>
        /// <param name="builder">The target string builder to write to.</param>
        /// <param name="node">The node.</param>
        /// <param name="location">The current location.</param>
        protected internal abstract void GenerateDebugInfo(
            StringBuilder builder,
            Node node,
            FileLocation location);

        /// <summary>
        /// Generate required debug-information sections in PTX code.
        /// </summary>
        /// <param name="builder">The target string builder to write to.</param>
        public abstract void GenerateDebugSections(StringBuilder builder);

        #endregion
    }

    /// <summary>
    /// Represents an info generator that does not generate anything.
    /// </summary>
    sealed class PTXNoDebugInfoGenerator : PTXDebugInfoGenerator
    {
        #region Static

        /// <summary>
        /// An empty debug information generator.
        /// </summary>
        public static readonly PTXNoDebugInfoGenerator Empty =
            new PTXNoDebugInfoGenerator();

        #endregion

        #region Instance

        private PTXNoDebugInfoGenerator() { }

        #endregion

        #region Methods

        /// <summary>
        /// Generates no debug information.
        /// </summary>
        protected internal override void GenerateDebugInfo(
            StringBuilder builder,
            Node node,
            FileLocation location)
        { }

        /// <summary>
        /// Generates no debug information section.
        /// </summary>
        public override void GenerateDebugSections(StringBuilder builder) { }

        #endregion
    }

    /// <summary>
    /// Generates line-based debug information for PTX kernels.
    /// </summary>
    class PTXDebugLineInfoGenerator : PTXDebugInfoGenerator
    {
        #region Instance

        private readonly Dictionary<string, int> fileMapping =
            new Dictionary<string, int>();

        /// <summary>
        /// Constructs a debug information generator.
        /// </summary>
        public PTXDebugLineInfoGenerator() { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current synchronization lock.
        /// </summary>
        protected ReaderWriterLockSlim SyncLock { get; } = new ReaderWriterLockSlim();

        #endregion

        #region Methods

        /// <summary>
        /// Gets or creates a new file entry.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <returns>The file index.</returns>
        private int RegisterFile(FileLocation location)
        {
            SyncLock.EnterUpgradeableReadLock();
            try
            {
                if (!fileMapping.TryGetValue(
                    location.FileName,
                    out int fileIndex))
                {
                    SyncLock.EnterWriteLock();
                    try
                    {
                        fileIndex = fileMapping.Count + 1;
                        fileMapping.Add(location.FileName, fileIndex);
                        OnRegisterFile(location);
                    }
                    finally
                    {
                        SyncLock.ExitWriteLock();
                    }
                }
                return fileIndex;
            }
            finally
            {
                SyncLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Invoked when a new file mapping entry has been registered.
        /// </summary>
        /// <param name="location">The current location.</param>
        protected virtual void OnRegisterFile(FileLocation location) { }

        /// <summary>
        /// Generates a line-based debug information string.
        /// </summary>
        protected internal override void GenerateDebugInfo(
            StringBuilder builder,
            Node node,
            FileLocation location)
        {
            // Register file or reuse an existing index
            int fileIndex = RegisterFile(location);

            // Append a debug annotation for this value
            builder.AppendLine();
            builder.Append("\t.loc\t");
            builder.Append(fileIndex);
            builder.Append(' ');
            builder.Append(location.StartLine);
            builder.Append(' ');
            builder.AppendLine(location.StartColumn.ToString());
        }

        /// <summary>
        /// Generates a debug sections header including file information.
        /// </summary>
        public override void GenerateDebugSections(StringBuilder builder)
        {
            Debug.Assert(builder != null, "Invalid builder");

            // Append all file declarations
            builder.AppendLine();
            foreach (var fileEntry in fileMapping)
            {
                builder.Append("\t.file\t");
                builder.Append(fileEntry.Value);
                builder.Append(" \"");
                builder.Append(fileEntry.Key.Replace('\\', '/'));
                builder.AppendLine("\"");
            }

            // Append debug section to enable debugging support
            builder.AppendLine();
            builder.AppendLine(".section.debug_info { }");
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                SyncLock.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// Generates line-based location information and inserts the referenced
    /// source lines into the generated PTX code.
    /// </summary>
    sealed class PTXDebugSourceLineInfoGenerator : PTXDebugLineInfoGenerator
    {
        #region Instance

        private readonly Dictionary<string, string[]> fileMapping =
            new Dictionary<string, string[]>();

        /// <summary>
        /// Constructs a debug information generator.
        /// </summary>
        public PTXDebugSourceLineInfoGenerator() { }

        #endregion

        #region Methods

        [SuppressMessage(
            "Microsoft.Design",
            "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Exceptions of any kind are ignored when trying to load" +
            "the referenced source files")]
        protected override void OnRegisterFile(FileLocation location)
        {
            base.OnRegisterFile(location);

            // Try to load file
            if (!File.Exists(location.FileName) ||
                fileMapping.ContainsKey(location.FileName))
            {
                return;
            }

            try
            {
                var lines = File.ReadAllLines(location.FileName);
                fileMapping.Add(location.FileName, lines);
            }
            catch
            {
                // Ignore exceptions
            }
        }

        /// <summary>
        /// Generates a line-based debug information string including inline source
        /// line information.
        /// </summary>
        protected internal override void GenerateDebugInfo(
            StringBuilder builder,
            Node node,
            FileLocation location)
        {
            base.GenerateDebugInfo(builder, node, location);

            // Try to load file contents
            SyncLock.EnterReadLock();
            try
            {
                if (fileMapping.TryGetValue(location.FileName, out var lines))
                {
                    // Append associated source lines
                    for (
                        int i = location.StartLine;
                        i <= location.EndLine;
                        ++i)
                    {
                        builder.Append("\t// ");
                        builder.AppendLine(lines[i - 1].Trim());
                    }
                }
                else
                {
                    // No debug information could be found
                    builder.AppendLine("\t// <No Source Line>");
                }
            }
            finally
            {
                SyncLock.ExitReadLock();
            }
        }

        #endregion
    }
}

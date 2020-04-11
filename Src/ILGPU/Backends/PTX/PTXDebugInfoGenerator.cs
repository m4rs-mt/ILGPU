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

using ILGPU.Frontend.DebugInformation;
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
            Current = SequencePoint.Invalid;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent debug information generator.
        /// </summary>
        public PTXDebugInfoGenerator Parent { get; }

        /// <summary>
        /// Returns the current sequence point.
        /// </summary>
        public SequencePoint Current { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Generates debug information for the given node.
        /// </summary>
        /// <param name="builder">The target string builder to write to.</param>
        /// <param name="node">The node.</param>
        public void GenerateDebugInfo(StringBuilder builder, Node node)
        {
            Debug.Assert(builder != null, "Invalid builder");
            Debug.Assert(node != null, "Invalid node");

            var sequencePoint = node.SequencePoint;
            if (!sequencePoint.IsValid || Current == sequencePoint)
                return;
            Parent.GenerateDebugInfo(builder, node, sequencePoint);
            Current = sequencePoint;
        }

        /// <summary>
        /// Reset all sequence point information.
        /// </summary>
        public void ResetSequencePoints() => Current = SequencePoint.Invalid;

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
        /// <param name="sequencePoint">The current sequence point.</param>
        protected internal abstract void GenerateDebugInfo(
            StringBuilder builder,
            Node node,
            in SequencePoint sequencePoint);

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
            in SequencePoint sequencePoint)
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
        /// <param name="sequencePoint">The current sequence point.</param>
        /// <returns>The file index.</returns>
        private int RegisterFile(in SequencePoint sequencePoint)
        {
            SyncLock.EnterUpgradeableReadLock();
            try
            {
                if (!fileMapping.TryGetValue(
                    sequencePoint.FileName,
                    out int fileIndex))
                {
                    SyncLock.EnterWriteLock();
                    try
                    {
                        fileIndex = fileMapping.Count + 1;
                        fileMapping.Add(sequencePoint.FileName, fileIndex);
                        OnRegisterFile(sequencePoint);
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
        /// <param name="sequencePoint">The current sequence point.</param>
        protected virtual void OnRegisterFile(in SequencePoint sequencePoint) { }

        /// <summary>
        /// Generates a line-based debug information string.
        /// </summary>
        protected internal override void GenerateDebugInfo(
            StringBuilder builder,
            Node node,
            in SequencePoint sequencePoint)
        {
            // Register file or reuse an existing index
            int fileIndex = RegisterFile(sequencePoint);

            // Append a debug annotation for this value
            builder.AppendLine();
            builder.Append("\t.loc\t");
            builder.Append(fileIndex);
            builder.Append(' ');
            builder.Append(sequencePoint.StartLine);
            builder.Append(' ');
            builder.AppendLine(sequencePoint.StartColumn.ToString());
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
        protected override void OnRegisterFile(in SequencePoint sequencePoint)
        {
            base.OnRegisterFile(sequencePoint);

            // Try to load file
            if (!File.Exists(sequencePoint.FileName) ||
                fileMapping.ContainsKey(sequencePoint.FileName))
            {
                return;
            }

            try
            {
                var lines = File.ReadAllLines(sequencePoint.FileName);
                fileMapping.Add(sequencePoint.FileName, lines);
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
            in SequencePoint sequencePoint)
        {
            base.GenerateDebugInfo(builder, node, sequencePoint);

            // Try to load file contents
            SyncLock.EnterReadLock();
            try
            {
                if (fileMapping.TryGetValue(sequencePoint.FileName, out var lines))
                {
                    // Append associated source lines
                    for (
                        int i = sequencePoint.StartLine;
                        i <= sequencePoint.EndLine;
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

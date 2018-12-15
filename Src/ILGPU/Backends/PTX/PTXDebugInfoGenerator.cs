// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXDebugInfoGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.DebugInformation;
using ILGPU.IR;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// A general debug info generator for PTX kernels.
    /// </summary>
    abstract class PTXDebugInfoGenerator
    {
        #region Instance

        /// <summary>
        /// Constructs a new generic debug info generator.
        /// </summary>
        protected PTXDebugInfoGenerator()
        {
            ResetSequencePoints();
        }

        #endregion

        #region Properties

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
            GenerateDebugInfo(builder, node, sequencePoint);
            Current = sequencePoint;
        }

        /// <summary>
        /// Reset all sequence point information.
        /// </summary>
        public void ResetSequencePoints()
        {
            Current = SequencePoint.Invalid;
        }

        /// <summary>
        /// Generates debug information for the given node.
        /// </summary>
        /// <param name="builder">The target string builder to write to.</param>
        /// <param name="node">The node.</param>
        /// <param name="sequencePoint">The current sequence point.</param>
        protected abstract void GenerateDebugInfo(
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
        public static readonly PTXNoDebugInfoGenerator Empty = new PTXNoDebugInfoGenerator();

        #endregion

        #region Instance

        private PTXNoDebugInfoGenerator() { }

        #endregion

        #region Methods

        /// <summary cref="PTXDebugInfoGenerator.GenerateDebugInfo(StringBuilder, Node, in SequencePoint)"/>
        protected override void GenerateDebugInfo(
            StringBuilder builder,
            Node node,
            in SequencePoint sequencePoint) { }

        /// <summary cref="PTXDebugInfoGenerator.GenerateDebugSections(StringBuilder)"/>
        public override void GenerateDebugSections(StringBuilder builder) { }

        #endregion
    }

    /// <summary>
    /// Generates line-based debug information for PTX kernels.
    /// </summary>
    class PTXDebugLineInfoGenerator : PTXDebugInfoGenerator
    {
        #region Instance

        private readonly Dictionary<string, int> fileMapping = new Dictionary<string, int>();

        /// <summary>
        /// Constructs a debug information generator.
        /// </summary>
        public PTXDebugLineInfoGenerator() { }

        #endregion

        #region Methods

        /// <summary cref="PTXDebugInfoGenerator.GenerateDebugInfo(StringBuilder, Node, in SequencePoint)"/>
        protected override void GenerateDebugInfo(
            StringBuilder builder,
            Node node,
            in SequencePoint sequencePoint)
        {
            // Register file or reuse an existing index
            if (!fileMapping.TryGetValue(sequencePoint.FileName, out int fileIndex))
            {
                fileIndex = fileMapping.Count + 1;
                fileMapping.Add(sequencePoint.FileName, fileIndex);
            }

            // Append a debug annotation for this value
            builder.AppendLine();
            builder.Append("\t.loc\t");
            builder.Append(fileIndex);
            builder.Append(' ');
            builder.Append(sequencePoint.StartLine);
            builder.Append(' ');
            builder.AppendLine(sequencePoint.StartColumn.ToString());
        }

        /// <summary cref="PTXDebugInfoGenerator.GenerateDebugSections(StringBuilder)"/>
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
    }

    /// <summary>
    /// Generates line-based location information and inserts the referenced
    /// source lines into the generated PTX code.
    /// </summary>
    sealed class PTXDebugSourceLineInfoGenerator : PTXDebugLineInfoGenerator
    {
        #region Instance

        private readonly Dictionary<string, string[]> fileMapping = new Dictionary<string, string[]>();

        /// <summary>
        /// Constructs a debug information generator.
        /// </summary>
        public PTXDebugSourceLineInfoGenerator() { }

        #endregion

        #region Methods

        /// <summary cref="PTXDebugInfoGenerator.GenerateDebugInfo(StringBuilder, Node, in SequencePoint)"/>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Exceptions of any kind are ignored when trying to load the referenced source files")]
        protected override void GenerateDebugInfo(
            StringBuilder builder,
            Node node,
            in SequencePoint sequencePoint)
        {
            base.GenerateDebugInfo(builder, node, sequencePoint);

            // Try to load file
            if (!File.Exists(sequencePoint.FileName))
                return;

            try
            {
                if (!fileMapping.TryGetValue(sequencePoint.FileName, out string[] lines))
                {
                    lines = File.ReadAllLines(sequencePoint.FileName);
                    fileMapping.Add(sequencePoint.FileName, lines);
                }
                for (int i = sequencePoint.StartLine; i <= sequencePoint.EndLine; ++i)
                {
                    builder.Append("\t// ");
                    builder.AppendLine(lines[i - 1].Trim());
                }
            }
            catch
            {
                // No debug information could be found
                builder.AppendLine("\t// <No Source Line>");
            }
        }

        #endregion
    }
}

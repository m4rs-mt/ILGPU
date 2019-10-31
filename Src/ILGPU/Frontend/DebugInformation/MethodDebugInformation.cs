// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: MethodDebugInformation.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;

namespace ILGPU.Frontend.DebugInformation
{
    /// <summary>
    /// Represents method debug information.
    /// </summary>
    public sealed class MethodDebugInformation
    {
        #region Instance

        /// <summary>
        /// Constructs method debug information.
        /// </summary>
        /// <param name="assemblyDebugInformation">The parent assembly debug information</param>
        /// <param name="methodBase">The target method.</param>
        /// <param name="debugInformationHandle">The debug handle of the given method.</param>
        internal MethodDebugInformation(
            AssemblyDebugInformation assemblyDebugInformation,
            MethodBase methodBase,
            MethodDebugInformationHandle debugInformationHandle)
        {
            MethodBase = methodBase;
            AssemblyDebugInformation = assemblyDebugInformation;
            DebugInformationHandle = debugInformationHandle;
            SequencePoints = ImmutableArray<SequencePoint>.Empty;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated assembly debug information.
        /// </summary>
        public AssemblyDebugInformation AssemblyDebugInformation { get; }

        /// <summary>
        /// Returns the associated method base.
        /// </summary>
        public MethodBase MethodBase { get; }

        /// <summary>
        /// Returns the associated sequence points.
        /// </summary>
        public ImmutableArray<SequencePoint> SequencePoints { get; private set; }

        /// <summary>
        /// Returns the associated metadata reader.
        /// </summary>
        internal MetadataReader MetadataReader => AssemblyDebugInformation.MetadataReader;

        /// <summary>
        /// Returns the associated the method debug-information handle.
        /// </summary>
        internal MethodDebugInformationHandle DebugInformationHandle { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Loads the requested sequence points.
        /// </summary>
        internal void LoadSequencePoints()
        {
            if (SequencePoints.Length > 0)
                return;

            var debugInformation = MetadataReader.GetMethodDebugInformation(DebugInformationHandle);

            // Gather sequence points
            var sequencePointsCollection = debugInformation.GetSequencePoints();
            var sequencePointEnumerator = sequencePointsCollection.GetEnumerator();
            int numSequencePoints;
            for (numSequencePoints = 0; sequencePointEnumerator.MoveNext();)
            {
                var sequencePoint = sequencePointEnumerator.Current;
                if (sequencePoint.IsHidden || sequencePoint.Document.IsNil)
                    continue;
                ++numSequencePoints;
            }
            var sequencePoints = ImmutableArray.CreateBuilder<SequencePoint>(numSequencePoints);
            sequencePointEnumerator = sequencePointsCollection.GetEnumerator();
            while (sequencePointEnumerator.MoveNext())
            {
                var sequencePoint = sequencePointEnumerator.Current;
                if (sequencePoint.IsHidden || sequencePoint.Document.IsNil)
                    continue;
                var doc = MetadataReader.GetDocument(sequencePoint.Document);
                sequencePoints.Add(new SequencePoint(
                    doc.Name.IsNil ? string.Empty : MetadataReader.GetString(doc.Name),
                    sequencePoint.Offset,
                    sequencePoint.StartColumn,
                    sequencePoint.EndColumn,
                    sequencePoint.StartLine,
                    sequencePoint.EndLine));
            }
            SequencePoints = sequencePoints.MoveToImmutable();
        }

        /// <summary>
        /// Creates a new sequence-point enumerator for the current method.
        /// </summary>
        /// <returns>The created sequence-point enumerator.</returns>
        public SequencePointEnumerator CreateSequencePointEnumerator() =>
            new SequencePointEnumerator(SequencePoints);

        /// <summary>
        /// Creates a new scope enumerator for the current method.
        /// </summary>
        /// <returns>The create scope enumerator.</returns>
        public MethodScopeEnumerator CreateScopeEnumerator() =>
            new MethodScopeEnumerator(this);

        #endregion
    }
}

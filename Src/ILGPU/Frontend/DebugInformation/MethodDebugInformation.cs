// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: MethodDebugInformation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

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
        #region Static

        /// <summary>
        /// Loads sequence points for the given information handle.
        /// </summary>
        /// <param name="readerOperationProvider">The parent reader provider.</param>
        /// <param name="handle">The source information handle.</param>
        /// <returns>The array of all sequence points.</returns>
        private static ImmutableArray<SequencePoint> LoadSequencePoints<TProvider>(
            TProvider readerOperationProvider,
            MethodDefinitionHandle handle)
            where TProvider : IMetadataReaderOperationProvider
        {
            using var operation = readerOperationProvider.BeginOperation();
            MetadataReader reader = operation.Reader;
            var debugInformation = reader.GetMethodDebugInformation(handle);

            // Gather sequence points
            var result = ImmutableArray.CreateBuilder<SequencePoint>();
            foreach (var sequencePoint in debugInformation.GetSequencePoints())
            {
                if (sequencePoint.IsHidden || sequencePoint.Document.IsNil)
                    continue;
                var doc = reader.GetDocument(sequencePoint.Document);
                result.Add(new SequencePoint(
                    doc.Name.IsNil ? string.Empty : reader.GetString(doc.Name),
                    sequencePoint.Offset,
                    sequencePoint.StartColumn,
                    sequencePoint.EndColumn,
                    sequencePoint.StartLine,
                    sequencePoint.EndLine));
            }

            return result.ToImmutable();
        }

        /// <summary>
        /// Loads method scopes for the given information handle.
        /// </summary>
        /// <param name="readerOperationProvider">The parent reader provider.</param>
        /// <param name="handle">The source information handle.</param>
        /// <returns>The array of all method scopes.</returns>
        private static ImmutableArray<MethodScope> LoadScopes<TProvider>(
            TProvider readerOperationProvider,
            MethodDefinitionHandle handle)
            where TProvider : IMetadataReaderOperationProvider
        {
            using var operation = readerOperationProvider.BeginOperation();
            MetadataReader reader = operation.Reader;
            var scopes = reader.GetLocalScopes(handle);

            // Gather method scopes
            var result = ImmutableArray.CreateBuilder<MethodScope>(scopes.Count);
            using var enumerator = scopes.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var localScope = reader.GetLocalScope(enumerator.Current);
                result.Add(new MethodScope(localScope, reader));
            }

            return result.MoveToImmutable();
        }

        #endregion

        #region Instance

        /// <summary>
        /// All associated sequence points.
        /// </summary>
        private ImmutableArray<SequencePoint> sequencePoints;

        /// <summary>
        /// All associated method scopes.
        /// </summary>
        private ImmutableArray<MethodScope> methodScopes;

        /// <summary>
        /// The internal synchronization object.
        /// </summary>
        private readonly object syncLock = new object();

        /// <summary>
        /// Constructs method debug information.
        /// </summary>
        /// <param name="assemblyDebugInformation">
        /// The parent assembly debug information
        /// </param>
        /// <param name="methodBase">The target method.</param>
        /// <param name="handle">
        /// The debug handle of the given method.
        /// </param>
        internal MethodDebugInformation(
            AssemblyDebugInformation assemblyDebugInformation,
            MethodBase methodBase,
            MethodDefinitionHandle handle)
        {
            MethodBase = methodBase;
            AssemblyDebugInformation = assemblyDebugInformation;
            Handle = handle;

            sequencePoints = default;
            methodScopes = default;
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
        /// Returns the associated the method debug-information handle.
        /// </summary>
        internal MethodDefinitionHandle Handle { get; }

        /// <summary>
        /// Returns all sequence points of the current method.
        /// </summary>
        public ImmutableArray<SequencePoint> SequencePoints
        {
            get
            {
                lock (syncLock)
                {
                    return !sequencePoints.IsDefault
                        ? sequencePoints
                        : (sequencePoints = LoadSequencePoints(
                            AssemblyDebugInformation,
                            Handle));
                }
            }
        }

        /// <summary>
        /// Returns all method scops of the current method.
        /// </summary>
        public ImmutableArray<MethodScope> MethodScopes
        {
            get
            {
                lock (syncLock)
                {
                    return !methodScopes.IsDefault
                        ? methodScopes
                        : (methodScopes = LoadScopes(
                            AssemblyDebugInformation,
                            Handle));
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new sequence-point enumerator for the current method.
        /// </summary>
        /// <returns>The created sequence-point enumerator.</returns>
        public SequencePointEnumerator CreateSequencePointEnumerator() =>
            new SequencePointEnumerator(SequencePoints);

        #endregion
    }
}

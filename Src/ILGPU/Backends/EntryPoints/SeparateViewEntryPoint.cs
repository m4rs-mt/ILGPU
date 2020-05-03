// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: SeparateViewEntryPoint.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.EntryPoints
{
    /// <summary>
    /// An entry point that differentiates between views and data structures.
    /// </summary>
    /// <remarks>
    /// This is useful since many APIs (like OpenCL) require intrinsic support for
    /// passing views to kernels via separate API calls.
    /// </remarks>
    public class SeparateViewEntryPoint : EntryPoint
    {
        #region Nested Types

        /// <summary>
        /// Represents a single view parameter in the scope of a kernel.
        /// </summary>
        public sealed class ViewParameter
        {
            #region Instance

            /// <summary>
            /// Constructs a new view parameter.
            /// </summary>
            /// <param name="index">The view parameter index.</param>
            /// <param name="parameter">The parameter info.</param>
            /// <param name="sourceChain">The source access chain.</param>
            /// <param name="targetAccess">The target access.</param>
            /// <param name="elementType">The element type of the view.</param>
            /// <param name="viewType">The source view type.</param>
            internal ViewParameter(
                int index,
                in (TypeInformationManager.TypeInformation, int) parameter,
                FieldAccessChain sourceChain,
                FieldAccess targetAccess,
                Type elementType,
                Type viewType)
            {
                Index = index;
                ParameterType = parameter.Item1;
                ParameterIndex = parameter.Item2;
                SourceChain = sourceChain;
                TargetAccess = targetAccess;
                ElementType = elementType;
                ViewType = viewType;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the index of the i-th view entry.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// Returns the associated parameter type.
            /// </summary>
            public TypeInformationManager.TypeInformation ParameterType { get; }

            /// <summary>
            /// Returns the associated kernel-parameter index.
            /// </summary>
            public int ParameterIndex { get; }

            /// <summary>
            /// Returns the access to resolve the view parameter.
            /// </summary>
            public FieldAccessChain SourceChain { get; }

            /// <summary>
            /// Returns the target access.
            /// </summary>
            public FieldAccess TargetAccess { get; }

            /// <summary>
            /// Returns the underlying element type.
            /// </summary>
            public Type ElementType { get; }

            /// <summary>
            /// Returns the associated array-view type.
            /// </summary>
            public Type ViewType { get; }

            #endregion
        }

        /// <summary>
        /// Represents a readonly list of view parameters.
        /// </summary>
        public readonly struct ViewParameterCollection : IReadOnlyList<ViewParameter>
        {
            #region Nested Types

            /// <summary>
            /// An enumerator to enumerate all view parameters in this collection.
            /// </summary>
            public struct Enumerator : IEnumerator<ViewParameter>
            {
                /// <summary>
                /// Constructs a new parameter enumerator.
                /// </summary>
                /// <param name="collection">The parent collection.</param>
                internal Enumerator(in ViewParameterCollection collection)
                {
                    EntryPoint = collection.EntryPoint;
                    Index = collection.StartIndex - 1;
                    EndIndex = collection.EndIndex;
                }

                /// <summary>
                /// Returns the parent entry point.
                /// </summary>
                public SeparateViewEntryPoint EntryPoint { get; }

                /// <summary>
                /// Returns the current index.
                /// </summary>
                public int Index { get; private set; }

                /// <summary>
                /// Returns the end index (exclusive).
                /// </summary>
                public int EndIndex { get; }

                /// <summary cref="IEnumerator{T}.Current"/>
                public ViewParameter Current => EntryPoint.ViewParameters[Index];

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                /// <summary cref="IDisposable.Dispose"/>
                public void Dispose() { }

                /// <summary cref="IEnumerator.MoveNext"/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext() => ++Index < EndIndex;

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();
            }

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new parameter collection.
            /// </summary>
            /// <param name="entryPoint">The parent entry point.</param>
            /// <param name="startIndex">The start index (inclusive).</param>
            /// <param name="endIndex">The end index (exclusive).</param>
            internal ViewParameterCollection(
                SeparateViewEntryPoint entryPoint,
                int startIndex,
                int endIndex)
            {
                EntryPoint = entryPoint;
                StartIndex = startIndex;
                EndIndex = endIndex;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent entry point.
            /// </summary>
            public SeparateViewEntryPoint EntryPoint { get; }

            /// <summary>
            /// Returns the start index (inclusive).
            /// </summary>
            public int StartIndex { get; }

            /// <summary>
            /// Returns the end index (exclusive).
            /// </summary>
            public int EndIndex { get; }

            /// <summary>
            /// Returns the number of view parameters.
            /// </summary>
            public int Count => EndIndex - StartIndex;

            /// <summary>
            /// Returns the i-th view parameter.
            /// </summary>
            /// <param name="index">The index of the view parameter to get.</param>
            /// <returns>The desired view parameter.</returns>
            public ViewParameter this[int index] =>
                EntryPoint.ViewParameters[StartIndex + index];

            #endregion

            #region Methods

            /// <summary>
            /// Returns an enumerator to enumerate all parameters in this collection.
            /// </summary>
            /// <returns>
            /// An enumerator to enumerate all parameters in this collection.
            /// </returns>
            public Enumerator GetEnumerator() => new Enumerator(this);

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            IEnumerator<ViewParameter> IEnumerable<ViewParameter>.GetEnumerator() =>
                GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Maps parameter indices to view-parameter intervals.
        /// </summary>
        private readonly Dictionary<int, (int, int)> viewParameterMapping =
            new Dictionary<int, (int, int)>();

        /// <summary>
        /// Constructs a new entry point targeting the given method.
        /// </summary>
        /// <param name="description">The entry point description.</param>
        /// <param name="sharedMemory">The shared memory specification.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="typeInformationManager">The information manager to use.</param>
        /// <param name="numImplementationFieldsPerView">
        /// The number of fields per view.
        /// </param>
        public SeparateViewEntryPoint(
            EntryPointDescription description,
            in SharedMemorySpecification sharedMemory,
            in KernelSpecialization specialization,
            TypeInformationManager typeInformationManager,
            int numImplementationFieldsPerView)
            : base(description, sharedMemory, specialization)
        {
            if (numImplementationFieldsPerView < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(numImplementationFieldsPerView));
            }
            NumImplementationFieldsPerView = numImplementationFieldsPerView;

            var builder = ImmutableArray.CreateBuilder<ViewParameter>(
                Parameters.Count);
            for (int i = 0, e = Parameters.Count; i < e; ++i)
            {
                var typeInfo = typeInformationManager.GetTypeInfo(Parameters[i]);
                int sourceIndex = builder.Count;
                ResolveVirtualViewParameters(
                    builder,
                    (typeInfo, i),
                    typeInfo,
                    FieldAccessChain.Empty,
                    new FieldAccess(0));

                var targetLength = builder.Count;
                if (targetLength - sourceIndex > 0)
                    viewParameterMapping.Add(i, (sourceIndex, targetLength));
            }

            ViewParameters = builder.ToImmutable();
        }

        /// <summary>
        /// Returns the number of fields per view.
        /// </summary>
        public int NumImplementationFieldsPerView { get; }

        /// <summary>
        /// Analyzes the given parameter types and resolves all virtual
        /// view parameters that should be passed separately.
        /// </summary>
        /// <param name="builder">The target builder to append to.</param>
        /// <param name="parameter">The parameter info.</param>
        /// <param name="type">The current type.</param>
        /// <param name="sourceChain">The source access chain.</param>
        /// <param name="targetAccess">The target field access.</param>
        private void ResolveVirtualViewParameters(
            ImmutableArray<ViewParameter>.Builder builder,
            in (TypeInformationManager.TypeInformation, int) parameter,
            TypeInformationManager.TypeInformation type,
            FieldAccessChain sourceChain,
            FieldAccess targetAccess)
        {
            // Check whether we have found an array view that has
            // to be passed separately
            if (type.ManagedType.IsArrayViewType(out var elementType))
            {
                // We have found an array view...
                builder.Add(new ViewParameter(
                    builder.Count,
                    parameter,
                    sourceChain,
                    targetAccess,
                    elementType,
                    type.ManagedType));
                targetAccess = targetAccess.Add(NumImplementationFieldsPerView);
            }

            // Resolve view field recursively
            for (int i = 0, e = type.NumFields; i < e; ++i)
            {
                var fieldOffset = type.FieldOffsets[i];
                var fieldType = type.GetFieldTypeInfo(i);
                ResolveVirtualViewParameters(
                    builder,
                    parameter,
                    fieldType,
                    sourceChain.Append(i),
                    targetAccess.Add(fieldOffset));
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of all separate view parameters.
        /// </summary>
        public int NumViewParameters => ViewParameters.Length;

        /// <summary>
        /// Contains all separate view parameters.
        /// </summary>
        public ImmutableArray<ViewParameter> ViewParameters { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to get view parameters for the given kernel-parameter index.
        /// </summary>
        /// <param name="parameterIndex">The kernel-parameter index.</param>
        /// <param name="viewParameters">The resolved view parameters (if any).</param>
        /// <returns>True, if view parameters could be determined.</returns>
        public bool TryGetViewParameters(
            int parameterIndex,
            out ViewParameterCollection viewParameters)
        {
            viewParameters = default;
            if (!viewParameterMapping.TryGetValue(parameterIndex, out var viewInterval))
                return false;
            viewParameters = new ViewParameterCollection(
                this,
                viewInterval.Item1,
                viewInterval.Item2);
            return true;
        }

        /// <summary>
        /// Get view parameters for the given kernel-parameter index.
        /// </summary>
        /// <param name="parameterIndex">The kernel-parameter index.</param>
        /// <returns>The collection of view parameters.</returns>
        public ViewParameterCollection GetViewParameters(int parameterIndex)
        {
            var viewInterval = viewParameterMapping[parameterIndex];
            return new ViewParameterCollection(
                this,
                viewInterval.Item1,
                viewInterval.Item2);
        }

        #endregion
    }
}

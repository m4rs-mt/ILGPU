// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: SeparateViewEntryPoint.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Types;
using ILGPUC.IR.Values;
using ILGPUC.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace ILGPUC.Backends.EntryPoints;

/// <summary>
/// An entry point that differentiates between views and data structures.
/// </summary>
/// <remarks>
/// This is useful since many APIs (like OpenCL) require intrinsic support for
/// passing views to kernels via separate API calls.
/// </remarks>
sealed class SeparateViewEntryPoint : EntryPoint
{
    #region Nested Types

    /// <summary>
    /// Represents a single view parameter in the scope of a kernel.
    /// </summary>
    /// <param name="Index">The view parameter index.</param>
    /// <param name="ParameterType">The parameter type info.</param>
    /// <param name="ParameterIndex">The parameter index.</param>
    /// <param name="SourceChain">The source access chain.</param>
    /// <param name="TargetAccess">The target access.</param>
    /// <param name="ElementType">The element type of the view.</param>
    /// <param name="ViewType">The source view type.</param>
    internal sealed record class ViewParameter(
        int Index,
        Type ParameterType,
        int ParameterIndex,
        FieldAccessChain SourceChain,
        FieldAccess TargetAccess,
        Type ElementType,
        Type ViewType)
    {
        /// <summary>
        /// Constructs a new view parameter.
        /// </summary>
        public ViewParameter(
            int index,
            in (Type, int) parameter,
            FieldAccessChain sourceChain,
            FieldAccess targetAccess,
            Type elementType,
            Type viewType)
            : this(
                index,
                parameter.Item1,
                parameter.Item2,
                sourceChain,
                targetAccess,
                elementType,
                viewType)
        { }
    }

    /// <summary>
    /// Represents a readonly list of view parameters.
    /// </summary>
    internal readonly struct ViewParameterCollection
    {
        #region Nested Types

        /// <summary>
        /// An enumerator to enumerate all view parameters in this collection.
        /// </summary>
        /// <remarks>
        /// Constructs a new parameter enumerator.
        /// </remarks>
        /// <param name="collection">The parent collection.</param>
        internal struct Enumerator(ViewParameterCollection collection)
        {
            /// <summary>
            /// Returns the current index.
            /// </summary>
            public int Index { get; private set; } = collection.StartIndex - 1;

            /// <summary>
            /// Returns the end index (exclusive).
            /// </summary>
            public int EndIndex { get; } = collection.EndIndex;

            /// <summary cref="IEnumerator{T}.Current"/>
            public readonly ViewParameter Current =>
                collection.EntryPoint.ViewParameters[Index];

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => ++Index < EndIndex;
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
        public Enumerator GetEnumerator() => new(this);

        #endregion
    }

    #endregion

    #region Instance

    /// <summary>
    /// Maps parameter indices to view-parameter intervals.
    /// </summary>
    private readonly Dictionary<int, (int, int)> _viewParameterMapping = [];

    /// <summary>
    /// Constructs a new entry point targeting the given method.
    /// </summary>
    /// <param name="method">The entry point method.</param>
    /// <param name="isGrouped">
    /// True if the kernel method is an explicitly grouped kernel.
    /// </param>
    /// <param name="typeInformationManager">The information manager to use.</param>
    /// <param name="numImplementationFieldsPerView">
    /// The number of fields per view.
    /// </param>
    public SeparateViewEntryPoint(
        MethodInfo method,
        bool isGrouped,
        TypeInformationManager typeInformationManager,
        int numImplementationFieldsPerView)
        : base(method, isGrouped)
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
                (typeInfo.ManagedType, i),
                typeInfo,
                FieldAccessChain.Empty,
                new FieldAccess(0));

            var targetLength = builder.Count;
            if (targetLength - sourceIndex > 0)
                _viewParameterMapping.Add(i, (sourceIndex, targetLength));
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
        in (Type, int) parameter,
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
        if (!_viewParameterMapping.TryGetValue(parameterIndex, out var viewInterval))
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
        var viewInterval = _viewParameterMapping[parameterIndex];
        return new ViewParameterCollection(
            this,
            viewInterval.Item1,
            viewInterval.Item2);
    }

    #endregion
}

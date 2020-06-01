// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: GridIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    enum GridIntrinsicKind
    {
        GetGridIndex,
        GetGroupIndex,

        GetGridDimension,
        GetGroupDimension,
    }

    /// <summary>
    /// Marks grid methods that are built in.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class GridIntrinsicAttribute : IntrinsicAttribute
    {
        public GridIntrinsicAttribute(
            GridIntrinsicKind intrinsicKind,
            DeviceConstantDimension3D dimension)
        {
            IntrinsicKind = intrinsicKind;
            Dimension = dimension;
        }

        public override IntrinsicType Type => IntrinsicType.Grid;

        /// <summary>
        /// The associated constant dimension.
        /// </summary>
        public DeviceConstantDimension3D Dimension { get; }

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public GridIntrinsicKind IntrinsicKind { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles grid operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleGridOperation(
            ref InvocationContext context,
            GridIntrinsicAttribute attribute)
        {
            var builder = context.Builder;
            return attribute.IntrinsicKind switch
            {
                GridIntrinsicKind.GetGridIndex => builder.CreateGridIndexValue(
                    context.Location,
                    attribute.Dimension),
                GridIntrinsicKind.GetGroupIndex => builder.CreateGroupIndexValue(
                    context.Location,
                    attribute.Dimension),
                GridIntrinsicKind.GetGridDimension => builder.CreateGridDimensionValue(
                    context.Location,
                    attribute.Dimension),
                _ => builder.CreateGroupDimensionValue(
                    context.Location,
                    attribute.Dimension),
            };
        }
    }
}

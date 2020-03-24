// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: IRBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using ILGPU.Util;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    /// <summary>
    /// An IR builder that can construct IR nodes.
    /// </summary>
    /// <remarks>Members of this class are thread safe.</remarks>
    public abstract partial class IRBuilder : DisposeBase
    {
        #region Instance

        /// <summary>
        /// Constructs a new IR builder.
        /// </summary>
        /// <param name="basicBlock">The current basic block.</param>
        protected IRBuilder(BasicBlock basicBlock)
        {
            Debug.Assert(basicBlock != null, "Invalid basic block");

            BasicBlock = basicBlock;
            Context = Method.Context;
            UseConstantPropagation = !Context.HasFlags(
                ContextFlags.DisableConstantPropagation);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public IRContext Context { get; }

        /// <summary>
        /// Returns the parent method.
        /// </summary>
        public Method Method => BasicBlock.Method;

        /// <summary>
        /// Returns the associated basic block.
        /// </summary>
        public BasicBlock BasicBlock { get; }

        /// <summary>
        /// Returns the void type.
        /// </summary>
        public VoidType VoidType => Context.VoidType;

        /// <summary>
        /// Returns the string type.
        /// </summary>
        public StringType StringType => Context.StringType;

        /// <summary>
        /// Returns the current index type.
        /// </summary>
        public TypeNode IndexType => Context.IndexType;

        /// <summary>
        /// True, if the IR builder should use constant propagation.
        /// </summary>
        public bool UseConstantPropagation { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new unique node marker.
        /// </summary>
        /// <returns>The new node marker.</returns>
        public NodeMarker NewNodeMarker() => Context.NewNodeMarker();

        /// <summary>
        /// Creates a node that represents an <see cref="Accelerator.CurrentType"/> property.
        /// </summary>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateAcceleratorTypeValue() =>
            Append(new AcceleratorTypeValue(Context, BasicBlock));

        /// <summary>
        /// Creates a node that represents a <see cref="Warp.WarpSize"/> property.
        /// </summary>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateWarpSizeValue() =>
            Append(new WarpSizeValue(Context, BasicBlock));

        /// <summary>
        /// Creates a node that represents a <see cref="Warp.LaneIdx"/> property.
        /// </summary>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateLaneIdxValue() =>
            Append(new LaneIdxValue(Context, BasicBlock));

        /// <summary>
        /// Creates a node that represents a <see cref="Grid.Index"/> property.
        /// </summary>
        /// <param name="dimension">The constant dimension.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGridIndexValue(DeviceConstantDimension3D dimension)
        {
            Debug.Assert(
                dimension >= DeviceConstantDimension3D.X && dimension <= DeviceConstantDimension3D.Z,
                "Invalid dimension value");
            return Append(new GridIndexValue(
                Context,
                BasicBlock,
                dimension));
        }

        /// <summary>
        /// Creates a node that represents a <see cref="Group.Index"/> property.
        /// </summary>
        /// <param name="dimension">The constant dimension.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGroupIndexValue(DeviceConstantDimension3D dimension)
        {
            Debug.Assert(
                dimension >= DeviceConstantDimension3D.X && dimension <= DeviceConstantDimension3D.Z,
                "Invalid dimension value");
            return Append(new GroupIndexValue(
                Context,
                BasicBlock,
                dimension));
        }

        /// <summary>
        /// Creates a node that represents a <see cref="Grid.Dimension"/> property.
        /// </summary>
        /// <param name="dimension">The constant dimension.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGridDimensionValue(DeviceConstantDimension3D dimension)
        {
            Debug.Assert(
                dimension >= DeviceConstantDimension3D.X && dimension <= DeviceConstantDimension3D.Z,
                "Invalid dimension value");
            return Append(new GridDimensionValue(
                Context,
                BasicBlock,
                dimension));
        }

        /// <summary>
        /// Creates a node that represents of a <see cref="Group.Dimension"/> property.
        /// </summary>
        /// <param name="dimension">The constant dimension.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGroupDimensionValue(DeviceConstantDimension3D dimension)
        {
            Debug.Assert(
                dimension >= DeviceConstantDimension3D.X && dimension <= DeviceConstantDimension3D.Z,
                "Invalid dimension value");
            return Append(new GroupDimensionValue(
                Context,
                BasicBlock,
                dimension));
        }

        /// <summary>
        /// Creates a node that represents the native size of the
        /// give type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateSizeOf(TypeNode type)
        {
            Debug.Assert(type != null, "Invalid type node");
            return Append(new SizeOfValue(
                Context,
                BasicBlock,
                type));
        }

        /// <summary>
        /// Creates a node that represents an undefined value.
        /// </summary>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateUndefined() => Context.UndefinedValue;

        /// <summary>
        /// Creates a node that represents a managed runtime handle.
        /// </summary>
        /// <param name="handle">The runtime handle.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateRuntimeHandle(object handle)
        {
            Debug.Assert(handle != null, "Invalid runtime handle");
            return Append(new HandleValue(
                BasicBlock,
                Context.HandleType,
                handle));
        }

        /// <summary>
        /// Creates a new index structure instance.
        /// </summary>
        /// <param name="dimension">The dimension value.</param>
        /// <returns>The created index type.</returns>
        public ValueReference CreateIndex(ValueReference dimension) =>
            CreateIndex(ImmutableArray.Create(dimension));

        /// <summary>
        /// Creates a new index structure instance.
        /// </summary>
        /// <param name="dimensions">The dimension values.</param>
        /// <returns>The created index type.</returns>
        public ValueReference CreateIndex(ImmutableArray<ValueReference> dimensions) =>
            CreateStructure(dimensions);

        /// <summary>
        /// Creates an instantiated phi value.
        /// </summary>
        /// <param name="phiValue">The phi value to create.</param>
        /// <returns>The created node.</returns>
        protected abstract PhiValue CreatePhiValue(PhiValue phiValue);

        /// <summary>
        /// Creates an instantiated terminator.
        /// </summary>
        /// <typeparam name="T">The terminator value type.</typeparam>
        /// <param name="node">The terminator to create.</param>
        /// <returns>The created node.</returns>
        protected abstract T CreateTerminator<T>(T node)
            where T : TerminatorValue;

        /// <summary>
        /// Append a new value.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="node">The node to create.</param>
        /// <returns>The created node.</returns>
        protected abstract T Append<T>(T node)
            where T : Value;

        #endregion
    }
}

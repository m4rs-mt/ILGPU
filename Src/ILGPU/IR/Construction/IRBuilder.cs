// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IRBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using ILGPU.Util;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Construction
{
    /// <summary>
    /// An IR builder that can construct IR nodes.
    /// </summary>
    /// <remarks>Members of this class are thread safe.</remarks>
    public abstract partial class IRBuilder : DisposeBase, ILocation
    {
        #region Instance

        /// <summary>
        /// Constructs a new IR builder.
        /// </summary>
        /// <param name="basicBlock">The current basic block.</param>
        protected IRBuilder(BasicBlock basicBlock)
        {
            BasicBlock = basicBlock;
            Context = Method.Context;
            UseConstantPropagation = !Context.HasFlags(
                ContextFlags.DisableConstantPropagation);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated type context.
        /// </summary>
        public IRTypeContext TypeContext { get; }

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
        public VoidType VoidType => TypeContext.VoidType;

        /// <summary>
        /// Returns the string type.
        /// </summary>
        public StringType StringType => TypeContext.StringType;

        /// <summary>
        /// True, if the IR builder should use constant propagation.
        /// </summary>
        public bool UseConstantPropagation { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Formats an error message to include specific exception information.
        /// </summary>
        /// <param name="message">The source error message.</param>
        /// <returns>The formatted error message.</returns>
        public string FormatErrorMessage(string message) =>
            BasicBlock.FormatErrorMessage(message);

        /// <summary>
        /// Creates a new initializer that is bound to the current block.
        /// </summary>
        /// <returns>The created value initializer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueInitializer GetInitializer(Location location) =>
            new ValueInitializer(Context, BasicBlock, location);

        /// <summary>
        /// Creates a node that represents an <see cref="Accelerator.CurrentType"/>
        /// property.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateAcceleratorTypeValue(Location location) =>
            Append(new AcceleratorTypeValue(GetInitializer(location)));

        /// <summary>
        /// Creates a node that represents a <see cref="Warp.WarpSize"/> property.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateWarpSizeValue(Location location) =>
            Append(new WarpSizeValue(GetInitializer(location)));

        /// <summary>
        /// Creates a node that represents a <see cref="Warp.LaneIdx"/> property.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateLaneIdxValue(Location location) =>
            Append(new LaneIdxValue(GetInitializer(location)));

        /// <summary>
        /// Creates a node that represents a <see cref="Grid.Index"/> property.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="dimension">The constant dimension.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGridIndexValue(
            Location location,
            DeviceConstantDimension3D dimension)
        {
            if (dimension < DeviceConstantDimension3D.X ||
                dimension > DeviceConstantDimension3D.Z)
            {
                throw location.GetArgumentException(nameof(dimension));
            }
            return Append(new GridIndexValue(
                GetInitializer(location),
                dimension));
        }

        /// <summary>
        /// Creates a node that represents a <see cref="Group.Index"/> property.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="dimension">The constant dimension.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGroupIndexValue(
            Location location,
            DeviceConstantDimension3D dimension)
        {
            if (dimension < DeviceConstantDimension3D.X ||
                dimension > DeviceConstantDimension3D.Z)
            {
                throw location.GetArgumentException(nameof(dimension));
            }
            return Append(new GroupIndexValue(
                GetInitializer(location),
                dimension));
        }

        /// <summary>
        /// Creates a node that represents a <see cref="Grid.Dimension"/> property.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="dimension">The constant dimension.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGridDimensionValue(
            Location location,
            DeviceConstantDimension3D dimension)
        {
            if (dimension < DeviceConstantDimension3D.X ||
                dimension > DeviceConstantDimension3D.Z)
            {
                throw location.GetArgumentException(nameof(dimension));
            }
            return Append(new GridDimensionValue(
                GetInitializer(location),
                dimension));
        }

        /// <summary>
        /// Creates a node that represents of a <see cref="Group.Dimension"/> property.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="dimension">The constant dimension.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateGroupDimensionValue(
            Location location,
            DeviceConstantDimension3D dimension)
        {
            if (dimension < DeviceConstantDimension3D.X ||
                dimension > DeviceConstantDimension3D.Z)
            {
                throw location.GetArgumentException(nameof(dimension));
            }
            return Append(new GroupDimensionValue(
                GetInitializer(location),
                dimension));
        }

        /// <summary>
        /// Creates a node that represents the native size of the given type.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="type">The type.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateSizeOf(Location location, TypeNode type)
        {
            location.AssertNotNull(type);
            return CreatePrimitiveValue(location, type.Size);
        }

        /// <summary>
        /// Creates a node that represents the native size of the given type.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="type">The type.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateLongSizeOf(Location location, TypeNode type) =>
            CreateConvertToInt64(
                location,
                CreateSizeOf(location, type));

        /// <summary>
        /// Creates a node that represents the native offset of the specified field
        /// index.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="type">The type.</param>
        /// <param name="fieldIndex">The field index.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateOffsetOf(
            Location location,
            TypeNode type,
            int fieldIndex)
        {
            location.AssertNotNull(type);

            return CreatePrimitiveValue(
                location,
                type is StructureType structureType
                ? structureType.GetOffset(fieldIndex)
                : 0);
        }

        /// <summary>
        /// Creates a node that represents an undefined value.
        /// </summary>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateUndefined() => Context.UndefinedValue;

        /// <summary>
        /// Creates a node that represents a managed runtime handle.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="handle">The runtime handle.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateRuntimeHandle(Location location, object handle)
        {
            if (handle == null)
                throw location.GetArgumentNullException(nameof(handle));
            return Append(new HandleValue(
                GetInitializer(location),
                handle));
        }

        /// <summary>
        /// Creates a new index structure instance.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="dimension">The dimension value.</param>
        /// <returns>The created index type.</returns>
        public ValueReference CreateIndex(Location location, ValueReference dimension)
        {
            var instance = CreateDynamicStructure(location, 1);
            instance.Add(dimension);
            return instance.Seal();
        }

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

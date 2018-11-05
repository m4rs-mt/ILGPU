// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: IRBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    /// <summary>
    /// Represents flags for an <see cref="IRBuilder"/> instance.
    /// </summary>
    [Flags]
    public enum IRBuilderFlags
    {
        /// <summary>
        /// The default flags.
        /// </summary>
        None = 0,

        /// <summary>
        /// Preserves all top-level functions by reusing existing
        /// functions instead of defining new ones.
        /// </summary>
        PreserveTopLevelFunctions = 1 << 0,
    }

    /// <summary>
    /// An IR builder that can construct IR nodes.
    /// </summary>
    /// <remarks>Members of this class are thread safe.</remarks>
    public sealed partial class IRBuilder : DisposeBase
    {
        #region Instance

        /// <summary>
        /// The synchronization object.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// Maps methods to top-level function builders.
        /// </summary>
        private readonly FunctionMapping<FunctionBuilder> functionMapping =
            new FunctionMapping<FunctionBuilder>();

        /// <summary>
        /// Unified values for CSE.
        /// </summary>
        private readonly Dictionary<UnifiedValue, UnifiedValue> unifiedValues =
            new Dictionary<UnifiedValue, UnifiedValue>();

        /// <summary>
        /// Constructs a new IR builder.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="flags">The builder flags.</param>
        internal IRBuilder(IRContext context, IRBuilderFlags flags)
        {
            Debug.Assert(context != null, "Invalid context");
            Context = context;
            Flags = flags;
            Generation = context.CurrentGeneration;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public IRContext Context { get; }

        /// <summary>
        /// Returns the associated builder flags.
        /// </summary>
        public IRBuilderFlags Flags { get; }

        /// <summary>
        /// Returns the generation of the builder.
        /// </summary>
        public ValueGeneration Generation { get; }

        /// <summary>
        /// Returns the void type.
        /// </summary>
        public VoidType VoidType => Context.VoidType;

        /// <summary>
        /// Returns the memory type.
        /// </summary>
        public MemoryType MemoryType => Context.MemoryType;

        /// <summary>
        /// Returns the string type.
        /// </summary>
        public StringType StringType => Context.StringType;

        /// <summary>
        /// Returns the current index type.
        /// </summary>
        public StructureType IndexType => Context.IndexType;

        /// <summary>
        /// Returns true if top-level functions have to be preserved.
        /// </summary>
        public bool PreserveTopLevelFunctions =>
            (Flags & IRBuilderFlags.PreserveTopLevelFunctions) ==
            IRBuilderFlags.PreserveTopLevelFunctions;

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new unique node marker.
        /// </summary>
        /// <returns>The new node marker.</returns>
        public NodeMarker NewNodeMarker()
        {
            return Context.NewNodeMarker();
        }

        /// <summary>
        /// Creates a new rebuilder that works on the given scope using
        /// the default rebuilder flags.
        /// </summary>
        /// <param name="scope">The used scope.</param>
        /// <returns>Teh created rebuilder.</returns>
        public IRRebuilder CreateRebuilder(Scope scope) =>
            CreateRebuilder(scope, IRRebuilderFlags.None);

        /// <summary>
        /// Creates a new rebuilder that works on the given scope.
        /// </summary>
        /// <param name="scope">The used scope.</param>
        /// <param name="flags">The rebuilder flags.</param>
        /// <returns>Teh created rebuilder.</returns>
        public IRRebuilder CreateRebuilder(Scope scope, IRRebuilderFlags flags)
        {
            return new IRRebuilder(this, scope, flags);
        }

        /// <summary>
        /// Creates a node that represents a <see cref="Warp.WarpSize"/> property.
        /// </summary>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateWarpSizeValue()
        {
            return Context.CreateInstantiated(new WarpSizeValue(
                Generation,
                CreatePrimitiveType(BasicValueType.Int32)));
        }

        /// <summary>
        /// Creates a node that represents a <see cref="Warp.LaneIdx"/> property.
        /// </summary>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateLaneIdxValue()
        {
            return Context.CreateInstantiated(new LaneIdxValue(
                Generation,
                CreatePrimitiveType(BasicValueType.Int32)));
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
            return Context.CreateInstantiated(new GridDimensionValue(
                Generation,
                dimension,
                CreatePrimitiveType(BasicValueType.Int32)));
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
            return Context.CreateInstantiated(new GroupDimensionValue(
                Generation,
                dimension,
                CreatePrimitiveType(BasicValueType.Int32)));
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
            return Context.CreateInstantiated(new SizeOfValue(
                Generation,
                type,
                CreatePrimitiveType(BasicValueType.Int32)));
        }

        /// <summary>
        /// Creates a unified value.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="node">The node to create.</param>
        /// <returns>The created node.</returns>
        private T CreateUnifiedValue<T>(T node)
            where T : UnifiedValue
        {
            Debug.Assert(node != null, "Invalid unified value");
#if VERIFICATION
            Context.VerifyGeneration(node);
#endif

            lock (syncRoot)
            {
                if (!unifiedValues.TryGetValue(node, out UnifiedValue result))
                {
                    result = node;
                    Context.CreateInstantiated(node);
                    unifiedValues.Add(node, node);
                }
                return result as T;
            }
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            Context.FinalizeBuilder(this, functionMapping);
        }

        #endregion
    }
}

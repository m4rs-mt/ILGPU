// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PureValueBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;

namespace ILGPUC.IR.PureValues.Construction;

/// <summary>
/// An IR builder that can construct IR nodes.
/// </summary>
/// <remarks>Members of this class are thread safe.</remarks>
/// <param name="generation">The current generation.</param>
/// <param name="location">The current location.</param>
abstract partial class PureValueBuilder(Generation generation, Location location) :
    ILocation,
    IGenerationObject
{
    #region Properties

    /// <summary>
    /// Returns the parent module builder.
    /// </summary>
    public abstract ModuleBuilder ModuleBuilder { get; }

    /// <summary>
    /// Returns the current generation.
    /// </summary>
    public Generation Generation => generation;

    /// <summary>
    /// Returns the current location.
    /// </summary>
    public Location Location => location;

    /// <summary>
    /// Returns the parent scope.
    /// </summary>
    public abstract Method Method { get; }

    /// <summary>
    /// Represents a undefined value in this generation.
    /// </summary>
    public UndefinedValue UndefinedValue => ModuleBuilder.UndefinedValue;

    #endregion

    #region Methods

    /// <summary>
    /// Is invoked when a new value is about to be created.
    /// </summary>
    protected virtual void OnBeforePureValueCreated(in PureValueInitializer initializer)
    { }

    /// <summary>
    /// Creates a new initializer that is bound to the current block.
    /// </summary>
    /// <returns>The created value initializer.</returns>
    private PureValueInitializer GetInitializer(Location location)
    {
        var initializer = new PureValueInitializer(this, location, Method);
        OnBeforePureValueCreated(initializer);
        return initializer;
    }

    /// <summary>
    /// Formats an error message to include specific exception information.
    /// </summary>
    /// <param name="message">The source error message.</param>
    /// <returns>The formatted error message.</returns>
    public virtual string FormatErrorMessage(string message) =>
        Location.FormatErrorMessage(message);

    /// <summary>
    /// Creates a node that represents an <see cref="Accelerator.CurrentType"/>
    /// property.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>A reference to the requested value.</returns>
    public PureValue CreateAcceleratorTypeValue(Location location) =>
        Append(new AcceleratorTypeValue(GetInitializer(location)));

    /// <summary>
    /// Creates a node that represents an <see cref="XMath.FastMath"/>
    /// property.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>A reference to the requested value.</returns>
    public PureValue CreateFastMathValue(Location location) =>
        Append(new FastMathValue(GetInitializer(location)));

    /// <summary>
    /// Creates a node that represents an <see cref="XMath.FlushToZero"/>
    /// property.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>A reference to the requested value.</returns>
    public PureValue CreateFlushToZeroValue(Location location) =>
        Append(new FlushToZeroValue(GetInitializer(location)));

    /// <summary>
    /// Creates a node that represents an <see cref="AcceleratorArchitecture.Current"/>
    /// property.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>A reference to the requested value.</returns>
    public PureValue CreateAcceleratorArchitectureValue(Location location) =>
        Append(new AcceleratorArchitectureValue(GetInitializer(location)));

    /// <summary>
    /// Creates a node that represents a <see cref="Grid.Index"/> property.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>A reference to the requested value.</returns>
    public PureValue CreateGridIndexValue(Location location) =>
        Append(new GridIndexValue(GetInitializer(location)));

    /// <summary>
    /// Creates a node that represents a <see cref="Group.Index"/> property.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>A reference to the requested value.</returns>
    public PureValue CreateGroupIndexValue(Location location) =>
        Append(new GroupIndexValue(GetInitializer(location)));

    /// <summary>
    /// Creates a node that represents a <see cref="Warp.Index"/> property.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>A reference to the requested value.</returns>
    public PureValue CreateSubGroupIndexValue(Location location) =>
        Append(new SubGroupIndexValue(GetInitializer(location)));

    /// <summary>
    /// Creates a node that represents a <see cref="Warp.LaneIndex"/> property.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>A reference to the requested value.</returns>
    public PureValue CreateSubGroupLaneIndexValue(Location location) =>
        Append(new SubGroupLaneIndexValue(GetInitializer(location)));

    /// <summary>
    /// Creates a node that represents a <see cref="Grid.Dimension"/> property.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>A reference to the requested value.</returns>
    public PureValue CreateGridDimensionValue(Location location) =>
        Append(new GridDimensionValue(GetInitializer(location)));

    /// <summary>
    /// Creates a node that represents of a <see cref="Group.Dimension"/> property.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>A reference to the requested value.</returns>
    public PureValue CreateGroupDimensionValue(Location location) =>
        Append(new GroupDimensionValue(GetInitializer(location)));

    /// <summary>
    /// Creates a node that represents of a <see cref="Warp.Dimension"/> property.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>A reference to the requested value.</returns>
    public PureValue CreateSubGroupDimensionValue(Location location) =>
        Append(new SubGroupDimensionValue(GetInitializer(location)));

    /// <summary>
    /// Creates a node that represents the native size of the given type.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="type">The type.</param>
    /// <returns>A reference to the requested value.</returns>
    public PrimitiveValue CreateSizeOf(Location location, TypeValue type)
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
    public Value CreateLongSizeOf(Location location, TypeValue type) =>
        CreateConvertToInt64(
            location,
            CreateSizeOf(location, type)).AsNotNull();

    /// <summary>
    /// Creates a node that represents the native offset of the specified field
    /// index.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="type">The type.</param>
    /// <param name="fieldIndex">The field index.</param>
    /// <returns>A reference to the requested value.</returns>
    public PrimitiveValue CreateOffsetOf(
        Location location,
        TypeValue type,
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
    /// Creates a node that represents a managed runtime handle.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="handle">The runtime handle.</param>
    /// <returns>A reference to the requested value.</returns>
    public PureValue CreateRuntimeHandle(Location location, object? handle) =>
        handle is null
        ? throw location.GetArgumentNullException(nameof(handle))
        : Append(new HandleValue(GetInitializer(location), handle));

    /// <summary>
    /// Creates a new index structure instance.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="dimension">The dimension value.</param>
    /// <returns>The created index type.</returns>
    public Value CreateIndex(Location location, Value dimension)
    {
        var instance = CreateDynamicStructure(location, 1);
        instance.Add(dimension);
        return instance.Seal();
    }

    /// <summary>
    /// Append a new value.
    /// </summary>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="node">The node to create.</param>
    /// <returns>The created node.</returns>
    private T Append<T>(T node) where T : PureValue
    {
        // This function acts as a debuggable wrapper to intercept node creation
#if DEBUG
        foreach (var childNode in node.Values)
            node.Assert(childNode != node);

#endif
        node.Assert(Generation == node.Generation);

        return node;
    }

    #endregion
}

// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: ValueKind.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.IR
{
    /// <summary>
    /// Represents the kind of a single IR value.
    /// </summary>
    public enum ValueKind : int
    {
        // General nodes

        /// <summary>
        /// An <see cref="Values.UnaryArithmeticValue"/> value.
        /// </summary>
        UnaryArithmetic,

        /// <summary>
        /// A <see cref="Values.BinaryArithmeticValue"/> value.
        /// </summary>
        BinaryArithmetic,

        /// <summary>
        /// A <see cref="Values.TernaryArithmeticValue"/> value.
        /// </summary>
        TernaryArithmetic,

        /// <summary>
        /// A <see cref="Values.CompareValue"/> value.
        /// </summary>
        Compare,

        /// <summary>
        /// A <see cref="Values.ConvertValue"/> value.
        /// </summary>
        Convert,

        /// <summary>
        /// A <see cref="Values.Predicate"/> value.
        /// </summary>
        Predicate,

        // Casts

        /// <summary>
        /// A <see cref="Values.PointerCast"/> value.
        /// </summary>
        PointerCast,

        /// <summary>
        /// A <see cref="Values.AddressSpaceCast"/> value.
        /// </summary>
        AddressSpaceCast,

        /// <summary>
        /// A <see cref="Values.ViewCast"/> value.
        /// </summary>
        ViewCast,

        /// <summary>
        /// A <see cref="Values.FloatAsIntCast"/> value.
        /// </summary>
        FloatAsIntCast,

        /// <summary>
        /// A <see cref="Values.IntAsFloatCast"/> value.
        /// </summary>
        IntAsFloatCast,

        // Constants

        /// <summary>
        /// A <see cref="Values.NullValue"/> value.
        /// </summary>
        Null,

        /// <summary>
        /// A <see cref="Values.PrimitiveValue"/> value.
        /// </summary>
        Primitive,

        /// <summary>
        /// A <see cref="Values.StringValue"/> value.
        /// </summary>
        String,

        /// <summary>
        /// A <see cref="Values.SizeOfValue"/> value.
        /// </summary>
        SizeOf,

        // Device Constants

        /// <summary>
        /// A <see cref="Values.GridIndexValue"/> value.
        /// </summary>
        GridIndex,

        /// <summary>
        /// A <see cref="Values.GroupIndexValue"/> value.
        /// </summary>
        GroupIndex,

        /// <summary>
        /// A <see cref="Values.GridDimensionValue"/> value.
        /// </summary>
        GridDimension,

        /// <summary>
        /// A <see cref="Values.GroupDimensionValue"/> value.
        /// </summary>
        GroupDimension,

        /// <summary>
        /// A <see cref="Values.WarpSizeValue"/> value.
        /// </summary>
        WarpSize,

        /// <summary>
        /// A <see cref="Values.LaneIdxValue"/> value.
        /// </summary>
        LaneIdx,

        // Memory

        /// <summary>
        /// An <see cref="Values.Alloca"/> value.
        /// </summary>
        Alloca,

        /// <summary>
        /// A <see cref="Values.MemoryBarrier"/> value.
        /// </summary>
        MemoryBarrier,

        /// <summary>
        /// A <see cref="Values.Load"/> value.
        /// </summary>
        Load,

        /// <summary>
        /// A <see cref="Values.Store"/> value.
        /// </summary>
        Store,

        /// <summary>
        /// A <see cref="Values.PhiValue"/> value.
        /// </summary>
        Phi,

        // Functions

        /// <summary>
        /// A <see cref="Values.Parameter"/> value.
        /// </summary>
        Parameter,

        /// <summary>
        /// A <see cref="Values.MethodCall"/> value.
        /// </summary>
        MethodCall,

        // Structures

        /// <summary>
        /// A <see cref="Values.GetField"/> value.
        /// </summary>
        GetField,

        /// <summary>
        /// A <see cref="Values.SetField"/> value.
        /// </summary>
        SetField,

        // Arrays

        /// <summary>
        /// A <see cref="Values.GetElement"/> value.
        /// </summary>
        GetElement,

        /// <summary>
        /// A <see cref="Values.SetElement"/> value.
        /// </summary>
        SetElement,

        // Views

        /// <summary>
        /// A <see cref="Values.NewView"/> value.
        /// </summary>
        NewView,

        /// <summary>
        /// A <see cref="Values.GetViewLength"/> value.
        /// </summary>
        GetViewLength,

        /// <summary>
        /// A <see cref="Values.SubViewValue"/> value.
        /// </summary>
        SubView,

        /// <summary>
        /// A <see cref="Values.LoadElementAddress"/> value.
        /// </summary>
        LoadElementAddress,

        /// <summary>
        /// A <see cref="Values.LoadFieldAddress"/> value.
        /// </summary>
        LoadFieldAddress,

        // Terminators

        /// <summary>
        /// A <see cref="Values.ReturnTerminator"/> terminator value.
        /// </summary>
        Return,

        /// <summary>
        /// An <see cref="Values.UnconditionalBranch"/> terminator value.
        /// </summary>
        UnconditionalBranch,

        /// <summary>
        /// A <see cref="Values.ConditionalBranch"/> terminator value.
        /// </summary>
        ConditionalBranch,

        /// <summary>
        /// A <see cref="Values.SwitchBranch"/> terminator value.
        /// </summary>
        SwitchBranch,

        // Atomic

        /// <summary>
        /// A <see cref="Values.GenericAtomic"/> value.
        /// </summary>
        GenericAtomic,

        /// <summary>
        /// A <see cref="Values.AtomicCAS"/> value.
        /// </summary>
        AtomicCAS,

        // Threads

        /// <summary>
        /// A <see cref="Values.PredicateBarrier"/> value.
        /// </summary>
        PredicateBarrier,

        /// <summary>
        /// A <see cref="Values.Barrier"/> value.
        /// </summary>
        Barrier,

        /// <summary>
        /// A <see cref="Values.Broadcast"/> value.
        /// </summary>
        Broadcast,

        /// <summary>
        /// A <see cref="Values.WarpShuffle"/> value.
        /// </summary>
        WarpShuffle,

        /// <summary>
        /// A <see cref="Values.SubWarpShuffle"/> value.
        /// </summary>
        SubWarpShuffle,

        // Debugging

        /// <summary>
        /// A <see cref="Values.DebugOperation"/> value.
        /// </summary>
        Debug,

        // Internal use

        /// <summary>
        /// An <see cref="Values.UndefinedValue"/> value.
        /// </summary>
        Undefined,

        /// <summary>
        /// A <see cref="Values.BuilderTerminator"/> terminator value.
        /// </summary>
        BuilderTerminator,

        /// <summary>
        /// A <see cref="Values.HandleValue"/> managed handle value.
        /// </summary>
        Handle,
    }

    /// <summary>
    /// Utility methods for <see cref="ValueKind"/> enumeration values.
    /// </summary>
    public static class ValueKinds
    {
        /// <summary>
        /// The number of different value kinds.
        /// </summary>
        public const int NumValueKinds = (int)ValueKind.Handle + 1;
    }
}

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueKind.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Serialization;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Reflection;

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
        /// A <see cref="Values.ArrayToViewCast"/> value.
        /// </summary>
        ArrayToViewCast,

        /// <summary>
        /// A <see cref="Values.IntAsPointerCast"/> value.
        /// </summary>
        IntAsPointerCast,

        /// <summary>
        /// A <see cref="Values.PointerAsIntCast"/> value.
        /// </summary>
        PointerAsIntCast,

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

        // Device Constants

        /// <summary>
        /// A <see cref="Values.AcceleratorTypeValue"/> value.
        /// </summary>
        AcceleratorType,

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

        /// <summary>
        /// A <see cref="Values.DynamicMemoryLengthValue"/> value.
        /// </summary>
        DynamicMemoryLength,

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
        /// A <see cref="Values.StructureValue"/> value.
        /// </summary>
        Structure,

        /// <summary>
        /// A <see cref="Values.GetField"/> value.
        /// </summary>
        GetField,

        /// <summary>
        /// A <see cref="Values.SetField"/> value.
        /// </summary>
        SetField,

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
        /// A <see cref="Values.AlignTo"/> value.
        /// </summary>
        AlignTo,

        /// <summary>
        /// A <see cref="Values.AsAligned"/> value.
        /// </summary>
        AsAligned,

        /// <summary>
        /// A <see cref="Values.SubViewValue"/> value.
        /// </summary>
        SubView,

        // Arrays

        /// <summary>
        /// A <see cref="Values.NewArray"/> value.
        /// </summary>
        Array,

        /// <summary>
        /// A <see cref="Values.GetArrayLength"/> value.
        /// </summary>
        GetArrayLength,

        /// <summary>
        /// A <see cref="Values.LoadElementAddress"/> value.
        /// </summary>
        LoadElementAddress,

        /// <summary>
        /// A <see cref="Values.LoadArrayElementAddress"/> value.
        /// </summary>
        LoadArrayElementAddress,

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
        /// A <see cref="Values.IfBranch"/> terminator value.
        /// </summary>
        IfBranch,

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
        /// A <see cref="Values.DebugAssertOperation"/> value.
        /// </summary>
        DebugAssert,

        // IO

        /// <summary>
        /// A <see cref="Values.WriteToOutput"/> value.
        /// </summary>
        WriteToOutput,

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

        // Language

        /// <summary>
        /// A <see cref="Values.LanguageEmitValue"/> value.
        /// </summary>
        LanguageEmit,

        /// <summary>
        /// Placeholder for the last value kind.
        /// </summary>
        MaxValue
    }

    /// <summary>
    /// Marks value classes with specific value kinds.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class ValueKindAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new value kind attribute.
        /// </summary>
        /// <param name="kind">The value kind.</param>
        public ValueKindAttribute(ValueKind kind)
        {
            Kind = kind;
        }

        /// <summary>
        /// Returns the value kind.
        /// </summary>
        public ValueKind Kind { get; }
    }

    /// <summary>
    /// Utility methods for <see cref="ValueKind"/> enumeration values.
    /// </summary>
    public static partial class ValueKinds
    {
        private readonly static Dictionary<ValueKind,
            GenericValueReader> _readerDelegates = new();

        /// <summary>
        /// Returns a table mapping <see cref="ValueKind"/> information
        /// to their corresponding <see cref="GenericValueReader"/> delegates.
        /// </summary>
        public static IReadOnlyDictionary<ValueKind,
            GenericValueReader> ReaderDelegates =>
            _readerDelegates;

        /// <summary>
        /// The number of different value kinds.
        /// </summary>
        public const int NumValueKinds = (int)ValueKind.MaxValue;

        /// <summary>
        /// Gets the value kind of the value type specified.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns>The determined value kind.</returns>
        public static ValueKind GetValueKind<TValue>()
            where TValue : Value =>
            typeof(TValue).GetCustomAttribute<ValueKindAttribute>().AsNotNull().Kind;

        /// <summary>
        /// Gets the value kind of the type specified.
        /// </summary>
        /// <returns>The determined value kind.</returns>
        public static ValueKind? GetValueKind(Type type) =>
            type.GetCustomAttribute<ValueKindAttribute>()?.Kind;
    }
}

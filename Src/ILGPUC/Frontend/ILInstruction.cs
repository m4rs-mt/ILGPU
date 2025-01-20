// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILInstruction.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.Frontend.DebugInformation;
using ILGPUC.IR;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPUC.Frontend;

/// <summary>
/// Represents a collection of branch targets.
/// </summary>
/// <param name="targets">All branch targets.</param>
sealed class ILInstructionBranchTargets(params int[] targets)
{
    #region Instance

    private readonly ReadOnlyMemory<int> _targetOffsets = targets;

    #endregion

    #region Properties

    /// <summary>
    /// Returns the number of targets
    /// </summary>
    public int Count => _targetOffsets.Length;

    /// <summary>
    /// Returns the target offset at the given index.
    /// </summary>
    /// <param name="index">The index of the target offset.</param>
    /// <returns>The resolved target offset.</returns>
    public int this[int index] => _targetOffsets.Span[index];

    /// <summary>
    /// Returns the unconditional branch target (if any).
    /// </summary>
    public int? UnconditionalBranchTarget =>
        _targetOffsets.Length > 0 ? _targetOffsets.Span[0] : null;

    /// <summary>
    /// Returns the conditional branch if-target (if any).
    /// </summary>
    public int? ConditionalBranchIfTarget => UnconditionalBranchTarget;

    /// <summary>
    /// Returns the conditional branch else-target (if any).
    /// </summary>
    public int? ConditionalBranchElseTarget =>
        _targetOffsets.Length > 1 ? _targetOffsets.Span[1] : null;

    /// <summary>
    /// Returns the default switch branch target (if any).
    /// </summary>
    public int? SwitchDefaultTarget => UnconditionalBranchTarget;

    #endregion

    #region Methods

    /// <summary>
    /// Returns the branch offsets.
    /// </summary>
    /// <returns>The branch offsets.</returns>
    public ReadOnlySpan<int> GetTargetOffsets() => _targetOffsets.Span;

    #endregion

    #region Object

    /// <summary>
    /// Returns the string representation of the branch targets.
    /// </summary>
    /// <returns>The string representation of the branch targets.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        for (int i = 0, e = _targetOffsets.Length; i < e; ++i)
        {
            builder.Append(_targetOffsets.Span[i].ToString("X4"));
            if (i + 1 < e)
                builder.Append(", ");
        }
        return builder.ToString();
    }

    #endregion
}

/// <summary>
/// Contains extension methods for instruction flags.
/// </summary>
static class ILInstructionFlagsExtensions
{
    /// <summary>
    /// Returns true if given flags have the other flags set;
    /// </summary>
    /// <param name="flags">The current flags.</param>
    /// <param name="otherFlags">The flags to check.</param>
    /// <returns>True, if given flags have the other flags set.</returns>
    public static bool HasFlags(
        this ILInstructionFlags flags,
        ILInstructionFlags otherFlags) =>
        (flags & otherFlags) == otherFlags;
}

/// <summary>
/// Represents a context of instruction flags.
/// </summary>
/// <param name="Flags">The instruction flags.</param>
/// <param name="Argument">The flags argument.</param>
readonly record struct ILInstructionFlagsContext(
    ILInstructionFlags Flags,
    object? Argument) : IEquatable<ILInstructionFlagsContext>
{
    /// <summary>
    /// Returns the hash code of this flags.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => Flags.GetHashCode();

    /// <summary>
    /// Returns the string representation of this flags.
    /// </summary>
    /// <returns>The string representation.</returns>
    public override string ToString() =>
        Argument is null
        ? $"{Flags}"
        : $"{Flags} [{Argument}]";
}

/// <summary>
/// An abstract operation that can be invoked for any instruction offset.
/// </summary>
interface IILInstructionOffsetOperation
{
    /// <summary>
    /// Applies the current operation with the given instruction offset.
    /// </summary>
    /// <param name="instruction">The parent instruction.</param>
    /// <param name="offset">An instruction offset of the parent operation.</param>
    void Apply(ILInstruction instruction, int offset);
}

/// <summary>
/// Represents dependent type information derived from the element type at the top
/// of the evaluation stack.
/// </summary>
abstract class DependentTypeArguments
{
    /// <summary>
    /// Determines the target type using the element type from the evaluation stack.
    /// </summary>
    /// <param name="topOfStack">The element type on the evaluation stack.</param>
    /// <returns>The target type.</returns>
    public abstract Type DetermineType(BasicValueType topOfStack);
}

/// <summary>
/// Represents a single IL instruction.
/// </summary>
/// <param name="offset">The instruction offset in bytes.</param>
/// <param name="type">The instruction type.</param>
/// <param name="flagsContext">The flags context.</param>
/// <param name="argument">The instruction argument.</param>
sealed class ILInstruction(
    int offset,
    ILInstructionType type,
    ILInstructionFlagsContext flagsContext,
    object? argument) : IEquatable<ILInstruction>
{
    #region Nested Types

    /// <summary>
    /// Represents an implement automatic dependent type conversion argument
    /// remapping using a dictionary.
    /// </summary>
    internal sealed class ConvertTypeArguments : DependentTypeArguments
    {
        private readonly Dictionary<BasicValueType, Type> mapping;

        /// <summary>
        /// Creates a new instance of type conversion arguments.
        /// </summary>
        /// <param name="typeMapping">All type mappings.</param>
        public ConvertTypeArguments(
            params ValueTuple<BasicValueType, Type>[] typeMapping)
        {
            mapping = new Dictionary<BasicValueType, Type>(typeMapping.Length);
            foreach (var (source, target) in typeMapping)
                mapping.Add(source, target);
        }

        /// <summary>
        /// Determines the target type using an internal dictionary mapping.
        /// </summary>
        /// <param name="topOfStack">The element type on the evaluation stack.</param>
        /// <returns>The target type.</returns>
        public override Type DetermineType(BasicValueType topOfStack) =>
            mapping[topOfStack];
    }

    #endregion

    #region Static

    /// <summary>
    /// Represents unsigned dependent type conversion arguments.
    /// </summary>
    public static readonly ConvertTypeArguments ConvRUnArguments = new(
        (BasicValueType.Int1, typeof(float)),
        (BasicValueType.Int8, typeof(float)),
        (BasicValueType.Int16, typeof(float)),
        (BasicValueType.Int32, typeof(float)),
        (BasicValueType.Int64, typeof(double)));

    #endregion

    #region Properties

    /// <summary>
    /// Returns the instruction offset in bytes.
    /// </summary>
    public int Offset { get; } = offset;

    /// <summary>
    /// Returns the instruction type.
    /// </summary>
    public ILInstructionType InstructionType { get; } = type;

    /// <summary>
    /// Returns the instruction flags.
    /// </summary>
    public ILInstructionFlags Flags => FlagsContext.Flags;

    /// <summary>
    /// Returns the instruction-flags context.
    /// </summary>
    public ILInstructionFlagsContext FlagsContext { get; } = flagsContext;

    /// <summary>
    /// Returns the instruction argument.
    /// </summary>
    public object? Argument { get; } = argument;

    /// <summary>
    /// Returns true if the instruction is a call instruction.
    /// </summary>
    public bool IsCall
    {
        get => InstructionType switch
        {
            ILInstructionType.Call or
            ILInstructionType.Callvirt or
            ILInstructionType.Jmp or
            ILInstructionType.Newobj => true,
            _ => false,
        };
    }

    /// <summary>
    /// Returns true if this instruction is a basic block terminator.
    /// </summary>
    public bool IsTerminator =>
        InstructionType == ILInstructionType.Jmp ||
        InstructionType == ILInstructionType.Ret ||
        Argument is ILInstructionBranchTargets;

    /// <summary>
    /// Returns the associated location.
    /// </summary>
    public Location Location { get; private set; } = Location.Unknown;

    #endregion

    #region Methods

    /// <summary>
    /// Returns the instruction argument as T.
    /// </summary>
    /// <typeparam name="T">The target type T.</typeparam>
    /// <returns>The instruction argument T.</returns>
    public T GetArgumentAs<T>() => (T)Argument.AsNotNull();

    /// <summary>
    /// Returns true if current instruction has the given flags.
    /// </summary>
    /// <param name="flags">The flags to check.</param>
    /// <returns>True, if current instruction has the given flags.</returns>
    public bool HasFlags(ILInstructionFlags flags) => (Flags & flags) == flags;

    /// <summary>
    /// Performs the given operation for each instruction offset.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEachOffset<TOperation>(TOperation operation)
        where TOperation : struct, IILInstructionOffsetOperation
    {
        int offset = Offset;

        // Early exit for no flags (the most common case)
        operation.Apply(this, offset);
        if (Flags == ILInstructionFlags.None)
            return;

        // Compute detailed offset information for all flags
        for (
            int i = (int)ILInstructionFlags.Unchecked;
            i < (int)ILInstructionFlags.Constrained;
            i <<= 1)
        {
            if (Flags.HasFlags((ILInstructionFlags)i))
            {
                // Subtract two bytes (OpCode)
                offset -= 2;
                operation.Apply(this, offset);
            }
        }
        if (Flags.HasFlags(ILInstructionFlags.Constrained))
        {
            // Subtract two bytes (OpCode) + the size of a metadata token
            operation.Apply(this, offset - 2 - sizeof(int));
        }
    }

    /// <summary>
    /// A location offset operation to update internal instruction location.
    /// </summary>
    /// <param name="enumerator">The sequence point enumerator to be used.</param>
    readonly struct LocationOffsetOperation(SequencePointEnumerator enumerator) :
        IILInstructionOffsetOperation
    {
        /// <summary>
        /// Tries to reconstruct instruction location.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Apply(ILInstruction instruction, int offset)
        {
            enumerator.MoveTo(offset);
            var current = enumerator.Current;
            instruction.Location = Location.Merge(instruction.Location, current);
        }
    }

    /// <summary>
    /// Updates the internal location using the given sequence point enumerator.
    /// </summary>
    /// <param name="enumerator">The current sequence point enumerator.</param>
    public void UpdateLocation(SequencePointEnumerator enumerator) =>
        ForEachOffset(new LocationOffsetOperation(enumerator));

    #endregion

    #region IEquatable

    /// <summary>
    /// Returns true if the current object is equal to the given one.
    /// </summary>
    /// <param name="other">The other object.</param>
    /// <returns>True, if the current object is equal to the given one.</returns>
    [SuppressMessage(
        "Style",
        "IDE0046:Convert to conditional expression",
        Justification = "Avoid nested if conditionals")]
    public bool Equals(ILInstruction? other)
    {
        if (other is null)
            return false;
        if (InstructionType != other.InstructionType ||
            FlagsContext != other.FlagsContext)
            return false;

        return Equals(Argument, other.Argument);
    }

    #endregion

    #region Object

    /// <summary>
    /// Returns true if the current object is equal to the given one.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns>True, if the current object is equal to the given one.</returns>
    public override bool Equals(object? obj) =>
        obj is ILInstruction otherObj && otherObj == this;

    /// <summary>
    /// Returns the hash code of this instruction.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => InstructionType.GetHashCode();

    /// <summary>
    /// Returns the string representation of this instruction.
    /// </summary>
    /// <returns>The string representation.</returns>
    public override string ToString()
    {
        string baseArg =
            Argument == null
            ? $"{Offset:X4}: {InstructionType}"
            : $"{Offset:X4}: {InstructionType} [{Argument}]";
        return Flags != ILInstructionFlags.None
            ? $"{FlagsContext}.{baseArg}"
            : baseArg;
    }

    #endregion
}

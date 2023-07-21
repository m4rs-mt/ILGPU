// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILInstruction.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.Frontend
{
    /// <summary>
    /// Represents an instruction type of a single IL instruction.
    /// </summary>
    public enum ILInstructionType
    {
        ///
        /// <summary>Nop</summary>
        ///
        Nop,

        ///
        /// <summary>Break</summary>
        ///
        Break,

        ///
        /// <summary>Ldarg</summary>
        ///
        Ldarg,
        ///
        /// <summary>Ldarga</summary>
        ///
        Ldarga,
        ///
        /// <summary>Starg</summary>
        ///
        Starg,

        ///
        /// <summary>Ldloc</summary>
        ///
        Ldloc,
        ///
        /// <summary>Ldloca</summary>
        ///
        Ldloca,
        ///
        /// <summary>Stloc</summary>
        ///
        Stloc,

        ///
        /// <summary>Ldnull</summary>
        ///
        Ldnull,
        ///
        /// <summary>LdI4</summary>
        ///
        LdI4,
        ///
        /// <summary>LdI8</summary>
        ///
        LdI8,
        ///
        /// <summary>LdR4</summary>
        ///
        LdR4,
        ///
        /// <summary>LdR8</summary>
        ///
        LdR8,
        ///
        /// <summary>Ldstr</summary>
        ///
        Ldstr,

        ///
        /// <summary>Dup</summary>
        ///
        Dup,
        ///
        /// <summary>Pop</summary>
        ///
        Pop,

        ///
        /// <summary>Jmp</summary>
        ///
        Jmp,
        ///
        /// <summary>Call</summary>
        ///
        Call,
        ///
        /// <summary>Calli</summary>
        ///
        Calli,
        ///
        /// <summary>Callvirt</summary>
        ///
        Callvirt,

        ///
        /// <summary>Ret</summary>
        ///
        Ret,

        ///
        /// <summary>Br</summary>
        ///
        Br,
        ///
        /// <summary>Brfalse</summary>
        ///
        Brfalse,
        ///
        /// <summary>Brtrue</summary>
        ///
        Brtrue,

        ///
        /// <summary></summary>
        ///
        Beq,
        ///
        /// <summary>Bne</summary>
        ///
        Bne,
        ///
        /// <summary>Bge</summary>
        ///
        Bge,
        ///
        /// <summary>Bgt</summary>
        ///
        Bgt,
        ///
        /// <summary>Ble</summary>
        ///
        Ble,
        ///
        /// <summary>Blt</summary>
        ///
        Blt,

        ///
        /// <summary>Switch</summary>
        ///
        Switch,

        ///
        /// <summary>Add</summary>
        ///
        Add,
        ///
        /// <summary>Sub</summary>
        ///
        Sub,
        ///
        /// <summary>Mul</summary>
        ///
        Mul,
        ///
        /// <summary>Div</summary>
        ///
        Div,
        ///
        /// <summary>Rem</summary>
        ///
        Rem,

        ///
        /// <summary>And</summary>
        ///
        And,
        ///
        /// <summary>Or</summary>
        ///
        Or,
        ///
        /// <summary>Xor</summary>
        ///
        Xor,
        ///
        /// <summary>Shl</summary>
        ///
        Shl,
        ///
        /// <summary>Shr</summary>
        ///
        Shr,
        ///
        /// <summary>Neg</summary>
        ///
        Neg,
        ///
        /// <summary>Not</summary>
        ///
        Not,

        ///
        /// <summary>Conv</summary>
        ///
        Conv,

        ///
        /// <summary>Initobj</summary>
        ///
        Initobj,
        ///
        /// <summary>Newobj</summary>
        ///
        Newobj,
        ///
        /// <summary>Newarr</summary>
        ///
        Newarr,

        ///
        /// <summary>Castclass</summary>
        ///
        Castclass,
        ///
        /// <summary>Isinst</summary>
        ///
        Isinst,

        ///
        /// <summary>Box</summary>
        ///
        Box,
        ///
        /// <summary>Unbox</summary>
        ///
        Unbox,

        ///
        /// <summary>Ldfld</summary>
        ///
        Ldfld,
        ///
        /// <summary>Ldflda</summary>
        ///
        Ldflda,
        ///
        /// <summary>Stfld</summary>
        ///
        Stfld,
        ///
        /// <summary>Ldsfld</summary>
        ///
        Ldsfld,
        ///
        /// <summary>Ldsflda</summary>
        ///
        Ldsflda,
        ///
        /// <summary>Stsfld</summary>
        ///
        Stsfld,

        ///
        /// <summary>Ldobj</summary>
        ///
        Ldobj,
        ///
        /// <summary>Stobj</summary>
        ///
        Stobj,
        ///
        /// <summary>Cpobj</summary>
        ///
        Cpobj,

        ///
        /// <summary>Ldlen</summary>
        ///
        Ldlen,
        ///
        /// <summary>Ldelem</summary>
        ///
        Ldelem,
        ///
        /// <summary>Ldelema</summary>
        ///
        Ldelema,
        ///
        /// <summary>Stelem</summary>
        ///
        Stelem,

        ///
        /// <summary>Ceq</summary>
        ///
        Ceq,
        ///
        /// <summary>Cgt</summary>
        ///
        Cgt,
        ///
        /// <summary>Clt</summary>
        ///
        Clt,

        ///
        /// <summary>Ldind</summary>
        ///
        Ldind,
        ///
        /// <summary>Stind</summary>
        ///
        Stind,
        ///
        /// <summary>Localloc</summary>
        ///
        Localloc,

        ///
        /// <summary>Cpblk</summary>
        ///
        Cpblk,
        ///
        /// <summary>Initblk</summary>
        ///
        Initblk,

        ///
        /// <summary>SizeOf</summary>
        ///
        SizeOf,
        ///
        /// <summary>LoadToken</summary>
        ///
        LdToken,
    }

    /// <summary>
    /// Represents a collection of branch targets.
    /// </summary>
    public sealed class ILInstructionBranchTargets
    {
        #region Instance

        private readonly int[] targetOffsets;

        /// <summary>
        /// Constructs a new container for branch targets.
        /// </summary>
        /// <param name="targets"></param>
        public ILInstructionBranchTargets(params int[] targets)
        {
            targetOffsets = targets;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of targets
        /// </summary>
        public int Count => targetOffsets.Length;

        /// <summary>
        /// Returns the target offset at the given index.
        /// </summary>
        /// <param name="index">The index of the target offset.</param>
        /// <returns>The resolved target offset.</returns>
        public int this[int index] => targetOffsets[index];

        /// <summary>
        /// Returns the unconditional branch target (if any).
        /// </summary>
        public int? UnconditionalBranchTarget =>
            targetOffsets.Length > 0 ? (int?)targetOffsets[0] : null;

        /// <summary>
        /// Returns the conditional branch if-target (if any).
        /// </summary>
        public int? ConditionalBranchIfTarget => UnconditionalBranchTarget;

        /// <summary>
        /// Returns the conditional branch else-target (if any).
        /// </summary>
        public int? ConditionalBranchElseTarget =>
            targetOffsets.Length > 1 ? (int?)targetOffsets[1] : null;

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
        public int[] GetTargetOffsets() => targetOffsets;

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of the branch targets.
        /// </summary>
        /// <returns>The string representation of the branch targets.</returns>
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0, e = targetOffsets.Length; i < e; ++i)
            {
                builder.Append(targetOffsets[i].ToString("X4"));
                if (i + 1 < e)
                    builder.Append(", ");
            }
            return builder.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Represent flags of an IL instruction.
    /// </summary>
    [Flags]
    public enum ILInstructionFlags : int
    {
        /// <summary>
        /// None.
        /// </summary>
        None = 0,

        /// <summary>
        /// Unsigned operation.
        /// </summary>
        Unsigned = 1 << 0,

        /// <summary>
        /// Overflow check requested.
        /// </summary>
        Overflow = 1 << 1,

        /// <summary>
        /// Unchecked operation.
        /// </summary>
        Unchecked = 1 << 2,

        /// <summary>
        /// Unaligned operation.
        /// </summary>
        Unaligned = 1 << 3,

        /// <summary>
        /// Volatile access.
        /// </summary>
        Volatile = 1 << 4,

        /// <summary>
        /// ReadOnly access.
        /// </summary>
        ReadOnly = 1 << 5,

        /// <summary>
        /// Tail call.
        /// </summary>
        Tail = 1 << 6,

        /// <summary>
        /// Constraint virtual-function access.
        /// </summary>
        Constrained = 1 << 7,
    }

    /// <summary>
    /// Contains extension methods for instruction flags.
    /// </summary>
    public static class ILInstructionFlagsExtensions
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
    public struct ILInstructionFlagsContext : IEquatable<ILInstructionFlagsContext>
    {
        #region Instance

        /// <summary>
        /// Constructs a new instruction-flag context.
        /// </summary>
        /// <param name="flags">The instruction flags.</param>
        /// <param name="argument">The flags argument.</param>
        public ILInstructionFlagsContext(
            ILInstructionFlags flags,
            object? argument)
        {
            Flags = flags;
            Argument = argument;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the flags.
        /// </summary>
        public ILInstructionFlags Flags { get; }

        /// <summary>
        /// Returns the flag argument.
        /// </summary>
        public object? Argument { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the current object is equal to the given one.
        /// </summary>
        /// <param name="other">The other object.</param>
        /// <returns>True, if the current object is equal to the given one.</returns>
        public bool Equals(ILInstructionFlagsContext other) =>
            other == this;

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the current object is equal to the given one.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the current object is equal to the given one.</returns>
        public override bool Equals(object? obj) =>
            obj is ILInstructionFlagsContext context && context == this;

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
            Argument == null
            ? $"{Flags}"
            : $"{Flags} [{Argument}]";

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the first instruction context is equal to the second one.
        /// </summary>
        /// <param name="first">The first instruction context.</param>
        /// <param name="second">The second instruction context.</param>
        /// <returns>
        /// True, if the first instruction is equal to the second one.
        /// </returns>
        [SuppressMessage(
            "Style",
            "IDE0046:Convert to conditional expression",
            Justification = "Avoid nested if conditionals")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(
            ILInstructionFlagsContext first,
            ILInstructionFlagsContext second)
        {
            if (first.Flags != second.Flags)
                return false;
            return Equals(first.Argument, second.Argument);
        }

        /// <summary>
        /// Returns true if the first instruction context is not equal to the second
        /// one.
        /// </summary>
        /// <param name="first">The first instruction context.</param>
        /// <param name="second">The second instruction context.</param>
        /// <returns>
        /// True, if the first instruction is not equal to the second one.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(
            ILInstructionFlagsContext first,
            ILInstructionFlagsContext second) =>
            !(first == second);

        #endregion
    }

    /// <summary>
    /// An abstract operation that can be invoked for any instruction offset.
    /// </summary>
    public interface IILInstructionOffsetOperation
    {
        /// <summary>
        /// Applies the current operation with the given instruction offset.
        /// </summary>
        /// <param name="instruction">The parent instruction.</param>
        /// <param name="offset">An instruction offset of the parent operation.</param>
        void Apply(ILInstruction instruction, int offset);
    }

    /// <summary>
    /// Represents a single IL instruction.
    /// </summary>
    public sealed class ILInstruction : IEquatable<ILInstruction>
    {
        #region Instance

        /// <summary>
        /// Constructs a new IL instruction.
        /// </summary>
        /// <param name="offset">The instruction offset in bytes.</param>
        /// <param name="type">The instruction type.</param>
        /// <param name="flagsContext">The flags context.</param>
        /// <param name="popCount">The number of elements to pop from the stack.</param>
        /// <param name="pushCount">
        /// The number of elements to push onto the stack.
        /// </param>
        /// <param name="argument">The instruction argument.</param>
        /// <param name="location">The current location.</param>
        [CLSCompliant(false)]
        public ILInstruction(
            int offset,
            ILInstructionType type,
            ILInstructionFlagsContext flagsContext,
            ushort popCount,
            ushort pushCount,
            object? argument,
            Location location)
        {
            Offset = offset;
            InstructionType = type;
            FlagsContext = flagsContext;
            PopCount = popCount;
            PushCount = pushCount;
            Argument = argument;
            Location = location ?? Location.Unknown;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the instruction offset in bytes.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// Returns the instruction type.
        /// </summary>
        public ILInstructionType InstructionType { get; }

        /// <summary>
        /// Returns the instruction flags.
        /// </summary>
        public ILInstructionFlags Flags => FlagsContext.Flags;

        /// <summary>
        /// Returns the instruction-flags context.
        /// </summary>
        public ILInstructionFlagsContext FlagsContext { get; }

        /// <summary>
        /// Returns the number of elements to pop from the stack.
        /// </summary>
        [CLSCompliant(false)]
        public ushort PopCount { get; }

        /// <summary>
        /// Returns the number of elements to push onto the stack.
        /// </summary>
        [CLSCompliant(false)]
        public ushort PushCount { get; }

        /// <summary>
        /// Returns the instruction argument.
        /// </summary>
        public object? Argument { get; }

        /// <summary>
        /// Returns true if the instruction is a call instruction.
        /// </summary>
        public bool IsCall
        {
            get
            {
                switch (InstructionType)
                {
                    case ILInstructionType.Call:
                    case ILInstructionType.Calli:
                    case ILInstructionType.Callvirt:
                    case ILInstructionType.Jmp:
                    case ILInstructionType.Newobj:
                        return true;
                    default:
                        return false;
                }
            }
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
        public Location Location { get; }

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
        public bool HasFlags(ILInstructionFlags flags) =>
            (Flags & flags) == flags;

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
            if (other == null)
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
}

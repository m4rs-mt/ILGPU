// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: ILInstruction.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler.DebugInformation;
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.Compiler
{
    /// <summary>
    /// Represents an instruction type of a single il instruction.
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
    }

    /// <summary>
    /// Represents a collection fo branch targets.
    /// </summary>
    public sealed class ILInstructionBranchTargets
    {
        #region Instance

        private int[] targetOffsets;

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
        public int? UnconditionalBranchTarget => targetOffsets.Length > 0 ? (int?)targetOffsets[0] : null;

        /// <summary>
        /// Returns the conditional branch if-target (if any).
        /// </summary>
        public int? ConditionalBranchIfTarget => UnconditionalBranchTarget;

        /// <summary>
        /// Returns the conditional branch else-target (if any).
        /// </summary>
        public int? ConditionalBranchElseTarget => targetOffsets.Length > 1 ? (int?)targetOffsets[1] : null;

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
        public int[] GetTargetOffsets()
        {
            return targetOffsets;
        }

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
    /// Represent flags of an il instruction.
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
            object argument)
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
        public object Argument { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the current object is equal to the given one.
        /// </summary>
        /// <param name="other">The other object.</param>
        /// <returns>True, if the current object is equal to the given one.</returns>
        public bool Equals(ILInstructionFlagsContext other)
        {
            return other == this;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the current object is equal to the given one.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the current object is equal to the given one.</returns>
        public override bool Equals(object obj)
        {
            if (obj is ILInstructionFlagsContext)
                return (ILInstructionFlagsContext)obj == this;
            return false;
        }

        /// <summary>
        /// Returns the hash code of this flags.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return Flags.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of this flags.
        /// </summary>
        /// <returns>The string representation.</returns>
        public override string ToString()
        {
            if (Argument == null)
                return $"{Flags}";
            return $"{Flags} [{Argument}]";
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first instruction context is equal to the second one.
        /// </summary>
        /// <param name="first">The first instruction context.</param>
        /// <param name="second">The second instruction context.</param>
        /// <returns>True, iff the first instruction is equal to the second one.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ILInstructionFlagsContext first, ILInstructionFlagsContext second)
        {
            if (first.Flags != second.Flags ||
                first.Argument == null && second.Argument != null ||
                first.Argument != null && second.Argument == null)
                return false;
            if (first.Argument == null && second.Argument == null)
                return true;
            return first.Argument.Equals(second.Argument);
        }

        /// <summary>
        /// Returns true iff the first instruction context is not equal to the second one.
        /// </summary>
        /// <param name="first">The first instruction context.</param>
        /// <param name="second">The second instruction context.</param>
        /// <returns>True, iff the first instruction is not equal to the second one.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ILInstructionFlagsContext first, ILInstructionFlagsContext second)
        {
            return !(first == second);
        }

        #endregion
    }

    /// <summary>
    /// Represents a single il instruction.
    /// </summary>
    public sealed class ILInstruction : IEquatable<ILInstruction>
    {
        #region Instance

        /// <summary>
        /// Constructs a new il instruction.
        /// </summary>
        /// <param name="offset">The instruction offset in bytes.</param>
        /// <param name="type">The instruction type.</param>
        /// <param name="flagsContext">The flags context.</param>
        /// <param name="popCount">The number of elements to pop from the stack.</param>
        /// <param name="pushCount">The number of elements to push onto the stack.</param>
        /// <param name="argument">The instruction argument.</param>
        [CLSCompliant(false)]
        public ILInstruction(
            int offset,
            ILInstructionType type,
            ILInstructionFlagsContext flagsContext,
            ushort popCount,
            ushort pushCount,
            object argument)
            : this(offset, type, flagsContext, popCount, pushCount, argument, null)
        { }

        /// <summary>
        /// Constructs a new il instruction.
        /// </summary>
        /// <param name="offset">The instruction offset in bytes.</param>
        /// <param name="type">The instruction type.</param>
        /// <param name="flagsContext">The flags context.</param>
        /// <param name="popCount">The number of elements to pop from the stack.</param>
        /// <param name="pushCount">The number of elements to push onto the stack.</param>
        /// <param name="argument">The instruction argument.</param>
        /// <param name="sequencePoint">The current sequence point.</param>
        [CLSCompliant(false)]
        public ILInstruction(
            int offset,
            ILInstructionType type,
            ILInstructionFlagsContext flagsContext,
            ushort popCount,
            ushort pushCount,
            object argument,
            SequencePoint? sequencePoint)
        {
            Offset = offset;
            InstructionType = type;
            FlagsContext = flagsContext;
            PopCount = popCount;
            PushCount = pushCount;
            Argument = argument;
            SequencePoint = sequencePoint;
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
        public object Argument { get; }

        /// <summary>
        /// Returns true iff this instruction is a basic block terminator.
        /// </summary>
        public bool IsTerminator =>
            InstructionType == ILInstructionType.Ret ||
            Argument is ILInstructionBranchTargets;

        /// <summary>
        /// Returns the associated sequence point.
        /// </summary>
        public SequencePoint? SequencePoint { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns the instruction argument as T.
        /// </summary>
        /// <typeparam name="T">The target type T.</typeparam>
        /// <returns>The instruction argument T.</returns>
        public T GetArgumentAs<T>()
        {
            return (T)Argument;
        }

        /// <summary>
        /// Returns true iff current instruction has the given flags.
        /// </summary>
        /// <param name="flags">The flags to check.</param>
        /// <returns>True, iff current instruction has the given flags.</returns>
        public bool HasFlags(ILInstructionFlags flags)
        {
            return (Flags & flags) == flags;
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the current object is equal to the given one.
        /// </summary>
        /// <param name="other">The other object.</param>
        /// <returns>True, if the current object is equal to the given one.</returns>
        public bool Equals(ILInstruction other)
        {
            if (other == null)
                return false;
            if (InstructionType != other.InstructionType ||
                FlagsContext != other.FlagsContext ||
                Argument == null && other.Argument != null ||
                Argument != null && other.Argument == null)
                return false;
            if (Argument == null && other.Argument == null)
                return true;
            return Argument.Equals(other.Argument);
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the current object is equal to the given one.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the current object is equal to the given one.</returns>
        public override bool Equals(object obj)
        {
            if (obj is ILInstruction otherObj)
                return otherObj.Equals(this);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this instruction.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return InstructionType.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of this instruction.
        /// </summary>
        /// <returns>The string representation.</returns>
        public override string ToString()
        {
            string baseArg;
            if (Argument == null)
                baseArg = $"{Offset.ToString("X4")}: {InstructionType}";
            else
                baseArg = $"{Offset.ToString("X4")}: {InstructionType} [{Argument}]";

            if (Flags != ILInstructionFlags.None)
                return $"{FlagsContext}.{baseArg}";
            else
                return baseArg;
        }

        #endregion
    }
}

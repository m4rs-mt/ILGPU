// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILInstructionTypes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPUC.Frontend;

/// <summary>
/// Represents an instruction type of a single IL instruction.
/// </summary>
enum ILInstructionType
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
    ///
    /// <summary>LoadFunction</summary>
    ///
    LdFunction,
    ///
    /// <summary>LoadVirtualFunction</summary>
    ///
    LdVirtualFunction,
    ///
    /// <summary>ThrowException</summary>
    ///
    Throw,
    ///
    /// <summary>RethrowException</summary>
    ///
    Rethrow,
    ///
    /// <summary>LeaveExceptionBlock</summary>
    ///
    Leave,
    ///
    /// <summary>EndFinallyBlock</summary>
    ///
    EndFinally
}

/// <summary>
/// Represent flags of an IL instruction.
/// </summary>
[Flags]
enum ILInstructionFlags : int
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
/// Helper class to handle instruction type functions.
/// </summary>
static class ILInstructionTypes
{
    private static readonly Dictionary<
        ILInstructionType,
        (ushort PopCount, ushort PushCount)> _stackBehavior = new()
       {
            // Misc
            { ILInstructionType.Nop, (0, 0) },
            { ILInstructionType.Break, (0, 0) },

            // Arguments
            { ILInstructionType.Ldarg, (0, 1) },
            { ILInstructionType.Ldarga, (0, 1) },
            { ILInstructionType.Starg, (1, 0) },

            { ILInstructionType.Ldloc, (0, 1) },
            { ILInstructionType.Ldloca, (0, 1) },
            { ILInstructionType.Stloc, (1, 0) },

            // Constants
            { ILInstructionType.Ldnull, (0, 1) },
            { ILInstructionType.LdI4, (0, 1) },
            { ILInstructionType.LdI8, (0, 1) },
            { ILInstructionType.LdR4, (0, 1) },
            { ILInstructionType.LdR8, (0, 1) },
            { ILInstructionType.Ldstr, (0, 1) },

            // Stack
            { ILInstructionType.Dup, (1, 2) },
            { ILInstructionType.Pop, (1, 0) },
            { ILInstructionType.Jmp, (0, 0) },

            // Call
            { ILInstructionType.Call, (ushort.MaxValue, ushort.MaxValue) },
            { ILInstructionType.Calli, (ushort.MaxValue, ushort.MaxValue) },
            { ILInstructionType.Callvirt, (ushort.MaxValue, ushort.MaxValue) },
            { ILInstructionType.Ret, (ushort.MaxValue, ushort.MaxValue) },

            // Branch
            { ILInstructionType.Br, (0, 0) },
            { ILInstructionType.Brfalse, (1, 0) },
            { ILInstructionType.Brtrue, (1, 0) },
            { ILInstructionType.Beq, (2, 0) },
            { ILInstructionType.Bgt, (2, 0) },
            { ILInstructionType.Ble, (2, 0) },
            { ILInstructionType.Blt, (2, 0) },
            { ILInstructionType.Bne, (2, 0) },
            { ILInstructionType.Bge, (2, 0) },
            { ILInstructionType.Switch, (1, 0) },

            // Arithmetic
            { ILInstructionType.Add, (2, 1) },
            { ILInstructionType.Sub, (2, 1) },
            { ILInstructionType.Mul, (2, 1) },
            { ILInstructionType.Div, (2, 1) },
            { ILInstructionType.Rem, (2, 1) },
            { ILInstructionType.And, (2, 1) },
            { ILInstructionType.Or, (2, 1) },
            { ILInstructionType.Xor, (2, 1) },
            { ILInstructionType.Shl, (2, 1) },
            { ILInstructionType.Shr, (2, 1) },
            { ILInstructionType.Neg, (1, 1) },
            { ILInstructionType.Not, (1, 1) },

            // Conversion
            { ILInstructionType.Conv, (1, 1) },

            // General Objects
            { ILInstructionType.Initobj, (1, 0) },
            { ILInstructionType.Newobj, (ushort.MaxValue, ushort.MaxValue) },
            { ILInstructionType.Newarr, (1, 1) },

            // Boxing
            { ILInstructionType.Box, (1, 1) },
            { ILInstructionType.Unbox, (1, 1) },

            { ILInstructionType.Castclass, (1, 1) },
            { ILInstructionType.Isinst, (1, 1) },

            // Fields
            { ILInstructionType.Ldfld, (1, 1) },
            { ILInstructionType.Ldflda, (1, 1) },
            { ILInstructionType.Ldsfld, (0, 1) },
            { ILInstructionType.Ldsflda, (0, 1) },
            { ILInstructionType.Stfld, (2, 0) },
            { ILInstructionType.Stsfld, (1, 0) },

            // Indirect Objects
            { ILInstructionType.Ldobj, (1, 1) },
            { ILInstructionType.Stobj, (2, 0) },
            { ILInstructionType.Cpobj, (2, 0) },

            // Arrays
            { ILInstructionType.Ldlen, (1, 1) },
            { ILInstructionType.Ldelem, (2, 1) },
            { ILInstructionType.Ldelema, (2, 1) },
            { ILInstructionType.Stelem, (3, 0) },

            // Compare
            { ILInstructionType.Ceq, (2, 1) },
            { ILInstructionType.Cgt, (2, 1) },
            { ILInstructionType.Clt, (2, 1) },

            // Indirect
            { ILInstructionType.Ldind, (1, 1) },
            { ILInstructionType.Stind, (2, 0) },
            { ILInstructionType.Localloc, (1, 1) },

            // Blk
            { ILInstructionType.Cpblk, (3, 0) },
            { ILInstructionType.Initblk, (3, 0) },

            // SizeOf
            { ILInstructionType.SizeOf, (0, 1) },

            // Token
            { ILInstructionType.LdToken, (0, 1) },

            // Function
            { ILInstructionType.LdFunction, (0, 1) },
            { ILInstructionType.LdVirtualFunction, (0, 1) },

            // Exceptions
            { ILInstructionType.Throw, (1, 0) },
            { ILInstructionType.Leave, (0, 0) },
            { ILInstructionType.EndFinally, (0, 0) },
        };

    /// <summary>
    /// Gets stack behavior for the given instruction.
    /// </summary>
    /// <param name="instruction">The instruction to analyze.</param>
    /// <returns>The push and pop count effects on the stack.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static (ushort PushCount, ushort PopCount) GetPushPopBehavior(
        this ILInstruction instruction)
    {
        switch (instruction.InstructionType)
        {
            case ILInstructionType.Call:
            case ILInstructionType.Callvirt:
                {
                    var methodInfo = instruction.GetArgumentAs<MethodInfo>();
                    int popCount = methodInfo.GetParameters().Length +
                        methodInfo.GetParameterOffset();
                    ushort pushCount = methodInfo.ReturnType != typeof(void)
                        ? (ushort)1 : (ushort)0;
                    return ((ushort)popCount, pushCount);
                }
            case ILInstructionType.Newobj:
                {
                    var constructorInfo = instruction.GetArgumentAs<ConstructorInfo>();
                    int popCount = constructorInfo.GetParameters().Length;
                    return ((ushort)popCount, 1);
                }
            case ILInstructionType.Ret:
                {
                    var methodBase = instruction.GetArgumentAs<MethodBase>();
                    return ((ushort)methodBase.GetParameterOffset(), 0);
                }
            default:
                return _stackBehavior[instruction.InstructionType];
        }
    }
}

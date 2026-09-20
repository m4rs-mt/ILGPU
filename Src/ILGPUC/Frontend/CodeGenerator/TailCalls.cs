// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TailCalls.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.Frontend.Intrinsic;
using ILGPUC.IR;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// The initial values of all local variables, which a call of the current
    /// method in tail position assigns again (see <see cref="MakeTailCall"/>).
    /// </summary>
    private readonly List<(VariableRef Local, Value Value)> _localInitialValues = [];

    /// <summary>
    /// The indices of the calls of the current method in tail position (null if
    /// the method cannot turn them into jumps).
    /// </summary>
    private readonly HashSet<int>? _tailCallCandidates;

    /// <summary>
    /// Returns true if the instruction at the given index calls the current method
    /// in tail position: the result of the call is returned right away, possibly
    /// through a local variable and unconditional branches. The control-flow
    /// builder turns such a call into a jump to the start of the method if only
    /// the call arguments are on the stack, so that a method calling itself in
    /// tail position becomes a loop.
    /// </summary>
    /// <param name="index">The instruction index.</param>
    /// <returns>True if the instruction is a call in tail position.</returns>
    internal bool IsTailCallCandidate(int index) =>
        _tailCallCandidates?.Contains(index) == true;

    /// <summary>
    /// Finds all calls of the current method in tail position.
    /// </summary>
    /// <returns>
    /// The instruction indices of the calls, or null if the method cannot turn
    /// calls of itself into jumps.
    /// </returns>
    private HashSet<int>? FindTailCallCandidates()
    {
        if (Method is not MethodInfo { IsStatic: true } method)
            return null;

        HashSet<int>? candidates = null;
        Dictionary<int, int>? indices = null;
        bool hasResult = method.ReturnType != typeof(void);
        for (int i = 0, e = DisassembledMethod.Count; i < e; ++i)
        {
            var instruction = DisassembledMethod[i];
            if (instruction.InstructionType != ILInstructionType.Call ||
                instruction.HasFlags(ILInstructionFlags.Constrained) ||
                !Method.Equals(instruction.Argument))
            {
                continue;
            }

            if (indices is null)
            {
                indices = new Dictionary<int, int>(e);
                for (int j = 0; j < e; ++j)
                    indices[DisassembledMethod[j].Offset] = j;
            }
            if (IsTailPosition(i, hasResult, indices))
                (candidates ??= []).Add(i);
        }
        return candidates is not null && CanJumpToStart() ? candidates : null;
    }

    /// <summary>
    /// Returns true if calls of the current method can jump to its start instead:
    /// the method is a static helper without exception handling, delegates and
    /// arrays it allocates, whose parameters are values, and whose next call
    /// cannot receive the address of one of its variables. An address of a
    /// variable or array of the current call would refer to the one of the next
    /// iteration instead.
    /// </summary>
    private bool CanJumpToStart()
    {
        if (IsEntryPoint ||
            _delegateTargets is not null ||
            Method is not MethodInfo { IsStatic: true } method ||
            (method.CallingConvention & CallingConventions.VarArgs) != 0 ||
            !IsPlainValue(method.ReturnType) ||
            DisassembledMethod.CatchClauses.Length > 0 ||
            DisassembledMethod.FinallyClauses.Length > 0)
        {
            return false;
        }

        foreach (var parameter in method.GetParameters())
        {
            if (!IsPlainValue(parameter.ParameterType))
                return false;
        }
        foreach (var local in method.GetMethodBody().AsNotNull().LocalVariables)
        {
            if (local.LocalType.IsDelegate())
                return false;
        }
        bool takesAddresses = false;
        bool convertsAddresses = false;
        List<MethodBase>? callees = null;
        foreach (var instruction in DisassembledMethod.Instructions)
        {
            switch (instruction.InstructionType)
            {
                case ILInstructionType.Newarr:
                case ILInstructionType.Localloc:
                    return false;
                case ILInstructionType.Ldloca:
                case ILInstructionType.Ldarga:
                    takesAddresses = true;
                    break;
                case ILInstructionType.Conv:
                    convertsAddresses |= Equals(
                        instruction.Argument,
                        Disassembler.NativePtrType);
                    break;
                case ILInstructionType.Call:
                case ILInstructionType.Callvirt:
                case ILInstructionType.Newobj:
                    if (!TryGetCallee(instruction, out var callee) ||
                        callee is MethodInfo info &&
                        CanHoldAddress(info.ReturnType, []))
                    {
                        convertsAddresses = true;
                    }
                    else if (ReceivesAddresses(callee))
                    {
                        // A structure constructed in place is a variable, too
                        takesAddresses |= instruction.InstructionType ==
                            ILInstructionType.Newobj &&
                            callee.DeclaringType?.IsValueType == true;
                        (callees ??= []).Add(callee);
                    }
                    break;
            }
        }
        if (!takesAddresses)
            return true;

        // Only a pointer or a pointer-sized integer carries the address of a
        // variable to the next call: converted in this method or returned by a
        // method it calls, it can reach it in any argument or through memory, and
        // stored by a method it calls, in a parameter that can hold it
        if (convertsAddresses)
            return false;
        foreach (var parameter in method.GetParameters())
        {
            if (CanHoldAddress(parameter.ParameterType, []))
                return false;
        }

        // A method receiving the address can convert and store it in turn
        if (callees is not null)
        {
            var visited = new HashSet<MethodBase>();
            foreach (var callee in callees)
            {
                if (CanConvertAddresses(callee, visited))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns true if the given type is a value type that is no pointer and no
    /// reference.
    /// </summary>
    private static bool IsPlainValue(Type type) =>
        type.IsValueType && !type.IsPointer && !type.IsByRefLike;

    /// <summary>
    /// Returns true if the given type is a pointer, a pointer-sized integer or a
    /// structure with fields of them.
    /// </summary>
    private static bool CanHoldAddress(Type type, HashSet<Type> visited)
    {
        if (type.IsPointer ||
            type.IsFunctionPointer ||
            type == typeof(nint) ||
            type == typeof(nuint))
        {
            return true;
        }
        if (!type.IsValueType || type.IsPrimitive || !visited.Add(type))
            return false;
        foreach (var field in type.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (CanHoldAddress(field.FieldType, visited))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if a call of the given method can receive the address of a
    /// variable: as the instance of a structure method or in a parameter that is a
    /// reference or can hold an address.
    /// </summary>
    private static bool ReceivesAddresses(MethodBase method)
    {
        if (!method.IsStatic && method.DeclaringType?.IsValueType == true)
            return true;
        foreach (var parameter in method.GetParameters())
        {
            var type = parameter.ParameterType;
            if (type.IsByRef || type.IsByRefLike || CanHoldAddress(type, []))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Resolves the method called by the given call instruction, following
    /// constrained calls to their implementation.
    /// </summary>
    /// <param name="instruction">The call instruction.</param>
    /// <param name="callee">The called method.</param>
    /// <returns>False if the called method cannot be determined.</returns>
    private bool TryGetCallee(
        ILInstruction instruction,
        [NotNullWhen(true)] out MethodBase? callee)
    {
        callee = instruction.Argument as MethodBase;
        if (callee is not MethodInfo virtualMethod ||
            !instruction.HasFlags(ILInstructionFlags.Constrained))
        {
            return callee is not null;
        }
        try
        {
            callee = ResolveVirtualCallTarget(
                virtualMethod,
                instruction.FlagsContext.Argument as Type);
            return true;
        }
        catch (InternalCompilerException)
        {
            callee = null;
            return false;
        }
    }

    /// <summary>
    /// Returns true if the given method, or a method it passes an address to, can
    /// convert an address to a pointer or pointer-sized integer, which it can then
    /// store. Intrinsics do not; methods without a body might.
    /// </summary>
    /// <param name="method">The method receiving an address.</param>
    /// <param name="visited">The methods checked so far.</param>
    private bool CanConvertAddresses(MethodBase method, HashSet<MethodBase> visited)
    {
        if (!visited.Add(method) ||
            !Intrinsics.TryGetEvaluationTarget(method, out var target))
        {
            return false;
        }
        DisassembledMethod? body;
        try
        {
            body = Disassembler.TryDisassemble(target);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            return true;
        }
        if (body is null)
            return true;

        foreach (var instruction in body.Instructions)
        {
            switch (instruction.InstructionType)
            {
                case ILInstructionType.Conv when Equals(
                    instruction.Argument,
                    Disassembler.NativePtrType):
                    return true;
                case ILInstructionType.Call:
                case ILInstructionType.Callvirt:
                case ILInstructionType.Newobj:
                    if (!TryGetCallee(instruction, out var callee) ||
                        callee is MethodInfo info &&
                        CanHoldAddress(info.ReturnType, []) ||
                        ReceivesAddresses(callee) &&
                        CanConvertAddresses(callee, visited))
                    {
                        return true;
                    }
                    break;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true if the result of the call at the given index is returned
    /// right away: the instructions following the call only branch, store the
    /// result to a local variable and load it again, and return.
    /// </summary>
    /// <param name="index">The index of the call instruction.</param>
    /// <param name="hasResult">True if the call returns a value.</param>
    /// <param name="indices">Maps instruction offsets to indices.</param>
    private bool IsTailPosition(
        int index,
        bool hasResult,
        Dictionary<int, int> indices)
    {
        bool isResultOnStack = hasResult;
        int resultLocal = -1;
        var visited = new HashSet<int>();
        for (int i = index + 1; i < DisassembledMethod.Count && visited.Add(i);)
        {
            var instruction = DisassembledMethod[i];
            switch (instruction.InstructionType)
            {
                case ILInstructionType.Nop:
                    ++i;
                    break;
                case ILInstructionType.Br:
                    {
                        var targets =
                            instruction.GetArgumentAs<ILInstructionBranchTargets>();
                        if (!indices.TryGetValue(targets.GetTargetOffsets()[0], out i))
                            return false;
                    }
                    break;
                case ILInstructionType.Stloc when isResultOnStack:
                    resultLocal = instruction.GetArgumentAs<int>();
                    isResultOnStack = false;
                    ++i;
                    break;
                case ILInstructionType.Ldloc when
                    !isResultOnStack &&
                    instruction.GetArgumentAs<int>() == resultLocal:
                    isResultOnStack = true;
                    ++i;
                    break;
                case ILInstructionType.Ret:
                    return isResultOnStack == hasResult;
                default:
                    return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Realizes a call of the current method in tail position as the start of
    /// the next iteration of the loop it becomes: assigns the call arguments to
    /// the parameters and the initial values to all local variables. The block
    /// of the call then jumps to the start of the method.
    /// </summary>
    private void MakeTailCall()
    {
        for (int i = Method.GetParameters().Length - 1; i >= 0; --i)
            StoreVariable(new VariableRef(i, VariableRefType.Argument));

        foreach (var (local, value) in _localInitialValues)
        {
            if (_variables.Contains(local))
                Builder.CreateStore(Location, Block.GetValue(local), value);
            else
                Block.SetValue(local, value);
        }
    }
}

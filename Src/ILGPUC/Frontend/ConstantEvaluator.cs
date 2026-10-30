// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConstantEvaluator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.Frontend.Intrinsic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPUC.Frontend;

/// <summary>
/// The result of evaluating a call at compile time.
/// </summary>
/// <param name="IsConstant">
/// <see langword="true"/> if the call evaluated to a constant.
/// </param>
/// <param name="Value">
/// The constant, typed as the method's return type (<see cref="bool"/>,
/// <see cref="int"/>, an enum value, ...).
/// </param>
/// <param name="Reason">Why the call is not constant, if it is not.</param>
/// <param name="IsBudgetExceeded">
/// <see langword="true"/> if the evaluation ran out of its instruction budget or
/// nested calls too deeply. Such results depend on where the evaluation started and
/// are not memoized.
/// </param>
/// <param name="Cost">
/// The number of instructions the evaluation executed, including nested calls. A
/// memoized constant charges this cost to every evaluation that uses it, so that
/// whether an evaluation fits its budget does not depend on what the memo holds.
/// </param>
readonly record struct ConstantEvaluationResult(
    bool IsConstant,
    object? Value,
    string? Reason,
    bool IsBudgetExceeded = false,
    int Cost = 0)
{
    /// <summary>
    /// Creates a constant result.
    /// </summary>
    public static ConstantEvaluationResult Constant(object? value, int cost = 0) =>
        new(true, value, null, Cost: cost);

    /// <summary>
    /// Creates a non-constant result.
    /// </summary>
    public static ConstantEvaluationResult NotConstant(string reason) =>
        new(false, null, reason);
}

/// <summary>
/// Evaluates calls of closed methods at compile time, when all arguments are
/// constants. This lets helper predicates such as <c>IsFloat&lt;T&gt;()</c> fold at
/// any call depth, so that type tests in callers turn into constant branches, and
/// recursive helpers such as <c>Factorial(5)</c> fold to their results.
/// </summary>
/// <remarks>
/// <para>
/// The evaluator interprets the IL of the callee concretely: with constant inputs,
/// execution follows a single path, so there are no phis or merges. It emits no IR,
/// so a failed attempt leaves nothing behind and the frontend simply compiles the
/// call as before.
/// </para>
/// <para>
/// Supported are loads and stores of arguments and locals, constants, arithmetic,
/// comparisons, conversions, branches and switches, <c>sizeof</c>, init-only static
/// fields, the reflection members of <see cref="ReflectionFolding"/>, and calls,
/// including constrained calls of static abstract and struct instance members.
/// Struct instances are opaque: reading their memory makes the result non-constant.
/// Intrinsic methods are not evaluated, except through their remappings.
/// </para>
/// <para>
/// Results are memoized per closed method, argument constants and pointer size.
/// Re-entering a call that is still being evaluated with the same arguments is
/// recursion that cannot terminate with constant inputs and is not constant;
/// recursion over different instantiations or arguments terminates through the
/// memo or a base case. An instruction budget per evaluation stops runaway loops,
/// and a limit of <see cref="MaxCallDepth"/> nested calls keeps deep recursion
/// from exhausting the stack of the compiler.
/// </para>
/// </remarks>
sealed class ConstantEvaluator
{
    #region Nested Types

    /// <summary>
    /// The address of a value the evaluator does not model: an argument or local
    /// struct, or the instance of a struct member. Reading through it is not
    /// constant.
    /// </summary>
    private sealed class OpaqueAddress
    {
        /// <summary>The shared instance.</summary>
        public static readonly OpaqueAddress Instance = new();
    }

    /// <summary>
    /// The address of a local variable.
    /// </summary>
    /// <param name="Index">The local index.</param>
    private sealed record LocalAddress(int Index);

    /// <summary>
    /// The address of an argument.
    /// </summary>
    /// <param name="Index">The argument index.</param>
    private sealed record ArgumentAddress(int Index);

    /// <summary>
    /// A boxed value type, which is never null and whose type is known exactly.
    /// </summary>
    /// <param name="Type">The boxed type.</param>
    /// <param name="Value">The boxed stack value.</param>
    private sealed record Boxed(Type Type, object? Value);

    /// <summary>
    /// The address of the value inside a box, as pushed by <c>unbox</c>.
    /// </summary>
    /// <param name="Value">The boxed stack value.</param>
    private sealed record UnboxedAddress(object? Value);

    /// <summary>
    /// A struct value the evaluator does not model.
    /// </summary>
    private sealed class OpaqueStruct
    {
        /// <summary>The shared instance.</summary>
        public static readonly OpaqueStruct Instance = new();
    }

    /// <summary>
    /// Signals that evaluation stopped with a non-constant result.
    /// </summary>
    private sealed class NotConstantException : Exception
    {
        /// <summary>Creates a new signal.</summary>
        public NotConstantException() { }

        /// <summary>Creates a new signal.</summary>
        /// <param name="message">Why evaluation stopped.</param>
        public NotConstantException(string message)
            : base(message)
        { }

        /// <summary>Creates a new signal.</summary>
        /// <param name="message">Why evaluation stopped.</param>
        /// <param name="isBudgetExceeded">Whether the budget ran out.</param>
        public NotConstantException(string message, bool isBudgetExceeded)
            : base(message)
        {
            IsBudgetExceeded = isBudgetExceeded;
        }

        /// <summary>Creates a new signal.</summary>
        /// <param name="message">Why evaluation stopped.</param>
        /// <param name="innerException">The inner exception.</param>
        public NotConstantException(string message, Exception innerException)
            : base(message, innerException)
        { }

        /// <summary>Whether the instruction budget ran out.</summary>
        public bool IsBudgetExceeded { get; }
    }

    #endregion

    #region Constants

    /// <summary>
    /// The maximum number of nested calls of one evaluation.
    /// </summary>
    public const int MaxCallDepth = 256;

    #endregion

    #region Instance

    private readonly ConcurrentDictionary<
        (MethodBase Method, string Arguments, bool Is64Bit),
        ConstantEvaluationResult> _memo;
    private readonly Func<MethodBase, DisassembledMethod?> _disassemble;
    private readonly HashSet<(MethodBase, string)> _inProgress = [];
    private readonly bool _is64Bit;
    private int _remainingBudget;

    /// <summary>
    /// Creates a new evaluator.
    /// </summary>
    /// <param name="memo">
    /// The memo of evaluated calls, shared across frontends of one compiler.
    /// </param>
    /// <param name="disassemble">Disassembles a method, or returns null.</param>
    /// <param name="budget">
    /// The number of IL instructions one evaluation may execute, across all frames.
    /// </param>
    /// <param name="is64Bit">Whether the target has 64-bit pointers.</param>
    public ConstantEvaluator(
        ConcurrentDictionary<(MethodBase, string, bool), ConstantEvaluationResult> memo,
        Func<MethodBase, DisassembledMethod?> disassemble,
        int budget,
        bool is64Bit)
    {
        _memo = memo;
        _disassemble = disassemble;
        Budget = budget;
        _is64Bit = is64Bit;
    }

    /// <summary>
    /// Returns the instruction budget of one evaluation.
    /// </summary>
    public int Budget { get; }

    /// <summary>
    /// Returns the number of evaluations requested.
    /// </summary>
    public int EvaluationCount { get; private set; }

    /// <summary>
    /// Returns the number of evaluations answered from the memo.
    /// </summary>
    public int MemoHitCount { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Evaluates a call of <paramref name="method"/>.
    /// </summary>
    /// <param name="method">The closed method to call.</param>
    /// <param name="arguments">
    /// The constant arguments, the instance first for instance methods. Integers
    /// are <see cref="int"/> or <see cref="long"/>, floats <see cref="double"/>,
    /// types <see cref="Type"/>. An instance argument may be <see langword="null"/>
    /// to stand for the address of a struct whose contents are unknown.
    /// </param>
    public ConstantEvaluationResult Evaluate(MethodBase method, object?[] arguments)
    {
        _remainingBudget = Budget;
        try
        {
            // Follow remappings and refuse intrinsics, as for nested calls
            if (!Intrinsics.TryGetEvaluationTarget(method, out var target) ||
                target is not MethodInfo { IsAbstract: false })
            {
                throw NotConstant(method, "is an intrinsic or abstract");
            }

            // The caller passes IR constants, which do not distinguish signed
            // from unsigned types; narrow them to the parameter types
            var parameters = target.GetParameters();
            int offset = target.IsStatic ? 0 : 1;
            if (arguments.Length != parameters.Length + offset)
                throw NotConstant(method, "is called with unexpected arguments");
            for (int i = 0; i < parameters.Length; ++i)
            {
                if (parameters[i].ParameterType.IsByRef)
                    throw NotConstant(method, "takes an argument by reference");
                arguments[i + offset] = Narrow(
                    arguments[i + offset],
                    parameters[i].ParameterType);
            }
            if (offset > 0)
                arguments[0] ??= OpaqueAddress.Instance;

            var value = Call(target, arguments);
            return ConstantEvaluationResult.Constant(value, Budget - _remainingBudget);
        }
        catch (NotConstantException e)
        {
            return new(false, null, e.Message, e.IsBudgetExceeded);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception e)
#pragma warning restore CA1031
        {
            // Malformed or unexpected IL must never fail code generation: the
            // call is simply compiled as a call
            return ConstantEvaluationResult.NotConstant(
                $"'{FrontendDisplay.Format(method)}' could not be evaluated: " +
                e.Message);
        }
    }

    /// <summary>
    /// Returns true if the given method calls other methods, which may call it
    /// again. Methods that call nothing fold just as well after inlining.
    /// </summary>
    /// <param name="method">The closed method to check.</param>
    public bool CallsMethods(MethodBase method)
    {
        if (!Intrinsics.TryGetEvaluationTarget(method, out var target) ||
            _disassemble(target) is not { } body)
        {
            return false;
        }
        foreach (var instruction in body.Instructions)
        {
            if (instruction.InstructionType is
                ILInstructionType.Call or ILInstructionType.Callvirt)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Evaluates a call, consulting and updating the memo.
    /// </summary>
    private object? Call(MethodBase method, object?[] arguments)
    {
        ++EvaluationCount;
        var argumentKey = GetArgumentKey(arguments);
        var key = (method, argumentKey, _is64Bit);
        if (_memo.TryGetValue(key, out var cached))
        {
            ++MemoHitCount;
            if (!cached.IsConstant)
                throw new NotConstantException(cached.Reason!);
            _remainingBudget -= cached.Cost;
            if (_remainingBudget < 0)
                throw BudgetExceeded(method);
            return cached.Value;
        }

        if (_inProgress.Count >= MaxCallDepth ||
            !RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new NotConstantException(
                $"the evaluation nests calls {_inProgress.Count} deep at " +
                $"'{FrontendDisplay.Format(method)}'",
                isBudgetExceeded: true);
        }
        if (!_inProgress.Add((method, argumentKey)))
        {
            throw new NotConstantException(
                $"'{FrontendDisplay.Format(method)}' calls itself recursively " +
                "with the same arguments");
        }

        try
        {
            int budgetBefore = _remainingBudget;
            var value = Interpret(method, arguments);
            _memo[key] = ConstantEvaluationResult.Constant(
                value,
                budgetBefore - _remainingBudget);
            return value;
        }
        catch (NotConstantException e) when (!e.IsBudgetExceeded)
        {
            _memo[key] = ConstantEvaluationResult.NotConstant(e.Message);
            throw;
        }
        finally
        {
            _inProgress.Remove((method, argumentKey));
        }
    }

    /// <summary>
    /// Creates the exception for an evaluation that ran out of its budget in
    /// <paramref name="method"/>.
    /// </summary>
    private NotConstantException BudgetExceeded(MethodBase method) =>
        new(
            $"the evaluation exceeded its budget of {Budget} instructions " +
            $"in '{FrontendDisplay.Format(method)}'",
            isBudgetExceeded: true);

    /// <summary>
    /// Returns a memo key for a list of argument constants.
    /// </summary>
    private static string GetArgumentKey(object?[] arguments)
    {
        if (arguments.Length == 0)
            return string.Empty;
        var parts = new string[arguments.Length];
        for (int i = 0; i < arguments.Length; ++i)
        {
            parts[i] = arguments[i] switch
            {
                null => "null",
                Type type => $"t:{type.TypeHandle.Value}",
                int value => $"i:{value}",
                long value => $"l:{value}",
                float value => $"f:{BitConverter.SingleToInt32Bits(value)}",
                double value => $"d:{BitConverter.DoubleToInt64Bits(value)}",
                OpaqueAddress => "a",
                var other => $"{other.GetType().Name}:{other}",
            };
        }
        return string.Join("|", parts);
    }

    /// <summary>
    /// Interprets the IL of <paramref name="method"/> with the given arguments.
    /// </summary>
    private object? Interpret(MethodBase method, object?[] arguments)
    {
        if (method is not MethodInfo methodInfo || methodInfo.ReturnType == typeof(void))
            throw NotConstant(method, "does not return a value");

        var body = _disassemble(method) ??
            throw NotConstant(method, "has no IL body that can be evaluated");
        if (body.CatchClauses.Length > 0 || body.FinallyClauses.Length > 0)
            throw NotConstant(method, "uses exception handling");

        var instructions = body.Instructions;
        var offsets = new Dictionary<int, int>(instructions.Length);
        for (int i = 0; i < instructions.Length; ++i)
            offsets[instructions[i].Offset] = i;

        var locals = CreateLocals(method);
        var args = (object?[])arguments.Clone();
        var stack = new Stack<object?>(body.MaxStackSize + 1);

        int pc = 0;
        while (true)
        {
            if (--_remainingBudget < 0)
                throw BudgetExceeded(method);
            if (pc >= instructions.Length)
                throw NotConstant(method, "runs past its last instruction");

            var instruction = instructions[pc];
            int next = pc + 1;
            switch (instruction.InstructionType)
            {
                case ILInstructionType.Nop:
                    break;

                case ILInstructionType.Ldarg:
                    stack.Push(args[instruction.GetArgumentAs<int>()]);
                    break;
                case ILInstructionType.Starg:
                    {
                        int index = instruction.GetArgumentAs<int>();
                        int offset = method.IsStatic ? 0 : 1;
                        var value = stack.Pop();
                        args[index] = index < offset
                            ? value
                            : Narrow(
                                value,
                                method.GetParameters()[index - offset].ParameterType);
                    }
                    break;
                case ILInstructionType.Ldarga:
                    stack.Push(new ArgumentAddress(instruction.GetArgumentAs<int>()));
                    break;
                case ILInstructionType.Ldloc:
                    stack.Push(locals[instruction.GetArgumentAs<int>()]);
                    break;
                case ILInstructionType.Stloc:
                    {
                        int index = instruction.GetArgumentAs<int>();
                        locals[index] = Narrow(
                            stack.Pop(),
                            GetLocalType(method, index));
                    }
                    break;
                case ILInstructionType.Ldloca:
                    stack.Push(new LocalAddress(instruction.GetArgumentAs<int>()));
                    break;

                case ILInstructionType.LdI4:
                    stack.Push(instruction.GetArgumentAs<int>());
                    break;
                case ILInstructionType.LdI8:
                    stack.Push(instruction.GetArgumentAs<long>());
                    break;
                case ILInstructionType.LdR4:
                    stack.Push(instruction.GetArgumentAs<float>());
                    break;
                case ILInstructionType.LdR8:
                    stack.Push(instruction.GetArgumentAs<double>());
                    break;
                case ILInstructionType.Ldnull:
                    stack.Push(null);
                    break;
                case ILInstructionType.LdToken:
                    stack.Push(instruction.Argument);
                    break;
                case ILInstructionType.SizeOf:
                    stack.Push(ReflectionFolding.GetManagedSize(
                        instruction.GetArgumentAs<Type>(),
                        _is64Bit));
                    break;

                case ILInstructionType.Dup:
                    stack.Push(stack.Peek());
                    break;
                case ILInstructionType.Pop:
                    stack.Pop();
                    break;

                case ILInstructionType.Ret:
                    return ToReturnValue(method, methodInfo.ReturnType, stack.Pop());

                case ILInstructionType.Br:
                    next = Jump(offsets, instruction, 0, method);
                    break;
                case ILInstructionType.Brtrue:
                case ILInstructionType.Brfalse:
                    {
                        bool truth = IsTrue(method, instruction, stack.Pop());
                        bool taken = instruction.InstructionType ==
                            ILInstructionType.Brtrue ? truth : !truth;
                        next = Jump(offsets, instruction, taken ? 0 : 1, method);
                    }
                    break;
                case ILInstructionType.Beq:
                case ILInstructionType.Bne:
                case ILInstructionType.Bge:
                case ILInstructionType.Bgt:
                case ILInstructionType.Ble:
                case ILInstructionType.Blt:
                    {
                        var right = stack.Pop();
                        var left = stack.Pop();
                        bool taken = CompareBranch(
                            method,
                            instruction,
                            left,
                            right);
                        next = Jump(offsets, instruction, taken ? 0 : 1, method);
                    }
                    break;
                case ILInstructionType.Switch:
                    {
                        var selector = stack.Pop();
                        ulong index = selector switch
                        {
                            int value => (uint)value,
                            long value => (ulong)value,
                            _ => throw NotConstant(
                                method,
                                instruction,
                                "switches over a non-integer"),
                        };
                        var targets = instruction
                            .GetArgumentAs<ILInstructionBranchTargets>();
                        int target = index < (ulong)(targets.Count - 1)
                            ? (int)index + 1
                            : 0;
                        next = Jump(offsets, instruction, target, method);
                    }
                    break;

                case ILInstructionType.Add:
                case ILInstructionType.Sub:
                case ILInstructionType.Mul:
                case ILInstructionType.Div:
                case ILInstructionType.Rem:
                case ILInstructionType.And:
                case ILInstructionType.Or:
                case ILInstructionType.Xor:
                case ILInstructionType.Shl:
                case ILInstructionType.Shr:
                    {
                        var right = stack.Pop();
                        var left = stack.Pop();
                        stack.Push(Binary(method, instruction, left, right));
                    }
                    break;
                case ILInstructionType.Neg:
                case ILInstructionType.Not:
                    stack.Push(Unary(method, instruction, stack.Pop()));
                    break;

                case ILInstructionType.Conv:
                    {
                        var value = stack.Pop();
                        var targetType = instruction.Argument is
                            DependentTypeArguments dependent
                            ? dependent.DetermineType(GetBasicValueType(value))
                            : instruction.GetArgumentAs<Type>();
                        stack.Push(Convert(method, instruction, value, targetType));
                    }
                    break;

                case ILInstructionType.Ceq:
                case ILInstructionType.Cgt:
                case ILInstructionType.Clt:
                    {
                        var right = stack.Pop();
                        var left = stack.Pop();
                        stack.Push(Compare(method, instruction, left, right) ? 1 : 0);
                    }
                    break;

                case ILInstructionType.Ldsfld:
                    stack.Push(LoadStaticField(
                        method,
                        instruction,
                        instruction.GetArgumentAs<FieldInfo>()));
                    break;

                case ILInstructionType.Initobj:
                    {
                        var value = GetDefault(instruction.GetArgumentAs<Type>());
                        switch (stack.Pop())
                        {
                            case LocalAddress local:
                                locals[local.Index] = value;
                                break;
                            case ArgumentAddress argument:
                                args[argument.Index] = value;
                                break;
                            default:
                                // Writing through this or another address has an
                                // effect the caller would observe
                                throw NotConstant(method, instruction, "writes memory");
                        }
                    }
                    break;

                case ILInstructionType.Call:
                case ILInstructionType.Callvirt:
                    {
                        var target = instruction.GetArgumentAs<MethodBase>();
                        var constrained = instruction.HasFlags(
                            ILInstructionFlags.Constrained)
                            ? instruction.FlagsContext.Argument as Type
                            : null;
                        var result = CallFrom(
                            method,
                            instruction,
                            target,
                            constrained,
                            stack);
                        stack.Push(result);
                    }
                    break;

                case ILInstructionType.Box:
                    {
                        var type = instruction.GetArgumentAs<Type>();
                        if (!type.IsValueType ||
                            Nullable.GetUnderlyingType(type) is not null)
                        {
                            throw NotConstant(method, instruction, "boxes a reference");
                        }
                        stack.Push(new Boxed(type, stack.Pop()));
                    }
                    break;
                case ILInstructionType.Isinst:
                    {
                        var type = instruction.GetArgumentAs<Type>();
                        stack.Push(stack.Pop() switch
                        {
                            null => null,
                            Boxed boxed =>
                                type.IsAssignableFrom(boxed.Type) ? boxed : null,
                            _ => throw NotConstant(
                                method,
                                instruction,
                                "tests the type of an object"),
                        });
                    }
                    break;
                case ILInstructionType.Unbox:
                    {
                        var type = instruction.GetArgumentAs<Type>();
                        if (stack.Pop() is not Boxed boxed || boxed.Type != type)
                            throw NotConstant(method, instruction, "unboxes an object");
                        stack.Push(new UnboxedAddress(boxed.Value));
                    }
                    break;
                case ILInstructionType.Ldobj:
                    if (stack.Pop() is not UnboxedAddress unboxed)
                        throw NotConstant(method, instruction, "reads memory");
                    stack.Push(unboxed.Value);
                    break;

                case ILInstructionType.Newobj:
                    throw NotConstant(method, instruction, "creates an object");
                case ILInstructionType.Throw:
                case ILInstructionType.Rethrow:
                    throw NotConstant(method, instruction, "throws an exception");

                default:
                    throw NotConstant(
                        method,
                        instruction,
                        $"uses the unsupported instruction " +
                        $"'{instruction.InstructionType}'");
            }
            pc = next;
        }
    }

    /// <summary>
    /// Evaluates a call instruction inside an interpreted method.
    /// </summary>
    private object? CallFrom(
        MethodBase caller,
        ILInstruction instruction,
        MethodBase target,
        Type? constrainedType,
        Stack<object?> stack)
    {
        if (target is not MethodInfo)
            throw NotConstant(caller, instruction, "calls a constructor");

        // GetType() of a value type is exact, because value types are sealed
        if (target.DeclaringType == typeof(object) && target.Name == nameof(GetType))
        {
            var instance = stack.Pop();
            if (constrainedType is { IsValueType: true })
                return constrainedType;
            if (instance is Boxed boxed)
                return boxed.Type;
            throw NotConstant(caller, instruction, "calls GetType() on an object");
        }

        // Resolve constrained calls of interface members on the constrained type
        if (constrainedType is not null)
        {
            target = ResolveConstrainedTarget(
                caller,
                instruction,
                target,
                constrainedType);
        }

        int count = target.GetParameters().Length + (target.IsStatic ? 0 : 1);
        var operands = new object?[count];
        for (int i = count - 1; i >= 0; --i)
            operands[i] = stack.Pop();

        if (ReflectionFolding.IsFolded(target))
        {
            for (int i = 0; i < count; ++i)
            {
                if (ReflectionFolding.IsTypeOperand(target, i) && operands[i] is not Type)
                {
                    throw NotConstant(
                        caller,
                        instruction,
                        $"passes a type that is not known to " +
                        $"'{FrontendDisplay.Format(target)}'");
                }
            }
            return ToStackValue(ReflectionFolding.Evaluate(target, operands, _is64Bit));
        }
        if (ReflectionFolding.IsUnsupportedReflection(target))
        {
            throw NotConstant(
                caller,
                instruction,
                $"calls the reflection member '{FrontendDisplay.Format(target)}'");
        }
        if (!Intrinsics.TryGetEvaluationTarget(target, out var evaluated))
        {
            throw NotConstant(
                caller,
                instruction,
                $"calls the intrinsic '{FrontendDisplay.Format(target)}'");
        }
        if (evaluated is not MethodInfo { IsAbstract: false } evaluatedMethod)
        {
            throw NotConstant(
                caller,
                instruction,
                $"calls the abstract member '{FrontendDisplay.Format(target)}'");
        }

        // A virtual call without a constrained type could dispatch to an
        // override, such as a default interface method a struct overrides
        if (constrainedType is null &&
            evaluatedMethod.IsVirtual &&
            !evaluatedMethod.IsFinal &&
            evaluatedMethod.DeclaringType is { IsSealed: false })
        {
            throw NotConstant(
                caller,
                instruction,
                $"makes the virtual call '{FrontendDisplay.Format(target)}'");
        }

        // Narrow the arguments to the parameter types, as storing them would
        var parameters = evaluatedMethod.GetParameters();
        int offset = evaluatedMethod.IsStatic ? 0 : 1;
        for (int i = 0; i < parameters.Length; ++i)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType.IsByRef)
                throw NotConstant(caller, instruction, "passes an argument by reference");
            operands[i + offset] = Narrow(operands[i + offset], parameterType);
        }
        if (offset > 0)
            operands[0] = ToInstance(caller, instruction, evaluatedMethod, operands[0]);

        try
        {
            return ToStackValue(Call(evaluatedMethod, operands));
        }
        catch (NotConstantException e) when (!e.IsBudgetExceeded)
        {
            throw new NotConstantException(
                $"'{FrontendDisplay.Format(evaluatedMethod)}' was not evaluated: " +
                e.Message);
        }
    }

    /// <summary>
    /// Maps the instance operand of a call to the evaluator's representation.
    /// Struct instances are opaque; reference instances are not supported.
    /// </summary>
    private static OpaqueAddress ToInstance(
        MethodBase caller,
        ILInstruction instruction,
        MethodBase target,
        object? instance) =>
        instance switch
        {
            LocalAddress or ArgumentAddress or OpaqueAddress =>
                OpaqueAddress.Instance,
            _ when target.DeclaringType?.IsValueType == true =>
                OpaqueAddress.Instance,
            _ => throw NotConstant(
                caller,
                instruction,
                $"calls '{FrontendDisplay.Format(target)}' on an object"),
        };

    /// <summary>
    /// Resolves the target of a constrained call on <paramref name="constrainedType"/>.
    /// </summary>
    private static MethodBase ResolveConstrainedTarget(
        MethodBase caller,
        ILInstruction instruction,
        MethodBase target,
        Type constrainedType)
    {
        if (target.DeclaringType is not { IsInterface: true } interfaceType)
        {
            // A call of a member the constrained type declares itself
            if (target.DeclaringType == constrainedType || !target.IsVirtual)
                return target;
            throw NotConstant(
                caller,
                instruction,
                $"makes a virtual call of '{FrontendDisplay.Format(target)}'");
        }

        var definition = target.IsGenericMethod
            ? ((MethodInfo)target).GetGenericMethodDefinition()
            : target;
        var map = constrainedType.GetInterfaceMap(interfaceType);
        for (int i = 0; i < map.InterfaceMethods.Length; ++i)
        {
            if (map.InterfaceMethods[i] != definition)
                continue;
            var resolved = map.TargetMethods[i];
            return target.IsGenericMethod
                ? resolved.MakeGenericMethod(target.GetGenericArguments())
                : resolved;
        }
        throw NotConstant(
            caller,
            instruction,
            $"calls '{FrontendDisplay.Format(target)}', which " +
            $"'{FrontendDisplay.Format(constrainedType)}' does not implement");
    }

    #endregion

    #region Values

    /// <summary>
    /// Creates the initial values of all locals.
    /// </summary>
    private static object?[] CreateLocals(MethodBase method)
    {
        var variables = method.GetMethodBody()?.LocalVariables;
        if (variables is null)
            return [];
        var locals = new object?[variables.Count];
        for (int i = 0; i < locals.Length; ++i)
            locals[i] = GetDefault(variables[i].LocalType);
        return locals;
    }

    /// <summary>
    /// Returns the type of a local.
    /// </summary>
    private static Type GetLocalType(MethodBase method, int index) =>
        method.GetMethodBody().AsNotNull().LocalVariables[index].LocalType;

    /// <summary>
    /// Returns the default stack value of a type.
    /// </summary>
    private static object? GetDefault(Type type)
    {
        if (type.IsEnum)
            type = type.GetEnumUnderlyingType();
        if (type == typeof(long) || type == typeof(ulong) ||
            type == typeof(nint) || type == typeof(nuint))
        {
            return 0L;
        }
        if (type == typeof(float))
            return 0.0f;
        if (type == typeof(double))
            return 0.0;
        if (type.IsPrimitive)
            return 0;
        return type.IsValueType ? OpaqueStruct.Instance : null;
    }

    /// <summary>
    /// Converts a CLR value (a method result or a folded reflection result) into
    /// its evaluation-stack representation.
    /// </summary>
    private static object? ToStackValue(object? value) =>
        value switch
        {
            null => null,
            bool boolean => boolean ? 1 : 0,
            sbyte or byte or short or ushort or char or int => System.Convert.ToInt32(
                value,
                CultureInfo.InvariantCulture),
            uint unsignedInt => unchecked((int)unsignedInt),
            long longValue => longValue,
            ulong unsignedLong => unchecked((long)unsignedLong),
            nint native => (long)native,
            nuint native => unchecked((long)(ulong)native),
            float single => single,
            double doubleValue => doubleValue,
            Enum enumValue => ToStackValue(System.Convert.ChangeType(
                enumValue,
                enumValue.GetTypeCode(),
                CultureInfo.InvariantCulture)),
            _ => value,
        };

    /// <summary>
    /// Narrows a stack value to the representation of <paramref name="type"/>, as
    /// storing it to a location of that type would.
    /// </summary>
    private static object? Narrow(object? value, Type type)
    {
        if (type.IsEnum)
            type = type.GetEnumUnderlyingType();
        return value switch
        {
            int i when type == typeof(bool) => i != 0 ? 1 : 0,
            int i when type == typeof(sbyte) => (int)(sbyte)i,
            int i when type == typeof(byte) => (int)(byte)i,
            int i when type == typeof(short) => (int)(short)i,
            int i when type == typeof(ushort) || type == typeof(char) => (int)(ushort)i,
            int i when type == typeof(long) || type == typeof(nint) => (long)i,
            int i when type == typeof(ulong) || type == typeof(nuint) => (long)(uint)i,
            long l when type == typeof(int) || type == typeof(uint) => (int)l,
            double d when type == typeof(float) => (float)d,
            float f when type == typeof(double) => (double)f,
            _ => value,
        };
    }

    /// <summary>
    /// Converts the returned stack value into a CLR value of the return type.
    /// </summary>
    private static object ToReturnValue(MethodBase method, Type returnType, object? value)
    {
        var type = returnType.IsEnum ? returnType.GetEnumUnderlyingType() : returnType;
        object? result = value switch
        {
            int i when type == typeof(bool) => i != 0,
            int i when type == typeof(sbyte) => (sbyte)i,
            int i when type == typeof(byte) => (byte)i,
            int i when type == typeof(short) => (short)i,
            int i when type == typeof(ushort) => (ushort)i,
            int i when type == typeof(char) => (char)i,
            int i when type == typeof(int) => i,
            int i when type == typeof(uint) => unchecked((uint)i),
            int i when type == typeof(long) => (long)i,
            int i when type == typeof(ulong) => (ulong)(uint)i,
            long l when type == typeof(long) => l,
            long l when type == typeof(ulong) => unchecked((ulong)l),
            long l when type == typeof(int) => (int)l,
            long l when type == typeof(uint) => unchecked((uint)l),
            float f when type == typeof(float) => f,
            float f when type == typeof(double) => (double)f,
            double d when type == typeof(float) => (float)d,
            double d when type == typeof(double) => d,
            Type t when type == typeof(Type) => t,
            _ => null,
        };
        if (result is null)
            throw NotConstant(method, $"returns a value that is not a constant");
        return returnType.IsEnum ? Enum.ToObject(returnType, result) : result;
    }

    /// <summary>
    /// Returns the basic value type of a stack value, for <c>conv.r.un</c>.
    /// </summary>
    private static BasicValueType GetBasicValueType(object? value) =>
        value switch
        {
            int => BasicValueType.Int32,
            long => BasicValueType.Int64,
            float => BasicValueType.Float32,
            double => BasicValueType.Float64,
            _ => BasicValueType.None,
        };

    /// <summary>
    /// Loads an init-only or literal static field.
    /// </summary>
    private static object? LoadStaticField(
        MethodBase method,
        ILInstruction instruction,
        FieldInfo field)
    {
        if (!field.IsInitOnly && !field.IsLiteral)
        {
            throw NotConstant(
                method,
                instruction,
                $"reads the mutable static field '{field.Name}'");
        }

        var fieldType = field.FieldType;
        if (!fieldType.IsPrimitive && !fieldType.IsEnum && fieldType != typeof(Type))
        {
            throw NotConstant(
                method,
                instruction,
                $"reads the static field '{field.Name}' of type " +
                $"'{FrontendDisplay.Format(fieldType)}'");
        }

        object? value;
        try
        {
            // Runs the static constructor in this process, as code
            // generation does for init-only fields
            value = field.GetValue(null);
        }
        catch (Exception)
        {
            throw NotConstant(
                method,
                instruction,
                $"reads '{field.Name}', whose static constructor failed");
        }

        return value switch
        {
            Type or null => value,
            _ when field.FieldType.IsPrimitive || field.FieldType.IsEnum =>
                ToStackValue(value),
            _ => throw NotConstant(
                method,
                instruction,
                $"reads the static field '{field.Name}' of type " +
                $"'{FrontendDisplay.Format(field.FieldType)}'"),
        };
    }

    #endregion

    #region Operations

    /// <summary>
    /// Returns the index of the instruction a branch target refers to.
    /// </summary>
    private static int Jump(
        Dictionary<int, int> offsets,
        ILInstruction instruction,
        int targetIndex,
        MethodBase method)
    {
        var targets = instruction.GetArgumentAs<ILInstructionBranchTargets>();
        int offset = targets.GetTargetOffsets()[targetIndex];
        return offsets.TryGetValue(offset, out int index)
            ? index
            : throw NotConstant(method, instruction, "branches to an unknown offset");
    }

    /// <summary>
    /// Returns whether a stack value counts as true for <c>brtrue</c>.
    /// </summary>
    private static bool IsTrue(
        MethodBase method,
        ILInstruction instruction,
        object? value) =>
        value switch
        {
            null => false,
            int i => i != 0,
            long l => l != 0,
            Type or Boxed => true,
            _ => throw NotConstant(
                method,
                instruction,
                "tests a value that is not known"),
        };

    /// <summary>
    /// The ordering relations of the comparing instructions.
    /// </summary>
    private enum Relation
    {
        Less,
        LessEqual,
        Greater,
        GreaterEqual,
    }

    /// <summary>
    /// Evaluates the condition of a comparing branch. The <c>.un</c> forms compare
    /// integers unsigned and are true for unordered floats.
    /// </summary>
    private static bool CompareBranch(
        MethodBase method,
        ILInstruction instruction,
        object? left,
        object? right)
    {
        bool un = instruction.HasFlags(ILInstructionFlags.Unsigned);
        return instruction.InstructionType switch
        {
            ILInstructionType.Beq => CompareEqual(method, instruction, left, right),
            ILInstructionType.Bne => !CompareEqual(method, instruction, left, right),
            ILInstructionType.Bge => Relate(
                method, instruction, left, right, Relation.GreaterEqual, un),
            ILInstructionType.Bgt => Relate(
                method, instruction, left, right, Relation.Greater, un),
            ILInstructionType.Ble => Relate(
                method, instruction, left, right, Relation.LessEqual, un),
            ILInstructionType.Blt => Relate(
                method, instruction, left, right, Relation.Less, un),
            _ => throw NotConstant(method, instruction, "compares unsupported values"),
        };
    }

    /// <summary>
    /// Evaluates <c>ceq</c>, <c>cgt</c> and <c>clt</c> (and their <c>.un</c>
    /// forms).
    /// </summary>
    private static bool Compare(
        MethodBase method,
        ILInstruction instruction,
        object? left,
        object? right)
    {
        bool un = instruction.HasFlags(ILInstructionFlags.Unsigned);
        return instruction.InstructionType switch
        {
            ILInstructionType.Ceq => CompareEqual(method, instruction, left, right),
            ILInstructionType.Cgt => Relate(
                method, instruction, left, right, Relation.Greater, un),
            _ => Relate(method, instruction, left, right, Relation.Less, un),
        };
    }

    /// <summary>
    /// Compares two stack values for equality. Floats compare ordered: NaN is not
    /// equal to anything.
    /// </summary>
    private static bool CompareEqual(
        MethodBase method,
        ILInstruction instruction,
        object? left,
        object? right) =>
        (left, right) switch
        {
            (int a, int b) => a == b,
            (int or long, int or long) => ToLong(left) == ToLong(right),
            (float or double, float or double) => ToDouble(left) == ToDouble(right),
            (null, null) => true,
            (Type a, Type b) => a.Equals(b),
            (Type, null) or (null, Type) => false,
            (Boxed, null) or (null, Boxed) => false,
            (Boxed a, Boxed b) => ReferenceEquals(a, b),
            _ => throw NotConstant(method, instruction, "compares unsupported values"),
        };

    /// <summary>
    /// Evaluates an ordering comparison. With <paramref name="un"/>, integers are
    /// compared unsigned and the result is true if a float operand is NaN;
    /// without it, integers are compared signed and NaN makes the result false.
    /// References only compare against null (<c>cgt.un</c> is the null test).
    /// </summary>
    private static bool Relate(
        MethodBase method,
        ILInstruction instruction,
        object? left,
        object? right,
        Relation relation,
        bool un)
    {
        int order;
        switch (left, right)
        {
            case (int a, int b):
                order = un ? ((uint)a).CompareTo((uint)b) : a.CompareTo(b);
                break;
            case (int or long, int or long):
                order = un
                    ? ((ulong)ToLong(left)).CompareTo((ulong)ToLong(right))
                    : ToLong(left).CompareTo(ToLong(right));
                break;
            case (float or double, float or double):
                double x = ToDouble(left), y = ToDouble(right);
                if (double.IsNaN(x) || double.IsNaN(y))
                    return un;
                order = x.CompareTo(y);
                break;
            case (null or Boxed or Type, null or Boxed or Type)
                when un && (left is null || right is null):
                // References as addresses: null is 0, any object is greater
                order = (left is null ? 0 : 1).CompareTo(right is null ? 0 : 1);
                break;
            default:
                throw NotConstant(method, instruction, "compares unsupported values");
        }

        return relation switch
        {
            Relation.Less => order < 0,
            Relation.LessEqual => order <= 0,
            Relation.Greater => order > 0,
            _ => order >= 0,
        };
    }

    /// <summary>
    /// Widens an integer stack value to <see cref="long"/>.
    /// </summary>
    private static long ToLong(object? value) =>
        value is int i ? i : (long)value!;

    /// <summary>
    /// Widens a float stack value to <see cref="double"/>, which is exact.
    /// </summary>
    private static double ToDouble(object? value) =>
        value is float f ? f : (double)value!;

    /// <summary>
    /// Evaluates a binary arithmetic or bitwise instruction.
    /// </summary>
    /// <remarks>
    /// Every switch arm is cast to <see cref="object"/>: the stack representation
    /// depends on the operand kind, and a switch expression with arms of different
    /// numeric types would otherwise widen all of them to a common type.
    /// </remarks>
    private static object Binary(
        MethodBase method,
        ILInstruction instruction,
        object? left,
        object? right)
    {
        var kind = instruction.InstructionType;
        bool overflow = instruction.HasFlags(ILInstructionFlags.Overflow);
        bool unsigned = instruction.HasFlags(ILInstructionFlags.Unsigned);
        try
        {
            if (kind is ILInstructionType.Shl or ILInstructionType.Shr)
                return Shift(method, instruction, left, right, unsigned);

            return (left, right) switch
            {
                (int a, int b) => (object)BinaryInt(kind, overflow, unsigned, a, b),
                (int or long, int or long) => (object)BinaryLong(
                    kind,
                    overflow,
                    unsigned,
                    ToLong(left),
                    ToLong(right)),
                (float a, float b) => (object)(kind switch
                {
                    ILInstructionType.Add => a + b,
                    ILInstructionType.Sub => a - b,
                    ILInstructionType.Mul => a * b,
                    ILInstructionType.Div => a / b,
                    ILInstructionType.Rem => a % b,
                    _ => throw NotConstant(
                        method,
                        instruction,
                        "applies a bitwise operation to a float"),
                }),
                (float or double, float or double) => (object)(kind switch
                {
                    ILInstructionType.Add => ToDouble(left) + ToDouble(right),
                    ILInstructionType.Sub => ToDouble(left) - ToDouble(right),
                    ILInstructionType.Mul => ToDouble(left) * ToDouble(right),
                    ILInstructionType.Div => ToDouble(left) / ToDouble(right),
                    ILInstructionType.Rem => ToDouble(left) % ToDouble(right),
                    _ => throw NotConstant(
                        method,
                        instruction,
                        "applies a bitwise operation to a float"),
                }),
                _ => throw NotConstant(
                    method,
                    instruction,
                    "computes with values that are not known"),
            };
        }
        catch (OverflowException)
        {
            throw NotConstant(method, instruction, "overflows");
        }
        catch (DivideByZeroException)
        {
            throw NotConstant(method, instruction, "divides by zero");
        }
    }

    /// <summary>
    /// Evaluates <c>shl</c>, <c>shr</c> and <c>shr.un</c>.
    /// </summary>
    private static object Shift(
        MethodBase method,
        ILInstruction instruction,
        object? value,
        object? amount,
        bool unsigned)
    {
        int shift = amount switch
        {
            int i => i,
            long l => (int)l,
            _ => throw NotConstant(method, instruction, "shifts by a non-integer"),
        };
        bool left = instruction.InstructionType == ILInstructionType.Shl;
        return value switch
        {
            int i => (object)(left
                ? i << shift
                : unsigned ? (int)((uint)i >> shift) : i >> shift),
            long l => (object)(left
                ? l << shift
                : unsigned ? (long)((ulong)l >> shift) : l >> shift),
            _ => throw NotConstant(method, instruction, "shifts a non-integer"),
        };
    }

    /// <summary>
    /// Evaluates a 32-bit integer operation.
    /// </summary>
    private static int BinaryInt(
        ILInstructionType kind,
        bool overflow,
        bool unsigned,
        int a,
        int b)
    {
        uint ua = (uint)a, ub = (uint)b;
        return kind switch
        {
            ILInstructionType.Add when overflow && unsigned => (int)checked(ua + ub),
            ILInstructionType.Add when overflow => checked(a + b),
            ILInstructionType.Add => unchecked(a + b),
            ILInstructionType.Sub when overflow && unsigned => (int)checked(ua - ub),
            ILInstructionType.Sub when overflow => checked(a - b),
            ILInstructionType.Sub => unchecked(a - b),
            ILInstructionType.Mul when overflow && unsigned => (int)checked(ua * ub),
            ILInstructionType.Mul when overflow => checked(a * b),
            ILInstructionType.Mul => unchecked(a * b),
            ILInstructionType.Div when unsigned => (int)(ua / ub),
            ILInstructionType.Div => a == int.MinValue && b == -1
                ? throw new OverflowException()
                : a / b,
            ILInstructionType.Rem when unsigned => (int)(ua % ub),
            ILInstructionType.Rem => a == int.MinValue && b == -1
                ? throw new OverflowException()
                : a % b,
            ILInstructionType.And => a & b,
            ILInstructionType.Or => a | b,
            ILInstructionType.Xor => a ^ b,
            _ => throw new InvalidOperationException(),
        };
    }

    /// <summary>
    /// Evaluates a 64-bit (or native) integer operation.
    /// </summary>
    private static long BinaryLong(
        ILInstructionType kind,
        bool overflow,
        bool unsigned,
        long a,
        long b)
    {
        ulong ua = (ulong)a, ub = (ulong)b;
        return kind switch
        {
            ILInstructionType.Add when overflow && unsigned => (long)checked(ua + ub),
            ILInstructionType.Add when overflow => checked(a + b),
            ILInstructionType.Add => unchecked(a + b),
            ILInstructionType.Sub when overflow && unsigned => (long)checked(ua - ub),
            ILInstructionType.Sub when overflow => checked(a - b),
            ILInstructionType.Sub => unchecked(a - b),
            ILInstructionType.Mul when overflow && unsigned => (long)checked(ua * ub),
            ILInstructionType.Mul when overflow => checked(a * b),
            ILInstructionType.Mul => unchecked(a * b),
            ILInstructionType.Div when unsigned => (long)(ua / ub),
            ILInstructionType.Div => a == long.MinValue && b == -1
                ? throw new OverflowException()
                : a / b,
            ILInstructionType.Rem when unsigned => (long)(ua % ub),
            ILInstructionType.Rem => a == long.MinValue && b == -1
                ? throw new OverflowException()
                : a % b,
            ILInstructionType.And => a & b,
            ILInstructionType.Or => a | b,
            ILInstructionType.Xor => a ^ b,
            _ => throw new InvalidOperationException(),
        };
    }

    /// <summary>
    /// Evaluates <c>neg</c> and <c>not</c>.
    /// </summary>
    private static object Unary(
        MethodBase method,
        ILInstruction instruction,
        object? value)
    {
        bool negate = instruction.InstructionType == ILInstructionType.Neg;
        return value switch
        {
            int i => (object)(negate ? unchecked(-i) : ~i),
            long l => (object)(negate ? unchecked(-l) : ~l),
            float f when negate => (object)(-f),
            double d when negate => (object)(-d),
            _ => throw NotConstant(
                method,
                instruction,
                "negates a value that is not known"),
        };
    }

    /// <summary>
    /// Evaluates a conversion to <paramref name="targetType"/>.
    /// </summary>
    /// <remarks>
    /// The <c>Unsigned</c> flag means the source is unsigned: for the overflow
    /// checking forms (<c>conv.ovf.*.un</c>) and for <c>conv.r.un</c>.
    /// <c>conv.u8</c> and <c>conv.u</c> zero-extend a 32-bit source.
    /// </remarks>
    private static object Convert(
        MethodBase method,
        ILInstruction instruction,
        object? value,
        Type targetType)
    {
        bool overflow = instruction.HasFlags(ILInstructionFlags.Overflow);
        bool unsigned = instruction.HasFlags(ILInstructionFlags.Unsigned);
        try
        {
            switch (value)
            {
                case float f:
                    return ConvertFloat(f, targetType, overflow);
                case double d:
                    return ConvertFloat(d, targetType, overflow);
                case int i:
                    return ConvertInteger(
                        i, (uint)i, true, targetType, overflow, unsigned);
                case long l:
                    return ConvertInteger(
                        l, (ulong)l, false, targetType, overflow, unsigned);
                default:
                    throw NotConstant(
                        method,
                        instruction,
                        "converts a value that is not known");
            }
        }
        catch (OverflowException)
        {
            throw NotConstant(method, instruction, "overflows in a conversion");
        }
    }

    /// <summary>
    /// Converts an integer to <paramref name="targetType"/> and returns the stack
    /// representation.
    /// </summary>
    /// <param name="signed">The source, sign-extended.</param>
    /// <param name="unsignedValue">The source, zero-extended.</param>
    /// <param name="isInt32">True if the source is a 32-bit integer.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="overflow">True for the overflow checking forms.</param>
    /// <param name="unsigned">True if the source is treated as unsigned.</param>
    private static object ConvertInteger(
        long signed,
        ulong unsignedValue,
        bool isInt32,
        Type targetType,
        bool overflow,
        bool unsigned)
    {
        if (targetType == typeof(float))
            return unsigned ? (float)unsignedValue : (float)signed;
        if (targetType == typeof(double))
            return unsigned ? (double)unsignedValue : (double)signed;

        if (overflow && unsigned)
        {
            return targetType switch
            {
                _ when targetType == typeof(sbyte) =>
                    (object)(int)checked((sbyte)unsignedValue),
                _ when targetType == typeof(byte) =>
                    (int)checked((byte)unsignedValue),
                _ when targetType == typeof(short) =>
                    (int)checked((short)unsignedValue),
                _ when targetType == typeof(ushort) =>
                    (int)checked((ushort)unsignedValue),
                _ when targetType == typeof(int) =>
                    checked((int)unsignedValue),
                _ when targetType == typeof(uint) =>
                    (int)checked((uint)unsignedValue),
                _ when targetType == typeof(long) || targetType == typeof(nint) =>
                    checked((long)unsignedValue),
                _ => (object)(long)unsignedValue,
            };
        }

        if (overflow)
        {
            return targetType switch
            {
                _ when targetType == typeof(sbyte) => (object)(int)checked((sbyte)signed),
                _ when targetType == typeof(byte) => (int)checked((byte)signed),
                _ when targetType == typeof(short) => (int)checked((short)signed),
                _ when targetType == typeof(ushort) => (int)checked((ushort)signed),
                _ when targetType == typeof(int) => checked((int)signed),
                _ when targetType == typeof(uint) => (int)checked((uint)signed),
                _ when targetType == typeof(ulong) || targetType == typeof(nuint) =>
                    (object)(long)checked((ulong)signed),
                _ => (object)signed,
            };
        }

        return targetType switch
        {
            _ when targetType == typeof(sbyte) => (object)(int)unchecked((sbyte)signed),
            _ when targetType == typeof(byte) => (int)unchecked((byte)signed),
            _ when targetType == typeof(short) => (int)unchecked((short)signed),
            _ when targetType == typeof(ushort) => (int)unchecked((ushort)signed),
            _ when targetType == typeof(int) || targetType == typeof(uint) =>
                unchecked((int)signed),
            _ when targetType == typeof(ulong) || targetType == typeof(nuint) ||
                unsigned && targetType == Disassembler.NativePtrType =>
                (object)(isInt32 ? (long)unsignedValue : signed),
            _ => (object)signed,
        };
    }

    /// <summary>
    /// Converts a float to <paramref name="targetType"/> and returns the stack
    /// representation.
    /// </summary>
    private static object ConvertFloat(double value, Type targetType, bool overflow)
    {
        if (targetType == typeof(float))
            return (float)value;
        if (targetType == typeof(double))
            return value;

        // Out-of-range and NaN conversions to integers are unspecified in IL
        // without an overflow check, and throw with one; fold neither
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new OverflowException();
        if (overflow)
        {
            return targetType switch
            {
                _ when targetType == typeof(sbyte) => (object)(int)checked((sbyte)value),
                _ when targetType == typeof(byte) => (int)checked((byte)value),
                _ when targetType == typeof(short) => (int)checked((short)value),
                _ when targetType == typeof(ushort) => (int)checked((ushort)value),
                _ when targetType == typeof(int) => checked((int)value),
                _ when targetType == typeof(uint) => (int)checked((uint)value),
                _ when targetType == typeof(ulong) || targetType == typeof(nuint) =>
                    (object)(long)checked((ulong)value),
                _ => (object)checked((long)value),
            };
        }

        // Only fold values that fit the target type: the device may saturate
        // where the CLR wraps, so out-of-range results are left to run time
        double truncated = Math.Truncate(value);
        var (min, max) = GetIntegerRange(targetType);
        if (truncated < min || truncated > max)
            throw new OverflowException();
        long integer = targetType == typeof(ulong) || targetType == typeof(nuint)
            ? (long)(ulong)truncated
            : (long)truncated;
        return ConvertInteger(integer, (ulong)integer, false, targetType, false, false);
    }

    /// <summary>
    /// Returns the range of an integer type as doubles.
    /// </summary>
    private static (double Min, double Max) GetIntegerRange(Type type) =>
        type switch
        {
            _ when type == typeof(sbyte) => (sbyte.MinValue, sbyte.MaxValue),
            _ when type == typeof(byte) => (byte.MinValue, byte.MaxValue),
            _ when type == typeof(short) => (short.MinValue, short.MaxValue),
            _ when type == typeof(ushort) => (ushort.MinValue, ushort.MaxValue),
            _ when type == typeof(int) => (int.MinValue, int.MaxValue),
            _ when type == typeof(uint) => (uint.MinValue, uint.MaxValue),
            _ when type == typeof(ulong) || type == typeof(nuint) =>
                (0.0, 18446744073709549568.0),
            _ => (-9223372036854775808.0, 9223372036854774784.0),
        };

    #endregion

    #region Errors

    /// <summary>
    /// Creates a non-constant signal naming the method.
    /// </summary>
    private static NotConstantException NotConstant(MethodBase method, string reason) =>
        new($"'{FrontendDisplay.Format(method)}' {reason}");

    /// <summary>
    /// Creates a non-constant signal naming the method and the IL offset.
    /// </summary>
    private static NotConstantException NotConstant(
        MethodBase method,
        ILInstruction instruction,
        string reason) =>
        new(string.Format(
            CultureInfo.InvariantCulture,
            "'{0}' {1} at IL_{2:X4}",
            FrontendDisplay.Format(method),
            reason,
            instruction.Offset));

    #endregion
}

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayBufferVerifier.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Roslyn.Analysis;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ILGPUC.Backends;

/// <summary>
/// Rejects the back buffers of arrays that the calls of a helper method would
/// share. Every allocation site of an array has a single buffer per thread, and
/// every inlined copy of a helper has one of its own (see
/// <see cref="Global.IsAllocatedPerCopy"/>). A helper that is not inlined uses
/// the same buffer in every call, so that two arrays it returns would be the
/// same array.
/// </summary>
/// <remarks>
/// A buffer is rejected in a method that can be called more than once per thread
/// if it escapes the call: if the method returns it or stores it to memory,
/// directly, through the methods it calls or as a part of a view or a structure.
/// It is also rejected in a method that calls itself, directly or through other
/// methods, if it is accessed after such a call, which uses the same buffer.
/// Buffers that only live during a call are not rejected, and neither are the
/// buffers of a method called once per thread. The check runs on the final
/// module, after optimization has inlined what it can and has promoted the
/// buffers that it can to registers.
/// </remarks>
static partial class ArrayBufferVerifier
{
    /// <summary>
    /// The number of calls of a method per thread that count as several calls.
    /// </summary>
    private const int Several = 2;

    /// <summary>
    /// How a value escapes a method.
    /// </summary>
    /// <param name="Stored">True if it is stored to memory.</param>
    /// <param name="Returned">True if it is returned.</param>
    private readonly record struct Escape(bool Stored, bool Returned);

    /// <summary>
    /// The state of the verification of a module.
    /// </summary>
    private sealed class Context
    {
        /// <summary>
        /// The methods of all call cycles.
        /// </summary>
        public HashSet<Method> Recursive { get; } = [];

        /// <summary>
        /// The calling methods of every method, once per call.
        /// </summary>
        public Dictionary<Method, List<Method>> Callers { get; } = [];

        /// <summary>
        /// The number of calls of every method per thread, at most
        /// <see cref="Several"/>.
        /// </summary>
        public Dictionary<Method, int> NumCalls { get; } = [];

        /// <summary>
        /// How the parameters of methods escape them.
        /// </summary>
        public Dictionary<(Method, int), Escape> Parameters { get; } = [];

        /// <summary>
        /// The values of every method, including pure values.
        /// </summary>
        public Dictionary<Method, List<Value>> Values { get; } = [];
    }

    /// <summary>
    /// Throws if a helper method of the given module shares the buffer of an
    /// array between its calls.
    /// </summary>
    /// <param name="module">The final module after all transformations.</param>
    public static void Verify(Module module)
    {
        var context = new Context();
        foreach (var cycle in CallCycle.FindAll(module.Methods.ToArray()))
            context.Recursive.UnionWith(cycle.Methods);
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation)
                continue;
            method.ForEachValue<MethodCall>(call =>
            {
                if (!context.Callers.TryGetValue(call.Target, out var callers))
                    context.Callers.Add(call.Target, callers = []);
                callers.Add(method);
            });
        }

        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation || method == module.EntryPoint)
                continue;
            bool recursive = context.Recursive.Contains(method);
            if (!recursive && GetNumCalls(context, module, method) < Several)
                continue;
            foreach (var value in GetValues(context, method))
            {
                if (value is Global { IsAllocatedPerCopy: true } global)
                    VerifyBuffer(context, method, global, recursive);
            }
        }

        foreach (var method in module.Methods)
        {
            if (method.HasImplementation)
                VerifyLoops(context, method);
        }
    }

    /// <summary>
    /// Throws if the given buffer escapes the given method, which can be called
    /// several times per thread, or if it is accessed after a recursive call.
    /// </summary>
    private static void VerifyBuffer(
        Context context,
        Method method,
        Global global,
        bool recursive)
    {
        var derived = new HashSet<Value> { global };
        var escape = Trace(context, method, derived);
        string? message = null;
        if (escape.Stored || escape.Returned)
            message = ErrorMessages.KernelDiagnosticArrayReturnedFromCalls;
        else if (recursive && IsAccessedAfterRecursiveCall(context, method, derived))
            message = ErrorMessages.KernelDiagnosticArrayInRecursion;
        if (message is null)
            return;

        throw new KernelCompilationException(
            KernelDiagnostic.UnsupportedConstructId,
            string.Format(
                CultureInfo.InvariantCulture,
                message,
                GetDisplayName(method)),
            global.Location);
    }

    /// <summary>
    /// Returns the number of calls of the given method per thread, at most
    /// <see cref="Several"/>: a kernel is called once, a method of a call cycle
    /// several times, and other methods as often as all their calls together.
    /// A method called in a loop counts once, like an array allocated in a
    /// loop, which is the same array in every iteration.
    /// </summary>
    private static int GetNumCalls(Context context, Module module, Method method)
    {
        if (method == module.EntryPoint)
            return 1;
        if (context.Recursive.Contains(method))
            return Several;
        if (context.NumCalls.TryGetValue(method, out int numCalls))
            return numCalls;

        // Methods outside of call cycles form a directed acyclic graph
        numCalls = 0;
        if (context.Callers.TryGetValue(method, out var callers))
        {
            foreach (var caller in callers)
            {
                numCalls += GetNumCalls(context, module, caller);
                if (numCalls >= Several)
                    break;
            }
        }
        numCalls = System.Math.Min(numCalls, Several);
        context.NumCalls.Add(method, numCalls);
        return numCalls;
    }

    /// <summary>
    /// Returns all values of the given method: the values of its blocks, the
    /// values they return and the pure values and globals they use.
    /// </summary>
    private static List<Value> GetValues(Context context, Method method)
    {
        if (context.Values.TryGetValue(method, out var values))
            return values;

        values = [];
        var visited = new HashSet<Value>();
        var worklist = new Stack<Value>();
        foreach (var block in method.Blocks)
        {
            foreach (var value in block)
                worklist.Push(value);
            if (block.TerminationValue is { } terminationValue)
                worklist.Push(terminationValue);
        }
        while (worklist.TryPop(out var value))
        {
            if (value is Method or TypeValue or BasicBlock || !visited.Add(value))
                continue;
            values.Add(value);
            foreach (var operand in value.Values)
                worklist.Push(operand);
        }
        context.Values.Add(method, values);
        return values;
    }

    /// <summary>
    /// Adds the values of the given method that hold an address derived from the
    /// given values to them, and returns how these values escape the method.
    /// </summary>
    /// <param name="context">The current context.</param>
    /// <param name="method">The method.</param>
    /// <param name="derived">
    /// The values whose derived values to find, such as a buffer or a parameter.
    /// </param>
    private static Escape Trace(Context context, Method method, HashSet<Value> derived)
    {
        var values = GetValues(context, method);
        bool stored = false;
        for (bool changed = true; changed;)
        {
            changed = false;
            foreach (var value in values)
            {
                if (derived.Contains(value))
                    continue;
                switch (value)
                {
                    case Load:
                        // A loaded value does not derive from its address
                        continue;
                    case Store store:
                        stored |= derived.Contains(store.Value);
                        continue;
                    case MethodCall call:
                        changed |= TraceCall(context, call, derived, ref stored);
                        continue;
                }
                if (!HoldsAddress(value.Type))
                    continue;
                foreach (var operand in value.Values)
                {
                    if (derived.Contains(operand))
                    {
                        derived.Add(value);
                        changed = true;
                        break;
                    }
                }
            }
        }

        bool returned = false;
        foreach (var block in method.Blocks)
        {
            if (block.TerminationKind == BlockTerminationKind.Return &&
                block.TerminationValue is { } returnValue)
            {
                returned |= derived.Contains(returnValue);
            }
        }
        return new(stored, returned);
    }

    /// <summary>
    /// Traces the derived values passed to the given call: the call stores them
    /// if the called method stores the parameters they are passed to, and the
    /// call result is derived from them if it returns these parameters.
    /// </summary>
    /// <returns>True if the call result is now a derived value.</returns>
    private static bool TraceCall(
        Context context,
        MethodCall call,
        HashSet<Value> derived,
        ref bool stored)
    {
        var target = call.Target;
        if (!target.HasImplementation)
            return false;

        bool returned = false;
        var arguments = call.Arguments;
        for (int i = 0; i < arguments.Length; ++i)
        {
            if (!derived.Contains(arguments[i]))
                continue;
            var escape = GetParameterEscape(context, target, i);
            stored |= escape.Stored;
            returned |= escape.Returned;
        }
        return returned && HoldsAddress(call.Type) && derived.Add(call);
    }

    /// <summary>
    /// Returns how the given parameter escapes its method. A parameter of a
    /// method whose analysis is in progress, as in a call cycle, does not
    /// escape through the calls of the cycle.
    /// </summary>
    private static Escape GetParameterEscape(Context context, Method method, int index)
    {
        if (context.Parameters.TryGetValue((method, index), out var escape))
            return escape;
        context.Parameters.Add((method, index), default);

        var derived = new HashSet<Value> { method.Parameters[index] };
        escape = Trace(context, method, derived);
        context.Parameters[(method, index)] = escape;
        return escape;
    }

    /// <summary>
    /// Returns true if a value of a block that uses one of the given derived
    /// values follows a call of a method of the call cycle of the given method,
    /// in the same block or in a block reachable from it, also through a loop.
    /// </summary>
    private static bool IsAccessedAfterRecursiveCall(
        Context context,
        Method method,
        HashSet<Value> derived)
    {
        var reachable = new HashSet<BasicBlock>();
        var worklist = new Stack<BasicBlock>();
        foreach (var block in method.Blocks)
        {
            bool afterCall = false;
            foreach (var (value, _) in block)
            {
                if (afterCall && UsesAny(value, derived))
                    return true;
                if (value is MethodCall call &&
                    context.Recursive.Contains(call.Target))
                {
                    afterCall = true;
                }
            }
            if (afterCall)
            {
                if (block.TerminationValue is { } terminationValue &&
                    derived.Contains(terminationValue))
                {
                    return true;
                }
                foreach (var successor in block.Successors)
                    worklist.Push(successor);
            }
        }

        while (worklist.TryPop(out var block))
        {
            if (!reachable.Add(block))
                continue;
            foreach (var (value, _) in block)
            {
                if (UsesAny(value, derived))
                    return true;
            }
            if (block.TerminationValue is { } terminationValue &&
                derived.Contains(terminationValue))
            {
                return true;
            }
            foreach (var successor in block.Successors)
                worklist.Push(successor);
        }
        return false;
    }

    /// <summary>
    /// Returns true if the given value uses one of the given values.
    /// </summary>
    private static bool UsesAny(Value value, HashSet<Value> values)
    {
        foreach (var operand in value.Values)
        {
            if (values.Contains(operand))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if values of the given type can hold an address: pointers,
    /// views, arrays and structures with such fields.
    /// </summary>
    private static bool HoldsAddress(TypeValue type)
    {
        switch (type)
        {
            case AddressSpaceType or ArrayType:
                return true;
            case StructureType structureType:
                foreach (var field in structureType.Fields)
                {
                    if (HoldsAddress(field))
                        return true;
                }
                return false;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns the user-facing name of a method.
    /// </summary>
    private static string GetDisplayName(Method method) =>
        method.Source is { } source
            ? $"{source.DeclaringType?.Name}.{source.Name}".TrimStart('.')
            : method.Name;
}

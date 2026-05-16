// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FixPointAnalysis.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Represents an analysis result of a <see cref="FixPointAnalysis{T, TDirection}"/>.
/// </summary>
/// <typeparam name="T">The analysis value type.</typeparam>
/// <param name="results">The results mapping.</param>
/// <param name="defaultValue">The default value to use.</param>
readonly struct FixPointAnalysisResult<T>(
    GlobalValueMap<AnalysisValue<T>>? results,
    T defaultValue)
    where T : struct, IEquatable<T>
{
    /// <summary>
    /// Returns an analysis value for the given IR value.
    /// </summary>
    /// <param name="value">The value to get analysis information for.</param>
    /// <returns>Analysis result for the given value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public AnalysisValue<T> GetResult(Value value)
    {
        if (!results.HasValue)
            return AnalysisValue.Create(defaultValue, value.Type);
        if (results.Value.TryGetValue(value, out var result))
            return result;
        return AnalysisValue.Create(defaultValue, value.Type);
    }

    /// <summary>
    /// Tries to get an analysis result from this mapping.
    /// </summary>
    /// <param name="value">The value to get the result for.</param>
    /// <param name="result">The determined result.</param>
    /// <returns>True if the result could have been determined.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetResult(Value value, out AnalysisValue<T> result)
    {
        result = AnalysisValue.Create(defaultValue, value.Type);
        if (!results.HasValue || !results.Value.TryGetValue(value, out var tempResult))
            return false;
        result = tempResult;
        return true;
    }

    /// <summary>
    /// Returns true if this alignment information object is empty.
    /// </summary>
    public bool IsEmpty => results is null;
}

/// <summary>
/// A fix point analysis that computes data flow information for values.
/// </summary>
/// <typeparam name="T">The data type computed per value.</typeparam>
/// <typeparam name="TDirection">The control-flow direction.</typeparam>
/// <param name="defaultValue">The default value for non-analyzed nodes.</param>
abstract class FixPointAnalysis<T, TDirection>(T defaultValue = default)
    where T : struct, IEquatable<T>
    where TDirection : struct, IControlFlowDirection
{
    /// <summary>
    /// Returns the default analysis value for generic IR nodes.
    /// </summary>
    public T DefaultValue { get; } = defaultValue;

    /// <summary>
    /// Computes the analysis value for a given IR value.
    /// </summary>
    /// <param name="value">The IR value to analyze.</param>
    /// <param name="data">Mapping from values to their analysis data.</param>
    /// <returns>The computed analysis value.</returns>
    protected abstract AnalysisValue<T> Analyze(
        Value value,
        GlobalValueMap<AnalysisValue<T>> data);

    /// <summary>
    /// Tries to provide a specialized analysis value for a given type.
    /// </summary>
    /// <param name="type">The type to provide a value for.</param>
    /// <returns>A specialized analysis value, or null to use the default.</returns>
    protected virtual AnalysisValue<T>? TryProvideForType(TypeValue type) => null;

    /// <summary>
    /// Creates an initial analysis value for the given type.
    /// </summary>
    protected AnalysisValue<T> CreateInitialValue(TypeValue type)
    {
        var provided = TryProvideForType(type);
        return provided ?? AnalysisValue.Create(DefaultValue, type);
    }

    /// <summary>
    /// Merges two data values.
    /// </summary>
    protected abstract T Merge(T first, T second);

    /// <summary>
    /// Merges two analysis values element-wise.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected AnalysisValue<T> Merge(AnalysisValue<T> first, AnalysisValue<T> second)
    {
        if (first.IsScalar || first.NumFields != second.NumFields)
            return new AnalysisValue<T>(Merge(first.Data, second.Data));

        var fieldData = new T[first.NumFields];
        for (int i = 0; i < first.NumFields; i++)
            fieldData[i] = Merge(first[i], second[i]);

        return new AnalysisValue<T>(
            Merge(first.Data, second.Data),
            fieldData);
    }

    /// <summary>
    /// Computes merged uniform information of the given <see cref="PhiValue"/> node.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected AnalysisValue<T> MergePhiValue(
        PhiValue phiValue,
        GlobalValueMap<AnalysisValue<T>> valueData)
    {
        if (phiValue.NumArguments == 0)
            return CreateInitialValue(phiValue.Type);

        // Start with the first value
        var result = valueData.TryGetValue(phiValue.Arguments[0], out var firstValue)
            ? firstValue
            : CreateInitialValue(phiValue.Type);

        // Merge with other phi arguments
        for (int i = 1; i < phiValue.NumArguments; ++i)
        {
            var operandValue = valueData.TryGetValue(phiValue.Arguments[i], out var opValue)
                ? opValue
                : CreateInitialValue(phiValue.Type);
            result = Merge(result, operandValue);
        }

        return result;
    }

    /// <summary>
    /// Analyzes a single method with optional initial value mappings.
    /// </summary>
    /// <typeparam name="TOrder">The traversal order.</typeparam>
    /// <typeparam name="TBlockDirection">The block direction.</typeparam>
    /// <param name="blocks">The blocks to analyze.</param>
    /// <param name="initialValueData">
    /// The value mapping to use and update. If not provided, a new mapping is created
    /// with default values for all IR values. Pass a pre-populated mapping to provide
    /// custom initial values (e.g., for parameters).
    /// </param>
    /// <param name="onCalleeChange">
    /// Optional callback invoked when a called method's parameters change.
    /// </param>
    /// <returns>The computed value mapping and method return value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected FixPointAnalysisResult<T> AnalyzeMethod<TOrder, TBlockDirection>(
        in BasicBlockCollection<TOrder, TBlockDirection> blocks,
        GlobalValueMap<AnalysisValue<T>>? initialValueData = null,
        Action<Method>? onCalleeChange = null)
        where TOrder : struct, ITraversalOrder<BasicBlock>
        where TBlockDirection : struct, IControlFlowDirection
    {
        var method = blocks.Method;
        var valueData = initialValueData ??
            method.Module.CreateGlobalMap<AnalysisValue<T>>();

        // Initialize all values that don't have data yet
        method.ForEachValue<Value<Method>>(value =>
        {
            if (!valueData.ContainsKey(value))
                valueData[value] = CreateInitialValue(value.Type);
        });

        // Initialize method return value
        valueData[method] = method.IsVoid
            ? default
            : CreateInitialValue(method.Type);

        // Work-list algorithm
        var onStack = method.CreateSet<BasicBlock>();
        var stack = new Stack<BasicBlock>(blocks.Count);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        void ProcessBlock(BasicBlock block)
        {
            bool changed = false;

            // Update all values (including terminator)
            foreach (var (bbValue, _) in block)
            {
                var oldValue = valueData[bbValue];
                var newValue = Analyze(bbValue, valueData);
                valueData[bbValue] = Merge(oldValue, newValue);
                changed |= !oldValue.Equals(newValue);

                // For method calls, propagate argument values to parameters
                if (bbValue is not MethodCall call)
                    continue;

                var target = call.Target;
                bool calleeChanged = false;
                var parameters = target.Parameters;
                var arguments = call.Values;

                for (int i = 0; i < Math.Min(parameters.Count, arguments.Length); ++i)
                {
                    var param = parameters[i];
                    var arg = arguments[i];

                    // Get current argument value
                    var argValue = valueData.TryGetValue(arg, out var av)
                        ? av
                        : CreateInitialValue(arg.Type);

                    // Get old parameter value
                    var oldParamValue = valueData.TryGetValue(param, out var pv)
                        ? pv
                        : CreateInitialValue(param.Type);

                    // Merge argument into parameter
                    var newParamValue = Merge(oldParamValue, argValue);
                    valueData[param] = newParamValue;

                    // Track if parameter changed
                    calleeChanged |= oldParamValue.Equals(newParamValue);
                }

                // Notify that the callee needs re-analysis
                if (calleeChanged)
                    onCalleeChange?.Invoke(target);
            }

            if (!changed)
                return;

            // Push successors onto work stack
            foreach (var successor in block.GetSuccessors<TDirection>())
            {
                if (onStack.Add(successor))
                    stack.Push(successor);
            }
        }

        // Initial pass
        foreach (var block in blocks)
            ProcessBlock(block);

        // Fix point iteration
        while (stack.Count > 0)
        {
            var block = stack.Pop();
            onStack.Remove(block);
            ProcessBlock(block);
        }

        return new(valueData, DefaultValue);
    }

    /// <summary>
    /// Analyzes an entire module starting from an entry point.
    /// </summary>
    /// <param name="entryPoint">The entry point method.</param>
    /// <param name="valueData">
    /// Optional pre-populated value mapping for all values (e.g., for parameters).
    /// </param>
    /// <returns>
    /// A mapping from methods to their computed value mappings and return values.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected FixPointAnalysisResult<T> AnalyzeModule(
        Method entryPoint,
        GlobalValueMap<AnalysisValue<T>>? valueData = null)
    {
        var module = entryPoint.Module;
        var callGraph = module.CreateCallGraph<TDirection>();
        var visited = module.CreateSet<Method>();

        var workListSet = module.CreateSet<Method>();
        var workList = new Queue<Method>(module.NumMethods / 2);

        // Initialize global value mapping if not provided
        valueData ??= module.CreateGlobalMap<AnalysisValue<T>>();
        var results = valueData.Value;

        workList.Enqueue(entryPoint);
        visited.Add(entryPoint);

        // Callback to enqueue callees when their parameters change
        void OnCalleeChange(Method callee)
        {
            if (visited.Contains(callee) && !workListSet.Add(callee))
                workList.Enqueue(callee);
        }

        while (workList.Count > 0)
        {
            var method = workList.Dequeue();
            workListSet.Remove(method);
            var node = callGraph[method];

            // Analyze the method with parameter propagation callback
            var valueMapping = AnalyzeMethod(method.Blocks, results, OnCalleeChange);

            // Update return value mapping
            var retValue = valueMapping.GetResult(method);
            var oldRetValue = results.TryGetValue(method, out var prev)
                ? prev : default;
            var mergedRetValue = oldRetValue.Equals(default)
                ? retValue
                : Merge(oldRetValue, retValue);
            results[method] = mergedRetValue;

            // If return value changed, re-analyze callers
            if (!oldRetValue.Equals(mergedRetValue))
            {
                foreach (var caller in node.Callers)
                    OnCalleeChange(caller);
            }

            // Discover and enqueue called methods
            var callees = node.Callees;
            foreach (var callee in callees)
                OnCalleeChange(callee);
        }

        return new(results, DefaultValue);
    }
}

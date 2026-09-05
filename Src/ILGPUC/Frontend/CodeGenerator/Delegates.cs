// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Delegates.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using ILGPUC.Roslyn.Analysis;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    #region Nested Types

    /// <summary>
    /// A delegate handle that is not bound to a single target statically, such
    /// as a delegate chosen by a condition. Calls on it dispatch on its tag (see
    /// <see cref="CreateDelegateTag(Value)"/>).
    /// </summary>
    /// <param name="ParameterIndex">
    /// The index of the delegate parameter the handle is passed to (-1 for an
    /// invoked handle).
    /// </param>
    /// <param name="Handle">The delegate handle.</param>
    /// <param name="Tag">The tag of the handle.</param>
    /// <param name="Targets">
    /// The distinct targets of the handle, or null if they are traced after
    /// code generation of the current method (as the handle depends on loops
    /// that are not closed yet).
    /// </param>
    /// <param name="Closures">
    /// The closure of each target, if all handles referring to it agree on a
    /// single closure value (null for ambiguous closures).
    /// </param>
    private sealed record DelegateSelection(
        int ParameterIndex,
        Value Handle,
        Value Tag,
        List<MethodBase>? Targets,
        Dictionary<MethodBase, Value?> Closures);

    /// <summary>
    /// The delegate arguments of a call.
    /// </summary>
    /// <param name="Targets">
    /// The statically known target of each delegate argument, indexed by
    /// parameter position (null for selected and non-delegate arguments).
    /// </param>
    /// <param name="Selections">
    /// The delegate arguments that are not bound to a single target statically.
    /// </param>
    private sealed record DelegateArguments(
        MethodBase?[] Targets,
        List<DelegateSelection> Selections);

    #endregion

    #region Instance

    /// <summary>
    /// The tag value of each delegate target used in the current method.
    /// </summary>
    private readonly Dictionary<MethodBase, int> _delegateTagValues = [];

    /// <summary>
    /// Maps delegate handles loaded from variables to their tags.
    /// </summary>
    private readonly Dictionary<Value, Value> _delegateTags = [];

    /// <summary>
    /// All variables of a delegate type whose tags are tracked as SSA values.
    /// </summary>
    private readonly HashSet<VariableRef> _delegateVariables = [];

    /// <summary>
    /// The dispatchers to implement once the current method has been
    /// generated and the targets of all delegate handles are known.
    /// </summary>
    private readonly List<Action> _pendingDispatchers = [];

    #endregion

    #region Delegate Tags

    /// <summary>
    /// The tag of null handles and of handles whose targets are unknown, which
    /// differs from the tags of all targets.
    /// </summary>
    private const int NoTargetTag = -1;

    /// <summary>
    /// Returns the variable holding the tag of the given delegate variable.
    /// </summary>
    /// <param name="var">The delegate variable.</param>
    /// <returns>The tag variable.</returns>
    private static VariableRef GetDelegateTagRef(VariableRef var) =>
        new(
            var.Index * 2 + (var.RefType == VariableRefType.Argument ? 0 : 1),
            VariableRefType.DelegateTag);

    /// <summary>
    /// Creates the tag constant of the given delegate target.
    /// </summary>
    /// <param name="target">The delegate target (if any).</param>
    /// <returns>The tag constant.</returns>
    private PrimitiveValue CreateDelegateTag(MethodBase? target)
    {
        int tagValue = NoTargetTag;
        if (target is not null &&
            !_delegateTagValues.TryGetValue(target, out tagValue))
        {
            tagValue = _delegateTagValues.Count;
            _delegateTagValues.Add(target, tagValue);
        }
        return EntryBlock.Builder.CreatePrimitiveValue(Location, tagValue);
    }

    /// <summary>
    /// Sets up the tag of a delegate variable in the entry block.
    /// </summary>
    /// <param name="var">The delegate variable.</param>
    /// <param name="target">The statically known target (if any).</param>
    private void SetupDelegateTag(VariableRef var, MethodBase? target)
    {
        _delegateVariables.Add(var);
        EntryBlock.SetValue(GetDelegateTagRef(var), CreateDelegateTag(target));
    }

    /// <summary>
    /// Records the tag of a delegate handle loaded from the given variable.
    /// </summary>
    /// <param name="var">The loaded variable.</param>
    /// <param name="handle">The loaded handle.</param>
    private void LoadDelegateTag(VariableRef var, Value handle)
    {
        // Tags are only required for handles not bound to a single target
        if (_delegateVariables.Contains(var) &&
            !_delegateTable.ContainsKey(handle) &&
            (!TryTraceDelegateHandle(handle, out var target) || target is null))
        {
            _delegateTags[handle] = Block.GetValue(GetDelegateTagRef(var));
        }
    }

    /// <summary>
    /// Stores the tag of a delegate handle stored to the given variable.
    /// </summary>
    /// <param name="var">The target variable.</param>
    /// <param name="handle">The stored handle.</param>
    /// <param name="target">The statically known target (if any).</param>
    private void StoreDelegateTag(VariableRef var, Value? handle, MethodBase? target)
    {
        if (!_delegateVariables.Contains(var))
            return;
        Block.SetValue(
            GetDelegateTagRef(var),
            target is null && handle is not null
                ? CreateDelegateTag(handle)
                : CreateDelegateTag(target));
    }

    /// <summary>
    /// Creates the tag of a delegate handle: an integer identifying its target,
    /// as the handle itself is a closure pointer that is null for all
    /// non-capturing lambdas. Handles loaded from variables use the tags
    /// tracked alongside the variables, and every phi of handles is mirrored by
    /// a phi of their tags.
    /// </summary>
    /// <param name="handle">The delegate handle.</param>
    /// <returns>The created tag.</returns>
    private Value CreateDelegateTag(Value handle) =>
        CreateDelegateTag(handle, []);

    /// <summary>
    /// Creates the tag of a delegate handle (see above).
    /// </summary>
    /// <param name="handle">The delegate handle.</param>
    /// <param name="tags">The tags created for phi values so far.</param>
    /// <returns>The created tag.</returns>
    private Value CreateDelegateTag(Value handle, Dictionary<Value, Value> tags)
    {
        if (tags.TryGetValue(handle, out var tag) ||
            _delegateTags.TryGetValue(handle, out tag))
        {
            return tag;
        }
        if (_delegateTable.TryGetValue(handle, out var target))
            return CreateDelegateTag(target);
        if (_delegateAliases.TryGetValue(handle, out var source))
            return CreateDelegateTag(source, tags);
        if (handle is PhiValue { NumArguments: 0 })
        {
            // Selections are traced when they are created or after all loops
            // have been closed, so a phi without arguments would leave its tag
            // undefined. C# keeps the evaluation stack empty at loop headers.
            throw Location.GetInvalidOperationException(
                "Delegate tag of an incomplete phi value");
        }
        if (handle is not PhiValue phi)
        {
            // A null or untraceable handle: calls on it are never reached, or
            // they are reported when the handle is traced for the call
            return CreateDelegateTag(target: null);
        }

        // Register the phi first to close cycles of loop-carried selections
        var phiBuilder = MethodBuilder[phi.BasicBlock].CreatePhi(
            phi.Location,
            ModuleBuilder.GetPrimitiveType(BasicValueType.Int32),
            phi.NumArguments);
        tags.Add(handle, phiBuilder.PhiValue);
        for (int i = 0, e = phi.NumArguments; i < e; ++i)
        {
            phiBuilder.AddArgument(
                phi.Sources[i],
                CreateDelegateTag(phi.Arguments[i], tags));
        }
        return phiBuilder.Seal();
    }

    #endregion

    #region Kernel Delegate Bindings

    /// <summary>
    /// Realizes a call to <c>KernelDelegates.Bind</c>, which ILGPUC emits into
    /// the kernels it compiles for each target of a delegate passed to a method
    /// launching a kernel. The handle is bound to the target registered under
    /// the given key in the assembly of the current method, like a delegate
    /// created from a non-capturing lambda.
    /// </summary>
    private void MakeKernelDelegateBinding()
    {
        if (Block.Pop() is not StringValue key)
            throw Location.GetInvalidOperationException();

        var assembly = DisassembledMethod.Method.Module.Assembly;
        var target = KernelDelegateTargets.Resolve(assembly, key.String) ??
            throw Location.GetNotSupportedException(
                ErrorMessages.NotSupportedUnresolvedKernelDelegate,
                key.String,
                assembly.GetName().Name ?? string.Empty);

        var handle = Builder.CreateNull(
            Location,
            ModuleBuilder.CreatePointerType(
                ModuleBuilder.IntPointerType,
                MemoryAddressSpace.Generic));
        _delegateTable[handle] = target;
        Block.Push(handle);
    }

    #endregion

    #region Delegate Selections

    /// <summary>
    /// Creates the selection of a delegate handle that is not bound to a
    /// single target statically. Its targets are traced right away if the
    /// handle does not depend on loops that are still open.
    /// </summary>
    /// <param name="parameterIndex">
    /// The index of the delegate parameter the handle is passed to (-1 for an
    /// invoked handle).
    /// </param>
    /// <param name="handle">The delegate handle.</param>
    /// <returns>The created selection.</returns>
    private DelegateSelection CreateDelegateSelection(int parameterIndex, Value handle)
    {
        var targets = new List<MethodBase>();
        var closures = new Dictionary<MethodBase, Value?>();
        bool traced = TryCollectDelegateTargets(handle, [], targets, closures);
        return new(
            parameterIndex,
            handle,
            CreateDelegateTag(handle),
            traced && targets.Count > 0 ? targets : null,
            closures);
    }

    /// <summary>
    /// Returns the targets of a delegate selection, tracing them if necessary.
    /// </summary>
    /// <param name="selection">The delegate selection.</param>
    /// <param name="targets">The traced targets.</param>
    /// <returns>True if the targets could be traced.</returns>
    private bool TryGetSelectedTargets(
        DelegateSelection selection,
        out List<MethodBase> targets)
    {
        targets = selection.Targets ?? [];
        return selection.Targets is not null ||
            TryCollectDelegateTargets(selection.Handle, [], targets) &&
            targets.Count > 0;
    }

    /// <summary>
    /// Returns the closure to pass to the given target of a traced selection:
    /// the closure value all handles referring to the target agree on, if it is
    /// available at the current block, and the selecting handle otherwise.
    /// </summary>
    /// <param name="selection">The delegate selection.</param>
    /// <param name="target">The selected target.</param>
    /// <param name="type">The type of the closure parameter.</param>
    /// <returns>The closure converted to the given type.</returns>
    private Value GetSelectedClosure(
        DelegateSelection selection,
        MethodBase target,
        TypeValue type)
    {
        var closure = selection.Closures.GetValueOrDefault(target) is { } value &&
            IsAvailable(value)
            ? value
            : selection.Handle;
        return closure.Type == type
            ? closure
            : CreateConversion(closure, type, ConvertFlags.None);
    }

    /// <summary>
    /// Returns true if the given value is available at the current block, as
    /// it is pure or defined in a block dominating the current one.
    /// </summary>
    /// <param name="value">The value to check.</param>
    private bool IsAvailable(Value value)
    {
        if (value is not BasicBlockValue blockValue)
        {
            foreach (var operand in value.Values)
            {
                if (!IsAvailable(operand))
                    return false;
            }
            return true;
        }

        // The defining block dominates the current block if the current block
        // cannot be reached from the entry block without passing it
        var definingBlock = blockValue.BasicBlock;
        var currentBlock = Block.BasicBlock;
        if (definingBlock == currentBlock)
            return true;
        var visited = new HashSet<BasicBlock> { definingBlock };
        var worklist = new Stack<BasicBlock>();
        worklist.Push(EntryBlock.BasicBlock);
        while (worklist.TryPop(out var block))
        {
            if (block == currentBlock)
                return false;
            if (!visited.Add(block))
                continue;
            foreach (var successor in block.Successors)
                worklist.Push(successor);
        }
        return true;
    }

    #endregion

    #region Delegate Calls

    /// <summary>
    /// Resolves the delegate arguments of a call to the given method, which are
    /// still on the evaluation stack. The handles are resolved before they are
    /// converted to the parameter types.
    /// </summary>
    /// <param name="target">The target method to invoke.</param>
    /// <returns>
    /// The delegate arguments, or null if the method has no delegate
    /// parameters.
    /// </returns>
    private DelegateArguments? PeekDelegateArguments(MethodBase target)
    {
        var parameters = target.GetParameters();
        DelegateArguments? arguments = null;
        for (int i = 0, e = parameters.Length; i < e; ++i)
        {
            if (!parameters[i].ParameterType.IsDelegate())
                continue;

            arguments ??= new(new MethodBase?[e], []);
            var handle = Block.GetValue(new VariableRef(
                Block.StackCounter - e + i,
                VariableRefType.Stack));
            if (TryResolveDelegateMethod(handle, out var method))
                arguments.Targets[i] = method;
            else
                arguments.Selections.Add(CreateDelegateSelection(i, handle));
        }
        return arguments;
    }

    /// <summary>
    /// Creates a call to the copies of the given method that are specialized
    /// for its delegate arguments. If delegate arguments are not bound to
    /// single targets statically, the call dispatches on their tags to the
    /// copies specialized for all combinations of their targets.
    /// </summary>
    /// <param name="method">The method to call.</param>
    /// <param name="arguments">The call arguments.</param>
    /// <param name="delegateArguments">The delegate arguments.</param>
    /// <returns>The created call.</returns>
    private MethodCall CreateSpecializedCall(
        MethodBase method,
        ref ValueBuilderList arguments,
        DelegateArguments delegateArguments)
    {
        var selections = delegateArguments.Selections;
        if (selections.Count < 1)
        {
            var specialized = GetSpecializedMethod(method, delegateArguments.Targets);
            return Builder.CreateCall(Location, specialized, ref arguments);
        }

        // Pass the closure of each traced target in place of the handle
        var values = new List<Value>(arguments.AsReadOnlySpan().ToArray());
        int numArguments = values.Count;
        int parameterOffset = method.GetParameterOffset();
        var closureIndices = new Dictionary<(int, MethodBase), int>();
        foreach (var selection in selections)
        {
            if (selection.Targets is null)
                continue;
            int index = selection.ParameterIndex + parameterOffset;
            var type = values[index].Type;
            foreach (var target in selection.Targets)
            {
                closureIndices.Add((index, target), values.Count);
                values.Add(GetSelectedClosure(selection, target, type));
            }

            // The handle itself is not passed on
            values[index] = Builder.CreateNull(Location, type);
        }

        var location = Location;
        return CreateDispatch(
            ModuleBuilder.CreateType(method.GetReturnType()),
            method,
            selections,
            values,
            (targets, builder, parameters) =>
            {
                var caseTargets = (MethodBase?[])delegateArguments.Targets.Clone();
                var callArguments = ValueBuilderList.Create(
                    builder.Generation,
                    numArguments);
                for (int i = 0; i < numArguments; ++i)
                {
                    int closure = i;
                    for (int j = 0; j < selections.Count; ++j)
                    {
                        if (selections[j].ParameterIndex + parameterOffset == i)
                        {
                            closure = closureIndices.GetValueOrDefault(
                                (i, targets[j]),
                                i);
                        }
                    }
                    callArguments.Add(parameters[closure]);
                }
                for (int i = 0; i < selections.Count; ++i)
                    caseTargets[selections[i].ParameterIndex] = targets[i];
                return builder.CreateCall(
                    location,
                    GetSpecializedMethod(method, caseTargets),
                    ref callArguments);
            });
    }

    /// <summary>
    /// Realizes a Delegate.Invoke callvirt on a delegate that is not bound to a
    /// single target statically by dispatching on its tag.
    /// </summary>
    /// <param name="invokeSig">The Invoke method signature.</param>
    /// <param name="handle">The delegate handle.</param>
    /// <param name="arguments">The invocation arguments.</param>
    private void MakeSelectedDelegateInvoke(
        MethodInfo invokeSig,
        Value handle,
        Value[] arguments)
    {
        var selection = CreateDelegateSelection(-1, handle);
        var parameters = invokeSig.GetParameters();
        var values = new List<Value>(arguments.Length + 1);
        for (int i = 0; i < arguments.Length; ++i)
        {
            var parameterType = parameters[i].ParameterType;
            var type = ModuleBuilder.CreateType(parameterType);
            values.Add(arguments[i].Type == type
                ? arguments[i]
                : CreateConversion(
                    arguments[i],
                    type,
                    parameterType.IsUnsignedInt()
                        ? ConvertFlags.TargetUnsigned
                        : ConvertFlags.None));
        }

        // Pass the closures of all traced targets expecting one as 'this', or
        // the handle itself for targets that are traced later
        var closureIndices = new Dictionary<MethodBase, int>();
        foreach (var target in selection.Targets ?? [])
        {
            if (target.GetParameterOffset() < 1)
                continue;
            closureIndices.Add(target, values.Count);
            values.Add(GetSelectedClosure(
                selection,
                target,
                GetParameterTypes(target)[0]));
        }
        int handleIndex = values.Count;
        if (selection.Targets is null)
        {
            values.Add(CreateConversion(
                handle,
                ModuleBuilder.CreateType(invokeSig.DeclaringType.AsNotNull()),
                ConvertFlags.None));
        }

        var location = Location;
        var result = CreateDispatch(
            ModuleBuilder.CreateType(invokeSig.ReturnType),
            callee: null,
            [selection],
            values,
            (targets, builder, dispatchParameters) =>
            {
                var target = targets[0];
                var types = GetParameterTypes(target);
                int offset = target.GetParameterOffset();
                var caseArguments = ValueBuilderList.Create(
                    builder.Generation,
                    types.Length);
                if (offset > 0)
                {
                    var closure = dispatchParameters[
                        closureIndices.GetValueOrDefault(target, handleIndex)];
                    caseArguments.Add(ConvertDispatchValue(builder, closure, types[0]));
                }
                for (int i = offset; i < types.Length; ++i)
                {
                    caseArguments.Add(ConvertDispatchValue(
                        builder,
                        dispatchParameters[i - offset],
                        types[i]));
                }
                return builder.CreateCall(location, GetMethod(target), ref caseArguments);
            });
        MakeCallReturnValue(invokeSig, result);
    }

    /// <summary>
    /// Gets the copy of the given method that is specialized for the given
    /// delegate arguments, which binds each invocation of a delegate parameter
    /// to its statically known target.
    /// </summary>
    /// <param name="method">The method to call.</param>
    /// <param name="delegateTargets">The resolved delegate targets.</param>
    /// <returns>The specialized method.</returns>
    private Method GetSpecializedMethod(
        MethodBase method,
        MethodBase?[] delegateTargets)
    {
        var parameters = method.GetParameters();
        for (int i = 0, e = parameters.Length; i < e; ++i)
        {
            if (delegateTargets[i] is null &&
                parameters[i].ParameterType.IsDelegate())
            {
                // Unbound delegate arguments are selections (see above)
                throw Location.GetInvalidOperationException(
                    "Unbound delegate argument of a specialization");
            }
        }

        var specializations = _specializations ??
            throw Location.GetInvalidOperationException();
        return specializations.GetOrCreate(method, delegateTargets);
    }

    /// <summary>
    /// Creates the error for a delegate that cannot be resolved statically.
    /// </summary>
    /// <param name="callee">
    /// The method the delegate is passed to (null for an invoked delegate).
    /// </param>
    /// <param name="parameterIndex">The index of the delegate parameter.</param>
    /// <param name="handle">The delegate handle.</param>
    /// <returns>The created exception.</returns>
    private KernelCompilationException GetUnresolvedDelegateException(
        MethodBase? callee,
        int parameterIndex,
        Value handle)
    {
        var origin = DescribeDelegateOrigin(handle);
        var message = callee is null
            ? string.Format(
                ErrorMessages.KernelDiagnosticUnresolvedDelegateInvocation,
                Method.Name,
                origin)
            : string.Format(
                ErrorMessages.KernelDiagnosticUnresolvedDelegateArgument,
                callee.GetParameters()[parameterIndex].Name ??
                    parameterIndex.ToString(CultureInfo.InvariantCulture),
                callee.Name,
                origin);
        return new KernelCompilationException(
            KernelDiagnostic.UnresolvedDelegateId,
            message,
            Location);
    }

    /// <summary>
    /// Records where the value on top of the evaluation stack comes from if it
    /// is a delegate, as delegates loaded from memory or returned from methods
    /// cannot be traced to their targets.
    /// </summary>
    /// <param name="type">The managed type of the value.</param>
    /// <param name="origin">The description of its origin.</param>
    private void RecordDelegateOrigin(Type type, string origin)
    {
        if (type.IsDelegate())
            RecordDelegateOrigin(origin);
    }

    /// <summary>
    /// Records where the value on top of the evaluation stack comes from (see
    /// above), if it is a delegate.
    /// </summary>
    /// <param name="origin">The description of its origin.</param>
    private void RecordDelegateOrigin(string origin)
    {
        var value = Block.GetValue(
            new VariableRef(Block.StackCounter - 1, VariableRefType.Stack));
        _delegateOrigins[value] = origin;
    }

    /// <summary>
    /// Describes why the given delegate handle cannot be traced to its targets,
    /// based on the first handle it is merged from that cannot be traced.
    /// </summary>
    /// <param name="handle">The delegate handle.</param>
    /// <returns>The description of its origin.</returns>
    private string DescribeDelegateOrigin(Value handle)
    {
        var visited = new HashSet<Value>();
        var worklist = new Stack<Value>();
        worklist.Push(handle);
        while (worklist.TryPop(out var current))
        {
            if (!visited.Add(current))
                continue;
            if (_delegateOrigins.TryGetValue(current, out var origin))
                return origin;
            if (_delegateAliases.TryGetValue(current, out var source))
            {
                worklist.Push(source);
            }
            else if (current is PhiValue phi)
            {
                foreach (var argument in phi.Arguments)
                    worklist.Push(argument);
            }
            else if (current is Parameter parameter &&
                !_delegateTable.ContainsKey(parameter))
            {
                return string.Format(
                    ErrorMessages.KernelDiagnosticDelegateUnboundParameter,
                    parameter.Name,
                    Method.Name);
            }
        }
        return ErrorMessages.KernelDiagnosticDelegateUnsupportedOperation;
    }

    #endregion

    #region Dispatchers

    /// <summary>
    /// Creates a call to a new dispatcher method that takes the tags of the
    /// given selections followed by the given values and calls one case for
    /// each combination of the targets of the selections. The dispatcher is
    /// implemented after code generation of the current method, when the
    /// targets of selections depending on loops are known as well.
    /// </summary>
    /// <param name="returnType">The return type.</param>
    /// <param name="callee">
    /// The method the selected delegates are passed to (null for an invoked
    /// delegate).
    /// </param>
    /// <param name="selections">The delegate selections.</param>
    /// <param name="values">The values passed to the cases.</param>
    /// <param name="createCase">
    /// Creates the call of a case from the targets of all selections, its block
    /// builder and the parameters holding the passed values.
    /// </param>
    /// <returns>The created call.</returns>
    private MethodCall CreateDispatch(
        TypeValue returnType,
        MethodBase? callee,
        List<DelegateSelection> selections,
        List<Value> values,
        Func<MethodBase[], BasicBlockBuilder, Value[], Value> createCase)
    {
        var declaration = new MethodDeclaration(
            MethodHandle.Create("DelegateDispatch"),
            returnType,
            MethodFlags.Inline);
        var methodBuilder = ModuleBuilder.GetOrCreateMethod(declaration);
        var tagType = ModuleBuilder.GetPrimitiveType(BasicValueType.Int32);
        var tags = new Value[selections.Count];
        for (int i = 0; i < tags.Length; ++i)
            tags[i] = methodBuilder.CreateParameter(tagType, "tag");
        var parameters = new Value[values.Count];
        for (int i = 0; i < values.Count; ++i)
            parameters[i] = methodBuilder.CreateParameter(values[i].Type, null);

        var arguments = ValueBuilderList.Create(
            Builder.Generation,
            tags.Length + values.Count);
        foreach (var selection in selections)
            arguments.Add(selection.Tag);
        foreach (var value in values)
            arguments.Add(value);
        var call = Builder.CreateCall(Location, methodBuilder.Method, ref arguments);

        var location = Location;
        _pendingDispatchers.Add(() =>
        {
            Location = location;
            var targets = new List<MethodBase>[selections.Count];
            for (int i = 0; i < selections.Count; ++i)
            {
                if (!TryGetSelectedTargets(selections[i], out targets[i]))
                {
                    throw GetUnresolvedDelegateException(
                        callee,
                        selections[i].ParameterIndex,
                        selections[i].Handle);
                }
            }
            ImplementDispatcher(
                methodBuilder,
                returnType,
                tags,
                parameters,
                targets,
                createCase);
        });
        return call;
    }

    /// <summary>
    /// Implements a dispatcher as a chain of tag comparisons, one for each
    /// combination of targets, falling through to the last combination.
    /// </summary>
    /// <param name="methodBuilder">The dispatcher builder.</param>
    /// <param name="returnType">The return type.</param>
    /// <param name="tags">The tag parameters.</param>
    /// <param name="parameters">The value parameters.</param>
    /// <param name="targets">The targets of all selections.</param>
    /// <param name="createCase">Creates the call of a case.</param>
    private void ImplementDispatcher(
        MethodBuilder methodBuilder,
        TypeValue returnType,
        Value[] tags,
        Value[] parameters,
        List<MethodBase>[] targets,
        Func<MethodBase[], BasicBlockBuilder, Value[], Value> createCase)
    {
        int numCases = 1;
        foreach (var selectionTargets in targets)
            numCases *= selectionTargets.Count;

        var exitBuilder = methodBuilder.CreateBasicBlock(Location, "Exit");
        var phiBuilder = returnType is VoidType || numCases < 2
            ? null
            : exitBuilder.CreatePhi(Location, returnType, numCases);
        Value? singleResult = null;
        var checkBuilder = methodBuilder.EntryBuilder;
        for (int caseIndex = 0; caseIndex < numCases; ++caseIndex)
        {
            // Determine the targets of this case in mixed radix
            var caseTargets = new MethodBase[targets.Length];
            Value? condition = null;
            int remainder = caseIndex;
            for (int i = 0; i < targets.Length; ++i)
            {
                var target = targets[i][remainder % targets[i].Count];
                remainder /= targets[i].Count;
                caseTargets[i] = target;

                var check = checkBuilder.CreateCompare(
                    Location,
                    tags[i],
                    checkBuilder.CreatePrimitiveValue(
                        Location,
                        _delegateTagValues.GetValueOrDefault(target)),
                    CompareKind.Equal);
                condition = condition is null
                    ? check
                    : checkBuilder.CreateArithmetic(
                        Location,
                        condition,
                        check,
                        BinaryArithmeticKind.And);
            }

            var caseBuilder = methodBuilder.CreateBasicBlock(Location, "Case");
            if (caseIndex < numCases - 1)
            {
                var nextBuilder = methodBuilder.CreateBasicBlock(Location, "Check");
                checkBuilder.CreateConditionalTermination(
                    condition,
                    caseBuilder.BasicBlock,
                    nextBuilder.BasicBlock);
                checkBuilder = nextBuilder;
            }
            else
            {
                checkBuilder.CreateUnconditionalTermination(caseBuilder.BasicBlock);
            }

            var result = createCase(caseTargets, caseBuilder, parameters);
            if (returnType is not VoidType)
            {
                result = ConvertDispatchValue(caseBuilder, result, returnType);
                phiBuilder?.AddArgument(caseBuilder.BasicBlock, result);
                singleResult = result;
            }
            caseBuilder.CreateUnconditionalTermination(exitBuilder.BasicBlock);
        }
        exitBuilder.CreateReturnTermination(phiBuilder?.Seal() ?? singleResult);
        methodBuilder.Seal();
    }

    /// <summary>
    /// Implements all dispatchers of the current method.
    /// </summary>
    private void ImplementPendingDispatchers()
    {
        foreach (var implement in _pendingDispatchers)
            implement();
        _pendingDispatchers.Clear();
    }

    /// <summary>
    /// Returns the IR types of all parameters of the given method, including the
    /// closure parameter of capturing lambdas.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>The parameter types.</returns>
    private TypeValue[] GetParameterTypes(MethodBase method)
    {
        var parameters = method.GetParameters();
        int offset = method.GetParameterOffset();
        var types = new TypeValue[parameters.Length + offset];
        if (offset > 0)
        {
            var declaringType = ModuleBuilder.CreateType(
                method.DeclaringType.AsNotNull());
            types[0] = declaringType is PointerType
                ? declaringType
                : ModuleBuilder.CreatePointerType(
                    declaringType,
                    MemoryAddressSpace.Generic);
        }
        for (int i = 0; i < parameters.Length; ++i)
            types[i + offset] = ModuleBuilder.CreateType(parameters[i].ParameterType);
        return types;
    }

    /// <summary>
    /// Converts a value passed through a dispatcher to the given type. Values
    /// of different types can only be pointers to different element types,
    /// such as a delegate handle passed as the closure of a lambda.
    /// </summary>
    /// <param name="builder">The current block builder.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="type">The target type.</param>
    /// <returns>The converted value.</returns>
    private Value ConvertDispatchValue(
        BasicBlockBuilder builder,
        Value value,
        TypeValue type)
    {
        if (value.Type == type)
            return value;
        if (type is not PointerType pointerType || value.Type is not PointerType)
            throw Location.GetInvalidOperationException();
        return builder.CreatePointerCast(
            Location,
            builder.CreateAddressSpaceCast(
                Location,
                value,
                pointerType.AddressSpace),
            pointerType.ElementType).AsNotNull();
    }

    #endregion
}

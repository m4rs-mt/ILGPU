// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Calls.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.Frontend.Intrinsic;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Roslyn.Analysis;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace ILGPUC.Frontend;

#pragma warning disable CA1031 // Do not catch general exception types

partial class CodeGenerator
{
    /// <summary>
    /// Creates a call instruction to the given method with the given arguments.
    /// </summary>
    /// <param name="method">The target method to invoke.</param>
    /// <param name="arguments">The call arguments.</param>
    /// <param name="delegateArguments">
    /// The delegate arguments, if the method has delegate parameters (see
    /// <see cref="PeekDelegateArguments"/>).
    /// </param>
    private void CreateCall(
        MethodBase method,
        ref ValueBuilderList arguments,
        DelegateArguments? delegateArguments = null)
    {
        // Try to implement the current intrinsic right now by generating IR code
        var intrinsicContext = new InvocationContext(
            this,
            Location,
            Block,
            Method,
            method,
            ref arguments);
        if (Intrinsics.TryGenerateCode(ref intrinsicContext, out var result))
        {
            // The method has already been implemented in IR code. We now have to map
            // the return value to the method return value handle
            result ??= Builder.UndefinedValue;
            MakeCallReturnValue(method, result);
        }
        else if (delegateArguments is null)
        {
            var target = GetMethod(method);
            _specializations?.RecordCall(target, MethodBuilder.Method, Location);
            result = Builder.CreateCall(Location, target, ref arguments);

            // Setup result
            MakeCallReturnValue(method, result);
        }
        else
        {
            result = CreateSpecializedCall(method, ref arguments, delegateArguments);

            // Setup result
            MakeCallReturnValue(method, result);
        }
    }

    /// <summary>
    /// Implements a call return value.
    /// </summary>
    /// <param name="method">The method that was called.</param>
    /// <param name="result">The return value.</param>
    private void MakeCallReturnValue(MethodBase method, Value result)
    {
        if (result.Type is VoidType or KindType)
            return;

        var returnType = method.GetReturnType();
        var flags = returnType.IsUnsignedInt()
            ? ConvertFlags.SourceUnsigned
            : ConvertFlags.None;
        Block.Push(LoadOntoEvaluationStack(result, flags));
        RecordDelegateOrigin(
            returnType,
            string.Format(
                ErrorMessages.KernelDiagnosticDelegateReturned,
                method.GetDisplayName()));
    }

    /// <summary>
    /// Realizes a call instruction.
    /// </summary>
    /// <param name="instruction">The instruction to realize.</param>
    private void MakeCall(ILInstruction instruction)
    {
        var method = instruction.GetArgumentAs<MethodBase>();
        if (instruction.HasFlags(ILInstructionFlags.Constrained)
            && method is MethodInfo methodInfo)
        {
            var constrainedType = instruction.FlagsContext.Argument as Type;
            method = ResolveVirtualCallTarget(methodInfo, constrainedType);
        }
        MakeCall(method);
    }

    /// <summary>
    /// Realizes a call instruction.
    /// </summary>
    /// <param name="target">The target method to invoke.</param>
    private void MakeCall(MethodBase target)
    {
        if (target == null)
            throw Location.GetInvalidOperationException();
        VerifyDelegateMemberCall(target);

        // The constructor of System.Object has no effect: a base constructor call
        // (e.g. in a closure class constructor) only consumes the instance
        if (target is ConstructorInfo && target.DeclaringType == typeof(object))
        {
            Block.Pop();
            return;
        }

        // Delegates bound by ILGPUC to the targets registered at call sites
        if (KernelDelegateTargets.IsBind(target))
        {
            MakeKernelDelegateBinding();
            return;
        }

        var delegateArguments = PeekDelegateArguments(target);
        var values = Block.PopMethodArgs(Location, target, null);
        CreateCall(target, ref values, delegateArguments);
    }

    /// <summary>
    /// Resolves the virtual call target of the given virtual (or abstract) method.
    /// </summary>
    /// <param name="target">The virtual method to call.</param>
    /// <param name="constrainedType">
    /// The constrained type of the virtual call.
    /// </param>
    /// <returns>The resolved call target.</returns>
    private MethodInfo ResolveVirtualCallTarget(
        MethodInfo target,
        Type? constrainedType)
    {
        const BindingFlags ConstraintMethodFlags = BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic;

        if (!target.IsVirtual)
            return target;
        if (constrainedType == null)
        {
            throw Location.GetNotSupportedException(
                ErrorMessages.NotSupportedVirtualMethodCallToUnconstrainedInstance,
                target.Name);
        }
        var sourceGenerics = target.GetGenericArguments();
        // This can only happen in constrained generic cases like:
        // Val GetVal<T>(T instance) where T : IValProvider
        // {
        //      return instance.GetVal();
        // }

        // However, there are two special cases that are supported:
        // x.GetHashCode(), x.ToString()
        // where GetHashCode and ToString are defined in Object.
        MethodInfo? actualTarget = null;
        if (target.DeclaringType == typeof(object))
        {
            var @params = target.GetParameters();
            var types = new Type[@params.Length];
            for (int i = 0, e = @params.Length; i < e; ++i)
                types[i] = @params[i].ParameterType;
            actualTarget = constrainedType.GetMethod(
                target.Name,
                ConstraintMethodFlags,
                null,
                types,
                null);
            if (actualTarget != null &&
                actualTarget.DeclaringType != constrainedType)
            {
                throw Location.GetNotSupportedException(
                    ErrorMessages.NotSupportedVirtualMethodCallToObject,
                    target.Name,
                    actualTarget.DeclaringType.AsNotNull(),
                    constrainedType);
            }
        }
        else
        {
            // Resolve the actual call target
            if (sourceGenerics.Length > 0)
                target = target.GetGenericMethodDefinition();
            var interfaceMapping = constrainedType.GetInterfaceMap(
                target.DeclaringType.AsNotNull());
            for (
                int i = 0, e = interfaceMapping.InterfaceMethods.Length;
                i < e;
                ++i)
            {
                if (interfaceMapping.InterfaceMethods[i] != target)
                    continue;
                actualTarget = interfaceMapping.TargetMethods[i];
                break;
            }
        }
        if (actualTarget == null)
        {
            throw Location.GetNotSupportedException(
                ErrorMessages.NotSupportedVirtualMethodCall,
                target.Name);
        }
        if (sourceGenerics.Length > 0 && !actualTarget.IsGenericMethodDefinition)
            actualTarget = actualTarget.GetGenericMethodDefinition();
        return sourceGenerics.Length > 0
            ? actualTarget.MakeGenericMethod(sourceGenerics)
            : actualTarget;
    }

    /// <summary>
    /// Realizes a virtual-call instruction.
    /// </summary>
    /// <param name="instruction">The current IL instruction.</param>
    private void MakeVirtualCall(ILInstruction instruction)
    {
        var method = instruction.GetArgumentAs<MethodInfo>();
        if (instruction.HasFlags(ILInstructionFlags.Constrained))
        {
            MakeVirtualCall(
                method,
                instruction.FlagsContext.Argument as Type);
        }
        else
        {
            MakeVirtualCall(method, null);
        }
    }

    /// <summary>
    /// Realizes a virtual-call instruction.
    /// </summary>
    /// <param name="target">The target method to invoke.</param>
    /// <param name="constrainedType">
    /// The target type on which to invoke the method.
    /// </param>
    private void MakeVirtualCall(MethodInfo target, Type? constrainedType)
    {
        // Intercept delegate Invoke calls for static devirtualization
        if (target.Name == "Invoke" &&
            target.DeclaringType is not null &&
            target.DeclaringType.IsDelegate())
        {
            MakeDelegateInvoke(target);
            return;
        }
        VerifyDelegateMemberCall(target);
        target = ResolveVirtualCallTarget(target, constrainedType);
        MakeCall(target);
    }

    /// <summary>
    /// Realizes a load-function-pointer instruction (ldftn).
    /// Registers the lambda body for IR generation and records it as the pending
    /// ldftn method (to be consumed by the subsequent newobj Delegate.ctor).
    /// </summary>
    /// <param name="method">The target method whose function pointer is loaded.</param>
    private void MakeLdFunction(MethodBase method)
    {
        VerifyNoStructMethodGroup(method);

        // Register the method so ILFrontend generates IR for it
        GetMethod(method);
        _pendingLdFunctionMethod = method;

        // If the target is an intrinsic method with a registered
        // generator, reify it into a one-block IR body so downstream
        // passes (e.g. TryRecognizeOperation, Inliner) can introspect
        // it. Direct-call sites still bypass the body via the inline
        // generator fast path in CreateCall.
        TrySynthesizeIntrinsicBody(method);

        // Push a null IntPtr placeholder — the real value is the closure pointer
        Block.Push(Builder.CreateNull(Location, ModuleBuilder.IntPointerType));
    }

    /// <summary>
    /// Rejects delegates created from instance methods of structs, whose
    /// targets are boxed copies of the struct instances.
    /// </summary>
    /// <param name="method">The target method of the delegate.</param>
    private void VerifyNoStructMethodGroup(MethodBase method)
    {
        if (method.IsStatic || method.DeclaringType?.IsValueType != true)
            return;
        throw new KernelCompilationException(
            KernelDiagnostic.StructMethodGroupId,
            string.Format(
                ErrorMessages.KernelDiagnosticStructMethodGroup,
                $"{method.DeclaringType.Name}.{method.Name}"),
            Location);
    }

    /// <summary>
    /// Attempts to synthesise a one-block IR body for an intrinsic
    /// <paramref name="method"/> by running its registered
    /// <c>IntrinsicGenerator</c>. Triggered from <see cref="MakeLdFunction"/>
    /// so only method-group delegate targets pay the synthesis cost —
    /// direct intrinsic call sites are untouched. Memoised via
    /// <see cref="MethodFlags.BodySynthesized"/>.
    /// </summary>
    /// <remarks>
    /// Failure cases (generator uses <c>CodeGenerator</c> state that isn't
    /// available during synthesis — e.g. <c>PullDelegateMethod</c> for
    /// Warp/Group collective intrinsics) are caught and leave the method
    /// bodiless. Direct-call sites for those methods still work via the
    /// inline generator path.
    /// </remarks>
    private void TrySynthesizeIntrinsicBody(MethodBase method)
    {
        if (!Intrinsics.HasIntrinsicGenerator(method))
            return;

        var declaration = ModuleBuilder.CreateMethodDeclaration(method);
        var methodBuilder = ModuleBuilder.GetOrCreateMethod(declaration);
        var irMethod = methodBuilder.Method;

        if (irMethod.HasFlags(MethodFlags.BodySynthesized))
            return;

        // Sealed methods have their bodies fixed — shouldn't happen for
        // intrinsics under normal flow, but guard anyway.
        if (irMethod.IsSealed)
            return;

        DisassembledMethod? disassembled;

        try
        {
            disassembled = Disassembler.TryDisassemble(method);
        }
        catch
        {
            // Disassembly failed (e.g. missing method body) — leave the
            // method bodiless and fall through to the direct-call path.
            return;
        }

        if (disassembled is null)
            return;

        try
        {
            var synth = new CodeGenerator(methodBuilder, disassembled);
            synth.GenerateIntrinsicBody(method);
            methodBuilder.Seal();
            irMethod.AddFlags(MethodFlags.BodySynthesized);
            irMethod.RemoveFlags(MethodFlags.Intrinsic);
        }
        catch
        {
            // Generator uses state unavailable during synthesis (e.g.
            // delegate pulling). The method stays bodiless — direct
            // calls still work via the inline generator path.
        }
    }

    /// <summary>
    /// Realizes a Delegate.Invoke callvirt by statically de-virtualizing it
    /// to a direct call to the lambda body method.
    /// </summary>
    /// <param name="invokeSig">The Invoke method signature.</param>
    private void MakeDelegateInvoke(MethodInfo invokeSig)
    {
        // Pop explicit arguments in reverse order (preserve left-to-right order)
        int numArgs = invokeSig.GetParameters().Length;
        var savedArgs = new Value[numArgs];
        for (int i = numArgs - 1; i >= 0; i--)
            savedArgs[i] = Block.Pop();

        // Pop the delegate handle (the closure pointer)
        var delegateHandle = Block.Pop();

        // Resolve lambda body via static devirtualization
        if (!TryResolveDelegateMethod(delegateHandle, out var lambdaMethodBase))
        {
            // A delegate that is not bound to a single target statically (e.g.
            // one selected at runtime) dispatches on its tag
            MakeSelectedDelegateInvoke(invokeSig, delegateHandle, savedArgs);
            return;
        }

        // Only re-push the closure pointer as 'this' when PopMethodArgs
        // expects it (i.e. GetParameterOffset > 0). For static methods
        // and non-capturing lambdas (instance methods on fieldless <>c),
        // GetParameterOffset returns 0 so 'this' is not expected.
        if (lambdaMethodBase.GetParameterOffset() > 0)
            Block.Push(delegateHandle);
        foreach (var arg in savedArgs)
            Block.Push(arg);

        // Emit a direct call to the lambda body (PopMethodArgs handles this+args)
        MakeCall(lambdaMethodBase);
    }

    /// <summary>
    /// Tries to resolve the lambda MethodBase behind a delegate handle value.
    /// Checks the direct value table (same-block resolution) and traces
    /// conversions and phi merges back to registered handles (cross-block
    /// resolution). The resolution fails for handles that cannot be traced,
    /// such as delegates returned from methods, and for handles selecting
    /// between several lambda bodies, which dispatch on their tags instead.
    /// </summary>
    internal bool TryResolveDelegateMethod(
        Value handle,
        [NotNullWhen(true)] out MethodBase? method)
    {
        // Direct lookup (same-block or after LoadVariable propagation)
        if (_delegateTable.TryGetValue(handle, out method))
            return true;

        // Trace converted handles and handles that crossed a block boundary on
        // the evaluation stack (phi values) back to registered handles,
        // ignoring the null constants of empty delegate caches. This succeeds
        // only when all incoming paths agree on a single lambda body.
        if (TryTraceDelegateHandle(handle, out method) && method is not null)
            return true;
        method = null;
        return false;
    }

    /// <summary>
    /// Resolves the delegate passed to an intrinsic method to its single
    /// lambda body, or reports it as a delegate that cannot be resolved.
    /// </summary>
    /// <param name="handle">The delegate handle.</param>
    /// <param name="intrinsic">The intrinsic method.</param>
    /// <param name="argumentIndex">
    /// The index of the argument, including the instance argument.
    /// </param>
    /// <returns>The resolved lambda body.</returns>
    internal MethodBase ResolveIntrinsicDelegate(
        Value handle,
        MethodBase intrinsic,
        int argumentIndex)
    {
        if (TryResolveDelegateMethod(handle, out var method))
            return method;
        throw GetUnresolvedDelegateException(
            intrinsic,
            argumentIndex - intrinsic.GetParameterOffset(),
            handle);
    }

    /// <summary>
    /// Traces a delegate handle through phi merges back to the handles
    /// registered in <see cref="_delegateTable"/> and succeeds if all of them
    /// agree on a single lambda body (see <see cref="TryCollectDelegateTargets"/>).
    /// </summary>
    /// <param name="handle">The delegate handle value to trace.</param>
    /// <param name="method">
    /// The resolved lambda body (null if the handle is always null).
    /// </param>
    private bool TryTraceDelegateHandle(Value handle, out MethodBase? method)
    {
        var targets = new List<MethodBase>(2);
        method = null;
        if (!TryCollectDelegateTargets(handle, [], targets) || targets.Count > 1)
            return false;
        method = targets.Count > 0 ? targets[0] : null;
        return true;
    }

    /// <summary>
    /// Recursively traces a delegate handle through phi merges and stored
    /// variables back to the handles registered in
    /// <see cref="_delegateTable"/> and collects their distinct lambda bodies.
    /// Null constants (the cache-miss branch of the static delegate-cache
    /// diamond) are ignored. Returns false as soon as the handle becomes
    /// untraceable.
    /// </summary>
    /// <param name="handle">The delegate handle value to trace.</param>
    /// <param name="visited">The set of already visited values.</param>
    /// <param name="targets">The collected lambda bodies.</param>
    /// <param name="closures">
    /// The closure of each lambda body, if all handles referring to it agree on
    /// a single closure value (null for ambiguous closures).
    /// </param>
    private bool TryCollectDelegateTargets(
        Value handle,
        HashSet<Value> visited,
        List<MethodBase> targets,
        Dictionary<MethodBase, Value?>? closures = null)
    {
        if (!visited.Add(handle))
        {
            // Cycle: this path is constrained by the remaining phi arguments
            return true;
        }

        if (_delegateTable.TryGetValue(handle, out var found))
        {
            if (!targets.Contains(found))
                targets.Add(found);
            if (closures is not null)
            {
                closures[found] = !closures.TryGetValue(found, out var closure) ||
                    closure == handle
                    ? handle
                    : null;
            }
            return true;
        }
        if (_delegateAliases.TryGetValue(handle, out var source))
            return TryCollectDelegateTargets(source, visited, targets, closures);

        switch (handle)
        {
            case NullValue:
                // The cache-miss branch of the static delegate-cache diamond
                return true;
            case PhiValue phi when phi.NumArguments > 0:
                foreach (var argument in phi.Arguments)
                {
                    if (!TryCollectDelegateTargets(
                        argument,
                        visited,
                        targets,
                        closures))
                    {
                        return false;
                    }
                }
                return true;
            default:
                // Unknown handle kind (or an incomplete phi of a loop header
                // that has not been sealed yet) — nothing can be proven about it
                return false;
        }
    }

    /// <summary>
    /// Realizes an indirect call instruction.
    /// </summary>
    /// <param name="signature">The target signature.</param>
    private void MakeCalli(object signature) =>
        throw Location.GetNotSupportedException(
            ErrorMessages.NotSupportedIndirectMethodCall,
            signature);

    /// <summary>
    /// Realizes a jump instruction.
    /// </summary>
    /// <param name="target">The target method to invoke.</param>
    private void MakeJump(MethodBase target) =>
        throw Location.GetNotSupportedException(
            ErrorMessages.NotSupportedMethodJump,
            target.Name);
}

#pragma warning restore CA1031 // Do not catch general exception types

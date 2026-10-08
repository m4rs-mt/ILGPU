// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2018-2026 ILGPU Project
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
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Creates a call instruction to the given method with the given arguments.
    /// </summary>
    /// <param name="method">The target method to invoke.</param>
    /// <param name="arguments">The call arguments.</param>
    private void CreateCall(MethodBase method, ref ValueBuilderList arguments)
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
        else
        {
            var targetFunction = GetMethod(method);
            result = Builder.CreateCall(
                Location,
                targetFunction,
                ref arguments);

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

        var flags = method.GetReturnType().IsUnsignedInt()
            ? ConvertFlags.SourceUnsigned
            : ConvertFlags.None;
        Block.Push(LoadOntoEvaluationStack(result, flags));
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
        var values = Block.PopMethodArgs(Location, target, null);
        CreateCall(target, ref values);
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
            throw Location.GetNotSupportedException(
                ErrorMessages.NotSupportedCannotDevirtualizeDelegate,
                invokeSig.Name);
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
    /// Checks both the direct value table (same-block resolution) and the
    /// local variable table (cross-block resolution through phi merges).
    /// </summary>
    internal bool TryResolveDelegateMethod(
        Value handle,
        [NotNullWhen(true)] out MethodBase? method)
    {
        // Direct lookup (same-block or after LoadVariable propagation)
        if (_delegateTable.TryGetValue(handle, out method))
            return true;

        // Cross-block fallback: check the local variable table (populated
        // when a delegate was stored to a local via stloc).
        foreach (var kv in _delegateLocalTable)
        {
            method = kv.Value;
            return true;
        }

        // Pending delegate fallback: when MakeNewDelegate created a delegate
        // but it was passed directly as an argument (no stloc), the pending
        // delegate tracks it. This handles Warp.Reduce(value, (a,b) => a+b)
        // where the lambda goes straight from newobj to the call arguments.
        if (_pendingDelegateForLocal is not null)
        {
            method = _pendingDelegateForLocal;
            _pendingDelegateForLocal = null;
            return true;
        }

        method = null;
        return false;
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

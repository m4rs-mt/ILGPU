// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
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
using ILGPUC.IR.Values;
using ILGPUC.Util;
using System;
using System.Reflection;
using ValueList = ILGPU.Util.InlineList<ILGPUC.IR.Values.ValueReference>;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Creates a call instruction to the given method with the given arguments.
    /// </summary>
    /// <param name="method">The target method to invoke.</param>
    /// <param name="arguments">The call arguments.</param>
    private void CreateCall(
        MethodBase method,
        ref ValueList arguments)
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
            MakeCallReturnValue(method, result);
        }
        else
        {
            // Early rejection for runtime-dependent methods
            VerifyNotRuntimeMethod(method);

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
    private void MakeCallReturnValue(MethodBase method, ValueReference result)
    {
        if (!result.IsValid || result.Type.IsVoidType)
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
        target = ResolveVirtualCallTarget(target, constrainedType);
        MakeCall(target);
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

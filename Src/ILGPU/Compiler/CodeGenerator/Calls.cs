// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Calls.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler.Intrinsic;
using ILGPU.Resources;
using ILGPU.Util;
using LLVMSharp;
using System;
using System.Reflection;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        /// <summary>
        /// Creates a call instruction to the given method with the given arguments.
        /// </summary>
        /// <param name="target">The target method to invoke.</param>
        /// <param name="args">The call arguments</param>
        private void CreateCall(MethodBase target, Value[] args)
        {
            var intrinsicContext = new InvocationContext(InstructionBuilder, Method, target, args, this);
            // Check for remapping first
            var remappedContext = Unit.RemapIntrinsic(intrinsicContext);
            if (remappedContext != null)
                intrinsicContext = remappedContext.Value;

            // Early rejection for runtime-dependent methods
            VerifyNotRuntimeMethod(CompilationContext, intrinsicContext.Method);

            // Handle device functions
            if (!Unit.HandleIntrinsic(intrinsicContext, out Value? methodInvocationResult))
            {
                var method = Unit.GetMethod(intrinsicContext.Method);
                var llvmArgs = new LLVMValueRef[args.Length];
                for (int i = 0, e = args.Length; i < e; ++i)
                    llvmArgs[i] = args[i].LLVMValue;
                var call = InstructionBuilder.CreateCall(method.LLVMFunction, llvmArgs, string.Empty);
                if (!method.IsVoid)
                    methodInvocationResult = new Value(method.ReturnType, call);
            }
            if (methodInvocationResult.HasValue)
                CurrentBlock.Push(methodInvocationResult.Value);
        }

        /// <summary>
        /// Realizes a call instruction.
        /// </summary>
        /// <param name="target">The target method to invoke.</param>
        private void MakeCall(MethodBase target)
        {
            // Check parameter types for interfaces that involve virtual function calls
            Unit.GetType(target);
            var @params = target.GetParameters();
            var valueOffset = target.GetParameterOffset();
            Value[] args = new Value[@params.Length + valueOffset];
            CurrentBlock.PopMethodArgs(target, args, valueOffset);
            CreateCall(target, args);
        }

        /// <summary>
        /// Resolves the virtual call target of the given virtual (or abstract) method.
        /// </summary>
        /// <param name="compilationContext">The current compilation context.</param>
        /// <param name="target">The virtual method to call.</param>
        /// <param name="constrainedType">The constrained type of the virtual call.</param>
        /// <returns>The resolved call target.</returns>
        public static MethodInfo ResolveVirtualCallTarget(
            CompilationContext compilationContext,
            MethodInfo target,
            Type constrainedType)
        {
            if (!target.IsVirtual)
                return target;
            if (constrainedType == null)
                throw compilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedVirtualMethodCallToUnconstrainedInstance, target.Name);
            var sourceGenerics = target.GetGenericArguments();
            // This can only happen in constrained generic cases like:
            // Val GetVal<T>(T instance) where T : IValProvider
            // {
            //      return instance.GetVal();
            // }

            // However, there are two special cases that are supported:
            // x.GetHashCode(), x.ToString()
            // where GetHashCode and ToString are defined in Object.
            MethodInfo actualTarget = null;
            if (target.DeclaringType == typeof(object))
            {
                var @params = target.GetParameters();
                var types = new Type[@params.Length];
                for (int i = 0, e = @params.Length; i < e; ++i)
                    types[i] = @params[i].ParameterType;
                actualTarget = constrainedType.GetMethod(
                    target.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    types,
                    null);
                if (actualTarget != null && actualTarget.DeclaringType != constrainedType)
                    throw compilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedVirtualMethodCallToObject,
                        target.Name,
                        actualTarget.DeclaringType,
                        constrainedType);
            }
            else
            {
                // Resolve the actual call target
                if (sourceGenerics.Length > 0)
                    target = target.GetGenericMethodDefinition();
                var interfaceMapping = constrainedType.GetInterfaceMap(target.DeclaringType);
                for (int i = 0, e = interfaceMapping.InterfaceMethods.Length; i < e; ++i)
                {
                    if (interfaceMapping.InterfaceMethods[i] != target)
                        continue;
                    actualTarget = interfaceMapping.TargetMethods[i];
                    break;
                }
            }
            if (actualTarget == null)
                throw compilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedVirtualMethodCall, target.Name);
            if (sourceGenerics.Length > 0)
                return actualTarget.MakeGenericMethod(sourceGenerics);
            else
                return actualTarget;
        }

        /// <summary>
        /// Realizes a virtual-call instruction.
        /// </summary>
        /// <param name="target">The target method to invoke.</param>
        /// <param name="constrainedType">The target type on which to invoke the method.</param>
        private void MakeVirtualCall(MethodInfo target, Type constrainedType)
        {
            target = ResolveVirtualCallTarget(CompilationContext, target, constrainedType);
            MakeCall(target);
        }

        /// <summary>
        /// Realizes an indirect call instruction.
        /// </summary>
        /// <param name="signature">The target signature.</param>
        private void MakeCalli(object signature)
        {
            throw CompilationContext.GetNotSupportedException(
                ErrorMessages.NotSupportedIndirectMethodCall, signature);
        }

        /// <summary>
        /// Realizes a jump instruction.
        /// </summary>
        /// <param name="target">The target method to invoke.</param>
        private void MakeJump(MethodBase target)
        {
            throw CompilationContext.GetNotSupportedException(
                ErrorMessages.NotSupportedMethodJump, target.Name);
        }
    }
}

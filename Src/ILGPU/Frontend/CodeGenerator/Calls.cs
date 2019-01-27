// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Calls.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System;
using System.Collections.Immutable;
using System.Reflection;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Creates a call instruction to the given method with the given arguments.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="method">The target method to invoke.</param>
        /// <param name="arguments">The call arguments.</param>
        private void CreateCall(
            Block block,
            IRBuilder builder,
            MethodBase method,
            ImmutableArray<ValueReference> arguments)
        {
            var intrinsicContext = new InvocationContext(this, block, Method, method, arguments);

            // Check for remapping first
            RemappedIntrinsics.RemapIntrinsic(ref intrinsicContext);
            Frontend.RemapIntrinsic(ref intrinsicContext);

            // Early rejection for runtime-dependent methods
            VerifyNotRuntimeMethod(intrinsicContext.Method);

            // Handle device functions
            if (!Intrinsics.HandleIntrinsic(intrinsicContext, out ValueReference result) &&
                !Frontend.HandleIntrinsic(intrinsicContext, out result))
            {
                var targetFunction = DeclareMethod(intrinsicContext.Method);

                result = builder.CreateCall(
                    targetFunction,
                    intrinsicContext.Arguments);
            }

            // Setup result
            if (result.IsValid && !result.Type.IsVoidType)
                block.Push(result);
        }

        /// <summary>
        /// Realizes a call instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="target">The target method to invoke.</param>
        private void MakeCall(
            Block block,
            IRBuilder builder,
            MethodBase target)
        {
            if (target == null)
                throw this.GetInvalidILCodeException();
            var values = block.PopMethodArgs(target, null);
            CreateCall(block, builder, target, values);
        }

        /// <summary>
        /// Resolves the virtual call target of the given virtual (or abstract) method.
        /// </summary>
        /// <param name="target">The virtual method to call.</param>
        /// <param name="constrainedType">The constrained type of the virtual call.</param>
        /// <returns>The resolved call target.</returns>
        private MethodInfo ResolveVirtualCallTarget(MethodInfo target, Type constrainedType)
        {
            if (!target.IsVirtual)
                return target;
            if (constrainedType == null)
                throw this.GetNotSupportedException(
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
                    throw this.GetNotSupportedException(
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
                throw this.GetNotSupportedException(
                    ErrorMessages.NotSupportedVirtualMethodCall, target.Name);
            if (sourceGenerics.Length > 0)
                return actualTarget.MakeGenericMethod(sourceGenerics);
            else
                return actualTarget;
        }

        /// <summary>
        /// Realizes a virtual-call instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="instruction">The current IL instruction.</param>
        private void MakeVirtualCall(
            Block block,
            IRBuilder builder,
            ILInstruction instruction)
        {
            var method = instruction.GetArgumentAs<MethodInfo>();
            if (instruction.HasFlags(ILInstructionFlags.Constrained))
                MakeVirtualCall(block, builder, method, instruction.FlagsContext.Argument as Type);
            else
                MakeVirtualCall(block, builder, method, null);
        }

        /// <summary>
        /// Realizes a virtual-call instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="target">The target method to invoke.</param>
        /// <param name="constrainedType">The target type on which to invoke the method.</param>
        private void MakeVirtualCall(
            Block block,
            IRBuilder builder,
            MethodInfo target,
            Type constrainedType)
        {
            target = ResolveVirtualCallTarget(target, constrainedType);
            MakeCall(block, builder, target);
        }

        /// <summary>
        /// Realizes an indirect call instruction.
        /// </summary>
        /// <param name="signature">The target signature.</param>
        private void MakeCalli(object signature)
        {
            throw this.GetNotSupportedException(
                ErrorMessages.NotSupportedIndirectMethodCall, signature);
        }

        /// <summary>
        /// Realizes a jump instruction.
        /// </summary>
        /// <param name="target">The target method to invoke.</param>
        private void MakeJump(MethodBase target)
        {
            throw this.GetNotSupportedException(
                ErrorMessages.NotSupportedMethodJump, target.Name);
        }
    }
}

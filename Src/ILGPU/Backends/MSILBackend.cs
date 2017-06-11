// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: MSLIBackend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.ABI;
using ILGPU.Compiler;
using ILGPU.Resources;
using ILGPU.Runtime.CPU;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a MSIL backend that is used by the CPU runtime.
    /// </summary>
    public sealed class MSILBackend : Backend
    {
        #region Static

        /// <summary>
        /// Contains intrinsic types.
        /// </summary>
        private static readonly ISet<Type> IntrinsicTypes;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static MSILBackend()
        {
            IntrinsicTypes = new HashSet<Type>()
            {
                typeof(Atomic),
                typeof(Debug),
                typeof(GPUMath),
                typeof(Grid),
                typeof(Group),
                typeof(Interop),
                typeof(Math),
                typeof(MemoryFence),
                typeof(Warp),
                typeof(CPURuntimeWarpContext),
                typeof(CPURuntimeGroupContext),
            };
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new MSIL backend.
        /// </summary>
        /// <param name="context">The used context.</param>
        public MSILBackend(Context context)
            : base(context, RuntimePlatform)
        { }

        #endregion

        #region Methods

        /// <summary cref="Backend.TargetUnit(CompileUnit)"/>
        internal override void TargetUnit(CompileUnit unit)
        { }

        /// <summary cref="Backend.CreateABISpecification(CompileUnit)"/>
        internal override ABISpecification CreateABISpecification(CompileUnit unit)
        {
            return new MSILABI(unit);
        }

        /// <summary>
        /// Compiles a given compile unit with the specified entry point.
        /// </summary>
        /// <param name="unit">The compile unit to compile.</param>
        /// <param name="entry">The desired entry point.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        public override CompiledKernel Compile(CompileUnit unit, MethodInfo entry)
        {
            var entryPoint = new EntryPoint(entry, unit);
            CheckMethod(unit, entry, entryPoint);
            return new CompiledKernel(Context, entry, new byte[] { }, entry.Name, entryPoint);
        }

        /// <summary>
        /// Checks the given method for compatibility.
        /// </summary>
        /// <param name="unit">The current compilation unit.</param>
        /// <param name="method">The method to test for compatiblity.</param>
        /// <param name="entryPoint">The entry point.</param>
        private static void CheckMethod(
            CompileUnit unit,
            MethodBase method,
            EntryPoint entryPoint)
        {
            var body = method.GetMethodBody();
            if (body == null)
                return;

            var compilationContext = unit.CompilationContext;
            compilationContext.EnterMethod(method);
            if (body.ExceptionHandlingClauses.Count > 0)
                throw compilationContext.GetNotSupportedException(
                    ErrorMessages.CustomExceptionSemantics, method.Name);
            var disassembledMethod = DisassembledMethod.Disassemble(
                method,
                compilationContext.NotSupportedILInstructionHandler);
            foreach (var instruction in disassembledMethod.Instructions)
            {
                switch (instruction.InstructionType)
                {
                    case ILInstructionType.Ldsfld:
                        CodeGenerator.VerifyStaticFieldLoad(
                            compilationContext,
                            unit.Flags,
                            instruction.GetArgumentAs<FieldInfo>());
                        break;
                    case ILInstructionType.Stsfld:
                        CodeGenerator.VerifyStaticFieldStore(
                            compilationContext,
                            unit.Flags,
                            instruction.GetArgumentAs<FieldInfo>());
                        break;
                    case ILInstructionType.Box:
                    case ILInstructionType.Unbox:
                    case ILInstructionType.Calli:
                        throw compilationContext.GetNotSupportedException(
                            ErrorMessages.NotSupportedInstruction, method.Name, instruction.InstructionType);
                    case ILInstructionType.Callvirt:
                        var virtualTarget = instruction.GetArgumentAs<MethodBase>();
                        var constrainedType = instruction.HasFlags(ILInstructionFlags.Constrained) ?
                            instruction.FlagsContext.Argument as Type : null;
                        CheckCall(
                            unit,
                            CodeGenerator.ResolveVirtualCallTarget(
                                compilationContext,
                                virtualTarget,
                                constrainedType),
                            entryPoint);
                        break;
                    case ILInstructionType.Call:
                    case ILInstructionType.Newobj:
                        CheckCall(
                            unit,
                            instruction.GetArgumentAs<MethodBase>(),
                            entryPoint);
                        break;
                }
            }
            compilationContext.LeaveMethod(method);
        }

        /// <summary>
        /// Checks the given target method for a compatible activator call.
        /// </summary>
        /// <param name="unit">The current compilation unit.</param>
        /// <param name="target">The call target to test for compatiblity.</param>
        /// <returns>True, iff the given method is a valid activator call.</returns>
        private static bool VerifyActivatorCall(
            CompileUnit unit,
            MethodBase target)
        {
            var activatorType = typeof(Activator);
            if (target.DeclaringType != activatorType)
                return false;
            var genericArgs = target.GetGenericArguments();
            if (target.Name != nameof(Activator.CreateInstance) ||
                genericArgs.Length != 1 ||
                !genericArgs[0].IsValueType ||
                target.GetParameters().Length != 0)
                throw unit.CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedActivatorOperation, target.Name);
            return true;
        }

        /// <summary>
        /// Checks the given call target for compatibility.
        /// </summary>
        /// <param name="unit">The current compilation unit.</param>
        /// <param name="target">The call target to test for compatiblity.</param>
        /// <param name="entryPoint">The entry point.</param>
        private static void CheckCall(
            CompileUnit unit,
            MethodBase target,
            EntryPoint entryPoint)
        {
            var compilationContext = unit.CompilationContext;
            if (target.IsAbstract)
                throw compilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedVirtualMethodCall, target.Name);
            if (IntrinsicTypes.Contains(target.DeclaringType))
                return;
            if (VerifyActivatorCall(unit, target))
                return;
            CodeGenerator.VerifyNotRuntimeMethod(
                compilationContext,
                target);
            CodeGenerator.VerifyAccessToWarpShuffle(
                compilationContext,
                target,
                entryPoint);
            CheckMethod(unit, target, entryPoint);
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        { }

        #endregion
    }
}

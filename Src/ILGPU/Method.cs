// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Method.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler;
using ILGPU.LLVM;
using ILGPU.Resources;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU
{
    /// <summary>
    /// Represents a method that was compiled to LLVM.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class Method
    {
        #region Instance

        /// <summary>
        /// Constructs a new method.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        /// <param name="methodBase">The target method.</param>
        internal Method(CompileUnit unit, MethodBase methodBase)
        {
            if ((methodBase.MethodImplementationFlags & MethodImplAttributes.InternalCall) == MethodImplAttributes.InternalCall)
                throw unit.CompilationContext.GetNotSupportedException(
                    ErrorMessages.RuntimeInternalMethodNotSupported, methodBase);
            if ((methodBase.MethodImplementationFlags & MethodImplAttributes.Native) == MethodImplAttributes.Native)
                throw unit.CompilationContext.GetNotSupportedException(
                    ErrorMessages.NativeMethodNotSupported, methodBase);
            CompileUnit = unit;
            Name = unit.GetLLVMName(methodBase);
            MethodBase = methodBase;
            if (methodBase is MethodInfo)
            {
                var genericArgs = methodBase.GetGenericArguments();
                for (int i = 0, e = genericArgs.Length; i < e; ++i)
                {
                    if (genericArgs[i].IsGenericParameter)
                        throw unit.CompilationContext.GetNotSupportedException(
                            ErrorMessages.NotSupportedGenericMethod, methodBase);
                }
            }
            var functionType = unit.GetType(methodBase);
            LLVMFunction = AddFunction(unit.LLVMModule, Name, functionType);
            AddFunctionAttr(LLVMFunction, LLVMAttribute.LLVMAlwaysInlineAttribute);
            SetLinkage(LLVMFunction, LLVMLinkage.LLVMInternalLinkage);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated compile unit.
        /// </summary>
        public CompileUnit CompileUnit { get; }

        /// <summary>
        /// Returns the LLVM-compatible name of method.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns the full name in the managed context of this method.
        /// </summary>
        public string ManagedFullName => MethodBase.DeclaringType.FullName + "." + MethodBase.Name;

        /// <summary>
        /// Returns the return-type of the method.
        /// </summary>
        public Type ReturnType
        {
            get
            {
                var info = MethodBase as MethodInfo;
                if (info == null)
                    return typeof(void);
                return info.ReturnType;
            }
        }

        /// <summary>
        /// Returns true iff the return type of the method is void.
        /// </summary>
        public bool IsVoid => ReturnType == typeof(void);

        /// <summary>
        /// Returns true iff the mehtod is a static method.
        /// </summary>
        public bool IsStatic => MethodBase.IsStatic;

        /// <summary>
        /// Returns true iff the method is an instance method (non-static).
        /// </summary>
        public bool IsInstance => !MethodBase.IsStatic;

        /// <summary>
        /// Returns the parameter offset of the method.
        /// Instance methods have an offset of 1, whereas static methods
        /// have an offset of 0.
        /// </summary>
        public int ParameterOffset => IsInstance ? 1 : 0;

        /// <summary>
        /// Returns the LLVM-function-value that represents
        /// the encapsulated method.
        /// </summary>
        [CLSCompliant(false)]
        public LLVMValueRef LLVMFunction { get; }

        /// <summary>
        /// Returns the internal method base.
        /// </summary>
        public MethodBase MethodBase { get; }

        /// <summary>
        /// Returns the disassembled method.
        /// </summary>
        public DisassembledMethod DisassembledMethod { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Visits all call instructions and invokes the call handler for each
        /// called method. Note that this method is invoked recursively on each
        /// called method.
        /// </summary>
        /// <param name="callHandler">The handler to handle called methods.</param>
        public void VisitCalls(Action<ILInstruction, Method> callHandler)
        {
            VisitCalls(callHandler, true);
        }

        /// <summary>
        /// Visits all call instructions and invokes the call handler for each
        /// called method.
        /// </summary>
        /// <param name="callHandler">The handler to handle called methods.</param>
        /// <param name="recursive">True, iff this method should be invoked on each called method recursively.</param>
        public void VisitCalls(
            Action<ILInstruction, Method> callHandler,
            bool recursive)
        {
            foreach (var call in DisassembledMethod.DirectCallInstructions)
            {
                var m = CompileUnit.GetMethod(call.GetArgumentAs<MethodBase>(), false);
                if (m == null)
                    continue;
                callHandler(call, m);
                if (recursive)
                    m.VisitCalls(callHandler);
            }
        }

        /// <summary>
        /// Triggers disassembly of this method.
        /// </summary>
        /// <param name="compilationContext">The current compilation context.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Disassemble(CompilationContext compilationContext)
        {
            Debug.Assert(compilationContext != null, "Invalid compilation context");
            if (DisassembledMethod != null)
                return;
            DisassembledMethod = DisassembledMethod.Disassemble(
                MethodBase,
                compilationContext.NotSupportedILInstructionHandler,
                compilationContext.CurrentSequencePointEnumerator);
            if (DisassembledMethod.Method.GetMethodBody().ExceptionHandlingClauses.Count > 0)
                throw compilationContext.GetNotSupportedException(
                    ErrorMessages.CustomExceptionSemantics, MethodBase.Name);
        }

        /// <summary>
        /// Triggers decompilation of this method in the scope of
        /// the given compilation unit.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Decompile(CompileUnit unit)
        {
            Debug.Assert(unit != null, "Invalid unit");

            Disassemble(unit.CompilationContext);
            using (var decompiler = new CodeGenerator(unit, this, DisassembledMethod))
            {
                decompiler.GenerateCode();
            }

            // Perform a simple pass over the method
            RunFunctionPassManager(unit.CodeGenFunctionPassManager, LLVMFunction);
        }

        #endregion
    }
}

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Objects.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Realizes a boxing operation that boxes a value.
    /// </summary>
    /// <param name="type">The type of the boxed value.</param>
    private void MakeBox(Type type)
    {
        // Delegates created from instance methods of structs box the instance
        int nextIndex = CurrentInstructionIndex + 1;
        if (nextIndex < DisassembledMethod.Count &&
            DisassembledMethod[nextIndex].InstructionType is
                ILInstructionType.LdFunction or ILInstructionType.LdVirtualFunction)
        {
            VerifyNoStructMethodGroup(
                DisassembledMethod[nextIndex].GetArgumentAs<MethodBase>());
        }

        var value = Block.Pop();
        if (value.Type is not ObjectType)
        {
            throw Location.GetNotSupportedException(
                ErrorMessages.NotSupportedBoxing,
                FrontendDisplay.Format(type));
        }
        var alloca = CreateTempAlloca(value.Type);
        CreateStore(alloca, value);

        // Push the address of the boxed storage as the object reference.
        // MakeUnbox pops this address and reinterprets it as a pointer to
        // the target value type; the subsequent ldobj loads the value. This
        // matches _stackBehavior[Box], which declares (pop 1, push 1).
        Block.Push(alloca);
    }

    /// <summary>
    /// Realizes an unboxing operation that unboxes a previously boxed value.
    /// </summary>
    /// <param name="type">The target type.</param>
    private void MakeUnbox(Type type)
    {
        if (type == null || !type.IsValueType)
            throw Location.GetInvalidOperationException();
        var address = Block.Pop();
        if (address.Type is not PointerType)
            throw Location.GetInvalidOperationException();

        // IL unbox pushes a managed pointer to the value stored inside the
        // box (MakeBox pushes the address of the boxed storage). The
        // disassembler lowers unbox.any into unbox + ldobj, so the actual
        // value load happens in MakeLoadObject.
        var typeNode = ModuleBuilder.CreateType(type);
        Block.Push(CreateConversion(
            address,
            ModuleBuilder.CreatePointerType(typeNode, MemoryAddressSpace.Generic),
            ConvertFlags.None));
    }

    /// <summary>
    /// Realizes a new-object operation that creates a new instance of a specified
    /// type.
    /// </summary>
    /// <param name="method">The target method.</param>
    private void MakeNewObject(MethodBase method)
    {
        var constructor = method as ConstructorInfo ??
            throw Location.GetInvalidOperationException();

        var type = constructor.DeclaringType.AsNotNull();

        // Creating an exception means the code throws, which kernels cannot do.
        // Report it here, before the class-type check below would reject the
        // exception type with a less helpful message.
        if (typeof(Exception).IsAssignableFrom(type))
        {
            // Code generation continues, so that other errors in the method are
            // found; the exception object is never used for anything but a throw
            DeferThrow(GetReachableThrowException(FrontendDisplay.Format(type)));
            for (int i = 0; i < constructor.GetParameters().Length; ++i)
                Block.Pop();
            Block.Push(Builder.UndefinedValue);
            return;
        }

        // Dispatch class types to specialized handlers
        if (type.IsClass && !type.IsValueType)
        {
            if (typeof(Delegate).IsAssignableFrom(type))
            {
                MakeNewDelegate(constructor);
                return;
            }
            if (type.IsSealed ||
                type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            {
                MakeNewClosureObject(constructor, type);
                return;
            }
            throw Location.GetNotSupportedException(
                ErrorMessages.NotSupportedClassType,
                type.Name);
        }

        var typeNode = ModuleBuilder.CreateType(type);
        var alloca = CreateTempAlloca(typeNode);

        var value = Builder.CreateNull(Location, typeNode);
        CreateStore(alloca, value);

        // Invoke constructor for type
        var values = Block.PopMethodArgs(Location, method, alloca);
        CreateCall(constructor, ref values);

        // Push created instance on the stack
        Block.Push(CreateLoad(
            alloca,
            typeNode,
            ConvertFlags.None));
    }

    /// <summary>
    /// Realizes a newobj instruction for a sealed or compiler-generated class
    /// (closure class). The object is allocated as a module-level Global in
    /// Local (per-thread stack) memory.
    /// </summary>
    /// <param name="ctor">The constructor to invoke.</param>
    /// <param name="type">The class type being instantiated.</param>
    private void MakeNewClosureObject(ConstructorInfo ctor, Type type)
    {
        // Build the struct layout for the closure class
        var closureStructType = ModuleBuilder.CreateClassStructureType(type);

        // Allocate a module-level Global in Local (per-thread) memory
        var global = ModuleBuilder.CreateGlobalDirect(
            Location,
            closureStructType,
            MemoryAddressSpace.Local,
            rawArrayLength: new PrimitiveValueBox(BasicValueType.Int32, 1L),
            rawValueInitializer: new PrimitiveValueBox(BasicValueType.Int32, 0L));

        if (global is null)
            throw Location.GetInvalidOperationException();

        // Create a GCInit node in the current block that marks where this managed
        // object enters existence.  LowerGCInit will validate (no-loop) and lower it.
        var gcInit = Builder.CreateGCInit(Location, global)
            ?? throw Location.GetInvalidOperationException();

        // Invoke the constructor (pops ctor args from stack, passes gcInit as 'this')
        var values = Block.PopMethodArgs(Location, ctor, gcInit);
        CreateCall(ctor, ref values);

        // Push the pointer (reference semantics: push the GCInit itself)
        Block.Push(gcInit);
    }

    /// <summary>
    /// Realizes a newobj Delegate.ctor instruction. Instead of creating a real
    /// delegate object, we record the closure-pointer → lambda-method association
    /// and push the closure pointer as the delegate handle.
    /// </summary>
    /// <param name="ctor">The Delegate constructor being invoked.</param>
    private void MakeNewDelegate(ConstructorInfo ctor)
    {
        // Stack top: IntPtr placeholder (from ldftn), below it: closure pointer
        Block.Pop();  // discard the IntPtr placeholder pushed by MakeLdFunction
        var closurePtr = Block.Pop();  // the closure object (Global pointer)

        var method = _pendingLdFunctionMethod
            ?? throw Location.GetInvalidOperationException();
        _pendingLdFunctionMethod = null;

        // Record: this closure pointer maps to this lambda body
        _delegateTable[closurePtr] = method;

        // Push the closure pointer as the delegate handle (type-erased)
        Block.Push(closurePtr);
    }

    /// <summary>
    /// Realizes a managed-object initialization.
    /// </summary>
    /// <param name="type">The target type.</param>
    private void MakeInitObject(Type type)
    {
        if (type == null)
            throw Location.GetInvalidOperationException();

        var address = Block.Pop();
        var typeNode = ModuleBuilder.CreateType(type);
        var value = Builder.CreateNull(Location, typeNode);
        CreateStore(address, value);
    }

    /// <summary>
    /// Realizes an is-instance instruction.
    /// </summary>
    /// <param name="type">The target type.</param>
    private void MakeIsInstance(Type type)
    {
        VerifyNoDelegateCast(type, "is");
        throw Location.GetNotSupportedException(
            ErrorMessages.NotSupportedIsInstance,
            FrontendDisplay.Format(type));
    }

    /// <summary>
    /// Realizes a cast-class instruction, which is not supported.
    /// </summary>
    /// <param name="type">The target type.</param>
    private void MakeCastClass(Type type)
    {
        VerifyNoDelegateCast(type, "cast to");
        throw Location.GetNotSupportedException(
            ErrorMessages.NotSupportedInstruction,
            $"castclass {type}",
            Method.Name);
    }

    /// <summary>
    /// Realizes a throw or rethrow instruction. Both are only supported in code
    /// that is removed at compile time, so reaching one is an error.
    /// </summary>
    /// <param name="isRethrow">True for a rethrow, which has no operand.</param>
    private void MakeThrow(bool isRethrow)
    {
        DeferThrow(GetReachableThrowException(
            isRethrow ? "the current exception" : "an exception"));
        if (!isRethrow)
            Block.Pop();
    }

    /// <summary>
    /// Creates the error for a throw that is reachable in the current method.
    /// </summary>
    /// <param name="exceptionType">Describes the thrown exception.</param>
    private InternalCompilerException GetReachableThrowException(
        string exceptionType)
    {
        var message = string.Format(
            ErrorMessages.NotSupportedExceptionThrow,
            exceptionType,
            FrontendDisplay.Format(Method));

        // A throw often stays reachable because a type test could not be decided
        // at compile time; name the calls that could not be evaluated, and why
        foreach (var (method, reason) in EvaluationFailures)
        {
            message += $". '{FrontendDisplay.Format(method)}' was not evaluated " +
                $"at compile time: {reason}";
        }
        return Location.GetException(new ReachableThrowException(message));
    }

    /// <summary>
    /// Realizes an indirect load instruction.
    /// </summary>
    /// <param name="type">The target type.</param>
    private void MakeLoadObject(Type type)
    {
        var address = Block.Pop();

        // ldind.ref loads the reference stored at the address, such as a
        // delegate passed by reference
        if (type == typeof(object) &&
            address.Type is PointerType { ElementType: PointerType })
        {
            Block.Push(Builder.CreateLoad(Location, address));
            RecordDelegateOrigin(
                ErrorMessages.KernelDiagnosticDelegateLoadedThroughReference);
            return;
        }

        var targetElementType = ModuleBuilder.CreateType(type);
        Block.Push(CreateLoad(
            address,
            targetElementType,
            type.ToTargetUnsignedFlags()));
        RecordDelegateOrigin(
            type,
            ErrorMessages.KernelDiagnosticDelegateLoadedThroughReference);
    }

    /// <summary>
    /// Realizes an indirect store instruction.
    /// </summary>
    /// <param name="type">The target type.</param>
    private void MakeStoreObject(Type type)
    {
        var typeNode = ModuleBuilder.CreateType(type);
        var value = Block.Pop(typeNode, ConvertFlags.None);
        var address = Block.Pop();
        CreateStore(address, value);
    }

    /// <summary>
    /// Loads the size of the type (in bytes).
    /// </summary>
    /// <param name="type">The target type.</param>
    private void LoadSizeOf(Type type)
    {
        // Pushes the size, in bytes, of a supplied value type onto the evaluation
        // stack.
        //
        // For a reference type, the size returned is the size of a reference value
        // of the corresponding type (4 bytes on 32-bit systems), not the size of the
        // data stored in objects referred to by the reference value. A generic type
        // parameter can be used only in the body of the type or method that defines
        // it. When that type or method is instantiated, the generic type parameter
        // is replaced by a value type or reference type.
        if (type.IsValueType)
        {
            Load(type.SizeOf());
        }
        else
        {
            var pointerSize = ModuleBuilder.TargetPlatform.Is64Bit() ? 8 : 4;
            Load(pointerSize);
        }
    }
}

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
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
    private void MakeBox()
    {
        var value = Block.Pop();
        if (value.Type is not ObjectType)
            throw Location.GetInvalidOperationException();
        var alloca = CreateTempAlloca(value.Type);
        CreateStore(alloca, value);
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
        var typeNode = ModuleBuilder.CreateType(type);
        Block.Push(CreateLoad(
            address,
            typeNode,
            type.ToTargetUnsignedFlags()));
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

        // Also record as a "pending" delegate for cross-block propagation.
        // When a stloc stores a phi-merged delegate value, the direct
        // _delegateTable lookup fails (phi != closurePtr). This fallback
        // ensures the delegate method is propagated to _delegateLocalTable.
        _pendingDelegateForLocal = method;

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
    private void MakeIsInstance(Type type) =>
        throw Location.GetNotSupportedException(
            ErrorMessages.NotSupportedIsInstance);

    /// <summary>
    /// Realizes an indirect load instruction.
    /// </summary>
    /// <param name="type">The target type.</param>
    private void MakeLoadObject(Type type)
    {
        var address = Block.Pop();
        var targetElementType = ModuleBuilder.CreateType(type);
        Block.Push(CreateLoad(
            address,
            targetElementType,
            type.ToTargetUnsignedFlags()));
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

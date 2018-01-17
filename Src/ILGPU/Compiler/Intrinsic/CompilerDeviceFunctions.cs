// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: CompilerDeviceFunctions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using ILGPU.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Compiler.Intrinsic
{
    /// <summary>
    /// Represents the base class for all compiler-specific device functions.
    /// </summary>
    abstract class CompilerDeviceFunctions : IDeviceFunctions
    {
        #region Static

        /// <summary>
        /// Represents a basic handler for all compiler-specific device functions.
        /// </summary>
        protected delegate Value? DeviceFunctionHandler(CompilerDeviceFunctions functions, InvocationContext context);

        /// <summary>
        /// Represents a basic handler for all ILGPU intrinsic device functions.
        /// </summary>
        protected delegate Value? IntrinsicDeviceFunctionHandler(CompilerDeviceFunctions functions, InvocationContext context, IntrinsicAttribute attribute);

        /// <summary>
        /// Represents a basic remapper for all compiler-specific device functions.
        /// </summary>
        protected delegate InvocationContext? DeviceFunctionRemapper(CompilerDeviceFunctions functions, InvocationContext context);

        /// <summary>
        /// Stores the default handlers.
        /// </summary>
        protected static readonly Dictionary<MethodBase, DeviceFunctionHandler> DeviceFunctionHandlers = new Dictionary<MethodBase, DeviceFunctionHandler>();

        /// <summary>
        /// Stores the intrinsic handlers.
        /// </summary>
        protected static readonly Dictionary<IntrinsicType, IntrinsicDeviceFunctionHandler> IntrinsicDeviceFunctionHandlers = new Dictionary<IntrinsicType, IntrinsicDeviceFunctionHandler>();

        /// <summary>
        /// Stores the default function remappers.
        /// </summary>
        protected static readonly Dictionary<MethodBase, DeviceFunctionRemapper> DeviceFunctionRemappers = new Dictionary<MethodBase, DeviceFunctionRemapper>();

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Justification = "Caching of compiler-known functions")]
        static CompilerDeviceFunctions()
        {
            // General handlers

            IntrinsicDeviceFunctionHandlers.Add(IntrinsicType.Atomic,
                (deviceFunctions, context, attribute) => deviceFunctions.MakeAtomic(context, (attribute as AtomicIntrinsicAttribute).IntrinsicKind));
            IntrinsicDeviceFunctionHandlers.Add(IntrinsicType.MemoryFence,
                (deviceFunctions, context, attribute) => deviceFunctions.MakeMemoryFence(context, (attribute as MemoryFenceIntrinsicAttribute).IntrinsicKind));
            IntrinsicDeviceFunctionHandlers.Add(IntrinsicType.Grid,
                (deviceFunctions, context, attribute) => deviceFunctions.MakeGrid(context, (attribute as GridIntrinsicAttribute).IntrinsicKind));
            IntrinsicDeviceFunctionHandlers.Add(IntrinsicType.Group,
                (deviceFunctions, context, attribute) => deviceFunctions.MakeGroup(context, (attribute as GroupIntrinsicAttribute).IntrinsicKind));
            IntrinsicDeviceFunctionHandlers.Add(IntrinsicType.Warp,
                (deviceFunctions, context, attribute) => deviceFunctions.MakeWarp(context, (attribute as WarpIntrinsicAttribute).IntrinsicKind));
            IntrinsicDeviceFunctionHandlers.Add(IntrinsicType.Math,
                (deviceFunctions, context, attribute) => deviceFunctions.MakeMathInternal(context, (attribute as MathIntrinsicAttribute).IntrinsicKind));
            IntrinsicDeviceFunctionHandlers.Add(IntrinsicType.Interop,
                (deviceFunctions, context, attribute) => deviceFunctions.MakeInterop(context, (attribute as InteropIntrinsicAttribute).IntrinsicKind));

            // IntPtr
            RegisterIntPtrMappings(typeof(IntPtr), "Int32", "Int64", typeof(int), typeof(long));
            RegisterIntPtrMappings(typeof(UIntPtr), "UInt32", "UInt64", typeof(uint), typeof(ulong));

            // Debugging

            var debugType = typeof(Debug);

            DeviceFunctionHandlers.Add(debugType.GetMethod("Assert", BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(bool) }, null),
                (deviceFunctions, context) => deviceFunctions.MakeConditionAssertChecked(context));
            DeviceFunctionHandlers.Add(debugType.GetMethod("Assert", BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(bool), typeof(string) }, null),
                (deviceFunctions, context) => deviceFunctions.MakeMessageAssertChecked(context));

            // Math functions
            RegisterMathMappings();
        }

        static void RegisterIntPtrMappings(Type type, string smallIntTypeName, string largeIntTypeName, Type smallIntType, Type largeIntType)
        {
            DeviceFunctionHandlers.Add(type.GetConstructor(new Type[] { smallIntType }),
                (deviceFunctions, context) => deviceFunctions.MakeIntPtr(context, smallIntType));
            DeviceFunctionHandlers.Add(type.GetConstructor(new Type[] { largeIntType }),
                (deviceFunctions, context) => deviceFunctions.MakeIntPtr(context, largeIntType));
            DeviceFunctionHandlers.Add(type.GetConstructor(new Type[] { typeof(void).MakePointerType() }),
                (deviceFunctions, context) => deviceFunctions.MakeIntPtr(context, typeof(void).MakePointerType()));

            DeviceFunctionHandlers.Add(type.GetMethod("op_Equality", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
                (deviceFunctions, context) => deviceFunctions.MakeIntPtrCompare(context, LLVMIntPredicate.LLVMIntEQ));
            DeviceFunctionHandlers.Add(type.GetMethod("op_Inequality", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
                (deviceFunctions, context) => deviceFunctions.MakeIntPtrCompare(context, LLVMIntPredicate.LLVMIntNE));

            DeviceFunctionHandlers.Add(type.GetMethod("Add", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
                (deviceFunctions, context) => deviceFunctions.MakeIntPtrOffsetManipulation(context, true));
            DeviceFunctionHandlers.Add(type.GetMethod("op_Addition", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
                (deviceFunctions, context) => deviceFunctions.MakeIntPtrOffsetManipulation(context, true));

            DeviceFunctionHandlers.Add(type.GetMethod("Subtract", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
                (deviceFunctions, context) => deviceFunctions.MakeIntPtrOffsetManipulation(context, false));
            DeviceFunctionHandlers.Add(type.GetMethod("op_Subtraction", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
                (deviceFunctions, context) => deviceFunctions.MakeIntPtrOffsetManipulation(context, false));

            DeviceFunctionHandlers.Add(type.GetMethod("To" + smallIntTypeName, BindingFlags.Instance | BindingFlags.Public),
                (deviceFunctions, context) => deviceFunctions.MakeIntPtrCastToInt(context, smallIntType));
            DeviceFunctionHandlers.Add(type.GetMethod("op_Explicit", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null, new Type[] { smallIntType }, null),
                (deviceFunctions, context) => deviceFunctions.MakeCastIntToIntPtr(context));

            DeviceFunctionHandlers.Add(type.GetMethod("To" + largeIntTypeName, BindingFlags.Instance | BindingFlags.Public),
                (deviceFunctions, context) => deviceFunctions.MakeIntPtrCastToInt(context, largeIntType));
            DeviceFunctionHandlers.Add(type.GetMethod("op_Explicit", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null, new Type[] { largeIntType}, null),
                (deviceFunctions, context) => deviceFunctions.MakeCastIntToIntPtr(context));

            DeviceFunctionHandlers.Add(type.GetMethod("ToPointer", BindingFlags.Instance | BindingFlags.Public),
                (deviceFunctions, context) => deviceFunctions.MakeIntPtrCastToPtr(context));
        }

        static void RegisterMathMappings()
        {
            var mathType = typeof(Math);

            AddMathRemapping(mathType, "Abs", "Abs", new Type[] { typeof(sbyte) });
            AddMathRemapping(mathType, "Abs", "Abs", new Type[] { typeof(short) });
            AddMathRemapping(mathType, "Abs", "Abs", new Type[] { typeof(int) });
            AddMathRemapping(mathType, "Abs", "Abs", new Type[] { typeof(long) });
            AddMathRemapping(mathType, "Abs", "Abs", new Type[] { typeof(float) });
            AddMathRemapping(mathType, "Abs", "Abs", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Sqrt", "Sqrt", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Asin", "Asin", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Sin", "Sin", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Sinh", "Sinh", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Acos", "Acos", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Cos", "Cos", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Cosh", "Cosh", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Atan", "Atan", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Atan2", "Atan2", new Type[] { typeof(double), typeof(double) });
            AddMathRemapping(mathType, "Tan", "Tan", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Tanh", "Tanh", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Pow", "Pow", new Type[] { typeof(double), typeof(double) });

            AddMathRemapping(mathType, "Exp", "Exp", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Floor", "Floor", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Ceiling", "Ceiling", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Log", "Log", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Log", "Log", new Type[] { typeof(double), typeof(double) });
            AddMathRemapping(mathType, "Log10", "Log10", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Min", "Min", new Type[] { typeof(sbyte), typeof(sbyte) });
            AddMathRemapping(mathType, "Min", "Min", new Type[] { typeof(short), typeof(short) });
            AddMathRemapping(mathType, "Min", "Min", new Type[] { typeof(int), typeof(int) });
            AddMathRemapping(mathType, "Min", "Min", new Type[] { typeof(long), typeof(long) });
            AddMathRemapping(mathType, "Min", "Min", new Type[] { typeof(byte), typeof(byte) });
            AddMathRemapping(mathType, "Min", "Min", new Type[] { typeof(ushort), typeof(ushort) });
            AddMathRemapping(mathType, "Min", "Min", new Type[] { typeof(uint), typeof(uint) });
            AddMathRemapping(mathType, "Min", "Min", new Type[] { typeof(ulong), typeof(ulong) });

            AddMathRemapping(mathType, "Max", "Max", new Type[] { typeof(sbyte), typeof(sbyte) });
            AddMathRemapping(mathType, "Max", "Max", new Type[] { typeof(short), typeof(short) });
            AddMathRemapping(mathType, "Max", "Max", new Type[] { typeof(int), typeof(int) });
            AddMathRemapping(mathType, "Max", "Max", new Type[] { typeof(long), typeof(long) });
            AddMathRemapping(mathType, "Max", "Max", new Type[] { typeof(byte), typeof(byte) });
            AddMathRemapping(mathType, "Max", "Max", new Type[] { typeof(ushort), typeof(ushort) });
            AddMathRemapping(mathType, "Max", "Max", new Type[] { typeof(uint), typeof(uint) });
            AddMathRemapping(mathType, "Max", "Max", new Type[] { typeof(ulong), typeof(ulong) });

            AddMathRemapping(mathType, "Sign", "Sign", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Sign", "Sign", new Type[] { typeof(float) });

            AddMathRemapping(mathType, "Round", "RoundToEven", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Round", "Round", new Type[] { typeof(double), typeof(MidpointRounding) });

            // Note: BigMul and DivRem can be mapped automatically (since they are implemented in il).

            var floatType = typeof(float);
            AddMathRemapping(floatType, "IsNaN", "IsNaN", new Type[] { floatType });
            AddMathRemapping(floatType, "IsInfinity", "IsInfinity", new Type[] { floatType });

            var doubleType = typeof(double);
            AddMathRemapping(doubleType, "IsNaN", "IsNaN", new Type[] { doubleType });
            AddMathRemapping(doubleType, "IsInfinity", "IsInfinity", new Type[] { doubleType });
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new compiler-specific device-function storage.
        /// </summary>
        /// <param name="unit">The target compilation unit.</param>
        public CompilerDeviceFunctions(CompileUnit unit)
        {
            Unit = unit;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the assigned compilation unit.
        /// </summary>
        public CompileUnit Unit { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to remap the given invocation context to another context.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The remapped context or null, iff the remapping operation was not successful.</returns>
        public InvocationContext? Remap(InvocationContext context)
        {
            // Check registered mappings
            if (!DeviceFunctionRemappers.TryGetValue(context.Method, out DeviceFunctionRemapper handler))
            {
                // Check for nullable types
                return RemapNullableMethods(context);
            }
            return handler(this, context);
        }

        /// <summary>
        /// Tries to handle the given invocation context in the scope of these compiler-known functions.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="result">The resulting value.</param>
        /// <returns>True, iff this context can handle the curent invocation context.</returns>
        public bool Handle(InvocationContext context, out Value? result)
        {
            result = null;

            var intrinsic = context.Method.GetCustomAttribute<IntrinsicAttribute>();

            // Check custom intrinsics
            if (intrinsic != null)
            {
                if (!IntrinsicDeviceFunctionHandlers.TryGetValue(
                    intrinsic.Type,
                    out IntrinsicDeviceFunctionHandler intrinsicHandler))
                    throw context.CompilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedIntrinsic, intrinsic.Type);
                result = intrinsicHandler(this, context, intrinsic);
                return true;
            }

            // Check for intrinsic activator functionality
            if (context.Method.DeclaringType == typeof(Activator))
            {
                result = MakeActivatorCall(context);
                return true;
            }

            // Check external intrinsic functions
            if (!DeviceFunctionHandlers.TryGetValue(context.Method, out DeviceFunctionHandler handler))
                return false;

            result = handler(this, context);
            return true;
        }

        #endregion

        #region Atomics

        /// <summary>
        /// Makes a general atomic invocation of the given kind.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="kind">The kind of the atomic operation.</param>
        /// <returns>The resulting value.</returns>
        protected virtual Value? MakeAtomic(InvocationContext context, AtomicIntrinsicKind kind)
        {
            var builder = context.Builder;
            var args = context.GetArgs();

            // First arg is a VariableView<T>
            // Extract the pointer from the tuple
            var variableView = args[0];
            var ptr = BuildExtractValue(builder, variableView.LLVMValue, 0, "ptr");
            var elementType = variableView.ValueType.GetGenericArguments()[0];
            ptr = BuildBitCast(builder, ptr, Unit.GetType(elementType.MakePointerType()), "ptrT");

            var value = args[1].LLVMValue;

            switch (kind)
            {
                case AtomicIntrinsicKind.CmpXch:
                    var cmpxch =
                        BuildAtomicCmpXchg(builder, ptr, value, args[2].LLVMValue,
                        LLVMAtomicOrdering.LLVMAtomicOrderingSequentiallyConsistent, LLVMAtomicOrdering.LLVMAtomicOrderingSequentiallyConsistent, false);
                    return new Value(
                        elementType,
                        BuildExtractValue(builder, cmpxch, 0, string.Empty));
                case AtomicIntrinsicKind.AddF32:
                    return MakeAtomicAdd(context, ptr, kind);
                case AtomicIntrinsicKind.IncU32:
                    return MakeAtomicInc(context, ptr, kind);
                case AtomicIntrinsicKind.DecU32:
                    return MakeAtomicDec(context, ptr, kind);
                default:
                    return new Value(
                        elementType,
                        BuildAtomicRMW(builder, (LLVMAtomicRMWBinOp)kind, ptr, value, LLVMAtomicOrdering.LLVMAtomicOrderingSequentiallyConsistent, false));
            }

        }

        /// <summary>
        /// Makes a specifc atomic-inc invocation of the given kind.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="ptr">The pointer to the data location.</param>
        /// <param name="kind">The kind of the atomic operations.</param>
        /// <returns>The resulting value.</returns>
        protected abstract Value MakeAtomicAdd(InvocationContext context, LLVMValueRef ptr, AtomicIntrinsicKind kind);

        /// <summary>
        /// Makes a specifc atomic-inc invocation of the given kind.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="ptr">The pointer to the data location.</param>
        /// <param name="kind">The kind of the atomic operations.</param>
        /// <returns>The resulting value.</returns>
        protected abstract Value MakeAtomicInc(InvocationContext context, LLVMValueRef ptr, AtomicIntrinsicKind kind);

        /// <summary>
        /// Makes a specifc atomic-dec invocation of the given kind.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="ptr">The pointer to the data location.</param>
        /// <param name="kind">The kind of the atomic operations.</param>
        /// <returns>The resulting value.</returns>
        protected abstract Value MakeAtomicDec(InvocationContext context, LLVMValueRef ptr, AtomicIntrinsicKind kind);

        #endregion

        #region Memory Fences

        /// <summary>
        /// Handles general memory-fence operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="kind">The kind of the memory-fence intrinsic.</param>
        /// <returns>The resulting value.</returns>
        protected abstract Value? MakeMemoryFence(InvocationContext context, MemoryFenceIntrinsicKind kind);

        #endregion

        #region Grids

        /// <summary>
        /// Handles general grid operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="kind">The kind of the grid intrinsic.</param>
        /// <returns>The resulting value.</returns>
        protected abstract Value? MakeGrid(InvocationContext context, GridIntrinsicKind kind);

        #endregion

        #region Groups

        /// <summary>
        /// Handles general group operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="kind">The kind of the group intrinsic.</param>
        /// <returns>The resulting value.</returns>
        protected abstract Value? MakeGroup(InvocationContext context, GroupIntrinsicKind kind);

        #endregion

        #region Warps

        /// <summary>
        /// Handles general warp operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="kind">The kind of the warp intrinsic.</param>
        /// <returns>The resulting value.</returns>
        protected abstract Value? MakeWarp(InvocationContext context, WarpIntrinsicKind kind);

        #endregion

        #region IntPtr

        /// <summary>
        /// Handles IntPtr creation.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="sourceType">The source type.</param>
        /// <returns>The resulting value.</returns>
        protected virtual Value? MakeIntPtr(InvocationContext context, Type sourceType)
        {
            Debug.Assert(sourceType.IsPrimitive || sourceType.IsPointer);
            var builder = context.Builder;
            var args = context.GetArgs();
            LLVMValueRef ptrValue;
            var ptrType = context.LLVMContext.VoidPtrType;
            if (sourceType.IsPointer)
                ptrValue = BuildPointerCast(builder, args[1].LLVMValue, ptrType, string.Empty);
            else
                ptrValue = BuildIntToPtr(builder, args[1].LLVMValue, ptrType, string.Empty);
            // Since this is a constructor, we have to store the value into the instance value
            BuildStore(builder, ptrValue, args[0].LLVMValue);
            return null;
        }

        /// <summary>
        /// Handles IntPtr comparisons.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="predicate">The comparison predicate.</param>
        /// <returns>The resulting value.</returns>
        protected virtual Value? MakeIntPtrCompare(InvocationContext context, LLVMIntPredicate predicate)
        {
            var builder = context.Builder;
            var args = context.GetArgs();
            var firstVal = BuildPtrToInt(builder, args[0].LLVMValue, Unit.NativeIntPtrType, string.Empty);
            var secondVal = BuildPtrToInt(builder, args[1].LLVMValue, Unit.NativeIntPtrType, string.Empty);
            return new Value(
                typeof(bool),
                BuildICmp(builder, predicate, firstVal, secondVal, string.Empty));
        }

        /// <summary>
        /// Handles int to IntPtr casts.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        protected virtual Value? MakeCastIntToIntPtr(InvocationContext context)
        {
            return new Value(
                typeof(IntPtr),
                BuildIntToPtr(
                    context.Builder,
                    context.GetArgs()[0].LLVMValue,
                    context.LLVMContext.VoidPtrType,
                    string.Empty));
        }

        /// <summary>
        /// Handles IntPtr to void* casts.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        protected virtual Value? MakeIntPtrCastToPtr(InvocationContext context)
        {
            return new Value(
                typeof(IntPtr),
                BuildLoad(
                    context.Builder,
                    context.GetArgs()[0].LLVMValue,
                    string.Empty));
        }

        /// <summary>
        /// Handles IntPtr to int casts.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="intType">The target int type.</param>
        /// <returns>The resulting value.</returns>
        protected virtual Value? MakeIntPtrCastToInt(InvocationContext context, Type intType)
        {
            // Load ptr value from instance
            var builder = context.Builder;
            var ptr = BuildLoad(builder, context.GetArgs()[0].LLVMValue, string.Empty);
            return new Value(
                intType,
                BuildPtrToInt(
                    builder,
                    ptr,
                    Unit.GetType(intType),
                    string.Empty));
        }

        /// <summary>
        /// Handles IntPtr offset manipilations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="positive">True, iff the given offset should be treated as positive.</param>
        /// <returns>The resulting value.</returns>
        protected virtual Value? MakeIntPtrOffsetManipulation(InvocationContext context, bool positive)
        {
            var builder = context.Builder;
            var args = context.GetArgs();
            var ptrType = context.LLVMContext.VoidPtrType;
            var ptr = BuildBitCast(builder, args[0].LLVMValue, ptrType, string.Empty);
            var add = positive ?
                BuildAdd(builder, ptr, args[1].LLVMValue, string.Empty) :
                BuildSub(builder, ptr, args[1].LLVMValue, string.Empty);
            return new Value(
                args[0].ValueType,
                BuildBitCast(builder, add, TypeOf(args[0].LLVMValue), string.Empty));
        }

        #endregion

        #region Nullable

        /// <summary>
        /// Remaps methods of nullable types.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The remapped invocation context.</returns>
        private static InvocationContext? RemapNullableMethods(InvocationContext context)
        {
            // Check for nullable types
            var declaringType = context.Method.DeclaringType;
            var baseType = Nullable.GetUnderlyingType(declaringType);
            if (baseType == null)
                return null;

            var nullableType = typeof(Nullable<>).MakeGenericType(baseType);
            var valueGetter = nullableType.GetProperty("Value").GetGetMethod();
            if (valueGetter != context.Method)
                return null;

            var nullableGetValueMapper = typeof(CompilerDeviceFunctions).GetMethod(
                "GetNullableValue",
                BindingFlags.NonPublic | BindingFlags.Static);
            return new InvocationContext(
                context.Builder,
                context.CallerMethod,
                nullableGetValueMapper.MakeGenericMethod(baseType),
                context.GetArgs(),
                context.CodeGenerator);
        }

        /// <summary>
        /// Wraps the getter T?.Value.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="nullable">The nullable value.</param>
        /// <returns>The internal value of the given nullable.</returns>
        private static T GetNullableValue<T>(ref T? nullable)
            where T : struct
        {
            Debug.Assert(nullable.HasValue, "Nullable value is null");
            return nullable.GetValueOrDefault();
        }

        #endregion

        #region Math

        /// <summary>
        /// Registers a math mapping for a function from mathType via ILGPU.GPUMath.
        /// </summary>
        /// <param name="mathType">The scope of the function.</param>
        /// <param name="mathName">The name of the function in the scope of mathType.</param>
        /// <param name="gpuMathName">The name of the function in the scope of ILGPU.GPUMath.</param>
        /// <param name="paramTypes">The parameter types of both functions.</param>
        private static void AddMathRemapping(Type mathType, string mathName, string gpuMathName, Type[] paramTypes)
        {
            var mathFunc = mathType.GetMethod(mathName, BindingFlags.Public | BindingFlags.Static, null, paramTypes, null);
            Debug.Assert(mathFunc != null, "Invalid source function");
            var gpuMathFunc = typeof(GPUMath).GetMethod(gpuMathName, BindingFlags.Public | BindingFlags.Static, null, paramTypes, null);
            Debug.Assert(gpuMathFunc != null, "Invalid target function");
            DeviceFunctionRemappers.Add(mathFunc, (deviceFunctions, context) => deviceFunctions.MakeMathRemapping(context, gpuMathFunc));
        }

        /// <summary>
        /// Default remapping of a generic math operation.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="targetMethod">The target method from the gpu-math class.</param>
        /// <returns>The remapped invocation context.</returns>
        private static InvocationContext MakeDefaultMathRemapping(InvocationContext context, MethodInfo targetMethod)
        {
            return new InvocationContext(
                context.Builder,
                context.CallerMethod,
                targetMethod,
                context.GetArgs(),
                context.CodeGenerator);
        }

        /// <summary>
        /// Remaps general math operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="targetMethod">The target method from the gpu-math class.</param>
        /// <returns>The remapped invocation context.</returns>
        protected virtual InvocationContext? MakeMathRemapping(InvocationContext context, MethodInfo targetMethod)
        {
            return MakeDefaultMathRemapping(context, targetMethod);
        }

        /// <summary>
        /// Handles general math operations and performs double-intrinsic to float-intrinsic conversions if requested.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="kind">The kind of the math intrinsic.</param>
        /// <returns>The resulting value.</returns>
        private Value? MakeMathInternal(InvocationContext context, MathIntrinsicKind kind)
        {
            kind = kind.ResolveIntrinsicKind(context.Unit.Force32BitFloats);
            var targetMethod = GPUMath.MathFunctionMapping[kind];
            context = MakeDefaultMathRemapping(context, targetMethod);
            return MakeMath(context, kind);
        }

        /// <summary>
        /// Handles general math operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="kind">The kind of the math intrinsic.</param>
        /// <returns>The resulting value.</returns>
        protected abstract Value? MakeMath(InvocationContext context, MathIntrinsicKind kind);

        #endregion

        #region Assert

        private Value? MakeConditionAssertChecked(InvocationContext context)
        {
            if ((context.Unit.Flags & CompileUnitFlags.EnableAssertions) != CompileUnitFlags.EnableAssertions)
                return null;
            return MakeConditionAssert(context);
        }

        /// <summary>
        /// Triggers code generation of a conditional assert function.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        protected abstract Value? MakeConditionAssert(InvocationContext context);

        private Value? MakeMessageAssertChecked(InvocationContext context)
        {
            if ((context.Unit.Flags & CompileUnitFlags.EnableAssertions) != CompileUnitFlags.EnableAssertions)
                return null;
            return MakeMessageAssert(context);
        }

        /// <summary>
        /// Triggers code generation of a message assert function.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        protected abstract Value? MakeMessageAssert(InvocationContext context);

        #endregion

        #region Activator

        /// <summary>
        /// Handles general activator operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        protected virtual Value? MakeActivatorCall(InvocationContext context)
        {
            var compilationContext = context.CompilationContext;
            var genericArgs = context.GetMethodGenericArguments();
            if (context.Method.Name != nameof(Activator.CreateInstance) ||
                context.GetArgs().Length != 0 ||
                genericArgs.Length != 1 ||
                !genericArgs[0].IsValueType)
                throw compilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedActivatorOperation, context.Method.Name);
            var targetType = genericArgs[0];
            var llvmTargetType = context.Unit.GetType(targetType);
            return new Value(targetType, ConstNull(llvmTargetType));
        }

        #endregion

        #region Interop

        /// <summary>
        /// Handles general interop operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="kind">The kind of the interop intrinsic.</param>
        /// <returns>The resulting value.</returns>
        protected virtual Value? MakeInterop(InvocationContext context, InteropIntrinsicKind kind)
        {
            var compilationContext = context.CompilationContext;
            var args = context.GetArgs();
            var genericArgs = context.GetMethodGenericArguments();
            var type = genericArgs.Length > 0 ? genericArgs[0] : null;
            var llvmType = context.Unit.GetType(type);
            var builder = context.Builder;
            var int32Type = context.LLVMContext.Int32Type;

            switch (kind)
            {
                case InteropIntrinsicKind.DestroyStructure:
                    return new Value(type, BuildStore(builder, args[0].LLVMValue, ConstNull(llvmType)));
                case InteropIntrinsicKind.SizeOf:
                    return new Value(typeof(int), BuildTruncOrBitCast(builder, SizeOf(llvmType), int32Type, string.Empty));
                case InteropIntrinsicKind.OffsetOf:
                    {
                        if (type.IsPrimitive)
                            throw compilationContext.GetNotSupportedException(
                                ErrorMessages.CannotTakeFieldOffsetOfPrimitiveType, type);
                        // Argument is a GEP
                        var testVal = args[0].LLVMValue;
                        var gepSource = GetOperand(testVal, 0);
                        // First argument is the string const
                        var stringConst = GetOperand(gepSource, 0);
                        var stringPtr = GetAsString(stringConst, out IntPtr size);
                        var sizeVal = size.ToInt32();
                        if (sizeVal < 2)
                            throw compilationContext.GetNotSupportedException(
                                ErrorMessages.CannotFindFieldOfType, string.Empty, type);
                        var fieldName = Marshal.PtrToStringAnsi(stringPtr, Math.Max(sizeVal - 1, 0));
                        var mappedType = context.Unit.GetObjectType(type);
                        var info = mappedType.ManagedType.GetField(fieldName);
                        if (info == null || !mappedType.TryResolveOffset(info, out int offset))
                            throw compilationContext.GetNotSupportedException(
                                ErrorMessages.CannotFindFieldOfType, fieldName, type);

                        var index = ConstInt(int32Type, offset, false);
                        var offsetPointer = BuildInBoundsGEP(
                            builder,
                            ConstPointerNull(llvmType),
                            out index,
                            1,
                            string.Empty);
                        var intOffset = BuildPtrToInt(builder, offsetPointer, int32Type, string.Empty);

                        return new Value(typeof(int), intOffset);
                    }
                default:
                    throw compilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedIntrinsic, kind);
            }
        }

        #endregion
    }
}

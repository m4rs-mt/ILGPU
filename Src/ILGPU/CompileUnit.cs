// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: CompileUnit.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.ABI;
using ILGPU.Compiler;
using ILGPU.Compiler.Intrinsic;
using ILGPU.Resources;
using ILGPU.Util;
using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ILGPU
{
    /// <summary>
    /// Represents a single compile unit (a bunch of compiled functions).
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class CompileUnit : DisposeBase
    {
        #region Static

        /// <summary>
        /// The maximum length of LLVM names.
        /// </summary>
        private const int MaxLLVMNameLength = 40;

        /// <summary>
        /// The replacement regex for LLVM names.
        /// </summary>
        private static readonly Regex LLVMNameRegex = new Regex(@"[<>.,+`\s\(\)\[\]]+", RegexOptions.Compiled);

        #endregion

        #region Instance

        private readonly Dictionary<MethodBase, Method> methodMapping =
            new Dictionary<MethodBase, Method>();
        private readonly Dictionary<Type, MappedType> typeMapping =
            new Dictionary<Type, MappedType>();
        private readonly List<IDeviceFunctions> deviceFunctions;
        private readonly List<IDeviceTypes> deviceTypes;
        private ABISpecification abiSpecification;
        private int memberIdCounter = 0;

        private bool needsOptimization = false;

        /// <summary>
        /// Constructs a new compile unit using the provided method.
        /// </summary>
        /// <param name="context">The parent context.</param>
        /// <param name="name">The name of the unit.</param>
        /// <param name="backend">The final backend.</param>
        /// <param name="deviceFunctions">The device functions to use.</param>
        /// <param name="deviceTypes">The device types to use.</param>
        /// <param name="flags">The compile-unit flags.</param>
        internal CompileUnit(
            Context context,
            string name,
            Backend backend,
            IReadOnlyList<IDeviceFunctions> deviceFunctions,
            IReadOnlyList<IDeviceTypes> deviceTypes,
            CompileUnitFlags flags)
        {
            Debug.Assert(context != null && backend != null && !string.IsNullOrWhiteSpace(name));

            Name = name;
            Flags = flags;
            Context = context;
            Backend = backend;
            LLVMModule = LLVM.ModuleCreateWithNameInContext(name, context.LLVMContext);

            typeMapping.Add(typeof(object), new MappedType(
                typeof(object),
                LLVMContext.StructCreateNamed("Object"),
                0,
                null));

            this.deviceFunctions = new List<IDeviceFunctions>(deviceFunctions.Count + 1);
            for (int i = 0, e = deviceFunctions.Count; i < e; ++i)
                this.deviceFunctions.Add(deviceFunctions[i]);
            backend.TargetUnit(this);

            this.deviceTypes = new List<IDeviceTypes>(deviceTypes.Count + 1);
            for (int i = 0, e = deviceTypes.Count; i < e; ++i)
                this.deviceTypes.Add(deviceTypes[i]);

            CodeGenFunctionPassManager = LLVM.CreateFunctionPassManagerForModule(LLVMModule);
#if DEBUG
            LLVM.AddVerifierPass(CodeGenFunctionPassManager);
#endif
            LLVM.AddPromoteMemoryToRegisterPass(CodeGenFunctionPassManager);
            LLVM.AddCFGSimplificationPass(CodeGenFunctionPassManager);
            LLVM.AddScalarReplAggregatesPassSSA(CodeGenFunctionPassManager);

            abiSpecification = backend.CreateABISpecification(this);
            CompilationContext = new CompilationContext(this);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the name of this unit.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns the associated compile-unit flags.
        /// </summary>
        public CompileUnitFlags Flags { get; }

        /// <summary>
        /// Returns true iff the current flags contain the <see cref="CompileUnitFlags.Force32BitFloats"/> flag.
        /// </summary>
        public bool Force32BitFloats => HasFlags(CompileUnitFlags.Force32BitFloats);

        /// <summary>
        /// Returns true iff the current flags contain the <see cref="CompileUnitFlags.EnableDebugInformation"/> flag.
        /// </summary>
        public bool UseDebugInformation => HasFlags(CompileUnitFlags.EnableDebugInformation);

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the associated backend.
        /// </summary>
        public Backend Backend { get; }

        /// <summary>
        /// Returns the target platform.
        /// </summary>
        public TargetPlatform Platform => Backend.Platform;

        /// <summary>
        /// Returns the native LLVM context.
        /// </summary>
        internal LLVMContextRef LLVMContext => Context.LLVMContext;

        /// <summary>
        /// Returns the native LLVM module.
        /// </summary>
        [CLSCompliant(false)]
        public LLVMModuleRef LLVMModule { get; private set; }

        /// <summary>
        /// Returns the native int-pointer type.
        /// </summary>
        internal Type IntPtrType => Backend.IntPtrType;

        /// <summary>
        /// Returns the native LLVM int-pointer type.
        /// </summary>
        internal LLVMTypeRef NativeIntPtrType => GetType(IntPtrType);

        /// <summary>
        /// Returns all compiled methods.
        /// </summary>
        public IReadOnlyDictionary<MethodBase, Method> Methods => methodMapping;

        /// <summary>
        /// Returns all compiled types.
        /// </summary>
        public IReadOnlyDictionary<Type, MappedType> Types => typeMapping;

        /// <summary>
        /// Returns a function-pass manager that is run on new generated methods.
        /// </summary>
        internal LLVMPassManagerRef CodeGenFunctionPassManager { get; private set; }

        /// <summary>
        /// Returns the custom device-function handlers.
        /// </summary>
        public IReadOnlyList<IDeviceFunctions> DeviceFunctions => deviceFunctions;

        /// <summary>
        /// Returns the custom device-type handlers.
        /// </summary>
        public IReadOnlyList<IDeviceTypes> DeviceTypes => deviceTypes;

        /// <summary>
        /// Returns the current compilation context.
        /// </summary>
        internal CompilationContext CompilationContext { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Registers the given device-function handlers.
        /// </summary>
        /// <param name="handler">The device-function handler to register.</param>
        public void RegisterDeviceFunctions(IDeviceFunctions handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            deviceFunctions.Add(handler);
        }

        /// <summary>
        /// Registers the given device-type handlers.
        /// </summary>
        /// <param name="handler">The device-type handler to register.</param>
        public void RegisterDeviceTypes(IDeviceTypes handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            deviceTypes.Add(handler);
        }

        /// <summary>
        /// Returns true iff the given flags are set.
        /// </summary>
        /// <param name="flags">The flags to check.</param>
        /// <returns>True, iff the given flags are set.</returns>
        public bool HasFlags(CompileUnitFlags flags)
        {
            return (Flags & flags) == flags;
        }

        /// <summary>
        /// Tries to remap a method invocation to another invocation.
        /// </summary>
        /// <param name="context">The invocation context.</param>
        /// <returns>True, iff the method was handled successfully.</returns>
        internal InvocationContext? RemapIntrinsic(InvocationContext context)
        {
            for (int i = 0, e = deviceFunctions.Count; i < e; ++i)
            {
                var function = deviceFunctions[i];
                InvocationContext? result;
                if ((result = function.Remap(context)) != null)
                    return result;
            }
            return null;
        }

        /// <summary>
        /// Tries to handle a method invocation in a custom device-function handler.
        /// </summary>
        /// <param name="context">The invocation context.</param>
        /// <param name="result">The resulting stack value.</param>
        /// <returns>True, iff the method was handled successfully.</returns>
        internal bool HandleIntrinsic(InvocationContext context, out Value? result)
        {
            for (int i = 0, e = deviceFunctions.Count; i < e; ++i)
            {
                var function = deviceFunctions[i];
                if (function.Handle(context, out result))
                    return true;
            }
            result = null;
            return false;
        }

        /// <summary>
        /// Converts the given class or struct type into a corresponding LLVM type.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The mapped LLVM type.</returns>
        public MappedType GetObjectType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (type.IsInterface)
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedInterfaceType, type);
            if (type.GetTypeInfo().GenericTypeParameters.Length > 0)
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedGenericType, type);
            if (type.IsTreatedAsPtr())
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedPointerToObjectType, type);

            // Check cache
            if (typeMapping.TryGetValue(type, out MappedType result))
                return result;

            // Check for custom mappings
            for (int i = 0, e = deviceTypes.Count; i < e; ++i)
            {
                if ((result = deviceTypes[i].MapType(type)) != null)
                {
                    typeMapping.Add(type, result);
                    return result;
                }
            }

            // Disable class types for now
            if (!type.IsValueType)
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedClassType, type);
            result = GetStructType(type);

            typeMapping.Add(type, result);
            return result;
        }

        /// <summary>
        /// Converts the given fixed-buffer struct into a corresponding LLVM type.
        /// </summary>
        /// <param name="fba">The fixed-buffer attribute.</param>
        /// <param name="type">The type to convert.</param>
        /// <returns>The mapped LLVM type.</returns>
        private LLVMTypeRef GetFixedBufferType(FixedBufferAttribute fba, Type type)
        {
            Debug.Assert(fba != null, "Invalid fixed-buffer attribute");
            Debug.Assert(type != null, "Invalid type");

            if (!typeMapping.TryGetValue(type, out MappedType mappedType))
            {
                var elementType = GetType(fba.ElementType);
                var llvmType = LLVM.ArrayType(elementType, (uint)fba.Length);
                var fieldOffsets = new Dictionary<FieldInfo, int>
                {
                    [type.GetFields()[0]] = 0
                };
                mappedType = new MappedType(type, llvmType, fba.Length, fieldOffsets);
                typeMapping.Add(type, mappedType);
            }
            return mappedType.LLVMType;
        }

        /// <summary>
        /// Converts the given struct or class type into a corresponding LLVM type.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The mapped LLVM type.</returns>
        private MappedType GetStructType(Type type)
        {
            Debug.Assert(type != null, "Invalid type");
            if (type.IsPrimitive)
                throw CompilationContext.GetArgumentException(
                    ErrorMessages.NotSupportedPrimitiveToObjectType, type);
            if (!type.IsValueType)
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedClassType, type);

            var nullableBaseType = Nullable.GetUnderlyingType(type);
            if (nullableBaseType != null)
                return GetNullableType(type, nullableBaseType);

            // Resolve fields
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // Check for explicit struct layout
            var structLayout = type.StructLayoutAttribute;
            if (structLayout != null)
            {
                if (structLayout.CharSet != CharSet.Ansi)
                    throw CompilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedStructDueToNonAnsiCharSet, type);
                if (structLayout.Value != LayoutKind.Sequential)
                    throw CompilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedStructDueToNonSequentialMemoryLayout, type);
                if (structLayout.Size > 0 && fields.Length != 0)
                    throw CompilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedStructDueToExplicitSize, type);
            }

            // Convert fields
            var entries = new List<LLVMTypeRef>(fields.Length);
            var fieldOffsets = new Dictionary<FieldInfo, int>();

            for (int i = 0, e = fields.Length; i < e; ++i)
            {
                abiSpecification.AlignField(fields, i, entries);

                var fieldOffset = entries.Count;
                var field = fields[i];
                LLVMTypeRef fieldType;
                var fba = field.GetCustomAttribute<FixedBufferAttribute>();
                if (fba != null)
                    fieldType = GetFixedBufferType(fba, field.FieldType);
                else
                    fieldType = GetType(field.FieldType);

                entries.Add(fieldType);
                fieldOffsets[field] = fieldOffset;
            }

            abiSpecification.AlignType(type, entries);

            var resultType = LLVMContext.StructCreateNamed(GetLLVMName(type));
            resultType.StructSetBody(entries.ToArray(), false);
            return new MappedType(type, resultType, entries.Count, fieldOffsets);
        }

        /// <summary>
        /// Converts the given nullable type into a corresponding LLVM type.
        /// </summary>
        /// <param name="type">The nullable type to convert.</param>
        /// <param name="baseType">The encapsulated type of the given nullable type.</param>
        /// <returns>The mapped LLVM type.</returns>
        private MappedType GetNullableType(Type type, Type baseType)
        {
            const int NumExpectedFields = 2;
            Debug.Assert(type != null, "Invalid type");
            Debug.Assert(Nullable.GetUnderlyingType(type) == baseType, "Invalid base type");
            var nullableType = LLVMContext.StructCreateNamed(GetLLVMName(type.ToString(), "Nullable"));
            var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            if (fields.Length != NumExpectedFields)
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedType, type);
            var boolFlagOffset = fields[0].FieldType == typeof(bool) ? 0 : 1;
            var typeOffset = 1 - boolFlagOffset;
            var fieldOffsets = new Dictionary<FieldInfo, int>()
            {
                { fields[boolFlagOffset], boolFlagOffset },
                { fields[typeOffset], typeOffset }
            };
            var structTypes = new LLVMTypeRef[NumExpectedFields];
            structTypes[boolFlagOffset] = LLVMContext.Int1TypeInContext();
            structTypes[typeOffset] = GetType(baseType);
            nullableType.StructSetBody(structTypes, false);
            return new MappedType(type, nullableType, structTypes.Length, fieldOffsets);
        }

        /// <summary>
        /// Converts the given array type into a corresponding LLVM type.
        /// </summary>
        /// <param name="type">The array type to convert.</param>
        /// <returns>The mapped LLVM type.</returns>
        public MappedType GetArrayType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (!type.IsArray)
                throw CompilationContext.GetArgumentException(
                    ErrorMessages.WrongArrayType, type);
            if (!type.GetElementType().IsValueType)
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedArrayElementType, type);
            if (!typeMapping.TryGetValue(type, out MappedType result))
            {
                var arrayType = LLVMContext.StructCreateNamed(GetLLVMName(type.ToString(), "Array"));
                arrayType.StructSetBody(new LLVMTypeRef[]
                {
                    LLVM.PointerType(GetType(type.GetElementType()), 0),
                    LLVMContext.Int32TypeInContext()
                }, false);
                result = new MappedType(type, arrayType, 2, null);
                typeMapping.Add(type, result);
            }
            return result;
        }

        /// <summary>
        /// Tries to convert the given basic-value type to a LLVM type.
        /// </summary>
        /// <param name="valueType">The basic-value type to convert.</param>
        /// <param name="type">The target type.</param>
        /// <returns>True, iff the basic-value type could be converted.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetBasicType(BasicValueType valueType, out LLVMTypeRef type)
        {
            switch (valueType)
            {
                case BasicValueType.UInt1:
                    type = LLVMContext.Int1TypeInContext();
                    break;
                case BasicValueType.Int8:
                case BasicValueType.UInt8:
                    type = LLVMContext.Int8TypeInContext();
                    break;
                case BasicValueType.Int16:
                case BasicValueType.UInt16:
                    type = LLVMContext.Int16TypeInContext();
                    break;
                case BasicValueType.Int32:
                case BasicValueType.UInt32:
                    type = LLVMContext.Int32TypeInContext();
                    break;
                case BasicValueType.Int64:
                case BasicValueType.UInt64:
                    type = LLVMContext.Int64TypeInContext();
                    break;
                case BasicValueType.Single:
                    type = LLVMContext.FloatTypeInContext();
                    break;
                case BasicValueType.Double:
                    type = Force32BitFloats ?
                        LLVMContext.FloatTypeInContext() :
                        LLVMContext.DoubleTypeInContext();
                    break;
                default:
                    type = default(LLVMTypeRef);
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Converts the given basic-value type into an LLVM type.
        /// </summary>
        /// <param name="valueType"></param>
        /// <returns>The converted LLVM type.</returns>
        [CLSCompliant(false)]
        public LLVMTypeRef GetType(BasicValueType valueType)
        {
            if (valueType == BasicValueType.Ptr)
                return NativeIntPtrType;
            if (!TryGetBasicType(valueType, out LLVMTypeRef result))
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedType, valueType);
            return result;
        }

        /// <summary>
        /// Converts the given .Net type into the corresponding LLVM type.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The converted LLVM type.</returns>
        [CLSCompliant(false)]
        public LLVMTypeRef GetType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (type == typeof(decimal))
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedType, typeof(decimal));
            if (type.GetTypeInfo().GenericTypeParameters.Length > 0)
                throw new NotSupportedException();
            if (type.IsTreatedAsPtr())
                return LLVM.PointerType(GetType(type.GetElementType()), 0);
            if (type.IsArray)
                return GetArrayType(type).LLVMType;
            if (type == typeof(void))
                return LLVMContext.VoidTypeInContext();
            if (type == typeof(string))
                return LLVM.PointerType(LLVMContext.Int8TypeInContext(), 0);
            if (type == typeof(IntPtr) || type == typeof(UIntPtr))
                return LLVM.PointerType(LLVMContext.VoidTypeInContext(), 0);
            // Assume CharSet = ANSI
            if (type == typeof(char))
                return LLVMContext.Int8TypeInContext();
            if (TryGetBasicType(type.GetBasicValueType(), out LLVMTypeRef result))
                return result;
            return GetObjectType(type).LLVMType;
        }

        /// <summary>
        /// Returns a LLVM type that represents the signature of the method.
        /// </summary>
        /// <param name="method">The method that has to be converted to a LLVM type.</param>
        /// <returns>A LLVM type that represents the signature of the method</returns>
        [CLSCompliant(false)]
        public LLVMTypeRef GetType(MethodBase method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            var methodParams = method.GetParameters();
            var @params = new LLVMTypeRef[methodParams.Length + (method.IsStatic ? 0 : 1)];
            int offset = 0;
            if (!method.IsStatic)
            {
                var declTypeArgs = method.DeclaringType.GetGenericArguments();
                for (int j = 0, e = declTypeArgs.Length; j < e; ++j)
                {
                    if (declTypeArgs[j].IsGenericParameter)
                        throw new NotSupportedException();
                }
                // Instance pointer is always passed by "reference"
                @params[0] = LLVM.PointerType(GetType(method.DeclaringType), 0);
                ++offset;
            }
            for (int i = 0, e = methodParams.Length; i < e; ++i)
            {
                // Pass structures by value and classes by "reference"
                var methodParamType = methodParams[i].ParameterType.GetLLVMTypeRepresentation();
                var paramType = GetType(methodParamType);
                @params[i + offset] = paramType;
            }
            var methodInfo = method as MethodInfo;
            var returnType = methodInfo != null ? GetType(methodInfo.ReturnType) : LLVMContext.VoidTypeInContext();
            return LLVM.FunctionType(returnType, @params, false);
        }

        /// <summary>
        /// Returns a LLVM method that represents the given method.
        /// </summary>
        /// <param name="methodBase">The method to compile.</param>
        /// <param name="create">True, iff the method should be created in case of an unknown method.</param>
        /// <returns>The corrensponding LLVM method.</returns>
        public Method GetMethod(MethodBase methodBase, bool create)
        {
            if (methodBase == null)
                throw new ArgumentNullException(nameof(methodBase));
            if (methodBase.DeclaringType == typeof(string))
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedStringOperation);
            CompilationContext.EnterMethod(methodBase);
            if (!methodMapping.TryGetValue(methodBase, out Method method) && create)
            {
                method = new Method(this, methodBase);
                methodMapping.Add(methodBase, method);
                method.Decompile(this);
                needsOptimization = true;
            }
            CompilationContext.LeaveMethod(methodBase);
            return method;
        }

        /// <summary>
        /// Returns a LLVM method that represents the given method.
        /// </summary>
        /// <param name="methodBase">The method to compile.</param>
        /// <returns>The corrensponding LLVM method.</returns>
        public Method GetMethod(MethodBase methodBase)
        {
            return GetMethod(methodBase, true);
        }

        /// <summary>
        /// Converts a .Net value into its LLVM representation.
        /// </summary>
        /// <param name="type">The type of the value to convert.</param>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted LLVM value.</returns>
        [CLSCompliant(false)]
        public LLVMValueRef GetValue(Type type, object value)
        {
            if (value != null && type != value.GetType())
                throw CompilationContext.GetArgumentException(
                    ErrorMessages.MismatchingTypes, type, value.GetType());
            var context = LLVMContext;
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return LLVMExtensions.ConstInt(context.Int1TypeInContext(), (bool)value ? 1 : 0, false);
                case TypeCode.SByte:
                    return LLVMExtensions.ConstInt(context.Int8TypeInContext(), (sbyte)value, true);
                case TypeCode.Int16:
                    return LLVMExtensions.ConstInt(context.Int16TypeInContext(), (short)value, true);
                case TypeCode.Int32:
                    return LLVMExtensions.ConstInt(context.Int32TypeInContext(), (int)value, true);
                case TypeCode.Int64:
                    return LLVMExtensions.ConstInt(context.Int64TypeInContext(), (long)value, true);
                case TypeCode.Byte:
                    return LLVMExtensions.ConstInt(context.Int8TypeInContext(), (byte)value, false);
                case TypeCode.UInt16:
                    return LLVMExtensions.ConstInt(context.Int16TypeInContext(), (ushort)value, false);
                case TypeCode.UInt32:
                    return LLVMExtensions.ConstInt(context.Int32TypeInContext(), (uint)value, false);
                case TypeCode.UInt64:
                    return LLVM.ConstInt(context.Int64TypeInContext(), (ulong)value, false);
                case TypeCode.Single:
                    return LLVM.ConstReal(context.FloatTypeInContext(), (float)value);
                case TypeCode.Double:
                    if (Force32BitFloats)
                        return LLVM.ConstReal(context.FloatTypeInContext(), (float)value);
                    else
                        return LLVM.ConstReal(context.DoubleTypeInContext(), (double)value);
                case TypeCode.Char:
                    return LLVM.ConstInt(context.Int8TypeInContext(), (byte)value, false);
                case TypeCode.String:
                    return LLVMExtensions.ConstStringInContext(context, (string)value, false);
                case TypeCode.Object:
                    if (type == typeof(IntPtr))
                        return LLVMExtensions.ConstInt(NativeIntPtrType, ((IntPtr)value).ToInt64(), true);
                    else if (type == typeof(UIntPtr))
                        return LLVM.ConstInt(NativeIntPtrType, ((UIntPtr)value).ToUInt64(), true);
                    else
                        return GetObjectValue(GetObjectType(type), value);
                default:
                    throw CompilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedValue, value, type);
            }
        }

        /// <summary>
        /// Converts a .Net object value into its LLVM representation.
        /// </summary>
        /// <param name="type">The type of the value to convert.</param>
        /// <param name="instanceValue">The .Net instance of the value to convert.</param>
        /// <returns>The converted LLVM value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LLVMValueRef GetObjectValue(MappedType type, object instanceValue)
        {
            if (instanceValue == null)
                return LLVM.ConstNull(type.LLVMType);

            var instanceType = type.ManagedType;
            var hasSupportedBase = instanceType.HasSupportedBaseClass();
            var elements = new LLVMValueRef[type.NumLLVMTypeElements];

            // Fill all elements will zero
            var elementTypes = type.LLVMType.GetStructElementTypes();
            for (int i = 0, e = elements.Length; i < e; ++i)
                elements[i] = LLVM.ConstNull(elementTypes[i]);

            // Fill with real values
            foreach (var field in type.Fields)
            {
                var fieldValue = field.Key.GetValue(instanceValue);
                var llvmFieldValue = GetValue(field.Key.FieldType, fieldValue);
                elements[field.Value] = llvmFieldValue;
            }

            if (hasSupportedBase)
            {
                // We have to handle the base-class fields
                var baseClassValue = Convert.ChangeType(instanceValue, instanceType.BaseType);
                var mappedBaseType = GetStructType(instanceType.BaseType);
                var llvmBaseClassValue = GetObjectValue(mappedBaseType, baseClassValue);
                elements[0] = llvmBaseClassValue;
            }

            return LLVM.ConstNamedStruct(type.LLVMType, elements);
        }

        /// <summary>
        /// Verifies this unit.
        /// </summary>
        public void Verify()
        {
            if (LLVM.VerifyModule(
                LLVMModule,
                LLVMVerifierFailureAction.LLVMReturnStatusAction,
                out IntPtr errorMessage))
                throw new InvalidOperationException(string.Format(
                    ErrorMessages.LLVMModuleVerificationFailed,
                    Marshal.PtrToStringAnsi(errorMessage)));
        }

        /// <summary>
        /// Optimizes all methods in this module.
        /// </summary>
        public void Optimize()
        {
            if (!needsOptimization)
                return;
            needsOptimization = false;

#if DEBUG
            Verify();
#endif
            LLVM.RunPassManager(Context.OptimizeModulePassManager, LLVMModule);
        }

        // Name handling

        /// <summary>
        /// Creates a new unique name in the LLVM world based on the primary
        /// <paramref name="name"/> and the given <paramref name="category"/>.
        /// </summary>
        /// <param name="name">The primary name.</param>
        /// <param name="category">The category of the given name (like Type or Method).</param>
        /// <returns>A new composed name in the LLVM world.</returns>
        public string GetLLVMName(string name, string category)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentNullException(nameof(category));
            name = name.Substring(0, Math.Min(name.Length, MaxLLVMNameLength));
            return $"{category}{memberIdCounter++}_{LLVMNameRegex.Replace(name, "_")}";
        }

        /// <summary>
        /// Creates a new unique name for the given method in the LLVM world.
        /// </summary>
        /// <param name="methodBase">The method.</param>
        /// <param name="category">The category of the given method (like Method or Kernel).</param>
        /// <returns>A name for the given method in the LLVM world.</returns>
        public string GetLLVMName(MethodBase methodBase, string category)
        {
            if (methodBase == null)
                throw new ArgumentNullException(nameof(methodBase));
            var typeName = methodBase.DeclaringType.FullName;
            var name = typeName.Substring(0, Math.Min(typeName.Length, MaxLLVMNameLength / 2)) +
                methodBase.Name.Substring(0, Math.Min(methodBase.Name.Length, MaxLLVMNameLength / 2));
            return GetLLVMName(name, category);
        }

        /// <summary>
        /// Creates a new unique name for the given method in the LLVM world.
        /// </summary>
        /// <param name="methodBase">The method.</param>
        /// <returns>A name for the given method in the LLVM world.</returns>
        public string GetLLVMName(MethodBase methodBase)
        {
            return GetLLVMName(methodBase, "Method");
        }

        /// <summary>
        /// Creates a new unique name for the given type in the LLVM world.
        /// </summary>
        /// <param name="type">The Type.</param>
        /// <returns>A name for the given type in the LLVM world.</returns>
        public string GetLLVMName(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            return GetLLVMName(type.ToString(), "Type");
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            Dispose(ref abiSpecification);
            if (CodeGenFunctionPassManager.Pointer != IntPtr.Zero)
            {
                LLVM.DisposePassManager(CodeGenFunctionPassManager);
                CodeGenFunctionPassManager = default(LLVMPassManagerRef);
            }

            if (LLVMModule.Pointer != IntPtr.Zero)
            {
                LLVM.DisposeModule(LLVMModule);
                LLVMModule = default(LLVMModuleRef);
            }
        }

        #endregion
    }
}
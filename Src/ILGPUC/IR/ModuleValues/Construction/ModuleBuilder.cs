// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ModuleBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using static ILGPUC.IR.TypeInformationManager;

namespace ILGPUC.IR.ModuleValues.Construction;

/// <summary>
/// Represents a fully features module builder.
/// </summary>
partial class ModuleBuilder : IGenerationObject
{
    #region Nested Types

    /// <summary>
    /// A method visitor to store visited methods.
    /// </summary>
    /// <param name="capacity">The initial capacity.</param>
    private ref struct MethodVisitor(int capacity) : ITraversalVisitor<Method>
    {
        private InlineList<Method> _methods = InlineList<Method>.Create(capacity);

        /// <summary>
        /// Visits the given method and adds this method to the underlying list.
        /// </summary>
        public void Visit(Method value) => _methods.Add(value);

        /// <summary>
        /// Exposes the internally stored inline list.
        /// </summary>
        public readonly InlineList<Method> ToInlineList() => _methods;
    }

    #endregion

    #region Instance

    private readonly BasicValueTypeMap<PrimitiveType> _basicValueTypes = [];
    private readonly Dictionary<TypeValue, TypeValue> _unifiedTypes = [];
    private readonly Dictionary<(Type, MemoryAddressSpace), TypeValue> _typeMapping = [];
    private readonly Dictionary<Type, TypeValue> _classStructCache = new(8);
    private readonly HashSet<Type> _classStructInProgress = new(4);

    private InlineList<Global> _globals = InlineList<Global>.Create(32);
    private readonly Dictionary<MethodBase, Method> _methodMapping = new(32);
    private readonly Dictionary<MethodHandle, MethodBuilder> _methods = new(32);

    private int _numValues;

    /// <summary>
    /// Constructs a new module builder.
    /// </summary>
    /// <param name="properties">The compilation properties.</param>
    /// <param name="generation">The current generation.</param>
    /// <param name="location">The location of the module.</param>
    /// <param name="typeInformationManager">The shared type information manager.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public ModuleBuilder(
        CompilationProperties properties,
        Generation generation,
        Location location,
        TypeInformationManager typeInformationManager)
    {
        TypeInformationManager = typeInformationManager;
        Properties = properties;
        Generation = generation;
        Location = location;

        // Initialize module
        Module = new Module(generation, location);

        // Initialize types
        T AddNewType<T>(T typeValue) where T : TypeValue
        {
            _unifiedTypes.Add(typeValue, typeValue);
            return typeValue;
        }

        // Initialize main types
        KindType = AddNewType(new KindType(GetInitializer()));
        VoidType = AddNewType(new VoidType(GetInitializer()));
        StringType = AddNewType(new StringType(GetInitializer()));
        HandleType = AddNewType(new HandleType(GetInitializer()));

        // Initialize basic value types
        foreach (var type in Enum.GetValues<BasicValueType>())
            _basicValueTypes.Add(type, new PrimitiveType(GetInitializer(), type));
        if (properties.MathMode == MathMode.Fast32BitOnly)
        {
            _basicValueTypes[BasicValueType.Float64] =
                _basicValueTypes[BasicValueType.Float32];
        }

        TargetPlatform = properties.TargetPlatform;
        IntPointerArithmeticType = properties.TargetPlatform.Is64Bit()
            ? ArithmeticBasicValueType.UInt64
            : ArithmeticBasicValueType.UInt32;
        IntPointerType = GetPrimitiveType(properties.TargetPlatform.Is64Bit()
            ? BasicValueType.Int64
            : BasicValueType.Int32);

        Padding8Type = AddNewType(new PaddingType(
            GetInitializer(),
            GetPrimitiveType(BasicValueType.Int8)));
        Padding16Type = AddNewType(new PaddingType(
            GetInitializer(),
            GetPrimitiveType(BasicValueType.Int16)));
        Padding32Type = AddNewType(new PaddingType(
            GetInitializer(),
            GetPrimitiveType(BasicValueType.Int32)));
        Padding64Type = AddNewType(new PaddingType(
            GetInitializer(),
            GetPrimitiveType(BasicValueType.Int64)));

        // Populate type mapping
        foreach (var space in AddressSpaces.Spaces)
        {
            _typeMapping.Add((typeof(void), space), VoidType);
            _typeMapping.Add((typeof(string), space), StringType);
            _typeMapping.Add((typeof(Int128), space), CreateInt128Type());
            _typeMapping.Add((typeof(UInt128), space), CreateInt128Type());

            _typeMapping.Add((typeof(Array), space), HandleType);
            _typeMapping.Add((typeof(RuntimeFieldHandle), space), HandleType);
            _typeMapping.Add((typeof(RuntimeMethodHandle), space), HandleType);
            _typeMapping.Add((typeof(RuntimeTypeHandle), space), HandleType);
        }

        // Populate unified types
        foreach (var basicValueType in Enum.GetValues<BasicValueType>())
        {
            var basicType = GetPrimitiveType(basicValueType);
            AddNewType(basicType);
        }

        // Initialize static values
        UndefinedValue = new(GetInitializer());

        // Finish module initialization
        Module.Initialize(this);
    }

    /// <summary>
    /// Creates the structure type for <see cref="Int128"/>.
    /// This is considered an intrinsic in the .Net world, and is internally aligned
    /// to 16 bytes.
    /// </summary>
    private TypeValue CreateInt128Type()
    {
        // Force 16 byte alignment.
        var builder = CreateStructureType(2);
        builder.Add(GetPrimitiveType(BasicValueType.Int64));
        builder.Add(GetPrimitiveType(BasicValueType.Int64));
        builder.Alignment = 16;
        return UnifyType(builder.Seal());
    }

    /// <summary>
    /// Returns a new module initializer for value initialization.
    /// </summary>
    private ModuleValueInitializer GetInitializer(Location? location = null)
    {
        RegisterNewValue();
        return new(this, location ?? Location);
    }

    /// <summary>
    /// Registers a new value.
    /// </summary>
    private void RegisterNewValue() => ++_numValues;

    #endregion

    #region Properties

    /// <summary>
    /// Returns associated compilation properties.
    /// </summary>
    public CompilationProperties Properties { get; }

    /// <summary>
    /// Returns the current generation.
    /// </summary>
    public Generation Generation { get; }

    /// <summary>
    /// Returns the current location.
    /// </summary>
    public Location Location { get; }

    /// <summary>
    /// Returns the underlying module reference of the incomplete module to be built.
    /// </summary>
    public Module Module { get; }

    /// <summary>
    /// Returns the type of all types.
    /// </summary>
    public KindType KindType { get; }

    /// <summary>
    /// Returns the void type.
    /// </summary>
    public VoidType VoidType { get; }

    /// <summary>
    /// Returns the memory type.
    /// </summary>
    public StringType StringType { get; }

    /// <summary>
    /// Returns the managed handle type.
    /// </summary>
    public HandleType HandleType { get; }

    /// <summary>
    /// Returns a custom padding type that is used to pad structure values (8-bits).
    /// </summary>
    public PaddingType Padding8Type { get; }

    /// <summary>
    /// Returns a custom padding type that is used to pad structure values (16-bits).
    /// </summary>
    public PaddingType Padding16Type { get; }

    /// <summary>
    /// Returns a custom padding type that is used to pad structure values (32-bits).
    /// </summary>
    public PaddingType Padding32Type { get; }

    /// <summary>
    /// Returns a custom padding type that is used to pad structure values (64-bits).
    /// </summary>
    public PaddingType Padding64Type { get; }

    /// <summary>
    /// Represents a undefined value in this generation.
    /// </summary>
    public UndefinedValue UndefinedValue { get; }

    #endregion

    #region Pointers

    /// <summary>
    /// Returns the current target platform.
    /// </summary>
    public TargetPlatform TargetPlatform { get; }

    /// <summary>
    /// Returns the arithmetic type of a native pointer.
    /// </summary>
    public ArithmeticBasicValueType IntPointerArithmeticType { get; }

    /// <summary>
    /// Returns the basic type of a native pointer.
    /// </summary>
    public BasicValueType IntPointerBasicValueType => IntPointerType.BasicValueType;

    /// <summary>
    /// Returns the type of a native pointer.
    /// </summary>
    public PrimitiveType IntPointerType { get; }

    /// <summary>
    /// Returns the pointer size of a native pointer type.
    /// </summary>
    public int IntPointerSize => IntPointerType.Size;

    /// <summary>
    /// Returns the method building belonging to the given method handle.
    /// </summary>
    /// <param name="methodHandle">The method handle.</param>
    /// <returns>The method builder.</returns>
    public MethodBuilder this[MethodHandle methodHandle] => _methods[methodHandle];

    #endregion

    #region Types

    /// <summary>
    /// Returns the underlying type information manager.
    /// </summary>
    public TypeInformationManager TypeInformationManager { get; }

    /// <summary>
    /// Resolves the primitive type that corresponds to the given
    /// <see cref="BasicValueType"/>.
    /// </summary>
    /// <param name="basicValueType">The basic value type.</param>
    /// <returns>The created primitive type.</returns>
    public PrimitiveType GetPrimitiveType(BasicValueType basicValueType) =>
        _basicValueTypes[basicValueType];

    /// <summary>
    /// Resolves the padding type that corresponds to the given
    /// <see cref="BasicValueType"/>.
    /// </summary>
    /// <param name="basicValueType">The basic value type.</param>
    /// <returns>The padding type.</returns>
    public PaddingType GetPaddingType(BasicValueType basicValueType) =>
        basicValueType switch
        {
            BasicValueType.Int8 => Padding8Type,
            BasicValueType.Int16 or BasicValueType.Float16 => Padding16Type,
            BasicValueType.Int32 or BasicValueType.Float32 => Padding32Type,
            BasicValueType.Int64 or BasicValueType.Float64 => Padding64Type,
            _ => throw new ArgumentOutOfRangeException(nameof(basicValueType)),
        };

    /// <summary>
    /// Resolves type information for the given type.
    /// </summary>
    /// <param name="type">The type to resolve.</param>
    /// <returns>The resolved type information.</returns>
    public TypeInformation GetTypeInfo(Type type) =>
        TypeInformationManager.GetTypeInfo(type);

    /// <summary>
    /// Creates a pointer type.
    /// </summary>
    /// <param name="elementType">The pointer element type.</param>
    /// <param name="addressSpace">The address space.</param>
    /// <returns>The created pointer type.</returns>
    public PointerType CreatePointerType(
        TypeValue elementType,
        MemoryAddressSpace addressSpace) =>
        UnifyType(new PointerType(GetInitializer(), elementType, addressSpace));

    /// <summary>
    /// Creates a view type.
    /// </summary>
    /// <param name="elementType">The view element type.</param>
    /// <param name="addressSpace">The address space.</param>
    /// <returns>The created view type.</returns>
    public ViewType CreateViewType(
        TypeValue elementType,
        MemoryAddressSpace addressSpace) =>
        UnifyType(new ViewType(GetInitializer(), elementType, addressSpace));

    /// <summary>
    /// Creates an empty structure type.
    /// </summary>
    /// <returns>The type representing an empty structure.</returns>
    public TypeValue CreateEmptyStructureType() => GetPrimitiveType(BasicValueType.Int8);

    /// <summary>
    /// Creates a new structure type builder with the given capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity.</param>
    /// <returns>The created structure builder.</returns>
    public StructureType.Builder CreateStructureType(int capacity) =>
        new(this, capacity, 0);

    /// <summary>
    /// Creates a new structure type.
    /// </summary>
    /// <param name="builder">The current builder.</param>
    /// <returns>The created type.</returns>
    [SuppressMessage(
        "Style",
        "IDE0046:Convert to conditional expression",
        Justification = "Avoid nested if conditionals")]
    internal TypeValue FinishStructureType(in StructureType.Builder builder)
    {
        if (builder.Count < 1)
            return CreateEmptyStructureType();

        return builder.Count < 2
            ? builder[0]
            : UnifyType(new StructureType(GetInitializer(), builder));
    }

    /// <summary>
    /// Creates a new array type.
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <param name="dimensions">The number of array dimensions.</param>
    /// <returns>The created array type.</returns>
    public ArrayType CreateArrayType(TypeValue elementType, int dimensions) =>
        dimensions < 1
        ? throw new NotSupportedException(
            string.Format(
                ErrorMessages.NotSupportedArrayDimension,
                dimensions.ToString()))
        : UnifyType(new ArrayType(GetInitializer(), elementType, dimensions));

    /// <summary>
    /// Creates a new type based on a type from the .Net world.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <returns>The IR type.</returns>
    public TypeValue CreateType(Type type) =>
        CreateType(type, MemoryAddressSpace.Generic);

    /// <summary>
    /// Creates a new type based on a type from the .Net world.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <param name="addressSpace">The address space for pointer types.</param>
    /// <returns>The IR type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TypeValue CreateType(Type type, MemoryAddressSpace addressSpace)
    {
        // Avoid querying the cache for primitive types
        var basicValueType = type.GetBasicValueType();
        if (basicValueType != BasicValueType.None)
            return GetPrimitiveType(basicValueType);

        // Explicitly check the local cache for a potential type
        if (_typeMapping.TryGetValue((type, addressSpace), out var result))
            return result;

        if (type.IsEnum)
        {
            // Do not store enum types
            return CreateType(type.GetEnumUnderlyingType(), addressSpace);
        }
        else if (type.IsArray)
        {
            var arrayElementType = CreateType(
                type.GetElementType().AsNotNull(),
                addressSpace);
            var dimension = type.GetArrayRank();
            return Map(
                type,
                addressSpace,
                CreateArrayType(arrayElementType, dimension));
        }
        else if (type.IsArrayViewType(out Type? elementType))
        {
            return Map(
                type,
                addressSpace,
                CreateViewType(
                    CreateType(elementType, addressSpace),
                    addressSpace));
        }
        else if (type.IsVoidPtr())
        {
            return Map(
                type,
                addressSpace,
                CreatePointerType(VoidType, addressSpace));
        }
        else if (type.IsByRef || type.IsPointer)
        {
            return Map(
                type,
                addressSpace,
                CreatePointerType(
                    CreateType(type.GetElementType().AsNotNull(), addressSpace),
                    addressSpace));
        }
        else if (type.IsClass)
        {
            if (type.IsDelegate())
            {
                // Delegate types (Func<T>, Action<T>, etc.) are represented as
                // opaque pointers — we don't model the CLR delegate internals.
                return Map(
                    type,
                    addressSpace,
                    CreatePointerType(IntPointerType, MemoryAddressSpace.Generic));
            }
            else if (type.IsSealed ||
                     type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            {
                // Sealed and compiler-generated classes (closure classes) are
                // represented as pointers to their struct layout.
                var structType = CreateClassStructureType(type);
                return Map(
                    type,
                    addressSpace,
                    CreatePointerType(structType, MemoryAddressSpace.Generic));
            }
            else
            {
                throw new NotSupportedException(
                    string.Format(
                        ErrorMessages.NotSupportedClassType,
                        type));
            }
        }
        else
        {
            PrepareStructureType(type, addressSpace, out var builder);
            return Map(
                type,
                addressSpace,
                UnifyType(builder.Seal()));
        }
    }

    /// <summary>
    /// Creates (or returns cached) the StructureType layout for a class type.
    /// Unlike CreateType, this returns the raw struct layout without the outer pointer
    /// wrapper.
    /// </summary>
    /// <param name="type">The class type to build a struct layout for.</param>
    /// <returns>The StructureType representing the class field layout.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal TypeValue CreateClassStructureType(Type type)
    {
        // Check cache first using a sentinel address space
        const MemoryAddressSpace ClassStructSpace = MemoryAddressSpace.Local;
        if (_classStructCache.TryGetValue(type, out var cached))
            return cached;

        // Detect recursive types
        if (!_classStructInProgress.Add(type))
        {
            throw new NotSupportedException(
                string.Format(
                    ErrorMessages.NotSupportedRecursiveReferenceType,
                    type));
        }

        try
        {
            var typeInfo = GetTypeInfo(type);
            var builder = new StructureType.Builder(
                this,
                typeInfo.NumFlattenedFields,
                typeInfo.Size);
            foreach (var field in typeInfo.Fields)
            {
                // Use CreateType for each field — class-typed fields become pointers
                builder.Add(CreateType(field.FieldType, ClassStructSpace));
            }
            var result = UnifyType(builder.Seal());
            _classStructCache[type] = result;
            return result;
        }
        finally
        {
            _classStructInProgress.Remove(type);
        }
    }

    /// <summary>
    /// Maps the given type and address space to the type node provided.
    /// </summary>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="type">The managed type.</param>
    /// <param name="addressSpace">The address space.</param>
    /// <param name="typeNode">The type node to map to.</param>
    /// <returns>The given type node.</returns>
    private T Map<T>(Type type, MemoryAddressSpace addressSpace, T typeNode)
        where T : TypeValue
    {
        _typeMapping[(type, addressSpace)] = typeNode;
        return typeNode;
    }

    /// <summary>
    /// Creates a new structure type based on a type from the .Net world.
    /// The builder is returned so that further customization can be performed.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <param name="addressSpace">The address space for pointer types.</param>
    /// <param name="builder">Filled in with the structure builder.</param>
    private void PrepareStructureType(
        Type type,
        MemoryAddressSpace addressSpace,
        out StructureType.Builder builder)
    {
        // Must be a structure type
        if (!type.IsValueType)
        {
            throw new NotSupportedException(
                string.Format(
                    ErrorMessages.NotSupportedType,
                    type));
        }

        var typeInfo = GetTypeInfo(type);
        builder = new StructureType.Builder(
            this,
            typeInfo.NumFlattenedFields,
            typeInfo.Size);
        foreach (var field in typeInfo.Fields)
            builder.Add(CreateType(field.FieldType, addressSpace));
    }

    /// <summary>
    /// Specializes the address space of the given <see cref="TypeValue"/>.
    /// </summary>
    /// <param name="type">The source type.</param>
    /// <param name="provider">The address-space provider for all fields.</param>
    /// <returns>The created specialized <see cref="AddressSpaceType"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TypeValue? TrySpecializeAddressSpace(
        TypeValue type,
        Func<FieldAccess, MemoryAddressSpace> provider)
    {
        if (type is AddressSpaceType addressSpaceType)
        {
            return SpecializeAddressSpaceType(
                addressSpaceType,
                provider(default));
        }

        if (type is StructureType structureType)
        {
            var structBuilder = CreateStructureType(structureType.NumFields);

            // All fields are flat - just specialize each field's address space
            for (int i = 0; i < structureType.NumFields; i++)
            {
                var fieldType = structureType[i];
                var specializedFieldType = fieldType;

                // Specialize address space types using per-field analysis
                if (fieldType is AddressSpaceType fieldAddrType)
                {
                    var addressSpace = provider(new(i));

                    specializedFieldType = SpecializeAddressSpaceType(
                        fieldAddrType,
                        addressSpace);
                }
                structBuilder.Add(specializedFieldType);
            }

            var result = structBuilder.Seal();
            return result == type ? null : result;
        }

        return null;
    }

    /// <summary>
    /// Specializes the address space of the given <see cref="AddressSpaceType"/>.
    /// </summary>
    /// <param name="addressSpaceType">The source type.</param>
    /// <param name="addressSpace">The new address space.</param>
    /// <returns>The created specialized <see cref="AddressSpaceType"/>.</returns>
    public AddressSpaceType SpecializeAddressSpaceType(
        AddressSpaceType addressSpaceType,
        MemoryAddressSpace addressSpace)
    {
        if (addressSpaceType is PointerType pointerType)
        {
            return CreatePointerType(
                pointerType.ElementType,
                addressSpace);
        }
        else
        {
            var viewType = addressSpaceType.AsNotNullCast<ViewType>();
            return CreateViewType(
                viewType.ElementType,
                addressSpace);
        }
    }

    /// <summary>
    /// Tries to specialize a view or a pointer address space.
    /// </summary>
    /// <param name="type">The pointer or view type.</param>
    /// <param name="addressSpace">The target address space.</param>
    /// <param name="specializedType">The specialized type.</param>
    /// <returns>True, if the type could be specialized.</returns>
    public bool TrySpecializeAddressSpaceType(
        TypeValue type,
        MemoryAddressSpace addressSpace,
        [NotNullWhen(true)] out TypeValue? specializedType)
    {
        specializedType = type is AddressSpaceType addressSpaceType
            ? SpecializeAddressSpaceType(addressSpaceType, addressSpace)
            : null;
        return specializedType is not null;
    }

    /// <summary>
    /// Creates a type.
    /// </summary>
    /// <typeparam name="T">The type of the  type.</typeparam>
    /// <param name="type">The type to create.</param>
    /// <returns>The created type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T UnifyType<T>(T type) where T : TypeValue
    {
        if (_unifiedTypes.TryGetValue(type, out var result))
            return result.AsNotNullCast<T>();
        _unifiedTypes.Add(type, type);
        return type;
    }

    #endregion

    #region Globals

    /// <summary>
    /// Tries to retrieve information for a global value.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="value">The IR value.</param>
    /// <returns>A tuple of a primitive box and a method reference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private (PrimitiveValueBox? Box, Method? Method) TryGetPrimitiveValue(
        Location location,
        Value? value)
    {
        if (value is null)
            return (null, null);

        if (value is Method initializerMethod)
        {
            location.Assert(
                initializerMethod.Module == Module &&
                initializerMethod.NumParameters == 0);

            // Try to retrieve constant initializer value
            var returnValue = initializerMethod.EntryBlock.TerminationValue;
            if (returnValue is PrimitiveValue primitiveValue)
                return (primitiveValue.Box, null);
        }
        else if (value is PureValue pureValue)
        {
            // For runtime-sized arrays, the arithmetic tree may
            // contain MethodValues (Parameters). Skip assertion
            // if any non-PureValue child is found — the value
            // will be wrapped in an initializer method below.
            bool allPure = true;
            pureValue.VisitDFS(childValue =>
            {
                if (childValue is not PureValue)
                    allPure = false;
            });
            if (!allPure)
                return (null, null);

            // Test whether we can fold the array length computation
            if (pureValue is PrimitiveValue primitiveValue)
            {
                return (primitiveValue.Box, null);
            }
            else
            {
                // We have to create an initializer method and seal it
                var handle = MethodHandle.Create("value_initializer");
                var methodBuilder = GetOrCreateMethod(
                    new MethodDeclaration(handle, pureValue.Type, MethodFlags.NoInline));

                var rewriter = methodBuilder.CreatePureValueRewriter();
                var newPureValue = pureValue.Rewrite(rewriter);

                methodBuilder.EntryBuilder.CreateReturnTermination(newPureValue);
                return (null, methodBuilder.Seal());
            }
        }

        // MethodValue (e.g., Parameters used as runtime array sizes).
        // These can't be folded to a constant. Create an initializer
        // method that just returns the value. The Global stores a
        // Method reference for the array length computation.
        if (value is MethodValue)
            return (null, null);

        throw new UnreachableException();
    }

    /// <summary>
    /// Creates a new initialized global with the given initializer.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="arrayLength">The array length to allocate.</param>
    /// <param name="mallocType">The allocation type.</param>
    /// <param name="addressSpace">The target address space.</param>
    /// <param name="valueInitializer">The initializer to use.</param>
    /// <returns>The created node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Global? CreateGlobal(
        Location location,
        TypeValue? mallocType,
        MemoryAddressSpace addressSpace,
        Value? arrayLength = null,
        Value? valueInitializer = null)
    {
        if (mallocType is null) return null;

        location.Assert(
            arrayLength is null or Method or PureValue or MethodValue,
            $"Invalid arrayLength type: {arrayLength?.GetType().Name}");
        location.Assert(
            valueInitializer is null or Method or PureValue or MethodValue,
            $"Invalid valueInitializer type: {valueInitializer?.GetType().Name}");

        // Try to determine array length and initializer
        var arrayLengthInfo = TryGetPrimitiveValue(location, arrayLength);
        var valueInitializerInfo = TryGetPrimitiveValue(location, valueInitializer);

        return CreateGlobalDirect(
            location,
            mallocType,
            addressSpace,
            arrayLengthInfo.Method,
            valueInitializerInfo.Method,
            arrayLengthInfo.Box,
            valueInitializerInfo.Box);
    }

    /// <summary>
    /// Creates a new initialized global with the given initializer.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="arrayLength">The array length to allocate.</param>
    /// <param name="mallocType">The allocation type.</param>
    /// <param name="addressSpace">The target address space.</param>
    /// <param name="valueInitializer">The initializer to use.</param>
    /// <param name="rawArrayLength">The raw array length as box.</param>
    /// <param name="rawValueInitializer">The raw value initializer as box.</param>
    /// <returns>The created node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal Global? CreateGlobalDirect(
        Location location,
        TypeValue? mallocType,
        MemoryAddressSpace addressSpace,
        Value? arrayLength = null,
        Value? valueInitializer = null,
        PrimitiveValueBox? rawArrayLength = null,
        PrimitiveValueBox? rawValueInitializer = null)
    {
        if (mallocType is null) return null;
        if (arrayLength is null && !rawArrayLength.HasValue) return null;

        var global = new Global(
            GetInitializer(location),
            mallocType,
            addressSpace,
            arrayLength as Method,
            valueInitializer as Method,
            rawArrayLength,
            rawValueInitializer);

        _globals.Add(global);
        return global;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Creates a method declaration from the given method base.
    /// </summary>
    /// <param name="methodBase">The source method base.</param>
    /// <returns>The method declaration created.</returns>
    public MethodDeclaration CreateMethodDeclaration(MethodBase methodBase)
    {
        var handle = MethodHandle.Create(methodBase.Name);
        var flags = Method.ResolveMethodFlags(methodBase);
        var returnType = CreateType(methodBase.GetReturnType());

        return new(handle, returnType, methodBase, flags);
    }

    /// <summary>
    /// A delegate for the creation of a method builder.
    /// </summary>
    /// <param name="initializer">The module value initializer.</param>
    /// <returns>The created method builder.</returns>
    internal delegate MethodBuilder CreateMethodBuilder(
        in ModuleValueInitializer initializer);

    /// <summary>
    /// Gets the method corresponding to the provided managed method base.
    /// </summary>
    /// <param name="methodBase">The method base to map to an IR method.</param>
    /// <returns>The IR method.</returns>
    public Method GetMethod(MethodBase methodBase) => _methodMapping[methodBase];

    /// <summary>
    /// Gets or creates a method corresponding to the given method declaration.
    /// </summary>
    /// <param name="declaration">The method declaration.</param>
    /// <param name="createBuilder">
    /// The optional handler to create a method builder.
    /// </param>
    /// <returns>The method builder.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public MethodBuilder GetOrCreateMethod(
        MethodDeclaration declaration,
        CreateMethodBuilder? createBuilder = null)
    {
        // Try to disambiguate the declaration
        if (declaration.Source is not null &&
            _methodMapping.TryGetValue(declaration.Source, out var existingMethod))
        {
            return _methods[existingMethod.Declaration.Handle];
        }

        // Check methods
        if (_methods.TryGetValue(declaration.Handle, out var tempBuilder))
            return tempBuilder;

        // Declare new method
        var initializer = GetMethodInitializer(declaration);
        var builder = createBuilder?.Invoke(initializer) ??
            new MethodBuilder(this, initializer, declaration);

        _methods.Add(declaration.Handle, builder);
        if (declaration.Source is not null)
            _methodMapping.Add(declaration.Source, builder.Method);

        return builder;
    }

    /// <summary>
    /// Creates a method corresponding to the given old method from a previous generation.
    /// </summary>
    /// <param name="oldMethod">The previous method.</param>
    /// <param name="createBuilder">
    /// The optional handler to create a method builder.
    /// </param>
    /// <returns>The method builder.</returns>
    public MethodBuilder CreateMethodFromOldMethod(
        Method oldMethod,
        CreateMethodBuilder? createBuilder = null)
    {
        Generation.ValidatePreviousGeneration(oldMethod);
        return GetOrCreateMethod(oldMethod.Declaration, createBuilder);
    }

    /// <summary>
    /// Gets a new method initializer.
    /// </summary>
    /// <param name="declaration">The method declaration.</param>
    /// <returns>The module value initializer.</returns>
    internal ModuleValueInitializer GetMethodInitializer(MethodDeclaration declaration) =>
        GetInitializer(new Method.MethodLocation(declaration.Source));

    #endregion

    #region Sealing

    /// <summary>
    /// Seals this module builder by specifying an entry point.
    /// </summary>
    /// <param name="entryPointHandle">The entry point to use.</param>
    /// <returns>The sealed module.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Module Seal(MethodHandle entryPointHandle)
    {
        // Seal all methods first (skip External methods — they may have
        // incomplete blocks from a failed code generation attempt)
        foreach (var method in _methods.Values)
        {
            if (method.Method.IsExternal)
                continue;
            method.Seal();
        }

        int totalNumValues = 0;
        foreach (var method in _methods.Values)
            totalNumValues += method.NumValues;

        // Traverse all methods from the entry point to ensure ordering and tracking of
        // used methods only (implicit UCE)
        var entryPoint = _methods[entryPointHandle].Method;
        var methodVisitor = new MethodVisitor(_methods.Count);
        ReversePostOrder<Method>.Traverse<
            ValueSet<Module, Method>,
            MethodVisitor,
            Method.SuccessorsProvider,
            Forwards>(
            entryPoint,
            new ValueSet<Module, Method>(Module, _numValues),
            ref methodVisitor);

        // Get ordered methods (exclude External methods — they were
        // declared but never compiled, so they have no valid block structure)
        var allMethods = methodVisitor.ToInlineList();
        var orderedMethods = InlineList<Method>.Create(allMethods.Count);
        foreach (var m in allMethods)
        {
            if (!m.IsExternal)
                orderedMethods.Add(m);
        }

        // Compute all uses for all methods and nested values
        var usesSet = new GlobalValueSet(Generation, totalNumValues);
        foreach (var method in orderedMethods)
        {
#if DEBUG
            try
            {
#endif
                method.ComputeUses(usesSet);
#if DEBUG
            }
            catch (Exception ex) when (ex is not IRVerificationException)
            {
                // Walk the method to find the exact stale value chain
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[ModuleBuilder.Seal] ComputeUses failed in method " +
                    $"{method.ToReferenceString()} (gen={method.Generation})");

                // Check Method._values (includes entry BasicBlock and parameters)
                sb.AppendLine($"  Method._values count={method.Count}:");
                foreach (var mv in method.Values)
                {
                    sb.AppendLine($"    _value: {mv.GetType().Name}:" +
                        $"{mv.ToReferenceString()} " +
                        $"(gen={mv.Generation}, sealed={mv.IsSealed})");
                }

                // Deep recursive scan for stale values
                var visited = new HashSet<Value>(ReferenceEqualityComparer.Instance);
                foreach (var block in method.Blocks)
                {
                    foreach (var (bbValue, _) in block.BasicBlockValues)
                    {
                        FindStaleDeep(sb, bbValue, block, Generation, visited);
                    }
                    if (block.TerminationValue is { } tv)
                        FindStaleDeep(sb, tv, block, Generation, visited);
                }
                Console.Error.WriteLine(sb.ToString());
                Console.Error.Flush();
                throw new InvalidOperationException(sb.ToString(), ex);
            }
#endif
        }

        // Remove unused globals (implicit DCE)
        var usedGlobals = InlineList<Global>.Create(_globals.Count);
        foreach (var global in _globals)
        {
            if (global.HasUses)
                usedGlobals.Add(global);
        }
#if DEBUG
        usedGlobals.Sort(Value.Comparison);
#endif

        // Get all types referenced in this module
        var usedTypes = InlineList<TypeValue>.Create(_unifiedTypes.Count / 2);
        foreach (var type in _unifiedTypes.Keys)
        {
            if (type.HasUses)
                usedTypes.Add(type);
        }

        // Seal the parent module
        Module.Seal(
            totalNumValues,
            _numValues,
            orderedMethods.AsReadOnlyMemory(),
            usedGlobals.AsReadOnlyMemory(),
            usedTypes.AsReadOnlyMemory(),
            entryPointHandle);

        return Module;
    }

    #endregion

#if DEBUG
    /// <summary>
    /// Recursively finds stale operands in a value's entire operand tree (debug
    /// diagnostics). Traverses into ALL operand types (BBValues, Methods, PureValues).
    /// </summary>
    private static void FindStaleDeep(
        System.Text.StringBuilder sb,
        Value root,
        BasicBlock block,
        Generation gen,
        HashSet<Value> visited,
        string path = "",
        int depth = 0)
    {
        if (depth > 15 || !visited.Add(root)) return;
        if (path.Length == 0)
            path = $"{root.GetType().Name}:{root.ToReferenceString()}";

        foreach (var op in root.Values)
        {
            var opDesc = $"{op.GetType().Name}:{op.ToReferenceString()}(gen=" +
                $"{op.Generation})";
            if (op.Generation != gen)
            {
                sb.AppendLine($"  STALE-DEEP in {block.ToReferenceString()}: " +
                    $"{path} → {opDesc}");
            }
            // Recurse into ALL value types
            FindStaleDeep(sb, op, block, gen, visited,
                $"{path} → {opDesc}", depth + 1);
        }

        // Also check the Type reference
        if (root.Type is not null && root.Type.Generation != gen)
        {
            sb.AppendLine($"  STALE-TYPE in {block.ToReferenceString()}: " +
                $"{path} has type {root.Type.GetType().Name}:" +
                $"{root.Type.ToReferenceString()}(gen={root.Type.Generation})");
        }
    }
#endif
}

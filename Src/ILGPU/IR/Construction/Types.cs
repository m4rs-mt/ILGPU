// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Types.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a primitive type.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The created primitive type.</returns>
        public PrimitiveType CreatePrimitiveType(BasicValueType basicValueType) =>
            Context.GetPrimitiveType(basicValueType);

        /// <summary>
        /// Creates a pointer type.
        /// </summary>
        /// <param name="elementType">The pointer element type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The created pointer type.</returns>
        public PointerType CreatePointerType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
        {
            return Context.CreateType(new PointerType(
                elementType,
                addressSpace));
        }

        /// <summary>
        /// Creates a view type.
        /// </summary>
        /// <param name="elementType">The view element type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The created view type.</returns>
        public ViewType CreateViewType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
        {
            return Context.CreateType(new ViewType(
                elementType,
                addressSpace));
        }

        /// <summary>
        /// Creates a new structure type.
        /// </summary>
        /// <param name="fieldTypes">The structure field types.</param>
        /// <returns>The created structure type.</returns>
        public StructureType CreateStructureType(ImmutableArray<TypeNode> fieldTypes) =>
            CreateStructureType(fieldTypes, ImmutableArray<string>.Empty, null);

        /// <summary>
        /// Creates a new structure type.
        /// </summary>
        /// <param name="fieldTypes">The structure field types.</param>
        /// <param name="fieldNames">The structure field names.</param>
        /// <param name="sourceType">The source structure type.</param>
        /// <returns>The created structure type.</returns>
        public StructureType CreateStructureType(
            ImmutableArray<TypeNode> fieldTypes,
            ImmutableArray<string> fieldNames,
            Type sourceType)
        {
            return Context.CreateType(new StructureType(
                fieldTypes,
                fieldNames,
                sourceType));
        }

        /// <summary>
        /// Creates a new structure type.
        /// </summary>
        /// <param name="fieldTypes">The structure field types.</param>
        /// <param name="sourceType">The source structure type.</param>
        /// <returns>The created structure type.</returns>
        public StructureType CreateStructureType(
            ImmutableArray<TypeNode> fieldTypes,
            StructureType sourceType)
        {
            Debug.Assert(sourceType != null, "Invalid source type");
            Debug.Assert(sourceType.NumChildren == fieldTypes.Length, "Incompatible field types");
            return CreateStructureType(fieldTypes, sourceType.Names, sourceType.Source);
        }

        /// <summary>
        /// Creates a new function type.
        /// </summary>
        /// <param name="parameterTypes">The parameter types.</param>
        /// <returns>The created function type.</returns>
        public FunctionType CreateFunctionType(ImmutableArray<TypeNode> parameterTypes) =>
            CreateFunctionType(parameterTypes, null);

        /// <summary>
        /// Creates a new function type.
        /// </summary>
        /// <param name="parameterTypes">The parameter types.</param>
        /// <param name="sourceType">The source type in the .Net world.</param>
        /// <returns>The created function type.</returns>
        public FunctionType CreateFunctionType(
            ImmutableArray<TypeNode> parameterTypes,
            Type sourceType)
        {
            return Context.CreateType(new FunctionType(
                parameterTypes,
                ImmutableArray<string>.Empty,
                sourceType));
        }

        /// <summary>
        /// Creates a new function type.
        /// </summary>
        /// <param name="parameterTypes">The parameter types.</param>
        /// <param name="parameterNames">The parameter names.</param>
        /// <param name="sourceType">The source type in the .Net world.</param>
        /// <returns>The created function type.</returns>
        internal FunctionType CreateFunctionType(
            ImmutableArray<TypeNode> parameterTypes,
            ImmutableArray<string> parameterNames,
            Type sourceType)
        {
            return Context.CreateType(new FunctionType(
                parameterTypes,
                parameterNames,
                sourceType));
        }

        /// <summary>
        /// Creates a new type based on a type from the .Net world.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>The IR type.</returns>
        public TypeNode CreateType(Type type) =>
            CreateType(type, MemoryAddressSpace.Generic);

        /// <summary>
        /// Creates a new type based on a type from the .Net world.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <param name="addressSpace">The address space for pointer types.</param>
        /// <returns>The IR type.</returns>
        public TypeNode CreateType(Type type, MemoryAddressSpace addressSpace)
        {
            var typeInformationManager = Context.TypeInformationManager;
            type = typeInformationManager.MapType(type);

            Debug.Assert(!type.IsArray, "Invalid array type");

            var basicValueType = type.GetBasicValueType();
            if (basicValueType != BasicValueType.None)
                return CreatePrimitiveType(basicValueType);
            else if (type.IsEnum)
                return CreateType(type.GetEnumUnderlyingType(), addressSpace);
            else if (type == typeof(void))
                return VoidType;
            else if (type == typeof(string))
                return StringType;
            else if (type.IsArrayViewType(out Type elementType))
                return CreateViewType(CreateType(elementType, addressSpace), addressSpace);
            else if (type.IsVoidPtr())
                return CreatePointerType(VoidType, addressSpace);
            else if (type.IsByRef || type.IsPointer)
            {
                return CreatePointerType(
                    CreateType(type.GetElementType(), addressSpace),
                    addressSpace);
            }
            else if (type.IsDelegate())
            {
                var invokeMethod = type.GetDelegateInvokeMethod();
                var parameters = invokeMethod.GetParameters();
                var parameterTypes = ImmutableArray.CreateBuilder<TypeNode>(parameters.Length);
                var parameterNames = ImmutableArray.CreateBuilder<string>(parameters.Length);
                foreach (var parameter in parameters)
                {
                    parameterTypes.Add(CreateType(parameter.ParameterType, addressSpace));
                    parameterNames.Add(parameter.Name);
                }
                return CreateFunctionType(
                    parameterTypes.MoveToImmutable(),
                    parameterNames.MoveToImmutable(),
                    type);
            }
            else
            {
                // Struct type
                Debug.Assert(type.IsValueType, "Invalid struct type");
                var typeInfo = typeInformationManager.GetTypeInfo(type);
                var fieldTypes = ImmutableArray.CreateBuilder<TypeNode>(typeInfo.NumFields);
                var fieldNames = ImmutableArray.CreateBuilder<string>(typeInfo.NumFields);
                foreach (var fieldInfo in typeInfo.Fields)
                {
                    fieldTypes.Add(CreateType(fieldInfo.FieldType, addressSpace));
                    fieldNames.Add(fieldInfo.Name);
                }
                return CreateStructureType(
                    fieldTypes.MoveToImmutable(),
                    fieldNames.MoveToImmutable(),
                    type);
            }
        }

        /// <summary>
        /// Specializes the address space of the given <see cref="AddressSpaceType"/>.
        /// </summary>
        /// <param name="addressSpaceType">The source type.</param>
        /// <param name="addressSpace">The new address space.</param>
        /// <returns>The created specialzized <see cref="AddressSpaceType"/>.</returns>
        public AddressSpaceType SpecializeAddressSpaceType(
            AddressSpaceType addressSpaceType,
            MemoryAddressSpace addressSpace)
        {
            Debug.Assert(addressSpaceType != null, "Invalid address space type");

            if (addressSpaceType is PointerType pointerType)
                return CreatePointerType(
                    pointerType.ElementType,
                    addressSpace);
            else
            {
                var viewType = addressSpaceType as ViewType;
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
        /// <returns>True, iff the type could be specialized.</returns>
        public bool TrySpecializeAddressSpaceType(
            TypeNode type,
            MemoryAddressSpace addressSpace,
            out TypeNode specializedType)
        {
            Debug.Assert(type != null, "Invalid type");

            if (type is AddressSpaceType addressSpaceType)
                specializedType = SpecializeAddressSpaceType(
                    addressSpaceType,
                    addressSpace);
            else
                specializedType = null;
            return specializedType != null;
        }
    }
}

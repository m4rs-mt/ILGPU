// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: TypeInformationManager.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents the base interface for custom types that require a specific mapping.
    /// </summary>
    public interface ITypeMapper
    {
        /// <summary>
        /// Maps the given type to a custom type.
        /// </summary>
        /// <param name="type">The type to map.</param>
        /// <returns>The mapped result type, or null iff the type could not be mapped.</returns>
        Type MapType(Type type);
    }

    /// <summary>
    /// Represents a context that manages type information.
    /// </summary>
    public sealed class TypeInformationManager : DisposeBase
    {
        #region Instance

        private readonly ReaderWriterLockSlim mappingLock = new ReaderWriterLockSlim();
        private readonly ReaderWriterLockSlim cachingLock = new ReaderWriterLockSlim();
        private readonly Dictionary<Type, ManagedTypeInfo> typeInfoMapping =
            new Dictionary<Type, ManagedTypeInfo>();
        private readonly List<ITypeMapper> typeMappers = new List<ITypeMapper>();

        /// <summary>
        /// Constructs a new type context.
        /// </summary>
        public TypeInformationManager() { }

        #endregion

        #region Methods

        /// <summary>
        /// Registers the given type mapper.
        /// Such a mapper allows to intercept 
        /// </summary>
        /// <param name="typeMapper"></param>
        public void RegisterTypeMapper(ITypeMapper typeMapper)
        {
            mappingLock.EnterWriteLock();
            try
            {
                typeMappers.Add(typeMapper);
            }
            finally
            {
                mappingLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Resolves type information for the given type.
        /// </summary>
        /// <param name="type">The type to resolve.</param>
        /// <returns>The resolved type information.</returns>
        public ManagedTypeInfo GetTypeInfo(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (type.IsPrimitive || type.IsPointer())
                throw new ArgumentException("Not supported type", nameof(type));
            cachingLock.EnterUpgradeableReadLock();
            try
            {
                if (!typeInfoMapping.TryGetValue(type, out ManagedTypeInfo typeInfo))
                    type = MapType(type);
                if (!typeInfoMapping.TryGetValue(type, out typeInfo))
                {
                    cachingLock.EnterWriteLock();
                    try
                    {
                        typeInfo = new ManagedTypeInfo(type);
                        typeInfoMapping.Add(type, typeInfo);
                    }
                    finally
                    {
                        cachingLock.ExitWriteLock();
                    }
                }
                return typeInfo;
            }
            finally
            {
                cachingLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Maps a type using the given address space for pointers.
        /// </summary>
        /// <param name="type">The type to map.</param>
        /// <returns>The mapped type.</returns>
        public Type MapType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!type.IsEnum && !type.IsDelegate() &&
                (type.IsClass || type.IsInterface) && type != typeof(string) &&
                !type.IsPointer() || type.IsArray)
                throw new NotSupportedException(
                    string.Format(ErrorMessages.NotSupportedType, type.GetStringRepresentation()));

            mappingLock.EnterReadLock();
            try
            {
                foreach (var typeMapper in typeMappers)
                    type = typeMapper.MapType(type);
            }
            finally
            {
                mappingLock.ExitReadLock();
            }

            return type;
        }

        /// <summary>
        /// Clears the cached type information.
        /// </summary>
        public void Clear()
        {
            cachingLock.EnterWriteLock();
            try
            {
                typeInfoMapping.Clear();
            }
            finally
            {
                cachingLock.ExitWriteLock();
            }
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            mappingLock.Dispose();
            cachingLock.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Represents a type information about a managed type.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class ManagedTypeInfo
    {
        #region Instance

        /// <summary>
        /// Constructs a new type information.
        /// </summary>
        /// <param name="type">The .Net type.</param>
        internal ManagedTypeInfo(Type type)
        {
            Debug.Assert(type != null, "Invalid type");
            ManagedType = type;
            var fieldArray = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(fieldArray, (left, right) =>
            {
                var leftOffset = Marshal.OffsetOf(type, left.Name).ToInt32();
                var rightOffset = Marshal.OffsetOf(type, right.Name).ToInt32();
                return leftOffset.CompareTo(rightOffset);
            });
            Fields = ImmutableArray.Create(fieldArray);
            var fieldIndicesBuilder = ImmutableDictionary.CreateBuilder<FieldInfo, int>();
            var fieldTypesBuilder = ImmutableArray.CreateBuilder<Type>(Fields.Length);
            var fieldIndex = 0;
            foreach (var field in Fields)
            {
                fieldIndicesBuilder.Add(field, fieldIndex++);
                fieldTypesBuilder.Add(field.FieldType);
            }
            FieldIndices = fieldIndicesBuilder.ToImmutable();
            FieldTypes = fieldTypesBuilder.MoveToImmutable();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the .Net type.
        /// </summary>
        public Type ManagedType { get; }

        /// <summary>
        /// Returns the number of fields.
        /// </summary>
        public int NumFields => Fields.Length;

        /// <summary>
        /// Resolves the index of the given field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>The field index.</returns>
        public int this[FieldInfo field] => FieldIndices[field];

        /// <summary>
        /// Returns all fields.
        /// </summary>
        public ImmutableArray<FieldInfo> Fields { get; }

        /// <summary>
        /// Returns all field types.
        /// </summary>
        public ImmutableArray<Type> FieldTypes { get; }

        /// <summary>
        /// Maps field information to field indices.
        /// </summary>
        public ImmutableDictionary<FieldInfo, int> FieldIndices { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve the field of the given field.
        /// </summary>
        /// <param name="index">The target index.</param>
        /// <param name="field">The resolved field.</param>
        /// <returns>True, if the field could be resolved.</returns>
        public bool TryResolveField(int index, out FieldInfo field)
        {
            field = default;
            if (index < 0 || index >= NumFields)
                return false;
            field = Fields[index];
            return true;
        }

        /// <summary>
        /// Tries to resolve an index of the given field.
        /// </summary>
        /// <param name="info">The field.</param>
        /// <param name="index">The target index.</param>
        /// <returns>True, if the field could be resolved.</returns>
        public bool TryResolveIndex(FieldInfo info, out int index)
        {
            return FieldIndices.TryGetValue(info, out index);
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this type.
        /// </summary>
        /// <returns>The string representation of this type.</returns>
        public override string ToString()
        {
            return ManagedType.Name;
        }

        #endregion
    }
}

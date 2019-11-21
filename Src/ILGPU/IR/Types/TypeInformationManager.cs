// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: TypeInformationManager.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

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
    /// Represents a context that manages type information.
    /// </summary>
    public class TypeInformationManager : DisposeBase, ICache
    {
        #region Nested Types

        /// <summary>
        /// Represents a type information about a managed type.
        /// </summary>
        /// <remarks>Members of this class are not thread safe.</remarks>
        public sealed class TypeInformation
        {
            #region Instance

            /// <summary>
            /// Constructs a new type information.
            /// </summary>
            /// <param name="parent">The parent type manager.</param>
            /// <param name="type">The .Net type.</param>
            /// <param name="fields">All managed fields.</param>
            /// <param name="fieldTypes">All managed field types.</param>
            /// <param name="fieldIndices">Maps fields to their indices.</param>
            /// <param name="isBlittable">True, if this type is blittable.</param>
            internal TypeInformation(
                TypeInformationManager parent,
                Type type,
                ImmutableArray<FieldInfo> fields,
                ImmutableArray<Type> fieldTypes,
                ImmutableDictionary<FieldInfo, int> fieldIndices,
                bool isBlittable)
            {
                Debug.Assert(parent != null, "Invalid parent");
                Debug.Assert(type != null, "Invalid type");

                Parent = parent;
                ManagedType = type;
                Fields = fields;
                FieldTypes = fieldTypes;
                FieldIndices = fieldIndices;
                IsBlittable = isBlittable;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent information manager.
            /// </summary>
            public TypeInformationManager Parent { get; }

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

            /// <summary>
            /// Returns true if the associated .Net type is blittable.
            /// </summary>
            public bool IsBlittable { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Gets nested field type information.
            /// </summary>
            /// <param name="index">The field index.</param>
            /// <returns>The resulting type information.</returns>
            public TypeInformation GetFieldTypeInfo(int index) =>
                Parent.GetTypeInfo(FieldTypes[index]);

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

        #endregion

        #region Instance

        private readonly ReaderWriterLockSlim cachingLock = new ReaderWriterLockSlim();
        private readonly Dictionary<Type, TypeInformation> typeInfoMapping =
            new Dictionary<Type, TypeInformation>();

        /// <summary>
        /// Constructs a new type context.
        /// </summary>
        public TypeInformationManager()
        {
            InitIntrinsicTypeInformation();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes intrinsic type information.
        /// </summary>
        private void InitIntrinsicTypeInformation()
        {
            AddTypeInfo(typeof(bool), false);

            AddTypeInfo(typeof(byte), true);
            AddTypeInfo(typeof(ushort), true);
            AddTypeInfo(typeof(uint), true);
            AddTypeInfo(typeof(ulong), true);

            AddTypeInfo(typeof(sbyte), true);
            AddTypeInfo(typeof(short), true);
            AddTypeInfo(typeof(int), true);
            AddTypeInfo(typeof(long), true);

            AddTypeInfo(typeof(float), true);
            AddTypeInfo(typeof(double), true);

            AddTypeInfo(typeof(char), false);
            AddTypeInfo(typeof(string), false);
        }

        /// <summary>
        /// Resolves type information for the given type.
        /// </summary>
        /// <param name="type">The type to resolve.</param>
        /// <returns>The resolved type information.</returns>
        public TypeInformation GetTypeInfo(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            cachingLock.EnterUpgradeableReadLock();
            try
            {
                if (!typeInfoMapping.TryGetValue(type, out TypeInformation typeInfo))
                {
                    cachingLock.EnterWriteLock();
                    try
                    {
                        typeInfo = CreateTypeInfo(type);
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
        /// Resolves type information for the given type.
        /// </summary>
        /// <param name="type">The type to resolve.</param>
        /// <returns>The resolved type information.</returns>
        private TypeInformation GetTypeInfoInternal(Type type)
        {
            if (!typeInfoMapping.TryGetValue(type, out TypeInformation typeInfo))
                typeInfo = CreateTypeInfo(type);
            return typeInfo;
        }

        /// <summary>
        /// Adds primitive type information.
        /// </summary>
        /// <param name="type">The type to add.</param>
        /// <param name="isBlittable">True, if this type is blittable.</param>
        /// <returns>The created type information instance.</returns>
        private TypeInformation AddTypeInfo(Type type, bool isBlittable)
        {
            var result = new TypeInformation(
                this,
                type,
                ImmutableArray<FieldInfo>.Empty,
                ImmutableArray<Type>.Empty,
                ImmutableDictionary<FieldInfo, int>.Empty,
                isBlittable);
            typeInfoMapping.Add(type, result);
            return result;
        }

        /// <summary>
        /// Creates new type information and registers the created object
        /// in the internal cache.
        /// </summary>
        /// <param name="type">The base .Net type.</param>
        /// <returns>The created type information object.</returns>
        private TypeInformation CreateTypeInfo(Type type)
        {
            Debug.Assert(type != null, "Invalid type");

            TypeInformation result;

            // Check for pointers and arrays
            if (type.IsPointer || type.IsByRef || type.IsArray)
            {
                var elementInfo = GetTypeInfoInternal(type.GetElementType());
                result = AddTypeInfo(type, elementInfo.IsBlittable);
            }
            // Check for opaque view types
            else if (type.IsArrayViewType(out Type _))
                result = AddTypeInfo(type, false);
            else
            {
                result = CreateCompoundTypeInfo(type);
                typeInfoMapping.Add(type, result);
            }

            return result;
        }

        /// <summary>
        /// Creates new type information for compound types.
        /// </summary>
        /// <param name="type">The base .Net type.</param>
        /// <returns>The created type information object.</returns>
        private TypeInformation CreateCompoundTypeInfo(Type type)
        {
            var fieldArray = type.GetFields(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(fieldArray, (left, right) =>
            {
                var leftOffset = Marshal.OffsetOf(type, left.Name).ToInt32();
                var rightOffset = Marshal.OffsetOf(type, right.Name).ToInt32();
                return leftOffset.CompareTo(rightOffset);
            });
            var fields = ImmutableArray.Create(fieldArray);
            var fieldIndicesBuilder = ImmutableDictionary.CreateBuilder<FieldInfo, int>();
            var fieldTypesBuilder = ImmutableArray.CreateBuilder<Type>(fields.Length);
            var fieldIndex = 0;
            bool isBlittable = !type.IsEnum;
            foreach (var field in fields)
            {
                fieldIndicesBuilder.Add(field, fieldIndex++);
                fieldTypesBuilder.Add(field.FieldType);
                isBlittable &= GetTypeInfoInternal(field.FieldType).IsBlittable;
            }
            var fieldIndices = fieldIndicesBuilder.ToImmutable();
            var fieldTypes = fieldTypesBuilder.MoveToImmutable();

            return new TypeInformation(
                this,
                type,
                fields,
                fieldTypes,
                fieldIndices,
                isBlittable);
        }

        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        public virtual void ClearCache(ClearCacheMode mode)
        {
            cachingLock.EnterWriteLock();
            try
            {
                typeInfoMapping.Clear();
                InitIntrinsicTypeInformation();
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
            if (disposing)
                cachingLock.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }
}

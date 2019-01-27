// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: ILTypeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.PointerViews;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// Generates type in the .Net world.
    /// </summary>
    public sealed class ILTypeGenerator
    {
        #region Nested Types

        private readonly struct TypeVisitor : ITypeNodeVisitor
        {
            public TypeVisitor(ILTypeGenerator parent)
            {
                Parent = parent;
            }

            public ILTypeGenerator Parent { get; }

            /// <summary cref="ITypeNodeVisitor.Visit(VoidType)"/>
            public void Visit(VoidType type) =>
                Parent.AddType(type, typeof(void));

            /// <summary cref="ITypeNodeVisitor.Visit(StringType)"/>
            public void Visit(StringType type) =>
                Parent.AddType(type, typeof(string));

            /// <summary cref="ITypeNodeVisitor.Visit(PrimitiveType)"/>
            public void Visit(PrimitiveType type) =>
                Parent.AddType(type, type.BasicValueType.GetManagedType());

            /// <summary cref="ITypeNodeVisitor.Visit(PointerType)"/>
            public void Visit(PointerType type) =>
                Parent.AddType(type, Parent.GenerateType(type.ElementType).MakePointerType());

            /// <summary cref="ITypeNodeVisitor.Visit(ViewType)"/>
            public void Visit(ViewType type) =>
                Parent.AddType(type, ViewImplementation.GetImplementationType(
                    Parent.GenerateType(type.ElementType)));

            /// <summary cref="ITypeNodeVisitor.Visit(StructureType)"/>
            public void Visit(StructureType type)
            {
                var builder = Parent.Context.DefineRuntimeStruct();

                var fieldInfos = ImmutableArray.CreateBuilder<FieldInfo>(type.NumChildren);
                foreach (var fieldType in type.Children)
                {
                    var field = builder.DefineField(
                        "Field_" + fieldInfos.Count,
                        Parent.GenerateType(fieldType),
                        FieldAttributes.Public);
                    fieldInfos.Add(field);
                }

                Parent.AddType(type, builder.CreateTypeInfo().AsType());
            }
        }

        #endregion

        #region Instance

        private readonly Dictionary<TypeNode, Type> mapping = new Dictionary<TypeNode, Type>();

        /// <summary>
        /// Creates a new IL type generator.
        /// </summary>
        /// <param name="context">The associated context.</param>
        public ILTypeGenerator(Context context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            TypeInformationManager = context.TypeContext;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the underlying type information manager.
        /// </summary>
        public TypeInformationManager TypeInformationManager { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Maps the given type.
        /// </summary>
        /// <param name="typeNode">The type node to map.</param>
        /// <returns>The mapped type entry.</returns>
        private Type MapType(TypeNode typeNode)
        {
            if (typeNode == null)
                throw new ArgumentNullException(nameof(typeNode));
            if (!mapping.TryGetValue(typeNode, out Type entry))
            {
                var visitor = new TypeVisitor(this);
                typeNode.Accept(visitor);
                entry = mapping[typeNode];
            }
            return entry;
        }

        /// <summary>
        /// Generates a .Net type representation for the given type node.
        /// </summary>
        /// <param name="typeNode">The type node.</param>
        /// <returns>The .Net type representation.</returns>
        public Type GenerateType(TypeNode typeNode) =>
            MapType(typeNode);

        private void AddType(TypeNode typeNode, Type type) =>
            mapping.Add(typeNode, type);

        #endregion
    }
}

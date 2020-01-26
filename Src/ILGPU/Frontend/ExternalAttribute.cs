// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: ExternalAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Reflection;

namespace ILGPU.Frontend
{
    /// <summary>
    /// Marks external methods that are opaque in the scope of the ILGPU IR.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ExternalAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new external attribute.
        /// </summary>
        /// <param name="name">The external name.</param>
        public ExternalAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Returns the associated internal function name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Resolves the actual IR name.
        /// </summary>
        /// <param name="method">The source method.</param>
        /// <returns>The IR name.</returns>
        public string GetName(MethodInfo method) =>
            string.IsNullOrEmpty(Name) ? method.Name : Name;
    }
}

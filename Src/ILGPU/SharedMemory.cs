// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: SharedMemory.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Reflection;

namespace ILGPU
{
    /// <summary>
    /// Marks parameters as shared-memory variables.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class SharedMemoryAttribute : Attribute
    {
        #region Static

        /// <summary>
        /// Tries to get the shared-memory count for the given parameter.
        /// </summary>
        /// <param name="parameter">The input parameter information.</param>
        /// <param name="count">The resolved count information.</param>
        /// <returns>True, iff the shared-memory could be determined.</returns>
        internal static bool TryGetSharedMemoryCount(ParameterInfo parameter, out int? count)
        {
            var attr = parameter.GetCustomAttribute<SharedMemoryAttribute>();
            if (attr == null)
            {
                count = null;
                return false;
            }
            else
            {
                count = attr.NumElements;
                return true;
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Marks the parameter as shared-memory variable with
        /// an unspecified number of elements. This number is either
        /// implictly fixed by the use of VariableView to one element,
        /// or the number is dynamically determined by the runtime system.
        /// </summary>
        public SharedMemoryAttribute()
        {
            NumElements = null;
        }

        /// <summary>
        /// Marks the parameter as a shared-memory variable with
        /// the given number of elements.
        /// </summary>
        /// <param name="numElements">The number of elements.</param>
        public SharedMemoryAttribute(int numElements)
        {
            if (numElements < 1)
                throw new ArgumentOutOfRangeException(nameof(numElements));
            NumElements = numElements;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the specified number of elements (if any).
        /// </summary>
        public int? NumElements { get; }

        #endregion

    }
}

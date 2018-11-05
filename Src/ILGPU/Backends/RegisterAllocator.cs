// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: RegisterAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents an abstract register of a specific kind.
    /// </summary>
    /// <typeparam name="TKind">The register kind.</typeparam>
    interface IRegister<TKind>
        where TKind : struct
    {
        /// <summary>
        /// Returns the actual register kind.
        /// </summary>
        TKind Kind { get; }

        /// <summary>
        /// Returns the register index value.
        /// </summary>
        int RegisterValue { get; }
    }

    /// <summary>
    /// Represents a general allocation and free strategy.
    /// </summary>
    /// <typeparam name="TKind">The register kind.</typeparam>
    /// <typeparam name="T">The actual register value type.</typeparam>
    interface IRegisterAllocationBehavior<TKind, T>
        where TKind : struct
        where T : struct, IRegister<TKind>
    {
        /// <summary>
        /// Allocates a register of the given kind.
        /// </summary>
        /// <param name="kind">The register kind to allocate.</param>
        /// <returns></returns>
        T AllocateRegister(TKind kind);

        /// <summary>
        /// Frees the given register.
        /// </summary>
        /// <param name="register">The register to free.</param>
        void FreeRegister(T register);
    }

    /// <summary>
    /// Represents a generic register allocator.
    /// </summary>
    /// <typeparam name="TKind">The register kind.</typeparam>
    /// <typeparam name="T">The actual register value type.</typeparam>
    /// <typeparam name="TBehavior">The allocation behavior.</typeparam>
    /// <remarks>The members of this class are not thread safe.</remarks>
    sealed class RegisterAllocator<TKind, T, TBehavior>
        where TKind : struct
        where T : struct, IRegister<TKind>
        where TBehavior : IRegisterAllocationBehavior<TKind, T>
    {
        #region Nested Types

        /// <summary>
        /// Represents a register mapping entry.
        /// </summary>
        private readonly struct RegisterEntry
        {
            /// <summary>
            /// Constructs a new mapping entry.
            /// </summary>
            /// <param name="register">The register.</param>
            /// <param name="node">The node.</param>
            public RegisterEntry(T register, Value node)
            {
                Register = register;
                Node = node;
            }

            /// <summary>
            /// Returns the associated register.
            /// </summary>
            public T Register { get; }

            /// <summary>
            /// Returns the associated value.
            /// </summary>
            public Value Node { get; }
        }

        #endregion

        #region Instance

        private readonly Dictionary<Value, RegisterEntry> registerLookup =
            new Dictionary<Value, RegisterEntry>();
        private readonly Dictionary<Value, Value> aliases =
            new Dictionary<Value, Value>();

        /// <summary>
        /// Constructs a new register allocator.
        /// </summary>
        /// <param name="behavior">The allocation behavior.</param>
        public RegisterAllocator(TBehavior behavior)
        {
            Behavior = behavior;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated behavior.
        /// </summary>
        public TBehavior Behavior { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Allocates a specific register kind for the given node.
        /// </summary>
        /// <param name="node">The node to allocate the register for.</param>
        /// <param name="kind">The register kind to allocate.</param>
        /// <returns>The allocated register.</returns>
        public T Allocate(Value node, TKind kind)
        {
            Debug.Assert(node != null, "Invalid node");
            if (aliases.TryGetValue(node, out Value alias))
                node = alias;
            if (!registerLookup.TryGetValue(node, out RegisterEntry entry))
            {
                var targetRegister = Behavior.AllocateRegister(kind);
                entry = new RegisterEntry(targetRegister, node);
                registerLookup.Add(node, entry);
            }
            return entry.Register;
        }

        /// <summary>
        /// Regisers a register alias.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="alias">The alias node.</param>
        public void Alias(Value node, Value alias)
        {
            Debug.Assert(node != null, "Invalid node");
            Debug.Assert(alias != null, "Invalid alias");
            if (aliases.TryGetValue(alias, out Value otherAlias))
                alias = otherAlias;
            aliases[node] = alias;
        }

        /// <summary>
        /// Loads the allocated register of the given node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>The allocated register.</returns>
        public T Load(Value node)
        {
            Debug.Assert(node != null, "Invalid node");
            if (aliases.TryGetValue(node, out Value alias))
                node = alias;
            if (!registerLookup.TryGetValue(node, out RegisterEntry entry))
                throw new InvalidCodeGenerationException();
            return entry.Register;
        }

        /// <summary>
        /// Frees the given node.
        /// </summary>
        /// <param name="node">The node to free.</param>
        public void Free(Value node)
        {
            Debug.Assert(node != null, "Invalid node");
            Behavior.FreeRegister(registerLookup[node].Register);
            registerLookup.Remove(node);
        }

        #endregion
    }
}

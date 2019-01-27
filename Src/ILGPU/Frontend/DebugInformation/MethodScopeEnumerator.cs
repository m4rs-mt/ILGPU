// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: MethodScopeEnumerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;

namespace ILGPU.Frontend.DebugInformation
{
    /// <summary>
    /// Represents a scope enumerator for methods.
    /// </summary>
    public sealed class MethodScopeEnumerator : IDebugInformationEnumerator<MethodScope>
    {
        #region Constants

        /// <summary>
        /// Represents an empty scope enumerator.
        /// </summary>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "The empty scope-enumerator is immutable")]
        public static readonly MethodScopeEnumerator Empty = new MethodScopeEnumerator();

        #endregion

        #region Instance

        private readonly Stack<LocalScope> scopes = new Stack<LocalScope>(10);

        /// <summary>
        /// Constructs an empty scope enumerator.
        /// </summary>
        private MethodScopeEnumerator() { }

        /// <summary>
        /// Constructs a new scope enumerator.
        /// </summary>
        /// <param name="methodDebugInformation">The referenced method debug information.</param>
        internal MethodScopeEnumerator(MethodDebugInformation methodDebugInformation)
        {
            Debug.Assert(methodDebugInformation != null, "Invalid method debug information");
            MetadataReader = methodDebugInformation.MetadataReader;
            var localScopes = MetadataReader.GetLocalScopes(methodDebugInformation.DebugInformationHandle).GetEnumerator();
            if (!localScopes.MoveNext())
                return;
            scopes.Push(MetadataReader.GetLocalScope(localScopes.Current));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated metadata reader.
        /// </summary>
        internal MetadataReader MetadataReader { get; }

        /// <summary>
        /// Returns the current scope.
        /// </summary>
        public MethodScope Current =>
            scopes.Count < 1 ? MethodScope.Invalid : new MethodScope(scopes.Peek(), MetadataReader);

        #endregion

        #region Methods

        /// <summary>
        /// Tries to move the scope enumerator to the given offset in bytes.
        /// </summary>
        /// <param name="offset">The target instruction offset in bytes.</param>
        /// <returns>True, iff the enumerator could be moved to the next scope.</returns>
        public bool MoveTo(int offset)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (scopes.Count < 1)
                return false;

            // Pop scopes
            var currentScope = scopes.Peek();
            while (offset < currentScope.StartOffset)
            {
                scopes.Pop();
                Debug.Assert(scopes.Count > 0, "Popped root scope");
                currentScope = scopes.Peek();
            }

            // Push scopes
            var enumerator = currentScope.GetChildren();
            while (enumerator.MoveNext())
            {
                var currentScopeHandle = enumerator.Current;
                var childScope = MetadataReader.GetLocalScope(currentScopeHandle);
                if (offset < childScope.StartOffset)
                    break;
                if (offset < childScope.EndOffset)
                {
                    // Matching scope -> register variables
                    scopes.Push(childScope);
                    enumerator = childScope.GetChildren();
                }
            }

            return scopes.Count > 0;
        }

        #endregion
    }
}

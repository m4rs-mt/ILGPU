// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Verifier.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ILGPU.Tests
{
    sealed class Verifier
    {
        #region Static

        public static bool Verify(
            Context context,
            IRContext irContext,
            Method entryPoint,
            Stream serializedStream)
        {
            var deserialized = IRContext.Deserialize(
                context.TypeInformationManger,
                serializedStream,
                IRContextSerializationMode.Binary,
                IRContextSerializationFlags.None,
                out IRContextDeserializationInfo info);
            var functionHandles = info.TopLevelFunctions;

            var otherHandle = functionHandles.First();
            var verifier = new Verifier(
                irContext,
                entryPoint,
                deserialized,
                deserialized.GetFunction(otherHandle));
            return verifier.Verify();
        }

        #endregion

        #region Instance

        private readonly HashSet<Value> currentVisited = new HashSet<Value>();
        private readonly HashSet<Value> referenceVisited = new HashSet<Value>();

        public Verifier(
            IRContext currentContext,
            TopLevelFunction current,
            IRContext referenceContext,
            TopLevelFunction reference)
        {
            CurrentContext = currentContext;
            Current = current;

            ReferenceContext = referenceContext;
            Reference = reference;
        }

        #endregion

        public IRContext CurrentContext { get; }

        public Method Current { get; }

        public IRContext ReferenceContext { get; }

        public Method Reference { get; }

        public bool Verify()
        {
            currentVisited.Clear();
            referenceVisited.Clear();
            return VerifyNode(Current, Reference);
        }

        private bool VerifyNode(Value current, Value reference)
        {
            if (currentVisited.Contains(current))
            {
                if (!referenceVisited.Contains(reference))
                    return false;
                return true;
            }
            else if (referenceVisited.Contains(reference))
            {
                if (!currentVisited.Contains(current))
                    return false;
                return true;
            }

            currentVisited.Add(current);
            referenceVisited.Add(reference);

            if (current.Nodes.Length != reference.Nodes.Length ||
                !Equals(current, reference))
                return false;

            for (int i = 0, e = current.Nodes.Length; i < e; ++i)
            {
                if (!VerifyNode(current.Nodes[i], reference.Nodes[i]))
                    return false;
            }

            return true;
        }

        private static bool Equals(Value current, Value reference)
        {
            if (current.GetType() != reference.GetType() ||
                current.Type.GetType() != reference.Type.GetType())
                return false;
            var visitor = new Visitor(reference);
            current.Accept(visitor);
            return visitor.Success;
        }
    }
}

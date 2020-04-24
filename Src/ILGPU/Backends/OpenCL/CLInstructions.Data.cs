// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLInstructions.Data.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Runtime.OpenCL;
using System.Collections.Generic;

namespace ILGPU.Backends.OpenCL
{
    partial class CLInstructions
    {
        /// <summary>
        /// An address-of operation.
        /// </summary>
        public const string AddressOfOperation = "&";

        /// <summary>
        /// A dereference operation.
        /// </summary>
        public const string DereferenceOperation = "*";

        /// <summary>
        /// An assignment operation.
        /// </summary>
        public const string AssignmentOperation = "=";

        /// <summary>
        /// The first part of a select operation.
        /// </summary>
        public const string SelectOperation1 = "?";

        /// <summary>
        /// The second part of a select operation.
        /// </summary>
        public const string SelectOperation2 = ":";

        /// <summary>
        /// A structure type prefix.
        /// </summary>
        public const string StructTypePrefix = "struct";

        /// <summary>
        /// A typedef statement.
        /// </summary>
        public const string TypeDefStatement = "typedef";

        /// <summary>
        /// An if statement.
        /// </summary>
        public const string IfStatement = "if";

        /// <summary>
        /// An else statement.
        /// </summary>
        public const string ElseStatement = "else";

        /// <summary>
        /// A break statement.
        /// </summary>
        public const string BreakStatement = "break";

        /// <summary>
        /// A continue statement.
        /// </summary>
        public const string ContinueStatement = "continue";

        /// <summary>
        /// A return statement.
        /// </summary>
        public const string ReturnStatement = "return";

        /// <summary>
        /// A goto statement.
        /// </summary>
        public const string GotoStatement = "goto";

        /// <summary>
        /// A atomic load operation.
        /// </summary>
        public const string AtomicLoadOperation = "atomic_load";

        /// <summary>
        /// A atomic store operation.
        /// </summary>
        public const string AtomicStoreOperation = "atomic_store";

        /// <summary>
        /// An atomic CAS operation.
        /// </summary>
        public const string AtomicCASOperation = "atomic_compare_exchange_strong";

        /// <summary>
        /// An int-as-float operation.
        /// </summary>
        public const string IntAsFloat = "as_float";

        /// <summary>
        /// An long-as-double operation.
        /// </summary>
        public const string LongAsDouble = "as_double";

        /// <summary>
        /// A float-as-int operation.
        /// </summary>
        public const string FloatAsInt = "as_int";

        /// <summary>
        /// A double-as-long operation.
        /// </summary>
        public const string DoubleAsLong = "as_long";

        /// <summary>
        /// Resolves the current global work-item id.
        /// </summary>
        public const string GetGlobalId = "get_global_id";

        /// <summary>
        /// Resolves the current grid size.
        /// </summary>
        public const string GetGridSize = "get_global_size";

        /// <summary>
        /// Resolves the current grid index.
        /// </summary>
        public const string GetGridIndex = "get_group_id";

        /// <summary>
        /// Resolves the current group size.
        /// </summary>
        public const string GetGroupSize = "get_local_size";

        /// <summary>
        /// Resolves the current group index.
        /// </summary>
        public const string GetGroupIndex = "get_local_id";

        /// <summary>
        /// Resolves the current warp size.
        /// </summary>
        public const string GetWarpSize = "get_sub_group_size()";

        /// <summary>
        /// Resolves the current warp index.
        /// </summary>
        public const string GetWarpIndexOperation = "get_sub_group_id()";

        /// <summary>
        /// Resolves the current lane index.
        /// </summary>
        public const string GetLaneIndexOperation = "get_sub_group_local_id()";

        private static readonly string[] MemoryFenceFlags =
        {
            "CLK_GLOBAL_MEM_FENCE",
            "CLK_LOCAL_MEM_FENCE",
        };

        private static readonly string[] AddressSpacePrefixes =
        {
            "__generic",
            "global",
            "local",
            "private",
        };

        private static readonly string[] AddressSpaceCastOperations =
        {
            null,
            "to_global",
            "to_local",
            "to_private",
        };

        private static readonly string[] BarrierOperations =
        {
            "sub_group_barrier",
            "work_group_barrier",
        };

        private static readonly string[] PredicateBarrierOperations =
        {
            null,
            "work_group_all",
            "work_group_any",
        };

        private static readonly string[] MemoryScopes =
        {
            "memory_scope_work_group",
            "memory_scope_device",
            "memory_scope_device"
        };

        private static readonly string[] BroadcastOperations =
        {
            "sub_group_broadcast",
            "work_group_broadcast",
        };

        private static readonly Dictionary<
            (CLAcceleratorVendor, ShuffleKind),
            string> ShuffleOperations =
            new Dictionary<(CLAcceleratorVendor, ShuffleKind), string>()
            {
                {
                    (CLAcceleratorVendor.Intel, ShuffleKind.Generic),
                    "intel_sub_group_shuffle"
                },
                {
                    (CLAcceleratorVendor.Intel, ShuffleKind.Down),
                    "intel_sub_group_shuffle_down"
                },
                {
                    (CLAcceleratorVendor.Intel, ShuffleKind.Up),
                    "intel_sub_group_shuffle_up"
                },
                {
                    (CLAcceleratorVendor.Intel, ShuffleKind.Xor),
                    "intel_sub_group_shuffle_xor"
                },
            };

        private static readonly string[] CompareOperations =
        {
            "==",
            "!=",
            "<",
            "<=",
            ">",
            ">="
        };

        private static readonly Dictionary<
            (UnaryArithmeticKind, bool),
            (string, bool)> UnaryArithmeticOperations =
            new Dictionary<(UnaryArithmeticKind, bool), (string, bool)>()
            {
                // Basic arithmetic
                
                { (UnaryArithmeticKind.Neg, false), ("-", false) },
                { (UnaryArithmeticKind.Neg, true), ("-", false) },

                { (UnaryArithmeticKind.Not, false), ("~", false) },
                { (UnaryArithmeticKind.Not, true), ("~", false) },

                // Functions

                { (UnaryArithmeticKind.Abs, false), ("abs", true) },
                { (UnaryArithmeticKind.Abs, true), ("fabs", true) },

                { (UnaryArithmeticKind.IsNaNF, true), ("isnan", true) },
                { (UnaryArithmeticKind.IsInfF, true), ("isinf", true) },

                { (UnaryArithmeticKind.SqrtF, true), ("sqrt", true) },
                { (UnaryArithmeticKind.RsqrtF, true), ("rsqrt", true) },

                { (UnaryArithmeticKind.SinF, true), ("sin", true) },
                { (UnaryArithmeticKind.AsinF, true), ("asin", true) },
                { (UnaryArithmeticKind.SinHF, true), ("sinh", true) },

                { (UnaryArithmeticKind.CosF, true), ("cos", true) },
                { (UnaryArithmeticKind.AcosF, true), ("acos", true) },
                { (UnaryArithmeticKind.CosHF, true), ("cosh", true) },

                { (UnaryArithmeticKind.TanF, true), ("tan", true) },
                { (UnaryArithmeticKind.AtanF, true), ("atan", true) },
                { (UnaryArithmeticKind.TanHF, true), ("tanh", true) },

                { (UnaryArithmeticKind.ExpF, true), ("exp", true) },
                { (UnaryArithmeticKind.Exp2F, true), ("exp2", true) },

                { (UnaryArithmeticKind.Log2F, true), ("log2", true) },
                { (UnaryArithmeticKind.LogF, true), ("log", true) },
                { (UnaryArithmeticKind.Log10F, true), ("log10", true) },

                { (UnaryArithmeticKind.FloorF, true), ("floor", true) },
                { (UnaryArithmeticKind.CeilingF, true), ("ceil", true) },
            };

        private static readonly Dictionary<
            (BinaryArithmeticKind, bool),
            (string, bool)> BinaryArithmeticOperations =
            new Dictionary<(BinaryArithmeticKind, bool), (string, bool)>()
            {

                { (BinaryArithmeticKind.Add, false), ("+", false) },
                { (BinaryArithmeticKind.Add, true), ("+", false) },

                { (BinaryArithmeticKind.Sub, false), ("-", false) },
                { (BinaryArithmeticKind.Sub, true), ("-", false) },

                { (BinaryArithmeticKind.Mul, false), ("*", false) },
                { (BinaryArithmeticKind.Mul, true), ("*", false) },

                { (BinaryArithmeticKind.Div, false), ("/", false) },
                { (BinaryArithmeticKind.Div, true), ("/", false) },

                { (BinaryArithmeticKind.Rem, false), ("%", false) },
                { (BinaryArithmeticKind.Rem, true), ("remainder", true) },

                { (BinaryArithmeticKind.And, false), ("&", false) },
                { (BinaryArithmeticKind.Or, false), ("|", false) },
                { (BinaryArithmeticKind.Xor, false), ("^", false) },

                { (BinaryArithmeticKind.Shl, false), ("<<", false) },
                { (BinaryArithmeticKind.Shr, false), (">>", false) },

                { (BinaryArithmeticKind.Min, false), ("min", true) },
                { (BinaryArithmeticKind.Min, true), ("fmin", true) },

                { (BinaryArithmeticKind.Max, false), ("max", true) },
                { (BinaryArithmeticKind.Max, true), ("fmax", true) },

                { (BinaryArithmeticKind.Atan2F, true), ("atan2", true) },
                { (BinaryArithmeticKind.PowF, true), ("pow", true) },
            };

        private static readonly Dictionary<
            (TernaryArithmeticKind, bool),
            string> TernaryArithmeticOperations =
            new Dictionary<(TernaryArithmeticKind, bool), string>()
            {
                { (TernaryArithmeticKind.MultiplyAdd, true), "fma" },
            };

        private static readonly Dictionary<AtomicKind, string> AtomicOperations =
            new Dictionary<AtomicKind, string>()
            {
                { AtomicKind.Exchange, "atomic_exchange" },
                { AtomicKind.Add, "atomic_fetch_add" },
                { AtomicKind.And, "atomic_fetch_and" },
                { AtomicKind.Or, "atomic_fetch_or" },
                { AtomicKind.Xor, "atomic_fetch_xor" },
                { AtomicKind.Max, "atomic_fetch_max" },
                { AtomicKind.Min, "atomic_fetch_min" },
            };
    }
}

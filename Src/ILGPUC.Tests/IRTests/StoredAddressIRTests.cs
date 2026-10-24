// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StoredAddressIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Tests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Checks that a local whose address is stored to memory keeps its allocation.
/// </summary>
public sealed class StoredAddressIRTests(ITestOutputHelper output) :
    CompilationTestBase(output)
{
    private unsafe struct Holder
    {
        public int* Pointer;
        public int Offset;
    }

    /// <summary>
    /// Removes the locals whose address is stored to memory together with the stores
    /// to them, the way SSA construction removes a promoted allocation, and verifies
    /// the uses of the removed locals that remain.
    /// </summary>
    private sealed class RemoveStoredAddressLocals(TransformationArgs args) :
        Transformation(args)
    {
        protected override void OnTransform(MethodTransform transform)
        {
            base.OnTransform(transform);

            transform.OldMethod.ForEachValue<Store>(store =>
            {
                if (!TryGetLocal(store.Value, out var local))
                    return;
                transform.Replace(local, null);
                transform.OldMethod.ForEachValue<Store>(localStore =>
                {
                    if (localStore.Target == local)
                        transform.Replace(localStore, null);
                });
            });
            SSAConstruction.VerifyPromotedAllocationUses(transform);
        }

        /// <summary>
        /// Returns the local whose address the given value is derived from.
        /// </summary>
        private static bool TryGetLocal(Value value, out Alloca local)
        {
            local = null!;
            if (value is Alloca alloca)
            {
                local = alloca;
                return true;
            }
            if (value is not PureValue)
                return false;
            foreach (var operand in value.Values)
            {
                if (TryGetLocal(operand, out local))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Stores the address of a local into a structure and reads the local through
    /// the stored address.
    /// </summary>
    internal static unsafe void StoredAddressKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index];
        var holder = new Holder { Pointer = &value, Offset = 1 };
        data[index] = *holder.Pointer + holder.Offset;
    }

    /// <summary>
    /// The address escapes into the structure it is stored to, so the local keeps
    /// its allocation until the address is resolved, and the store of the result
    /// remains after the global optimizations.
    /// </summary>
    [Fact]
    public void LocalWhoseAddressIsStored_KeepsItsAllocation()
    {
        var ir = DumpIRString(
            GetKernel(typeof(StoredAddressIRTests), nameof(StoredAddressKernel)),
            IRDumpPoint.AfterGlobalOpt);
        Assert.Matches(@"store i32 %\d+, ptr %\d+", ir);
        Assert.DoesNotContain("ptr null", ir);
    }

    /// <summary>
    /// A store of the address of a local removed like a promoted allocation would be
    /// removed with the local, so the verification of SSA construction reports it as
    /// an internal compiler error that names the kernel.
    /// </summary>
    [Fact]
    public void RemovedLocalWhoseAddressIsStored_ReportsInternalError()
    {
        using var helper = new CompilationHelper();
        var kernel = GetKernel(typeof(StoredAddressIRTests), nameof(StoredAddressKernel));
        var properties = new CompilationProperties();
        var module = helper.CompileToModule(kernel, properties);
        var transformer = Transformer.Create(
            static args => new RemoveStoredAddressLocals(args));

        var exception = Assert.Throws<InternalCompilerException>(
            () => transformer.Apply(properties, helper.TypeManager, module));
        var message = exception.InnerException?.Message;
        Assert.Contains("st_", message);
        Assert.Contains($"'{nameof(StoredAddressKernel)}'", message);
    }
}

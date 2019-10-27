// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLSourceBackend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using System;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Represents an OpenCL source backend.
    /// </summary>
    public sealed class CLBackend : Backend<CLIntrinsic.Handler>
    {
        #region Instance

        /// <summary>
        /// Constructs a new OpenCL source backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="platform">The target platform.</param>
        public CLBackend(
            Context context,
            TargetPlatform platform)
            : base(
                  context,
                  BackendType.OpenCL,
                  BackendFlags.RequiresIntrinsicImplementations,
                  new CLABI(context.TypeContext, platform),
                  abi => new CLArgumentMapper(context, abi))
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated <see cref="Backend.ArgumentMapper"/>.
        /// </summary>
        public new CLArgumentMapper ArgumentMapper => base.ArgumentMapper as CLArgumentMapper;

        #endregion

        #region Methods

        /// <summary cref="Backend.Compile(EntryPoint, in BackendContext, in KernelSpecialization)"/>
        protected override CompiledKernel Compile(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: IComputeHost.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------



using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ILGPU;
using ILGPU.Runtime;

namespace BlazorSampleApp.ILGPUWebHost
{
    /// <summary>
    /// represents the type of a host
    /// </summary>
    public interface IComputeHost
    {
        /// <summary>
        /// host this host been configured?
        /// </summary>
        bool HostConfigured { get; }

        /// <summary>
        /// number of active compute sessions
        /// </summary>
        int SessionCount { get; }

        /// <summary>
        /// determine the minium specifications for an accelerator 
        /// </summary>
        /// <param name="allowedAcceleratorTypes"></param>
        /// <param name="miniumMemory"></param>
        /// <param name="multiProcessors"></param>
        /// <returns></returns>
        bool ConfigureAcceleration(AcceleratorType[] allowedAcceleratorTypes, long miniumMemory,int multiProcessors);

        /// <summary>
        /// request a new compute session
        /// </summary>
        /// <param name="sessionID"></param>
        /// <returns></returns>
        ComputeSession NewComputeStream(string sessionID);


        /// <summary>
        /// return a compute session
        /// </summary>
        /// <param name="session"></param>
        void ReturnSession(ComputeSession session);

    }
}

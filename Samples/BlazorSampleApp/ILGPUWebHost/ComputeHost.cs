// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2021 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: ComputeHost.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------



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
    /// This is a singleton object for sharing all accelerators on a host server.
    /// </summary>
    /// <details>
    /// We assume a server can have multiple GPUs, this object's job would be to manage the load
    /// across all GPUs on this server. Imagine a farm with 1000 server each with 4 GPUs, 
    /// how do we manage a GPU farm? At a single "blazor" server, sharing GPU's is not too hard to 
    /// imagine how we spread the load across the 4 GPUs but at a farm level; networking, routing and 
    /// bandwidth become important very important.
    /// 
    /// 
    /// We could in theory re-establish streams between GPUs and clients to balance
    /// the computational load. It is a trivial task counting accelerator streams per GPU
    /// should we want to balance similar compute tasks. 
    /// 
    /// We will note the Mandelbrot set computed here uses approximately 100 MB of memory per session
    /// therefor we should secure buffer allocation as part of the session. 
    /// </details>

    public class ComputeHost : IComputeHost, IDisposable
    {
        // the context for session and accelerator instantiation 
        private Context _context;

        private bool _disposing;

        // all current sessions
        private List<ComputeSession> _computeSessions;

        // all compliant accelerators
        private List<Accelerator> _accelerators;

        // has this host been configured?
        public bool HostConfigured { get { return !_disposing && _accelerators.Count>0; } }

        // current active session list
        public int SessionCount { get { return _computeSessions?.Count ?? 0; }  }


        /// <summary>
        /// This is used to establish the host as a singlton object on the web server
        /// </summary>
        public ComputeHost()
        {
            _disposing = false;

            _context = Context.Create(builder => builder.Default());

            _computeSessions = new List<ComputeSession>();

            _accelerators = new List<Accelerator>();
        }


        /// <summary>
        /// Each application must specify what are the requirements for accelerators on the host server
        /// </summary>
        /// <param name="allowedAcceleratorTypes"></param>
        /// <param name="miniumMemory"></param>
        /// <returns></returns>
        public bool ConfigureAcceleration(AcceleratorType[] allowedAcceleratorTypes, long miniumMemory, int multiProcessors)
        {
            bool result = false;

            if (!HostConfigured)
            {

                List<AcceleratorType> types = allowedAcceleratorTypes.ToList();
                
                foreach (var device in _context)
                {
                    
                    if (types.Exists(x => x == device.AcceleratorType))
                    {

                        var accelerator = device.CreateAccelerator(_context);

                        if (accelerator.MemorySize >= miniumMemory && accelerator.NumMultiprocessors >= multiProcessors)
                        {

                            result = true;

                            _accelerators.Add(accelerator);
                        }
                        else
                        {
                            accelerator.Dispose();
                        }
                    }
                }

                if (_accelerators.Count > 1)
                {
                    var ordered = from acc in _accelerators orderby acc.AcceleratorRank() descending
                                    select acc;

                    _accelerators = ordered.ToList();
                }
            }
            return result;
        }

        /// <summary>
        /// Here we assume only one accelerator on the system. If there were multiple accelerators we could do a 
        /// check of how many sessions were on each accelerator and return a new compute session on the leased
        /// used accelerator.
        /// </summary>
        /// <param name="sessionID"></param>
        /// <returns></returns>
        public ComputeSession NewComputeStream (string sessionID)
        {
            if (!_disposing)
            {
                ComputeSession computeSession = new ComputeSession(sessionID, _accelerators[0], this); // preform load balancing magic here is supporting multiple accelerators
                _computeSessions.Add(computeSession);
                return computeSession;
            }
            return null;
        }


        /// <summary>
        /// If for some reason a blazor compute session end without notice, we can attempt to 
        /// connect back the our original session provided we stored the seesion ID in the 
        /// client browser. Note if we have a server farm, we may need to keep a server session map.
        /// </summary>
        /// <param name="sessionID"></param>
        /// <returns></returns>
        public ComputeSession FindComputeSession(string sessionID)
        {

            ComputeSession result = null;

            if (!_disposing)
            {
                result = _computeSessions?.Find(x => x.SessionID == sessionID);
            }

            return null;
        }

        /// <summary>
        /// We are tracking all compute sessions on the host for GPU resource allowcation. Sessions must be removed 
        /// from the host otherwise when disposed otherside we will take the GPU down on all sessions by overallocation
        /// or resources. 
        /// </summary>
        /// <param name="session"></param>

        public void ReturnSession(ComputeSession session)
        {
            if (session != null)
            {
                session.IsActive = false;

                _computeSessions.Remove(session);

                if (!session.IsDisposing)
                {
                    session.Dispose();
                }
            }
        }


        /// <summary>
        /// "gracefully clean up sessions" If the host is disposing we need to clean up outstanding sessions
        /// </summary>
        public void CleanUpSessions()
        {
            foreach(var session in _computeSessions)
            {
                // stop all new compute
                session.IsActive = false;
            }

            foreach (var session in _computeSessions)
            {
                while(session.IsComputing)
                {
                    // this does not work if the current session is in the host thread.
                    Thread.Sleep(10);
                }
                if (!session.IsDisposing)
                {
                    session?.Dispose();
                }
            }
            _computeSessions.Clear();
        }

        /// <summary>
        /// clean up accelerators.
        /// 
        /// </summary>
        private void CleanUpAcceleratorHosts()
        {
            foreach (var accelerator in _accelerators)
            {
                // we assume and accelerator could be active
                accelerator?.Synchronize();
                
            }
            foreach (var accelerator in _accelerators)
            {
                accelerator?.Dispose();
            }
            _accelerators.Clear();
        }


        /// <summary>
        /// clear up resources should the host be disposed.
        /// </summary>

        public void Dispose()
        {
            _disposing = true;

            CleanUpSessions();

            CleanUpAcceleratorHosts();
            
            _context?.Dispose();

        }
    }
}

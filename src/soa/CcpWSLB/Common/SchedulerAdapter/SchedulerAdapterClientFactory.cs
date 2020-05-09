﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ServiceBroker.Common.SchedulerAdapter
{
    using System;
    using System.ServiceModel;
    using System.Threading;
    using System.Threading.Tasks;
    using IdentityUtil;
    using Microsoft.Telepathy.Common.TelepathyContext;
    using Microsoft.Telepathy.Common.TelepathyContext.Extensions;
    using Microsoft.Telepathy.ServiceBroker.BackEnd;
    using Microsoft.Telepathy.ServiceBroker.Common.ServiceJobMonitor;
    using Microsoft.Telepathy.Session;
    using Microsoft.Telepathy.Session.Common;
    using Microsoft.Telepathy.Session.Internal;

    /// <summary>
    /// Factory class for scheduler adapter client
    /// </summary>
    internal partial class SchedulerAdapterClientFactory : IDisposable
    {
        /// <summary>
        /// Stores the shared data
        /// </summary>
        private SharedData sharedData;

        /// <summary>
        /// Stores the instance of the scheduler adapter client
        /// </summary>
        private ISchedulerAdapter schedulerAdapterClient;

        /// <summary>
        /// Stores the semaphore to create client
        /// </summary>
        private SemaphoreSlim createClientSS = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Stores the service job monitor instance
        /// </summary>
        private ServiceJobMonitorBase monitor;

        /// <summary>
        /// Stores the dispatcher manager
        /// </summary>
        private DispatcherManager dispatcherManager;

        /// <summary>
        /// Stores the fabric cluster context;
        /// </summary>
        private ITelepathyContext context;

        /// <summary>
        /// Initializes a new instance of the SchedulerAdapterClientFactory class
        /// </summary>
        /// <param name="sharedData">indicating the shared data</param>
        /// <param name="monitor">indicating the monitor</param>
        /// <param name="dispatcherManager">indicating the dispatcher manager</param>
        public SchedulerAdapterClientFactory(SharedData sharedData, ServiceJobMonitorBase monitor, DispatcherManager dispatcherManager, ITelepathyContext context)
        {
            this.sharedData = sharedData;
            this.monitor = monitor;
            this.dispatcherManager = dispatcherManager;
            this.context = context;
        }

        /// <summary>
        /// Finalizes an instance of the SchedulerAdapterClientFactory class
        /// </summary>
        ~SchedulerAdapterClientFactory()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Dispose the shceduler adapter client factory
        /// </summary>
        void IDisposable.Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the instance of scheduler adapter client
        /// </summary>
        /// <returns>returns the instance of the scheduler adapter client</returns>
        public async Task<ISchedulerAdapter> GetSchedulerAdapterClientAsync()
        {
            // this.CheckClient();
            bool newClientCreated = false;
            ICommunicationObject client = this.schedulerAdapterClient as ICommunicationObject;
            if (this.schedulerAdapterClient == null || client == null || client.State == CommunicationState.Faulted )
            {
                await this.createClientSS.WaitAsync();
                try
                {
                    if (this.schedulerAdapterClient == null)
                    {
                        await this.CreateClient();
                        if (!SoaCommonConfig.WithoutSessionLayer)
                        {
                            newClientCreated = true;
                        }
                    }
                    else
                    {
                        client = this.schedulerAdapterClient as ICommunicationObject;
                        if (client == null || client.State == CommunicationState.Faulted )
                        {
                            try
                            {
                                Utility.AsyncCloseICommunicationObject(client);
                                await this.CreateClient();
                                if (!SoaCommonConfig.WithoutSessionLayer)
                                {
                                    newClientCreated = true;
                                }
                            }
                            catch (Exception e)
                            {
                                // Swallow exception when creating client
                                BrokerTracing.TraceError("[SchedulerAdapterClientFactory] Exception thrown when creating client: {0}", e);
                            }
                        }
                    }
                }
                finally
                {
                    this.createClientSS.Release();
                }

                if (newClientCreated)
                {
                    await this.monitor.RegisterJob();
                }
            }

            return this.schedulerAdapterClient;
        }

        /// <summary>
        /// Create client
        /// </summary>
        private async Task CreateClient()
        {
            BrokerTracing.TraceVerbose(
                "[SchedulerAdapterClientFactory] Creating client with StartInfo.IpAddress: {0}, StartInfo.EprList: {1}, WithoutSessionLayer: {2}",
                this.sharedData.StartInfo.IpAddress != null,
                this.sharedData.StartInfo.EprList != null,
                SoaCommonConfig.WithoutSessionLayer);

            if (SoaCommonConfig.WithoutSessionLayer)
            {
                this.schedulerAdapterClient = new DummySchedulerAdapterClient(this.sharedData.StartInfo.IpAddress, this.dispatcherManager);
            }
            else
            {
               
                // TODO: implementing new authentication logic between brokerworker and sessionlauncher
                
                string headnodeMachine = await this.context.ResolveSessionLauncherNodeAsync();
                // certThrumbprint = await this.context.GetSSLThumbprint();


                // if (this.monitor.TransportScheme == TransportScheme.AzureStorage)
                // {
                //     this.schedulerAdapterClient = new SchedulerAdapterClient(
                //         new TableTransportBinding() { ConnectionString = this.monitor.SharedData.BrokerInfo.AzureStorageConnectionString, TargetPartitionKey = Guid.NewGuid().ToString() },
                //         new EndpointAddress(new Uri(TelepathyConstants.SessionSchedulerDelegationAzureTableBindingAddress)),
                //         this.sharedData.StartInfo.IpAddress,
                //         this.dispatcherManager);
                // }
                // else
                // {
                // this.schedulerAdapterClient = new HpcSchedulerAdapterClient(headnodeMachine, certThrumbprint, new System.ServiceModel.InstanceContext(this.monitor));
                this.schedulerAdapterClient = new SchedulerAdapterClient(
                    BindingHelper.HardCodedUnSecureNetTcpBinding,
                    new EndpointAddress(new Uri(SoaHelper.GetSchedulerDelegationAddress(headnodeMachine))),
                    this.sharedData.StartInfo.IpAddress,
                    this.dispatcherManager,
                    new System.ServiceModel.InstanceContext(this.monitor));
                try
                {
                    await this.schedulerAdapterClient.GetJobOwnerIDAsync(this.sharedData.BrokerInfo.SessionId);
                }
                catch(FaultException e)
                {
                    this.schedulerAdapterClient = new SchedulerAdapterClient(
                        BindingHelper.HardCodedUnSecureNetTcpBinding,
                        new EndpointAddress(new Uri(SoaHelper.GetSchedulerDelegationAddress(headnodeMachine))),
                        this.sharedData.StartInfo.IpAddress,
                        this.dispatcherManager,
                        new System.ServiceModel.InstanceContext(this.monitor), e);
                }

                // }
            }
        }

        /// <summary>
        /// Check client state, recreate new client if the client is faulted
        /// </summary>
        private void CheckClient()
        {
            SchedulerAdapterClient client = this.schedulerAdapterClient as SchedulerAdapterClient;
            if (client != null)
            {
                if (client.State == CommunicationState.Faulted || client.InnerChannel.State == CommunicationState.Faulted)
                {
                    try
                    {
                        Utility.AsyncCloseICommunicationObject(client);
                        this.CreateClient().GetAwaiter().GetResult();
                    }
                    catch (Exception e)
                    {
                        // Swallow exception when creating client
                        BrokerTracing.TraceError("[SchedulerAdapterClientFactory] Exception thrown when creating client: {0}", e);
                    }
                }
            }
        }

        /// <summary>
        /// Close the factory
        /// </summary>
        public void Close()
        {
            ((IDisposable)this).Dispose();
        }

        /// <summary>
        /// Dispose the scheduler adapter client factory
        /// </summary>
        /// <param name="disposing">indicating whether it is disposing</param>
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.schedulerAdapterClient is ICommunicationObject)
                {
                    Utility.AsyncCloseICommunicationObject((ICommunicationObject)this.schedulerAdapterClient);
                }
            }
        }
    }
}

﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ServiceBroker.Common.SchedulerAdapter
{
    using System.Collections.Generic;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Threading.Tasks;
    using IdentityUtil;
    using Microsoft.Telepathy.ServiceBroker.BackEnd;
    using Microsoft.Telepathy.Session;
    using Microsoft.Telepathy.Session.Data;
    using Microsoft.Telepathy.Session.Internal;

    public class SchedulerAdapterClient : DuplexClientBase<ISchedulerAdapter>, ISchedulerAdapter
    {
        /// <summary>
        /// Stores the unique id
        /// </summary>
        private static int uniqueIdx;

        private string[] predefinedSvcHost = new string[0];

        private DispatcherManager dispatcherManager = null;

        public SchedulerAdapterClient(Binding binding, EndpointAddress address, InstanceContext context, FaultException fault = null) : this(binding, address, null, null, context, fault)
        {
        }

        internal SchedulerAdapterClient(Binding binding, EndpointAddress address, string[] predefinedSvcHost, DispatcherManager dispatcherManager, InstanceContext instanceContext, FaultException fault = null) : base(instanceContext, binding, address)
        {
            this.predefinedSvcHost = predefinedSvcHost;
            this.dispatcherManager = dispatcherManager;
            if (fault != null && fault.Code.Name.Equals(IdentityMessageFault.FaultCode))
            {
                IdentityMessageFault faultDetail = fault.CreateMessageFault().GetDetail<IdentityMessageFault>();
                this.Endpoint.Behaviors.AddBehaviorFromExForClient(faultDetail).GetAwaiter().GetResult();
            }
        }

        public async Task<bool> UpdateBrokerInfoAsync(string sessionId, Dictionary<string, object> properties) => await this.Channel.UpdateBrokerInfoAsync(sessionId, properties);

        public async Task<(bool succeed, BalanceInfo balanceInfo, List<string> taskIds, List<string> runningTaskIds)> GetGracefulPreemptionInfoAsync(string sessionId) =>
            await this.Channel.GetGracefulPreemptionInfoAsync(sessionId);

        public async Task<bool> FinishTaskAsync(string jobId, string taskUniqueId) => await this.Channel.FinishTaskAsync(jobId, taskUniqueId);

        public async Task<bool> ExcludeNodeAsync(string jobid, string nodeName) => await this.Channel.ExcludeNodeAsync(jobid, nodeName);

        public async Task RequeueOrFailJobAsync(string sessionId, string reason)
        {
            await this.Channel.RequeueOrFailJobAsync(sessionId, reason);
        }

        public async Task FailJobAsync(string sessionId, string reason)
        {
            await this.Channel.FailJobAsync(sessionId, reason);
        }

        public async Task FinishJobAsync(string sessionId, string reason)
        {
            await this.Channel.FinishJobAsync(sessionId, reason);
        }

        public async Task<(JobState jobState, int autoMax, int autoMin)> RegisterJobAsync(string jobid)
        {
            // TODO: this is not proper place to put dispatcher creating logic. Remove this.
            return await this.Channel.RegisterJobAsync(jobid);
        }

        // TODO: remove globalTaskId
        public async Task<int?> GetTaskErrorCode(string jobId, string globalTaskId)
        {
            return await this.Channel.GetTaskErrorCode(jobId, globalTaskId);
        }

        public async Task<string> GetJobOwnerIDAsync(string sessionId)
        {
            return await this.Channel.GetJobOwnerIDAsync(sessionId);
        }
    }
}
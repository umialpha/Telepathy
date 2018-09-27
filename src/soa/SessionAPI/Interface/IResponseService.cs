//------------------------------------------------------------------------------
// <copyright file="IResponseService.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
//      The interface for the client to request the responses
// </summary>
//------------------------------------------------------------------------------

namespace Microsoft.Hpc.Scheduler.Session
{
    using System.Runtime.Serialization;
    using System.ServiceModel;

    using Microsoft.Hpc.Scheduler.Session.Internal;

    /// <summary>
    /// The interface for the client to request the responses
    /// </summary>
    [ServiceContract(Name = "ResponseService", CallbackContract = typeof(IResponseServiceCallback), Namespace = "http://hpc.microsoft.com")]
    public interface IResponseService
    {
        /// <summary>
        /// Get specifies response messages
        /// </summary>
        /// <param name="action">Which resonses to return</param>
        /// <param name="clientData">Client data to return in response message headers</param>
        /// <param name="position">Position in the enum to start (start or current)</param>
        /// <param name="count">Number of messages to return</param>
        [OperationContract]
        [FaultContract(typeof(SessionFault), Action = SessionFault.Action)]
        void GetResponses(string action, string clientData, GetResponsePosition resetToBegin, int count, string clientId);
    }

    /// <summary>
    /// Represent the response position
    /// </summary>
    public enum GetResponsePosition
    {
        /// <summary>
        /// From beginning
        /// </summary>
        Begin,

        /// <summary>
        /// Current location
        /// </summary>
        Current
    }
}
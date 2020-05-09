﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Session.Internal
{
    using System;

    using Microsoft.Telepathy.Session.Common;
    using Microsoft.Telepathy.Session.Interface;

    /// <summary>
    /// Provides an unified interface for SessionInfo
    /// </summary>
    /// <remarks>
    /// SessionInfo and WebSessionInfo will implement this interface
    /// </remarks>
    public abstract class SessionInfoBase : IConnectionInfo
    {
        public abstract string Id { get; set; }

        public abstract int ServiceOperationTimeout { get; set; }

        public abstract bool Secure { get; set; }

        public abstract TransportScheme TransportScheme { get; set; }

        public abstract bool UseInprocessBroker { get; set; }

        public abstract Version ServiceVersion { get; set; }

        /// <summary>
        /// Get or set whether to use aad integration for authentication
        /// </summary>
        public bool UseAad { get; set; }

        public bool UseIds { get; set; }

        public string IdsUrl { get; set; }

        private bool useLocalUser = false;

        /// <summary>
        /// Get or set whether login as local user. 
        /// This flag only tasks effort if client machine is non-domain joined.
        /// </summary>
        public virtual bool LocalUser
        {
            get => this.useLocalUser && SoaHelper.IsCurrentUserLocal();
            set => this.useLocalUser = value && SoaHelper.IsCurrentUserLocal();
        }


        public bool IsAadOrLocalUser => this.UseAad || this.LocalUser;

        /// <summary>
        /// Gets or sets the Username.
        /// This property is safe to be pushed down to <see cref="SessionInfo"/>.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the Internal Password.
        /// This property is safe to be pushed down to <see cref="SessionInfo"/>.
        /// </summary>
        public string InternalPassword { get; set; }

        /// <summary>
        /// Gets or sets the head node name.
        /// This property is safe to be pushed down to <see cref="SessionInfo"/>.
        /// </summary>
        public string Headnode { get; set; }

        public string AzureStorageConnectionString { get; set; }

        public string AzureTableStoragePartitionKey { get; set; }
    }
}

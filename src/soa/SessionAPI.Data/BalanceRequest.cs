﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Session.Data
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [Serializable]
    [DataContract]
    public class SoaBalanceRequest
    {
        [DataMember]
        public int AllowedCoreCount { get; set; }

        [DataMember]
        public IList<string> TaskIds { get; set; }
    }
}

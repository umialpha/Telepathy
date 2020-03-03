// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license

using System.Security.Claims;

namespace WcfService
{
    class RequireAuthenticationAuthorizationManager : ClaimsAuthorizationManager
    {
        public override bool CheckAccess(AuthorizationContext context)
        {
            return context.Principal.Identity.IsAuthenticated;
        }
    }
}

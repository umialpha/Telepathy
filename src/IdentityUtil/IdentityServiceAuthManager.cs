

namespace IdentityUtil
{
    using System;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Linq;
    using System.Security.Claims;
    using System.Security.Cryptography.X509Certificates;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Threading;
    using Microsoft.IdentityModel.Logging;
    using Microsoft.IdentityModel.Tokens;

    public class IdentityServiceAuthManager : ServiceAuthorizationManager
    {
        private X509Certificate2 signingCert;
        private string issuerName;
        private readonly string audience;
        private string authority;
        private string targetPath = null;

        public IdentityServiceAuthManager(string targetPath, string authority, string audience)
        {
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap = new Dictionary<string, string>();

            var disco = IdentityUtil.GetDiscoveryResponse(authority).Result;

            IdentityModelEventSource.ShowPII = true;
            this.signingCert = new X509Certificate2(Convert.FromBase64String(disco.KeySet.Keys.First().X5c.First()));
            this.issuerName = disco.Issuer;
            this.audience = audience;
            this.authority = authority;
            this.targetPath = targetPath;
        }

        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            if (!string.IsNullOrEmpty(this.targetPath))
            {
                string incomingPath = operationContext.IncomingMessageHeaders.To.PathAndQuery.ToString();
                if (!incomingPath.Equals(this.targetPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    // This thread comes from thread pool, which may previously be assigned AAD user principal.
                    // We need to clear principal here.
                    Thread.CurrentPrincipal = null;
                    return base.CheckAccessCore(operationContext);
                }
            }

            string customData = AuthMessageHeader.ReadHeader(operationContext.RequestContext.RequestMessage);
            return this.CheckHeaderAccess(customData);
        }

        public bool CheckHeaderAccess(string header)
        {
            ClaimsPrincipal principle = this.ValidateJwtToken(header);

            if (principle == null)
                return false;

            Thread.CurrentPrincipal = principle;
            return true;
        }

        public ClaimsPrincipal ValidateJwtToken(string jwtToken)
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                throw new FaultException(
                    MessageFault.CreateFault(
                        new FaultCode(IdentityMessageFault.FaultCode),
                        new FaultReason("Unauthenticated"),
                        new IdentityMessageFault(authority, IdentityUtil.DefaultROClientId, IdentityUtil.DefaultClientSecret, audience)));
            }

            var parameters = new TokenValidationParameters
            {
                ValidAudience = audience,
                ValidIssuer = issuerName,
                IssuerSigningKey = new X509SecurityKey(signingCert)
            };

            SecurityToken validatedToken;
            var handler = new JwtSecurityTokenHandler();

            try
            {
                return handler.ValidateToken(jwtToken, parameters, out validatedToken);
            }
            catch
            {
                throw new FaultException(
                    MessageFault.CreateFault(
                        new FaultCode(IdentityMessageFault.FaultCode),
                        new FaultReason("Invalid Jwt"),
                        new IdentityMessageFault(authority, IdentityUtil.DefaultROClientId, IdentityUtil.DefaultClientSecret, audience)));
            }
        }
    }
}

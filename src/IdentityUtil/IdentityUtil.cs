

namespace IdentityUtil
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Security.Principal;
    using System.ServiceModel.Description;
    using System.Threading.Tasks;
    using IdentityModel.Client;

    public static class IdentityUtil
    {
        private static string identityServerUrl = "https://localhost:5000";

        private static string defaultROClientId = "ro.client";

        private static string defaultClientSecret = "secret";

        private static string defaultClientId = "client";

        private static string serviceScope = "SessionLauncher";

        public static string IdentityServerUrl
        {
            get { return identityServerUrl; }
        }

        public static string DefaultROClientId
        {
            get { return defaultROClientId; }
        }

        public static string DefaultClientSecret
        {
            get { return defaultClientSecret; }
        }

        public static string DefaultClientId
        {
            get { return defaultClientId; }
        }

        public static string GenerateIdentityFromHeader(this IPrincipal principal, string header)
        {
            //var pi = ValidateJwtToken(header);
            return string.Empty;
        }

        public static async Task<string> GetJwtTokenFromROAsync(string authority, string clientId, string clientSecret, string userName, string password, string scope)
        {
            var disco = await GetDiscoveryResponse(authority);
            var client = new HttpClient();
            var tokenResponse = await client.RequestPasswordTokenAsync(new PasswordTokenRequest()
            {
                Address = disco.TokenEndpoint,
                ClientId = clientId,
                ClientSecret = clientSecret,
                Scope = scope,
                UserName = userName,
                Password = password
            });

            return tokenResponse.AccessToken;
        }

        public static async Task<string> GetJwtTokenFromClientAsync(string authority, string clientId, string clientSecret, string scope)
        {
            var disco = await GetDiscoveryResponse(authority);
            var client = new HttpClient();
            var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest()
            {
                Address = disco.TokenEndpoint,
                ClientId = clientId,
                ClientSecret = clientSecret,
                Scope = scope,
            });

            return tokenResponse.AccessToken;
        }

        public static async Task<DiscoveryResponse> GetDiscoveryResponse(string authority)
        {
            var client = new HttpClient();
            var disco = await client.GetDiscoveryDocumentAsync(authority);
            if (disco.IsError)
            {
                return new DiscoveryResponse(new Exception(disco.Error), disco.Error);
            }

            return disco;
        }

        public static async Task<KeyedByTypeCollection<IEndpointBehavior>> AddBehaviorForClient(
            this KeyedByTypeCollection<IEndpointBehavior> behaviors)
        {
            var token = await GetJwtTokenFromClientAsync(IdentityServerUrl, DefaultClientId, DefaultClientSecret,
                serviceScope).ConfigureAwait(false);
            behaviors.Add(new IdentityServiceEndpointBehavior(token));
            return behaviors;
        }

        public static async Task<KeyedByTypeCollection<IEndpointBehavior>> AddBehaviorFromEx(
            this KeyedByTypeCollection<IEndpointBehavior> behaviors, IdentityMessageFault faultDetail, string username,
            string password)
        {
            var token = await GetJwtTokenFromROAsync(faultDetail.Authority, faultDetail.ClientId, faultDetail.ClientSecret,
                username, password, faultDetail.ServiceScope).ConfigureAwait(false);
            behaviors.Add(new IdentityServiceEndpointBehavior(token));
            return behaviors;
        }

    }
}

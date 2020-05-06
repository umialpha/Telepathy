

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
        private const string identityServerUrl = "https://localhost:44372";

        private const string defaultROClientId = "ro.client";

        private const string defaultClientSecret = "secret";

        private const string defaultClientId = "client";

        private const string defaultWinAuthClientId = "win.client";

        private const string sessionLauncherApi = "SessionLauncher";

        private const string WinAuthGrantType = "windows_auth";

        private const string schedulerAdapterApi = "SchedulerAdapter";

        private const string brokerLauncherApi = "BrokerLauncher";

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

        public static string SessionLauncherApi
        {
            get { return sessionLauncherApi; }
        }

        public static string SchedulerAdapterApi
        {
            get { return schedulerAdapterApi; }
        }

        public static string BrokerLauncherApi
        {
            get { return brokerLauncherApi; }
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

        public static async Task<string> GetJwtTokenFromWinAuthAsync(string authority = identityServerUrl, string clientId = defaultWinAuthClientId, string clientSecret = defaultClientSecret, string scope = sessionLauncherApi)
        {
            var disco = await GetDiscoveryResponse(authority);

            var httpHandler = new HttpClientHandler
            {
                UseDefaultCredentials = true
            };

            var client = new HttpClient(httpHandler);
            var tokenResponse = await client.RequestTokenAsync(new TokenRequest()
            {
                Address = disco.TokenEndpoint,
                ClientId = clientId,
                ClientSecret = clientSecret,
                GrantType = WinAuthGrantType,
                Parameters =
                {
                    { "scope", scope }
                }
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
                Scope = scope
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
            this KeyedByTypeCollection<IEndpointBehavior> behaviors, string scope)
        {
            var token = await GetJwtTokenFromClientAsync(IdentityServerUrl, DefaultClientId, DefaultClientSecret,
                scope).ConfigureAwait(false);
            behaviors.Add(new IdentityServiceEndpointBehavior(token));
            return behaviors;
        }

        public static async Task<KeyedByTypeCollection<IEndpointBehavior>> AddBehaviorForWinAuthClient(
            this KeyedByTypeCollection<IEndpointBehavior> behaviors, string scope = sessionLauncherApi)
        {
            var token = await GetJwtTokenFromWinAuthAsync(scope: scope).ConfigureAwait(false);
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

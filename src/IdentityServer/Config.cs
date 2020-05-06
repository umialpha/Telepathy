
using IdentityServer.WinAuth;
using IdentityServer4.Models;
using System.Collections.Generic;

namespace IdentityServer
{
    public static class Config
    {
        public static IEnumerable<IdentityResource> Ids =>
            new IdentityResource[]
            { 
                new IdentityResources.OpenId(),
                new IdentityResources.Profile() 
            };

        public static IEnumerable<ApiResource> Apis =>
            new List<ApiResource>
            {
                new ApiResource("SessionLauncher"),
                new ApiResource("SchedulerAdapter"),
                new ApiResource("BrokerLauncher")
            };
        
        public static IEnumerable<Client> Clients =>
            new Client[]
            {
                new Client
                {
                    ClientId = "ro.client",

                    AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,

                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                    },

                    // scopes that client has access to
                    AllowedScopes =
                    {
                        "SessionLauncher"
                    }
                },
                new Client
                {
                    ClientId = "client",

                    AllowedGrantTypes = GrantTypes.ClientCredentials,

                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                    },

                    // scopes that client has access to
                    AllowedScopes =
                    {
                        "SessionLauncher",
                        "SchedulerAdapter",
                        "BrokerLauncher"
                    }
                },
                new Client
                {
                    ClientId = "win.client",

                    AllowedGrantTypes = new List<string>{ WinAuthOption.WindowsAuthGrantType },

                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                    },

                    // scopes that client has access to
                    AllowedScopes =
                    {
                        "SessionLauncher",
                        "BrokerLauncher"
                    }
                }
            };
        
    }
}
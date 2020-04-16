
namespace IdentityUtil
{
    using System;

    [Serializable]
    public class IdentityMessageFault
    {
        public const string FaultCode = "IdentityError";

        public string Authority { get; set; }
        public string ServiceScope { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }

        public IdentityMessageFault(string authority, string clientId, string clientSecret,
             string serviceScope)
        {
            this.Authority = authority;
            this.ClientId = clientId;
            this.ClientSecret = clientSecret;
            this.ServiceScope = serviceScope;
        }
    }
}

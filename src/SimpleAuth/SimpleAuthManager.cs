

namespace SimpleAuth
{
    using System.ServiceModel;

    public class SimpleAuthManager : ServiceAuthorizationManager
    {
        private string fingerPrint;


        public SimpleAuthManager(string fingerPrint)
        {
            this.fingerPrint = fingerPrint;
        }

        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            string customData = SimpleAuthHeader.ReadHeader(operationContext.RequestContext.RequestMessage);
            return this.CheckHeaderAccess(customData);
        }

        public bool CheckHeaderAccess(string header)
        {
            return this.fingerPrint.Equals(header);
        }
    }
}

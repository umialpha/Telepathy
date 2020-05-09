

namespace SimpleAuth
{
    using System.ServiceModel.Channels;
    using System.ServiceModel.Description;
    using System.ServiceModel.Dispatcher;

    public class SimpleAuthEndpointBehavior : IEndpointBehavior
    {
        private string fingerprint;

        public SimpleAuthEndpointBehavior(string fingerprint)
        {
            this.fingerprint = fingerprint;    
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            SimpleAuthMessageInspector inspector = new SimpleAuthMessageInspector(this.fingerprint);
            clientRuntime.MessageInspectors.Add(inspector);
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }
    }
}

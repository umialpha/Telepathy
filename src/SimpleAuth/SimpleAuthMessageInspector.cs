

namespace SimpleAuth
{
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.ServiceModel.Dispatcher;

    class SimpleAuthMessageInspector : IClientMessageInspector
    {
        private string fingerprint;

        public SimpleAuthMessageInspector(string fingerprint)
        {
            this.fingerprint = fingerprint;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            MessageBuffer buffer = request.CreateBufferedCopy(int.MaxValue);
            request = buffer.CreateMessage();
            SimpleAuthHeader header = new SimpleAuthHeader(fingerprint);

            request.Headers.Add(header);
            return null;
        }
    }
}

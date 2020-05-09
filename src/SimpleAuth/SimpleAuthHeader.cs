﻿

namespace SimpleAuth
{
    using System;
    using System.ServiceModel.Channels;
    using System.Xml;

    public class SimpleAuthHeader : MessageHeader
    {
        public static readonly string CustomHeaderName = "FingerPrint";
        public static readonly string CustomHeaderNamespace = "hpccluster";
        public string CustomData { get; private set; }
        public SimpleAuthHeader(string customData)
        {
            this.CustomData = customData;
        }
        public override string Name
        {
            get { return CustomHeaderName; }
        }

        public override string Namespace
        {
            get { return CustomHeaderNamespace; }
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            writer.WriteElementString(CustomHeaderName, CustomHeaderNamespace, this.CustomData);
        }

        public static string ReadHeader(Message request)
        {
            Int32 headerPosition = request.Headers.FindHeader(CustomHeaderName, CustomHeaderNamespace);
            if (headerPosition == -1)
            {
                return null;
            }

            MessageHeaderInfo headerInfo = request.Headers[headerPosition];
            XmlNode[] content = request.Headers.GetHeader<XmlNode[]>(headerPosition);
            return content[0].InnerText;
        }
    }
}

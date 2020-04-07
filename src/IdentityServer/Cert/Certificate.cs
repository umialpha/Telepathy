
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace IdentityServer.Cert
{
    static class Certificate
    {
        private static string certPath = "IdentityServer.Cert.OneCert.pfx";

        private static string certPwd = "!!123abc";

        public static X509Certificate2 Get()
        {
            var assembly = typeof(Certificate).Assembly;
            using (var stream = assembly.GetManifestResourceStream(certPath))
            {
                return new X509Certificate2(ReadStream(stream), certPwd);
            }
        }

        private static byte[] ReadStream(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}

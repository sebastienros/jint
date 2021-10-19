using System;
using System.IO;
using System.Text;
using System.Net;

namespace Jint.Runtime.Modules
{
    internal sealed class HttpModuleSource : IModuleSource
    {
        public bool TryLoadModuleSource(Uri location, out string moduleSourceCode)
        {
            var request = WebRequest.Create(location);

            request.Headers.Add(HttpRequestHeader.Accept, "text/javascript");
            request.Method = "GET";

            using var response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var stream = response.GetResponseStream();
                var responseEncoding = response.Headers[HttpResponseHeader.ContentEncoding];
                Encoding encoding;
                if (!string.IsNullOrEmpty(responseEncoding))
                {
                    encoding = Encoding.GetEncoding(responseEncoding);
                }
                else
                {
                    encoding = Encoding.Default;
                }

                using var textReader = new StreamReader(stream, encoding);

                moduleSourceCode = textReader.ReadToEnd();
                return true;
            }

            moduleSourceCode = null;
            return false;
        }
    }
}

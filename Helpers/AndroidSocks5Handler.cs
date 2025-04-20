#if ANDROID
using Java.IO;
using Java.Net;
using System.Net;

namespace Manta.Helpers;

public class AndroidSocks5Handler : HttpClientHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await Task.Run(async() =>
        {
            var proxy = new Proxy(Java.Net.Proxy.Type.Socks, new InetSocketAddress("127.0.0.1", 9050));
            var url = new URL(request.RequestUri?.ToString());
            var httpURLConnection = (HttpURLConnection?)url.OpenConnection(proxy);

            if (httpURLConnection is null)
                throw new Exception("httpURLConnection was null in AndroidSocks5Handler.SendAsync()");

            httpURLConnection.SetRequestProperty("Content-Type", "application/grpc-web");
            httpURLConnection.SetRequestProperty("Accept", "application/grpc-web");

            httpURLConnection.RequestMethod = request.Method.Method;
            httpURLConnection.DoOutput = true;
            httpURLConnection.DoInput = true;

            foreach (var header in request.Headers)
            {
                httpURLConnection.SetRequestProperty(header.Key, string.Join(",", header.Value));
            }

            if (request.Content is not null)
            {
                using var outputStream = httpURLConnection.OutputStream;
                if (outputStream is not null)
                {
                    await request.Content.CopyToAsync(outputStream);
                }
            }

            var response = new HttpResponseMessage((HttpStatusCode)httpURLConnection.ResponseCode);
            if (httpURLConnection.InputStream is not null)
            {
                response.Content = new StreamContent(httpURLConnection.InputStream);
            }

            if (httpURLConnection.HeaderFields is not null)
            {
                foreach (var entry in httpURLConnection.HeaderFields)
                {
                    var key = entry.Key;
                    if (key is null)
                        continue;

                    var values = entry.Value;
                    if (values is not null && values.Count > 0)
                    {
                        foreach (var value in values)
                        {
                            if (!response.Headers.TryAddWithoutValidation(key, value))
                            {
                                response.Content.Headers.TryAddWithoutValidation(key, value);
                            }
                        }
                    }
                }
            }

            return response;
        });
    }
}
#endif


using System.Text;

namespace AI.Learning.Common
{
    public sealed class LoggingHandler : DelegatingHandler
    {
        public LoggingHandler(HttpMessageHandler? httpMessageHandler=null):base(httpMessageHandler?? new HttpClientHandler()) 
        {

        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if(request.Content is not null)
            {
                await request.Content.LoadIntoBufferAsync(cancellationToken);
                Console.WriteLine($"——> {request.Method} {request.RequestUri}");
                Console.WriteLine(await request.Content.ReadAsStringAsync());
            }

            var response = await base.SendAsync(request, cancellationToken);

            if(request.Content is not null)
            {
                var origin = await response.Content.ReadAsStreamAsync();
                var ms = new MemoryStream();
                await origin.CopyToAsync(ms,cancellationToken);
                await origin.DisposeAsync();

                Console.WriteLine($"<—— {response.StatusCode.ToString()}");
                Console.WriteLine(Encoding.UTF8.GetString(ms.ToArray()));

                ms.Position = 0;
                response.Content = new StreamContent(ms);
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(response.Content.Headers.ContentType?.MediaType ?? "application/json");
            }

            return response;
        }
    }
}

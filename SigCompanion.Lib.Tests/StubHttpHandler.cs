using System.Net;
using System.Text;

namespace SigCompanion.Lib.Tests;

internal sealed class StubHttpHandler(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(respond(request), Encoding.UTF8, "application/json"),
        });
    }
}

using System.Net;

namespace HearthstoneDeckTracker.Core.Tests;

/// <summary>In-memory <see cref="HttpMessageHandler"/> so catalog/art tests never touch the network.</summary>
internal sealed class ScriptedHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _send;

    public ScriptedHttpHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send)
    {
        _send = send;
    }

    public ScriptedHttpHandler(HttpStatusCode status, string body)
        : this((_, _) => new HttpResponseMessage(status)
        {
            Content = new StringContent(body),
        })
    {
    }

    public int Calls { get; private set; }

    public List<Uri?> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Calls++;
        Requests.Add(request.RequestUri);
        return Task.FromResult(_send(request, cancellationToken));
    }
}

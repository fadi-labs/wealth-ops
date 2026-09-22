using System.Net;
using System.Text;

namespace WealthOps.Infrastructure.UnitTest.Clients.OpenAiCompatible;

/// <summary>
/// Answers HTTP calls from a script, so gateway clients can be exercised with no model running.
/// </summary>
/// <remarks>
/// NFR-3 and AC-9: the whole suite must pass with no language model available. Every response
/// here is an invented payload.
/// </remarks>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> RequestBodies { get; } = [];

    public static StubHttpMessageHandler RespondingWithJson(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    public static StubHttpMessageHandler RespondingWith(HttpStatusCode status, string body = "")
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        });

    public static StubHttpMessageHandler Throwing(Exception exception)
        => new(_ => throw exception);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (request.Content is not null)
        {
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }

        return respond(request);
    }
}

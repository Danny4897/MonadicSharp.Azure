using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace MonadicSharp.Azure.Functions.Tests.Helpers;

/// <summary>
/// Concrete implementation of <see cref="HttpRequestData"/> for unit testing.
/// Allows injecting a JSON body and inspecting the response via <see cref="TestHttpResponseData"/>.
/// </summary>
public sealed class TestHttpRequestData : HttpRequestData
{
    private readonly Stream _body;

    public TestHttpRequestData(FunctionContext context, string jsonBody = "")
        : base(context)
    {
        _body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody));
    }

    public override Stream Body => _body;
    public override HttpHeadersCollection Headers { get; } = new HttpHeadersCollection();
    public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = [];
    public override Uri Url { get; } = new Uri("https://localhost/api/test");
    public override IEnumerable<ClaimsIdentity> Identities { get; } = [];
    public override string Method { get; } = "POST";

    public override HttpResponseData CreateResponse() => new TestHttpResponseData(FunctionContext);
}

/// <summary>
/// Concrete implementation of <see cref="HttpResponseData"/> for unit testing.
/// Captures the response body written by extension methods.
/// </summary>
public sealed class TestHttpResponseData : HttpResponseData
{
    public TestHttpResponseData(FunctionContext context) : base(context)
    {
        Body = new MemoryStream();
        Headers = new HttpHeadersCollection();
    }

    public override HttpStatusCode StatusCode { get; set; }
    public override HttpHeadersCollection Headers { get; set; }
    public override Stream Body { get; set; }
    public override HttpCookies Cookies { get; } = new TestHttpCookies();

    /// <summary>
    /// Reads the response body back as a string for assertions.
    /// </summary>
    public string ReadBody()
    {
        Body.Position = 0;
        using var reader = new StreamReader(Body, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private sealed class TestHttpCookies : HttpCookies
    {
        public override void Append(string name, string value) { }
        public override void Append(IHttpCookie cookie) { }
        public override IHttpCookie CreateNew() => throw new NotImplementedException();
    }
}

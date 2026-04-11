using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Vorluno.Planilla.Web.Middleware;

namespace Planilla.Web.IntegrationTests.Middleware;

public class RequestIdMiddlewareTests
{
    [Fact]
    public async Task NoClientHeader__GeneratesGuid_AndSetsResponseHeader()
    {
        var context = BuildContext();
        var middleware = new RequestIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var requestId = context.GetRequestId();
        requestId.Should().NotBeNullOrEmpty();
        requestId.Length.Should().Be(32); // GUID "N" format = 32 hex chars sin guiones
        context.Response.Headers[RequestIdMiddleware.HeaderName].ToString().Should().Be(requestId);
    }

    [Fact]
    public async Task ClientProvidedHeader__IsRespected()
    {
        var context = BuildContext();
        context.Request.Headers[RequestIdMiddleware.HeaderName] = "my-correlation-id-xyz";
        var middleware = new RequestIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.GetRequestId().Should().Be("my-correlation-id-xyz");
        context.Response.Headers[RequestIdMiddleware.HeaderName].ToString().Should().Be("my-correlation-id-xyz");
    }

    [Fact]
    public async Task ClientProvidedEmpty__FallsBackToGuid()
    {
        var context = BuildContext();
        context.Request.Headers[RequestIdMiddleware.HeaderName] = "   ";
        var middleware = new RequestIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.GetRequestId().Should().NotBe("   ");
        context.GetRequestId().Length.Should().Be(32);
    }

    [Fact]
    public async Task ClientProvidedTooLong__FallsBackToGuid()
    {
        var context = BuildContext();
        context.Request.Headers[RequestIdMiddleware.HeaderName] = new string('x', 1000);
        var middleware = new RequestIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.GetRequestId().Should().NotContain("x");
        context.GetRequestId().Length.Should().Be(32);
    }

    [Fact]
    public async Task GetRequestId_WithoutMiddleware__ReturnsEmpty()
    {
        var context = new DefaultHttpContext();
        context.GetRequestId().Should().Be(string.Empty);
    }

    private static DefaultHttpContext BuildContext()
    {
        return new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() }
        };
    }
}

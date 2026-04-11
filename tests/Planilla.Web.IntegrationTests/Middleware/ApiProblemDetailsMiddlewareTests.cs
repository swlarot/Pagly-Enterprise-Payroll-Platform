using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Vorluno.Planilla.Web.Middleware;

namespace Planilla.Web.IntegrationTests.Middleware;

/// <summary>
/// Tests unitarios del middleware RFC 7807 para el API Platform.
/// No usa WebApplicationFactory — construye DefaultHttpContext directo.
/// </summary>
public class ApiProblemDetailsMiddlewareTests
{
    // ====================================================================
    // Scope guard: rutas que NO son /v1/* no deben ser interceptadas
    // ====================================================================

    [Fact]
    public async Task NonV1Path__DoesNotIntercept_ExceptionPropagates()
    {
        // Arrange: middleware con un _next que lanza. Si el middleware NO intercepta,
        // la excepción debería propagarse (y el legacy middleware la manejaría).
        var context = BuildContext("/api/empleados");
        var middleware = new ApiProblemDetailsMiddleware(
            next: _ => throw new InvalidOperationException("should propagate"),
            logger: NullLogger<ApiProblemDetailsMiddleware>.Instance,
            env: new FakeHostEnv("Production"));

        // Act + Assert
        await FluentActions.Invoking(() => middleware.InvokeAsync(context))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("should propagate");

        // Response no fue tocada
        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().BeNull();
    }

    [Fact]
    public async Task NonV1Path_HappyCase__PassesThrough()
    {
        var context = BuildContext("/api/empleados");
        var nextCalled = false;
        var middleware = new ApiProblemDetailsMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; },
            logger: NullLogger<ApiProblemDetailsMiddleware>.Instance,
            env: new FakeHostEnv("Production"));

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    // ====================================================================
    // V1 path: exception mapping
    // ====================================================================

    [Theory]
    [InlineData(typeof(ArgumentException), 400, "invalid_request_error")]
    [InlineData(typeof(InvalidOperationException), 400, "invalid_request_error")]
    [InlineData(typeof(UnauthorizedAccessException), 401, "authentication_error")]
    [InlineData(typeof(KeyNotFoundException), 404, "not_found_error")]
    [InlineData(typeof(NotImplementedException), 501, "api_error")]
    [InlineData(typeof(Exception), 500, "api_error")]
    public async Task V1Path_Exception__MapsToCorrectProblem(Type exceptionType, int expectedStatus, string expectedErrorCode)
    {
        var context = BuildContext("/v1/payroll/calculate");
        context.Items["RequestId"] = "test-req-123";
        var exception = (Exception)Activator.CreateInstance(exceptionType, "boom")!;

        var middleware = new ApiProblemDetailsMiddleware(
            next: _ => throw exception,
            logger: NullLogger<ApiProblemDetailsMiddleware>.Instance,
            env: new FakeHostEnv("Production"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(expectedStatus);
        context.Response.ContentType.Should().Be("application/problem+json");

        var body = ReadResponse(context);
        var problem = JsonSerializer.Deserialize<ApiProblemDetails>(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(expectedStatus);
        problem.ErrorCode.Should().Be(expectedErrorCode);
        problem.Type.Should().Be($"https://pagly.com/errors/{expectedErrorCode}");
        problem.Instance.Should().Be("/v1/payroll/calculate");
        problem.RequestId.Should().Be("test-req-123");
    }

    // ====================================================================
    // Production vs Development: detail vs exception.Message leak
    // ====================================================================

    [Fact]
    public async Task V1Path_500InProduction__DoesNotLeakExceptionMessage()
    {
        var context = BuildContext("/v1/payroll/calculate");
        context.Items["RequestId"] = "req-prod";

        var middleware = new ApiProblemDetailsMiddleware(
            next: _ => throw new Exception("SECRET DATABASE CONNECTION STRING"),
            logger: NullLogger<ApiProblemDetailsMiddleware>.Instance,
            env: new FakeHostEnv("Production"));

        await middleware.InvokeAsync(context);

        var body = ReadResponse(context);
        body.Should().NotContain("SECRET DATABASE CONNECTION STRING");

        var problem = JsonSerializer.Deserialize<ApiProblemDetails>(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })!;
        problem.Detail.Should().Contain("requestId");
        problem.Status.Should().Be(500);
    }

    [Fact]
    public async Task V1Path_500InDevelopment__LeaksExceptionMessageForDebugging()
    {
        var context = BuildContext("/v1/payroll/calculate");

        var middleware = new ApiProblemDetailsMiddleware(
            next: _ => throw new Exception("DEV friendly message"),
            logger: NullLogger<ApiProblemDetailsMiddleware>.Instance,
            env: new FakeHostEnv("Development"));

        await middleware.InvokeAsync(context);

        var problem = JsonSerializer.Deserialize<ApiProblemDetails>(
            ReadResponse(context),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        problem.Detail.Should().Be("DEV friendly message");
    }

    [Fact]
    public async Task V1Path_400InProduction__StillLeaksExceptionMessage_BecauseItsUserError()
    {
        // Razón: un 400 está causado por el cliente — el mensaje le ayuda a corregir su request.
        // No hay secretos internos en un 400.
        var context = BuildContext("/v1/payroll/calculate");

        var middleware = new ApiProblemDetailsMiddleware(
            next: _ => throw new ArgumentException("salarioPeriodo must be > 0"),
            logger: NullLogger<ApiProblemDetailsMiddleware>.Instance,
            env: new FakeHostEnv("Production"));

        await middleware.InvokeAsync(context);

        var problem = JsonSerializer.Deserialize<ApiProblemDetails>(
            ReadResponse(context),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        problem.Status.Should().Be(400);
        problem.Detail.Should().Be("salarioPeriodo must be > 0");
    }

    // ====================================================================
    // V1 happy path
    // ====================================================================

    [Fact]
    public async Task V1Path_HappyCase__DoesNotTouchResponse()
    {
        var context = BuildContext("/v1/payroll/calculate");
        var middleware = new ApiProblemDetailsMiddleware(
            next: ctx => { ctx.Response.StatusCode = 200; return Task.CompletedTask; },
            logger: NullLogger<ApiProblemDetailsMiddleware>.Instance,
            env: new FakeHostEnv("Production"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(200);
        ReadResponse(context).Should().BeEmpty();
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private static DefaultHttpContext BuildContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "POST";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadResponse(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private class FakeHostEnv : IHostEnvironment
    {
        public FakeHostEnv(string name) { EnvironmentName = name; }
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

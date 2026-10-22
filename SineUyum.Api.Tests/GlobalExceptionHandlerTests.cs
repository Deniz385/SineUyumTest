using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Localization;
using SineUyum.Api.Middleware;

namespace SineUyum.Api.Tests;

// TestServer does not apply Kestrel's body limit, so the mapping is tested directly.
public class GlobalExceptionHandlerTests
{
    private static async Task<(int Status, string Title)> HandleAsync(Exception exception, string language = "en")
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance,
            new JsonStringLocalizer(LocaleCatalog.Shared, language));
        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, doc.RootElement.GetProperty("title").GetString()!);
    }

    [Theory]
    [InlineData(413)]
    [InlineData(400)]
    public async Task KestrelRejection_KeepsItsStatusCode(int status) =>
        Assert.Equal(status, (await HandleAsync(new BadHttpRequestException("kestrel", status))).Status);

    [Fact]
    public async Task UnexpectedError_500() =>
        Assert.Equal(500, (await HandleAsync(new InvalidOperationException("internal detail"))).Status);

    // A domain error's key and arguments are rendered in the request language.
    [Fact]
    public async Task DomainError_TitleLocalizedWithArguments()
    {
        var error = new DomainException("matching.notEnoughParticipants", [4, 1]);

        var en = await HandleAsync(error);
        Assert.Equal(400, en.Status);
        Assert.Equal("Groups could not be created because there are not enough participants. Required: 4, current: 1", en.Title);

        var tr = await HandleAsync(error, "tr");
        Assert.Equal("Yeterli katılımcı olmadığı için gruplar oluşturulamadı. Gerekli: 4, Mevcut: 1", tr.Title);
    }
}

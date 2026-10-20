using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SineUyum.Api.Middleware;

namespace SineUyum.Api.Tests;

// TestServer, Kestrel'in gövde sınırını uygulamaz; bu yüzden eşleme doğrudan sınanır.
public class GlobalExceptionHandlerTests
{
    private static async Task<int> StatusFor(Exception exception)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);
        Assert.True(handled);
        return context.Response.StatusCode;
    }

    [Theory]
    [InlineData(413)]
    [InlineData(400)]
    public async Task KestrelReddi_KendiDurumKoduylaDoner(int status) =>
        Assert.Equal(status, await StatusFor(new BadHttpRequestException("kestrel", status)));

    [Fact]
    public async Task BeklenmeyenHata_500() =>
        Assert.Equal(500, await StatusFor(new InvalidOperationException("iç ayrıntı")));
}

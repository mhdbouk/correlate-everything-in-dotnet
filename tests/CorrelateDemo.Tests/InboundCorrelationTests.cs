using System.Net;
using CorrelateDemo.ServiceB;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace CorrelateDemo.Tests;

public class InboundCorrelationTests : IClassFixture<WebApplicationFactory<EntryMarker>>
{
    private readonly WebApplicationFactory<EntryMarker> _factory;

    public InboundCorrelationTests(WebApplicationFactory<EntryMarker> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Should_echo_supplied_header_on_response()
    {
        using var client = _factory.CreateClient();
        var expected = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", expected);

        var response = await client.GetAsync("/downstream");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-Correlation-ID", out var values).ShouldBeTrue();
        values!.ShouldContain(expected);
    }

    [Fact]
    public async Task Should_generate_an_id_when_request_has_no_header()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/downstream");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("X-Correlation-ID").ShouldBeTrue();
    }
}

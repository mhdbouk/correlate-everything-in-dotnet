using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Shouldly;
using ServiceAMarker = CorrelateDemo.ServiceA.EntryMarker;
using ServiceBMarker = CorrelateDemo.ServiceB.EntryMarker;

namespace CorrelateDemo.Tests;

public class CrossServicePropagationTests : IAsyncLifetime
{
    private WebApplicationFactory<ServiceAMarker> _serviceA = null!;
    private WebApplicationFactory<ServiceBMarker> _serviceB = null!;

    public Task InitializeAsync()
    {
        _serviceB = new WebApplicationFactory<ServiceBMarker>();

        _serviceA = new WebApplicationFactory<ServiceAMarker>()
            .WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            {
                services.Configure<HttpClientFactoryOptions>("downstream", opts =>
                {
                    opts.HttpMessageHandlerBuilderActions.Add(builder =>
                    {
                        builder.PrimaryHandler = _serviceB.Server.CreateHandler();
                    });
                });
            }));

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _serviceA.DisposeAsync();
        await _serviceB.DisposeAsync();
    }

    [Fact]
    public async Task Should_flow_supplied_correlation_id_from_ServiceA_to_ServiceB()
    {
        using var client = _serviceA.CreateClient();
        var expected = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", expected);

        var response = await client.PostAsJsonAsync("/orders", new { OrderId = Guid.NewGuid() });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        body.ShouldNotBeNull();
        body["serviceACorrelationId"].ShouldBe(expected);
        body["serviceBCorrelationId"].ShouldBe(expected);
    }

    [Fact]
    public async Task Should_share_generated_id_across_services_when_caller_sent_none()
    {
        using var client = _serviceA.CreateClient();

        var response = await client.PostAsJsonAsync("/orders", new { OrderId = Guid.NewGuid() });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        body.ShouldNotBeNull();
        body["serviceACorrelationId"].ShouldNotBe("none");
        body["serviceBCorrelationId"].ShouldBe(body["serviceACorrelationId"]);
    }
}

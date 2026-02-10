---
name: test-integration-api
description: End-to-end API testing - WebApplicationFactory for full request pipeline testing, authenticated client creation, HTTP dependency mocking (Flurl), typed client testing, and route validation. Use for testing controllers, endpoints, and API behavior. Keywords: api-testing, e2e-testing, webapplicationfactory, http-mocking, flurl, endpoint-testing, controller-testing, authentication, integration-testing, dtt, skills, test, integration
---

# API Integration Testing

> End-to-end API testing using `WebApplicationFactory` and HTTP interaction mocking.

## Core Patterns

### Authenticated Client
`CreateClientAsync` automatically handles:
1. App startup
2. DB migrations & external dependencies binding
3. Test user authentication

```csharp
[Theory]
[DataInline]
public async Task Api_Should_Return_Data(DrnTestContext context, ITestOutputHelper outputHelper)
{
    var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
    
    var endpoint = Get.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
    var forecasts = await client.GetFromJsonAsync<WeatherForecast[]>(endpoint);
    
    forecasts.Should().NotBeEmpty();
}
```

### Mocking HTTP Dependencies
Mock downstream services (Typed Clients, InternalRequest, ExternalRequest) to test proxy behavior and failure modes.

> [!TIP]
> Typed clients (like `INexusClient`, `IXyzApiClient`) typically use `Flurl` under the hood. Mock them by targeting their URL patterns.

#### Typed HTTP Clients (e.g., `INexusClient`)
Most typed clients wrap `IInternalRequest` (for internal mesh services) or `IExternalRequest`.
The pattern for testing them is identical: mock the underlying URL pattern.

```csharp
[Theory]
[DataInline]
public async Task Should_Proxy_Nexus_Response(DrnTestContext context, ITestOutputHelper outputHelper)
{
    var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
    var appSettings = context.GetRequiredService<IAppSettings>();
    
    // Pattern: {ServiceAddress}/{Path}
    // IInternalRequest automatically handles the base URL from settings
    // Example: Mocking a Nexus service call
    var nexusUrl = $"*{appSettings.NexusAppSettings.NexusAddress}/WeatherForecast";
    var mockData = new[] { new WeatherForecast { Summary = "Mocked Nexus Data" } };
    
    // Mock the specific call
    context.FlurlHttpTest.ForCallsTo(nexusUrl).RespondWithJson(mockData);
    
    // Call the application endpoint that triggers the Client
    var endpoint = Get.Endpoint.Sample.WeatherForecast.GetNexusWeatherForecasts.RoutePattern;
    var result = await client.GetFromJsonAsync<WeatherForecast[]>(endpoint);
    
    result.Should().BeEquivalentTo(mockData);
}
```

#### External Request (`IExternalRequest`)
For direct external calls (e.g., 3rd party APIs).

```csharp
[Theory]
[DataInline]
public async Task Should_Mock_External_Api(DrnTestContext context, ITestOutputHelper outputHelper)
{
    var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
    
    context.FlurlHttpTest
        .ForCallsTo("https://api.thirdparty.com/*")
        .RespondWith("OK", 200);
        
    // ... triggering action
}
```

### Simulating Failures

```csharp
[Theory]
[DataInline]
public async Task Should_Handle_API_Failure(DrnTestContext context, ITestOutputHelper output)
{
    var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(output);
    
    // Simulate server error
    context.FlurlHttpTest.ForCallsTo("*").RespondWith(status: 500);
    
    var response = await client.GetAsync("/api/external-dependent");
    response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
}
```

### Route Validation
Validate route patterns without starting the full app server if only route data is needed.

```csharp
[Theory]
[DataInline]
public async Task EndPointFor_Should_Return_Endpoint_Address(DrnTestContext context)
{
    await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<SampleProgram>();
    
    var endpoint = Get.Endpoint.User.Identity.RegisterController.ConfirmEmail;
    endpoint.RoutePattern.Should().NotBeNull();
}
```

### Test Isolation

`ApplicationContext` automatically sets the following flags to ensure your tests run in isolation and do not attempt to launch external dependencies (which would collide with test containers):

1. `TestEnvironment.DrnTestContextEnabled` is set to `true`.
2. `AppSettings.DevelopmentSettings.TemporaryApplication` is set to `true`.

This ensures that logic like `LaunchExternalDependenciesAsync` knows it is running in a test context and skips starting local development containers.

---

## Related
- [test-integration.md](../test-integration/SKILL.md)


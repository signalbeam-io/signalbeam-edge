---
paths:
  - "tests/**/*.cs"
---

# Testing Rules

## Unit Tests
- xUnit with `[Fact]` for single cases, `[Theory]` with `[InlineData]` for parameterized
- NSubstitute for mocking: `var repo = Substitute.For<IDeviceRepository>();`
- FluentAssertions for assertions: `result.Should().BeSuccess()`, `result.Value.Should().NotBeNull()`
- Arrange-Act-Assert pattern with clear sections
- Verify calls: `repo.Received(1).AddAsync(Arg.Any<Device>(), Arg.Any<CancellationToken>())`

## Integration Tests
- Custom `WebApplicationFactory<Program>` subclass per service (e.g., `DeviceManagerWebApplicationFactory`)
- Implements `IAsyncLifetime` for setup/teardown
- Testcontainers for PostgreSQL with TimescaleDB image
- `CreateAuthenticatedClient()` returns `HttpClient` with default API key headers
- Database reset between tests: `EnsureDeletedAsync()` then `EnsureCreatedAsync()`
- Use `PostAsJsonAsync()`, `GetAsync()`, `DeleteAsync()` on HttpClient
- Assert on HTTP status codes and deserialized response bodies

## Test Organization
- Unit tests: `tests/SignalBeam.Domain.Tests/`
- Integration tests: `tests/{ServiceName}.Tests.Integration/`
- Integration tests filtered by `Category=Integration`

## Naming
- Test classes: `{ClassUnderTest}Tests`
- Test methods: `{Method}_{Scenario}_{ExpectedResult}` or descriptive sentence
- Example: `Handle_WhenDeviceExists_ReturnsConflict`

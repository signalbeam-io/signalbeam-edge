using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Models;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Tests.Unit.Commands;

public class RequestCertificateCommandHandlerTests
{
    private readonly ICloudClient _cloudClient = Substitute.For<ICloudClient>();
    private readonly ICsrGenerator _csrGenerator = Substitute.For<ICsrGenerator>();
    private readonly ICertificateStore _certificateStore = Substitute.For<ICertificateStore>();
    private readonly IDeviceCredentialsStore _credentialsStore = Substitute.For<IDeviceCredentialsStore>();
    private readonly RequestCertificateCommandHandler _handler;

    private readonly Guid _deviceId = Guid.NewGuid();

    public RequestCertificateCommandHandlerTests()
    {
        var logger = Substitute.For<ILogger<RequestCertificateCommandHandler>>();
        _handler = new RequestCertificateCommandHandler(
            _cloudClient, _csrGenerator, _certificateStore, _credentialsStore, logger);
    }

    [Fact]
    public async Task Handle_GeneratesCsrSignsStoresAndUpdatesCredentials()
    {
        var credentials = new DeviceCredentials { DeviceId = _deviceId, ApiKey = "sb_device_x" };
        _credentialsStore.LoadCredentialsAsync(Arg.Any<CancellationToken>()).Returns(credentials);
        _csrGenerator.GenerateCsr(Arg.Any<string>()).Returns(("csr-pem", "key-pem"));
        var expiry = DateTimeOffset.UtcNow.AddDays(90);
        _cloudClient.RequestCertificateAsync(_deviceId, "csr-pem", Arg.Any<CancellationToken>())
            .Returns(new DeviceCertificateBundle("cert-pem", "ca-pem", "SERIAL123", expiry));
        _certificateStore.SaveAsync("cert-pem", "key-pem", "ca-pem", Arg.Any<CancellationToken>())
            .Returns(new StoredCertificatePaths("/certs/device.crt", "/certs/device.key", "/certs/ca.crt"));

        var result = await _handler.Handle(new RequestCertificateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SerialNumber.Should().Be("SERIAL123");
        credentials.ClientCertificatePath.Should().Be("/certs/device.crt");
        credentials.ClientPrivateKeyPath.Should().Be("/certs/device.key");
        credentials.CaCertificatePath.Should().Be("/certs/ca.crt");
        credentials.CertificateSerialNumber.Should().Be("SERIAL123");
        credentials.CertificateExpiresAt.Should().Be(expiry);
        await _credentialsStore.Received(1).SaveCredentialsAsync(
            Arg.Is<DeviceCredentials>(c => c.CertificateSerialNumber == "SERIAL123"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotRegistered_ReturnsFailure()
    {
        _credentialsStore.LoadCredentialsAsync(Arg.Any<CancellationToken>()).Returns((DeviceCredentials?)null);

        var result = await _handler.Handle(new RequestCertificateCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _cloudClient.DidNotReceive().RequestCertificateAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoApiKey_ReturnsFailure()
    {
        _credentialsStore.LoadCredentialsAsync(Arg.Any<CancellationToken>())
            .Returns(new DeviceCredentials { DeviceId = _deviceId, ApiKey = null });

        var result = await _handler.Handle(new RequestCertificateCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _cloudClient.DidNotReceive().RequestCertificateAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

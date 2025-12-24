using FluentValidation;
using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Models;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Application.Storage;
using SignalBeam.BundleOrchestrator.Application.Validators;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Commands;

public class UploadBundleHandlerTests
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly IBundleStorageService _bundleStorageService;
    private readonly IValidator<BundleDefinition> _validator;
    private readonly UploadBundleHandler _handler;

    public UploadBundleHandlerTests()
    {
        _bundleRepository = Substitute.For<IBundleRepository>();
        _bundleVersionRepository = Substitute.For<IBundleVersionRepository>();
        _bundleStorageService = Substitute.For<IBundleStorageService>();
        _validator = new BundleDefinitionValidator();

        _handler = new UploadBundleHandler(
            _bundleRepository,
            _bundleVersionRepository,
            _bundleStorageService,
            _validator);
    }

    [Fact]
    public async Task Handle_ShouldUploadBundleAndCreateVersion()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var bundleId = Guid.NewGuid().ToString();

        var definition = new BundleDefinition
        {
            BundleId = bundleId,
            Version = "1.0.0",
            Description = "Warehouse monitoring",
            Containers = new List<ContainerDefinition>
            {
                new()
                {
                    Name = "temp-sensor",
                    Image = "ghcr.io/signalbeam/temp-sensor:1.0.0"
                }
            }
        };

        var command = new UploadBundleCommand(tenantId, definition);

        _bundleRepository.GetByIdAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns((AppBundle?)null);

        _bundleStorageService.UploadBundleWithMetadataAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Stream>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var checksumOverride = callInfo.ArgAt<string?>(4) ?? "sha256:missing";
                return new BundleMetadata(
                    "https://blob.local/bundles/manifest.json",
                    checksumOverride,
                    512);
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.BundleId.Should().Be(Guid.Parse(bundleId));
        result.Value.Version.Should().Be("1.0.0");
        result.Value.BlobStorageUri.Should().Be("https://blob.local/bundles/manifest.json");
        result.Value.SizeBytes.Should().Be(512);

        await _bundleStorageService.Received(1).UploadBundleWithMetadataAsync(
            tenantId.ToString(),
            bundleId,
            "1.0.0",
            Arg.Any<Stream>(),
            Arg.Is<string?>(checksum => !string.IsNullOrWhiteSpace(checksum)),
            Arg.Any<CancellationToken>());

        await _bundleRepository.Received(1).AddAsync(
            Arg.Is<AppBundle>(b => b.TenantId.Value == tenantId),
            Arg.Any<CancellationToken>());

        await _bundleVersionRepository.Received(1).AddAsync(
            Arg.Is<AppBundleVersion>(v =>
                v.BundleId.Value.ToString() == bundleId &&
                v.Version.ToString() == "1.0.0" &&
                v.Status == BundleStatus.Published &&
                v.BlobStorageUri == "https://blob.local/bundles/manifest.json"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenChecksumMismatch()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var bundleId = Guid.NewGuid().ToString();

        var definition = new BundleDefinition
        {
            BundleId = bundleId,
            Version = "1.0.0",
            Checksum = "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
            Containers = new List<ContainerDefinition>
            {
                new()
                {
                    Name = "temp-sensor",
                    Image = "ghcr.io/signalbeam/temp-sensor:1.0.0"
                }
            }
        };

        var command = new UploadBundleCommand(tenantId, definition);

        _bundleRepository.GetByIdAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns((AppBundle?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("CHECKSUM_MISMATCH");

        await _bundleStorageService.DidNotReceive().UploadBundleWithMetadataAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Stream>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }
}

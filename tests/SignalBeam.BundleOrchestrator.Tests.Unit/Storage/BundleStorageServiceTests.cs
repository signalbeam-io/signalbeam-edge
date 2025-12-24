using SignalBeam.BundleOrchestrator.Infrastructure.Storage;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Storage;

public class BundleStorageServiceTests
{
    [Fact]
    public void GetBlobName_BuildsTenantScopedPath()
    {
        var blobName = BundleStorageService.GetBlobName(
            "tenant-123",
            "bundle-456",
            "1.2.3");

        blobName.Should().Be("tenant-123/bundle-456/versions/1.2.3.json");
    }
}

# Bundle Artifact Blob Storage

## Overview

The BundleOrchestrator uses Azure Blob Storage to store bundle manifests and artifacts that are deployed to edge devices. This document describes the storage structure and usage patterns.

## Container Structure

**Container Name:** `bundle-manifests` (configurable via `AzureBlobStorage:ContainerName`)

**Blob Naming Convention:**
```
{tenantId}/{bundleId}/versions/{version}.json
```

### Example Structure
```
bundle-manifests/
├── 550e8400-e29b-41d4-a716-446655440000/
│   ├── warehouse-monitor/
│   │   └── versions/
│   │       ├── 1.0.0.json
│   │       ├── 1.1.0.json
│   │       └── 2.0.0.json
│   ├── temp-sensor/
│   │   └── versions/
│   │       ├── 1.0.0.json
│   │       └── 1.1.0.json
```

## Bundle Manifest Format

Each `manifest.json` file contains the container specifications for a specific bundle version:

```json
{
  "bundleId": "warehouse-monitor",
  "version": "1.2.0",
  "containers": [
    {
      "name": "temp-sensor",
      "image": "ghcr.io/org/temp-sensor:1.2.0",
      "environmentVariables": "{\"API_KEY\":\"xyz\"}",
      "portMappings": "[{\"host\":8080,\"container\":80}]",
      "volumeMounts": "[{\"/data\":\"/app/data\"}]"
    },
    {
      "name": "relay-controller",
      "image": "ghcr.io/org/relay-controller:2.0.1"
    }
  ],
  "createdAt": "2025-12-18T10:00:00Z",
  "releaseNotes": "Added new temperature sensor support"
}
```

## Authentication Methods

### Development (Connection String)
```json
{
  "AzureBlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=signalbeam;AccountKey=...;EndpointSuffix=core.windows.net",
    "ContainerName": "bundle-manifests"
  }
}
```

### Production (Managed Identity)
```json
{
  "AzureBlobStorage": {
    "ServiceUri": "https://signalbeam.blob.core.windows.net",
    "UseManagedIdentity": true,
    "ContainerName": "bundle-manifests"
  }
}
```

**Required Azure RBAC Role:** `Storage Blob Data Contributor`

## SAS Token Generation

The `BundleStorageService.GenerateBundleDownloadUrlAsync` method generates time-limited SAS (Shared Access Signature) tokens that allow edge agents to download bundle manifests securely without authentication.

### SAS Token Properties
- **Permissions:** Read-only
- **Validity:** Configurable (typically 1-24 hours)
- **Clock Skew:** Starts 5 minutes before current time to handle clock differences

### Example Usage
```csharp
// Generate a download URL valid for 1 hour
var downloadUrl = await bundleStorageService.GenerateBundleDownloadUrlAsync(
    tenantId: "550e8400-e29b-41d4-a716-446655440000",
    bundleId: "warehouse-monitor",
    version: "1.2.0",
    validity: TimeSpan.FromHours(1));

// Edge agent can now download the manifest using this URL
// https://signalbeam.blob.core.windows.net/bundle-manifests/550e8400-e29b-41d4-a716-446655440000/warehouse-monitor/versions/1.2.0.json?sv=2021-08-06&se=2025-12-18T12%3A00%3A00Z&sr=b&sp=r&sig=...
```

## Blob Metadata

Each uploaded manifest includes metadata for tracking:
- `tenantId`: The tenant identifier
- `bundleId`: The bundle identifier
- `version`: The bundle version
- `uploadedAt`: ISO 8601 timestamp of upload

## API Methods

### IBundleStorageService

| Method | Description |
|--------|-------------|
| `UploadBundleManifestAsync` | Uploads a bundle manifest to blob storage |
| `GenerateBundleDownloadUrlAsync` | Generates a SAS token URL for secure downloads |
| `DownloadBundleManifestAsync` | Downloads a bundle manifest from blob storage |
| `DeleteBundleManifestAsync` | Deletes a bundle manifest |
| `BundleManifestExistsAsync` | Checks if a bundle manifest exists |

## Security Considerations

1. **Container Access:** Containers are created with `PublicAccessType.None` to prevent anonymous access
2. **SAS Tokens:** Time-limited tokens prevent indefinite access to bundle artifacts
3. **Managed Identity:** Production deployments should use Managed Identity instead of connection strings
4. **Blob Encryption:** Azure Storage encrypts all data at rest by default

## Disaster Recovery

### Backup Strategy
- Enable Azure Blob Storage versioning for automatic backup
- Use Azure Blob Storage lifecycle management to archive old versions
- Consider geo-redundant storage (GRS or GZRS) for production

### Retention Policy
Bundle manifests should be retained based on your rollback requirements:
- **Recommended:** Keep all versions for 90 days
- **Minimum:** Keep at least the last 3 versions

## Cost Optimization

1. **Storage Tier:** Use Hot tier for frequently accessed bundles, Cool tier for older versions
2. **Lifecycle Policies:** Automatically move blobs to Cool/Archive tier after 30/90 days
3. **Compression:** Consider compressing large bundle manifests before upload
4. **Cleanup:** Delete old bundle versions that are no longer deployed to any devices

## Monitoring

Monitor the following metrics in Azure Portal:
- **Blob Storage Transactions:** Track upload/download operations
- **SAS Token Generation Failures:** May indicate permission issues
- **Storage Capacity:** Monitor container size growth
- **Egress Bandwidth:** Track data transfer costs for edge agent downloads

## Troubleshooting

### "Failed to generate SAS token"
- Ensure the BlobServiceClient is created with `StorageSharedKeyCredential` or Managed Identity
- Verify Managed Identity has `Storage Blob Data Reader` role
- Check if the blob exists before generating SAS token

### "Access Denied" errors
- Verify Azure RBAC role assignments
- Check if Managed Identity is properly configured
- Ensure SAS token hasn't expired

### "Blob not found"
- Verify the blob name follows the naming convention: `bundles/{bundleId}/{version}/manifest.json`
- Check if the bundle manifest was successfully uploaded
- Confirm you're using the correct container name

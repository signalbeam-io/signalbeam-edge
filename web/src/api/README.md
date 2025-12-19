# API Client Layer

This directory contains the API client implementation for the SignalBeam Edge web application.

## Structure

```
api/
├── types/                  # TypeScript type definitions
│   ├── common.ts          # Common types (pagination, etc.)
│   ├── device.ts          # Device-related types
│   ├── heartbeat.ts       # Heartbeat types
│   ├── group.ts           # Device group types
│   ├── bundle.ts          # App bundle types
│   ├── rollout.ts         # Rollout types
│   ├── error.ts           # Error types
│   └── index.ts           # Barrel export
│
├── services/              # API service clients
│   ├── devices.api.ts     # Devices API
│   ├── heartbeat.api.ts   # Heartbeat API
│   ├── groups.api.ts      # Groups API
│   ├── bundles.api.ts     # Bundles API
│   ├── rollouts.api.ts    # Rollouts API
│   └── index.ts           # Barrel export
│
├── client.ts              # Axios client configuration
└── README.md              # This file
```

## Usage

### Using API Services Directly

```typescript
import { devicesApi } from '@/api/services'

// Fetch devices
const devices = await devicesApi.getDevices({ status: 'online' })

// Get single device
const device = await devicesApi.getDevice('device-id')

// Update device
const updated = await devicesApi.updateDevice('device-id', { name: 'New Name' })
```

### Using TanStack Query Hooks (Recommended)

The recommended way to interact with the API is through the TanStack Query hooks located in `hooks/api/`:

```typescript
import { useDevices, useUpdateDevice } from '@/hooks/api'

function DevicesPage() {
  // Fetch devices with automatic caching and refetching
  const { data, isLoading, error } = useDevices({ status: 'online' })

  // Mutation hook for updating devices
  const updateDevice = useUpdateDevice()

  const handleUpdate = async (id: string, name: string) => {
    await updateDevice.mutateAsync({ id, data: { name } })
  }

  if (isLoading) return <div>Loading...</div>
  if (error) return <div>Error: {error.message}</div>

  return (
    <div>
      {data?.data.map(device => (
        <div key={device.id}>{device.name}</div>
      ))}
    </div>
  )
}
```

## Available Hooks

### Devices

- `useDevices(filters?)` - Fetch paginated devices
- `useDevice(id)` - Fetch single device
- `useRegisterDevice()` - Register new device (mutation)
- `useUpdateDevice()` - Update device (mutation)
- `useDeleteDevice()` - Delete device (mutation)

### Heartbeat

- `useHeartbeats(filters)` - Fetch device heartbeat history
- `useLatestHeartbeat(deviceId)` - Get latest heartbeat (auto-refreshes)
- `useSendHeartbeat()` - Send heartbeat (mutation)

### Groups

- `useGroups(filters?)` - Fetch paginated groups
- `useGroup(id)` - Fetch single group
- `useCreateGroup()` - Create group (mutation)
- `useUpdateGroup()` - Update group (mutation)
- `useDeleteGroup()` - Delete group (mutation)
- `useAddDevicesToGroup()` - Add devices to group (mutation)
- `useRemoveDevicesFromGroup()` - Remove devices from group (mutation)

### Bundles

- `useBundles(filters?)` - Fetch paginated bundles
- `useBundle(id)` - Fetch single bundle
- `useCreateBundle()` - Create bundle (mutation)
- `useUpdateBundle()` - Update bundle (mutation)
- `useDeleteBundle()` - Delete bundle (mutation)
- `useBundleVersions(bundleId)` - Fetch bundle versions
- `useCreateBundleVersion()` - Create bundle version (mutation)
- `useSetActiveVersion()` - Set active version (mutation)

### Rollouts

- `useRollouts(filters?)` - Fetch paginated rollouts
- `useRollout(id)` - Fetch single rollout (auto-refreshes when in progress)
- `useCreateRollout()` - Create rollout (mutation)
- `useCancelRollout()` - Cancel rollout (mutation)
- `useDeviceRolloutStatus(rolloutId)` - Get device-level status (auto-refreshes)
- `useRetryFailedDevices()` - Retry failed devices (mutation)

## Error Handling

All API calls throw `ApiException` with detailed error information:

```typescript
import { getErrorMessage, isApiException } from '@/api/client'

try {
  await devicesApi.getDevice('invalid-id')
} catch (error) {
  if (isApiException(error)) {
    console.log('Status:', error.statusCode)
    console.log('Code:', error.code)
    console.log('Message:', error.message)
    console.log('Details:', error.details)
  }

  // Get user-friendly message
  const message = getErrorMessage(error)
}
```

### Automatic Retry Logic

The query client is configured with intelligent retry logic:

**Queries:**
- Retries up to 3 times on network errors and 5xx errors
- Does NOT retry on 4xx errors (except 408 timeout and 429 rate limit)
- Uses exponential backoff (1s, 2s, 4s, etc.)
- Auto-refetches on reconnect

**Mutations:**
- Retries up to 2 times on network errors and 5xx errors only
- Does NOT retry on client errors (4xx)
- Uses exponential backoff

## Authentication

The API client automatically:
- Adds JWT token from localStorage to all requests
- Redirects to `/login` on 401 Unauthorized responses
- Clears auth token on logout

## Configuration

### Environment Variables

Create a `.env` file:

```env
VITE_API_URL=http://localhost:8080
```

### Query Client Configuration

Customize in `src/lib/query-client.ts`:

```typescript
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5, // 5 minutes
      gcTime: 1000 * 60 * 10,   // 10 minutes
      retry: shouldRetry,
      retryDelay: getRetryDelay,
    },
  },
})
```

## Best Practices

1. **Always use hooks** instead of calling API services directly in components
2. **Handle loading and error states** in your UI
3. **Use optimistic updates** for better UX when appropriate
4. **Invalidate queries** after mutations to keep data fresh
5. **Use the `enabled` option** to conditionally fetch data
6. **Leverage staleTime** to reduce unnecessary requests

## Example: Complete CRUD Flow

```typescript
import {
  useDevices,
  useDevice,
  useUpdateDevice,
  useDeleteDevice
} from '@/hooks/api'

function DeviceManagement() {
  // List devices
  const { data: devices, isLoading } = useDevices()

  // Get single device
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const { data: device } = useDevice(selectedId!, { enabled: !!selectedId })

  // Update mutation
  const updateDevice = useUpdateDevice()
  const handleUpdate = (id: string, name: string) => {
    updateDevice.mutate({ id, data: { name } }, {
      onSuccess: () => {
        toast.success('Device updated!')
      },
      onError: (error) => {
        toast.error(getErrorMessage(error))
      },
    })
  }

  // Delete mutation
  const deleteDevice = useDeleteDevice()
  const handleDelete = (id: string) => {
    if (confirm('Are you sure?')) {
      deleteDevice.mutate(id)
    }
  }

  // Render UI...
}
```

## TypeScript Support

All types are fully typed with TypeScript. Import types from `@/api/types`:

```typescript
import type {
  Device,
  DeviceStatus,
  DeviceFilters,
  RegisterDeviceRequest
} from '@/api/types'
```

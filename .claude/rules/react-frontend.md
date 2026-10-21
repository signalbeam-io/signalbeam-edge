---
paths:
  - "web/src/**/*.ts"
  - "web/src/**/*.tsx"
---

# React Frontend Rules

## API Client
- Axios-based `apiClient` singleton with interceptors
- Request interceptor adds auth: JWT Bearer token or X-Api-Key header from `useAuthStore`
- Response interceptor handles 401 -> redirect to login
- All API calls wrapped in `apiRequest<T>(config)` for typed error handling
- `ApiException` class with code, status, and details fields

## API Services
- Organized by domain: `devicesApi.ts`, `bundlesApi.ts`, `groupsApi.ts`
- Functions return typed `Promise<T>`
- Use `URLSearchParams` for query parameters
- Pagination via `pageNumber` and `pageSize` params
- All list/detail API calls append tenant scope via `appendTenantId(params)` (or `withTenantId`) from `web/src/api/services/tenant` — multi-tenancy is mandatory on every data-fetching call
- Response mapping functions: `mapDevice(response)` to convert API shapes to frontend types

## State Management
- **Server state:** TanStack Query (React Query) exclusively
  - One hook per query/mutation: `useDevices()`, `useRegisterDevice()`
  - Query hooks: `useQuery({ queryKey: [...], queryFn: ... })`
  - Mutation hooks: `useMutation({ mutationFn: ..., onSuccess: ... })`
  - Invalidate queries on mutations: `queryClient.invalidateQueries({ queryKey: [...] })`
  - Stale time: 30-60 seconds, refetch intervals: 60 seconds for live data
- **Client state:** Zustand with `persist` middleware for auth, preferences
  - `useAuthStore` stores user, tokens, subscription info
  - Persisted to localStorage with `partialize` to select stored fields

## Components
- shadcn/ui components with Tailwind CSS
- TypeScript strict mode, no `any` types
- Props defined as interfaces
- Functional components only

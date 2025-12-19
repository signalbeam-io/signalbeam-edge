# SignalBeam Web UI

Modern React + TypeScript frontend for the SignalBeam Edge platform.

## Tech Stack

- **React 18** - UI library
- **TypeScript** - Type safety with strict mode enabled
- **Vite** - Build tool and dev server
- **Tailwind CSS** - Utility-first CSS framework
- **shadcn/ui** - High-quality component library
- **TanStack Query** - Server state management
- **Zustand** - Client state management
- **React Router v6** - Routing
- **Axios** - HTTP client
- **React Hook Form + Zod** - Form handling and validation
- **date-fns** - Date utilities

## Prerequisites

- Node.js 20+ (recommended: use [nvm](https://github.com/nvm-sh/nvm))
- npm, yarn, or pnpm

## Getting Started

### 1. Install Dependencies

```bash
npm install
# or
yarn install
# or
pnpm install
```

### 2. Environment Setup

Copy the example environment file:

```bash
cp .env.example .env.development
```

Edit `.env.development` to configure your local API endpoint:

```env
VITE_API_URL=http://localhost:8080
VITE_APP_ENV=development
VITE_ENABLE_DEVTOOLS=true
```

### 3. Run Development Server

```bash
npm run dev
```

The application will start at `http://localhost:3000`

## Available Scripts

- `npm run dev` - Start development server with hot reload
- `npm run build` - Build for production
- `npm run preview` - Preview production build locally
- `npm run lint` - Run ESLint
- `npm run lint:fix` - Fix ESLint errors automatically
- `npm run format` - Format code with Prettier
- `npm run format:check` - Check code formatting
- `npm run type-check` - Run TypeScript type checking

## Project Structure

```
web/
├── public/              # Static assets
├── src/
│   ├── api/             # API client and endpoints
│   │   └── client.ts    # Axios instance with interceptors
│   ├── components/      # Reusable UI components
│   │   └── layouts/     # Layout components
│   ├── features/        # Feature-based modules
│   │   ├── dashboard/   # Dashboard feature
│   │   ├── devices/     # Devices management
│   │   └── bundles/     # App bundles management
│   ├── hooks/           # Custom React hooks
│   │   └── use-toast.ts # Toast notifications hook
│   ├── lib/             # Utilities and helpers
│   │   ├── utils.ts     # Utility functions (cn, etc.)
│   │   ├── query-client.ts  # React Query configuration
│   │   └── constants.ts # App constants
│   ├── routes/          # Route configurations
│   │   └── index.tsx    # Main routes
│   ├── stores/          # Zustand stores
│   │   └── auth-store.ts # Authentication state
│   ├── App.tsx          # Root component
│   ├── main.tsx         # Entry point
│   ├── index.css        # Global styles + Tailwind
│   └── vite-env.d.ts    # TypeScript env declarations
├── index.html           # HTML template
├── package.json         # Dependencies and scripts
├── tsconfig.json        # TypeScript configuration
├── vite.config.ts       # Vite configuration
├── tailwind.config.js   # Tailwind CSS configuration
├── postcss.config.js    # PostCSS configuration
├── eslint.config.js     # ESLint configuration
├── .prettierrc          # Prettier configuration
└── components.json      # shadcn/ui configuration
```

## Code Style & Quality

### TypeScript

This project uses **strict TypeScript** configuration:

- Strict null checks
- No implicit any
- Unused locals/parameters detection
- No unchecked indexed access
- Exact optional property types

### ESLint + Prettier

Code quality is enforced with:

- **ESLint** - Linting with TypeScript rules
- **Prettier** - Code formatting with Tailwind CSS class sorting
- Pre-configured rules for React and TypeScript best practices

Run `npm run lint:fix` and `npm run format` before committing.

## Adding Components with shadcn/ui

This project is configured to use shadcn/ui components. To add a new component:

```bash
npx shadcn-ui@latest add button
npx shadcn-ui@latest add card
npx shadcn-ui@latest add input
# etc.
```

Components will be added to `src/components/ui/` and can be customized.

## State Management

### Server State (TanStack Query)

Use TanStack Query for all server data fetching:

```typescript
import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/api/client'

function useDevices() {
  return useQuery({
    queryKey: ['devices'],
    queryFn: async () => {
      const { data } = await apiClient.get('/api/devices')
      return data
    },
  })
}
```

### Client State (Zustand)

Use Zustand for client-side state like auth, UI preferences:

```typescript
import { create } from 'zustand'

interface Store {
  count: number
  increment: () => void
}

const useStore = create<Store>((set) => ({
  count: 0,
  increment: () => set((state) => ({ count: state.count + 1 })),
}))
```

## Routing

Routes are defined in `src/routes/index.tsx` using React Router v6:

```typescript
import { Routes, Route } from 'react-router-dom'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<DashboardLayout />}>
        <Route path="dashboard" element={<DashboardPage />} />
        <Route path="devices" element={<DevicesPage />} />
      </Route>
    </Routes>
  )
}
```

## API Integration

The API client is configured in `src/api/client.ts` with:

- Base URL from environment variables
- Request/response interceptors
- Authentication token handling
- Error handling

```typescript
import { apiClient } from '@/api/client'

const response = await apiClient.get('/api/devices')
const device = await apiClient.post('/api/devices', { name: 'Device 1' })
```

## Environment Variables

All environment variables must be prefixed with `VITE_`:

- `VITE_API_URL` - Backend API URL
- `VITE_APP_ENV` - Environment (development, staging, production)
- `VITE_ENABLE_DEVTOOLS` - Enable React Query DevTools

## Building for Production

```bash
npm run build
```

This will:

1. Run TypeScript type checking
2. Build optimized production bundle to `dist/`
3. Minify and tree-shake code
4. Generate source maps

Preview the production build:

```bash
npm run preview
```

## Docker Build

Build the Docker image:

```bash
docker build -t signalbeam/web-ui:latest .
```

Run the container:

```bash
docker run -p 3000:80 signalbeam/web-ui:latest
```

## Development Guidelines

### Component Organization

- **Features** - Feature-based folder structure (`features/devices/`, `features/bundles/`)
- **Components** - Reusable components in `components/`
- **Layouts** - Layout components in `components/layouts/`

### File Naming

- Components: `dashboard-layout.tsx`, `device-card.tsx` (kebab-case)
- Hooks: `use-toast.ts`, `use-devices.ts` (kebab-case with `use-` prefix)
- Types: Define inline or in `types.ts` within feature folders

### Import Aliases

Path aliases are configured for cleaner imports:

```typescript
import { cn } from '@/lib/utils'
import { DashboardLayout } from '@/components/layouts/dashboard-layout'
import { useDevices } from '@/features/devices/hooks/use-devices'
```

## Troubleshooting

### Type errors after installing dependencies

Run type checking:

```bash
npm run type-check
```

### ESLint errors

Auto-fix most errors:

```bash
npm run lint:fix
```

### Formatting issues

Format all files:

```bash
npm run format
```

## Next Steps

1. Add shadcn/ui components as needed: `npx shadcn-ui@latest add <component>`
2. Implement device management features
3. Create app bundle management UI
4. Add authentication flow
5. Implement real-time device status updates
6. Add data tables with filtering and sorting
7. Create forms with React Hook Form + Zod validation

## Contributing

See the main project [CLAUDE.md](../CLAUDE.md) for architecture guidelines and contributing instructions.

## License

See LICENSE in the root of the repository.

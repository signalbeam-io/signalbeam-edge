/**
 * Application constants
 */

export const APP_NAME = 'SignalBeam Edge'
export const APP_VERSION = '0.1.0'

export const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:8080'

export const ROUTES = {
  DASHBOARD: '/dashboard',
  DEVICES: '/devices',
  BUNDLES: '/bundles',
} as const

export const QUERY_KEYS = {
  DEVICES: 'devices',
  DEVICE: 'device',
  BUNDLES: 'bundles',
  BUNDLE: 'bundle',
} as const

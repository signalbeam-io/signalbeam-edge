/**
 * Device Overview Tab - Shows tags, groups, bundle, and metadata
 */

import { format } from 'date-fns'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Device } from '@/api/types'
import { TagManager } from '../tags/tag-manager'

interface DeviceOverviewTabProps {
  device: Device
}

export function DeviceOverviewTab({ device }: DeviceOverviewTabProps) {
  return (
    <div className="grid gap-6 md:grid-cols-2">
      {/* Tags - Full width for better visibility */}
      <div className="md:col-span-2">
        <TagManager deviceId={device.id} currentTags={device.tags} />
      </div>

      {/* Groups */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-semibold">Groups</CardTitle>
        </CardHeader>
        <CardContent>
          {device.groupIds && device.groupIds.length > 0 ? (
            <div className="flex flex-wrap gap-2">
              {device.groupIds.map((groupId) => (
                <Badge key={groupId} variant="outline">
                  {groupId}
                </Badge>
              ))}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">Not part of any group</p>
          )}
        </CardContent>
      </Card>

      {/* Current Bundle */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-semibold">Current Bundle</CardTitle>
        </CardHeader>
        <CardContent>
          {device.currentBundleId ? (
            <div className="space-y-2">
              <div>
                <p className="text-sm font-medium text-muted-foreground">Bundle ID</p>
                <p className="text-sm font-semibold">{device.currentBundleId}</p>
              </div>
              {device.currentBundleVersion && (
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Version</p>
                  <p className="text-sm font-semibold">
                    {device.currentBundleVersion}
                  </p>
                </div>
              )}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">No bundle assigned</p>
          )}
        </CardContent>
      </Card>

      {/* Device Information */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-semibold">
            Device Information
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div>
            <p className="text-sm font-medium text-muted-foreground">Created</p>
            <p className="text-sm">
              {format(new Date(device.createdAt), 'PPpp')}
            </p>
          </div>
          <div>
            <p className="text-sm font-medium text-muted-foreground">Last Updated</p>
            <p className="text-sm">
              {format(new Date(device.updatedAt), 'PPpp')}
            </p>
          </div>
        </CardContent>
      </Card>

      {/* Metadata */}
      {device.metadata && Object.keys(device.metadata).length > 0 && (
        <Card className="md:col-span-2">
          <CardHeader>
            <CardTitle className="text-base font-semibold">Metadata</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {Object.entries(device.metadata).map(([key, value]) => (
                <div key={key}>
                  <p className="text-sm font-medium text-muted-foreground">{key}</p>
                  <p className="text-sm font-semibold">
                    {typeof value === 'object'
                      ? JSON.stringify(value)
                      : String(value)}
                  </p>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

/**
 * Bundle Detail Page
 */

import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { formatDistanceToNow } from 'date-fns'
import { ArrowLeft, Package, GitBranch, CheckCircle2, Users, Edit } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useBundle } from '@/hooks/api/use-bundles'
import { CreateVersionDialog } from '../components/create-version-dialog'
import { AssignBundleDialog } from '../components/assign-bundle-dialog'
import { BundleVersion } from '@/api/types'

export function BundleDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { data: bundle, isLoading, isError } = useBundle(id || '', !!id)

  const [createVersionOpen, setCreateVersionOpen] = useState(false)
  const [assignBundleOpen, setAssignBundleOpen] = useState(false)

  if (isLoading) {
    return (
      <div className="container mx-auto space-y-6 py-6">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (isError || !bundle) {
    return (
      <div className="container mx-auto py-6">
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <Package className="mb-4 h-12 w-12 text-muted-foreground" />
            <p className="text-lg font-medium">Bundle not found</p>
            <Button onClick={() => navigate('/bundles')} className="mt-4">
              Back to Bundles
            </Button>
          </CardContent>
        </Card>
      </div>
    )
  }

  const activeVersion = bundle.versions?.find((v) => v.isActive)

  return (
    <div className="container mx-auto space-y-6 py-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="sm" onClick={() => navigate('/bundles')}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{bundle.name}</h1>
            {bundle.description && (
              <p className="text-sm text-muted-foreground">{bundle.description}</p>
            )}
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => setAssignBundleOpen(true)}>
            <Users className="mr-2 h-4 w-4" />
            Assign to Devices
          </Button>
          <Button onClick={() => setCreateVersionOpen(true)}>
            <GitBranch className="mr-2 h-4 w-4" />
            New Version
          </Button>
        </div>
      </div>

      {/* Bundle Info Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Current Version</CardTitle>
            <GitBranch className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">v{bundle.currentVersion}</div>
            {activeVersion && (
              <p className="text-xs text-muted-foreground">
                Released {formatDistanceToNow(new Date(activeVersion.createdAt), { addSuffix: true })}
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Versions</CardTitle>
            <Package className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{bundle.versions?.length || 0}</div>
            <p className="text-xs text-muted-foreground">
              Created {formatDistanceToNow(new Date(bundle.createdAt), { addSuffix: true })}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Deployed Devices</CardTitle>
            <Users className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">-</div>
            <p className="text-xs text-muted-foreground">Coming soon</p>
          </CardContent>
        </Card>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="versions" className="space-y-4">
        <TabsList>
          <TabsTrigger value="versions">Versions</TabsTrigger>
          <TabsTrigger value="containers">Containers</TabsTrigger>
        </TabsList>

        <TabsContent value="versions" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Version History</CardTitle>
              <CardDescription>All versions of this bundle</CardDescription>
            </CardHeader>
            <CardContent>
              {bundle.versions && bundle.versions.length > 0 ? (
                <div className="space-y-4">
                  {bundle.versions
                    .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
                    .map((version) => (
                      <VersionCard key={version.version} version={version} />
                    ))}
                </div>
              ) : (
                <p className="text-sm text-muted-foreground py-8 text-center">No versions available</p>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="containers" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Container Definitions</CardTitle>
              <CardDescription>Containers in the current version (v{bundle.currentVersion})</CardDescription>
            </CardHeader>
            <CardContent>
              {activeVersion?.containers && activeVersion.containers.length > 0 ? (
                <div className="space-y-4">
                  {activeVersion.containers.map((container, index) => (
                    <Card key={index}>
                      <CardHeader>
                        <div className="flex items-center justify-between">
                          <CardTitle className="text-base">{container.name}</CardTitle>
                          <Badge variant="outline" className="font-mono">
                            {container.image}:{container.tag}
                          </Badge>
                        </div>
                      </CardHeader>
                      <CardContent className="space-y-2">
                        {container.environment && Object.keys(container.environment).length > 0 && (
                          <div>
                            <p className="text-sm font-medium mb-2">Environment Variables:</p>
                            <div className="rounded-md bg-muted p-3 space-y-1">
                              {Object.entries(container.environment).map(([key, value]) => (
                                <div key={key} className="flex items-center gap-2 text-sm font-mono">
                                  <span className="text-muted-foreground">{key}=</span>
                                  <span>{value}</span>
                                </div>
                              ))}
                            </div>
                          </div>
                        )}
                        {container.ports && container.ports.length > 0 && (
                          <div>
                            <p className="text-sm font-medium mb-2">Port Mappings:</p>
                            <div className="flex flex-wrap gap-2">
                              {container.ports.map((port, i) => (
                                <Badge key={i} variant="secondary">
                                  {port.host}:{port.container} ({port.protocol})
                                </Badge>
                              ))}
                            </div>
                          </div>
                        )}
                        {container.volumes && container.volumes.length > 0 && (
                          <div>
                            <p className="text-sm font-medium mb-2">Volume Mounts:</p>
                            <div className="space-y-1">
                              {container.volumes.map((volume, i) => (
                                <div key={i} className="text-sm font-mono">
                                  {volume.hostPath} → {volume.containerPath}
                                  {volume.readOnly && (
                                    <Badge variant="outline" className="ml-2 text-xs">
                                      read-only
                                    </Badge>
                                  )}
                                </div>
                              ))}
                            </div>
                          </div>
                        )}
                      </CardContent>
                    </Card>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-muted-foreground py-8 text-center">
                  No container definitions available
                </p>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Dialogs */}
      <CreateVersionDialog
        open={createVersionOpen}
        onOpenChange={setCreateVersionOpen}
        bundle={bundle}
      />
      <AssignBundleDialog
        open={assignBundleOpen}
        onOpenChange={setAssignBundleOpen}
        bundle={bundle}
      />
    </div>
  )
}

function VersionCard({ version }: { version: BundleVersion }) {
  return (
    <div className="flex items-start justify-between rounded-lg border p-4">
      <div className="flex items-start gap-3">
        <div className="rounded-full bg-muted p-2">
          {version.isActive ? (
            <CheckCircle2 className="h-4 w-4 text-green-600" />
          ) : (
            <GitBranch className="h-4 w-4 text-muted-foreground" />
          )}
        </div>
        <div>
          <div className="flex items-center gap-2">
            <p className="font-semibold font-mono">v{version.version}</p>
            {version.isActive && (
              <Badge variant="default" className="bg-green-600">
                Active
              </Badge>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            {version.containers.length} container{version.containers.length !== 1 ? 's' : ''}
          </p>
          <p className="text-xs text-muted-foreground">
            Released {formatDistanceToNow(new Date(version.createdAt), { addSuffix: true })}
          </p>
        </div>
      </div>
      {!version.isActive && (
        <Button variant="outline" size="sm">
          <Edit className="mr-2 h-3 w-3" />
          Activate
        </Button>
      )}
    </div>
  )
}

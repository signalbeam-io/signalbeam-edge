/**
 * Device Detail Page - Comprehensive view of a single device
 */

import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, RefreshCw, Edit, Trash2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { useDevice, useDeleteDevice } from '@/hooks/api/use-devices'
import { DeviceHeader } from '../components/device-detail/device-header'
import { DeviceOverviewTab } from '../components/device-detail/device-overview-tab'
import { DeviceHealthTab } from '../components/device-detail/device-health-tab'
import { DeviceContainersTab } from '../components/device-detail/device-containers-tab'
import { DeviceActivityTab } from '../components/device-detail/device-activity-tab'

export function DeviceDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [activeTab, setActiveTab] = useState('overview')
  const [showDeleteDialog, setShowDeleteDialog] = useState(false)

  const { data: device, isLoading, isError, refetch, isFetching } = useDevice(
    id ?? '',
    !!id
  )
  const deleteDevice = useDeleteDevice()

  const handleBack = () => {
    navigate('/devices')
  }

  const handleRefresh = () => {
    refetch()
  }

  const handleEdit = () => {
    // TODO: Implement edit dialog
    console.log('Edit device:', id)
  }

  const handleDelete = async () => {
    if (!id) return

    try {
      await deleteDevice.mutateAsync(id)
      navigate('/devices')
    } catch (error) {
      console.error('Failed to delete device:', error)
    }
  }

  // Loading state
  if (isLoading) {
    return (
      <div className="container mx-auto space-y-6 py-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-96 w-full" />
      </div>
    )
  }

  // Error state
  if (isError || !device) {
    return (
      <div className="container mx-auto py-6">
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <h2 className="mb-2 text-lg font-semibold">Device Not Found</h2>
            <p className="mb-4 text-sm text-muted-foreground">
              The device you are looking for does not exist or has been deleted.
            </p>
            <Button onClick={handleBack}>Back to Devices</Button>
          </CardContent>
        </Card>
      </div>
    )
  }

  return (
    <div className="container mx-auto space-y-6 py-6">
      {/* Breadcrumb Navigation */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/devices">Devices</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>{device.name}</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Header with Back Button and Actions */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button onClick={handleBack} variant="ghost" size="icon">
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">{device.name}</h1>
            <p className="text-sm text-muted-foreground">Device ID: {device.id}</p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button
            onClick={handleRefresh}
            variant="outline"
            size="sm"
            disabled={isFetching}
          >
            <RefreshCw className={`mr-2 h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
          <Button onClick={handleEdit} variant="outline" size="sm">
            <Edit className="mr-2 h-4 w-4" />
            Edit
          </Button>
          <Button
            onClick={() => setShowDeleteDialog(true)}
            variant="destructive"
            size="sm"
          >
            <Trash2 className="mr-2 h-4 w-4" />
            Delete
          </Button>
        </div>
      </div>

      {/* Device Header Card */}
      <DeviceHeader device={device} />

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="health">Health</TabsTrigger>
          <TabsTrigger value="containers">Containers</TabsTrigger>
          <TabsTrigger value="activity">Activity</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="mt-6">
          <DeviceOverviewTab device={device} />
        </TabsContent>

        <TabsContent value="health" className="mt-6">
          <DeviceHealthTab deviceId={device.id} />
        </TabsContent>

        <TabsContent value="containers" className="mt-6">
          <DeviceContainersTab deviceId={device.id} />
        </TabsContent>

        <TabsContent value="activity" className="mt-6">
          <DeviceActivityTab deviceId={device.id} />
        </TabsContent>
      </Tabs>

      {/* Delete Confirmation Dialog */}
      <AlertDialog open={showDeleteDialog} onOpenChange={setShowDeleteDialog}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Device</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to delete {device.name}? This action cannot be
              undone and will permanently remove the device and all its data.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDelete}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

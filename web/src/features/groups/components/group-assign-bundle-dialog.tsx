/**
 * Bulk bundle assignment dialog for device groups
 */

import { useState } from 'react'
import { Loader2, Package } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Card, CardContent } from '@/components/ui/card'
import { useToast } from '@/hooks/use-toast'
import { useBundles, useCreateRollout } from '@/hooks/api'

export interface GroupAssignBundleDialogProps {
  groupId: string
  groupName: string
  deviceCount: number
}

export function GroupAssignBundleDialog({
  groupId,
  groupName,
  deviceCount,
}: GroupAssignBundleDialogProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [selectedBundleId, setSelectedBundleId] = useState<string>('')
  const [selectedVersion, setSelectedVersion] = useState<string>('')
  const { toast } = useToast()

  // Fetch bundles
  const { data: bundlesData } = useBundles({ pageSize: 100 })
  const bundles = bundlesData?.data || []

  // Get selected bundle
  const selectedBundle = bundles.find((b) => b.id === selectedBundleId)

  // Create rollout mutation
  const createRollout = useCreateRollout()

  const handleAssign = async () => {
    if (!selectedBundleId || !selectedVersion) {
      toast({
        title: 'Validation error',
        description: 'Please select a bundle and version',
        variant: 'destructive',
      })
      return
    }

    try {
      await createRollout.mutateAsync({
        bundleId: selectedBundleId,
        version: selectedVersion,
        targetType: 'group',
        targetIds: [groupId],
      })

      toast({
        title: 'Bundle assignment started',
        description: `Assigning ${selectedBundle?.name} v${selectedVersion} to ${deviceCount} devices in ${groupName}`,
      })

      // Reset and close
      setSelectedBundleId('')
      setSelectedVersion('')
      setIsOpen(false)
    } catch (error) {
      toast({
        title: 'Assignment failed',
        description: error instanceof Error ? error.message : 'Failed to assign bundle to group',
        variant: 'destructive',
      })
    }
  }

  const handleBundleChange = (bundleId: string) => {
    setSelectedBundleId(bundleId)
    setSelectedVersion('')

    // Auto-select current version if available
    const bundle = bundles.find((b) => b.id === bundleId)
    if (bundle?.currentVersion) {
      setSelectedVersion(bundle.currentVersion)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={setIsOpen}>
      <DialogTrigger asChild>
        <Button variant="outline">
          <Package className="mr-2 h-4 w-4" />
          Assign Bundle
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>Assign Bundle to Group</DialogTitle>
          <DialogDescription>
            Deploy a bundle to all {deviceCount} device{deviceCount !== 1 ? 's' : ''} in{' '}
            <strong>{groupName}</strong>
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* Bundle Selection */}
          <div className="space-y-2">
            <Label htmlFor="bundle">Bundle</Label>
            <Select value={selectedBundleId} onValueChange={handleBundleChange}>
              <SelectTrigger id="bundle">
                <SelectValue placeholder="Select a bundle" />
              </SelectTrigger>
              <SelectContent>
                {bundles.length === 0 ? (
                  <div className="p-2 text-sm text-muted-foreground">No bundles available</div>
                ) : (
                  bundles.map((bundle) => (
                    <SelectItem key={bundle.id} value={bundle.id}>
                      {bundle.name}
                    </SelectItem>
                  ))
                )}
              </SelectContent>
            </Select>
          </div>

          {/* Version Selection */}
          {selectedBundle && (
            <div className="space-y-2">
              <Label htmlFor="version">Version</Label>
              <Select value={selectedVersion} onValueChange={setSelectedVersion}>
                <SelectTrigger id="version">
                  <SelectValue placeholder="Select a version" />
                </SelectTrigger>
                <SelectContent>
                  {selectedBundle.versions.length === 0 ? (
                    <div className="p-2 text-sm text-muted-foreground">No versions available</div>
                  ) : (
                    selectedBundle.versions.map((version) => (
                      <SelectItem key={version.version} value={version.version}>
                        {version.version}
                        {version.version === selectedBundle.currentVersion && (
                          <span className="ml-2 text-xs text-muted-foreground">(current)</span>
                        )}
                      </SelectItem>
                    ))
                  )}
                </SelectContent>
              </Select>
            </div>
          )}

          {/* Preview */}
          {selectedBundle && selectedVersion && (
            <Card>
              <CardContent className="pt-4">
                <div className="space-y-2 text-sm">
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Bundle:</span>
                    <span className="font-medium">{selectedBundle.name}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Version:</span>
                    <span className="font-medium">{selectedVersion}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Target Group:</span>
                    <span className="font-medium">{groupName}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Affected Devices:</span>
                    <span className="font-medium">{deviceCount}</span>
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Warning */}
          <Alert>
            <AlertDescription>
              This will create a rollout to deploy the selected bundle to all devices in this group.
              Devices will update when they next check in.
            </AlertDescription>
          </Alert>
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => setIsOpen(false)}
            disabled={createRollout.isPending}
          >
            Cancel
          </Button>
          <Button
            onClick={handleAssign}
            disabled={createRollout.isPending || !selectedBundleId || !selectedVersion}
          >
            {createRollout.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Assign Bundle
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

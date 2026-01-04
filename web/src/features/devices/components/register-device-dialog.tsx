/**
 * Register Device Dialog Component
 *
 * Note: In the current implementation, devices register themselves using registration tokens.
 * This dialog explains the registration workflow to users.
 */

import { useState } from 'react'
import { Plus, Copy, CheckCircle2, AlertCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { useToast } from '@/hooks/use-toast'

export function RegisterDeviceDialog() {
  const [open, setOpen] = useState(false)
  const { toast } = useToast()

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text)
    toast({
      title: 'Copied to clipboard',
      description: 'Command copied successfully',
    })
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus className="mr-2 h-4 w-4" />
          Register Device
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-[600px]">
        <DialogHeader>
          <DialogTitle>Register New Device</DialogTitle>
          <DialogDescription>
            Follow these steps to register a new edge device to your fleet.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              Devices self-register using the Edge Agent. Run the agent on your edge device
              to complete registration.
            </AlertDescription>
          </Alert>

          <div className="space-y-3">
            <div className="flex items-start gap-3">
              <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-semibold text-primary-foreground">
                1
              </div>
              <div className="space-y-1">
                <p className="text-sm font-medium">Install the Edge Agent</p>
                <p className="text-xs text-muted-foreground">
                  Download and install the SignalBeam Edge Agent on your device (Raspberry Pi, mini-PC, etc.)
                </p>
              </div>
            </div>

            <div className="flex items-start gap-3">
              <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-semibold text-primary-foreground">
                2
              </div>
              <div className="space-y-1">
                <p className="text-sm font-medium">Run the Edge Agent</p>
                <p className="text-xs text-muted-foreground">
                  Deploy the SignalBeam Edge Agent on your device and configure it with your API key:
                </p>
                <div className="mt-2 flex items-center gap-2 rounded-md bg-muted p-2 font-mono text-xs">
                  <code className="flex-1">
                    signalbeam-agent --api-key YOUR_API_KEY --device-name my-device
                  </code>
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() =>
                      copyToClipboard(
                        'signalbeam-agent --api-key YOUR_API_KEY --device-name my-device'
                      )
                    }
                  >
                    <Copy className="h-3 w-3" />
                  </Button>
                </div>
                <p className="mt-2 text-xs text-muted-foreground italic">
                  Note: For development/testing, you can use the Edge Agent Simulator instead.
                </p>
              </div>
            </div>

            <div className="flex items-start gap-3">
              <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-semibold text-primary-foreground">
                3
              </div>
              <div className="space-y-1">
                <p className="text-sm font-medium">Device appears in fleet</p>
                <p className="text-xs text-muted-foreground">
                  Once the agent connects successfully, the device will appear in your fleet
                  overview automatically.
                </p>
              </div>
            </div>
          </div>

          <Alert>
            <CheckCircle2 className="h-4 w-4" />
            <AlertDescription className="text-xs">
              <strong>Tip:</strong> You can find your API key in the Authentication section of
              your tenant settings, or use the one from your appsettings.json during development.
            </AlertDescription>
          </Alert>
        </div>

        <DialogFooter>
          <Button type="button" onClick={() => setOpen(false)}>
            Got it
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

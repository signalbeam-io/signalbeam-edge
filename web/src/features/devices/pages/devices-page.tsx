export function DevicesPage() {
  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-3xl font-bold">Devices</h1>
        <button className="rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90">
          Add Device
        </button>
      </div>
      <div className="rounded-lg border bg-card">
        <div className="p-6">
          <p className="text-muted-foreground">No devices registered yet.</p>
        </div>
      </div>
    </div>
  )
}

/**
 * Device Health Tab - Shows CPU, Memory, and Disk usage metrics
 */

import { format } from 'date-fns'
import { Activity } from 'lucide-react'
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Progress } from '@/components/ui/progress'
import { useDeviceMetrics } from '@/hooks/api/use-devices'

interface DeviceHealthTabProps {
  deviceId: string
}

export function DeviceHealthTab({ deviceId }: DeviceHealthTabProps) {
  const { data: metrics, isLoading } = useDeviceMetrics(deviceId)

  if (isLoading) {
    return (
      <div className="grid gap-6 md:grid-cols-2">
        <Skeleton className="h-64 w-full" />
        <Skeleton className="h-64 w-full" />
        <Skeleton className="h-64 w-full md:col-span-2" />
      </div>
    )
  }

  if (!metrics || metrics.length === 0) {
    return (
      <Card>
        <CardContent className="flex flex-col items-center justify-center py-12">
          <Activity className="mb-4 h-12 w-12 text-muted-foreground" />
          <p className="text-lg font-medium">No metrics available</p>
          <p className="text-sm text-muted-foreground">
            Waiting for device to send health data
          </p>
        </CardContent>
      </Card>
    )
  }

  // Get latest metrics for gauges
  const latestMetrics = metrics[metrics.length - 1]

  // Format data for charts
  const chartData = metrics.map((m) => ({
    time: format(new Date(m.timestamp), 'HH:mm'),
    cpu: m.cpuUsage,
    memory: m.memoryUsage,
    disk: m.diskUsage,
  }))

  return (
    <div className="space-y-6">
      {/* Current Status Gauges */}
      <div className="grid gap-6 md:grid-cols-3">
        <MetricGauge
          title="CPU Usage"
          value={latestMetrics?.cpuUsage ?? 0}
          unit="%"
        />
        <MetricGauge
          title="Memory Usage"
          value={latestMetrics?.memoryUsage ?? 0}
          unit="%"
        />
        <MetricGauge
          title="Disk Usage"
          value={latestMetrics?.diskUsage ?? 0}
          unit="%"
        />
      </div>

      {/* CPU Usage Chart */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-semibold">
            CPU Usage (Last 24h)
          </CardTitle>
        </CardHeader>
        <CardContent>
          <ResponsiveContainer width="100%" height={250}>
            <LineChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis
                dataKey="time"
                tick={{ fontSize: 12 }}
                tickLine={false}
              />
              <YAxis
                tick={{ fontSize: 12 }}
                tickLine={false}
                domain={[0, 100]}
              />
              <Tooltip
                contentStyle={{
                  backgroundColor: 'hsl(var(--card))',
                  border: '1px solid hsl(var(--border))',
                  borderRadius: '6px',
                }}
              />
              <Line
                type="monotone"
                dataKey="cpu"
                stroke="hsl(var(--chart-1))"
                strokeWidth={2}
                dot={false}
              />
            </LineChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>

      {/* Memory Usage Chart */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-semibold">
            Memory Usage (Last 24h)
          </CardTitle>
        </CardHeader>
        <CardContent>
          <ResponsiveContainer width="100%" height={250}>
            <LineChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis
                dataKey="time"
                tick={{ fontSize: 12 }}
                tickLine={false}
              />
              <YAxis
                tick={{ fontSize: 12 }}
                tickLine={false}
                domain={[0, 100]}
              />
              <Tooltip
                contentStyle={{
                  backgroundColor: 'hsl(var(--card))',
                  border: '1px solid hsl(var(--border))',
                  borderRadius: '6px',
                }}
              />
              <Line
                type="monotone"
                dataKey="memory"
                stroke="hsl(var(--chart-2))"
                strokeWidth={2}
                dot={false}
              />
            </LineChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>
    </div>
  )
}

interface MetricGaugeProps {
  title: string
  value: number
  unit: string
}

function MetricGauge({ title, value, unit }: MetricGaugeProps) {
  const getColorClass = () => {
    if (value >= 90) return 'text-red-500'
    if (value >= 75) return 'text-yellow-500'
    return 'text-green-500'
  }

  const getProgressColorClass = () => {
    if (value >= 90) return '[&>div]:bg-red-500'
    if (value >= 75) return '[&>div]:bg-yellow-500'
    return '[&>div]:bg-green-500'
  }

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-sm font-medium text-muted-foreground">
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          <div className="flex items-baseline gap-1">
            <span className={`text-3xl font-bold ${getColorClass()}`}>
              {value.toFixed(1)}
            </span>
            <span className="text-sm text-muted-foreground">{unit}</span>
          </div>
          <Progress value={value} className={getProgressColorClass()} />
        </div>
      </CardContent>
    </Card>
  )
}

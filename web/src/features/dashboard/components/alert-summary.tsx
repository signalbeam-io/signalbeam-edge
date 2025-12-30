/**
 * Alert Summary Component - Shows aggregated alert statistics
 */

import { AlertTriangle, AlertCircle, Clock } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAlertStatistics } from '@/hooks/api'
import { useNavigate } from 'react-router-dom'

export function AlertSummary() {
  const navigate = useNavigate()
  const { data: stats, isLoading } = useAlertStatistics()

  if (isLoading) {
    return (
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        {[...Array(4)].map((_, i) => (
          <Skeleton key={i} className="h-32" />
        ))}
      </div>
    )
  }

  if (!stats) {
    return null
  }

  return (
    <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
      <StatCard
        title="Active Alerts"
        value={stats.totalActive}
        icon={AlertTriangle}
        iconColor="text-red-600"
        onClick={() => navigate('/alerts')}
      />
      <StatCard
        title="Critical"
        value={stats.bySeverity.critical}
        icon={AlertCircle}
        iconColor="text-red-600"
        onClick={() => navigate('/alerts')}
      />
      <StatCard
        title="Warning"
        value={stats.bySeverity.warning}
        icon={AlertTriangle}
        iconColor="text-yellow-600"
        onClick={() => navigate('/alerts')}
      />
      <StatCard
        title="Acknowledged"
        value={stats.totalAcknowledged}
        icon={Clock}
        iconColor="text-yellow-600"
        onClick={() => navigate('/alerts')}
      />
    </div>
  )
}

interface StatCardProps {
  title: string
  value: number
  icon: React.ElementType
  iconColor?: string
  onClick?: () => void
}

function StatCard({ title, value, icon: Icon, iconColor = 'text-muted-foreground', onClick }: StatCardProps) {
  return (
    <Card
      className={onClick ? 'cursor-pointer hover:bg-muted/50 transition-colors' : ''}
      onClick={onClick}
    >
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium">{title}</CardTitle>
        <Icon className={`h-4 w-4 ${iconColor}`} />
      </CardHeader>
      <CardContent>
        <div className="text-2xl font-bold">{value}</div>
      </CardContent>
    </Card>
  )
}

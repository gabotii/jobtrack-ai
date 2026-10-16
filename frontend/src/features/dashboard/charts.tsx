import { Table2, BarChart3 } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Bar, BarChart, CartesianGrid, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/display'
import { formatShortDate } from '@/lib/dates'
import { STATUS_META } from '@/lib/labels'
import type { Dashboard } from '@/types/api'

// Validated with the dataviz palette checker against the white surface (chroma + 3:1 contrast). The brand teal
// (#0f766e) is too desaturated for marks, so charts use one step brighter.
const MARK = '#0d9488'
const GRID = '#e6eae8'
const AXIS_TEXT = '#56625f'

function ChartCard({
  title,
  subtitle,
  table,
  children,
}: {
  title: string
  subtitle: string
  table: ReactNode
  children: ReactNode
}) {
  const [showTable, setShowTable] = useState(false)
  return (
    <Card>
      <CardHeader>
        <div>
          <CardTitle>{title}</CardTitle>
          <p className="text-xs text-muted-foreground">{subtitle}</p>
        </div>
        <Button
          variant="ghost"
          size="sm"
          aria-pressed={showTable}
          onClick={() => setShowTable((v) => !v)}
          aria-label={showTable ? `Ver ${title} como gráfico` : `Ver ${title} como tabla`}
        >
          {showTable ? <BarChart3 /> : <Table2 />}
          {showTable ? 'Gráfico' : 'Tabla'}
        </Button>
      </CardHeader>
      <CardContent>{showTable ? table : children}</CardContent>
    </Card>
  )
}

function ChartTooltip({
  active,
  payload,
  label,
}: {
  active?: boolean
  payload?: { value: number }[]
  label?: string
}) {
  if (!active || !payload?.length) return null
  return (
    <div className="rounded-lg border border-border bg-surface px-3 py-2 text-xs shadow-md">
      <p className="font-semibold text-foreground">{label}</p>
      <p className="text-muted-foreground">
        {payload[0].value} postulación{payload[0].value === 1 ? '' : 'es'}
      </p>
    </div>
  )
}

function DataTable({ head, rows }: { head: [string, string]; rows: [string, number][] }) {
  return (
    <table className="w-full text-sm">
      <thead className="text-left text-xs text-muted-foreground">
        <tr>
          <th scope="col" className="py-1 font-semibold">
            {head[0]}
          </th>
          <th scope="col" className="py-1 text-right font-semibold">
            {head[1]}
          </th>
        </tr>
      </thead>
      <tbody className="divide-y divide-border">
        {rows.map(([label, value]) => (
          <tr key={label}>
            <td className="py-1.5">{label}</td>
            <td className="py-1.5 text-right tabular-nums">{value}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

export function WeeklyChart({ data }: { data: Dashboard['applicationsPerWeek'] }) {
  const rows = data.map((w) => ({ label: `Sem. ${formatShortDate(w.weekStart)}`, count: w.count }))
  const total = data.reduce((sum, w) => sum + w.count, 0)
  return (
    <ChartCard
      title="Postulaciones por semana"
      subtitle={`Últimas 12 semanas · ${total} en total`}
      table={<DataTable head={['Semana', 'Postulaciones']} rows={rows.map((r) => [r.label, r.count])} />}
    >
      <div
        className="h-56"
        role="img"
        aria-label={`Gráfico de columnas: ${total} postulaciones en las últimas 12 semanas.`}
      >
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} margin={{ top: 16, right: 4, bottom: 0, left: -24 }}>
            <CartesianGrid vertical={false} stroke={GRID} strokeWidth={1} />
            <XAxis
              dataKey="label"
              tickFormatter={(v: string) => v.replace('Sem. ', '')}
              tick={{ fontSize: 11, fill: AXIS_TEXT }}
              tickLine={false}
              axisLine={{ stroke: GRID }}
              interval="preserveStartEnd"
            />
            <YAxis
              allowDecimals={false}
              tick={{ fontSize: 11, fill: AXIS_TEXT }}
              tickLine={false}
              axisLine={false}
            />
            <Tooltip content={<ChartTooltip />} cursor={{ fill: 'rgba(13,148,136,0.08)' }} />
            <Bar
              dataKey="count"
              fill={MARK}
              maxBarSize={24}
              radius={[4, 4, 0, 0]}
              isAnimationActive={false}
            />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </ChartCard>
  )
}

export function StatusChart({ data }: { data: Dashboard['statusDistribution'] }) {
  const rows = data.map((s) => ({ label: STATUS_META[s.status].label, count: s.count }))
  return (
    <ChartCard
      title="Distribución por estado"
      subtitle="Todas tus postulaciones"
      table={<DataTable head={['Estado', 'Postulaciones']} rows={rows.map((r) => [r.label, r.count])} />}
    >
      <div
        className="h-64"
        role="img"
        aria-label="Gráfico de barras con el número de postulaciones en cada estado."
      >
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} layout="vertical" margin={{ top: 0, right: 28, bottom: 0, left: 8 }}>
            <CartesianGrid horizontal={false} stroke={GRID} strokeWidth={1} />
            <XAxis type="number" allowDecimals={false} hide />
            <YAxis
              type="category"
              dataKey="label"
              width={118}
              tick={{ fontSize: 12, fill: AXIS_TEXT }}
              tickLine={false}
              axisLine={{ stroke: GRID }}
            />
            <Tooltip content={<ChartTooltip />} cursor={{ fill: 'rgba(13,148,136,0.08)' }} />
            <Bar dataKey="count" fill={MARK} maxBarSize={18} radius={[0, 4, 4, 0]} isAnimationActive={false}>
              <LabelList
                dataKey="count"
                position="right"
                style={{ fontSize: 12, fill: '#16201e', fontWeight: 600 }}
              />
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </ChartCard>
  )
}

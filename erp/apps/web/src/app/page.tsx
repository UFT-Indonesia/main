'use client';

import { useTranslations } from 'next-intl';
import { Users, UserCheck, Clock } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useEmployees } from '@/hooks/use-employees';
import { useAttendanceLogs } from '@/hooks/use-attendance';
import { useHasRole } from '@/lib/auth/store';

function todayJakartaRange(): { dateFrom: string; dateTo: string } {
  const dateStr = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Jakarta',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date());
  const start = new Date(`${dateStr}T00:00:00+07:00`);
  const end = new Date(start.getTime() + 86_400_000);
  return {
    dateFrom: start.toISOString(),
    dateTo: end.toISOString(),
  };
}

export default function HomePage() {
  const { dateFrom, dateTo } = todayJakartaRange();
  const tNav = useTranslations('nav');
  const tHome = useTranslations('home');

  // Headcount comes from the employees endpoint, which is Owner,Manager — Staff would just
  // collect four 403s on their landing page, so they don't ask.
  const canSeeHeadcount = useHasRole('Owner', 'Manager');
  const activeQuery = useEmployees({ status: 'Active', pageSize: 1 }, canSeeHeadcount);
  const ownerQuery = useEmployees({ status: 'Active', role: 'Owner', pageSize: 1 }, canSeeHeadcount);
  const managerQuery = useEmployees({ status: 'Active', role: 'Manager', pageSize: 1 }, canSeeHeadcount);
  const staffQuery = useEmployees({ status: 'Active', role: 'Staff', pageSize: 1 }, canSeeHeadcount);
  const todayLogsQuery = useAttendanceLogs({ dateFrom, dateTo, pageSize: 1 });

  return (
    <AppShell>
      <div className="space-y-4">
        <header>
          <h1 className="text-2xl font-semibold tracking-tight">{tNav('dashboard')}</h1>
          <p className="text-sm text-muted-foreground">
            {canSeeHeadcount ? tHome('subtitle') : tHome('subtitleStaff')}
          </p>
        </header>

        {canSeeHeadcount && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <SummaryCard
            icon={<Users className="h-4 w-4 text-muted-foreground" />}
            label={tHome('cards.activeEmployees')}
            value={activeQuery.data?.totalCount}
            loading={activeQuery.isLoading}
          />
          <SummaryCard
            icon={<Users className="h-4 w-4 text-muted-foreground" />}
            label={tHome('cards.owners')}
            value={ownerQuery.data?.totalCount}
            loading={ownerQuery.isLoading}
          />
          <SummaryCard
            icon={<UserCheck className="h-4 w-4 text-muted-foreground" />}
            label={tHome('cards.managers')}
            value={managerQuery.data?.totalCount}
            loading={managerQuery.isLoading}
          />
          <SummaryCard
            icon={<UserCheck className="h-4 w-4 text-muted-foreground" />}
            label={tHome('cards.staff')}
            value={staffQuery.data?.totalCount}
            loading={staffQuery.isLoading}
          />
        </div>
        )}

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Clock className="h-4 w-4 text-muted-foreground" />
              {tHome('cards.todayLogs')}
            </CardTitle>
          </CardHeader>
          <CardContent>
            {todayLogsQuery.isLoading ? (
              <Skeleton className="h-8 w-20" />
            ) : (
              <p className="text-3xl font-semibold tabular-nums">
                {todayLogsQuery.data?.totalCount ?? '—'}
              </p>
            )}
          </CardContent>
        </Card>
      </div>
    </AppShell>
  );
}

function SummaryCard({
  icon,
  label,
  value,
  loading,
}: {
  icon: React.ReactNode;
  label: string;
  value: number | undefined;
  loading: boolean;
}) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{label}</CardTitle>
        {icon}
      </CardHeader>
      <CardContent>
        {loading ? (
          <Skeleton className="h-8 w-16" />
        ) : (
          <p className="text-3xl font-semibold tabular-nums">{value ?? '—'}</p>
        )}
      </CardContent>
    </Card>
  );
}

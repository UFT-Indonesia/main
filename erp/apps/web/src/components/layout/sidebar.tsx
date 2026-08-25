'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Users, LayoutDashboard, Clock, Settings, CalendarDays, KeyRound, Cpu, History, UserCheck } from 'lucide-react';
import type { Route } from 'next';
import { cn } from '@/lib/utils';
import { APP_NAME } from '@/lib/constants';
import { useAuthStore } from '@/lib/auth/store';

interface NavItem {
  href: Route;
  labelKey:
    | 'dashboard'
    | 'employees'
    | 'attendance'
    | 'attendanceSettings'
    | 'attendanceDevices'
    | 'leave'
    | 'probation'
    | 'accounts'
    | 'employeeAuditLog';
  icon: typeof Users;
  /** Only shown to users with one of these roles; omitted means visible to everyone. */
  roles?: string[];
}

const NAV: NavItem[] = [
  { href: '/' as Route, labelKey: 'dashboard', icon: LayoutDashboard },
  // ListEmployeesEndpoint is Owner,Manager — Staff would only get a 403 from this item.
  { href: '/employees' as Route, labelKey: 'employees', icon: Users, roles: ['Owner', 'Manager'] },
  // A change history exposes every salary/reporting-line change ever made, so it's Owner-only.
  {
    href: '/employees/audit-log' as Route,
    labelKey: 'employeeAuditLog',
    icon: History,
    roles: ['Owner'],
  },
  { href: '/attendance' as Route, labelKey: 'attendance', icon: Clock },
  // Everyone can reach leave now — Staff file their own; the list is scoped server-side.
  { href: '/leave' as Route, labelKey: 'leave', icon: CalendarDays },
  // A manager files extension requests, an owner decides them. Staff are not shown the file
  // on themselves being discussed, so they get no entry.
  {
    href: '/probation' as Route,
    labelKey: 'probation',
    icon: UserCheck,
    roles: ['Owner', 'Manager'],
  },
  { href: '/accounts' as Route, labelKey: 'accounts', icon: KeyRound, roles: ['Owner', 'Manager'] },
  {
    href: '/attendance/settings' as Route,
    labelKey: 'attendanceSettings',
    icon: Settings,
    roles: ['Owner', 'Manager'],
  },
  // A device secret can punch for any employee, so registering one is Owner-only.
  {
    href: '/attendance/devices' as Route,
    labelKey: 'attendanceDevices',
    icon: Cpu,
    roles: ['Owner'],
  },
];

export function Sidebar() {
  const pathname = usePathname();
  const t = useTranslations('nav');
  const user = useAuthStore((s) => s.user);

  const items = NAV.filter((item) => !item.roles || item.roles.some((role) => user?.roles.includes(role)));

  return (
    <aside className="hidden w-64 shrink-0 border-r border-border bg-card md:block">
      <div className="flex h-14 items-center border-b border-border px-4">
        <span className="text-base font-semibold">{APP_NAME}</span>
      </div>
      <nav className="flex flex-col gap-1 p-2">
        {items.map((item) => {
          const active =
            item.href === '/'
              ? pathname === '/'
              : pathname === item.href || pathname.startsWith(`${item.href}/`);
          const Icon = item.icon;
          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                'flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                active
                  ? 'bg-accent text-accent-foreground'
                  : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
              )}
            >
              <Icon className="h-4 w-4" />
              {t(item.labelKey)}
            </Link>
          );
        })}
      </nav>
    </aside>
  );
}

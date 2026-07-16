'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Users, LayoutDashboard, Clock, Settings, CalendarDays } from 'lucide-react';
import type { Route } from 'next';
import { cn } from '@/lib/utils';
import { APP_NAME } from '@/lib/constants';
import { useAuthStore } from '@/lib/auth/store';

interface NavItem {
  href: Route;
  labelKey: 'dashboard' | 'employees' | 'attendance' | 'attendanceSettings' | 'leave';
  icon: typeof Users;
  /** Only shown to users with one of these roles; omitted means visible to everyone. */
  roles?: string[];
}

const NAV: NavItem[] = [
  { href: '/' as Route, labelKey: 'dashboard', icon: LayoutDashboard },
  { href: '/employees' as Route, labelKey: 'employees', icon: Users },
  { href: '/attendance' as Route, labelKey: 'attendance', icon: Clock },
  { href: '/leave' as Route, labelKey: 'leave', icon: CalendarDays, roles: ['Owner', 'Manager'] },
  {
    href: '/attendance/settings' as Route,
    labelKey: 'attendanceSettings',
    icon: Settings,
    roles: ['Owner', 'Manager'],
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

'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Plus, Lock, LockOpen } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { DeviceSecretDialog } from '@/components/attendance/device-secret-dialog';
import { RegisterDeviceDialog } from '@/components/attendance/register-device-dialog';
import {
  useAttendanceDevices,
  useRegisterAttendanceDevice,
  useSetAttendanceDeviceEnabled,
} from '@/hooks/use-attendance-devices';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import { useHasRole } from '@/lib/auth/store';
import type { AttendanceDevice } from '@/lib/api/types';

const dateFormatter = new Intl.DateTimeFormat('id-ID', { dateStyle: 'medium' });

export default function AttendanceDevicesPage() {
  const t = useTranslations('attendanceDevices');
  const tCommon = useTranslations('common');
  const toast = useToast();

  // Registering a device hands out a key that can punch for anyone, so this is Owner-only.
  const isOwner = useHasRole('Owner');

  const [registerOpen, setRegisterOpen] = useState(false);
  const [revealed, setRevealed] = useState<{ name: string; secret: string } | null>(null);

  const { data, isLoading, error } = useAttendanceDevices(isOwner);
  const registerMutation = useRegisterAttendanceDevice();
  const setEnabledMutation = useSetAttendanceDeviceEnabled();

  const handleRegister = async (deviceKey: string, name: string) => {
    try {
      const response = await registerMutation.mutateAsync({ deviceKey, name });
      setRegisterOpen(false);
      setRevealed({ name: response.device.name, secret: response.secret });
    } catch (err) {
      toast.error(t('register.errorTitle'), extractApiError(err).message);
    }
  };

  const handleToggle = async (device: AttendanceDevice) => {
    try {
      await setEnabledMutation.mutateAsync({ id: device.id, enabled: !device.enabled });
      toast.success(device.enabled ? t('toggle.disabledTitle') : t('toggle.enabledTitle'), device.name);
    } catch (err) {
      toast.error(t('toggle.errorTitle'), extractApiError(err).message);
    }
  };

  if (!isOwner) {
    return (
      <AppShell>
        <div className="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
          {t('ownerOnly')}
        </div>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <div className="space-y-4">
        <header className="flex items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
            <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
          </div>
          <Button onClick={() => setRegisterOpen(true)}>
            <Plus className="h-4 w-4" />
            {t('register.button')}
          </Button>
        </header>

        {error ? (
          <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
            {extractApiError(error).message}
          </div>
        ) : isLoading ? (
          <div className="space-y-2">
            {Array.from({ length: 3 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        ) : (data?.items.length ?? 0) === 0 ? (
          <div className="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
            {t('empty')}
          </div>
        ) : (
          <div className="rounded-lg border border-border bg-card">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('table.name')}</TableHead>
                  <TableHead>{t('table.deviceKey')}</TableHead>
                  <TableHead>{t('table.status')}</TableHead>
                  <TableHead>{t('table.registered')}</TableHead>
                  <TableHead className="text-right">{tCommon('actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data!.items.map((device) => (
                  <TableRow key={device.id}>
                    <TableCell className="font-medium">{device.name}</TableCell>
                    <TableCell className="font-mono text-xs">{device.deviceKey}</TableCell>
                    <TableCell>
                      <Badge variant={device.enabled ? 'success' : 'destructive'}>
                        {device.enabled ? t('status.enabled') : t('status.disabled')}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {dateFormatter.format(new Date(device.createdAtUtc))}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => handleToggle(device)}
                        disabled={setEnabledMutation.isPending}
                        aria-label={device.enabled ? t('toggle.disable') : t('toggle.enable')}
                        title={device.enabled ? t('toggle.disable') : t('toggle.enable')}
                      >
                        {device.enabled ? (
                          <Lock className="h-4 w-4" />
                        ) : (
                          <LockOpen className="h-4 w-4" />
                        )}
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </div>

      <RegisterDeviceDialog
        open={registerOpen}
        onOpenChange={setRegisterOpen}
        onConfirm={handleRegister}
        submitting={registerMutation.isPending}
      />

      <DeviceSecretDialog
        open={revealed !== null}
        onOpenChange={(o) => { if (!o) setRevealed(null); }}
        deviceName={revealed?.name ?? ''}
        secret={revealed?.secret ?? ''}
      />
    </AppShell>
  );
}

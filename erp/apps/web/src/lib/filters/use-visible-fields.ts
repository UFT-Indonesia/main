'use client';

import { useMemo } from 'react';
import { useHasRole } from '@/lib/auth/store';
import type { FilterFieldDef } from './types';

/**
 * Drops the fields this caller may not filter by. Presentation only — the API refuses them
 * whoever asks — but it also keeps "Add filter" from seeding a row on a field that would come
 * straight back as a 403.
 */
export function useVisibleFilterFields(fields: readonly FilterFieldDef[]): readonly FilterFieldDef[] {
  const isOwner = useHasRole('Owner');
  return useMemo(() => fields.filter((field) => !field.ownerOnly || isOwner), [fields, isOwner]);
}

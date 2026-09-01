'use client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { I18nProvider } from 'react-aria-components';

export function Providers({ children }: { children: ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            refetchOnWindowFocus: false,
          },
        },
      }),
  );

  return (
    <QueryClientProvider client={queryClient}>
      {/* Pins every DatePicker/DateRangePicker segment order to dd/mm/yyyy, regardless of the
          visitor's browser locale — react-aria-components otherwise infers segment order from
          navigator.language, which reads mm/dd/yyyy for an en-US browser. en-GB, not id-ID:
          id-ID zero-pads inconsistently and swaps the empty-segment placeholder to Indonesian
          field abbreviations ("hh/bb/tttt"); en-GB gives dd/mm/yyyy ordering with numeric,
          zero-padded segments and a plain "dd/mm/yyyy" placeholder. */}
      <I18nProvider locale="en-GB">{children}</I18nProvider>
    </QueryClientProvider>
  );
}

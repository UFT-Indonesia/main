'use client';

import { useId, useRef, useState, type DragEvent } from 'react';
import { useTranslations } from 'next-intl';
import { FileText, Upload, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

interface FileDropzoneProps {
  value: File | null;
  onChange: (file: File | null) => void;
  /** MIME types the picker offers and the drop handler accepts. */
  accept: readonly string[];
  maxBytes: number;
  disabled?: boolean;
  /** Rendered under the box, e.g. "PDF, JPG or PNG · max 10MB". */
  hint?: string;
}

/**
 * Drag-or-click file picker for a single file. Validates type and size here so the user hears
 * about a 40MB scan before uploading it, but the server checks both again — this runs in a
 * browser, which is not somewhere a limit can actually be enforced.
 */
export function FileDropzone({
  value,
  onChange,
  accept,
  maxBytes,
  disabled,
  hint,
}: FileDropzoneProps) {
  const t = useTranslations('common.fileUpload');
  const inputId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function accepted(file: File): boolean {
    if (!accept.includes(file.type)) {
      setError(t('wrongType'));
      return false;
    }
    if (file.size > maxBytes) {
      setError(t('tooLarge', { megabytes: Math.floor(maxBytes / (1024 * 1024)) }));
      return false;
    }
    setError(null);
    return true;
  }

  function take(file: File | undefined) {
    if (!file || !accepted(file)) return;
    onChange(file);
  }

  function handleDrop(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setDragging(false);
    if (disabled) return;
    take(e.dataTransfer.files[0]);
  }

  function clear() {
    onChange(null);
    setError(null);
    // The input keeps its own value, so re-picking the same file would fire no change event.
    if (inputRef.current) inputRef.current.value = '';
  }

  return (
    <div className="flex flex-col gap-1.5">
      {value ? (
        <div className="flex items-center gap-3 rounded-lg border border-border bg-card p-3">
          <FileText className="h-5 w-5 shrink-0 text-muted-foreground" />
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-medium">{value.name}</p>
            <p className="text-xs text-muted-foreground tabular-nums">
              {(value.size / 1024).toFixed(0)} KB
            </p>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="h-7 w-7 shrink-0"
            onClick={clear}
            disabled={disabled}
            aria-label={t('remove')}
            title={t('remove')}
          >
            <X className="h-4 w-4" />
          </Button>
        </div>
      ) : (
        <div
          onDragOver={(e) => {
            e.preventDefault();
            if (!disabled) setDragging(true);
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={handleDrop}
          className={cn(
            'rounded-lg border border-dashed transition-colors',
            dragging ? 'border-primary bg-accent' : 'border-border-strong',
            disabled && 'opacity-50',
          )}
        >
          {/* The label is the click target, so the hidden input needs no click handler and
              keyboard focus lands somewhere that opens the picker on Enter. */}
          <label
            htmlFor={inputId}
            className={cn(
              'flex cursor-pointer flex-col items-center gap-2 px-4 py-6 text-center',
              disabled && 'cursor-not-allowed',
            )}
          >
            <Upload className="h-6 w-6 text-muted-foreground" />
            <span className="text-sm">
              <span className="font-medium text-primary">{t('browse')}</span>{' '}
              <span className="text-muted-foreground">{t('orDrag')}</span>
            </span>
          </label>
          <input
            id={inputId}
            ref={inputRef}
            type="file"
            className="sr-only"
            accept={accept.join(',')}
            disabled={disabled}
            onChange={(e) => take(e.target.files?.[0])}
          />
        </div>
      )}

      {error ? (
        <p className="text-xs text-destructive">{error}</p>
      ) : hint ? (
        <p className="text-xs text-muted-foreground">{hint}</p>
      ) : null}
    </div>
  );
}

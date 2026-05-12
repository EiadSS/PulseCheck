import { Save } from 'lucide-react';
import { useState, type FormEvent, type ReactNode } from 'react';
import type { MonitorDetail, MonitorRequest, MonitorType } from '../types';

const defaultValues: MonitorRequest = {
  name: '',
  url: '',
  type: 'Website',
  checkIntervalSeconds: 60,
  timeoutSeconds: 10,
  degradedThresholdMs: 800,
  expectedStatusCode: 200,
  expectedKeyword: '',
  isPublic: false
};

export function MonitorForm({
  initialValue,
  submitLabel,
  onSubmit
}: {
  initialValue?: MonitorDetail;
  submitLabel: string;
  onSubmit: (payload: MonitorRequest) => Promise<void>;
}) {
  const [form, setForm] = useState<MonitorRequest>(
    initialValue
      ? {
          name: initialValue.name,
          url: initialValue.url,
          type: initialValue.type,
          checkIntervalSeconds: initialValue.checkIntervalSeconds,
          timeoutSeconds: initialValue.timeoutSeconds,
          degradedThresholdMs: initialValue.degradedThresholdMs,
          expectedStatusCode: initialValue.expectedStatusCode,
          expectedKeyword: initialValue.expectedKeyword ?? '',
          isPublic: initialValue.isPublic
        }
      : defaultValues
  );
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSaving(true);

    try {
      await onSubmit({
        ...form,
        expectedKeyword: form.expectedKeyword?.trim() || null
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to save monitor.');
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6 rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
      {error && <div className="rounded-lg bg-rose-50 px-4 py-3 text-sm font-medium text-rose-700">{error}</div>}

      <div className="grid gap-5 md:grid-cols-2">
        <Field label="Name">
          <input
            required
            value={form.name}
            onChange={(event) => setForm({ ...form, name: event.target.value })}
            className="focus-ring w-full rounded-lg border border-slate-300 px-3 py-2"
            placeholder="Marketing website"
          />
        </Field>

        <Field label="Type">
          <select
            value={form.type}
            onChange={(event) => setForm({ ...form, type: event.target.value as MonitorType })}
            className="focus-ring w-full rounded-lg border border-slate-300 px-3 py-2"
          >
            <option value="Website">Website</option>
            <option value="Api">API</option>
          </select>
        </Field>

        <Field label="URL" className="md:col-span-2">
          <input
            required
            type="url"
            value={form.url}
            onChange={(event) => setForm({ ...form, url: event.target.value })}
            className="focus-ring w-full rounded-lg border border-slate-300 px-3 py-2"
            placeholder="https://example.com/health"
          />
        </Field>

        <NumberField
          label="Check interval"
          suffix="seconds"
          min={30}
          value={form.checkIntervalSeconds}
          onChange={(value) => setForm({ ...form, checkIntervalSeconds: value })}
        />
        <NumberField
          label="Timeout"
          suffix="seconds"
          min={1}
          max={60}
          value={form.timeoutSeconds}
          onChange={(value) => setForm({ ...form, timeoutSeconds: value })}
        />
        <NumberField
          label="Degraded threshold"
          suffix="ms"
          min={1}
          value={form.degradedThresholdMs}
          onChange={(value) => setForm({ ...form, degradedThresholdMs: value })}
        />
        <NumberField
          label="Expected status"
          min={100}
          max={599}
          value={form.expectedStatusCode}
          onChange={(value) => setForm({ ...form, expectedStatusCode: value })}
        />

        <Field label="Required keyword" className="md:col-span-2">
          <input
            value={form.expectedKeyword ?? ''}
            onChange={(event) => setForm({ ...form, expectedKeyword: event.target.value })}
            className="focus-ring w-full rounded-lg border border-slate-300 px-3 py-2"
            placeholder="Optional text that must appear in the response body, like Welcome"
          />
        </Field>
      </div>

      <label className="flex items-center gap-3 rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm font-medium text-slate-700">
        <input
          type="checkbox"
          checked={form.isPublic}
          onChange={(event) => setForm({ ...form, isPublic: event.target.checked })}
          className="h-4 w-4 rounded border-slate-300 text-sky-600 focus:ring-sky-500"
        />
        Show this monitor on the public status page
      </label>

      <button
        type="submit"
        disabled={isSaving}
        className="focus-ring inline-flex items-center gap-2 rounded-lg bg-sky-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-sky-700 disabled:cursor-not-allowed disabled:opacity-60"
      >
        <Save className="h-4 w-4" aria-hidden="true" />
        {isSaving ? 'Saving...' : submitLabel}
      </button>
    </form>
  );
}

function Field({
  label,
  className,
  children
}: {
  label: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <label className={className}>
      <span className="mb-1.5 block text-sm font-semibold text-slate-700">{label}</span>
      {children}
    </label>
  );
}

function NumberField({
  label,
  suffix,
  min,
  max,
  value,
  onChange
}: {
  label: string;
  suffix?: string;
  min: number;
  max?: number;
  value: number;
  onChange: (value: number) => void;
}) {
  return (
    <Field label={label}>
      <div className="flex rounded-lg border border-slate-300 bg-white focus-within:ring-2 focus-within:ring-sky-400 focus-within:ring-offset-2">
        <input
          required
          type="number"
          min={min}
          max={max}
          value={value}
          onChange={(event) => onChange(Number(event.target.value))}
          className="w-full rounded-l-lg border-0 px-3 py-2 focus:outline-none"
        />
        {suffix && <span className="flex items-center border-l border-slate-200 px-3 text-sm font-medium text-slate-500">{suffix}</span>}
      </div>
    </Field>
  );
}

import { useState, type FormEvent, type ReactNode } from 'react';
import { Activity, CheckCircle2, Circle } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { ApiError } from '../api/client';
import { useAuth } from '../contexts/AuthContext';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<string[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setErrors([]);
    setIsSubmitting(true);

    try {
      await login(email, password);
      navigate('/dashboard');
    } catch {
      setErrors(['Email or password is incorrect.']);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <AuthShell title="Welcome back" subtitle="Sign in to manage your monitors.">
      <form onSubmit={handleSubmit} className="space-y-4">
        <AuthAlert messages={errors} />
        <Input label="Email" type="email" value={email} onChange={setEmail} />
        <Input label="Password" type="password" value={password} onChange={setPassword} />
        <button
          type="submit"
          disabled={isSubmitting}
          className="focus-ring w-full rounded-lg bg-sky-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-sky-700 disabled:opacity-60"
        >
          {isSubmitting ? 'Signing in...' : 'Sign in'}
        </button>
      </form>
      <p className="mt-6 text-center text-sm text-slate-500">
        New here?{' '}
        <Link to="/register" className="font-semibold text-sky-700 hover:text-sky-800">
          Create an account
        </Link>
      </p>
    </AuthShell>
  );
}

export function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [workspaceName, setWorkspaceName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<string[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setErrors([]);

    const registrationIssues = getRegistrationIssues(workspaceName, email, password);
    if (registrationIssues.length > 0) {
      setErrors(registrationIssues);
      return;
    }

    setIsSubmitting(true);

    try {
      await register(email, password, workspaceName);
      navigate('/dashboard');
    } catch (err) {
      setErrors(err instanceof ApiError ? err.messages : [err instanceof Error ? err.message : 'Unable to create account.']);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <AuthShell title="Create your workspace">
      <form onSubmit={handleSubmit} noValidate className="space-y-4">
        <AuthAlert messages={errors} />
        <Input label="Workspace name" value={workspaceName} onChange={setWorkspaceName} />
        <Input label="Email" type="email" value={email} onChange={setEmail} />
        <Input label="Password" type="password" value={password} onChange={setPassword} />
        <PasswordGuidance password={password} />
        <button
          type="submit"
          disabled={isSubmitting}
          className="focus-ring w-full rounded-lg bg-sky-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-sky-700 disabled:opacity-60"
        >
          {isSubmitting ? 'Creating...' : 'Create account'}
        </button>
      </form>
      <p className="mt-6 text-center text-sm text-slate-500">
        Already have an account?{' '}
        <Link to="/login" className="font-semibold text-sky-700 hover:text-sky-800">
          Sign in
        </Link>
      </p>
    </AuthShell>
  );
}

function AuthAlert({ messages }: { messages: string[] }) {
  if (messages.length === 0) {
    return null;
  }

  return (
    <div className="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
      <p className="font-semibold">Please fix the following:</p>
      <ul className="mt-2 list-disc space-y-1 pl-5">
        {messages.map((message) => (
          <li key={message}>{message}</li>
        ))}
      </ul>
    </div>
  );
}

function PasswordGuidance({ password }: { password: string }) {
  const rules = [
    { label: 'At least 8 characters', isMet: password.length >= 8 },
    { label: 'One uppercase letter', isMet: /[A-Z]/.test(password) },
    { label: 'One lowercase letter', isMet: /[a-z]/.test(password) },
    { label: 'One number', isMet: /\d/.test(password) }
  ];

  return (
    <div className="rounded-lg bg-slate-50 px-4 py-3">
      <p className="text-xs font-semibold uppercase text-slate-500">Password must include</p>
      <div className="mt-2 grid gap-1.5 text-sm sm:grid-cols-2">
        {rules.map((rule) => (
          <span key={rule.label} className={`flex items-center gap-2 ${rule.isMet ? 'font-medium text-emerald-700' : 'text-slate-500'}`}>
            {rule.isMet ? <CheckCircle2 className="h-4 w-4" aria-hidden="true" /> : <Circle className="h-4 w-4" aria-hidden="true" />}
            {rule.label}
          </span>
        ))}
      </div>
    </div>
  );
}

function getRegistrationIssues(workspaceName: string, email: string, password: string) {
  const issues: string[] = [];

  if (!workspaceName.trim()) {
    issues.push('Workspace name is required.');
  }

  if (!email.trim()) {
    issues.push('Email is required.');
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
    issues.push('Enter a valid email address.');
  }

  return [...issues, ...getPasswordIssues(password)];
}

function getPasswordIssues(password: string) {
  const issues: string[] = [];

  if (password.length < 8) {
    issues.push('Use at least 8 characters.');
  }

  if (!/[A-Z]/.test(password)) {
    issues.push('Add at least one uppercase letter.');
  }

  if (!/[a-z]/.test(password)) {
    issues.push('Add at least one lowercase letter.');
  }

  if (!/\d/.test(password)) {
    issues.push('Add at least one number.');
  }

  return issues;
}

function AuthShell({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <main className="grid min-h-screen place-items-center bg-slate-50 px-4 py-10">
      <div className="w-full max-w-md">
        <Link to="/" className="mx-auto mb-8 flex w-max items-center gap-3 text-slate-950">
          <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-sky-600 text-white">
            <Activity className="h-5 w-5" aria-hidden="true" />
          </span>
          <span className="text-lg font-semibold">PulseCheck</span>
        </Link>
        <div className="rounded-lg border border-slate-200 bg-white p-6 shadow-soft">
          <h1 className="text-2xl font-semibold text-slate-950">{title}</h1>
          {subtitle && <p className="mt-2 text-sm text-slate-500">{subtitle}</p>}
          <div className="mt-6">{children}</div>
        </div>
      </div>
    </main>
  );
}

function Input({
  label,
  type = 'text',
  value,
  onChange
}: {
  label: string;
  type?: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label>
      <span className="mb-1.5 block text-sm font-semibold text-slate-700">{label}</span>
      <input
        required
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="focus-ring w-full rounded-lg border border-slate-300 px-3 py-2"
      />
    </label>
  );
}

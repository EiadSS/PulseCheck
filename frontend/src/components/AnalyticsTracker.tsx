import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../contexts/AuthContext';

const visitorIdKey = 'pulsecheck.visitorId';
let fallbackVisitorId: string | null = null;

export function AnalyticsTracker() {
  const location = useLocation();
  const { user, isLoading } = useAuth();

  useEffect(() => {
    if (isLoading) {
      return;
    }

    if (user && location.pathname === '/') {
      return;
    }

    void api
      .trackAnalyticsEvent({
        eventType: 'PageView',
        path: location.pathname,
        visitorId: getOrCreateVisitorId()
      })
      .catch(() => undefined);
  }, [isLoading, location.pathname, user?.id]);

  return null;
}

export function getOrCreateVisitorId() {
  try {
    const existing = localStorage.getItem(visitorIdKey);
    if (existing) {
      return existing;
    }

    const visitorId = createVisitorId();
    localStorage.setItem(visitorIdKey, visitorId);
    return visitorId;
  } catch {
    fallbackVisitorId ??= createVisitorId();
    return fallbackVisitorId;
  }
}

function createVisitorId() {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID();
  }

  const bytes = new Uint8Array(16);
  if (globalThis.crypto?.getRandomValues) {
    globalThis.crypto.getRandomValues(bytes);
  } else {
    for (let index = 0; index < bytes.length; index++) {
      bytes[index] = Math.floor(Math.random() * 256);
    }
  }

  return `visitor-${Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('')}`;
}

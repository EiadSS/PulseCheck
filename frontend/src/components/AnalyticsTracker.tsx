import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../contexts/AuthContext';

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
        path: location.pathname
      })
      .catch(() => undefined);
  }, [isLoading, location.pathname, user?.id]);

  return null;
}

import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { api } from '../api/client';

export function AnalyticsTracker() {
  const location = useLocation();

  useEffect(() => {
    void api
      .trackAnalyticsEvent({
        eventType: 'PageView',
        path: location.pathname
      })
      .catch(() => undefined);
  }, [location.pathname]);

  return null;
}

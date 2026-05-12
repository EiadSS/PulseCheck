import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AnalyticsTracker } from './components/AnalyticsTracker';
import { AppLayout } from './components/AppLayout';
import { ProtectedRoute } from './components/ProtectedRoute';
import { AuthProvider } from './contexts/AuthContext';
import { AnalyticsPage } from './pages/AnalyticsPage';
import { DashboardPage } from './pages/DashboardPage';
import { LandingPage } from './pages/LandingPage';
import { LoginPage, RegisterPage } from './pages/LoginPage';
import { EditMonitorPage, NewMonitorPage } from './pages/MonitorEditorPage';
import { MonitorDetailsPage } from './pages/MonitorDetailsPage';
import { PublicStatusPage } from './pages/PublicStatusPage';
import { SloPage } from './pages/SloPage';

export function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AnalyticsTracker />
        <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/status/:slug" element={<PublicStatusPage />} />
          <Route element={<ProtectedRoute />}>
            <Route element={<AppLayout />}>
              <Route path="/dashboard" element={<DashboardPage />} />
              <Route path="/analytics" element={<AnalyticsPage />} />
              <Route path="/slo" element={<SloPage />} />
              <Route path="/monitors/new" element={<NewMonitorPage />} />
              <Route path="/monitors/:id" element={<MonitorDetailsPage />} />
              <Route path="/monitors/:id/edit" element={<EditMonitorPage />} />
            </Route>
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}

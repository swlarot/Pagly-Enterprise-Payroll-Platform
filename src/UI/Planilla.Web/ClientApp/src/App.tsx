import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { AuthProvider } from './contexts/AuthContext';
import { ProtectedRoute } from './components/auth/ProtectedRoute';
import { RoleGuard } from './components/auth/RoleGuard';
import AuthLayout from './components/layout/AuthLayout';
import { TenantRole } from './types/api';

// Auth Pages
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import AcceptInvitePage from './pages/AcceptInvitePage';

// Admin Pages
import AdminDashboardPage from './pages/AdminDashboardPage';
import UsersPage from './pages/UsersPage';
import AuditLogPage from './pages/AuditLogPage';
import BillingPage from './pages/BillingPage';

// Existing Pages
import EmpleadosPage from './pages/EmpleadosPage';
import DepartamentosPage from './pages/DepartamentosPage';
import PosicionesPage from './pages/PosicionesPage';
import PrestamosPage from './pages/PrestamosPage';
import DeduccionesPage from './pages/DeduccionesPage';
import AnticiposPage from './pages/AnticiposPage';
import HorasExtraPage from './pages/HorasExtraPage';
import AusenciasPage from './pages/AusenciasPage';
import VacacionesPage from './pages/VacacionesPage';
import PlanillasPage from './pages/PlanillasPage';
import ConfiguracionPage from './pages/ConfiguracionPage';
import ReportesPage from './pages/ReportesPage';

function App() {
  return (
    <AuthProvider>
      <Toaster
        position="top-right"
        toastOptions={{
          duration: 4000,
          style: {
            background: '#fff',
            color: '#363636',
            fontSize: '14px',
          },
          success: {
            duration: 3000,
            iconTheme: {
              primary: '#10b981',
              secondary: '#fff',
            },
          },
          error: {
            duration: 5000,
            iconTheme: {
              primary: '#ef4444',
              secondary: '#fff',
            },
          },
        }}
      />

      <Routes>
        {/* Public Routes */}
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/accept-invite" element={<AcceptInvitePage />} />

        {/* Protected Routes */}
        <Route
          path="/dashboard"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <AdminDashboardPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        {/* Admin Routes - Owner/Admin Only */}
        <Route
          path="/users"
          element={
            <ProtectedRoute>
              <RoleGuard allowedRoles={[TenantRole.Owner, TenantRole.Admin]}>
                <AuthLayout>
                  <UsersPage />
                </AuthLayout>
              </RoleGuard>
            </ProtectedRoute>
          }
        />

        {/* Audit Log - Owner/Admin/Manager/Accountant */}
        <Route
          path="/audit"
          element={
            <ProtectedRoute>
              <RoleGuard
                allowedRoles={[
                  TenantRole.Owner,
                  TenantRole.Admin,
                  TenantRole.Manager,
                  TenantRole.Accountant,
                ]}
              >
                <AuthLayout>
                  <AuditLogPage />
                </AuthLayout>
              </RoleGuard>
            </ProtectedRoute>
          }
        />

        {/* Billing - Owner Only */}
        <Route
          path="/billing"
          element={
            <ProtectedRoute>
              <RoleGuard allowedRoles={[TenantRole.Owner]}>
                <AuthLayout>
                  <BillingPage />
                </AuthLayout>
              </RoleGuard>
            </ProtectedRoute>
          }
        />

        {/* Existing Planilla Routes - All Authenticated Users */}
        <Route
          path="/empleados"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <EmpleadosPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        <Route
          path="/departamentos"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <DepartamentosPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        <Route
          path="/posiciones"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <PosicionesPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        <Route
          path="/prestamos"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <PrestamosPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        <Route
          path="/deducciones"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <DeduccionesPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        <Route
          path="/anticipos"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <AnticiposPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        <Route
          path="/horas-extra"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <HorasExtraPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        <Route
          path="/ausencias"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <AusenciasPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        <Route
          path="/vacaciones"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <VacacionesPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        <Route
          path="/planillas"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <PlanillasPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        <Route
          path="/reportes"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <ReportesPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        <Route
          path="/configuracion"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <ConfiguracionPage />
              </AuthLayout>
            </ProtectedRoute>
          }
        />

        {/* Redirect root to dashboard */}
        <Route path="/" element={<Navigate to="/dashboard" replace />} />

        {/* 404 - Redirect to dashboard */}
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </AuthProvider>
  );
}

export default App;

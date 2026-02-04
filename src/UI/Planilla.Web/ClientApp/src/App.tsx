import { Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { AuthProvider } from './contexts/AuthContext';
import { ProtectedRoute } from './components/auth/ProtectedRoute';
import { RoleGuard } from './components/auth/RoleGuard';
import { SystemAdminRoute } from './components/auth/SystemAdminRoute';
import AuthLayout from './components/layout/AuthLayout';
import { TenantRole } from './types/api';

// Auth Pages
import LoginPage from './pages/LoginPage';
import AcceptInvitePage from './pages/AcceptInvitePage';
import TenantSelectorPage from './pages/TenantSelectorPage';

// Admin Pages (Tenant)
import AdminDashboardPage from './pages/AdminDashboardPage';
import AuditLogPage from './pages/AuditLogPage';
import RolesAndPermissionsPage from './pages/RolesAndPermissionsPage';

// System Admin Pages
import SystemAdminDashboardPage from './pages/SystemAdminDashboardPage';
import TenantsManagementPage from './pages/TenantsManagementPage';
import CreateTenantPage from './pages/CreateTenantPage';
import TenantDetailsPage from './pages/TenantDetailsPage';
import SystemUsersPage from './pages/SystemUsersPage';

// Existing Pages (.jsx)
import EmpleadosPage from './pages/EmpleadosPage.jsx';
import DepartamentosPage from './pages/DepartamentosPage.jsx';
import PosicionesPage from './pages/PosicionesPage.jsx';
import PrestamosPage from './pages/PrestamosPage.jsx';
import DeduccionesPage from './pages/DeduccionesPage.jsx';
import AnticiposPage from './pages/AnticiposPage.jsx';
import HorasExtraPage from './pages/HorasExtraPage.jsx';
import AusenciasPage from './pages/AusenciasPage.jsx';
import VacacionesPage from './pages/VacacionesPage.jsx';
import PlanillasPage from './pages/PlanillasPage.jsx';
import ConfiguracionPage from './pages/ConfiguracionPage.jsx';
import ReportesPage from './pages/ReportesPage.jsx';

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
        {/* Public Routes - Login Only (No Self-Registration) */}
        <Route path="/login" element={<LoginPage />} />
        <Route path="/accept-invite" element={<AcceptInvitePage />} />
        <Route path="/select-tenant" element={<TenantSelectorPage />} />

        {/* System Admin Routes */}
        <Route
          path="/system-admin/dashboard"
          element={
            <SystemAdminRoute>
              <SystemAdminDashboardPage />
            </SystemAdminRoute>
          }
        />
        <Route
          path="/system-admin/tenants"
          element={
            <SystemAdminRoute>
              <TenantsManagementPage />
            </SystemAdminRoute>
          }
        />
        <Route
          path="/system-admin/tenants/create"
          element={
            <SystemAdminRoute>
              <CreateTenantPage />
            </SystemAdminRoute>
          }
        />
        <Route
          path="/system-admin/tenants/:id"
          element={
            <SystemAdminRoute>
              <TenantDetailsPage />
            </SystemAdminRoute>
          }
        />
        <Route
          path="/system-admin/users"
          element={
            <SystemAdminRoute>
              <SystemUsersPage />
            </SystemAdminRoute>
          }
        />

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

        {/* Audit Log - Owner and User */}
        <Route
          path="/audit"
          element={
            <ProtectedRoute>
              <RoleGuard
                allowedRoles={[
                  TenantRole.Owner,
                  TenantRole.User,
                ]}
              >
                <AuthLayout>
                  <AuditLogPage />
                </AuthLayout>
              </RoleGuard>
            </ProtectedRoute>
          }
        />

        {/* Roles & Permissions (includes Users Management) - Owner Only */}
        <Route
          path="/roles"
          element={
            <ProtectedRoute>
              <RoleGuard allowedRoles={[TenantRole.Owner]}>
                <AuthLayout>
                  <RolesAndPermissionsPage />
                </AuthLayout>
              </RoleGuard>
            </ProtectedRoute>
          }
        />

        {/* Billing removed from client app - managed via Admin Panel only */}

        {/* Existing Pagly Routes - All Authenticated Users */}
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

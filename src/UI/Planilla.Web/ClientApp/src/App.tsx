import { Routes, Route, Navigate } from 'react-router-dom';
import toast, { Toaster, ToastBar } from 'react-hot-toast';
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
import MiPerfilPage from './pages/MiPerfilPage';

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
import AcreedoresPage from './pages/AcreedoresPage.jsx';
import AnticiposPage from './pages/AnticiposPage.jsx';
import HorasExtraPage from './pages/HorasExtraPage.jsx';
import AusenciasPage from './pages/AusenciasPage.jsx';
import VacacionesPage from './pages/VacacionesPage.jsx';
import PlanillasPage from './pages/PlanillasPage.jsx';
import DecimoPage from './pages/DecimoPage.jsx';
import ConfiguracionPage from './pages/ConfiguracionPage.jsx';
import SalarioMinimoPage from './pages/SalarioMinimoPage.jsx';
import ReportesPage from './pages/ReportesPage.jsx';

function App() {
  return (
    <AuthProvider>
      <Toaster
        position="top-right"
        toastOptions={{
          duration: 5000,
          style: {
            background: '#102a43',
            color: '#f3f4f6',
            fontSize: '15px',
            border: '1px solid #334e68',
            borderRadius: '12px',
            padding: '14px 16px',
            maxWidth: '420px',
            boxShadow: '0 8px 24px rgba(0,0,0,0.4)',
          },
          success: {
            duration: 4000,
            iconTheme: { primary: '#10b981', secondary: '#fff' },
          },
          error: {
            duration: 7000,
            iconTheme: { primary: '#ef4444', secondary: '#fff' },
          },
        }}
      >
        {(t) => (
          <ToastBar toast={t}>
            {({ icon, message }) => (
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px', width: '100%' }}>
                {icon}
                <span style={{ flex: 1, fontSize: '15px', lineHeight: '1.5' }}>{message}</span>
                {t.type !== 'loading' && (
                  <button
                    onClick={() => toast.dismiss(t.id)}
                    style={{
                      flexShrink: 0,
                      width: '28px',
                      height: '28px',
                      borderRadius: '8px',
                      border: '1px solid rgba(255,255,255,0.15)',
                      background: 'rgba(255,255,255,0.08)',
                      color: '#94a3b8',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontSize: '16px',
                      lineHeight: '1',
                      transition: 'all 0.15s',
                      padding: 0,
                      minHeight: 'unset',
                    }}
                    onMouseEnter={(e) => {
                      (e.currentTarget as HTMLButtonElement).style.background = 'rgba(255,255,255,0.18)';
                      (e.currentTarget as HTMLButtonElement).style.color = '#f1f5f9';
                    }}
                    onMouseLeave={(e) => {
                      (e.currentTarget as HTMLButtonElement).style.background = 'rgba(255,255,255,0.08)';
                      (e.currentTarget as HTMLButtonElement).style.color = '#94a3b8';
                    }}
                    aria-label="Cerrar notificación"
                  >
                    ✕
                  </button>
                )}
              </div>
            )}
          </ToastBar>
        )}
      </Toaster>

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

        {/* Mi Perfil - Employee Self-Service */}
        <Route
          path="/mi-perfil"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <MiPerfilPage />
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
          path="/acreedores"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <AcreedoresPage />
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
          path="/decimo"
          element={
            <ProtectedRoute>
              <AuthLayout>
                <DecimoPage />
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

        <Route
          path="/configuracion/salario-minimo"
          element={
            <ProtectedRoute>
              <RoleGuard allowedRoles={[TenantRole.Owner]}>
                <AuthLayout>
                  <SalarioMinimoPage />
                </AuthLayout>
              </RoleGuard>
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

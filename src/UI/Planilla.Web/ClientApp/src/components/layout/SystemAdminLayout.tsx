import React from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import {
  LayoutDashboard,
  Building2,
  Users,
  LogOut,
  ChevronDown,
} from 'lucide-react';

interface SystemAdminLayoutProps {
  children: React.ReactNode;
}

export function SystemAdminLayout({ children }: SystemAdminLayoutProps) {
  const { user, tenant, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [showUserMenu, setShowUserMenu] = React.useState(false);

  const navigation = [
    { name: 'Dashboard', href: '/system-admin/dashboard', icon: LayoutDashboard },
    { name: 'Tenants', href: '/system-admin/tenants', icon: Building2 },
    { name: 'Usuarios', href: '/system-admin/users', icon: Users },
  ];

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="min-h-screen bg-navy-950">
      {/* Top Navigation Bar */}
      <header className="bg-navy-900 border-b border-navy-700 sticky top-0 z-40">
        <div className="px-6 py-4">
          <div className="flex items-center justify-between">
            {/* Logo and Title */}
            <div className="flex items-center gap-4">
              <div className="flex items-center gap-2">
                <div className="w-10 h-10 bg-gradient-to-br from-primary-600 to-primary-700 rounded-lg flex items-center justify-center">
                  <span className="text-white font-bold text-xl">P</span>
                </div>
                <div>
                  <h1 className="text-xl font-bold text-gray-100">Pagly Admin</h1>
                  <p className="text-xs text-gray-400">System Administration</p>
                </div>
              </div>

              {/* Navigation */}
              <nav className="hidden md:flex items-center gap-2 ml-8">
                {navigation.map((item) => {
                  const isActive = location.pathname.startsWith(item.href);
                  return (
                    <Link
                      key={item.name}
                      to={item.href}
                      className={`
                        flex items-center gap-2 px-4 py-2 rounded-lg
                        text-sm font-medium transition-colors
                        ${
                          isActive
                            ? 'bg-primary-500/15 text-primary-400'
                            : 'text-gray-400 hover:bg-navy-800 hover:text-gray-200'
                        }
                      `}
                    >
                      <item.icon className="w-4 h-4" />
                      {item.name}
                    </Link>
                  );
                })}
              </nav>
            </div>

            {/* User Menu */}
            <div className="relative">
              <button
                onClick={() => setShowUserMenu(!showUserMenu)}
                className="flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-navy-800 transition-colors"
              >
                <div className="w-8 h-8 bg-primary-600 rounded-full flex items-center justify-center">
                  <span className="text-white text-sm font-medium">
                    {user?.email.charAt(0).toUpperCase()}
                  </span>
                </div>
                <div className="hidden md:block text-left">
                  <p className="text-sm font-medium text-gray-200">{user?.email}</p>
                  <p className="text-xs text-primary-400">System Admin</p>
                </div>
                <ChevronDown className="w-4 h-4 text-gray-400" />
              </button>

              {showUserMenu && (
                <>
                  <div
                    className="fixed inset-0 z-10"
                    onClick={() => setShowUserMenu(false)}
                  />
                  <div className="absolute right-0 mt-2 w-56 bg-navy-900 rounded-lg shadow-xl shadow-black/25 border border-navy-700 py-1 z-20">
                    <div className="px-4 py-3 border-b border-navy-700">
                      <p className="text-sm font-medium text-gray-200">{user?.email}</p>
                      <p className="text-xs text-gray-400 mt-1">System Administrator</p>
                    </div>
                    {tenant && (
                      <button
                        onClick={() => navigate('/dashboard')}
                        className="w-full flex items-center gap-2 px-4 py-2 text-sm text-gray-300 hover:bg-navy-800 transition-colors"
                      >
                        <Building2 className="w-4 h-4" />
                        Ir a Mi Tenant
                      </button>
                    )}
                    <button
                      onClick={handleLogout}
                      className="w-full flex items-center gap-2 px-4 py-2 text-sm text-red-400 hover:bg-navy-800 transition-colors"
                    >
                      <LogOut className="w-4 h-4" />
                      Cerrar Sesión
                    </button>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="w-full">{children}</main>
    </div>
  );
}

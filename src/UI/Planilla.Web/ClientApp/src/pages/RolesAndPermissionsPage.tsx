import React, { useState } from 'react';
import { Shield } from 'lucide-react';
import { RolesTab } from '../components/roles/RolesTab';
import { UsersManagementTab } from '../components/roles/UsersManagementTab';

type TabId = 'roles' | 'users';

export default function RolesAndPermissionsPage() {
  const [activeTab, setActiveTab] = useState<TabId>('roles');

  const tabs = [
    { id: 'roles' as TabId, label: 'Roles Personalizados', icon: Shield },
    { id: 'users' as TabId, label: 'Gestión de Usuarios', icon: Shield },
  ];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold text-gray-900 flex items-center gap-3">
          <Shield className="w-8 h-8 text-blue-600" />
          Roles y Permisos
        </h1>
        <p className="text-gray-600 mt-2">
          Gestiona los roles, permisos y usuarios de tu organización
        </p>
      </div>

      {/* Tabs */}
      <div className="border-b border-gray-200">
        <nav className="-mb-px flex space-x-8" aria-label="Tabs">
          {tabs.map((tab) => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`
                  flex items-center gap-2 py-4 px-1 border-b-2 font-medium text-sm
                  transition-colors duration-200
                  ${
                    isActive
                      ? 'border-blue-600 text-blue-600'
                      : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                  }
                `}
              >
                <Icon className="w-5 h-5" />
                {tab.label}
              </button>
            );
          })}
        </nav>
      </div>

      {/* Tab Content */}
      <div className="mt-6">
        {activeTab === 'roles' && <RolesTab />}
        {activeTab === 'users' && <UsersManagementTab />}
      </div>
    </div>
  );
}

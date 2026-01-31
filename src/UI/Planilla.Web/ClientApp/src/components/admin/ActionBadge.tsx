import React from 'react';
import { Plus, Edit, Trash2, LogIn, UserPlus, Mail, Shield } from 'lucide-react';

interface ActionBadgeProps {
  action: string;
}

export function ActionBadge({ action }: ActionBadgeProps) {
  const getActionConfig = (action: string) => {
    // Normalizar la acción
    const normalizedAction = action.toLowerCase();

    if (normalizedAction.includes('create') || normalizedAction.includes('created')) {
      return {
        icon: Plus,
        color: 'bg-green-100 text-green-800',
        label: 'Crear',
      };
    }

    if (normalizedAction.includes('update') || normalizedAction.includes('updated')) {
      return {
        icon: Edit,
        color: 'bg-blue-100 text-blue-800',
        label: 'Actualizar',
      };
    }

    if (normalizedAction.includes('delete') || normalizedAction.includes('deleted')) {
      return {
        icon: Trash2,
        color: 'bg-red-100 text-red-800',
        label: 'Eliminar',
      };
    }

    if (normalizedAction.includes('login')) {
      return {
        icon: LogIn,
        color: 'bg-purple-100 text-purple-800',
        label: 'Login',
      };
    }

    if (normalizedAction.includes('invite')) {
      return {
        icon: UserPlus,
        color: 'bg-indigo-100 text-indigo-800',
        label: 'Invitar',
      };
    }

    if (normalizedAction.includes('email') || normalizedAction.includes('mail')) {
      return {
        icon: Mail,
        color: 'bg-amber-100 text-amber-800',
        label: 'Email',
      };
    }

    // Default
    return {
      icon: Shield,
      color: 'bg-gray-100 text-gray-800',
      label: action,
    };
  };

  const config = getActionConfig(action);
  const Icon = config.icon;

  return (
    <span className={`inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium ${config.color}`}>
      <Icon className="w-3 h-3" />
      {config.label}
    </span>
  );
}

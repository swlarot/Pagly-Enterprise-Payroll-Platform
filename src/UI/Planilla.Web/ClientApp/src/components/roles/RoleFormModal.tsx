import React, { useEffect, useState } from 'react';
import { Modal } from '../ui/Modal';
import { Button } from '../ui/Button';
import { Input } from '../ui/Input';
import { roleService } from '../../services/roleService';
import toast from 'react-hot-toast';
import { Save } from 'lucide-react';
import type { CustomTenantRoleDto, CreateCustomTenantRoleDto, UpdateCustomTenantRoleDto } from '../../types/api';

interface RoleFormModalProps {
  role: CustomTenantRoleDto | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

const PREDEFINED_COLORS = [
  { value: '#ef4444', label: 'Rojo' },
  { value: '#f97316', label: 'Naranja' },
  { value: '#eab308', label: 'Amarillo' },
  { value: '#22c55e', label: 'Verde' },
  { value: '#3b82f6', label: 'Azul' },
  { value: '#8b5cf6', label: 'Morado' },
  { value: '#ec4899', label: 'Rosa' },
  { value: '#6b7280', label: 'Gris' },
];

export function RoleFormModal({ role, isOpen, onClose, onSuccess }: RoleFormModalProps) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [color, setColor] = useState(PREDEFINED_COLORS[0].value);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (isOpen) {
      if (role) {
        setName(role.name);
        setDescription(role.description);
        setColor(role.color);
      } else {
        setName('');
        setDescription('');
        setColor(PREDEFINED_COLORS[0].value);
      }
    }
  }, [isOpen, role]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!name.trim()) {
      toast.error('El nombre del rol es requerido');
      return;
    }

    if (!description.trim()) {
      toast.error('La descripción es requerida');
      return;
    }

    try {
      setIsSubmitting(true);

      if (role) {
        // Actualizar rol existente
        const dto: UpdateCustomTenantRoleDto = {
          name: name.trim(),
          description: description.trim(),
          color,
        };
        await roleService.updateRole(role.id, dto);
        toast.success('Rol actualizado exitosamente');
      } else {
        // Crear nuevo rol
        const dto: CreateCustomTenantRoleDto = {
          name: name.trim(),
          description: description.trim(),
          color,
        };
        await roleService.createRole(dto);
        toast.success('Rol creado exitosamente');
      }

      onSuccess();
      onClose();
    } catch (error: any) {
      toast.error(error.message || 'Error al guardar rol');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={role ? 'Editar Rol' : 'Crear Rol Personalizado'}
      size="md"
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Nombre del Rol"
          type="text"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Ej: Supervisor de Nómina"
          required
          maxLength={50}
        />

        <div>
          <label className="block text-sm font-medium text-gray-300 mb-2">Descripción</label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Describe las responsabilidades de este rol"
            rows={3}
            maxLength={200}
            required
            className="w-full px-3 py-2 bg-navy-800 text-gray-100 border border-navy-600 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
          />
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-300 mb-2">Color</label>
          <div className="grid grid-cols-4 gap-3">
            {PREDEFINED_COLORS.map((colorOption) => (
              <button
                key={colorOption.value}
                type="button"
                onClick={() => setColor(colorOption.value)}
                className={`flex items-center justify-center p-3 rounded-lg border-2 transition-all ${
                  color === colorOption.value
                    ? 'border-gray-100 shadow-md scale-105'
                    : 'border-navy-700 hover:border-navy-500'
                }`}
              >
                <div
                  className="w-8 h-8 rounded-full"
                  style={{ backgroundColor: colorOption.value }}
                />
              </button>
            ))}
          </div>
        </div>

        <div className="flex items-center justify-end gap-3 pt-4 border-t border-navy-700">
          <Button type="button" variant="outline" onClick={onClose} disabled={isSubmitting}>
            Cancelar
          </Button>
          <Button type="submit" icon={Save} loading={isSubmitting}>
            {role ? 'Actualizar' : 'Crear'} Rol
          </Button>
        </div>
      </form>
    </Modal>
  );
}

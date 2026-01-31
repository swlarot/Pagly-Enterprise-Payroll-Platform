import React, { useState } from 'react';
import { Modal } from '../ui/Modal';
import { Input } from '../ui/Input';
import { Select } from '../ui/Select';
import { Button } from '../ui/Button';
import { TenantRole } from '../../types/api';
import { systemAdminService } from '../../services/systemAdminService';
import toast from 'react-hot-toast';

interface InviteUserModalProps {
  tenantId: number;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function InviteUserModal({ tenantId, isOpen, onClose, onSuccess }: InviteUserModalProps) {
  const [formData, setFormData] = useState({
    email: '',
    fullName: '',
    password: '',
    role: TenantRole.Employee,
  });
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    // Validaciones básicas
    if (!formData.email || !formData.fullName || !formData.password) {
      toast.error('Todos los campos son requeridos');
      return;
    }

    if (formData.password.length < 6) {
      toast.error('La contraseña debe tener al menos 6 caracteres');
      return;
    }

    try {
      setIsSubmitting(true);
      await systemAdminService.inviteUserToTenant(tenantId, formData);
      toast.success('Usuario invitado exitosamente');
      setFormData({
        email: '',
        fullName: '',
        password: '',
        role: TenantRole.Employee,
      });
      onSuccess();
      onClose();
    } catch (error: any) {
      toast.error(error.message || 'Error al invitar usuario');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleClose = () => {
    setFormData({
      email: '',
      fullName: '',
      password: '',
      role: TenantRole.Employee,
    });
    onClose();
  };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="Invitar Usuario al Tenant">
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Email"
          type="email"
          value={formData.email}
          onChange={(e) => setFormData({ ...formData, email: e.target.value })}
          placeholder="usuario@empresa.com"
          required
        />

        <Input
          label="Nombre Completo"
          type="text"
          value={formData.fullName}
          onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
          placeholder="Juan Pérez"
          required
        />

        <Input
          label="Contraseña Temporal"
          type="password"
          value={formData.password}
          onChange={(e) => setFormData({ ...formData, password: e.target.value })}
          placeholder="Mínimo 6 caracteres"
          required
          helperText="El usuario podrá cambiarla después del primer login"
        />

        <Select
          label="Rol"
          value={formData.role.toString()}
          onChange={(e) => setFormData({ ...formData, role: parseInt(e.target.value) })}
          options={[
            { value: TenantRole.Admin.toString(), label: 'Admin - Gestión completa excepto billing' },
            { value: TenantRole.Manager.toString(), label: 'Manager - Planillas, empleados, reportes' },
            { value: TenantRole.Accountant.toString(), label: 'Contador - Solo reportes y consultas' },
            { value: TenantRole.Employee.toString(), label: 'Empleado - Solo ver su información' },
          ]}
        />

        <div className="flex justify-end gap-3 pt-4">
          <Button type="button" variant="outline" onClick={handleClose} disabled={isSubmitting}>
            Cancelar
          </Button>
          <Button type="submit" loading={isSubmitting}>
            Invitar Usuario
          </Button>
        </div>
      </form>
    </Modal>
  );
}

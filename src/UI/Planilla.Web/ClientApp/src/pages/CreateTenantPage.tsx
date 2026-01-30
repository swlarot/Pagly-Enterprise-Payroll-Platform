import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { SystemAdminLayout } from '../components/layout/SystemAdminLayout';
import { Card, CardBody, CardHeader } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { systemAdminService } from '../services/systemAdminService';
import { ArrowLeft, CheckCircle2 } from 'lucide-react';
import toast from 'react-hot-toast';

export default function CreateTenantPage() {
  const navigate = useNavigate();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);
  const [createdTenant, setCreatedTenant] = useState<any>(null);

  const [formData, setFormData] = useState({
    name: '',
    ruc: '',
    dv: '',
    ownerEmail: '',
    ownerPassword: '',
    ownerFullName: '',
  });

  const [errors, setErrors] = useState<Record<string, string>>({});

  const validateForm = () => {
    const newErrors: Record<string, string> = {};

    if (!formData.name.trim()) {
      newErrors.name = 'El nombre de la empresa es requerido';
    }

    if (!formData.ownerEmail.trim()) {
      newErrors.ownerEmail = 'El email del propietario es requerido';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.ownerEmail)) {
      newErrors.ownerEmail = 'El email no es válido';
    }

    if (!formData.ownerPassword.trim()) {
      newErrors.ownerPassword = 'La contraseña es requerida';
    } else if (formData.ownerPassword.length < 6) {
      newErrors.ownerPassword = 'La contraseña debe tener al menos 6 caracteres';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validateForm()) {
      return;
    }

    try {
      setIsSubmitting(true);
      const tenant = await systemAdminService.createTenant(formData);
      setCreatedTenant(tenant);
      setShowSuccess(true);
      toast.success('Tenant creado exitosamente');
    } catch (error: any) {
      toast.error(error.message || 'Error al crear tenant');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleInputChange = (field: string, value: any) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
    // Clear error when user starts typing
    if (errors[field]) {
      setErrors((prev) => ({ ...prev, [field]: '' }));
    }
  };

  if (showSuccess && createdTenant) {
    return (
      <SystemAdminLayout>
        <div className="max-w-2xl mx-auto px-6 py-8">
          <Card>
            <CardBody className="text-center py-12">
              <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-6">
                <CheckCircle2 className="w-10 h-10 text-green-600" />
              </div>
              <h2 className="text-2xl font-bold text-gray-900 mb-2">Tenant Creado Exitosamente</h2>
              <p className="text-gray-600 mb-8">
                El tenant ha sido creado y el propietario puede iniciar sesión con las siguientes credenciales:
              </p>

              <div className="bg-gray-50 rounded-lg p-6 text-left mb-8 space-y-3">
                <div>
                  <p className="text-sm text-gray-500">Empresa:</p>
                  <p className="font-medium text-gray-900">{createdTenant.name}</p>
                </div>
                {createdTenant.ruc && createdTenant.dv && (
                  <div>
                    <p className="text-sm text-gray-500">RUC:</p>
                    <p className="font-medium text-gray-900">{createdTenant.ruc}-{createdTenant.dv}</p>
                  </div>
                )}
                <div>
                  <p className="text-sm text-gray-500">Email del Propietario:</p>
                  <p className="font-medium text-gray-900">{createdTenant.owner?.email || 'N/A'}</p>
                </div>
                {createdTenant.subscription && (
                  <>
                    <div>
                      <p className="text-sm text-gray-500">Plan:</p>
                      <p className="font-medium text-gray-900">{createdTenant.subscription.planName}</p>
                    </div>
                    {createdTenant.subscription.trialEndsAt && (
                      <div>
                        <p className="text-sm text-gray-500">Trial expira:</p>
                        <p className="font-medium text-gray-900">
                          {new Date(createdTenant.subscription.trialEndsAt).toLocaleDateString('es-PA')}
                        </p>
                      </div>
                    )}
                  </>
                )}
              </div>

              <div className="flex gap-3 justify-center">
                <Button variant="outline" onClick={() => navigate('/system-admin/tenants')}>
                  Ver Todos los Tenants
                </Button>
                <Button onClick={() => navigate(`/system-admin/tenants/${createdTenant.id}`)}>
                  Ver Detalles
                </Button>
              </div>
            </CardBody>
          </Card>
        </div>
      </SystemAdminLayout>
    );
  }

  return (
    <SystemAdminLayout>
      <div className="max-w-2xl mx-auto px-6 py-8">
        {/* Header */}
        <div className="mb-8">
          <button
            onClick={() => navigate('/system-admin/tenants')}
            className="flex items-center gap-2 text-gray-600 hover:text-gray-900 mb-4"
          >
            <ArrowLeft className="w-4 h-4" />
            Volver a Tenants
          </button>
          <h1 className="text-3xl font-bold text-gray-900">Crear Nuevo Tenant</h1>
          <p className="text-gray-600 mt-2">Registrar una nueva empresa en el sistema</p>
        </div>

        <form onSubmit={handleSubmit}>
          <Card>
            <CardHeader>
              <h3 className="font-semibold text-gray-900">Información de la Empresa</h3>
            </CardHeader>
            <CardBody className="space-y-4">
              <Input
                label="Nombre de la Empresa"
                required
                value={formData.name}
                onChange={(e) => handleInputChange('name', e.target.value)}
                error={errors.name}
                placeholder="Ej: Corporación ABC"
              />

              <div className="grid grid-cols-2 gap-4">
                <Input
                  label="RUC"
                  value={formData.ruc}
                  onChange={(e) => handleInputChange('ruc', e.target.value)}
                  placeholder="123456"
                />
                <Input
                  label="DV"
                  value={formData.dv}
                  onChange={(e) => handleInputChange('dv', e.target.value)}
                  placeholder="12"
                />
              </div>
            </CardBody>
          </Card>

          <Card className="mt-6">
            <CardHeader>
              <h3 className="font-semibold text-gray-900">Propietario del Tenant</h3>
            </CardHeader>
            <CardBody className="space-y-4">
              <Input
                label="Nombre Completo"
                value={formData.ownerFullName}
                onChange={(e) => handleInputChange('ownerFullName', e.target.value)}
                placeholder="Nombre completo del propietario"
                helperText="Opcional - si no se proporciona, se usará el nombre de la empresa"
              />

              <Input
                label="Email del Propietario"
                type="email"
                required
                value={formData.ownerEmail}
                onChange={(e) => handleInputChange('ownerEmail', e.target.value)}
                error={errors.ownerEmail}
                placeholder="admin@empresa.com"
                helperText="Esta persona tendrá acceso completo como Owner"
              />

              <Input
                label="Contraseña Inicial"
                type="password"
                required
                value={formData.ownerPassword}
                onChange={(e) => handleInputChange('ownerPassword', e.target.value)}
                error={errors.ownerPassword}
                placeholder="Mínimo 6 caracteres"
                helperText="El propietario podrá cambiarla después"
              />
            </CardBody>
          </Card>

          <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mt-6">
            <p className="text-sm text-blue-800">
              <strong>Nota:</strong> El tenant se creará con plan <strong>Professional</strong> y <strong>14 días de trial</strong> por defecto.
            </p>
          </div>

          <div className="mt-6 flex justify-end gap-3">
            <Button
              variant="outline"
              type="button"
              onClick={() => navigate('/system-admin/tenants')}
            >
              Cancelar
            </Button>
            <Button type="submit" loading={isSubmitting}>
              Crear Tenant
            </Button>
          </div>
        </form>
      </div>
    </SystemAdminLayout>
  );
}

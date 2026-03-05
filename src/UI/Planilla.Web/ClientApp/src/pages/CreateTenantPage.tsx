import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { SystemAdminLayout } from '../components/layout/SystemAdminLayout';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { systemAdminService } from '../services/systemAdminService';
import { ArrowLeft, CheckCircle2, Building2, Globe, MapPin } from 'lucide-react';
import toast from 'react-hot-toast';

export default function CreateTenantPage() {
  const navigate = useNavigate();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);
  const [createdTenant, setCreatedTenant] = useState<any>(null);

  const [formData, setFormData] = useState({
    nombre: '',
    ruc: '',
    dv: '',
    tipoContribuyente: 'Juridico',
    telefono: '',
    correo: '',
    pais: 'Panamá',
    direccion: '',
    sitioWeb: '',
  });

  const [errors, setErrors] = useState<Record<string, string>>({});

  const validateForm = () => {
    const newErrors: Record<string, string> = {};

    if (!formData.nombre.trim()) {
      newErrors.nombre = 'El nombre de la empresa es requerido';
    }

    if (!formData.ruc.trim()) {
      newErrors.ruc = 'El RUC es requerido';
    }

    if (!formData.dv.trim()) {
      newErrors.dv = 'El DV es requerido';
    }

    if (formData.correo && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.correo)) {
      newErrors.correo = 'El email no es válido';
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
      const tenant = await systemAdminService.createTenant(formData as any);
      setCreatedTenant(tenant);
      setShowSuccess(true);
      toast.success('Tenant creado exitosamente');
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Error al crear tenant';
      toast.error(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleInputChange = (field: string, value: any) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
    if (errors[field]) {
      setErrors((prev) => ({ ...prev, [field]: '' }));
    }
  };

  if (showSuccess && createdTenant) {
    return (
      <SystemAdminLayout>
        <div className="max-w-2xl mx-auto px-6 py-8">
          <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20">
            <div className="px-6 py-12 text-center">
              <div className="w-16 h-16 bg-green-500/15 rounded-full flex items-center justify-center mx-auto mb-6">
                <CheckCircle2 className="w-10 h-10 text-green-400" />
              </div>
              <h2 className="text-2xl font-bold text-gray-100 mb-2">Tenant Creado Exitosamente</h2>
              <p className="text-gray-400 mb-4">
                El tenant ha sido creado con plan Free.
              </p>
              <div className="bg-primary-500/15 border border-primary-500/25 rounded-lg p-4 mb-8">
                <p className="text-sm text-primary-300 font-medium">
                  Ahora asigna usuarios a este tenant desde el módulo de Usuarios
                </p>
              </div>

              <div className="bg-navy-800 rounded-lg p-6 text-left mb-8 space-y-3">
                <div>
                  <p className="text-sm text-gray-400">Empresa:</p>
                  <p className="font-medium text-gray-100">{createdTenant.name || formData.nombre}</p>
                </div>
                <div>
                  <p className="text-sm text-gray-400">RUC:</p>
                  <p className="font-medium text-gray-100">{createdTenant.ruc || formData.ruc}-{createdTenant.dv || formData.dv}</p>
                </div>
                <div>
                  <p className="text-sm text-gray-400">Tipo:</p>
                  <p className="font-medium text-gray-100">{createdTenant.tipoContribuyente || formData.tipoContribuyente}</p>
                </div>
                <div>
                  <p className="text-sm text-gray-400">Plan:</p>
                  <p className="font-medium text-gray-100">Free</p>
                </div>
              </div>

              <div className="flex gap-3 justify-center">
                <Button variant="outline" onClick={() => navigate('/system-admin/tenants')}>
                  Ver Todos los Tenants
                </Button>
                <Button onClick={() => navigate('/system-admin/users')}>
                  Asignar Usuarios
                </Button>
              </div>
            </div>
          </div>
        </div>
      </SystemAdminLayout>
    );
  }

  return (
    <SystemAdminLayout>
      <div className="max-w-3xl mx-auto px-6 py-8">
        {/* Header */}
        <div className="mb-8">
          <button
            onClick={() => navigate('/system-admin/tenants')}
            className="flex items-center gap-2 text-gray-400 hover:text-gray-100 mb-4 transition-colors"
          >
            <ArrowLeft className="w-4 h-4" />
            Volver
          </button>
          <h1 className="text-3xl font-bold text-gray-100">Crear Nuevo Tenant</h1>
          <p className="text-gray-400 mt-2">
            Crea un nuevo tenant. Los usuarios se asignan después desde el módulo de Usuarios.
          </p>
        </div>

        <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20">
          <div className="px-6 py-6">
            <form onSubmit={handleSubmit} className="space-y-8">
              {/* Información de la Empresa */}
              <div>
                <div className="flex items-center gap-2 mb-4">
                  <Building2 className="w-5 h-5 text-gray-300" />
                  <h2 className="text-lg font-semibold text-gray-100">Información de la Empresa</h2>
                </div>

                <div className="space-y-4">
                  <Input
                    label="Nombre de la Empresa"
                    value={formData.nombre}
                    onChange={(e) => handleInputChange('nombre', e.target.value)}
                    error={errors.nombre}
                    required
                    placeholder="Empresa ABC S.A."
                  />

                  <div className="grid grid-cols-2 gap-4">
                    <Input
                      label="RUC"
                      value={formData.ruc}
                      onChange={(e) => handleInputChange('ruc', e.target.value)}
                      error={errors.ruc}
                      required
                      placeholder="123456789"
                    />
                    <Input
                      label="DV"
                      value={formData.dv}
                      onChange={(e) => handleInputChange('dv', e.target.value)}
                      error={errors.dv}
                      required
                      maxLength={2}
                      placeholder="12"
                    />
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-gray-300 mb-2">
                      Tipo de Contribuyente <span className="text-red-400">*</span>
                    </label>
                    <div className="flex gap-6">
                      <label className="flex items-center gap-2 cursor-pointer">
                        <input
                          type="radio"
                          name="tipoContribuyente"
                          value="Natural"
                          checked={formData.tipoContribuyente === 'Natural'}
                          onChange={(e) => handleInputChange('tipoContribuyente', e.target.value)}
                          className="w-4 h-4 text-primary-500"
                        />
                        <span className="text-sm text-gray-300">Persona Natural</span>
                      </label>
                      <label className="flex items-center gap-2 cursor-pointer">
                        <input
                          type="radio"
                          name="tipoContribuyente"
                          value="Juridico"
                          checked={formData.tipoContribuyente === 'Juridico'}
                          onChange={(e) => handleInputChange('tipoContribuyente', e.target.value)}
                          className="w-4 h-4 text-primary-500"
                        />
                        <span className="text-sm text-gray-300">Persona Jurídica</span>
                      </label>
                    </div>
                  </div>
                </div>
              </div>

              {/* Información de Contacto */}
              <div>
                <div className="flex items-center gap-2 mb-4">
                  <Globe className="w-5 h-5 text-gray-300" />
                  <h2 className="text-lg font-semibold text-gray-100">Información de Contacto</h2>
                </div>

                <div className="space-y-4">
                  <Input
                    label="Teléfono"
                    value={formData.telefono}
                    onChange={(e) => handleInputChange('telefono', e.target.value)}
                    placeholder="+507 6000-0000"
                  />

                  <Input
                    label="Correo Electrónico"
                    type="email"
                    value={formData.correo}
                    onChange={(e) => handleInputChange('correo', e.target.value)}
                    error={errors.correo}
                    placeholder="contacto@empresa.com"
                  />
                </div>
              </div>

              {/* Ubicación */}
              <div>
                <div className="flex items-center gap-2 mb-4">
                  <MapPin className="w-5 h-5 text-gray-300" />
                  <h2 className="text-lg font-semibold text-gray-100">Ubicación</h2>
                </div>

                <div className="space-y-4">
                  <Input
                    label="País"
                    value={formData.pais}
                    onChange={(e) => handleInputChange('pais', e.target.value)}
                    placeholder="Panamá"
                  />

                  <Input
                    label="Dirección"
                    value={formData.direccion}
                    onChange={(e) => handleInputChange('direccion', e.target.value)}
                    placeholder="Calle 50, Edificio XYZ, Piso 5"
                  />
                </div>
              </div>

              {/* Información Adicional */}
              <div>
                <h2 className="text-lg font-semibold text-gray-100 mb-4">Información Adicional</h2>
                <div className="space-y-4">
                  <Input
                    label="Sitio Web"
                    type="url"
                    value={formData.sitioWeb}
                    onChange={(e) => handleInputChange('sitioWeb', e.target.value)}
                    placeholder="https://www.empresa.com"
                  />
                </div>
              </div>

              {/* Note */}
              <div className="bg-amber-500/15 border border-amber-500/25 rounded-lg p-4">
                <p className="text-sm text-amber-300">
                  <strong>Nota:</strong> El tenant se creará con plan <strong>Free</strong> por defecto.
                  No se creará ningún usuario automáticamente. Deberás asignar usuarios desde el módulo de Usuarios.
                </p>
              </div>

              {/* Actions */}
              <div className="flex gap-3 pt-4">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => navigate('/system-admin/tenants')}
                  disabled={isSubmitting}
                  className="flex-1"
                >
                  Cancelar
                </Button>
                <Button type="submit" disabled={isSubmitting} className="flex-1">
                  {isSubmitting ? 'Creando...' : 'Crear Tenant'}
                </Button>
              </div>
            </form>
          </div>
        </div>
      </div>
    </SystemAdminLayout>
  );
}

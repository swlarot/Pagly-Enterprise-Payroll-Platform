import { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import {
  Key,
  Plus,
  Trash2,
  Copy,
  Check,
  AlertTriangle,
  Loader2,
  BarChart3,
} from 'lucide-react';
import {
  ResponsiveContainer,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip as RechartsTooltip,
  Cell,
} from 'recharts';
import { Card, CardBody, CardHeader } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Badge } from '../components/ui/Badge';
import { Modal } from '../components/ui/Modal';
import { Input } from '../components/ui/Input';
import { Select } from '../components/ui/Select';
import { DataTable, type Column } from '../components/ui/DataTable';
import { apiKeysService } from '../services/apiKeysService';
import type { ApiKeyDto, CreateApiKeyRequest } from '../types/api';

/**
 * Dashboard de API keys para el Owner del tenant.
 *
 * Flujo principal:
 *   1. Owner ve la lista de sus keys con prefix, último uso y totalRequests.
 *   2. "Nueva API key" → modal con form → POST → plaintext se muestra UNA vez
 *      en un modal separado con botón Copy y warning.
 *   3. "Revocar" → confirmación → DELETE → la key queda como Revocada en la lista.
 *
 * Seguridad: el backend enforza Role=Owner y global query filter por tenant.
 * Este componente confía en eso y no replica validación.
 */
export default function ApiKeysPage() {
  const [keys, setKeys] = useState<ApiKeyDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // Create modal state
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [createForm, setCreateForm] = useState<CreateApiKeyRequest>({
    name: '',
    mode: 'Live',
    expiresInDays: null,
  });
  const [isCreating, setIsCreating] = useState(false);

  // Plaintext reveal modal state — se muestra UNA vez tras create exitoso
  const [revealedPlaintext, setRevealedPlaintext] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  // Revoke confirmation state
  const [keyToRevoke, setKeyToRevoke] = useState<ApiKeyDto | null>(null);
  const [isRevoking, setIsRevoking] = useState(false);

  useEffect(() => {
    loadKeys();
  }, []);

  // Top 5 keys por totalRequests, excluyendo keys sin uso. Memoizado para evitar
  // recalcular en cada render cuando otros state cambian (modales, etc).
  const topKeysData = useMemo(() => {
    return keys
      .filter((k) => k.totalRequests > 0)
      .sort((a, b) => b.totalRequests - a.totalRequests)
      .slice(0, 5)
      .map((k) => ({
        // Truncamos el nombre a 18 chars para que el label del eje X no se salga.
        name: k.name.length > 18 ? k.name.slice(0, 17) + '…' : k.name,
        fullName: k.name,
        requests: k.totalRequests,
        mode: k.mode,
        prefix: k.keyPrefix,
      }));
  }, [keys]);

  const totalRequestsAllKeys = useMemo(
    () => keys.reduce((sum, k) => sum + k.totalRequests, 0),
    [keys]
  );

  const loadKeys = async () => {
    try {
      setIsLoading(true);
      const data = await apiKeysService.list();
      setKeys(data);
    } catch (error: any) {
      toast.error(error?.message || 'Error al cargar las API keys');
    } finally {
      setIsLoading(false);
    }
  };

  const handleCreate = async () => {
    if (!createForm.name.trim()) {
      toast.error('El nombre de la key es obligatorio');
      return;
    }

    try {
      setIsCreating(true);
      const response = await apiKeysService.create({
        name: createForm.name.trim(),
        mode: createForm.mode,
        expiresInDays: createForm.expiresInDays ?? null,
      });
      setIsCreateModalOpen(false);
      setCreateForm({ name: '', mode: 'Live', expiresInDays: null });
      setRevealedPlaintext(response.plaintextKey);
      await loadKeys();
      toast.success('API key creada correctamente');
    } catch (error: any) {
      toast.error(error?.message || 'Error al crear la API key');
    } finally {
      setIsCreating(false);
    }
  };

  const handleCopyPlaintext = async () => {
    if (!revealedPlaintext) return;
    try {
      await navigator.clipboard.writeText(revealedPlaintext);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      toast.error('No se pudo copiar. Selecciona manualmente y copia con Ctrl+C.');
    }
  };

  const handleClosePlaintextModal = () => {
    setRevealedPlaintext(null);
    setCopied(false);
  };

  const handleRevoke = async () => {
    if (!keyToRevoke) return;
    try {
      setIsRevoking(true);
      await apiKeysService.revoke(keyToRevoke.id);
      toast.success(`Key "${keyToRevoke.name}" revocada`);
      setKeyToRevoke(null);
      await loadKeys();
    } catch (error: any) {
      toast.error(error?.message || 'Error al revocar la key');
    } finally {
      setIsRevoking(false);
    }
  };

  const formatDate = (value?: string | null): string => {
    if (!value) return '—';
    try {
      const d = new Date(value);
      return d.toLocaleString('es-PA', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return value;
    }
  };

  const renderStatusBadge = (key: ApiKeyDto) => {
    if (key.revokedAt) return <Badge variant="danger">Revocada</Badge>;
    if (key.expiresAt && new Date(key.expiresAt) < new Date()) {
      return <Badge variant="warning">Expirada</Badge>;
    }
    if (!key.isActive) return <Badge variant="default">Inactiva</Badge>;
    return <Badge variant="success">Activa</Badge>;
  };

  const columns: Column<ApiKeyDto>[] = [
    {
      key: 'name',
      header: 'Nombre',
      render: (k) => (
        <div>
          <div className="font-medium text-gray-100">{k.name}</div>
          <div className="text-xs text-gray-500 font-mono">
            pk_{k.mode.toLowerCase()}_{k.keyPrefix}••••
          </div>
        </div>
      ),
    },
    {
      key: 'mode',
      header: 'Modo',
      render: (k) => (
        <Badge variant={k.mode === 'Live' ? 'info' : 'default'}>{k.mode}</Badge>
      ),
    },
    {
      key: 'totalRequests',
      header: 'Requests',
      render: (k) => (
        <span className="tabular-nums">{k.totalRequests.toLocaleString('es-PA')}</span>
      ),
    },
    {
      key: 'lastUsedAt',
      header: 'Último uso',
      render: (k) => <span className="text-sm text-gray-400">{formatDate(k.lastUsedAt)}</span>,
    },
    {
      key: 'status',
      header: 'Estado',
      render: renderStatusBadge,
    },
    {
      key: 'actions',
      header: '',
      className: 'text-right',
      render: (k) => {
        if (k.revokedAt) return null;
        return (
          <button
            onClick={() => setKeyToRevoke(k)}
            className="inline-flex items-center gap-1 text-sm text-red-400 hover:text-red-300 transition-colors"
            title="Revocar key"
          >
            <Trash2 className="w-4 h-4" />
            Revocar
          </button>
        );
      },
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-100 flex items-center gap-3">
            <Key className="w-8 h-8 text-violet-400" />
            API Keys
          </h1>
          <p className="mt-2 text-sm text-gray-400">
            Gestiona las keys que tus clientes usan para consumir{' '}
            <span className="font-mono text-gray-300">POST /v1/payroll/calculate</span>.
          </p>
        </div>
        <Button onClick={() => setIsCreateModalOpen(true)}>
          <Plus className="w-4 h-4 mr-2" />
          Nueva API key
        </Button>
      </div>

      {/* Usage chart — solo se muestra si ya tienes al menos 1 key con uso.
          Si todas las keys están en 0, lo ocultamos — ver un chart vacío es ruido
          informativo y no agrega valor. */}
      {!isLoading && topKeysData.length > 0 && (
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <BarChart3 className="w-5 h-5 text-violet-400" />
                <h2 className="text-lg font-semibold text-gray-100">Uso por API key</h2>
              </div>
              <div className="text-sm text-gray-400">
                <span className="tabular-nums font-semibold text-gray-100">
                  {totalRequestsAllKeys.toLocaleString('es-PA')}
                </span>
                {' '}requests acumulados
              </div>
            </div>
          </CardHeader>
          <CardBody>
            <div className="h-64 w-full">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart
                  data={topKeysData}
                  margin={{ top: 8, right: 16, bottom: 8, left: 0 }}
                >
                  <CartesianGrid
                    strokeDasharray="3 3"
                    stroke="#1e293b"
                    vertical={false}
                  />
                  <XAxis
                    dataKey="name"
                    tick={{ fill: '#94a3b8', fontSize: 12 }}
                    axisLine={{ stroke: '#334155' }}
                    tickLine={false}
                  />
                  <YAxis
                    tick={{ fill: '#94a3b8', fontSize: 12 }}
                    axisLine={{ stroke: '#334155' }}
                    tickLine={false}
                    tickFormatter={(value: number) =>
                      value >= 1000 ? `${(value / 1000).toFixed(1)}k` : value.toString()
                    }
                  />
                  <RechartsTooltip
                    content={<TopKeysTooltip />}
                    cursor={{ fill: 'rgba(139, 92, 246, 0.08)' }}
                  />
                  <Bar dataKey="requests" radius={[6, 6, 0, 0]}>
                    {topKeysData.map((entry, index) => (
                      <Cell
                        key={entry.prefix}
                        fill={entry.mode === 'Live' ? '#8b5cf6' : '#64748b'}
                        opacity={1 - index * 0.12}
                      />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
            <p className="mt-3 text-xs text-gray-500">
              Top 5 keys por uso. Las barras violetas son Live; las grises, Test.
              El contador se incrementa en cada request 2xx al endpoint{' '}
              <span className="font-mono text-gray-400">/v1/payroll/calculate</span>.
            </p>
          </CardBody>
        </Card>
      )}

      <Card>
        <CardHeader>
          <h2 className="text-lg font-semibold text-gray-100">Tus API keys</h2>
        </CardHeader>
        <CardBody className="p-0">
          <DataTable
            columns={columns}
            data={keys}
            isLoading={isLoading}
            emptyMessage="No tienes API keys todavía. Crea una para empezar a integrar clientes."
            keyExtractor={(k) => k.id}
          />
        </CardBody>
      </Card>

      {/* Modal Crear */}
      <Modal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        title="Crear nueva API key"
      >
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-1">
              Nombre <span className="text-red-400">*</span>
            </label>
            <Input
              value={createForm.name}
              onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })}
              placeholder="ej: Urbis Production, Dev Laptop Juan"
              maxLength={100}
            />
            <p className="mt-1 text-xs text-gray-500">
              Un nombre descriptivo para identificar la key en esta lista. No afecta la seguridad.
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-300 mb-1">Modo</label>
            <Select
              value={createForm.mode}
              onChange={(e) =>
                setCreateForm({ ...createForm, mode: e.target.value as 'Live' | 'Test' })
              }
              options={[
                { value: 'Live', label: 'Live (producción)' },
                { value: 'Test', label: 'Test (sandbox)' },
              ]}
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-300 mb-1">
              Expira en (días, opcional)
            </label>
            <Input
              type="number"
              min={1}
              max={3650}
              value={createForm.expiresInDays ?? ''}
              onChange={(e) =>
                setCreateForm({
                  ...createForm,
                  expiresInDays: e.target.value ? Number(e.target.value) : null,
                })
              }
              placeholder="Sin expiración"
            />
            <p className="mt-1 text-xs text-gray-500">
              Dejar vacío si la key no debe expirar. Máximo 3650 (10 años).
            </p>
          </div>

          <div className="flex justify-end gap-2 pt-4">
            <Button variant="secondary" onClick={() => setIsCreateModalOpen(false)}>
              Cancelar
            </Button>
            <Button onClick={handleCreate} disabled={isCreating}>
              {isCreating && <Loader2 className="w-4 h-4 mr-2 animate-spin" />}
              Crear
            </Button>
          </div>
        </div>
      </Modal>

      {/* Modal Plaintext Reveal (one-time) */}
      <Modal
        isOpen={revealedPlaintext !== null}
        onClose={handleClosePlaintextModal}
        title="Tu nueva API key"
        size="lg"
      >
        <div className="space-y-4">
          <div className="flex items-start gap-3 p-4 bg-amber-500/10 border border-amber-500/30 rounded-lg">
            <AlertTriangle className="w-5 h-5 text-amber-400 flex-shrink-0 mt-0.5" />
            <div className="text-sm text-amber-200">
              <p className="font-semibold mb-1">Guarda esta key ahora</p>
              <p>
                Es la única vez que verás el secret completo. Si lo pierdes, no hay manera
                de recuperarlo — solo puedes revocar esta key y crear una nueva.
              </p>
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-300 mb-2">
              API key (copiar y guardar)
            </label>
            <div className="flex items-center gap-2 p-3 bg-navy-950 border border-navy-700 rounded-lg">
              <code className="flex-1 text-xs text-gray-100 font-mono break-all select-all">
                {revealedPlaintext}
              </code>
              <button
                onClick={handleCopyPlaintext}
                className="flex items-center gap-1 px-3 py-1.5 bg-violet-600 hover:bg-violet-500 text-white text-sm rounded-md transition-colors flex-shrink-0"
              >
                {copied ? (
                  <>
                    <Check className="w-4 h-4" />
                    Copiado
                  </>
                ) : (
                  <>
                    <Copy className="w-4 h-4" />
                    Copiar
                  </>
                )}
              </button>
            </div>
          </div>

          <div className="text-sm text-gray-400">
            <p className="font-medium mb-1">Uso:</p>
            <pre className="p-3 bg-navy-950 border border-navy-700 rounded-lg text-xs font-mono text-gray-300 overflow-x-auto">
{`curl -X POST https://tu-dominio.com/v1/payroll/calculate \\
  -H "X-Api-Key: ${revealedPlaintext}" \\
  -H "Content-Type: application/json" \\
  -d '{"grossPay": 2000, "payFrequency": "Mensual", ...}'`}
            </pre>
          </div>

          <div className="flex justify-end pt-2">
            <Button onClick={handleClosePlaintextModal}>Entendido, ya la guardé</Button>
          </div>
        </div>
      </Modal>

      {/* Modal Revoke Confirmation */}
      <Modal
        isOpen={keyToRevoke !== null}
        onClose={() => setKeyToRevoke(null)}
        title="Revocar API key"
      >
        <div className="space-y-4">
          <p className="text-gray-300">
            Estás a punto de revocar la key <span className="font-semibold text-gray-100">"{keyToRevoke?.name}"</span>.
          </p>
          <p className="text-sm text-gray-400">
            Después de revocar, cualquier request que use esta key recibirá un 401 Unauthorized.
            Los clientes que estén usándola dejarán de funcionar inmediatamente.
          </p>
          <p className="text-sm text-gray-400">
            Esta acción no se puede deshacer. Si necesitas una nueva key, genera una
            después de revocar.
          </p>

          <div className="flex justify-end gap-2 pt-4">
            <Button variant="secondary" onClick={() => setKeyToRevoke(null)} disabled={isRevoking}>
              Cancelar
            </Button>
            <Button
              onClick={handleRevoke}
              disabled={isRevoking}
              className="bg-red-600 hover:bg-red-500"
            >
              {isRevoking && <Loader2 className="w-4 h-4 mr-2 animate-spin" />}
              Revocar key
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}

// ============================================================================
// Custom tooltip para el BarChart de Recharts
// ============================================================================

interface TopKeysTooltipProps {
  active?: boolean;
  payload?: Array<{
    payload: {
      fullName: string;
      prefix: string;
      mode: 'Test' | 'Live';
      requests: number;
    };
  }>;
}

function TopKeysTooltip({ active, payload }: TopKeysTooltipProps) {
  if (!active || !payload || payload.length === 0) return null;
  const data = payload[0].payload;

  return (
    <div className="rounded-lg border border-navy-700 bg-navy-900/95 backdrop-blur-sm p-3 shadow-xl">
      <div className="font-semibold text-gray-100 text-sm mb-0.5">{data.fullName}</div>
      <div className="text-xs text-gray-500 font-mono mb-2">
        pk_{data.mode.toLowerCase()}_{data.prefix}••••
      </div>
      <div className="flex items-center gap-2">
        <span className="text-xs text-gray-400">Requests:</span>
        <span className="text-sm font-semibold tabular-nums text-violet-300">
          {data.requests.toLocaleString('es-PA')}
        </span>
      </div>
    </div>
  );
}

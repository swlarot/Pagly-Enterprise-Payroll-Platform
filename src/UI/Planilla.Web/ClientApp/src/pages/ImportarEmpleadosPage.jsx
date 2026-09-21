import React, { useCallback, useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Download, Upload, CheckCircle2, AlertTriangle, AlertCircle, Loader2, ArrowLeft, FileSpreadsheet, ChevronRight } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import { validarFila, cedulasRepetidas, normalizarCedula, tieneErrores, tieneAvisos } from '../utils/validacionImportacion';
import PanelEmpleadoImportado from '../components/importacion/PanelEmpleadoImportado';

// ============================================================
// Importar empleados (onboarding)
//
// 1. Descargar plantilla → 2. Subir → 3. Revisar y corregir → 4. Confirmar → 5. Resultado
//
// Sitio donde va: manda la tabla de revisión con el semáforo por empleado.
// Nada se guarda hasta el paso 4: se puede cerrar la pestaña sin consecuencias.
// Estados: subiendo (barra verde con el % real), validando (esqueleto),
// fallo de archivo (con palabras del producto), confirmando (una sola pieza
// en pantalla, lo demás se retira).
// ============================================================

const PASOS = ['Plantilla', 'Subir', 'Revisar', 'Confirmar'];

function Pasos({ actual }) {
  return (
    <ol className="flex items-center gap-2 text-sm">
      {PASOS.map((p, i) => {
        const n = i + 1;
        const activo = n === actual;
        const hecho = n < actual;
        return (
          <li key={p} className="flex items-center gap-2">
            <span className={`w-6 h-6 rounded-full flex items-center justify-center text-xs font-semibold ${
              activo ? 'bg-primary-600 text-white' : hecho ? 'bg-primary-900/60 text-primary-300' : 'bg-slate-700 text-gray-400'
            }`}>{hecho ? '✓' : n}</span>
            <span className={activo ? 'text-white font-medium' : 'text-gray-400'}>{p}</span>
            {i < PASOS.length - 1 && <ChevronRight className="w-4 h-4 text-gray-600" />}
          </li>
        );
      })}
    </ol>
  );
}

// El servidor manda las fechas como ISO; la pantalla trabaja con yyyy-MM-dd.
const aFilaLocal = (f) => ({
  ...f.datos,
  fechaContratacion: f.datos.fechaContratacion ? String(f.datos.fechaContratacion).slice(0, 10) : null,
  tipoContrato: f.datos.tipoContrato ?? 'Indefinido',
});

export default function ImportarEmpleadosPage() {
  const navigate = useNavigate();
  const inputRef = useRef(null);

  const [paso, setPaso] = useState(1);
  const [subiendo, setSubiendo] = useState(false);
  const [progreso, setProgreso] = useState(0);
  const [validando, setValidando] = useState(false);
  const [problemasArchivo, setProblemasArchivo] = useState([]);
  const [nombreArchivo, setNombreArchivo] = useState('');

  const [filas, setFilas] = useState([]);           // FilaImportacion[]
  const [omitidas, setOmitidas] = useState(new Set());
  const [seleccion, setSeleccion] = useState(null);  // índice
  const [filtro, setFiltro] = useState('todas');

  const [confirmando, setConfirmando] = useState(false);
  const [resumen, setResumen] = useState(null);

  const hoy = useMemo(() => new Date(), []);

  // Validación en tiempo real: cada fila con sus problemas, recalculada al cambiar.
  const repetidas = useMemo(() => cedulasRepetidas(filas), [filas]);
  const problemasPorFila = useMemo(() => filas.map(f => validarFila(f, repetidas, hoy)), [filas, repetidas, hoy]);

  const conteo = useMemo(() => {
    let listos = 0, avisos = 0, errores = 0, omit = 0;
    filas.forEach((f, i) => {
      if (omitidas.has(normalizarCedula(f.cedula) || `#${i}`)) { omit++; return; }
      const p = problemasPorFila[i];
      if (tieneErrores(p)) errores++;
      else if (tieneAvisos(p)) avisos++;
      else listos++;
    });
    return { listos, avisos, errores, omit };
  }, [filas, problemasPorFila, omitidas]);

  const claveOmision = (f, i) => normalizarCedula(f.cedula) || `#${i}`;

  const descargarPlantilla = async () => {
    try {
      await api.download('/api/importacion/plantilla', `Plantilla empleados Pagly.xlsx`);
      setPaso(2);
    } catch (e) {
      toast.error(e.message || 'No se pudo descargar la plantilla');
    }
  };

  const subir = useCallback(async (file) => {
    if (!file) return;
    if (!file.name.toLowerCase().endsWith('.xlsx')) {
      toast.error('El archivo debe ser un Excel .xlsx. Si es .xls, ábrelo y guárdalo como .xlsx.');
      return;
    }
    setSubiendo(true); setProgreso(0); setProblemasArchivo([]); setNombreArchivo(file.name);
    try {
      const r = await api.upload('/api/importacion/empleados/validar', file, 'archivo', (pct) => {
        setProgreso(pct);
        if (pct >= 100) { setSubiendo(false); setValidando(true); }
      });
      setValidando(false); setSubiendo(false);
      setProblemasArchivo(r.problemasDelArchivo ?? []);
      const nuevas = (r.filas ?? []).map(aFilaLocal);
      setFilas(nuevas);
      setOmitidas(new Set());
      setSeleccion(null);
      if (nuevas.length === 0 && (r.problemasDelArchivo ?? []).length === 0) {
        toast.error('El archivo no trae empleados. Llena la hoja Empleados de la plantilla.');
        return;
      }
      if (nuevas.length > 0) setPaso(3);
    } catch (e) {
      setSubiendo(false); setValidando(false);
      toast.error(e.message || 'No se pudo leer el archivo');
    }
  }, []);

  const onDrop = (e) => {
    e.preventDefault();
    const file = e.dataTransfer?.files?.[0];
    subir(file);
  };

  const actualizarFila = (i, nueva) => setFilas(prev => prev.map((f, j) => (j === i ? nueva : f)));
  const alternarOmitir = (i) => {
    const k = claveOmision(filas[i], i);
    setOmitidas(prev => { const n = new Set(prev); n.has(k) ? n.delete(k) : n.add(k); return n; });
  };

  const puedeConfirmar = filas.length > 0 && conteo.errores === 0 && (conteo.listos + conteo.avisos) > 0;

  const confirmar = async () => {
    if (!puedeConfirmar) return;
    setConfirmando(true);
    try {
      const r = await api.post('/api/importacion/empleados/confirmar', {
        filas: filas.filter((f, i) => !omitidas.has(claveOmision(f, i))),
        omitidas: [...omitidas].filter(k => !k.startsWith('#')),
        nombreArchivo,
      });
      setResumen(r);
      setPaso(5);
      toast.success(`Importados: ${r.creados} nuevos, ${r.actualizados} actualizados`);
    } catch (e) {
      toast.error(e.message || 'No se pudo importar');
    } finally {
      setConfirmando(false);
    }
  };

  const filasVisibles = filas
    .map((f, i) => ({ f, i, p: problemasPorFila[i], omitida: omitidas.has(claveOmision(f, i)) }))
    .filter(x => {
      if (filtro === 'todas') return true;
      if (filtro === 'omitidas') return x.omitida;
      if (x.omitida) return false;
      if (filtro === 'errores') return tieneErrores(x.p);
      if (filtro === 'avisos') return !tieneErrores(x.p) && tieneAvisos(x.p);
      if (filtro === 'listos') return !tieneErrores(x.p) && !tieneAvisos(x.p);
      return true;
    });

  // ── Resultado ──
  if (paso === 5 && resumen) {
    return (
      <div className="space-y-6 max-w-3xl">
        <div className="flex items-center gap-3">
          <CheckCircle2 className="w-8 h-8 text-primary-400" />
          <div>
            <h1 className="text-2xl font-bold text-white">Importación completada</h1>
            <p className="text-gray-400 text-sm">{resumen.creados} empleados nuevos · {resumen.actualizados} actualizados · {resumen.omitidos} omitidos · {resumen.mesesGuardados} meses de salario · {resumen.saldosGuardados} saldos de renta</p>
          </div>
        </div>
        <div className="bg-slate-800 border border-slate-700 rounded-xl divide-y divide-slate-700">
          {resumen.empleados.map(e => (
            <div key={e.id} className="flex items-center justify-between px-4 py-2.5 text-sm">
              <span className="text-gray-100">{e.nombreCompleto} <span className="text-gray-500 font-mono ml-2">{e.cedula}</span></span>
              <span className={`text-xs px-2 py-0.5 rounded ${e.creado ? 'bg-primary-900/60 text-primary-300' : 'bg-blue-900/60 text-blue-200'}`}>{e.creado ? 'nuevo' : 'actualizado'}</span>
            </div>
          ))}
        </div>
        <div className="flex gap-3">
          <Link to="/empleados" className="px-4 py-2 bg-primary-600 hover:bg-primary-700 text-white rounded-lg text-sm font-medium">Ir a Empleados</Link>
          <button onClick={() => { setPaso(1); setFilas([]); setResumen(null); }} className="px-4 py-2 bg-slate-700 hover:bg-slate-600 text-gray-100 rounded-lg text-sm">Importar otro archivo</button>
        </div>
      </div>
    );
  }

  const filaSel = seleccion != null ? filas[seleccion] : null;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <button onClick={() => navigate('/empleados')} className="text-sm text-gray-400 hover:text-gray-200 flex items-center gap-1 mb-2">
            <ArrowLeft className="w-4 h-4" /> Empleados
          </button>
          <h1 className="text-3xl font-bold text-white">Importar empleados</h1>
          <p className="text-gray-400 mt-1">Datos, hasta 60 meses de salarios y saldos de renta, desde una sola plantilla</p>
        </div>
        <Pasos actual={Math.min(paso, 4)} />
      </div>

      {/* Paso 1 y 2: plantilla y subida */}
      {paso <= 2 && (
        <div className="grid gap-4 md:grid-cols-2">
          <div className="bg-slate-800 border border-slate-700 rounded-xl p-5 space-y-3">
            <div className="flex items-center gap-2 text-white font-semibold"><FileSpreadsheet className="w-5 h-5 text-gray-400" /> 1. Descarga la plantilla</div>
            <p className="text-sm text-gray-400">Tres hojas: <span className="text-gray-200">Empleados</span>, <span className="text-gray-200">Salarios</span> (una columna por mes, los últimos 60) y <span className="text-gray-200">Saldos renta</span>. La llenas y la subes aquí mismo.</p>
            <button onClick={descargarPlantilla} className="flex items-center gap-2 px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg text-sm font-medium">
              <Download className="w-4 h-4" /> Descargar plantilla
            </button>
          </div>

          <div
            onDragOver={e => e.preventDefault()}
            onDrop={onDrop}
            className={`bg-slate-800 border-2 border-dashed rounded-xl p-5 flex flex-col items-center justify-center text-center gap-3 ${
              paso === 2 ? 'border-primary-500/60' : 'border-slate-700'
            }`}
          >
            <Upload className="w-8 h-8 text-gray-400" />
            <div className="text-white font-semibold">2. Sube la plantilla llena</div>
            <p className="text-sm text-gray-400">Arrastra el .xlsx aquí o elígelo. No se guarda nada hasta que confirmes.</p>
            <input ref={inputRef} type="file" accept=".xlsx" className="hidden" onChange={e => subir(e.target.files?.[0])} />
            <button
              onClick={() => inputRef.current?.click()}
              disabled={subiendo || validando}
              className="flex items-center gap-2 px-4 py-2 bg-primary-600 hover:bg-primary-700 disabled:opacity-60 text-white rounded-lg text-sm font-medium"
            >
              {subiendo || validando ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload className="w-4 h-4" />}
              {subiendo ? `Subiendo ${progreso}%` : validando ? 'Revisando el archivo…' : 'Elegir archivo'}
            </button>
            {(subiendo || validando) && (
              <div className="w-full h-1.5 bg-slate-700 rounded overflow-hidden" role="progressbar" aria-valuenow={subiendo ? progreso : 100} aria-valuemin={0} aria-valuemax={100}>
                <div className={`h-full bg-primary-500 transition-all ${validando ? 'animate-pulse' : ''}`} style={{ width: `${subiendo ? progreso : 100}%` }} />
              </div>
            )}
          </div>
        </div>
      )}

      {problemasArchivo.length > 0 && (
        <div className="bg-amber-950/40 border border-amber-800 rounded-xl p-4 text-sm text-amber-200 space-y-1">
          {problemasArchivo.map((m, i) => <p key={i} className="flex items-start gap-2"><AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />{m}</p>)}
        </div>
      )}

      {/* Paso 3: revisar */}
      {paso >= 3 && filas.length > 0 && (
        <>
          <div className="flex items-center justify-between gap-3 flex-wrap">
            <div className="flex items-center gap-2 text-sm">
              {[
                ['todas', `${filas.length} en el archivo`, 'text-gray-200'],
                ['listos', `${conteo.listos} listos`, 'text-primary-300'],
                ['avisos', `${conteo.avisos} con avisos`, 'text-amber-300'],
                ['errores', `${conteo.errores} con errores`, 'text-red-300'],
                ['omitidas', `${conteo.omit} omitidos`, 'text-gray-400'],
              ].map(([k, label, cls]) => (
                <button
                  key={k}
                  onClick={() => setFiltro(k)}
                  className={`px-3 py-1 rounded-full border ${filtro === k ? 'border-primary-500 bg-primary-900/30' : 'border-slate-700 bg-slate-800'} ${cls}`}
                >
                  {label}
                </button>
              ))}
            </div>
            <div className="flex items-center gap-2">
              <button onClick={() => { setPaso(2); }} className="px-3 py-2 text-sm text-gray-300 hover:text-white">Subir otro archivo</button>
              <button
                onClick={confirmar}
                disabled={!puedeConfirmar || confirmando}
                title={conteo.errores > 0 ? 'Corrige o omite los empleados con errores' : ''}
                className="flex items-center gap-2 px-4 py-2 bg-primary-600 hover:bg-primary-700 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-lg text-sm font-medium"
              >
                {confirmando ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle2 className="w-4 h-4" />}
                Importar {conteo.listos + conteo.avisos} empleados
              </button>
            </div>
          </div>

          {conteo.errores > 0 && (
            <p className="text-sm text-red-300 flex items-center gap-2"><AlertCircle className="w-4 h-4" /> Hay {conteo.errores} empleado(s) con errores. Corrígelos haciendo clic en su fila, u omítelos, para poder importar.</p>
          )}

          <div className={`bg-slate-800 border border-slate-700 rounded-xl overflow-x-auto ${seleccion != null ? 'opacity-40' : ''}`}>
            <table className="w-full text-sm min-w-[820px]">
              <thead>
                <tr className="bg-slate-700/50 text-gray-400 text-left text-xs">
                  <th className="px-3 py-2.5 w-10">Fila</th>
                  <th className="px-3 py-2.5 w-10"></th>
                  <th className="px-3 py-2.5">Cédula</th>
                  <th className="px-3 py-2.5">Empleado</th>
                  <th className="px-3 py-2.5 text-right">Salario base</th>
                  <th className="px-3 py-2.5">Contratación</th>
                  <th className="px-3 py-2.5 text-right">Meses con dato</th>
                  <th className="px-3 py-2.5">Qué revisar</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-700">
                {filasVisibles.length === 0 && (
                  <tr><td colSpan={8} className="px-3 py-8 text-center text-gray-400">Nada que mostrar con este filtro</td></tr>
                )}
                {filasVisibles.map(({ f, i, p, omitida }) => {
                  const err = tieneErrores(p), av = tieneAvisos(p);
                  const icono = omitida ? <span className="text-gray-500 text-xs">—</span>
                    : err ? <AlertCircle className="w-4 h-4 text-red-400" />
                    : av ? <AlertTriangle className="w-4 h-4 text-amber-400" />
                    : <CheckCircle2 className="w-4 h-4 text-primary-400" />;
                  const primero = p.find(x => x.tipo === 'Error') ?? p[0];
                  return (
                    <tr
                      key={i}
                      onClick={() => setSeleccion(i)}
                      className={`cursor-pointer hover:bg-slate-700/30 ${omitida ? 'text-gray-500 line-through' : 'text-gray-100'}`}
                    >
                      <td className="px-3 py-2 text-gray-500 font-mono text-xs">{f.fila}</td>
                      <td className="px-3 py-2">{icono}</td>
                      <td className="px-3 py-2 font-mono">{f.cedula || <span className="text-red-400">falta</span>}</td>
                      <td className="px-3 py-2">
                        {`${f.nombre} ${f.apellido}`.trim() || <span className="text-red-400">sin nombre</span>}
                        {f.yaExiste && <span className="ml-2 text-[10px] px-1.5 py-0.5 rounded bg-blue-900/60 text-blue-200 no-underline">actualiza</span>}
                      </td>
                      <td className="px-3 py-2 text-right font-mono">{f.salarioBase != null ? Number(f.salarioBase).toLocaleString('es-PA', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '—'}</td>
                      <td className="px-3 py-2 text-gray-300">{f.fechaContratacion ? f.fechaContratacion.split('-').reverse().join('/') : '—'}</td>
                      <td className="px-3 py-2 text-right font-mono text-gray-300">{f.meses.filter(m => m.monto != null).length}</td>
                      <td className="px-3 py-2 text-xs max-w-[360px] truncate" title={p.map(x => x.mensaje).join('\n')}>
                        {omitida ? <span className="text-gray-500">Omitido</span>
                          : primero ? <span className={primero.tipo === 'Error' ? 'text-red-300' : 'text-amber-300'}>{primero.mensaje}{p.length > 1 ? ` (+${p.length - 1})` : ''}</span>
                          : <span className="text-gray-500">—</span>}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </>
      )}

      {filaSel && (
        <PanelEmpleadoImportado
          fila={filaSel}
          problemas={problemasPorFila[seleccion]}
          omitida={omitidas.has(claveOmision(filaSel, seleccion))}
          onChange={(nueva) => actualizarFila(seleccion, nueva)}
          onOmitir={() => alternarOmitir(seleccion)}
          onCerrar={() => setSeleccion(null)}
        />
      )}
    </div>
  );
}

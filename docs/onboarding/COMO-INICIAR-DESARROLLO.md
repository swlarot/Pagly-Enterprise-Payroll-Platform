# 🚀 Cómo Iniciar Planilla en Modo Desarrollo

## 📋 Resumen Rápido

Planilla requiere **DOS servidores corriendo simultáneamente**:

```
┌─────────────────────────────────────────────────────────┐
│                    TU NAVEGADOR                         │
│              http://localhost:5173                      │
└─────────────────────┬───────────────────────────────────┘
                      │
                      ↓
┌─────────────────────────────────────────────────────────┐
│              FRONTEND (React + Vite)                    │
│              Puerto: 5173                               │
│              Hot Module Replacement: ✓                  │
└─────────────────────┬───────────────────────────────────┘
                      │
                      │ HTTP Requests
                      ↓
┌─────────────────────────────────────────────────────────┐
│              BACKEND (.NET Web API)                     │
│              Puerto: 5039                               │
│              Swagger: /swagger                          │
└─────────────────────────────────────────────────────────┘
```

## ✅ Método 1: Script Automático (RECOMENDADO)

### Paso 1: Ejecutar el script
```powershell
.\iniciar-desarrollo.ps1
```

### Paso 2: Esperar
- Se abrirán **3 ventanas de PowerShell**:
  - Ventana 1: Backend (puerto 5039)
  - Ventana 2: Frontend (puerto 5173)
  - Ventana 3: Esta ventana (información)

### Paso 3: El navegador se abrirá automáticamente
- URL: `http://localhost:5173`
- Login: `admin@sistema.com` / `Admin123!`

### Para detener:
```powershell
.\detener-desarrollo.ps1
```

## 🔧 Método 2: Inicio Manual

### Terminal 1 - Backend
```powershell
# Opción A: Visual Studio
# 1. Abrir Planilla.sln en Visual Studio
# 2. Presionar F5 (Start Debugging)
# 3. Verificar en la consola que inicia en puerto 5039

# Opción B: Línea de comandos
cd src/UI/Planilla.Web
dotnet run
```

### Terminal 2 - Frontend (NUEVA TERMINAL)
```powershell
cd src/UI/Planilla.Web/ClientApp
npm run dev
```

### Abrir Navegador
```
http://localhost:5173
```

## 🐛 Solución de Problemas

### ❌ "El login no funciona"

**Causa**: Solo tienes el backend corriendo, falta el frontend.

**Solución**:
1. Verifica que ambos servidores estén corriendo:
   ```powershell
   .\verificar-puertos.ps1
   ```
2. Debe mostrar:
   - ✅ Backend - Puerto 5039: ACTIVO
   - ✅ Frontend - Puerto 5173: ACTIVO

### ❌ "Visual Studio cambia de puerto"

**Causa**: El puerto 5039 está ocupado por otro proceso.

**Solución**:
```powershell
# Ver qué proceso usa el puerto 5039
Get-NetTCPConnection -LocalPort 5039 | Select-Object OwningProcess

# Detener procesos antiguos
.\detener-desarrollo.ps1

# Reiniciar
.\iniciar-desarrollo.ps1
```

### ❌ "Veo una página en blanco en localhost:5173"

**Causa**: El frontend no inició correctamente.

**Solución**:
```powershell
cd src/UI/Planilla.Web/ClientApp

# Reinstalar dependencias
npm install

# Iniciar de nuevo
npm run dev
```

### ❌ "Error de CORS en la consola del navegador"

**Causa**: El frontend no está en el puerto 5173.

**Solución**: Asegúrate de abrir `http://localhost:5173` (NO 5039)

## 📊 Verificar Estado

```powershell
# Ver estado de los servidores
.\verificar-puertos.ps1
```

Debe mostrar:
```
🔧 Backend (.NET) - Puerto 5039:
   ✅ ACTIVO - Proceso: dotnet (PID: 12345)
⚛️  Frontend (Vite) - Puerto 5173:
   ✅ ACTIVO - Proceso: node (PID: 67890)

✅ Todos los servidores están corriendo correctamente
   🌐 Abre: http://localhost:5173
```

## 🎯 Flujo de Desarrollo Típico

1. **Mañana (inicio del día)**:
   ```powershell
   .\iniciar-desarrollo.ps1
   ```

2. **Durante el día**:
   - Editas archivos `.tsx`, `.ts`, `.jsx` en `src/UI/Planilla.Web/ClientApp/src/`
   - Los cambios se reflejan **instantáneamente** en el navegador (HMR)
   - Editas archivos `.cs` en `src/`
   - Visual Studio recarga automáticamente

3. **Tarde (fin del día)**:
   ```powershell
   .\detener-desarrollo.ps1
   ```

## 🔐 Credenciales de Prueba

### Usuario Administrador del Sistema
```
Email: admin@sistema.com
Password: Admin123!
Rol: System Admin
```

### Usuario Tenant Demo (si existe)
```
Email: demo@empresa.com
Password: Demo123!
Rol: Owner/Admin
```

## 📝 Notas Importantes

1. **NO uses el puerto 5039 en el navegador** - ese es solo para API
2. **SIEMPRE abre localhost:5173** - ese es el frontend con HMR
3. Si modificas archivos en `ClientApp/src/`, verás cambios instantáneos
4. Si modificas C#, necesitas que Visual Studio recompile (automático)
5. Los archivos en `wwwroot/` son solo para producción (ignóralos en desarrollo)

## 🚢 Para Producción (Deploy)

```powershell
# 1. Compilar frontend
cd src/UI/Planilla.Web/ClientApp
npm run build

# 2. Los archivos se copian a ../wwwroot/

# 3. Publicar aplicación .NET
cd ../../..
dotnet publish -c Release
```

En producción, un solo servidor (5039) sirve tanto el API como el frontend compilado.

## 🆘 Ayuda

Si nada funciona:
1. Ejecuta `.\detener-desarrollo.ps1`
2. Cierra Visual Studio
3. Cierra todas las ventanas de PowerShell
4. Reinicia la computadora (opcional pero efectivo)
5. Ejecuta `.\iniciar-desarrollo.ps1`

---

**Última actualización**: 2026-01-30

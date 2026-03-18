# Planilla Desktop (Tauri v2)

App de escritorio de Pagly - Sistema de Planilla, empaquetada con Tauri v2.
Se conecta al backend en la nube (`https://app.pagly.io`) — no funciona offline.

## Prerequisitos

### 1. Rust + Cargo
```bash
# Instalar rustup (Windows: descargar desde https://rustup.rs/)
rustup install stable
rustup default stable
```

### 2. Microsoft C++ Build Tools (Windows)
Descargar desde: https://visualstudio.microsoft.com/visual-cpp-build-tools/
Instalar la carga de trabajo: **"Desarrollo para el escritorio con C++"**

### 3. WebView2 Runtime (Windows)
Normalmente ya viene preinstalado en Windows 10/11. Si no:
https://developer.microsoft.com/microsoft-edge/webview2/

### 4. Node.js / npm
Requerido para el CLI de Tauri.

---

## Desarrollo local

```bash
# 1. Arrancar el backend (.NET) en una terminal
cd /ruta/a/Planilla
dotnet run --project src/UI/Planilla.Web

# 2. Arrancar el frontend en otra terminal
cd src/UI/Planilla.Web/ClientApp
npm run dev
# Queda en http://localhost:5173

# 3. Arrancar la app Tauri (en este directorio)
cd src/UI/Planilla.Desktop
npm install
npm run tauri:dev
```

La app abrirá cargando `http://localhost:5173` (configurado en `tauri.conf.json > build.devUrl`).

---

## Build del instalador

```bash
# 1. Build del frontend con la URL de producción
cd src/UI/Planilla.Web/ClientApp
npm run build:desktop
# Genera el SPA en ../wwwroot/ con VITE_API_URL=https://app.pagly.io

# 2. Build del instalador Tauri
cd src/UI/Planilla.Desktop
npm install
npm run tauri:build
```

### Archivos generados (en `src-tauri/target/release/bundle/`)
| Plataforma | Formato | Descripción |
|------------|---------|-------------|
| Windows    | `.msi`  | Instalador MSI (Wix) |
| Windows    | `.exe`  | Instalador NSIS |
| macOS      | `.dmg`  | Imagen de disco |
| Linux      | `.AppImage` | Portable |
| Linux      | `.deb`  | Paquete Debian |

---

## Configuración CORS (Backend en producción)

Agregar en CapRover → App → Environment Variables:

```
Cors__AllowedOrigins=https://app.pagly.io,tauri://localhost,https://tauri.localhost
```

Los orígenes `tauri://localhost` y `https://tauri.localhost` son los que usa la WebView de Tauri
cuando carga el frontend empaquetado (`frontendDist`).

---

## Firma de código (para distribución comercial)

Sin firma de código, Windows SmartScreen mostrará una advertencia al instalar.
Para el MVP: el usuario puede hacer clic en "Más información → Ejecutar de todas formas".

Para la versión comercial, se requiere un certificado de firma de código (~$200-400/año):
- DigiCert, Sectigo, o similar
- Configurar en `tauri.conf.json > bundle > windows > certificateThumbprint`

---

## Auto-update (futuro)

El plugin `@tauri-apps/plugin-updater` está incluido en `Cargo.toml`.
Para activarlo, configurar en `tauri.conf.json > plugins > updater`:
- `pubkey`: clave pública Ed25519 generada con `tauri signer generate`
- `endpoints`: URL del archivo `latest.json` (p.ej. GitHub Releases)

---

## Estructura de archivos

```
Planilla.Desktop/
  src-tauri/
    tauri.conf.json     # Configuración principal de Tauri
    Cargo.toml          # Dependencias Rust
    build.rs            # Script de build requerido por tauri-build
    src/
      main.rs           # Entry point (Windows subsystem)
      lib.rs            # Lógica de la app (~10 líneas)
    icons/              # Iconos de la app (.ico, .png, .icns)
  package.json          # Scripts: tauri:dev, tauri:build
  .env.desktop          # VITE_API_URL para build de producción
  README.md             # Este archivo
```

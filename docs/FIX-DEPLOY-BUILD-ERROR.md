# Fix: Error en Build de Deploy - baseline-browser-mapping

## Problema

Al hacer deploy, el build del frontend falla en el paso `RUN npm run build` del Dockerfile. Los logs muestran:

- **Warning:** `[baseline-browser-mapping] The data in this module is over two months old. To ensure accurate Baseline data, please update: npm i baseline-browser-mapping@latest -D`
- Múltiples errores "Something bad happened" en la interfaz de CapRover

## Causa

El paquete `baseline-browser-mapping` está desactualizado y puede estar causando problemas en el build de producción con Vite 7.

## Solución

Actualizar `baseline-browser-mapping` a la última versión como dependencia de desarrollo:

```bash
cd src/UI/Planilla.Web/ClientApp
npm i baseline-browser-mapping@latest -D
```

Luego verificar que el build funciona localmente:

```bash
npm run build
```

Si el build local funciona, el deploy debería funcionar también.

## Archivo a modificar

- `src/UI/Planilla.Web/ClientApp/package.json` (se actualizará automáticamente con el comando npm)

## Verificación

Después de actualizar, verificar que:
1. El build local funciona sin errores
2. El Dockerfile sigue funcionando (si usa `npm ci` o `npm install`)
3. El deploy en CapRover completa exitosamente

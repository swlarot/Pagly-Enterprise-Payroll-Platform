#!/bin/bash
# ZAP API Scan (basado en OpenAPI/Swagger) — Planilla (Pagly)
# Requiere acceso al spec Swagger del ambiente de DESARROLLO local.
# Swagger está deshabilitado en staging/producción.
#
# Uso:
#   1. Asegúrate de que el backend local corre en localhost:5039
#   2. ./zap-api-scan.sh <jwt_token>
#
# El spec se descarga automáticamente desde localhost:5039

JWT_TOKEN="${1}"
DEV_URL="http://localhost:5039"
SWAGGER_URL="$DEV_URL/swagger/v1/swagger.json"
SPEC_FILE="./swagger-planilla.json"
REPORT_FILE="reporte-zap-api-$(date +%Y%m%d-%H%M).html"
REPORT_DIR="./reportes"

if [ -z "$JWT_TOKEN" ]; then
  echo "ERROR: Debes proporcionar un JWT token del ambiente local."
  echo "Uso: $0 <jwt_token>"
  echo ""
  echo "Para obtener el token:"
  echo "  curl -s -X POST $DEV_URL/api/auth/login \\"
  echo "    -H 'Content-Type: application/json' \\"
  echo "    -d '{\"email\":\"tu@email.com\",\"password\":\"tupassword\"}'"
  exit 1
fi

mkdir -p "$REPORT_DIR"

echo "Descargando spec Swagger desde $SWAGGER_URL..."
curl -s -o "$SPEC_FILE" "$SWAGGER_URL"

if [ ! -f "$SPEC_FILE" ] || [ ! -s "$SPEC_FILE" ]; then
  echo "ERROR: No se pudo descargar el spec desde $SWAGGER_URL"
  echo "Asegurate de que el backend local esta corriendo: dotnet run --project src/UI/Planilla.Web"
  exit 1
fi

echo "Spec descargado: $SPEC_FILE"
echo ""

docker run --rm \
  -v "$(pwd):/zap/wrk" \
  ghcr.io/zaproxy/zaproxy:stable \
  zap-api-scan.py \
  -t "/zap/wrk/$(basename $SPEC_FILE)" \
  -f openapi \
  -r "$REPORT_FILE" \
  -I \
  -T 60 \
  -z "-config api.addrs.addr.name=.* \
      -config api.addrs.addr.regex=true \
      -config replacer.full_list\(0\).description=auth_header \
      -config replacer.full_list\(0\).enabled=true \
      -config replacer.full_list\(0\).matchtype=REQ_HEADER \
      -config replacer.full_list\(0\).matchstr=Authorization \
      -config replacer.full_list\(0\).regex=false \
      -config replacer.full_list\(0\).replacement=Bearer\ $JWT_TOKEN"

mv "$REPORT_FILE" "$REPORT_DIR/" 2>/dev/null || true

echo ""
echo "============================================"
echo "  API Scan completado"
echo "  Reporte: $REPORT_DIR/$REPORT_FILE"
echo "============================================"

#!/bin/bash
# ZAP Full Active Scan — Planilla (Pagly)
# Scan completo con ataques activos. Solo en STAGING, nunca producción.
# Duración: 1-3 horas
#
# Uso:
#   ./zap-full-scan.sh <jwt_token>
#   ./zap-full-scan.sh <jwt_token> https://staging-planilla.vorluno.dev
#
# Prerequisito: Obtener JWT haciendo login primero:
#   curl -s -X POST https://staging-planilla.vorluno.dev/api/auth/login \
#     -H "Content-Type: application/json" \
#     -d '{"email":"admin@test.com","password":"TuPassword"}' | python3 -m json.tool
#
# Copia el campo "accessToken" y pásalo como primer argumento.

JWT_TOKEN="${1}"
BASE_URL="${2:-https://staging-planilla.vorluno.dev}"
REPORT_FILE="reporte-zap-full-$(date +%Y%m%d-%H%M).html"
REPORT_DIR="./reportes"

if [ -z "$JWT_TOKEN" ]; then
  echo "ERROR: Debes proporcionar un JWT token."
  echo "Uso: $0 <jwt_token> [url]"
  echo ""
  echo "Para obtener el token:"
  echo "  curl -s -X POST $BASE_URL/api/auth/login \\"
  echo "    -H 'Content-Type: application/json' \\"
  echo "    -d '{\"email\":\"tu@email.com\",\"password\":\"tupassword\"}'"
  exit 1
fi

mkdir -p "$REPORT_DIR"

echo "============================================"
echo "  ZAP Full Active Scan — Planilla"
echo "  Target: $BASE_URL"
echo "  Reporte: $REPORT_DIR/$REPORT_FILE"
echo "============================================"
echo ""
echo "ADVERTENCIA: Este scan envía ataques reales al servidor."
echo "Verificar que apuntas a STAGING, no a produccion."
echo ""
read -p "Confirmar target '$BASE_URL' [s/N]: " confirm
if [[ "$confirm" != "s" && "$confirm" != "S" ]]; then
  echo "Cancelado."
  exit 0
fi
echo ""

docker run --rm \
  -v "$(pwd)/$REPORT_DIR:/zap/wrk" \
  -e "ZAP_AUTH_HEADER=Authorization" \
  -e "ZAP_AUTH_HEADER_VALUE=Bearer $JWT_TOKEN" \
  ghcr.io/zaproxy/zaproxy:stable \
  zap-full-scan.py \
  -t "$BASE_URL" \
  -r "$REPORT_FILE" \
  -I \
  -j \
  -z "-config api.addrs.addr.name=.* \
      -config api.addrs.addr.regex=true \
      -config replacer.full_list\(0\).description=auth_header \
      -config replacer.full_list\(0\).enabled=true \
      -config replacer.full_list\(0\).matchtype=REQ_HEADER \
      -config replacer.full_list\(0\).matchstr=Authorization \
      -config replacer.full_list\(0\).regex=false \
      -config replacer.full_list\(0\).replacement=Bearer\ $JWT_TOKEN"

echo ""
echo "============================================"
echo "  Scan completado"
echo "  Reporte: $REPORT_DIR/$REPORT_FILE"
echo "============================================"

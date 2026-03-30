#!/bin/bash
# ZAP Baseline Scan — Planilla (Pagly)
# Passive scan + baseline checks. Sin riesgo para el servidor.
# Duración: ~5-15 minutos
#
# Uso:
#   ./zap-baseline.sh
#   ./zap-baseline.sh https://staging-planilla.vorluno.dev
#
# Requisitos: Docker instalado

BASE_URL="${1:-https://staging-planilla.vorluno.dev}"
REPORT_FILE="reporte-zap-baseline-$(date +%Y%m%d-%H%M).html"
REPORT_DIR="./reportes"

mkdir -p "$REPORT_DIR"

echo "============================================"
echo "  ZAP Baseline Scan — Planilla"
echo "  Target: $BASE_URL"
echo "  Reporte: $REPORT_DIR/$REPORT_FILE"
echo "============================================"
echo ""
echo "Este scan NO envía ataques. Solo observa."
echo "Detecta: headers faltantes, cookies inseguras, info disclosure."
echo ""

docker run --rm \
  -v "$(pwd)/$REPORT_DIR:/zap/wrk" \
  ghcr.io/zaproxy/zaproxy:stable \
  zap-baseline.py \
  -t "$BASE_URL" \
  -r "$REPORT_FILE" \
  -I \
  -j \
  --hook=/zap/auth_hook.py 2>/dev/null || \
docker run --rm \
  -v "$(pwd)/$REPORT_DIR:/zap/wrk" \
  ghcr.io/zaproxy/zaproxy:stable \
  zap-baseline.py \
  -t "$BASE_URL" \
  -r "$REPORT_FILE" \
  -I \
  -j

echo ""
echo "============================================"
echo "  Scan completado"
echo "  Reporte: $REPORT_DIR/$REPORT_FILE"
echo "============================================"
echo ""
echo "Abre el reporte HTML en tu navegador para ver los hallazgos."
echo "Alertas por color: ROJO=Alta, NARANJA=Media, AMARILLO=Baja, AZUL=Informativa"

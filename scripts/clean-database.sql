-- Script para limpiar la base de datos y resetear secuencias
-- IMPORTANTE: Este script NO limpia PayrollTaxConfigurations ni TaxBrackets (configuración base)

-- Limpiar tablas en orden inverso de dependencias
TRUNCATE TABLE "PayrollDetails" CASCADE;
TRUNCATE TABLE "PayrollHeaders" CASCADE;
TRUNCATE TABLE "SaldosVacaciones" CASCADE;
TRUNCATE TABLE "SolicitudesVacaciones" CASCADE;
TRUNCATE TABLE "Ausencias" CASCADE;
TRUNCATE TABLE "HorasExtra" CASCADE;
TRUNCATE TABLE "PagosPrestamos" CASCADE;
TRUNCATE TABLE "Prestamos" CASCADE;
TRUNCATE TABLE "Anticipos" CASCADE;
TRUNCATE TABLE "DeduccionesFijas" CASCADE;
TRUNCATE TABLE "Empleados" CASCADE;
TRUNCATE TABLE "Posiciones" CASCADE;
TRUNCATE TABLE "Departamentos" CASCADE;
TRUNCATE TABLE "RecibosDeSueldo" CASCADE;

-- Resetear secuencias a 1
ALTER SEQUENCE "Empleados_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "Departamentos_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "Posiciones_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "PayrollHeaders_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "PayrollDetails_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "Prestamos_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "DeduccionesFijas_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "Anticipos_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "HorasExtra_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "Ausencias_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "SolicitudesVacaciones_Id_seq" RESTART WITH 1;

-- Verificar que las tablas están vacías
SELECT 'Empleados' as tabla, COUNT(*) as registros FROM "Empleados"
UNION ALL
SELECT 'Departamentos', COUNT(*) FROM "Departamentos"
UNION ALL
SELECT 'Posiciones', COUNT(*) FROM "Posiciones"
UNION ALL
SELECT 'PayrollHeaders', COUNT(*) FROM "PayrollHeaders"
UNION ALL
SELECT 'PayrollDetails', COUNT(*) FROM "PayrollDetails"
UNION ALL
SELECT 'Prestamos', COUNT(*) FROM "Prestamos"
UNION ALL
SELECT 'DeduccionesFijas', COUNT(*) FROM "DeduccionesFijas"
UNION ALL
SELECT 'Anticipos', COUNT(*) FROM "Anticipos"
UNION ALL
SELECT 'HorasExtra', COUNT(*) FROM "HorasExtra"
UNION ALL
SELECT 'Ausencias', COUNT(*) FROM "Ausencias";

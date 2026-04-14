-- Verificar usuarios duplicados por email
SELECT "Email", COUNT(*) as count
FROM "AspNetUsers"
GROUP BY "Email"
HAVING COUNT(*) > 1;

-- Ver detalles de usuarios con emails similares
SELECT "Id", "UserName", "Email", "EmailConfirmed"
FROM "AspNetUsers"
WHERE "Email" LIKE '%hatsukiminara%'
ORDER BY "Email";

-- Verificar si hay empleados vinculados
SELECT e."Id", e."Nombre", e."Email", e."AppUserId", u."Email" as "UserEmail"
FROM "Empleados" e
LEFT JOIN "AspNetUsers" u ON e."AppUserId" = u."Id"
WHERE e."Email" LIKE '%hatsukiminara%';

-- NOTA: No ejecutar DELETE aún - primero revisar los resultados
-- DELETE FROM "AspNetUsers" WHERE "Email" = 'hatsukiminara@gmai.com';

-- ====================================================================
-- Script: Crear rol predefinido "Empleado - Auto Servicio"
-- Descripción: Rol para empleados que pueden ver solo su información
-- Uso: Ejecutar para cada tenant que quiera habilitar auto-servicio
-- ====================================================================

-- IMPORTANTE: Reemplazar @TENANT_ID con el ID del tenant
-- Ejemplo: SET @TENANT_ID = 1;

DO $$
DECLARE
    v_tenant_id INT := 1; -- ⚠️ CAMBIAR ESTE VALOR AL TENANT ID CORRECTO
    v_role_id INT;
BEGIN
    -- Verificar si el rol ya existe para este tenant
    SELECT "Id" INTO v_role_id
    FROM "CustomTenantRoles"
    WHERE "TenantId" = v_tenant_id
      AND "Name" = 'Empleado - Auto Servicio'
      AND NOT "IsDeleted";

    -- Si no existe, crear el rol
    IF v_role_id IS NULL THEN
        -- Crear el rol personalizado
        INSERT INTO "CustomTenantRoles" (
            "TenantId",
            "Name",
            "Description",
            "IsSystem",
            "Color",
            "DisplayOrder",
            "IsDeleted",
            "CreatedAt"
        )
        VALUES (
            v_tenant_id,
            'Empleado - Auto Servicio',
            'Empleado que puede ver solo su información personal, planillas y solicitar vacaciones',
            TRUE, -- IsSystem = true para que no se pueda eliminar
            '#10B981', -- Color verde
            10,
            FALSE,
            NOW()
        )
        RETURNING "Id" INTO v_role_id;

        RAISE NOTICE 'Rol "Empleado - Auto Servicio" creado con ID: %', v_role_id;

        -- Asignar permisos al rol
        INSERT INTO "RolePermissions" ("CustomTenantRoleId", "Permission", "CreatedAt")
        VALUES
            -- Dashboard
            (v_role_id, 'dashboard.view', NOW()),

            -- Ver y editar solo su perfil de empleado
            (v_role_id, 'employee.view_self', NOW()),
            (v_role_id, 'employee.update_self', NOW()),

            -- Ver solo sus planillas
            (v_role_id, 'payroll.view_self', NOW()),

            -- Solicitar y ver sus vacaciones
            (v_role_id, 'vacations.request_self', NOW()),

            -- Ver sus ausencias
            (v_role_id, 'absences.view_self', NOW()),

            -- Ver sus horas extra
            (v_role_id, 'overtime.view_self', NOW()),

            -- Ver sus préstamos
            (v_role_id, 'loans.view_self', NOW()),

            -- Ver sus deducciones
            (v_role_id, 'deductions.view_self', NOW());

        RAISE NOTICE 'Permisos asignados exitosamente al rol';
    ELSE
        RAISE NOTICE 'El rol "Empleado - Auto Servicio" ya existe con ID: %', v_role_id;
    END IF;

END $$;

-- ====================================================================
-- Verificación: Listar roles y permisos creados
-- ====================================================================
SELECT
    r."Id",
    r."Name",
    r."Description",
    r."Color",
    COUNT(rp."Id") as "TotalPermisos"
FROM "CustomTenantRoles" r
LEFT JOIN "RolePermissions" rp ON r."Id" = rp."CustomTenantRoleId"
WHERE r."TenantId" = 1 -- ⚠️ CAMBIAR AL TENANT ID CORRECTO
  AND r."Name" = 'Empleado - Auto Servicio'
  AND NOT r."IsDeleted"
GROUP BY r."Id", r."Name", r."Description", r."Color";

-- Ver permisos del rol
SELECT
    r."Name" as "Rol",
    rp."Permission" as "Permiso"
FROM "CustomTenantRoles" r
INNER JOIN "RolePermissions" rp ON r."Id" = rp."CustomTenantRoleId"
WHERE r."TenantId" = 1 -- ⚠️ CAMBIAR AL TENANT ID CORRECTO
  AND r."Name" = 'Empleado - Auto Servicio'
  AND NOT r."IsDeleted"
ORDER BY rp."Permission";

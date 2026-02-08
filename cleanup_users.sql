-- Script SQL para limpiar usuarios problemáticos de la base de datos
-- ADVERTENCIA: Este script hace eliminaciones físicas (hard delete)
-- Ejecutar con precaución y hacer backup antes

-- ============================================
-- PASO 1: Identificar usuarios a eliminar
-- ============================================
-- Reemplaza estos IDs con los IDs reales de los usuarios que quieres eliminar
-- Puedes obtener los IDs ejecutando la consulta de abajo primero

-- Ver usuarios problemáticos:
-- SELECT "Id", "Email", "UserName", "IsSystemAdmin", "IsDeleted" 
-- FROM "AspNetUsers" 
-- WHERE "Email" IN ('hatsukiminara@gmai.com', 'hatsukiminara@gmail.com', 'kmovil2014.km@gmail.com');

-- ============================================
-- PASO 2: Limpiar datos relacionados ANTES de eliminar el usuario
-- ============================================

-- Función helper para limpiar un usuario específico
-- Reemplaza 'USER_ID_AQUI' con el ID real del usuario

DO $$
DECLARE
    user_id_to_delete TEXT := 'USER_ID_AQUI'; -- ⚠️ CAMBIAR ESTE VALOR
    system_admin_id TEXT;
BEGIN
    -- Obtener un SystemAdmin válido para reemplazar referencias
    SELECT "Id" INTO system_admin_id
    FROM "AspNetUsers"
    WHERE "IsSystemAdmin" = true AND "IsDeleted" = false
    LIMIT 1;

    -- Si no hay SystemAdmin, usar 'system' como fallback
    IF system_admin_id IS NULL THEN
        system_admin_id := 'system';
    END IF;

    RAISE NOTICE 'Limpiando usuario: %', user_id_to_delete;
    RAISE NOTICE 'SystemAdmin de reemplazo: %', system_admin_id;

    -- 1. Desvincular empleados
    UPDATE "Empleados" 
    SET "UserId" = NULL 
    WHERE "UserId" = user_id_to_delete;
    RAISE NOTICE 'Empleados desvinculados';

    -- 2. Eliminar RefreshTokens
    DELETE FROM "RefreshTokens" 
    WHERE "UserId" = user_id_to_delete;
    RAISE NOTICE 'RefreshTokens eliminados';

    -- 3. Eliminar TenantUsers (membresías)
    DELETE FROM "TenantUsers" 
    WHERE "UserId" = user_id_to_delete;
    RAISE NOTICE 'TenantUsers eliminados';

    -- 4. Eliminar TenantInvitations NO aceptadas
    DELETE FROM "TenantInvitations" 
    WHERE "CreatedByUserId" = user_id_to_delete 
    AND "AcceptedAt" IS NULL;
    RAISE NOTICE 'Invitaciones pendientes eliminadas';

    -- 5. Actualizar TenantInvitations ACEPTADAS (cambiar CreatedByUserId)
    UPDATE "TenantInvitations" 
    SET "CreatedByUserId" = system_admin_id
    WHERE "CreatedByUserId" = user_id_to_delete 
    AND "AcceptedAt" IS NOT NULL;
    RAISE NOTICE 'Invitaciones aceptadas actualizadas';

    -- 6. Eliminar registros de ASP.NET Identity
    DELETE FROM "AspNetUserClaims" WHERE "UserId" = user_id_to_delete;
    DELETE FROM "AspNetUserLogins" WHERE "UserId" = user_id_to_delete;
    DELETE FROM "AspNetUserRoles" WHERE "UserId" = user_id_to_delete;
    DELETE FROM "AspNetUserTokens" WHERE "UserId" = user_id_to_delete;
    RAISE NOTICE 'Registros de Identity eliminados';

    -- 7. Finalmente, eliminar el usuario
    DELETE FROM "AspNetUsers" WHERE "Id" = user_id_to_delete;
    RAISE NOTICE 'Usuario eliminado exitosamente';

END $$;

-- ============================================
-- PASO 3: Eliminar usuarios específicos (ejemplo)
-- ============================================
-- Descomenta y ajusta los IDs según tus necesidades

/*
-- Usuario 1: hatsukiminara@gmai.com (sin asignar)
DO $$
DECLARE
    user_id TEXT := 'b7d902a4-65a0-4f29-9161-c260f014751c'; -- ⚠️ Verificar ID real
    system_admin_id TEXT;
BEGIN
    SELECT "Id" INTO system_admin_id FROM "AspNetUsers" WHERE "IsSystemAdmin" = true AND "IsDeleted" = false LIMIT 1;
    IF system_admin_id IS NULL THEN system_admin_id := 'system'; END IF;
    
    UPDATE "Empleados" SET "UserId" = NULL WHERE "UserId" = user_id;
    DELETE FROM "RefreshTokens" WHERE "UserId" = user_id;
    DELETE FROM "TenantUsers" WHERE "UserId" = user_id;
    DELETE FROM "TenantInvitations" WHERE "CreatedByUserId" = user_id AND "AcceptedAt" IS NULL;
    UPDATE "TenantInvitations" SET "CreatedByUserId" = system_admin_id WHERE "CreatedByUserId" = user_id AND "AcceptedAt" IS NOT NULL;
    DELETE FROM "AspNetUserClaims" WHERE "UserId" = user_id;
    DELETE FROM "AspNetUserLogins" WHERE "UserId" = user_id;
    DELETE FROM "AspNetUserRoles" WHERE "UserId" = user_id;
    DELETE FROM "AspNetUserTokens" WHERE "UserId" = user_id;
    DELETE FROM "AspNetUsers" WHERE "Id" = user_id;
END $$;

-- Usuario 2: hatsukiminara@gmail.com (con tenant)
DO $$
DECLARE
    user_id TEXT := '527aa4ad-2318-4468-84b6-c950ff7500bd'; -- ⚠️ Verificar ID real
    system_admin_id TEXT;
BEGIN
    SELECT "Id" INTO system_admin_id FROM "AspNetUsers" WHERE "IsSystemAdmin" = true AND "IsDeleted" = false LIMIT 1;
    IF system_admin_id IS NULL THEN system_admin_id := 'system'; END IF;
    
    UPDATE "Empleados" SET "UserId" = NULL WHERE "UserId" = user_id;
    DELETE FROM "RefreshTokens" WHERE "UserId" = user_id;
    DELETE FROM "TenantUsers" WHERE "UserId" = user_id;
    DELETE FROM "TenantInvitations" WHERE "CreatedByUserId" = user_id AND "AcceptedAt" IS NULL;
    UPDATE "TenantInvitations" SET "CreatedByUserId" = system_admin_id WHERE "CreatedByUserId" = user_id AND "AcceptedAt" IS NOT NULL;
    DELETE FROM "AspNetUserClaims" WHERE "UserId" = user_id;
    DELETE FROM "AspNetUserLogins" WHERE "UserId" = user_id;
    DELETE FROM "AspNetUserRoles" WHERE "UserId" = user_id;
    DELETE FROM "AspNetUserTokens" WHERE "UserId" = user_id;
    DELETE FROM "AspNetUsers" WHERE "Id" = user_id;
END $$;

-- Usuario 3: kmovil2014.km@gmail.com
DO $$
DECLARE
    user_id TEXT := '60cea108-b62f-4239-bcfa-5503543bd882'; -- ⚠️ Verificar ID real
    system_admin_id TEXT;
BEGIN
    SELECT "Id" INTO system_admin_id FROM "AspNetUsers" WHERE "IsSystemAdmin" = true AND "IsDeleted" = false LIMIT 1;
    IF system_admin_id IS NULL THEN system_admin_id := 'system'; END IF;
    
    UPDATE "Empleados" SET "UserId" = NULL WHERE "UserId" = user_id;
    DELETE FROM "RefreshTokens" WHERE "UserId" = user_id;
    DELETE FROM "TenantUsers" WHERE "UserId" = user_id;
    DELETE FROM "TenantInvitations" WHERE "CreatedByUserId" = user_id AND "AcceptedAt" IS NULL;
    UPDATE "TenantInvitations" SET "CreatedByUserId" = system_admin_id WHERE "CreatedByUserId" = user_id AND "AcceptedAt" IS NOT NULL;
    DELETE FROM "AspNetUserClaims" WHERE "UserId" = user_id;
    DELETE FROM "AspNetUserLogins" WHERE "UserId" = user_id;
    DELETE FROM "AspNetUserRoles" WHERE "UserId" = user_id;
    DELETE FROM "AspNetUserTokens" WHERE "UserId" = user_id;
    DELETE FROM "AspNetUsers" WHERE "Id" = user_id;
END $$;
*/

-- ============================================
-- VERIFICACIÓN: Ver qué usuarios quedan
-- ============================================
-- SELECT "Id", "Email", "UserName", "IsSystemAdmin", "IsDeleted" 
-- FROM "AspNetUsers" 
-- ORDER BY "Email";

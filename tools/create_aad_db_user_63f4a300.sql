-- Create a contained Azure AD user for the service principal that acquires tokens
-- Replace YOUR_SERVICE_PRINCIPAL_OBJECT_ID with the actual object ID from your Azure AD app registration
-- Run this as the server admin (or as an AD admin) against the WileyWidgetDB database.

USE [WileyWidgetDB];
GO

-- Recommended least-privilege roles for app: reader + writer
CREATE USER [YOUR_SERVICE_PRINCIPAL_OBJECT_ID] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [YOUR_SERVICE_PRINCIPAL_OBJECT_ID];
ALTER ROLE db_datawriter ADD MEMBER [YOUR_SERVICE_PRINCIPAL_OBJECT_ID];

-- Optional for troubleshooting (grant full DB rights temporarily):
-- ALTER ROLE db_owner ADD MEMBER [63f4a300-6ef5-41e0-aaf4-8b82f8774674];

GO

-- Verify mapping
SELECT dp.name, dp.type_desc, dp.create_date
FROM sys.database_principals dp
WHERE dp.authentication_type_desc = 'EXTERNAL_PROVIDER'
  AND dp.name = '63f4a300-6ef5-41e0-aaf4-8b82f8774674';
GO

-- Test permissions: run simple select (requires existing table)
-- SELECT COUNT(*) FROM dbo.Enterprises; -- run after user creation to validate access

-- Create a contained Azure AD user for the service principal that acquires tokens
-- Object id captured from app token: 63f4a300-6ef5-41e0-aaf4-8b82f8774674
-- Run this as the server admin (or as an AD admin) against the WileyWidgetDB database.

USE [WileyWidgetDB];
GO

-- Recommended least-privilege roles for app: reader + writer
CREATE USER [63f4a300-6ef5-41e0-aaf4-8b82f8774674] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [63f4a300-6ef5-41e0-aaf4-8b82f8774674];
ALTER ROLE db_datawriter ADD MEMBER [63f4a300-6ef5-41e0-aaf4-8b82f8774674];

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

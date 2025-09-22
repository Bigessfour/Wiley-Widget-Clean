Purpose
=======
This document explains how to create a contained Azure AD user in the WileyWidgetDB for the service principal used by the app (object id 63f4a300-6ef5-41e0-aaf4-8b82f8774674).

When to run
===========
Run these steps when your application is using Azure AD passwordless authentication and you see SQL login failures like:

  Microsoft.Data.SqlClient.SqlException: Login failed for user '<token-identified principal>' (Error 18456)

This file was generated after capturing the OID in the token used by DefaultAzureCredential.

Steps (Portal)
==============
1. Open the Azure Portal -> SQL servers -> Select your logical server (e.g., wileywidget-sql).
2. Ensure an Azure Active Directory admin is configured for the server:
   - Under 'Active Directory admin', set an admin if not already set. You must be an admin to create AAD users in the DB.
3. Open the SQL Database blade -> select 'WileyWidgetDB' -> 'Query editor (preview)'.
4. Sign in using the server admin (or AD admin) credentials.
5. Copy the SQL from `tools/create_aad_db_user_63f4a300.sql` and run it.
6. If you used db_owner for testing, remove it after verification and use least privilege.

Steps (Azure Data Studio / SSMS)
===============================
1. Connect to the logical server using an account with admin permissions.
2. Open a new query, select the WileyWidgetDB database context.
3. Execute `tools/create_aad_db_user_63f4a300.sql`.

Verify
======
- Re-run the app in Production mode (ASPNETCORE_ENVIRONMENT=Production) and observe logs. The 18456 errors should disappear and EF Core queries should succeed.
- In SQL, verify the user exists by running:
  SELECT name, type_desc FROM sys.database_principals WHERE authentication_type_desc = 'EXTERNAL_PROVIDER';

Security notes
==============
- Do NOT commit sensitive credentials to the repo.
- Remove any temporary claim logging from the code after verification.

If you want, I can
==================
- Run the Production test again to confirm success once you applied the SQL script, and then remove the temporary token logging and commit the changes.
- Create a branch and commit these files and changes for you.

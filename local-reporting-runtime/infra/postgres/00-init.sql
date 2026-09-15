-- Dev-only Postgres bootstrap for the local reporting runtime.
-- One cluster, one database per platform service, admin + runtime role per service
-- (matching the role names each service's migrations expect).
ALTER SYSTEM SET max_connections TO '400';

CREATE ROLE platform_storage_admin WITH LOGIN SUPERUSER PASSWORD 'Password';
CREATE ROLE platform_storage WITH LOGIN PASSWORD 'Password';
CREATE DATABASE storagedb OWNER platform_storage_admin;

CREATE ROLE platform_events_admin WITH LOGIN SUPERUSER PASSWORD 'Password';
CREATE ROLE platform_events WITH LOGIN PASSWORD 'Password';
CREATE DATABASE eventsdb OWNER platform_events_admin;

CREATE ROLE platform_profile_admin WITH LOGIN SUPERUSER PASSWORD 'Password';
CREATE ROLE platform_profile WITH LOGIN PASSWORD 'Password';
CREATE DATABASE profiledb OWNER platform_profile_admin;

CREATE ROLE auth_authentication_admin WITH LOGIN SUPERUSER PASSWORD 'Password';
CREATE ROLE auth_authentication WITH LOGIN PASSWORD 'Password';
CREATE DATABASE authentication OWNER auth_authentication_admin;

-- Authorization (PDP) and Access Management share authorizationdb upstream as well.
CREATE ROLE platform_authorization_admin WITH LOGIN SUPERUSER PASSWORD 'Password';
CREATE ROLE platform_authorization WITH LOGIN PASSWORD 'Password';
CREATE DATABASE authorizationdb OWNER platform_authorization_admin;

CREATE ROLE register WITH LOGIN SUPERUSER PASSWORD 'Password';
CREATE DATABASE register OWNER register;

CREATE ROLE workflow_engine WITH LOGIN PASSWORD 'Password';
CREATE DATABASE workflow_engine OWNER workflow_engine;

\c storagedb
CREATE SCHEMA IF NOT EXISTS storage AUTHORIZATION platform_storage_admin;
GRANT USAGE ON SCHEMA storage TO platform_storage;
ALTER DEFAULT PRIVILEGES FOR ROLE platform_storage_admin IN SCHEMA storage GRANT SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER ON TABLES TO platform_storage;
ALTER DEFAULT PRIVILEGES FOR ROLE platform_storage_admin IN SCHEMA storage GRANT ALL ON SEQUENCES TO platform_storage;
ALTER DEFAULT PRIVILEGES FOR ROLE platform_storage_admin IN SCHEMA storage GRANT EXECUTE ON FUNCTIONS TO platform_storage;

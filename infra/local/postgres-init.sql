-- Inicialización de Rappix
-- Crea una base de datos independiente por microservicio
-- Cada servicio tiene aislamiento total (no comparte tablas con otros)

CREATE DATABASE rappix_identity;
CREATE DATABASE rappix_merchants;
CREATE DATABASE rappix_catalog;
CREATE DATABASE rappix_pricing;
CREATE DATABASE rappix_orders;
CREATE DATABASE rappix_dispatch;
CREATE DATABASE rappix_tracking;
CREATE DATABASE rappix_payments;
CREATE DATABASE rappix_notifications;
CREATE DATABASE rappix_ratings;
CREATE DATABASE rappix_hangfire;

-- Habilitar PostGIS en las bases de datos que lo necesitan
\c rappix_merchants
CREATE EXTENSION IF NOT EXISTS postgis;

\c rappix_dispatch
CREATE EXTENSION IF NOT EXISTS postgis;

\c rappix_tracking
CREATE EXTENSION IF NOT EXISTS postgis;

-- Habilitar uuid-ossp en todas (para gen_random_uuid si se necesita compatibilidad)
\c rappix_identity
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c rappix_merchants
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c rappix_catalog
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c rappix_orders
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c rappix_payments
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

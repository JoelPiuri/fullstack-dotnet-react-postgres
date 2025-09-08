-- ===========================================
--  Esquema base + datos semilla + relaciones
--  Entidades: clientes, servicios
--  Relación M:N: cliente_servicios
-- ===========================================

BEGIN;

-- --------- Tablas principales ----------
CREATE TABLE IF NOT EXISTS clientes (
  id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  nombre_cliente   VARCHAR(255) NOT NULL,
  correo           VARCHAR(255) NOT NULL
);

-- Evita correos duplicados
CREATE UNIQUE INDEX IF NOT EXISTS uq_clientes_correo ON clientes (correo);

CREATE TABLE IF NOT EXISTS servicios (
  id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  nombre_servicio  VARCHAR(255) NOT NULL,
  descripcion      VARCHAR(255)
);

-- Evita nombres duplicados de servicio
CREATE UNIQUE INDEX IF NOT EXISTS uq_servicios_nombre ON servicios (nombre_servicio);

-- --------- Tabla intermedia M:N ----------
CREATE TABLE IF NOT EXISTS cliente_servicios (
  cliente_id  INTEGER NOT NULL,
  servicio_id INTEGER NOT NULL,
  PRIMARY KEY (cliente_id, servicio_id),
  CONSTRAINT fk_cliente_servicios_cliente
    FOREIGN KEY (cliente_id) REFERENCES clientes(id) ON DELETE CASCADE,
  CONSTRAINT fk_cliente_servicios_servicio
    FOREIGN KEY (servicio_id) REFERENCES servicios(id) ON DELETE CASCADE
);

-- Índices de consulta (opcionales pero recomendados)
CREATE INDEX IF NOT EXISTS idx_cliente_servicios_cliente ON cliente_servicios (cliente_id);
CREATE INDEX IF NOT EXISTS idx_cliente_servicios_servicio ON cliente_servicios (servicio_id);

-- =========================
-- Datos semilla
-- =========================

-- Servicios
INSERT INTO servicios (nombre_servicio, descripcion) VALUES
  ('Consultoría', 'Servicios de consultoría tecnológica'),
  ('Soporte', 'Soporte técnico nivel 1'),
  ('Desarrollo', 'Desarrollo de software a medida'),
  ('Capacitación', 'Entrenamientos y workshops')
ON CONFLICT (nombre_servicio) DO UPDATE
SET descripcion = EXCLUDED.descripcion;

-- Clientes
INSERT INTO clientes (nombre_cliente, correo) VALUES
  ('Acme S.A.', 'contacto@acme.com'),
  ('Globex LLC', 'info@globex.com'),
  ('Initech', 'hello@initech.com')
ON CONFLICT (correo) DO NOTHING;

-- =========================
-- Relaciones Cliente-Servicios
-- =========================

-- Acme: Consultoría + Soporte
INSERT INTO cliente_servicios (cliente_id, servicio_id)
SELECT c.id, s.id
FROM clientes c
JOIN servicios s ON s.nombre_servicio IN ('Consultoría','Soporte')
WHERE c.correo = 'contacto@acme.com'
ON CONFLICT DO NOTHING;

-- Globex: Soporte + Desarrollo
INSERT INTO cliente_servicios (cliente_id, servicio_id)
SELECT c.id, s.id
FROM clientes c
JOIN servicios s ON s.nombre_servicio IN ('Soporte','Desarrollo')
WHERE c.correo = 'info@globex.com'
ON CONFLICT DO NOTHING;

-- Initech: todos los servicios
INSERT INTO cliente_servicios (cliente_id, servicio_id)
SELECT c.id, s.id
FROM clientes c
CROSS JOIN servicios s
WHERE c.correo = 'hello@initech.com'
ON CONFLICT DO NOTHING;

COMMIT;

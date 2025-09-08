# Imagen oficial de Postgres (ligera)
FROM postgres:16-alpine

# Copia los scripts de inicialización.
# Postgres ejecuta automáticamente cualquier .sql/.sql.gz/.sh en este directorio
# SOLO en el primer arranque del volumen de datos.
COPY init.sql /docker-entrypoint-initdb.d/

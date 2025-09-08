# fullstack-dotnet-react-postgres

Stack **full-stack** listo para levantar con Docker Compose:

- **Frontend:** React + Vite, servido con **nginx**
- **API (.NET):** Minimal API en **.NET 9**, EF Core + Npgsql
- **DB:** **PostgreSQL 16** con **seed** (datos iniciales) y relación **M:N** `cliente_servicios`

Incluye:
- CORS configurable por variable de entorno
- Healthchecks y preflight CORS
- Inicialización idempotente de la base con datos y relaciones
- `docker-compose` que levanta **DB → API → Web** en orden

---

## 📦 Requisitos

- Docker / Docker Desktop
- Docker Compose
- (Opcional) `curl` para probar endpoints

---

## 🗂️ Estructura

```
.
├─ db/
│  ├─ Dockerfile
│  └─ init.sql
├─ backend/
│  ├─ Api.csproj
│  ├─ Dockerfile
│  ├─ Program.cs
│  └─ src/
│     ├─ Data/AppDbContext.cs
│     └─ Models/
│        ├─ Cliente.cs
│        └─ Servicio.cs
├─ frontend/
│  ├─ Dockerfile
│  ├─ nginx.conf
│  ├─ package.json
│  ├─ vite.config.js
│  └─ src/App.jsx
└─ docker-compose.yml
```

---

## ⚙️ Variables de entorno

Crea un archivo `.env` en la raíz (o usa los defaults del compose):

```env
# Base de datos
POSTGRES_DB=appdb
POSTGRES_USER=appuser
POSTGRES_PASSWORD=apppassword

# Puertos host
API_PORT=8080
WEB_PORT=8081

# CORS (coma-separados). Para dev puedes usar * (no recomendado en prod)
ALLOWED_ORIGINS=http://localhost:8081,http://127.0.0.1:8081

# Host público para que el front apunte al API durante el build
PUBLIC_HOST=localhost
```

> Si cambias `.env`, vuelve a construir los servicios afectados:
> ```bash
> docker compose build web api
> ```

---

## ▶️ Levantar con Docker

```bash
# Construir e iniciar todo
docker compose up --build -d

# Ver logs en vivo
docker compose logs -f

# Detener
docker compose down
```

**Puertos (host):**

- **DB:** `localhost:5432`
- **API:** `http://localhost:8080`
- **Web:** `http://localhost:8081`

---

## 🗃️ Base de datos y seed

El contenedor Postgres ejecuta `db/init.sql` una **única vez**, cuando el volumen de datos está vacío. El script:

- Crea tablas `clientes`, `servicios` y la intermedia **M:N** `cliente_servicios`
- Añade **índices únicos** en `clientes.correo` y `servicios.nombre_servicio`
- Inserta **datos semilla** y **relaciones**:
  - **Acme** → Consultoría + Soporte  
  - **Globex** → Soporte + Desarrollo  
  - **Initech** → **todos** los servicios

> Si en logs ves “**Skipping initialization**”, significa que el volumen ya existía y no se volvió a ejecutar el seed.

### Opciones si ya existe el volumen

**A) Resetear (dev recomendado):**
```bash
docker compose down
docker volume rm fullstack-dotnet-react-postgres_dbdata
docker compose up -d db

# Verifica seed
docker exec -it db psql -U appuser -d appdb -c "select id,nombre_servicio from servicios;"
docker exec -it db psql -U appuser -d appdb -c "select id,nombre_cliente,correo from clientes;"
docker exec -it db psql -U appuser -d appdb -c "select * from cliente_servicios;"

# Luego levanta API y Web
docker compose up -d api web
```

**B) Cargar seed manualmente (sin borrar datos):**
```bash
# Windows PowerShell
type .\db\init.sql | docker exec -i db psql -U appuser -d appdb

# Linux/Mac
cat ./db/init.sql | docker exec -i db psql -U appuser -d appdb
```

---

## 🌐 CORS

La API permite orígenes definidos en `ALLOWED_ORIGINS` (coma-separados).
Para desarrollo puedes usar `ALLOWED_ORIGINS=*` (abre todo; **no usar en prod**).

Prueba rápida:
```bash
curl -i -H "Origin: http://localhost:8081" http://localhost:8080/health
```
Debes ver el header `Access-Control-Allow-Origin` en la respuesta.

> Si el navegador muestra “CORS” pero en realidad hay un **500**, revisa los logs del API:
> ```bash
> docker logs api --tail 200
> ```

---

## 🧠 API (.NET 9, Minimal API)

- EF Core con `UseSnakeCaseNamingConvention()` para mapear a columnas `snake_case`
- Relación **M:N** (clientes ↔ servicios) sin entidad puente explícita (EF crea `cliente_servicios`)
- DTOs de salida para incluir servicios en clientes
- Preflight `OPTIONS` y `UseExceptionHandler` para errores JSON limpios

### Endpoints

**Health:**
- `GET /health` → `{ status, utc }`
- `GET /dbcheck` → `{ db: "ok" }` si la conexión funciona

**Servicios:**
- `GET /api/servicios`
- `GET /api/servicios/{id}`
- `POST /api/servicios`
- `PUT /api/servicios/{id}`
- `DELETE /api/servicios/{id}`

**Clientes (con servicios):**
- `GET /api/clientes` → `{ id, nombreCliente, correo, servicios[] }`
- `GET /api/clientes/{id}`
- `POST /api/clientes` → crea cliente **asignando servicios** (ver ejemplo)
- `PUT /api/clientes/{id}` → actualización básica (nombre/correo)
- `DELETE /api/clientes/{id}`

### Crear cliente con servicios (ejemplo)

```bash
curl -i -H "Origin: http://localhost:8081" -H "Content-Type: application/json" \
  -d '{"nombreCliente":"Foo Corp","correo":"foo@corp.com","servicioIds":[1,2]}' \
  http://localhost:8080/api/clientes
```

---

## 🖥️ Frontend

- React + Vite
- En la creación de cliente se **debe** seleccionar **al menos un servicio**
- La URL del API se inyecta en build como `VITE_API_URL` (utiliza `PUBLIC_HOST` y `API_PORT` del `.env`)

**URL por defecto del front:** **`http://localhost:8081`**

---

## 🧪 Smoke tests

```bash
# servicios (deben aparecer 4 del seed)
curl http://localhost:8080/api/servicios

# clientes (devuelve servicios anidados)
curl http://localhost:8080/api/clientes
```

---

## 🐳 Detalles de contenedores

- **db**
  - Imagen: `postgres:16-alpine`
  - Volumen: `dbdata` → `/var/lib/postgresql/data`
  - Seeds: `/docker-entrypoint-initdb.d/init.sql`
  - Healthcheck: `pg_isready`

- **api**
  - Build multi-stage: `mcr.microsoft.com/dotnet/sdk:9.0` → `aspnet:9.0`
  - `ASPNETCORE_URLS=http://0.0.0.0:8080`
  - CORS por `ALLOWED_ORIGINS`
  - Conexión a DB por `ConnectionStrings__Default` (host `db` del compose)

- **web**
  - Build: `node:20-alpine`, entrega estáticos con `nginx:alpine`
  - Puerto contenedor `80` mapeado al host `WEB_PORT`

---

## 🛠️ Troubleshooting

- **No veo datos iniciales**  
  El log de Postgres dice *“Skipping initialization”*. Borra el volumen o carga el SQL manualmente (ver sección *Base de datos y seed*).

- **CORS / `Failed to fetch`**  
  Verifica `ALLOWED_ORIGINS`, y revisa `docker logs api`. Un 500 interno puede parecer CORS en el navegador.  
  Prueba con:
  ```bash
  curl -i -H "Origin: http://localhost:8081" http://localhost:8080/api/servicios
  ```

- **`ERR_INCOMPLETE_CHUNKED_ENCODING`**  
  Se corrige usando `UseExceptionHandler` y evitando modificar headers después de iniciar la respuesta (ya implementado).

- **El puerto 80 está ocupado (web)**  
  Cambia `WEB_PORT` en `.env` (p.ej., `WEB_PORT=8081`) y reconstruye `web`.

---

## ☁️ Despliegue en Azure VM (resumen)

1. Crear VM Linux con Docker/Compose.
2. Copiar repo y `.env`.
3. Abrir puertos en el NSG: `API_PORT`, `WEB_PORT` (y `5432` sólo si necesitas acceso externo).
4. Poner `PUBLIC_HOST` con el **IP/Dominio público**.
5. `docker compose up -d`.
6. (Opcional) Servicio `systemd` para auto-arranque.

> Para producción: usar **migraciones EF** (no `EnsureCreated`), HTTPS/Reverse Proxy, backups de Postgres y CORS restringido.

---

## 📄 Licencia

Uso educativo y libre para la prueba técnica.

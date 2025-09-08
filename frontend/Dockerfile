# Build
FROM node:20-alpine AS build
WORKDIR /app

# Copiamos manifiestos
COPY package*.json ./

# Inyecta la URL del API en build
ARG VITE_API_URL
ENV VITE_API_URL=${VITE_API_URL}

# Si hay package-lock.json -> npm ci; si no -> npm install
RUN if [ -f package-lock.json ]; then npm ci; else npm install; fi

# Copia el resto y construye
COPY . .
RUN npm run build

# Nginx para servir estáticos
FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80

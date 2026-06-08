# Multi-stage build for Angular apps (Nx monorepo).
#
# Build argument ile hangi app build edileceği seçilir:
#   docker build -f frontend/Dockerfile --build-arg APP=web-app -t SearchConsoleApp-web .
#   docker build -f frontend/Dockerfile --build-arg APP=admin-app -t SearchConsoleApp-admin .
#   docker build -f frontend/Dockerfile --build-arg APP=public-app -t SearchConsoleApp-public .

# === Stage 1: Build ===
FROM node:22-alpine AS build
WORKDIR /workspace

# pnpm/npm cache için package files'ı önce kopyala
COPY frontend/package.json frontend/package-lock.json* ./
# --legacy-peer-deps: @nx/angular ile Angular 21 arasındaki peer dep çakışması
RUN npm ci --legacy-peer-deps

COPY frontend/ .

ARG APP=web-app
RUN npx nx build ${APP} --configuration=production

# === Stage 2: Nginx ===
FROM nginx:alpine AS runtime

# Nginx config — SPA için fallback (deep link'ler index.html'e)
COPY <<EOF /etc/nginx/conf.d/default.conf
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    # Gzip
    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/javascript;

    # Static assets — long cache
    location ~* \.(?:css|js|woff2?|ttf|png|jpg|svg|webp|ico)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    # SPA fallback — Angular router için
    location / {
        try_files \$uri \$uri/ /index.html;
    }

    # Health check
    location /health {
        access_log off;
        return 200 "OK";
        add_header Content-Type text/plain;
    }
}
EOF

ARG APP=web-app
COPY --from=build /workspace/dist/apps/${APP}/browser /usr/share/nginx/html

EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s CMD wget -q --spider http://localhost/health || exit 1

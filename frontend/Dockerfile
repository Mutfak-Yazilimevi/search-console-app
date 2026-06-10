# Multi-stage build for Angular apps (Nx monorepo).
#
# Build context: repo root (.)
#   docker build -f frontend/Dockerfile --build-arg APP=public-app -t search-console-public .
#   docker build -f frontend/Dockerfile --build-arg APP=web-app -t search-console-web .
#   docker build -f frontend/Dockerfile --build-arg APP=admin-app -t search-console-admin .

# === Stage 1: Build ===
FROM node:22-alpine AS build
WORKDIR /repo

COPY frontend/package.json frontend/package-lock.json* ./frontend/
WORKDIR /repo/frontend
RUN npm ci --legacy-peer-deps

COPY frontend/ ./
COPY docs/seo-rules /repo/docs/seo-rules
COPY tools/generate-rule-catalog.mjs /repo/tools/generate-rule-catalog.mjs

ARG APP=web-app
RUN if [ "$APP" = "public-app" ]; then npm run generate:rules; fi
RUN npx nx build ${APP} --configuration=docker

# === Stage 2: Nginx ===
FROM nginx:alpine AS runtime

COPY <<EOF /etc/nginx/conf.d/default.conf
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/javascript;

    location ~* \.(?:css|js|woff2?|ttf|png|jpg|svg|webp|ico)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    location / {
        try_files \$uri \$uri/ /index.html;
    }

    location /health {
        access_log off;
        return 200 "OK";
        add_header Content-Type text/plain;
    }
}
EOF

ARG APP=web-app
COPY --from=build /repo/frontend/dist/apps/${APP}/browser /usr/share/nginx/html

EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s CMD wget -q --spider http://localhost/health || exit 1

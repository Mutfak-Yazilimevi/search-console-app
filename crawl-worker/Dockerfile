FROM mcr.microsoft.com/playwright:v1.49.1-noble

WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm ci --omit=dev || npm install --omit=dev

COPY tsconfig.json ./
COPY src ./src
RUN npm install typescript && npm run build && npm uninstall typescript

ENV NODE_ENV=production
ENV PLAYWRIGHT_ENABLED=true
ENV PLAYWRIGHT_MAX_PAGES=20

EXPOSE 3100
CMD ["node", "dist/index.js"]

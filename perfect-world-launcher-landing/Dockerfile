# Multi-stage Dockerfile for perfect-world-launcher-landing (Vite + vite-plugin-ssr prerender)

# 1) Build stage: install deps and build static assets
FROM node:20-alpine AS build
WORKDIR /app

# Install dependencies first (better caching)
COPY package*.json ./
RUN npm install

# Copy the rest of the source and build
COPY . .
RUN npm run build

# 2) Runtime stage: serve static files with Nginx
FROM nginx:1.25-alpine AS runtime

# Copy build output to Nginx's web root
COPY --from=build /app/dist /usr/share/nginx/html

# Run Nginx in the foreground
CMD ["nginx", "-g", "daemon off;"]

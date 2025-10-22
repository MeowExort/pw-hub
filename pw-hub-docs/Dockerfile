
FROM node:20-alpine AS build

WORKDIR /app

COPY package.json ./
COPY package-lock.json ./

RUN npm install

COPY . ./

RUN npm run build

# 2) Runtime stage: serve static files with Nginx
FROM nginx:alpine

# Copy build output to Nginx's web root
COPY --from=build /app/dist /usr/share/nginx/html
COPY docs.pw-hub.pw.conf /etc/nginx/conf.d/default.conf

# Run Nginx in the foreground
CMD ["nginx", "-g", "daemon off;"]

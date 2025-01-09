FROM oven/bun:1 AS build
WORKDIR /app

# Copy only package.json first
COPY package.json ./

# Install dependencies
RUN bun install

# Copy source and build
COPY . .
RUN bun run build

# Production environment
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
FROM oven/bun:1 AS frontend
WORKDIR /app
COPY frontend/app/package.json frontend/app/bun.lockb ./
RUN bun install --frozen-lockfile
COPY frontend/app/ .
RUN bun run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-source
WORKDIR /src
COPY backend/ .
RUN dotnet restore MovieComparison.sln

# Only runs when asked for: docker build --target backend-test .
FROM backend-source AS backend-test
RUN dotnet test MovieComparison.sln --no-restore

FROM backend-source AS backend
RUN dotnet publish src/MovieComparison.Api/MovieComparison.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS app
WORKDIR /app
COPY --from=backend /app .
COPY --from=frontend /app/dist ./wwwroot
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "MovieComparison.Api.dll"]

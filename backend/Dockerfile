FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first
COPY ["src/MovieComparison.Api/MovieComparison.Api.csproj", "src/MovieComparison.Api/"]
COPY ["src/MovieComparison.Core/MovieComparison.Core.csproj", "src/MovieComparison.Core/"]
COPY ["src/MovieComparison.Infrastructure/MovieComparison.Infrastructure.csproj", "src/MovieComparison.Infrastructure/"]

# Restore packages
RUN dotnet restore "src/MovieComparison.Api/MovieComparison.Api.csproj"

# Copy everything else
COPY . .

# Build and publish
RUN dotnet publish "src/MovieComparison.Api/MovieComparison.Api.csproj" -c Release -o /app

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "MovieComparison.Api.dll"]
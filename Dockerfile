# Stage 1: Build React frontend
FROM node:22-alpine AS frontend-build
WORKDIR /app/frontend
COPY frontend/package*.json ./
RUN npm install
COPY frontend/ ./
RUN npm run build
# vite.config.ts: outDir: '../CaixaDiario.API/wwwroot' → /app/CaixaDiario.API/wwwroot

# Stage 2: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app/CaixaDiario.API
COPY CaixaDiario.API/*.csproj ./
RUN dotnet restore
COPY CaixaDiario.API/ ./
COPY --from=frontend-build /app/CaixaDiario.API/wwwroot ./wwwroot
RUN dotnet publish -c Release -o /app/out

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .
COPY --from=frontend-build /app/CaixaDiario.API/wwwroot ./wwwroot
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CaixaDiario.API.dll"]

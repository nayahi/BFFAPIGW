# Etapa 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar archivo de proyecto y restaurar dependencias
COPY BFFAPIGW.csproj .
RUN dotnet restore "BFFAPIGW.csproj"

# Copiar el resto del código fuente
COPY . .

# Compilar la aplicación
RUN dotnet build "BFFAPIGW.csproj" -c Release -o /app/build

# Publicar la aplicación
FROM build AS publish
RUN dotnet publish "BFFAPIGW.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Exponer puerto 5000
EXPOSE 5000

# Copiar archivos publicados desde la etapa de build
COPY --from=publish /app/publish .

# Configurar punto de entrada
ENTRYPOINT ["dotnet", "BFFAPIGW.dll"]

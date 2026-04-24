# Etapa 1: compilar y publicar la Web API con todas sus capas.
# Usamos multi-stage build para que la imagen final quede más pequeña.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos primero los csproj para aprovechar la cache de restore.
COPY ["Appointment.API/Appointment.Api.csproj", "Appointment.API/"]
COPY ["Appointment.Infrastructure/Appointment.Infrastructure.csproj", "Appointment.Infrastructure/"]
COPY ["Appointment.Application/Appointment.Application.csproj", "Appointment.Application/"]
COPY ["Appointment.Domain/Appointment.Domain.csproj", "Appointment.Domain/"]
COPY ["Appointment.CrossCutting/Appointment.CrossCutting.csproj", "Appointment.CrossCutting/"]

RUN dotnet restore "Appointment.API/Appointment.Api.csproj"

COPY . .
WORKDIR "/src/Appointment.API"
RUN dotnet publish "Appointment.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: imagen final solo con runtime ASP.NET Core.
# Esta es la imagen que subiremos después a ECR para ECS Fargate.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Fargate expondrá este puerto dentro del contenedor.
# Luego el ALB lo consumirá y publicará HTTPS hacia afuera.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Appointment.Api.dll"]

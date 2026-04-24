# Appointment

Proyecto `Appointment.Api` en .NET 8.

## Qué hace

La API principal expone endpoints HTTP y además ejecuta un worker interno:

- `POST /appointments`
- `GET /appointments/{insuredId}`
- worker que consume `appointment-completion`

Flujo esperado:

1. La API guarda la cita en SQL Server con estado `Pending`
2. Publica `AppointmentRequested` a SNS
3. SNS enruta a la cola por país
4. Lambda PE o CL procesa y publica `AppointmentCompleted`
5. EventBridge envía ese evento a `appointment-completion`
6. El worker interno de la API consume esa cola
7. La API actualiza SQL Server a `Completed`

## Ejecución local

Desde `C:\Repositorios\NET AWS\Appointment`

```powershell
dotnet build Appointment.backend.sln
dotnet run --project Appointment.API\Appointment.Api.csproj
```

## Docker

Desde `C:\Repositorios\NET AWS\Appointment`

```powershell
docker build -t appointment-api:fase3 .
docker run -p 8080:8080 appointment-api:fase3
```

## Configuración principal

### Base de datos

- `AppointmentConnectionString`
- `Database__EnsureCreatedOnStartup`

### Swagger

- `Swagger__PublicEnabled`

Regla actual:

- En `Development`, Swagger siempre está activo
- En otros entornos, Swagger solo se expone si `Swagger__PublicEnabled=true`
- La UI queda en `/swagger`

### AWS

- `AWS__Region`
- `AWS__Sns__AppointmentTopicArn`
- `AWS__Sqs__CompletionQueueUrl`
- `AWS__Sqs__CompletionMaxMessages`
- `AWS__Sqs__CompletionWaitTimeSeconds`
- `AWS__Sqs__CompletionIdleDelaySeconds`

## Endpoints

- `GET /health`
- `POST /appointments`
- `GET /appointments/{insuredId}`
- `GET /swagger` si Swagger está habilitado

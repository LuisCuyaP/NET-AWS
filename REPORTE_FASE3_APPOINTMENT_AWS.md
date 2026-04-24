# Reporte Detallado Fase 3

## 1. Objetivo del trabajo

El objetivo fue llevar la solución `Appointment.Api` desde un proyecto .NET local a una API funcional en AWS, manteniendo la arquitectura ya definida:

Cliente -> Appointment.Api -> SQL Server -> SNS -> SQS por país -> Lambda PE/CL -> DynamoDB -> EventBridge -> SQS completion -> worker de Appointment.Api -> SQL Server

Además del despliegue, el objetivo fue dejar documentado:

- qué se revisó
- qué ya existía
- qué faltaba
- qué cambios se hicieron
- qué fallos aparecieron
- cómo se corrigieron

Este reporte está escrito para una persona que no necesita conocer AWS previamente.

## 2. Estado inicial encontrado

### Workspace revisado

Ruta base del workspace:

`C:\Repositorios\NET AWS`

Proyectos principales:

- `Appointment`
- `Appointment.Pe`
- `Appointment.Cl`
- `infra-pulumi`

### Lo que ya existía correctamente

- `Appointment.Api` ya publicaba a SNS y consumía desde SQS completion
- `Appointment.Pe` y `Appointment.Cl` ya consumían SQS, persistían en DynamoDB y publicaban a EventBridge
- `infra-pulumi` ya tenía SNS, SQS, DynamoDB, Lambdas y EventBridge
- Fase 2.5 ya estaba validada end-to-end en AWS

### Lo que faltaba para Fase 3

- Dockerfile para `Appointment.Api`
- despliegue de la API en AWS
- infraestructura ECS Fargate
- balanceador público para probar la API
- SQL Server administrado en AWS
- wiring final con variables reales

## 3. Revisión técnica inicial de Appointment.Api

Se revisaron estos archivos clave:

- `C:\Repositorios\NET AWS\Appointment\Appointment.API\Program.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.API\DependencyInjection.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.Infrastructure\DependencyInjection.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.Infrastructure\Messaging\SnsAppointmentPublisher.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.Infrastructure\Messaging\SqsCompletionConsumer.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.Infrastructure\HostedServices\CompletionQueueBackgroundService.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.API\appsettings.json`

Conclusiones:

- La API no usa controllers MVC; usa minimal endpoints
- El publisher SNS ya existía
- El worker SQS ya existía
- La app ya leía configuración compatible con variables de entorno
- No existía Dockerfile
- No existía health check
- No existía despliegue ECS/Fargate en Pulumi

## 4. Cambios aplicados en Appointment.Api

### 4.1 Preparación para contenedor

Se creó:

- `C:\Repositorios\NET AWS\Appointment\Dockerfile`
- `C:\Repositorios\NET AWS\Appointment\.dockerignore`

Propósito:

- construir una imagen Docker reproducible
- publicar la API como contenedor Linux
- preparar la imagen para ECR y ECS Fargate

### 4.2 Health check

Se agregó:

- `C:\Repositorios\NET AWS\Appointment\Appointment.API\EndPoints\Health\GetHealth.cs`

Propósito:

- dar un endpoint simple para que el ALB verifique si la aplicación está viva
- facilitar pruebas rápidas por navegador o Postman

### 4.3 Soporte para ALB / reverse proxy

Se modificaron:

- `C:\Repositorios\NET AWS\Appointment\Appointment.API\Program.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.API\DependencyInjection.cs`

Propósito:

- aceptar headers `X-Forwarded-*`
- permitir que la API funcione correctamente detrás del ALB

### 4.4 Inicialización de base de datos

Se modificaron:

- `C:\Repositorios\NET AWS\Appointment\Appointment.API\Program.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.Infrastructure\DependencyInjection.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.API\appsettings.json`

Propósito:

- permitir que la API cree automáticamente base y esquema cuando arranca por primera vez en AWS
- evitar que el despliegue dependa de migraciones inexistentes en el workspace

### 4.5 Swagger controlado por configuración

Se modificaron:

- `C:\Repositorios\NET AWS\Appointment\Appointment.API\Program.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.API\DependencyInjection.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.API\appsettings.json`

Qué hace ahora:

- en `Development`, Swagger está activo
- en AWS u otros entornos, solo se publica si `Swagger__PublicEnabled=true`
- la UI quedó en `/swagger`

Esto evita publicar documentación sensible por accidente.

## 5. Cambios en Appointment.Pe y Appointment.Cl

Se completaron keys faltantes en:

- `C:\Repositorios\NET AWS\Appointment.Pe\Appointment.Pe.Lambda\appsettings.json`
- `C:\Repositorios\NET AWS\Appointment.Cl\Appointment.Cl.Lambda\appsettings.json`

Valores agregados:

- tabla DynamoDB real
- bus EventBridge `default`

También se agregaron comentarios en:

- `C:\Repositorios\NET AWS\Appointment.Pe\Appointment.Pe.Infrastructure\DependencyInjection.cs`
- `C:\Repositorios\NET AWS\Appointment.Cl\Appointment.Cl.Infrastructure\DependencyInjection.cs`
- `C:\Repositorios\NET AWS\Appointment.Pe\Appointment.Pe.Lambda\Function.cs`
- `C:\Repositorios\NET AWS\Appointment.Cl\Appointment.Cl.Lambda\Function.cs`

Propósito:

- dejar explícito qué hace cada binding AWS
- facilitar la comprensión del flujo para revisión técnica

## 6. Cambios en infra-pulumi

Se extendió:

- `C:\Repositorios\NET AWS\infra-pulumi\index.ts`

### Recursos agregados para Fase 3

- ECR repository para la imagen de la API
- ALB público
- target group y listener
- ECS cluster
- ECS service
- task definition
- IAM task role
- IAM execution role
- CloudWatch log group
- Secrets Manager secret para connection string
- RDS SQL Server
- security groups
- subnet group de RDS

### Variables inyectadas al contenedor

- `Database__EnsureCreatedOnStartup=true`
- `Swagger__PublicEnabled=false`
- `AWS__Region`
- `AWS__Sns__AppointmentTopicArn`
- `AWS__Sqs__CompletionQueueUrl`
- variables de polling SQS
- `AppointmentConnectionString` desde secret

### Outputs agregados

- `appointmentApiBaseUrl`
- `appointmentApiHealthUrl`
- `appointmentApiSwaggerPublicEnabled`
- `appointmentApiSwaggerUrl`
- `appointmentApiEcrRepositoryUrl`
- `appointmentApiImageUri`
- `appointmentApiClusterName`
- `appointmentApiServiceName`
- `appointmentApiSqlServerEndpoint`

## 7. Comandos ejecutados y por qué

Esta sección resume los comandos más importantes ejecutados durante el trabajo.

### 7.1 Exploración del workspace

Desde `C:\Repositorios\NET AWS`

```powershell
Get-ChildItem -Force
rg --files
```

Propósito:

- identificar proyectos y archivos relevantes

### 7.2 Revisión de código

Desde `C:\Repositorios\NET AWS`

```powershell
Get-Content Appointment\Appointment.API\Program.cs
Get-Content Appointment\Appointment.Infrastructure\DependencyInjection.cs
Get-Content Appointment\Appointment.Infrastructure\Messaging\SnsAppointmentPublisher.cs
Get-Content Appointment\Appointment.Infrastructure\Messaging\SqsCompletionConsumer.cs
Get-Content Appointment\Appointment.Infrastructure\HostedServices\CompletionQueueBackgroundService.cs
Get-Content infra-pulumi\index.ts
```

Propósito:

- entender el diseño actual antes de cambiar nada

### 7.3 Compilación local .NET

Desde `C:\Repositorios\NET AWS\Appointment`

```powershell
dotnet build Appointment.backend.sln
```

Desde `C:\Repositorios\NET AWS\Appointment.Pe`

```powershell
dotnet build Appointment.Pe.Lambda\Appointment.Pe.Lambda.csproj
```

Desde `C:\Repositorios\NET AWS\Appointment.Cl`

```powershell
dotnet build Appointment.Cl.Lambda\Appointment.Cl.Lambda.csproj
```

Propósito:

- validar que los cambios no rompieran compilación

### 7.4 Validación del contenedor

Desde `C:\Repositorios\NET AWS\Appointment`

```powershell
docker build -t appointment-api:fase3 .
docker run -d --name appointment-api-fase3-test -p 8080:8080 appointment-api:fase3
Invoke-WebRequest http://localhost:8080/health
docker rm -f appointment-api-fase3-test
```

Propósito:

- confirmar que la API levantaba correctamente en Docker antes de ir a AWS

### 7.5 Pulumi stack y outputs

Desde `C:\Repositorios\NET AWS\infra-pulumi`

```powershell
pulumi stack ls
pulumi stack output
pulumi stack output --json
pulumi config
```

Propósito:

- obtener valores reales ya existentes en AWS
- reutilizar topic ARN, queue URL y demás recursos del stack

### 7.6 Configuración del stack

Desde `C:\Repositorios\NET AWS\infra-pulumi`

```powershell
pulumi config set sqlAdminUsername appointadmin
pulumi config set --secret sqlAdminPassword <password>
```

Propósito:

- definir las credenciales de la base RDS en el stack

### 7.7 Validación TypeScript de Pulumi

Desde `C:\Repositorios\NET AWS\infra-pulumi`

```powershell
npx tsc --noEmit
pulumi preview
pulumi up --yes
```

Propósito:

- validar tipado
- ver qué recursos se crearían
- aplicar la infraestructura

### 7.8 Publicación manual de imagen a ECR

Desde `C:\Repositorios\NET AWS`

```powershell
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 031088524212.dkr.ecr.us-east-1.amazonaws.com
docker tag appointment-api:fase3 031088524212.dkr.ecr.us-east-1.amazonaws.com/appointment-api:fase3
docker push 031088524212.dkr.ecr.us-east-1.amazonaws.com/appointment-api:fase3
```

Propósito:

- subir la imagen a ECR para que ECS pudiera descargarla

### 7.9 Verificación del servicio ECS

Desde `C:\Repositorios\NET AWS\infra-pulumi`

```powershell
aws ecs describe-services --cluster appointment-api-cluster --services appointment-api-service --region us-east-1
```

Propósito:

- verificar si la tarea Fargate estaba `pending`, `running` o estable

### 7.10 Verificación de la API desplegada

Desde `C:\Repositorios\NET AWS\infra-pulumi`

```powershell
Invoke-WebRequest http://appointment-api-alb-737476242.us-east-1.elb.amazonaws.com/health
Invoke-RestMethod -Method Post -Uri http://appointment-api-alb-737476242.us-east-1.elb.amazonaws.com/appointments ...
Invoke-RestMethod -Method Get -Uri http://appointment-api-alb-737476242.us-east-1.elb.amazonaws.com/appointments/54321
```

Propósito:

- validar que la API funcionaba realmente desde el ALB

### 7.11 Verificación de logs y colas

Desde `C:\Repositorios\NET AWS\infra-pulumi`

```powershell
aws logs describe-log-streams --log-group-name /aws/lambda/appointment-pe-lambda --region us-east-1
aws logs get-log-events --log-group-name /aws/lambda/appointment-pe-lambda --log-stream-name <stream> --region us-east-1
aws sqs get-queue-attributes --queue-url <queue-url> --attribute-names ApproximateNumberOfMessages ApproximateNumberOfMessagesNotVisible --region us-east-1
aws dynamodb get-item --table-name pe-appointments-table --key ...
```

Propósito:

- diagnosticar por qué una cita no llegaba a `Completed`

## 8. Problemas encontrados y correcciones

### Problema 1. Pulumi falló construyendo imagen con `awsx:ecr:Image`

Síntoma:

- `pulumi up` falló con timeout del builder interno de Docker

Causa:

- el recurso `awsx:ecr:Image` dependía de un builder que no arrancó bien en este entorno

Corrección:

- se quitó el build inline de Pulumi
- se dejó una URI fija `appointment-api:fase3`
- la imagen se construyó y publicó manualmente con Docker + ECR

### Problema 2. Docker falló exportando una imagen en un intento

Síntoma:

- error de snapshot inexistente al exportar imagen

Causa:

- inconsistencia temporal del cache local de Docker Desktop

Corrección:

- se reutilizó la imagen local ya cargada
- luego se repitió build/push correctamente

### Problema 3. La cita quedaba en `Pending`

Síntoma:

- `POST /appointments` funcionaba
- `GET /appointments/{insuredId}` devolvía la cita, pero seguía en `Pending`

Diagnóstico:

- los logs de `appointment-pe-lambda` mostraron:
  - `Validation.General`
  - `One or more validation errors occurred`

Causa real:

- `Appointment.Api` publicaba `eventName` en el body del mensaje
- las Lambdas PE/CL esperaban `eventType`

Archivo corregido:

- `C:\Repositorios\NET AWS\Appointment\Appointment.Infrastructure\Messaging\SnsAppointmentPublisher.cs`

Corrección:

- se cambió el payload de negocio de `eventName` a `eventType`
- se mantuvo `eventName` solo como message attribute

Resultado:

- la Lambda PE procesó correctamente
- EventBridge publicó `AppointmentCompleted`
- el worker de la API actualizó SQL Server a `Completed`

## 9. Validación final

### URL final de la API

`http://appointment-api-alb-737476242.us-east-1.elb.amazonaws.com`

### Endpoints finales

- Health:
  - `GET /health`
- Crear cita:
  - `POST /appointments`
- Consultar citas:
  - `GET /appointments/{insuredId}`
- Swagger:
  - `GET /swagger` si está habilitado por configuración

### Prueba end-to-end validada

Se probó:

```json
{
  "insuredId": "54321",
  "scheduleId": 200,
  "countryISO": "PE"
}
```

Resultado:

- el `POST` devolvió `Pending`
- la Lambda PE procesó correctamente
- la consulta posterior devolvió la misma cita en `Completed`

## 10. Archivos principales modificados

- `C:\Repositorios\NET AWS\Appointment\Dockerfile`
- `C:\Repositorios\NET AWS\Appointment\.dockerignore`
- `C:\Repositorios\NET AWS\Appointment\Appointment.API\Program.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.API\DependencyInjection.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.API\appsettings.json`
- `C:\Repositorios\NET AWS\Appointment\Appointment.API\EndPoints\Health\GetHealth.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.Infrastructure\DependencyInjection.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.Infrastructure\Messaging\SnsAppointmentPublisher.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.Infrastructure\Messaging\SqsCompletionConsumer.cs`
- `C:\Repositorios\NET AWS\Appointment\Appointment.Infrastructure\HostedServices\CompletionQueueBackgroundService.cs`
- `C:\Repositorios\NET AWS\Appointment.Pe\Appointment.Pe.Lambda\appsettings.json`
- `C:\Repositorios\NET AWS\Appointment.Cl\Appointment.Cl.Lambda\appsettings.json`
- `C:\Repositorios\NET AWS\Appointment.Pe\Appointment.Pe.Infrastructure\DependencyInjection.cs`
- `C:\Repositorios\NET AWS\Appointment.Cl\Appointment.Cl.Infrastructure\DependencyInjection.cs`
- `C:\Repositorios\NET AWS\Appointment.Pe\Appointment.Pe.Lambda\Function.cs`
- `C:\Repositorios\NET AWS\Appointment.Cl\Appointment.Cl.Lambda\Function.cs`
- `C:\Repositorios\NET AWS\infra-pulumi\index.ts`
- `C:\Repositorios\NET AWS\infra-pulumi\README.md`
- `C:\Repositorios\NET AWS\Appointment\README.md`

## 11. Recomendaciones siguientes

Para endurecer la solución después del reto:

- agregar DLQ o estrategia de reintento controlado para `appointment-completion`
- agregar migraciones EF Core en vez de depender de `EnsureCreated`
- mover Swagger detrás de autenticación o dejarlo solo en entornos no productivos
- separar el worker SQS de la API HTTP si se quisiera escalar horizontalmente la API
- agregar observabilidad más rica en CloudWatch para el worker de completion

# infra-pulumi

Infraestructura AWS del reto técnico Appointment.

Este proyecto Pulumi crea y mantiene dos bloques:

1. La arquitectura orientada a eventos validada en Fase 2.5
2. El despliegue de `Appointment.Api` en AWS para Fase 3

## Qué crea este stack

### Fase 1 / 2.5

- SNS Topic `appointment-topic`
- SQS por país:
  - `appointment-pe`
  - `appointment-cl`
  - `appointment-completion`
- DLQ para PE y CL
- Policies SNS -> SQS
- Suscripciones SNS con filtro por `countryISO`
- DynamoDB:
  - `pe-appointments-table`
  - `cl-appointments-table`
- Lambdas:
  - `appointment-pe-lambda`
  - `appointment-cl-lambda`
- Event source mappings SQS -> Lambda
- EventBridge rule para `AppointmentCompleted`
- EventBridge target -> `appointment-completion`

### Fase 3

- ECR repository `appointment-api`
- ECS Cluster `appointment-api-cluster`
- ECS Service `appointment-api-service`
- ALB público para exponer la API
- CloudWatch Log Group `/ecs/appointment-api`
- IAM execution role y task role para ECS
- Secret en Secrets Manager con la connection string
- RDS SQL Server `appointment-api-sqlserver`

## Configuración requerida

### Config ya usada en este workspace

- `aws:region = us-east-1`
- `sqlAdminUsername = appointadmin`
- `sqlAdminPassword = <secret>`

### Comandos Pulumi de configuración

Desde `C:\Repositorios\NET AWS\infra-pulumi`

```powershell
pulumi config set sqlAdminUsername appointadmin
pulumi config set --secret sqlAdminPassword <tu-password-seguro>
```

## Cómo desplegar

### 1. Publicar la imagen Docker de Appointment.Api en ECR

Desde `C:\Repositorios\NET AWS`

```powershell
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 031088524212.dkr.ecr.us-east-1.amazonaws.com
```

Desde `C:\Repositorios\NET AWS\Appointment`

```powershell
docker build -t appointment-api:fase3 .
docker tag appointment-api:fase3 031088524212.dkr.ecr.us-east-1.amazonaws.com/appointment-api:fase3
docker push 031088524212.dkr.ecr.us-east-1.amazonaws.com/appointment-api:fase3
```

### 2. Aplicar infraestructura

Desde `C:\Repositorios\NET AWS\infra-pulumi`

```powershell
pulumi preview
pulumi up --yes
```

## Variables que consume Appointment.Api en ECS

Estas variables se inyectan en el task definition:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://+:8080`
- `Database__EnsureCreatedOnStartup=true`
- `Swagger__PublicEnabled=false`
- `AWS__Region=us-east-1`
- `AWS__Sns__AppointmentTopicArn=<topic arn>`
- `AWS__Sqs__CompletionQueueUrl=<completion queue url>`
- `AWS__Sqs__CompletionMaxMessages=10`
- `AWS__Sqs__CompletionWaitTimeSeconds=20`
- `AWS__Sqs__CompletionIdleDelaySeconds=2`
- `AppointmentConnectionString` desde Secrets Manager

## Outputs importantes

Puedes verlos con:

```powershell
pulumi stack output
```

Outputs relevantes de Fase 3:

- `appointmentApiBaseUrl`
- `appointmentApiHealthUrl`
- `appointmentApiSwaggerPublicEnabled`
- `appointmentApiSwaggerUrl`
- `appointmentApiEcrRepositoryUrl`
- `appointmentApiImageUri`
- `appointmentApiClusterName`
- `appointmentApiServiceName`
- `appointmentApiSqlServerEndpoint`

Outputs relevantes de la arquitectura event-driven:

- `snsTopicArn`
- `peQueueUrl`
- `clQueueUrl`
- `completionQueueUrl`
- `peTableName`
- `clTableName`
- `peLambdaName`
- `clLambdaName`

## Endpoints de la API desplegada

- Health: `GET {appointmentApiBaseUrl}/health`
- Crear cita: `POST {appointmentApiBaseUrl}/appointments`
- Consultar citas: `GET {appointmentApiBaseUrl}/appointments/{insuredId}`
- Swagger: `GET {appointmentApiSwaggerUrl}`

Nota:
Swagger está deshabilitado por defecto en AWS porque `appointmentApiSwaggerPublicEnabled=false`.
Si lo quieres público, cambia `Swagger__PublicEnabled` a `true` en el task definition y vuelve a desplegar.

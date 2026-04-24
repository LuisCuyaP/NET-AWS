import * as pulumi from "@pulumi/pulumi";
import * as aws from "@pulumi/aws";
import * as path from "path";

/**
 * ============================================================
 * CONFIGURACIÓN GENERAL
 * ============================================================
 *
 * En este bloque resolvemos las rutas físicas de los archivos ZIP
 * que contienen el código publicado de cada Lambda.
 *
 * ¿Por qué es necesario?
 * Porque Pulumi necesita saber de dónde tomar el artefacto
 * que subirá a AWS Lambda.
 *
 * En este caso:
 * - Appointment.Pe genera un publish.zip
 * - Appointment.Cl genera un publish.zip
 *
 * Esos ZIP ya contienen el código compilado y listo para ejecutar.
 *
 * Importancia en el flujo end-to-end:
 * Sin estos archivos, Pulumi podría crear la Lambda como recurso,
 * pero no tendría el código real que debe ejecutar cuando llegue
 * un mensaje desde SQS.
 */
const peLambdaZipPath = path.resolve(
    process.cwd(),
    "..",
    "Appointment.Pe",
    "Appointment.Pe.Lambda",
    "publish.zip"
);

const clLambdaZipPath = path.resolve(
    process.cwd(),
    "..",
    "Appointment.Cl",
    "Appointment.Cl.Lambda",
    "publish.zip"
);

/**
 * Ruta del proyecto Appointment.Api.
 *
 * La reutilizaremos en Fase 3 para construir la imagen Docker
 * que se publicará en ECR y luego correrá en ECS Fargate.
 */
const appointmentApiProjectPath = path.resolve(
    process.cwd(),
    "..",
    "Appointment"
);

/**
 * Configuración del stack para Fase 3.
 *
 * Dejamos únicamente los valores que sí deben poder variar por stack:
 * - usuario administrador de SQL Server
 * - password del motor
 *
 * El resto de nombres de recursos se dejan explícitos para mantener
 * consistencia con el reto técnico.
 */
const stackConfig = new pulumi.Config();
const awsRegion = aws.config.region ?? "us-east-1";
const sqlAdminUsername = stackConfig.get("sqlAdminUsername") ?? "appointadmin";
const sqlAdminPassword = stackConfig.requireSecret("sqlAdminPassword");

/**
 * ============================================================
 * FASE 1 - MENSAJERÍA Y PERSISTENCIA BASE
 * ============================================================
 *
 * En esta fase construimos la base de la arquitectura orientada
 * a eventos:
 *
 * 1. Un Topic SNS donde entra el evento principal
 * 2. Colas SQS por país
 * 3. DLQ para tolerancia a fallos
 * 4. Cola de completion para el evento final
 * 5. Suscripciones SNS -> SQS
 * 6. Tablas DynamoDB para persistencia por país
 *
 * Esta fase es la columna vertebral del flujo distribuido.
 */

/**
 * SNS Topic principal.
 *
 * ¿Qué hace?
 * Es el punto de entrada del evento AppointmentRequested.
 *
 * ¿Quién publica aquí?
 * Appointment.Api.
 *
 * ¿Por qué usar SNS?
 * Porque SNS permite publicar una vez y distribuir ese mensaje
 * a múltiples consumidores/suscriptores.
 *
 * En este reto técnico:
 * - la API publica el evento una sola vez
 * - SNS decide a qué cola debe enviarlo según filtros
 *
 * Importancia en el flujo:
 * Aquí inicia el flujo end-to-end.
 *
 * Flujo:
 * Appointment.Api -> SNS Topic
 */
const appointmentTopic = new aws.sns.Topic("appointment-topic", {
    name: "appointment-topic",
});

/**
 * Dead Letter Queue de PE.
 *
 * ¿Qué hace?
 * Si un mensaje falla varias veces en la cola principal de PE,
 * termina en esta DLQ.
 *
 * ¿Por qué es importante?
 * Porque evita perder mensajes y ayuda a diagnosticar errores.
 *
 * Ejemplo:
 * Si la Lambda de PE falla 3 veces procesando el mismo mensaje,
 * ese mensaje no queda reintentándose eternamente:
 * se envía a la DLQ para revisión.
 *
 * Importancia en el flujo:
 * Aporta resiliencia y observabilidad.
 */
const appointmentPeDlq = new aws.sqs.Queue("appointment-pe-dlq", {
    name: "appointment-pe-dlq",
});

/**
 * Dead Letter Queue de CL.
 *
 * Mismo propósito que la de PE, pero para el flujo de Chile.
 */
const appointmentClDlq = new aws.sqs.Queue("appointment-cl-dlq", {
    name: "appointment-cl-dlq",
});

/**
 * Cola principal de PE.
 *
 * ¿Qué hace?
 * Recibe únicamente los mensajes AppointmentRequested
 * cuyo countryISO sea PE.
 *
 * ¿Por qué existe?
 * Porque desacopla la publicación del procesamiento.
 * La API no llama directamente a la Lambda de PE.
 * En vez de eso:
 * - publica un evento
 * - SNS lo enruta a esta cola
 * - la Lambda consume desde aquí
 *
 * Configuración importante:
 * - visibilityTimeoutSeconds: 60
 *   Si una Lambda empieza a procesar un mensaje, el mensaje se oculta
 *   por 60 segundos para que no sea consumido simultáneamente.
 *
 * - messageRetentionSeconds: 345600
 *   El mensaje se conserva hasta 4 días si no se elimina.
 *
 * - redrivePolicy
 *   Si falla demasiadas veces, se manda a la DLQ de PE.
 *
 * Importancia en el flujo:
 * SNS -> appointment-pe -> Lambda PE
 */
const appointmentPeQueue = new aws.sqs.Queue("appointment-pe", {
    name: "appointment-pe",
    visibilityTimeoutSeconds: 60,
    messageRetentionSeconds: 345600,
    redrivePolicy: appointmentPeDlq.arn.apply(dlqArn =>
        JSON.stringify({
            deadLetterTargetArn: dlqArn,
            maxReceiveCount: 3,
        })
    ),
});

/**
 * Cola principal de CL.
 *
 * Mismo concepto que PE, pero para mensajes con countryISO = CL.
 *
 * Importancia en el flujo:
 * SNS -> appointment-cl -> Lambda CL
 */
const appointmentClQueue = new aws.sqs.Queue("appointment-cl", {
    name: "appointment-cl",
    visibilityTimeoutSeconds: 60,
    messageRetentionSeconds: 345600,
    redrivePolicy: appointmentClDlq.arn.apply(dlqArn =>
        JSON.stringify({
            deadLetterTargetArn: dlqArn,
            maxReceiveCount: 3,
        })
    ),
});

/**
 * Cola de completion.
 *
 * ¿Qué hace?
 * Aquí termina llegando el evento AppointmentCompleted
 * después de que la Lambda de PE o CL procesa la cita.
 *
 * ¿Quién escribe aquí?
 * EventBridge.
 *
 * ¿Quién consumirá de aquí?
 * La API principal (o su worker interno / background service).
 *
 * ¿Por qué es importante?
 * Porque desacopla el procesamiento por país del sistema central.
 * La Lambda no actualiza SQL Server directamente.
 * En su lugar:
 * - publica un evento final
 * - EventBridge lo enruta
 * - la API consume este resultado después
 *
 * Importancia en el flujo:
 * EventBridge -> appointment-completion -> Appointment.Api Worker
 */
const appointmentCompletionQueue = new aws.sqs.Queue("appointment-completion", {
    name: "appointment-completion",
    visibilityTimeoutSeconds: 60,
    messageRetentionSeconds: 345600,
});

/**
 * Función helper para crear la policy de SQS que permite a SNS
 * enviar mensajes a la cola.
 *
 * Esto es CRÍTICO.
 *
 * ¿Por qué?
 * No basta con crear una suscripción SNS -> SQS.
 * La cola SQS también necesita autorizar explícitamente que SNS
 * le envíe mensajes.
 *
 * Si esta policy no existe o está mal:
 * - SNS recibe el mensaje
 * - la suscripción existe
 * - el filtro coincide
 * PERO la cola rechaza el envío
 *
 * Eso fue exactamente el problema detectado en tu validación.
 *
 * Puntos importantes de la policy:
 * - Principal: sns.amazonaws.com
 *   indica que el servicio SNS puede enviar a la cola
 *
 * - Action: sqs:SendMessage
 *   permiso mínimo necesario para publicar en la cola
 *
 * - Resource: queueArn
 *   la cola exacta a la que aplica el permiso
 *
 * - Condition / aws:SourceArn = topicArn
 *   restringe el permiso para que SOLO ese topic SNS pueda enviar
 *
 * FIX IMPORTANTE:
 * Aquí usamos pulumi.all([queue.arn, topic.arn]) porque ambos valores
 * son Output de Pulumi.
 * Si no los resolvemos correctamente, la policy queda con texto inválido
 * en vez del ARN real.
 *
 * Importancia en el flujo:
 * Hace posible el tramo:
 * SNS -> SQS
 */
function createSnsToSqsPolicy(
    name: string,
    queue: aws.sqs.Queue,
    topic: aws.sns.Topic
) {
    return new aws.sqs.QueuePolicy(name, {
        queueUrl: queue.url,
        policy: pulumi
            .all([queue.arn, topic.arn])
            .apply(([queueArn, topicArn]) =>
                JSON.stringify({
                    Version: "2012-10-17",
                    Statement: [
                        {
                            Sid: "AllowSnsSendMessage",
                            Effect: "Allow",
                            Principal: {
                                Service: "sns.amazonaws.com",
                            },
                            Action: "sqs:SendMessage",
                            Resource: queueArn,
                            Condition: {
                                ArnEquals: {
                                    "aws:SourceArn": topicArn,
                                },
                            },
                        },
                    ],
                })
            ),
    });
}

/**
 * Policy para permitir:
 * appointment-topic -> appointment-pe
 */
createSnsToSqsPolicy("appointment-pe-policy", appointmentPeQueue, appointmentTopic);

/**
 * Policy para permitir:
 * appointment-topic -> appointment-cl
 */
createSnsToSqsPolicy("appointment-cl-policy", appointmentClQueue, appointmentTopic);

/**
 * Suscripción SNS -> SQS para PE.
 *
 * ¿Qué hace?
 * Conecta el topic SNS con la cola PE.
 *
 * ¿Qué significa rawMessageDelivery: true?
 * Que el body se entrega tal cual, sin envelope extra de SNS.
 *
 * ¿Qué hace el filterPolicy?
 * Solo deja pasar mensajes cuyo message attribute countryISO sea PE.
 *
 * Es decir:
 * - si publicas countryISO=PE -> llega a appointment-pe
 * - si publicas countryISO=CL -> no llega a esta cola
 *
 * Importancia en el flujo:
 * Aquí ocurre el enrutamiento por país.
 */
new aws.sns.TopicSubscription("appointment-pe-sub", {
    topic: appointmentTopic.arn,
    protocol: "sqs",
    endpoint: appointmentPeQueue.arn,
    rawMessageDelivery: true,
    filterPolicy: JSON.stringify({
        countryISO: ["PE"],
    }),
});

/**
 * Suscripción SNS -> SQS para CL.
 *
 * Mismo concepto, pero solo para mensajes con countryISO=CL.
 */
new aws.sns.TopicSubscription("appointment-cl-sub", {
    topic: appointmentTopic.arn,
    protocol: "sqs",
    endpoint: appointmentClQueue.arn,
    rawMessageDelivery: true,
    filterPolicy: JSON.stringify({
        countryISO: ["CL"],
    }),
});

/**
 * Tabla DynamoDB para PE.
 *
 * ¿Qué hace?
 * Guarda las citas procesadas por la Lambda de Perú.
 *
 * ¿Por qué DynamoDB?
 * Porque encaja muy bien en arquitectura serverless:
 * - alta disponibilidad
 * - baja operación
 * - pago por uso
 *
 * La PK es AppointmentId.
 *
 * Importancia en el flujo:
 * Lambda PE -> DynamoDB PE
 *
 * Esto demuestra que el mensaje no solo fue consumido,
 * sino que se persistió correctamente.
 */
const peAppointmentsTable = new aws.dynamodb.Table("pe-appointments-table", {
    name: "pe-appointments-table",
    billingMode: "PAY_PER_REQUEST",
    hashKey: "AppointmentId",
    attributes: [
        {
            name: "AppointmentId",
            type: "S",
        },
    ],
});

/**
 * Tabla DynamoDB para CL.
 *
 * Mismo concepto que PE, pero para Chile.
 *
 * Importancia en el flujo:
 * Lambda CL -> DynamoDB CL
 */
const clAppointmentsTable = new aws.dynamodb.Table("cl-appointments-table", {
    name: "cl-appointments-table",
    billingMode: "PAY_PER_REQUEST",
    hashKey: "AppointmentId",
    attributes: [
        {
            name: "AppointmentId",
            type: "S",
        },
    ],
});

/**
 * ============================================================
 * FASE 2 - IAM, LAMBDAS, SQS TRIGGERS Y EVENTBRIDGE
 * ============================================================
 *
 * En esta fase agregamos el procesamiento real:
 *
 * - IAM Role
 * - permisos
 * - Lambdas
 * - triggers desde SQS
 * - EventBridge para el evento final
 *
 * Aquí es donde el sistema "cobra vida".
 */

/**
 * Role base de ejecución para las Lambdas.
 *
 * ¿Qué hace?
 * Es la identidad que usarán las Lambdas al ejecutarse.
 *
 * ¿Por qué existe?
 * Porque una Lambda no puede acceder automáticamente a CloudWatch,
 * SQS, DynamoDB o EventBridge.
 * Necesita un rol IAM que le conceda esos permisos.
 *
 * La assumeRolePolicy indica que el servicio Lambda puede asumir este rol.
 */
const lambdaExecutionRole = new aws.iam.Role("appointment-lambda-role", {
    name: "appointment-lambda-role",
    assumeRolePolicy: aws.iam.assumeRolePolicyForPrincipal({
        Service: "lambda.amazonaws.com",
    }),
});

/**
 * Attachment: permisos básicos para logs en CloudWatch.
 *
 * ¿Por qué es importante?
 * Sin esto, la Lambda no puede escribir logs.
 *
 * En validación end-to-end:
 * Si no tienes logs, se vuelve mucho más difícil depurar.
 *
 * Importancia:
 * Lambda -> CloudWatch Logs
 */
new aws.iam.RolePolicyAttachment("lambda-basic-execution-role-attachment", {
    role: lambdaExecutionRole.name,
    policyArn: "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole",
});

/**
 * Attachment: permisos para que Lambda consuma mensajes desde SQS.
 *
 * ¿Por qué es importante?
 * Porque la Lambda necesita:
 * - recibir mensajes
 * - leer atributos
 * - borrar mensajes ya procesados
 *
 * Si faltan estos permisos:
 * SQS puede tener mensajes, pero la Lambda no podrá procesarlos.
 */
new aws.iam.RolePolicyAttachment("lambda-sqs-execution-role-attachment", {
    role: lambdaExecutionRole.name,
    policyArn: "arn:aws:iam::aws:policy/service-role/AWSLambdaSQSQueueExecutionRole",
});

/**
 * Policy personalizada para:
 * - leer/escribir en DynamoDB
 * - publicar eventos en EventBridge
 *
 * ¿Por qué no basta con policies administradas?
 * Porque aquí necesitamos permisos específicos para tus tablas
 * y para emitir eventos del dominio.
 *
 * Permisos otorgados:
 * - dynamodb:PutItem
 * - dynamodb:GetItem
 * - dynamodb:UpdateItem
 * - dynamodb:DescribeTable
 *
 * y además:
 * - events:PutEvents
 *
 * Importancia en el flujo:
 * Lambda -> DynamoDB
 * Lambda -> EventBridge
 */
const lambdaCustomPolicy = new aws.iam.RolePolicy("appointment-lambda-custom-policy", {
    role: lambdaExecutionRole.id,
    policy: pulumi
        .all([
            peAppointmentsTable.arn,
            clAppointmentsTable.arn,
        ])
        .apply(([peTableArn, clTableArn]) =>
            JSON.stringify({
                Version: "2012-10-17",
                Statement: [
                    {
                        Sid: "AllowDynamoDbAccess",
                        Effect: "Allow",
                        Action: [
                            "dynamodb:PutItem",
                            "dynamodb:GetItem",
                            "dynamodb:UpdateItem",
                            "dynamodb:DescribeTable",
                        ],
                        Resource: [peTableArn, clTableArn],
                    },
                    {
                        Sid: "AllowPutEventsToEventBridge",
                        Effect: "Allow",
                        Action: [
                            "events:PutEvents",
                        ],
                        Resource: "*",
                    },
                ],
            })
        ),
});

/**
 * Lambda de PE.
 *
 * ¿Qué hace?
 * Consume mensajes desde la cola appointment-pe,
 * valida que correspondan a PE,
 * persiste en DynamoDB
 * y luego publica AppointmentCompleted a EventBridge.
 *
 * Handler:
 * Apunta a tu método FunctionHandler dentro del proyecto Lambda.
 *
 * runtime: dotnet8
 * Porque tu Lambda está construida sobre .NET 8.
 *
 * timeout: 30
 * Tiempo máximo de ejecución.
 *
 * memorySize: 512
 * Memoria asignada; también impacta CPU disponible.
 *
 * environment.variables:
 * Le pasan configuración al código:
 * - región AWS
 * - nombre de la tabla DynamoDB
 * - nombre del bus de EventBridge
 * - source del evento
 * - detail-type del evento
 *
 * Importancia en el flujo:
 * appointment-pe queue -> Lambda PE -> DynamoDB -> EventBridge
 */
const appointmentPeLambda = new aws.lambda.Function("appointment-pe-lambda", {
    name: "appointment-pe-lambda",
    runtime: "dotnet8",
    role: lambdaExecutionRole.arn,
    handler: "Appointment.Pe.Lambda::Appointment.Pe.Lambda.Function::FunctionHandler",
    code: new pulumi.asset.FileArchive(peLambdaZipPath),
    timeout: 30,
    memorySize: 512,
    environment: {
        variables: {
            AWS__Region: "us-east-1",
            AWS__PeAppointmentsTableName: peAppointmentsTable.name,
            AWS__EventBridgeBusName: "default",
            AWS__EventBridgeSource: "appointment-pe",
            AWS__EventBridgeDetailType: "AppointmentCompleted",
        },
    },
}, {
    dependsOn: [lambdaCustomPolicy],
});

/**
 * Lambda de CL.
 *
 * Mismo patrón que PE, pero para citas de Chile.
 *
 * Importancia en el flujo:
 * appointment-cl queue -> Lambda CL -> DynamoDB -> EventBridge
 */
const appointmentClLambda = new aws.lambda.Function("appointment-cl-lambda", {
    name: "appointment-cl-lambda",
    runtime: "dotnet8",
    role: lambdaExecutionRole.arn,
    handler: "Appointment.Cl.Lambda::Appointment.Cl.Lambda.Function::FunctionHandler",
    code: new pulumi.asset.FileArchive(clLambdaZipPath),
    timeout: 30,
    memorySize: 512,
    environment: {
        variables: {
            AWS__Region: "us-east-1",
            AWS__ClAppointmentsTableName: clAppointmentsTable.name,
            AWS__EventBridgeBusName: "default",
            AWS__EventBridgeSource: "appointment-cl",
            AWS__EventBridgeDetailType: "AppointmentCompleted",
        },
    },
}, {
    dependsOn: [lambdaCustomPolicy],
});

/**
 * Event Source Mapping: SQS -> Lambda PE
 *
 * ¿Qué hace?
 * Conecta la cola appointment-pe con la Lambda PE.
 *
 * Cuando llegan mensajes a la cola:
 * AWS invoca automáticamente la Lambda.
 *
 * batchSize: 10
 * Lambda puede recibir hasta 10 mensajes en una sola invocación.
 *
 * functionResponseTypes: ReportBatchItemFailures
 * Permite reportar fallos parciales por mensaje en procesamiento por lotes.
 *
 * Importancia en el flujo:
 * Hace automático el tramo:
 * SQS PE -> Lambda PE
 */
const peEventSourceMapping = new aws.lambda.EventSourceMapping("appointment-pe-sqs-trigger", {
    eventSourceArn: appointmentPeQueue.arn,
    functionName: appointmentPeLambda.arn,
    batchSize: 10,
    functionResponseTypes: ["ReportBatchItemFailures"],
});

/**
 * Event Source Mapping: SQS -> Lambda CL
 *
 * Igual concepto que PE.
 */
const clEventSourceMapping = new aws.lambda.EventSourceMapping("appointment-cl-sqs-trigger", {
    eventSourceArn: appointmentClQueue.arn,
    functionName: appointmentClLambda.arn,
    batchSize: 10,
    functionResponseTypes: ["ReportBatchItemFailures"],
});

/**
 * Regla de EventBridge para AppointmentCompleted.
 *
 * ¿Qué hace?
 * Escucha los eventos emitidos por las Lambdas de PE y CL.
 *
 * eventPattern:
 * - source: appointment-pe o appointment-cl
 * - detail-type: AppointmentCompleted
 *
 * Es decir:
 * Solo captura los eventos finales de procesamiento.
 *
 * ¿Por qué usar EventBridge aquí?
 * Porque desacopla la producción del evento final de su consumo posterior.
 *
 * La Lambda no necesita conocer a quién le importa el resultado.
 * Solo emite el evento.
 * EventBridge se encarga del ruteo.
 *
 * Importancia en el flujo:
 * Lambda -> EventBridge
 */
const appointmentCompletedRule = new aws.cloudwatch.EventRule("appointment-completed-rule", {
    name: "appointment-completed-rule",
    eventPattern: JSON.stringify({
        source: ["appointment-pe", "appointment-cl"],
        "detail-type": ["AppointmentCompleted"],
    }),
});

/**
 * Policy para permitir que EventBridge escriba en la cola completion.
 *
 * Igual que con SNS -> SQS, aquí SQS también necesita autorizar
 * al servicio que le enviará mensajes.
 *
 * En este caso:
 * - Principal: events.amazonaws.com
 * - Action: sqs:SendMessage
 * - Condition: solo desde esta regla de EventBridge
 *
 * Importancia en el flujo:
 * EventBridge -> appointment-completion
 */
const completionQueuePolicy = new aws.sqs.QueuePolicy("appointment-completion-policy", {
    queueUrl: appointmentCompletionQueue.url,
    policy: pulumi
        .all([appointmentCompletionQueue.arn, appointmentCompletedRule.arn])
        .apply(([queueArn, ruleArn]) =>
            JSON.stringify({
                Version: "2012-10-17",
                Statement: [
                    {
                        Sid: "AllowEventBridgeSendMessage",
                        Effect: "Allow",
                        Principal: {
                            Service: "events.amazonaws.com",
                        },
                        Action: "sqs:SendMessage",
                        Resource: queueArn,
                        Condition: {
                            ArnEquals: {
                                "aws:SourceArn": ruleArn,
                            },
                        },
                    },
                ],
            })
        ),
});

/**
 * Target de EventBridge hacia la cola completion.
 *
 * ¿Qué hace?
 * Define que todo evento que haga match con la regla
 * appointment-completed-rule será enviado a appointment-completion.
 *
 * Es el puente final entre el evento de negocio y la cola que consumirá
 * la API central.
 *
 * Importancia en el flujo:
 * AppointmentCompleted -> EventBridge Rule -> appointment-completion
 */
const appointmentCompletedTarget = new aws.cloudwatch.EventTarget("appointment-completed-target", {
    rule: appointmentCompletedRule.name,
    arn: appointmentCompletionQueue.arn,
});

/**
 * ============================================================
 * FASE 3 - APPOINTMENT.API EN ECS FARGATE
 * ============================================================
 *
 * En esta fase incorporamos la API principal al landscape AWS.
 *
 * Objetivo:
 * - exponer Appointment.Api por HTTP para pruebas en Postman
 * - conectar la API con SNS
 * - conectar su worker interno con SQS completion
 * - provisionar una base SQL Server administrada en RDS
 * - mantener el diseño existente: una sola API con worker embebido
 *
 * Decisión pragmática para este reto:
 * - usamos VPC default y subnets default para reducir fricción
 * - dejamos el ALB público para probar desde Postman
 * - dejamos la base RDS no pública y aislada por security groups
 * - mantenemos desiredCount = 1 porque el worker SQS vive dentro
 *   del mismo proceso de la API
 */

/**
 * Descubrimos la VPC default y sus subnets.
 *
 * ¿Por qué usar la VPC default?
 * Porque permite avanzar rápido sin rehacer networking completo
 * y es suficiente para este reto técnico.
 *
 * Tradeoff:
 * En un entorno productivo normalmente modelaríamos VPC privada,
 * subnets privadas para ECS/RDS, NAT Gateways, etc.
 */
const defaultVpc = aws.ec2.getVpcOutput({
    default: true,
});

const defaultSubnets = aws.ec2.getSubnetsOutput({
    filters: [
        {
            name: "vpc-id",
            values: [defaultVpc.id],
        },
    ],
});

/**
 * Repositorio ECR para la imagen de Appointment.Api.
 *
 * ¿Qué hace?
 * Guarda la imagen que ECS Fargate descargará para arrancar
 * el contenedor real de la API.
 */
const appointmentApiRepository = new aws.ecr.Repository("appointment-api-repository", {
    name: "appointment-api",
    imageScanningConfiguration: {
        scanOnPush: true,
    },
    forceDelete: true,
});

/**
 * Política de lifecycle del repositorio ECR.
 *
 * ¿Por qué?
 * Evita que el reto acumule imágenes antiguas sin control.
 */
new aws.ecr.LifecyclePolicy("appointment-api-repository-lifecycle", {
    repository: appointmentApiRepository.name,
    policy: JSON.stringify({
        rules: [
            {
                rulePriority: 1,
                description: "Keep last 10 images",
                selection: {
                    tagStatus: "any",
                    countType: "imageCountMoreThan",
                    countNumber: 10,
                },
                action: {
                    type: "expire",
                },
            },
        ],
    }),
});

/**
 * URI final de la imagen que correrá la API.
 *
 * Nota importante:
 * Inicialmente se intentó usar awsx:ecr:Image para build/push inline,
 * pero en este entorno el builder interno agotó timeout al arrancar.
 *
 * Por eso fijamos un tag estable (`fase3`) y hacemos el build/push con
 * Docker + AWS CLI fuera de Pulumi. Pulumi sigue gestionando el resto
 * de la infraestructura y consume esta URI ya publicada en ECR.
 */
const appointmentApiContainerImageUri = pulumi.interpolate`${appointmentApiRepository.repositoryUrl}:fase3`;

/**
 * Security group del ALB público.
 *
 * Permite tráfico HTTP desde internet para que puedas probar
 * la API desde Postman.
 */
const appointmentApiAlbSecurityGroup = new aws.ec2.SecurityGroup("appointment-api-alb-sg", {
    name: "appointment-api-alb-sg",
    description: "Public HTTP access to Appointment API ALB",
    vpcId: defaultVpc.id,
    ingress: [
        {
            protocol: "tcp",
            fromPort: 80,
            toPort: 80,
            cidrBlocks: ["0.0.0.0/0"],
        },
    ],
    egress: [
        {
            protocol: "-1",
            fromPort: 0,
            toPort: 0,
            cidrBlocks: ["0.0.0.0/0"],
        },
    ],
});

/**
 * Security group de las tareas ECS.
 *
 * Solo acepta tráfico desde el ALB hacia el puerto 8080,
 * que es el puerto expuesto por el contenedor ASP.NET Core.
 */
const appointmentApiServiceSecurityGroup = new aws.ec2.SecurityGroup("appointment-api-service-sg", {
    name: "appointment-api-service-sg",
    description: "ALB to ECS access for Appointment API",
    vpcId: defaultVpc.id,
    ingress: [
        {
            protocol: "tcp",
            fromPort: 8080,
            toPort: 8080,
            securityGroups: [appointmentApiAlbSecurityGroup.id],
        },
    ],
    egress: [
        {
            protocol: "-1",
            fromPort: 0,
            toPort: 0,
            cidrBlocks: ["0.0.0.0/0"],
        },
    ],
});

/**
 * Security group de RDS SQL Server.
 *
 * La base solo acepta conexiones desde el security group
 * de las tareas ECS. De esa forma evitamos exponer SQL Server
 * directamente a internet.
 */
const appointmentApiDatabaseSecurityGroup = new aws.ec2.SecurityGroup("appointment-api-db-sg", {
    name: "appointment-api-db-sg",
    description: "ECS tasks to SQL Server RDS",
    vpcId: defaultVpc.id,
    ingress: [
        {
            protocol: "tcp",
            fromPort: 1433,
            toPort: 1433,
            securityGroups: [appointmentApiServiceSecurityGroup.id],
        },
    ],
    egress: [
        {
            protocol: "-1",
            fromPort: 0,
            toPort: 0,
            cidrBlocks: ["0.0.0.0/0"],
        },
    ],
});

/**
 * ALB público de la API.
 *
 * Este será el punto de entrada para:
 * Cliente/Postman -> ALB -> ECS Task -> Appointment.Api
 */
const appointmentApiLoadBalancer = new aws.lb.LoadBalancer("appointment-api-alb", {
    name: "appointment-api-alb",
    internal: false,
    loadBalancerType: "application",
    securityGroups: [appointmentApiAlbSecurityGroup.id],
    subnets: defaultSubnets.ids,
});

/**
 * Target group del ALB.
 *
 * targetType = ip porque Fargate usa ENIs propias por tarea.
 *
 * healthCheck:
 * Se apoya en /health, que agregamos en Appointment.Api
 * específicamente para esta fase.
 */
const appointmentApiTargetGroup = new aws.lb.TargetGroup("appointment-api-target-group", {
    name: "appoint-api-tg",
    port: 8080,
    protocol: "HTTP",
    targetType: "ip",
    vpcId: defaultVpc.id,
    healthCheck: {
        path: "/health",
        protocol: "HTTP",
        matcher: "200-399",
        healthyThreshold: 2,
        unhealthyThreshold: 3,
        timeout: 5,
        interval: 30,
    },
});

/**
 * Listener HTTP del ALB.
 *
 * Mantenemos HTTP para facilitar la prueba del reto desde Postman.
 * Si después quieres endurecerlo, aquí agregaríamos certificado ACM
 * y listener HTTPS.
 */
const appointmentApiListener = new aws.lb.Listener("appointment-api-listener", {
    loadBalancerArn: appointmentApiLoadBalancer.arn,
    port: 80,
    protocol: "HTTP",
    defaultActions: [
        {
            type: "forward",
            targetGroupArn: appointmentApiTargetGroup.arn,
        },
    ],
});

/**
 * Subnet group para RDS.
 *
 * RDS necesita saber en qué subnets puede vivir la instancia.
 * Reutilizamos las subnets de la VPC default para no abrir
 * un frente nuevo de networking en este paso.
 */
const appointmentApiDbSubnetGroup = new aws.rds.SubnetGroup("appointment-api-db-subnet-group", {
    name: "appointment-api-db-subnet-group",
    subnetIds: defaultSubnets.ids,
});

/**
 * Instancia RDS SQL Server.
 *
 * ¿Por qué SQL Server?
 * Porque Appointment.Api ya está implementada con EF Core + SqlServer
 * y el objetivo es mantener coherencia con el diseño existente.
 *
 * Decisión de reto:
 * - motor sqlserver-ex para reducir costo/operación
 * - almacenamiento mínimo
 * - acceso solo desde ECS
 *
 * Nota importante:
 * La solución no tiene migraciones formales, por eso la API arrancará
 * con Database:EnsureCreatedOnStartup=true para crear base y esquema
 * al primer inicio.
 */
const appointmentApiDatabase = new aws.rds.Instance("appointment-api-sqlserver", {
    identifier: "appointment-api-sqlserver",
    engine: "sqlserver-ex",
    instanceClass: "db.t3.small",
    allocatedStorage: 20,
    maxAllocatedStorage: 100,
    storageType: "gp2",
    username: sqlAdminUsername,
    password: sqlAdminPassword,
    port: 1433,
    dbSubnetGroupName: appointmentApiDbSubnetGroup.name,
    vpcSecurityGroupIds: [appointmentApiDatabaseSecurityGroup.id],
    publiclyAccessible: false,
    backupRetentionPeriod: 0,
    skipFinalSnapshot: true,
    deletionProtection: false,
    multiAz: false,
    applyImmediately: true,
}, {
    dependsOn: [appointmentApiDbSubnetGroup],
});

/**
 * Secret de Secrets Manager para la connection string.
 *
 * ¿Por qué usar secret?
 * Porque la connection string contiene credenciales y no conviene
 * dejarla plana dentro del task definition.
 *
 * El ECS task execution role leerá este secreto al arrancar
 * el contenedor.
 */
const appointmentApiConnectionStringSecret = new aws.secretsmanager.Secret("appointment-api-connection-string-secret", {
    name: "appointment-api-connection-string",
    description: "SQL Server connection string for Appointment.Api ECS task",
});

const appointmentApiConnectionStringSecretVersion = new aws.secretsmanager.SecretVersion("appointment-api-connection-string-secret-version", {
    secretId: appointmentApiConnectionStringSecret.id,
    secretString: pulumi
        .all([
            appointmentApiDatabase.address,
            appointmentApiDatabase.port,
            sqlAdminPassword,
        ])
        .apply(([address, port, password]) =>
            `Server=${address},${port};Database=APPOINTMENT;User Id=${sqlAdminUsername};Password=${password};Encrypt=True;TrustServerCertificate=True;`
        ),
});

/**
 * Cluster ECS donde correrá Appointment.Api.
 */
const appointmentApiCluster = new aws.ecs.Cluster("appointment-api-cluster", {
    name: "appointment-api-cluster",
});

/**
 * Log group para la API.
 *
 * Centraliza logs HTTP, logs del publisher SNS y logs del worker SQS
 * dentro de CloudWatch Logs.
 */
const appointmentApiLogGroup = new aws.cloudwatch.LogGroup("appointment-api-log-group", {
    name: "/ecs/appointment-api",
    retentionInDays: 7,
});

/**
 * Execution role del task.
 *
 * Este rol lo usa el agente de ECS para:
 * - descargar la imagen desde ECR
 * - escribir logs en CloudWatch
 * - leer secrets desde Secrets Manager
 */
const appointmentApiExecutionRole = new aws.iam.Role("appointment-api-execution-role", {
    name: "appointment-api-execution-role",
    assumeRolePolicy: aws.iam.assumeRolePolicyForPrincipal({
        Service: "ecs-tasks.amazonaws.com",
    }),
});

new aws.iam.RolePolicyAttachment("appointment-api-execution-role-managed-policy", {
    role: appointmentApiExecutionRole.name,
    policyArn: "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy",
});

new aws.iam.RolePolicy("appointment-api-execution-role-secrets-policy", {
    role: appointmentApiExecutionRole.id,
    policy: appointmentApiConnectionStringSecret.arn.apply(secretArn =>
        JSON.stringify({
            Version: "2012-10-17",
            Statement: [
                {
                    Sid: "AllowReadConnectionStringSecret",
                    Effect: "Allow",
                    Action: [
                        "secretsmanager:GetSecretValue",
                    ],
                    Resource: secretArn,
                },
            ],
        })
    ),
});

/**
 * Task role de la API.
 *
 * Este sí lo usa el código .NET dentro del contenedor.
 *
 * Permisos necesarios:
 * - publicar AppointmentRequested a SNS
 * - consumir appointment-completion desde SQS
 */
const appointmentApiTaskRole = new aws.iam.Role("appointment-api-task-role", {
    name: "appointment-api-task-role",
    assumeRolePolicy: aws.iam.assumeRolePolicyForPrincipal({
        Service: "ecs-tasks.amazonaws.com",
    }),
});

new aws.iam.RolePolicy("appointment-api-task-role-aws-messaging-policy", {
    role: appointmentApiTaskRole.id,
    policy: pulumi
        .all([
            appointmentTopic.arn,
            appointmentCompletionQueue.arn,
        ])
        .apply(([topicArn, completionQueueArn]) =>
            JSON.stringify({
                Version: "2012-10-17",
                Statement: [
                    {
                        Sid: "AllowPublishAppointmentRequested",
                        Effect: "Allow",
                        Action: [
                            "sns:Publish",
                        ],
                        Resource: topicArn,
                    },
                    {
                        Sid: "AllowConsumeCompletionQueue",
                        Effect: "Allow",
                        Action: [
                            "sqs:ReceiveMessage",
                            "sqs:DeleteMessage",
                            "sqs:ChangeMessageVisibility",
                            "sqs:GetQueueAttributes",
                            "sqs:GetQueueUrl",
                        ],
                        Resource: completionQueueArn,
                    },
                ],
            })
        ),
});

/**
 * Task definition de Appointment.Api.
 *
 * Aquí conectamos el código real con la infraestructura real:
 * - imagen desde ECR
 * - puerto 8080
 * - logs
 * - variables de entorno AWS
 * - secret de connection string
 *
 * También activamos EnsureCreatedOnStartup para que el primer arranque
 * cree la base APPOINTMENT y la tabla Appointments si aún no existen.
 */
const appointmentApiTaskDefinition = new aws.ecs.TaskDefinition("appointment-api-task-definition", {
    family: "appointment-api",
    cpu: "512",
    memory: "1024",
    networkMode: "awsvpc",
    requiresCompatibilities: ["FARGATE"],
    executionRoleArn: appointmentApiExecutionRole.arn,
    taskRoleArn: appointmentApiTaskRole.arn,
    containerDefinitions: pulumi
        .all([
            appointmentApiContainerImageUri,
            appointmentApiLogGroup.name,
            appointmentApiConnectionStringSecret.arn,
            appointmentTopic.arn,
            appointmentCompletionQueue.url,
        ])
        .apply(([imageUri, logGroupName, connectionStringSecretArn, topicArn, completionQueueUrl]) =>
            JSON.stringify([
                {
                    name: "appointment-api",
                    image: imageUri,
                    essential: true,
                    portMappings: [
                        {
                            containerPort: 8080,
                            hostPort: 8080,
                            protocol: "tcp",
                        },
                    ],
                    environment: [
                        {
                            name: "ASPNETCORE_ENVIRONMENT",
                            value: "Production",
                        },
                        {
                            name: "ASPNETCORE_URLS",
                            value: "http://+:8080",
                        },
                        {
                            name: "Database__EnsureCreatedOnStartup",
                            value: "true",
                        },
                        {
                            name: "Swagger__PublicEnabled",
                            value: "false",
                        },
                        {
                            name: "AWS__Region",
                            value: awsRegion,
                        },
                        {
                            name: "AWS__Sns__AppointmentTopicArn",
                            value: topicArn,
                        },
                        {
                            name: "AWS__Sqs__CompletionQueueUrl",
                            value: completionQueueUrl,
                        },
                        {
                            name: "AWS__Sqs__CompletionMaxMessages",
                            value: "10",
                        },
                        {
                            name: "AWS__Sqs__CompletionWaitTimeSeconds",
                            value: "20",
                        },
                        {
                            name: "AWS__Sqs__CompletionIdleDelaySeconds",
                            value: "2",
                        },
                    ],
                    secrets: [
                        {
                            name: "AppointmentConnectionString",
                            valueFrom: connectionStringSecretArn,
                        },
                    ],
                    logConfiguration: {
                        logDriver: "awslogs",
                        options: {
                            "awslogs-group": logGroupName,
                            "awslogs-region": awsRegion,
                            "awslogs-stream-prefix": "ecs",
                        },
                    },
                },
            ])
        ),
}, {
    dependsOn: [
        appointmentApiConnectionStringSecretVersion,
    ],
});

/**
 * Servicio ECS Fargate.
 *
 * desiredCount = 1:
 * Lo mantenemos deliberadamente así porque el worker SQS está dentro
 * del mismo proceso de la API. Si escaláramos horizontalmente sin una
 * estrategia adicional, también escalaríamos consumidores SQS.
 */
const appointmentApiService = new aws.ecs.Service("appointment-api-service", {
    name: "appointment-api-service",
    cluster: appointmentApiCluster.arn,
    launchType: "FARGATE",
    desiredCount: 1,
    taskDefinition: appointmentApiTaskDefinition.arn,
    healthCheckGracePeriodSeconds: 300,
    deploymentMinimumHealthyPercent: 0,
    deploymentMaximumPercent: 200,
    networkConfiguration: {
        assignPublicIp: true,
        subnets: defaultSubnets.ids,
        securityGroups: [appointmentApiServiceSecurityGroup.id],
    },
    loadBalancers: [
        {
            targetGroupArn: appointmentApiTargetGroup.arn,
            containerName: "appointment-api",
            containerPort: 8080,
        },
    ],
}, {
    dependsOn: [
        appointmentApiListener,
        appointmentApiTaskDefinition,
        appointmentApiDatabase,
    ],
});

/**
 * ============================================================
 * OUTPUTS
 * ============================================================
 *
 * Los outputs sirven para exponer valores útiles después del deploy.
 *
 * ¿Para qué ayudan?
 * - pruebas manuales con AWS CLI
 * - validación de Fase 2.5
 * - integración futura con Appointment.Api
 * - inspección rápida de recursos sin entrar a consola AWS
 *
 * Por ejemplo:
 * - snsTopicArn lo usas para hacer aws sns publish
 * - peQueueUrl / clQueueUrl para probar colas
 * - nombres de tablas para validar DynamoDB
 * - nombres de Lambdas para ir rápido a revisar logs o métricas
 */
export const snsTopicArn = appointmentTopic.arn;
export const peQueueUrl = appointmentPeQueue.url;
export const clQueueUrl = appointmentClQueue.url;
export const completionQueueUrl = appointmentCompletionQueue.url;
export const peTableName = peAppointmentsTable.name;
export const clTableName = clAppointmentsTable.name;

export const peLambdaName = appointmentPeLambda.name;
export const clLambdaName = appointmentClLambda.name;
export const eventBridgeRuleName = appointmentCompletedRule.name;

export const peZipResolvedPath = peLambdaZipPath;
export const clZipResolvedPath = clLambdaZipPath;
export const peQueueArn = appointmentPeQueue.arn;
export const clQueueArn = appointmentClQueue.arn;
export const completionQueueArn = appointmentCompletionQueue.arn;

export const appointmentApiEcrRepositoryUrl = appointmentApiRepository.repositoryUrl;
export const appointmentApiImageUri = appointmentApiContainerImageUri;
export const appointmentApiClusterName = appointmentApiCluster.name;
export const appointmentApiServiceName = appointmentApiService.name;
export const appointmentApiLoadBalancerDns = appointmentApiLoadBalancer.dnsName;
export const appointmentApiBaseUrl = pulumi.interpolate`http://${appointmentApiLoadBalancer.dnsName}`;
export const appointmentApiHealthUrl = pulumi.interpolate`http://${appointmentApiLoadBalancer.dnsName}/health`;
export const appointmentApiSwaggerPublicEnabled = false;
export const appointmentApiSwaggerUrl = pulumi.interpolate`http://${appointmentApiLoadBalancer.dnsName}/swagger`;
export const appointmentApiSqlServerEndpoint = appointmentApiDatabase.endpoint;
export const appointmentApiSqlServerAddress = appointmentApiDatabase.address;

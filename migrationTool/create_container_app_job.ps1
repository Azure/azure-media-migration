$JOB_NAME="ams-migration-ca-job"
$RESOURCE_GROUP="sloth-rg"
$ENVIRONMENT="ams-migration-cae"

$CONTAINER_REGISTRY_NAME="pandoraacr"
$CONTAINER_IMAGE_NAME="ams-migration-tool:0.0.1"
$SERVICEBUS_NAMESPACE="ams-migration-sbns"

$SERVICEBUS_FQDN="$SERVICEBUS_NAMESPACE.servicebus.windows.net"
$SERVICEBUS_QUEUE="migrate-asset-sbq"
Write-Error -Message "Houston, we have a problem." -ErrorAction Stop

throw "THIS IS WORK IN PROGRESS"

# .env file with the variables:
# QUEUE_CONNECTION_STRING=<value>
# MK_IO_TOKEN=<value>
# APPINSIGHTS_CS=<value>
# COSMOSDB_KEY=<value>
$envFilePath=".\.env"

# Read the file and process each line
Get-Content -Path $envFilePath | ForEach-Object {
    if ($_ -eq "") {
        return
    }

    # Split the line into key and value
    $key, $value = $_.Split('=', 2)

    # Trim any potential whitespace from the key and value  
    $key = $key.Trim()
    $value = $value.Trim()

    # Create a variable with the same name as the key and assign the value to it
    Set-Variable -Name $key -Value $value -Scope Global
}

az containerapp job create `
    --name "$JOB_NAME" `
    --resource-group "$RESOURCE_GROUP" `
    --environment "$ENVIRONMENT" `
    --trigger-type "Event" `
    --replica-timeout "1800" `
    --replica-retry-limit "1" `
    --replica-completion-count "1" `
    --parallelism "1" `
    --min-executions "0" `
    --max-executions "10" `
    --polling-interval "60" `
    --scale-rule-name "azure-servicebus-queue-rule" `
    --scale-rule-type "azure-servicebus" `
    --scale-rule-metadata "queueName=$SERVICEBUS_QUEUE" "namespace=$SERVICEBUS_NAMESPACE" "messageCount=1" `
    --scale-rule-auth "connection=connection-string-secret" `
    --image "$CONTAINER_REGISTRY_NAME.azurecr.io/$CONTAINER_IMAGE_NAME" `
    --cpu "0.5" `
    --memory "1Gi" `
    --secrets "connection-string-secret=$SERVICEBUS_CONNECTION_STRING" `
    --registry-server "$CONTAINER_REGISTRY_NAME.azurecr.io" `
    --env-vars "SERVICEBUS_FQDN=$SERVICEBUS_FQDN" "SERVICEBUS_QUEUE=$SERVICEBUS_QUEUE"


az containerapp job identity assign `
    --name "$JOB_NAME" `
    --resource-group "$RESOURCE_GROUP" `
    --system-assigned

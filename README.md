# AgentApi

AgentApi is a .NET 8 Web API project designed to provide a RESTful interface for interacting with language models using Agent Framework.

## Quick Start with Telemetry

### Prerequisites

- Azure subscription
- Azure CLI installed and logged in
- Docker and Docker Compose installed
- .NET 8 SDK installed

### Azure OpenAI Service

To use Azure OpenAI Service, you need to create an Azure OpenAI resource and deploy a model. You can do this using the Azure CLI.

```bash
# create random resource identifier
RAND=$RANDOM
export RAND
echo "Random resource identifier will be: ${RAND}"

# set resource names
RG_NAME=rg-agentdemo$RAND
AOAI_NAME=oai-agentdemo$RAND

# choose a region that supports the model of your choice
LOCATION=swedencentral

# create resource group
az group create \
--name $RG_NAME \
--location $LOCATION

# create openai account
AOAI_ID=$(az cognitiveservices account create \
--resource-group $RG_NAME \
--location $LOCATION \
--name $AOAI_NAME \
--custom-domain $AOAI_NAME \
--kind OpenAI \
--sku S0 \
--assign-identity \
--query id -o tsv)

# deploy gpt-5-mini model
az cognitiveservices account deployment create \
-n $AOAI_NAME \
-g $RG_NAME \
--deployment-name gpt-5-mini \
--model-name gpt-5-mini \
--model-version 2025-08-07 \
--model-format OpenAI \
--sku-capacity 200 \
--sku-name GlobalStandard

# create managed identity
MI_ID=$(az identity create \
--name oai-agentdemo$RAND-id \
--resource-group $RG_NAME \
--query id -o tsv)

# get managed identity principal id
MI_PRINCIPAL_ID=$(az identity show \
--ids $MI_ID \
--query principalId \
-o tsv)

# assign role to managed identity
az role assignment create \
--role "Cognitive Services OpenAI User" \
--assignee $MI_PRINCIPAL_ID \
--scope $AOAI_ID

# assign role to your account too
az role assignment create \
--role "Cognitive Services OpenAI Contributor" \
--assignee $(az ad signed-in-user show --query id -o tsv) \
--scope $AOAI_ID
```

### Running the Application

1. **Start the telemetry stack** (Loki, Tempo, Prometheus, and OpenTelemetry Collector):

   ```bash
   docker compose up -d
   ```

2. **Run the application**:

   ```bash
   dotnet run
   ```

3. **View telemetry data in Grafana**:

   Open Grafana and use Explore to view traces from Tempo, logs from Loki, and metrics from Prometheus.

The application automatically sends logs, traces, and metrics to the OpenTelemetry Collector (<http://localhost:4317>).

### Stopping the Application

```bash
# Stop the .NET app
Ctrl+C

# Stop the telemetry stack
docker compose down -v
```

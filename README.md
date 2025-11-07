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
read -r MI_ID MI_PRINCIPAL_ID MI_CLIENT_ID <<< \
  "$(az identity create \
    --name $AOAI_NAME-id \
    --resource-group $RG_NAME \
    --query '{id:id, principalId:principalId, clientId:clientId}' -o tsv)"

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

Start the telemetry stack (Loki, Grafana, Tempo, Prometheus, and OpenTelemetry Collector):

```bash
docker compose up -d
```

Write a .env file with the following content:

```bash
cat <<EOF | tee .env
AZURE_OPENAI_ENDPOINT=https://$AOAI_NAME.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-5-mini
EOF
```

Run the application:

```bash
dotnet run
```

View telemetry data in Grafana:

Open Grafana and use Explore to view traces from Tempo, logs from Loki, and metrics from Prometheus.

### Stopping the Application

```bash
# Stop the .NET app
Ctrl+C

# Stop the telemetry stack
docker compose down -v
```

## Azure Kubernetes Service (AKS) Deployment

To deploy the application to Azure Kubernetes Service (AKS), follow these steps:

Create an AKS cluster and enable managed identity:

```bash
AKS_NAME=aks-agentdemo$RAND

read -r AKS_OIDC_ISSUER_URL <<< \
  "$(az aks create \
    --resource-group $RG_NAME \
    --name $AKS_NAME \
    --enable-workload-identity \
    --enable-oidc-issuer \
    --enable-azure-monitor-app-monitoring \
    --query '{oidcIssuerUrl:oidcIssuerProfile.issuerUrl}' -o tsv)"
```

Connect to the AKS cluster:

```bash
az aks get-credentials \
--resource-group $RG_NAME \
--name $AKS_NAME
```

Create a federated identity credential for the AKS cluster's workload identity:

```bash
az identity federated-credential create \
--name agentapi \
--identity-name $AOAI_NAME-id \
--resource-group $RG_NAME \
--issuer $AKS_OIDC_ISSUER_URL \
--subject "system:serviceaccount:demo:agentapi"
```

Build and push the container image:

```bash
IMG=$(uuidgen | tr '[:upper:]' '[:lower:]') 
docker buildx build --platform linux/amd64,linux/arm64 -t ttl.sh/$IMG:4h . --push
```

Make sure you are in the k8s directory:

```bash
cd k8s
```

Edit the kustomization.yaml file to use your image:

```bash
kustomize edit set image agentapi=ttl.sh/$IMG:4h
```

Write a .env file with the following content:

```bash
cat <<EOF | tee .env
AZURE_OPENAI_ENDPOINT=https://$AOAI_NAME.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-5-mini
EOF
```

Deploy to AKS:

```bash
kustomize build . | envsubst | kubectl apply -n demo -f -
```

Port-forward the service to access the API:

```bash
kubectl port-forward service/agentapi -n demo 5258:80
```

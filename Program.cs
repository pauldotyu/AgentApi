using Azure.AI.OpenAI;
using Azure.Identity;
using dotenv.net;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Load environment variables from .env file
DotEnv.Load();

const string ServiceName = "agent-api";
const string ServiceVersion = "1.0.0";
const string SourceName = "agent-telemetry-source";

var builder = WebApplication.CreateBuilder(args);

// Get Azure OpenAI configuration from environment or use defaults
var azureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? "https://your-account.openai.azure.com";
var azureOpenAIDeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? "your-model-deployment";

// Get OTLP endpoint from environment or default
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? "http://localhost:4317";

// Create a resource for the service
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(ServiceName, serviceVersion: ServiceVersion)
    .AddAttributes(new Dictionary<string, object>
    {
        ["service.instance.id"] = Environment.MachineName,
        ["deployment.environment"] = "development"
    });

// Configure OpenTelemetry tracing
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .SetSampler(new AlwaysOnSampler())
    .AddSource(SourceName)
    .AddSource("*Microsoft.Agents.AI")
    .AddHttpClientInstrumentation()
    .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint))
    .Build();

// Configure OpenTelemetry metrics
var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddMeter("*Microsoft.Agents.AI")
    .AddMeter("System.Net.Http")
    .AddRuntimeInstrumentation()
    .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint))
    .Build();

// Configure structured logging with OpenTelemetry
builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(resourceBuilder);
    options.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
});

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Agent API started with OpenTelemetry observability enabled");

var agent = new AzureOpenAIClient(
    new Uri(azureOpenAIEndpoint),
    new DefaultAzureCredential())
    .GetChatClient(azureOpenAIDeploymentName)
    .AsIChatClient()
    .CreateAIAgent(instructions: "You are good at telling jokes.", name: "OpenTelemetryDemoAgent")
    .AsBuilder()
    .UseOpenTelemetry(sourceName: SourceName, configure: cfg => cfg.EnableSensitiveData = true)
    .Build();

app.MapHealthChecks("/health");

app.MapGet("/", async () =>
{
    try
    {
        var result = await agent.RunAsync("Tell me a dad joke about programming.");
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Agent execution failed");
        return Results.StatusCode(500);
    }
});

app.Run();

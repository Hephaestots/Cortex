var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder
    .AddKeycloak("keycloak", 6001)
    .WithDataVolume("keycloak-data")
    .WithRealmImport("./Realms");

// One Postgres server for the whole dev environment, one database per service.
// The volume keeps data across restarts; delete it to start from an empty server.
var postgres = builder
    .AddPostgres("postgres", port: 5432)
    .WithDataVolume("postgres-data")
    .WithPgAdmin();

// The resource name is also the connection string name the service reads
// (ConnectionStrings:questions-db).
var questionsDb = postgres.AddDatabase("questions-db");

// Typesense's admin API key. A secret parameter, so the value never sits in the source: Aspire
// reads it from the AppHost's user secrets (Parameters:typesense-api-key) and asks for it in the
// dashboard when it's missing. The container and the Search service get the same value.
var typesenseApiKey = builder.AddParameter("typesense-api-key", secret: true);

// No Aspire integration for Typesense, so a plain container. Typesense reads TYPESENSE_* variables
// as well as --flags; the key goes through the environment, which keeps it off the command line.
var typesense = builder
    .AddContainer("typesense", "typesense/typesense", "30.2")
    .WithArgs("--data-dir", "/data", "--enable-cors")
    .WithEnvironment("TYPESENSE_API_KEY", typesenseApiKey)
    .WithVolume("typesense-data", "/data")
    // An HTTP endpoint named "http": the third positional argument of WithEndpoint is the URL
    // scheme, not the name, so the old call produced typesense://localhost:8108.
    .WithHttpEndpoint(port: 8108, targetPort: 8108, name: "http")
    // Typesense answers GET /health with {"ok":true} once it can serve, so WaitFor means ready.
    .WithHttpHealthCheck("/health");

var typesenseEndpoint = typesense.GetEndpoint("http");

var questionService = builder
    .AddDotnetProject("question-service", "../Services/Cortex.QuestionService/Cortex.QuestionService.csproj")
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(questionsDb)
    .WaitFor(questionsDb)
    // Without this, "healthy" only means the process started. /health (mapped by
    // MapDefaultEndpoints) answers once migrations and seeding are done and the app listens.
    .WithHttpHealthCheck("/health");

var searchService = builder
    .AddDotnetProject("search-service", "../Services/Cortex.SearchService/Cortex.SearchService.csproj")
    // Service discovery: the service resolves http://typesense (services__typesense__http__0).
    .WithReference(typesenseEndpoint)
    .WithEnvironment("Typesense__ApiKey", typesenseApiKey)
    .WaitFor(typesense)
    .WithHttpHealthCheck("/health");

builder.Build().Run();

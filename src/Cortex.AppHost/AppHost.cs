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

var questionService = builder
    .AddProject<Projects.Cortex_QuestionService>("question-service")
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(questionsDb)
    .WaitFor(questionsDb)
    // Without this, "healthy" only means the process started. /health (mapped by
    // MapDefaultEndpoints) answers once migrations and seeding are done and the app listens.
    .WithHttpHealthCheck("/health");

builder.Build().Run();

var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder
    .AddKeycloak("keycloak", 6001)
    .WithDataVolume("keycloak-data")
    .WithRealmImport("./Realms");

var questionService = builder
    .AddProject<Projects.Cortex_QuestionService>("question-service")
    .WithReference(keycloak)
    .WaitFor(keycloak);

builder.Build().Run();
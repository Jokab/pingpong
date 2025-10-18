using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Provision a PostgreSQL container and a database resource.
// Naming the database resource "DefaultConnection" will inject
// ConnectionStrings:DefaultConnection into the referenced project.
var postgres = builder.AddPostgres("postgres");
var defaultDb = postgres.AddDatabase("DefaultConnection");

// Wire the web app to the database and expose HTTP externally for local access.
builder.AddProject("pingpong-api", "../PingPong.Api/PingPong.Api.csproj")
    .WithReference(defaultDb)
    .WithExternalHttpEndpoints();

builder.Build().Run();



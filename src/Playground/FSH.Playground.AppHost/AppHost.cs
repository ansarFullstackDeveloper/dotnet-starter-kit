using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

// Postgres container + database
var postgres = builder.AddPostgres("postgres").WithDataVolume("fsh-postgres-data").AddDatabase("fsh");

var redis = builder.AddRedis("redis").WithDataVolume("fsh-redis-data");

// Build a plain TCP (non-TLS) Redis connection string using the secondary endpoint.
// Aspire 13.x enables TLS on the primary Redis port by default; the secondary endpoint is plain TCP.
var redisPlainTcp = redis.GetEndpoint("secondary");
var redisConnectionString = ReferenceExpression.Create(
    $"{redisPlainTcp.Property(EndpointProperty.HostAndPort)},password={redis.Resource.PasswordParameter!}");

// API Service
var api = builder.AddProject<Projects.Playground_Api>("playground-api")
    .WithReference(postgres)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("OpenTelemetryOptions__Exporter__Otlp__Endpoint", "https://localhost:4317")
    .WithEnvironment("OpenTelemetryOptions__Exporter__Otlp__Protocol", "grpc")
    .WithEnvironment("OpenTelemetryOptions__Exporter__Otlp__Enabled", "true")
    .WithEnvironment("DatabaseOptions__Provider", "POSTGRESQL")
    .WithEnvironment("DatabaseOptions__ConnectionString", postgres.Resource.ConnectionStringExpression)
    .WithEnvironment("DatabaseOptions__MigrationsAssembly", "FSH.Playground.Migrations.PostgreSQL")
    .WaitFor(postgres)
    .WithReference(redis)
    .WithEnvironment("CachingOptions__Redis", redisConnectionString)
    .WithEnvironment("CachingOptions__EnableSsl", "false")
    .WaitFor(redis);

// Blazor UI
builder.AddProject<Projects.Playground_Blazor>("playground-blazor");

// Admin App (Next.js)
builder.AddJavaScriptApp("fsh-admin", "../../clients/admin", "dev")
    .WithNpm()
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithEnvironment("FSH_API_URL", api.GetEndpoint("http"));

await builder.Build().RunAsync();

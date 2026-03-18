var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("hangfire");

builder.AddProject<Projects.ExecutionFlow_Examples_Producer>("executionflow-examples-producer")
    .WithReference(sql)
    .WaitFor(sql);

builder
    .AddProject<Projects.ExecutionFlow_Examples_ConsumerWithoutDi>("executionflow-examples-consumerwithoutdi")
    .WithReference(sql)
    .WaitFor(sql);

builder.AddProject<Projects.ExecutionFlow_Examples_Consumer>("executionflow-examples-consumer")
    .WithReference(sql)
    .WaitFor(sql);

builder.Build().Run();

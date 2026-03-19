using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Producer.Components;
using ExecutionFlow.Examples.Shared.Events;
using ExecutionFlow.Hangfire;
using ExecutionFlow.Hangfire.DependencyInjection;
using Hangfire;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("hangfire")!;

builder.Services.AddHangfire(config => config.UseSqlServerStorage(connectionString));

builder.Services.AddHangfireToExecutionFlow();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();
app.UseHangfireDashboard(options: new DashboardOptions
{
    DisplayNameFunc = (context, job) => app.Services.GetRequiredService<IHangfireJobName>().GetName(job),
});

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// API endpoint to enqueue messages
app.MapPost("/api/messages", (MessageRequest request, IDispatcher dispatcher) =>
{
    var @event = new SendMessageEvent
    {
        From = request.From,
        Content = request.Content,
        SentAt = DateTime.UtcNow
    };

    var jobId = dispatcher.Publish(@event);

    return Results.Ok(new { jobId, message = "Message enqueued successfully." });
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

record MessageRequest(string From, string Content);

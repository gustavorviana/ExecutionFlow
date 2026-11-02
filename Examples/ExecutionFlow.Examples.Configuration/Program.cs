using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Configuration.Configurations;
using ExecutionFlow.Examples.Configuration.Handlers;
using ExecutionFlow.Hangfire;
using System.Diagnostics;

// Every way to configure recurring handlers, run against HangfireSetup.Configure (no storage needed). Each form prints
// the resolved plan and checks what it should produce; the process exits with 1 if any check fails.
Trace.Listeners.Add(new ConsoleTraceListener());   // shows the configuration warnings

var failures = 0;

void Check(bool condition, string description)
{
    Console.WriteLine($"  {(condition ? "ok  " : "FAIL")} {description}");
    if (!condition) failures++;
}

HangfireSetup Configure(string title, Action<HangfireOptions> configure)
{
    Console.WriteLine();
    Console.WriteLine($"== {title}");
    var setup = new HangfireSetup();
    setup.Configure(configure);
    Print(setup);
    return setup;
}

void Print(HangfireSetup setup)
{
    if (setup.Options.Plan == null)
    {
        Console.WriteLine("  (no plan)");
        return;
    }

    foreach (var step in setup.Options.Plan.Steps)
    {
        var mode = step.RunsOnOwnSchedule ? "own schedule" : step.MinInterval.HasValue ? $"min {step.MinInterval}" : "triggered";
        var after = step.Prerequisites.Count == 0 ? "-" : string.Join(", ", step.Prerequisites.Select(t => t.Name));
        Console.WriteLine($"  {step.DisplayName,-22} enabled={step.Enabled,-5} after={after,-40} mode={mode}");
    }
}

static RecurringJobRegistryInfo Registration<T>(HangfireSetup setup) => setup.RecurringHandlers[typeof(T)];

var assembly = typeof(Program).Assembly;
Func<Type, bool> configurations = t => t.Namespace == typeof(ProductSyncConfiguration).Namespace;

// ---------------------------------------------------------------------------------------------------------------------
// 1. Attributes only: [DependsOn]/[DependentRun] on registered handlers form the automatic plan.
// ---------------------------------------------------------------------------------------------------------------------
var attributes = Configure("1. Attributes only (automatic plan)", o =>
{
    o.Add(typeof(CategorySyncHandler));
    o.Add(typeof(BrandSyncHandler));
    o.Add(typeof(ProductSyncHandler));
    o.Add(typeof(DailyReportHandler));
    o.Add(typeof(PriceAuditHandler));
});
Check(attributes.Options.Plan != null, "an automatic plan was formed from the attributes");
Check(attributes.Options.Plan!.Get<ProductSyncHandler>().Prerequisites.SequenceEqual(new[] { typeof(CategorySyncHandler), typeof(BrandSyncHandler) }),
    "ProductSync waits for CategorySync and BrandSync ([DependsOn])");
Check(attributes.Options.Plan.Get<ProductSyncHandler>().MinInterval == TimeSpan.FromMinutes(20), "ProductSync: MinInterval 20 min ([DependentRun])");
Check(attributes.Options.Plan.Get<DailyReportHandler>().RunsOnOwnSchedule, "DailyReport runs on its own schedule ([DependentRun])");
Check(Registration<BrandSyncHandler>(attributes).Interval == TimeSpan.FromMinutes(90), "BrandSync: 90-minute interval ([RecurringInterval])");
Check(attributes.Options.Plan.Get<CategorySyncHandler>().OverlapTimeout == TimeSpan.FromMinutes(5), "CategorySync: no overlap, 5 min timeout ([NoOverlap])");

// ---------------------------------------------------------------------------------------------------------------------
// 2. Options layer: GetPlanOf<T>() wins over the attributes, item by item (no user plan).
// ---------------------------------------------------------------------------------------------------------------------
var options = Configure("2. GetPlanOf (options layer, automatic plan)", o =>
{
    o.Add(typeof(CategorySyncHandler));
    o.Add(typeof(BrandSyncHandler));
    o.Add(typeof(ProductSyncHandler));
    o.Add(typeof(DailyReportHandler));
    o.Add(typeof(PriceAuditHandler));

    o.GlobalRecurringAutoRun = true;
    o.RecurringTimeZone = "America/Sao_Paulo";
    o.AutomaticPlanMissingPrerequisite = MissingPrerequisite.SkipDependents;

    o.GetPlanOf<PriceAuditHandler>().Enabled(false);
    o.GetPlanOf<CategorySyncHandler>().Cron("*/5 * * * *").NoOverlap(TimeSpan.FromMinutes(2));
    o.GetPlanOf<ProductSyncHandler>()
        .RunMode(DependentRunMode.OwnSchedule)
        .Cron("0 * * * *")                          // OwnSchedule needs a schedule of its own
        .Queue("catalog")
        .AutomaticRetry(3, 60, 300);
});
Check(!options.Options.Plan!.Get<PriceAuditHandler>().Enabled, "PriceAudit turned off by GetPlanOf");
Check(Registration<CategorySyncHandler>(options).Cron == "*/5 * * * *", "CategorySync: cron replaced by GetPlanOf");
Check(Registration<CategorySyncHandler>(options).Id == "category-sync", "CategorySync: ID kept from the attribute");
Check(options.Options.Plan.Get<CategorySyncHandler>().OverlapTimeout == TimeSpan.FromMinutes(2), "CategorySync: overlap timeout 2 min (GetPlanOf)");
Check(options.Options.Plan.Get<ProductSyncHandler>().RunsOnOwnSchedule, "ProductSync: RunMode from GetPlanOf replaces [DependentRun]");
Check(options.Options.Plan.Get<ProductSyncHandler>().Settings.ContainsKey("Hangfire:Queue"), "ProductSync: Hangfire queue set");

// ---------------------------------------------------------------------------------------------------------------------
// 3. User planner, inline. A handler in it ignores its GetPlanOf values as a whole (with a warning).
// ---------------------------------------------------------------------------------------------------------------------
var inlinePlanner = new ExecutionPlanner();
inlinePlanner.Add<CategorySyncHandler>();                               // keeps the attribute's cron, ID and NoOverlap
inlinePlanner.Add<BrandSyncHandler>().Interval(TimeSpan.FromHours(2));
inlinePlanner.Add<ProductSyncHandler>()
    .DependsOn<CategorySyncHandler>()
    .RemoveDependency<BrandSyncHandler>()                              // drops the one from [DependsOn]
    .RunMode(DependentRunMode.MinInterval, TimeSpan.FromMinutes(30))
    .DisplayName("Plan: Product Sync")
    .Queue("catalog");
inlinePlanner.Add<DailyReportHandler>().Enabled(false);
var inlinePlan = inlinePlanner.Build(MissingPrerequisite.Throw);

var inline = Configure("3. Inline planner (user plan)", o =>
{
    o.Add(typeof(CategorySyncHandler));
    o.Add(typeof(BrandSyncHandler));
    o.Add(typeof(ProductSyncHandler));
    o.Add(typeof(DailyReportHandler));
    o.UsePlan(inlinePlan);
    o.GetPlanOf<CategorySyncHandler>().Enabled(false);                 // ignored: CategorySync is in the user plan
});
Check(inline.Options.Plan!.Get<CategorySyncHandler>().Enabled, "CategorySync stays enabled: the user plan replaces GetPlanOf");
Check(inline.Options.Plan.Get<ProductSyncHandler>().Prerequisites.SequenceEqual(new[] { typeof(CategorySyncHandler) }), "ProductSync: BrandSync removed");
Check(inline.Options.Plan.Get<ProductSyncHandler>().MinInterval == TimeSpan.FromMinutes(30), "ProductSync: MinInterval 30 min (planner wins)");
Check(Registration<BrandSyncHandler>(inline).Interval == TimeSpan.FromHours(2), "BrandSync: interval 2 h (planner wins)");
Check(Registration<CategorySyncHandler>(inline).Cron == "*/10 * * * *", "CategorySync: cron kept from the attribute");
Check(!inline.Options.Plan.Get<DailyReportHandler>().Enabled, "DailyReport disabled in the plan");

// ---------------------------------------------------------------------------------------------------------------------
// 4. Configuration classes, applied on an explicit planner and mixed with inline configuration.
// ---------------------------------------------------------------------------------------------------------------------
var classesPlanner = new ExecutionPlanner();
classesPlanner.ApplyConfigurationsFromAssembly(assembly, configurations);
classesPlanner.Add<BrandSyncHandler>();
var classesPlan = classesPlanner.Build();

var classes = Configure("4. Configuration classes + inline", o =>
{
    o.Add(typeof(CategorySyncHandler));
    o.Add(typeof(BrandSyncHandler));
    o.Add(typeof(ProductSyncHandler));
    o.UsePlan(classesPlan);
});
Check(Registration<ProductSyncHandler>(classes).Interval == TimeSpan.FromMinutes(45), "ProductSync: interval 45 min (configuration class)");
Check(Registration<ProductSyncHandler>(classes).Id == "product-sync", "ProductSync: ID from the configuration class");
Check(classes.Options.Plan!.Get<ProductSyncHandler>().Prerequisites.SequenceEqual(new[] { typeof(CategorySyncHandler) }), "ProductSync: ClearDependencies + DependsOn");
Check(classes.Options.Plan.Get<ProductSyncHandler>().RunsOnOwnSchedule, "ProductSync: own schedule, gated by CategorySync");
Check(classes.Options.Plan.Get<CategorySyncHandler>().TimeZone == "Europe/Lisbon", "CategorySync: time zone from its configuration class");
Check(Registration<CategorySyncHandler>(classes).Cron == "*/10 * * * *", "CategorySync: cron kept from the attribute");

// ---------------------------------------------------------------------------------------------------------------------
// 5. Shortcut: a plan made only of configuration classes.
// ---------------------------------------------------------------------------------------------------------------------
var shortcut = Configure("5. UsePlanFromAssembly", o =>
{
    o.Add(typeof(CategorySyncHandler));
    o.Add(typeof(ProductSyncHandler));
    o.UsePlanFromAssembly(assembly, configurations, MissingPrerequisite.Throw);
});
Check(shortcut.Options.Plan!.Steps.Count == 2, "the plan has the two configured handlers");
Check(shortcut.Options.Plan.Get<ProductSyncHandler>().OverlapTimeout == null, "ProductSync: AllowOverlap");

// ---------------------------------------------------------------------------------------------------------------------
// 6. The Consumer's real configuration (Examples/ExecutionFlow.Examples.Handlers/OptionsConfigurator.cs).
// ---------------------------------------------------------------------------------------------------------------------
var consumer = Configure("6. The Consumer's OptionsConfigurator", ExecutionFlow.Examples.Handlers.OptionsConfigurator.Configure);
Check(consumer.Options.Plan != null, "the catalog plan is registered");
Check(consumer.EventHandlers.ContainsKey(typeof(ExecutionFlow.Examples.Shared.Events.OrderCreatedEvent)), "OrderCreatedEvent has a handler");
Check(consumer.Options.StateHandlerTypes.Contains(typeof(ExecutionFlow.Examples.Handlers.Hooks.JobMonitor)), "JobMonitor hooks are registered");

Console.WriteLine();
Console.WriteLine(failures == 0 ? "All configuration examples behave as documented." : $"{failures} check(s) failed.");
return failures == 0 ? 0 : 1;

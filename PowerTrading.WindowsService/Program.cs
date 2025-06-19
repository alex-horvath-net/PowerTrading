using System.Runtime.InteropServices;
using PowerTrading.Infrastructure;
using PowerTrading.WindowsService;

IHost host = Host.CreateDefaultBuilder(args)
     .ConfigureLogging(logging =>
     {
         logging.ClearProviders();
         logging.AddConsole();
         logging.AddDebug();
         //logging.AddEventSourceLogger();
         //if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
         //    try {
         //        logging.AddEventLog();
         //    } catch(Exception ex) {
         //        // Optionally log or ignore if EventLog is not available
         //    }
         //}
     })
    .ConfigureServices((builder,services) =>
    {
        services.AddIntraDayReport(builder.Configuration);
        services.Configure<WorkerSettings>(builder.Configuration.GetSection(WorkerSettings.SectionName));
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();

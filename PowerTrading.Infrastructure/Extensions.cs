using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PowerTrading.Infrastructure.Clients;
using PowerTrading.Infrastructure.Csv;
using PowerTrading.Infrastructure.Time;
using PowerTrading.Reporting.IntraDayReport;
using Services;

namespace PowerTrading.Infrastructure; 
public static class Extensions {
    public static IServiceCollection AddIntraDayReport(this IServiceCollection services, IConfiguration configuration) {
        services.AddSingleton<ITime, LondonTime>();
        
        services.AddSingleton<IPowerService, PowerService>();
        services.AddSingleton<IPowerServiceClient, PowerServiceClient>();
        
        services.Configure<CsvExporterSettings>(configuration.GetSection(CsvExporterSettings.SectionName));
        services.AddSingleton<ICsvExporter, CsvExporter>();
        
        services.AddSingleton<IIntraDayReportService, IntraDayReportService>();

        return services;
    }
}
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Core.Realtime;

namespace SearchConsoleApp.Web.Framework.Realtime;

public static class RealtimeSetup
{
    /// <summary>
    /// SignalR + INotificationBroadcaster kaydı.
    ///
    /// Config:
    ///   "Realtime:Enabled": true | false
    ///   "Realtime:Backplane:Redis": "..." (multi-instance için)
    ///
    /// Enabled=false: NoOpBroadcaster kullanılır, hub map edilmez.
    /// Multi-instance: Redis backplane gerek, yoksa user A pod1'de connected
    /// ama broadcast pod2'den geliyorsa mesaj gitmez.
    /// </summary>
    public static IServiceCollection AddSearchConsoleAppRealtime(this IServiceCollection services, IConfiguration config)
    {
        var enabled = config.GetValue("Realtime:Enabled", true);

        if (!enabled)
        {
            services.AddSingleton<INotificationBroadcaster, NoOpBroadcaster>();
            return services;
        }

        var signalR = services.AddSignalR(o =>
        {
            o.EnableDetailedErrors = config.GetValue("Realtime:DetailedErrors", false);
        });

        // Multi-instance için Redis backplane
        var redisConn = config["Realtime:Backplane:Redis"];
        if (!string.IsNullOrEmpty(redisConn))
        {
            signalR.AddStackExchangeRedis(redisConn);
        }

        services.AddSingleton<INotificationBroadcaster, SignalRBroadcaster>();
        return services;
    }

    public static IEndpointRouteBuilder MapSearchConsoleAppRealtime(this IEndpointRouteBuilder app, IConfiguration config)
    {
        var enabled = config.GetValue("Realtime:Enabled", true);
        if (enabled)
        {
            app.MapHub<NotificationHub>("/hubs/notifications");
        }
        return app;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using TraeCheckIn.Application.Configuration;
using TraeCheckIn.Application.Interfaces;
using TraeCheckIn.Application.Services;
using TraeCheckIn.Infrastructure.Http;
using TraeCheckIn.Infrastructure.Notifications;
using TraeCheckIn.Infrastructure.Scheduling;
using TraeCheckIn.Infrastructure.Storage;
using TraeCheckIn.Worker.Options;
using TraeCheckIn.Worker.Workers;

namespace TraeCheckIn.Worker;

/// <summary>程序入口：构建通用主机并完成各层依赖注册</summary>
public static class Program
{
    /// <summary>应用主入口</summary>
    /// <param name="args">命令行参数；传入 --run-once 时立即执行一次签到后退出，否则按计划时间定时运行</param>
    public static async Task<int> Main(string[] args)
    {
        // 启动阶段的引导日志：宿主构建失败时也能输出到控制台
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            await CreateHost(args).RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Trae 签到服务意外终止");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>构建主机：绑定配置、注册各层服务、启用 Windows 服务支持与 Serilog 日志</summary>
    private static IHost CreateHost(string[] args)
    {
        // 固定以程序所在目录为内容根，保证 Windows 服务 / 计划任务等场景下始终能加载 appsettings.json
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        var runOnce = args.Contains("--run-once", StringComparer.OrdinalIgnoreCase);

        // 支持通过 sc.exe 安装为 Windows 服务
        builder.Services.AddWindowsService(options => options.ServiceName = "TraeCheckIn");

        // 配置绑定
        builder.Services.Configure<TraeAccountOptions>(builder.Configuration.GetSection(TraeAccountOptions.SectionName));
        builder.Services.Configure<CheckInOptions>(builder.Configuration.GetSection(CheckInOptions.SectionName));
        builder.Services.Configure<HttpOptions>(builder.Configuration.GetSection(HttpOptions.SectionName));
        builder.Services.Configure<ScheduleOptions>(builder.Configuration.GetSection(ScheduleOptions.SectionName));
        builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));

        // 应用层服务
        builder.Services.AddSingleton<ITraeApiClient, TraeApiClient>();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<CheckInOptions>>().Value);
        builder.Services.AddSingleton<CheckInExecutor>();
        builder.Services.AddSingleton<CheckInAppService>();

        // 基础设施服务
        builder.Services.AddSingleton<ICheckInStateStore, JsonCheckInStateStore>();
        builder.Services.AddSingleton<ICheckInNotifier, LogNotifier>();
        builder.Services.AddSingleton<ICheckInNotifier, EmailNotifier>();
        builder.Services.AddSingleton<INotificationService, CompositeNotificationService>();
        builder.Services.AddSingleton<NextRunCalculator>();

        // 宿主后台服务与运行模式选项
        builder.Services.AddSingleton(new WorkerOptions(
            ResolveScheduleTimes(builder.Configuration),
            runOnce));
        builder.Services.AddHostedService<CheckInWorker>();

        // Serilog 结构化日志：控制台 + 按天滚动的日志文件（保留 30 天）
        builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
            .MinimumLevel.Information()
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(AppContext.BaseDirectory, "logs", "checkin-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30));

        return builder.Build();
    }

    /// <summary>解析触发时间点配置：支持配置文件中的数组形式，以及命令行等提供程序的标量形式（逗号分隔）</summary>
    /// <param name="configuration">配置根节点</param>
    /// <returns>触发时间点字符串列表（HH:mm 格式）</returns>
    private static string[] ResolveScheduleTimes(IConfiguration configuration)
    {
        // 命令行覆盖形如 --Schedule:Times=09:30,14:00；配置文件数组时标量键不存在，将回退到节绑定
        var scalarValue = configuration["Schedule:Times"];
        if (!string.IsNullOrWhiteSpace(scalarValue))
        {
            return scalarValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return configuration.GetSection(ScheduleOptions.SectionName).Get<ScheduleOptions>()?.Times ?? [];
    }
}

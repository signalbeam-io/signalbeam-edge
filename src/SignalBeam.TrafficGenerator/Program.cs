using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

namespace SignalBeam.TrafficGenerator;

// Scripted operator traffic against the control-plane APIs (devices, groups, tags, bundles,
// rollouts, alerts), through the API gateway like the web UI. Same --seed, same action sequence.
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var gatewayUrlOption = new Option<string>("--gateway-url")
        {
            Description = "API gateway base URL (or TRAFFIC_GATEWAY_URL)",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("TRAFFIC_GATEWAY_URL") ?? "http://localhost:8080"
        };
        var apiKeyOption = new Option<string>("--api-key")
        {
            Description = "Tenant API key, sent as X-API-Key like the web UI's dev login (or TRAFFIC_API_KEY)",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("TRAFFIC_API_KEY") ?? "dev-api-key-1"
        };
        var tenantIdOption = new Option<Guid>("--tenant-id")
        {
            Description = "Tenant ID (or TRAFFIC_TENANT_ID)",
            DefaultValueFactory = _ => Guid.TryParse(Environment.GetEnvironmentVariable("TRAFFIC_TENANT_ID"), out var id)
                ? id
                : Guid.Parse("00000000-0000-0000-0000-000000000001")
        };
        var seedOption = new Option<int>("--seed")
        {
            Description = "Seed for the action mix and its inputs",
            DefaultValueFactory = _ => 42
        };
        var rateOption = new Option<double>("--rate")
        {
            Description = "Operator actions per minute",
            DefaultValueFactory = _ => 30
        };
        var invalidRateOption = new Option<double>("--invalid-rate")
        {
            Description = "Share of deliberately invalid requests (validation errors, 404s), 0 to 1",
            DefaultValueFactory = _ => 0.05
        };
        var iterationsOption = new Option<int>("--iterations")
        {
            Description = "Number of actions to run; 0 runs until stopped",
            DefaultValueFactory = _ => 0
        };
        var startDelayOption = new Option<int>("--start-delay")
        {
            Description = "Seconds to wait before the first action, e.g. while the fleet registers",
            DefaultValueFactory = _ => 0
        };

        var rootCommand = new RootCommand("SignalBeam operator traffic generator")
        {
            gatewayUrlOption, apiKeyOption, tenantIdOption, seedOption, rateOption, invalidRateOption,
            iterationsOption, startDelayOption
        };

        rootCommand.SetAction(async (parseResult, _) =>
        {
            var rate = parseResult.GetValue(rateOption);
            var invalidRate = parseResult.GetValue(invalidRateOption);
            if (rate <= 0 || invalidRate is < 0 or > 1)
            {
                Console.Error.WriteLine("--rate must be > 0 and --invalid-rate between 0 and 1.");
                return 1;
            }

            using var host = TrafficTelemetry.BuildHost();
            await host.StartAsync();
            var stopping = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

            using var http = new HttpClient
            {
                BaseAddress = new Uri(parseResult.GetValue(gatewayUrlOption)!),
                Timeout = TimeSpan.FromSeconds(30)
            };
            http.DefaultRequestHeaders.Add("X-API-Key", parseResult.GetValue(apiKeyOption));

            var seed = parseResult.GetValue(seedOption);
            var session = new OperatorSession(
                http,
                new OperatorSettings(parseResult.GetValue(tenantIdOption), invalidRate, DateTime.UtcNow.ToString("yyMMddHHmmss")),
                new ActionPicker(seed, invalidRate));

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(parseResult.GetValue(startDelayOption)), stopping);
                Console.WriteLine($"Operator traffic: {rate} actions/min, seed {seed}, via {http.BaseAddress}");
                await RunAsync(session, TimeSpan.FromMinutes(1) / rate, parseResult.GetValue(iterationsOption), stopping);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
            }
            finally
            {
                host.Services.GetService<TracerProvider>()?.ForceFlush(5000);
                await host.StopAsync(CancellationToken.None);
            }

            return 0;
        });

        return await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration { ProcessTerminationTimeout = null });
    }

    internal static async Task RunAsync(OperatorSession session, TimeSpan interval, int iterations, CancellationToken stopping)
    {
        await session.WarmUpAsync(stopping);

        // Fixed cadence: the next action is due one interval after the previous one started.
        using var timer = new PeriodicTimer(interval);
        for (var i = 0; iterations == 0 || i < iterations; i++)
        {
            await session.NextAsync(stopping);
            if (!await timer.WaitForNextTickAsync(stopping))
            {
                break;
            }
        }
    }
}

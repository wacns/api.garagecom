using api.garagecom;
using dotenv.net;

public class Program
{
    public static void Main(string[] args)
    {
        var host = CreateHostBuilder(args);
        DotEnv.Load();
        host.Build().Run();
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
    }
}
namespace api.garagecom.Utils;

public static class Globals
{
    public static readonly string ConnString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING")!;
    public static readonly string Secret = Environment.GetEnvironmentVariable("SECRET")!;
}
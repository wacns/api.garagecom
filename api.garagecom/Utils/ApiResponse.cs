namespace api.garagecom.Utils;

public class ApiResponse
{
    public bool Succeeded { get; set; } = false;

    public string Message { get; set; } = "";

    public Dictionary<string, object> Parameters { get; set; } = new();
}
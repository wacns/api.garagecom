using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;

namespace api.garagecom.Controllers;

[Route("api/[controller]")]
public class NotificationController : Controller
{
    [HttpPost("Send")]
    public async Task<ApiResponse> SendNotification([FromBody] NotificationRequest req)
    {
        var apiResponse = new ApiResponse();
        try
        {
            var result = await NotificationHelper.SendNotification(req);
            apiResponse.Succeeded = result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            apiResponse.Succeeded = false;
            apiResponse.Message = "Error sending notification";
        }

        return apiResponse;
    }
}
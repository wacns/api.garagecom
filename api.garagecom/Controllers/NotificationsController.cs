#region

using api.garagecom.Filters;
using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

#endregion

namespace api.garagecom.Controllers;

[ActionFilter]
[Route("api/[controller]")]
public class NotificationsController : Controller
{
    [HttpPost("SetDeviceToken")]
    public ApiResponse SetDeviceToken(string deviceToken)
    {
        var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
        var apiResponse = new ApiResponse();
        try
        {
            var sql =
                "UPDATE Logins SET DeviceToken = @DeviceToken WHERE UserId = @UserId AND DeviceToken IS NULL ORDER BY CreatedIn DESC LIMIT 1;";
            MySqlParameter[] parameters =
            [
                new("DeviceToken", deviceToken),
                new("UserId", userId)
            ];
            apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
        }
        catch (Exception e)
        {
            apiResponse.Succeeded = false;
            apiResponse.Message = "Error setting device token";
        }

        return apiResponse;
    }
}
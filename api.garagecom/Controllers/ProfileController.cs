#region

using api.garagecom.Filters;
using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;

#endregion

#region Models

public class User
{
    
}

#endregion


namespace api.garagecom.Controllers
{
    [ActionFilter]
    [Route("api/[controller]")]
    public class ProfileController : Controller
    {
        [HttpGet()]
        public ApiResponse GetUserDetails()
        {
            int userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                apiResponse.Succeeded = true;
                apiResponse.Parameters.Add("User", new User());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
            return apiResponse;
        }
    }
}


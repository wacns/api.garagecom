using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace api.garagecom.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RegistrationController : Controller
    {
        [AllowAnonymous]
        [Route("register")]
        public IActionResult Register(string request)
        {
            return Ok();
        }
    }
}

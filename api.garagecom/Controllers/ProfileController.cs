#region

using api.garagecom.Filters;
using Microsoft.AspNetCore.Mvc;

#endregion

namespace api.garagecom.Controllers;

[ActionFilter]
[Route("api/[controller]")]
public class ProfileController : Controller
{
}
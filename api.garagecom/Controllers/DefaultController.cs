#region

using Microsoft.AspNetCore.Mvc;

#endregion

namespace api.garagecom.Controllers;

public class DefaultController : Controller
{
    public string Index()
    {
        return $"Garagecom Back Server ID = {Random.Shared.Next(0, 100)}";
    }
}
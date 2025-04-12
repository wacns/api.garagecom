using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;

namespace api.garagecom.Filters;

public class ActionFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext httpContext)
    {
        try
        {
            Authentication.Authenticate(httpContext);
        }
        catch (Exception)
        {
            httpContext.HttpContext.Response.StatusCode = 401;
            httpContext.Result = new EmptyResult();
        }
    }
}
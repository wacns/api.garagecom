#region

using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MySql.Data.MySqlClient;

#endregion

namespace api.garagecom.Filters;

public class Authorization : ActionFilterAttribute
{
    public string RoleName { get; set; } = string.Empty;

    public override void OnActionExecuting(ActionExecutingContext httpContext)
    {
        try
        {
            Authentication.Authenticate(httpContext);
            var userId = httpContext.HttpContext.Items["UserID"] == null
                ? -1
                : Convert.ToInt32(httpContext.HttpContext.Items["UserID"]!);
            var sql = @"SELECT RoleName FROM Users U INNER JOIN Roles R ON R.RoleID = U.RoleID WHERE UserID = @UserID";
            MySqlParameter[] parameters =
            [
                new("UserID", userId)
            ];
            var roleNameScalar = DatabaseHelper.ExecuteScalar(sql, parameters);
            string userRole;
            if (roleNameScalar.Succeeded)
                userRole = roleNameScalar.Parameters["Result"].ToString()!;
            else
                throw new Exception("Error getting role name");
            if (!userRole.Equals(RoleName, StringComparison.CurrentCultureIgnoreCase))
                throw new Exception("You are not authorized to access this resource");
        }
        catch (Exception)
        {
            httpContext.HttpContext.Response.StatusCode = 403;
            httpContext.Result = new EmptyResult();
        }
    }
}
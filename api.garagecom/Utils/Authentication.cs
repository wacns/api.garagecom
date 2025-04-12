using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using JWT.Algorithms;
using JWT.Builder;
using JWT.Exceptions;
using MySql.Data.MySqlClient;

namespace api.garagecom.Utils;

public abstract class Authentication
{
    public static void Authenticate(ActionExecutingContext httpContext)
    {
        while (true)
        {
            Dictionary<string, object> payload;

            httpContext.HttpContext.Items["UserName"] = "";
            httpContext.HttpContext.Items["Email"] = "";
            httpContext.HttpContext.Items["UserID"] = -999;
            string authentication;

            if ((httpContext.ActionArguments.TryGetValue("token", out var tok) && tok!.ToString() != "") || (httpContext.ActionArguments.TryGetValue("key", out var key) && key!.ToString() != ""))
            {
                continue;
            }

            try
            {
                // var authentication = httpContext.HttpContext.Request.Cookies["Token"];
                httpContext.HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader);
                authentication = authHeader.ToString().Replace("Bearer ", "");
                if (string.IsNullOrEmpty(authentication))
                {
                    httpContext.HttpContext.Response.StatusCode = 401;
                    httpContext.Result = new EmptyResult();
                    throw new UnauthorizedAccessException("Token not found");
                }
                payload = JwtBuilder.Create()
                    .WithAlgorithm(new HMACSHA256Algorithm())
                    .WithSecret(Globals.Secret)
                    .MustVerifySignature()
                    .Decode<Dictionary<string, object>>(authentication);
            }
            catch (TokenExpiredException)
            {
                httpContext.HttpContext.Response.StatusCode = 401;
                httpContext.Result = new EmptyResult();
                throw;
            }
            catch (SignatureVerificationException)
            {
                httpContext.HttpContext.Response.StatusCode = 401;
                httpContext.Result = new EmptyResult();
                throw;
            }

            if (!payload.TryGetValue("UserID", out var userIdObj))
            {
                httpContext.HttpContext.Response.StatusCode = 401;
                httpContext.Result = new EmptyResult();
                throw new UnauthorizedAccessException("User ID not found in token payload");
            }

            int userId = Convert.ToInt32(userIdObj.ToString());
            string userName = payload["UserName"].ToString()!;
            string email = payload["Email"].ToString()!;

            string sql = "SELECT LastToken FROM Logins WHERE UserID = @UserID";
            MySqlParameter[] parameters = [new("UserID", userId)];

            var scalarResult = DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"];
            if (scalarResult == null)
            {
                httpContext.HttpContext.Response.StatusCode = 401;
                httpContext.Result = new EmptyResult();
                throw new UnauthorizedAccessException("User ID not found in token payload");
            }
            string? lastSessionId = string.IsNullOrEmpty(scalarResult.ToString()) ? null : scalarResult.ToString();

            if (lastSessionId == null || lastSessionId != authentication)
            {
                httpContext.HttpContext.Response.StatusCode = 401;
                httpContext.Result = new EmptyResult();
                throw new UnauthorizedAccessException("User ID not found in token payload");
            }

            httpContext.HttpContext.Items["UserName"] = userName;
            httpContext.HttpContext.Items["Email"] = email;
            httpContext.HttpContext.Items["UserID"] = userId;
            break;
        }
    }

    public static string GenerateJsonWebToken(string userName, int userId, string email)
    {
        var token = JwtBuilder.Create()
            .WithAlgorithm(new HMACSHA256Algorithm())
            .WithSecret(Globals.Secret) // Fixed the empty secret
            .AddClaim("exp", DateTimeOffset.UtcNow.AddYears(200).AddSeconds(-5).ToUnixTimeSeconds())
            .AddClaim("UserName", userName)
            .AddClaim("UserID", userId)
            .AddClaim("Email", email)
            .Encode();
        return token;
    }
}
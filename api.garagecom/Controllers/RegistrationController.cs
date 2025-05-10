#region

using System.Net.Mail;
using System.Text.RegularExpressions;
using api.garagecom.Utils;
using JWT.Algorithms;
using JWT.Builder;
using JWT.Exceptions;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

#endregion

namespace api.garagecom.Controllers;

[Route("api/[controller]")]
public class RegistrationController : Controller
{
    [HttpPost("register")]
    public ApiResponse Register(string userName, string email, string password, string firstName,
        string lastName, string phoneNumber)
    {
        var apiResponse = new ApiResponse();
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || phoneNumber.Length != 8)
        {
            apiResponse.Succeeded = false;
            apiResponse.Message = "Please fill all fields";
            return apiResponse;
        }

        try
        {
            var userNamePattern = "[^a-zA-Z0-9_.]{3,20}";
            var phoneNumberPattern = "^[1-9][0-9]{7}$";

            var matches = Regex.Matches(userName, userNamePattern);

            if (matches.Count > 0)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = "Username can only contain letters, numbers, underscores and dots";
                return apiResponse;
            }

            matches = Regex.Matches(phoneNumber, phoneNumberPattern);

            if (matches.Count != 1)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = "Phone Number can only contain 8 numbers";
                return apiResponse;
            }

            try
            {
                var mail = new MailAddress(email);
                if (mail.Address != email)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Email is not valid";
                    return apiResponse;
                }
            }
            catch (Exception)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = "Email is not valid";
                return apiResponse;
            }

            var sql = @"SELECT COUNT(*) > 0 AS Existing
                            FROM Users
                            WHERE LOWER(Email) = LOWER(@Email)
                               OR LOWER(UserName) = LOWER(@UserName)";
            MySqlParameter[] parameters =
            [
                new("Email", email),
                new("UserName", userName)
            ];
            var exists = DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString() == "1";

            if (exists)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = "User already exists";
                return apiResponse;
            }

            sql =
                @"INSERT INTO Users (UserName, Email, Password, FirstName, LastName, CreatedIn, Mobile, RoleID)
                    VALUES (@UserName, @Email, @Password, @FirstName, @LastName, NOW(), @PhoneNumber, 2);
                    SELECT LAST_INSERT_ID() AS UserID;
                    ";
            parameters =
            [
                new MySqlParameter("Email", email),
                new MySqlParameter("UserName", userName),
                new MySqlParameter("Password", GeneralHelper.HashEncrypt(password)),
                new MySqlParameter("FirstName", firstName),
                new MySqlParameter("LastName", lastName),
                new MySqlParameter("PhoneNumber", phoneNumber)
            ];

            apiResponse = DatabaseHelper.ExecuteScalar(sql, parameters);

            var userId = Convert.ToInt32(apiResponse.Parameters["Result"].ToString());

            var token = Authentication.GenerateJsonWebToken(userName.ToLower(), userId, email);

            sql = @"INSERT INTO Logins (UserID, LastToken, CreatedIn)
                    VALUES (@UserID, @LastToken, NOW())";
            parameters =
            [
                new MySqlParameter("UserID", userId),
                new MySqlParameter("LastToken", token)
            ];
            apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);

            var result = new ApiResponse
            {
                Parameters =
                {
                    ["Token"] = token,
                    ["UserID"] = userId,
                    ["RoleName"] = "User"
                },
                Succeeded = true
            };

            return result;
        }
        catch (Exception e)
        {
            apiResponse.Succeeded = false;
            apiResponse.Message = e.Message;
            return apiResponse;
        }
    }

    [HttpPost("login")]
    public ApiResponse Login(string userName, string password)
    {
        var apiResponse = new ApiResponse();
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
        {
            apiResponse.Succeeded = false;
            apiResponse.Message = "Please fill all fields";
            return apiResponse;
        }

        var pattern = "[^a-zA-Z0-9_.]";

        var matches = Regex.Matches(userName, pattern);

        if (matches.Count > 0)
        {
            apiResponse.Succeeded = false;
            apiResponse.Message = "Username can only contain letters, numbers, underscores and dots";
        }

        int userId;
        string email;
        string roleName;

        var sql = @"SELECT UserID, Email, R.RoleName
                            FROM Users
                            INNER JOIN Garagecom.Roles R ON R.RoleID = Users.RoleID
                            WHERE LOWER(UserName) = LOWER(@UserName) AND Password = @Password";
        MySqlParameter[] parameters =
        [
            new("UserName", userName),
            new("Password", GeneralHelper.HashEncrypt(password))
        ];

        using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
        {
            if (reader.Read())
            {
                userId = reader["UserID"] == DBNull.Value ? -1 : Convert.ToInt32(reader["UserID"]);
                email = reader["Email"].ToString()!;
                roleName = reader["RoleName"].ToString()!;
            }
            else
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = "User not found";
                return apiResponse;
            }
        }

        if (userId == -1)
        {
            apiResponse.Succeeded = false;
            apiResponse.Message = "User not found";
            return apiResponse;
        }

        var token = Authentication.GenerateJsonWebToken(userName.ToLower(), userId, email);

        sql = @"INSERT INTO Logins (UserID, LastToken, CreatedIn)
                    VALUES (@UserID, @LastToken, NOW())";
        parameters =
        [
            new MySqlParameter("UserID", userId),
            new MySqlParameter("LastToken", token)
        ];
        apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
        if (!apiResponse.Succeeded)
        {
            apiResponse.Succeeded = false;
            apiResponse.Message = "Error Logging In!";
            return apiResponse;
        }

        apiResponse.Parameters["Token"] = token;
        apiResponse.Parameters["UserID"] = userId;
        apiResponse.Parameters["RoleName"] = roleName;
        apiResponse.Succeeded = true;

        return apiResponse;
    }

    [HttpGet("ValidateUser")]
    public IActionResult ValidateUser(string token)
    {
        if (string.IsNullOrEmpty(token)) return StatusCode(401);

        Dictionary<string, object> payload;
        try
        {
            payload = JwtBuilder.Create()
                .WithAlgorithm(new HMACSHA256Algorithm())
                .WithSecret(Globals.Secret)
                .MustVerifySignature()
                .Decode<Dictionary<string, object>>(token);
        }
        catch (TokenExpiredException)
        {
            return StatusCode(401);
        }
        catch (SignatureVerificationException)
        {
            return StatusCode(401);
        }
        catch (Exception)
        {
            return StatusCode(401);
        }

        return payload == null ? StatusCode(401) : Ok();
    }
}
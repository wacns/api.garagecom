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
            var userNamePattern = "[^a-zA-Z0-9_.]";
            var phoneNumberPattern = "^[0-9]{8}$";

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
                @"INSERT INTO Users (UserName, Email, Password, FirstName, LastName, CreatedIn, Mobile)
                    VALUES (@UserName, @Email, @Password, @FirstName, @LastName, NOW(), @PhoneNumber);
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
                    ["UserID"] = userId
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

        var sql = @"SELECT UserID, Email
                            FROM Users
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

    [HttpGet("UploadSigns")]
    public async Task<IActionResult> UploadSigns()
    {
        var apiResponse = new ApiResponse();
        try
        {
            var uploadsDirectory = Path.Combine("C:", "Users", "ABU-LAYLA", "Documents", "University", "ITSE498",
                "Dashboard Signs", "Dashboard Signs");
            var supportedExtensions = new[] { ".jpg", ".jpeg", ".png" };

            var imageFiles = Directory.GetFiles(uploadsDirectory)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();

            foreach (var imagePath in imageFiles)
            {
                var fileName = Path.GetFileName(imagePath).Split(".")[0];
                var attachmentId = "DashboardSign_" + Guid.NewGuid();

                // Open the file stream directly
                using (var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    var formFile = new FormFile(
                        fileStream,
                        0,
                        fileStream.Length,
                        "file",
                        fileName
                    );

                    if (formFile.Length <= 0) continue;
                    var result = await S3Helper.UploadAttachmentAsync(formFile, attachmentId, "Images/DashboardSigns/");
                    if (!result) continue;
                    var sql = @"UPDATE DashboardSigns 
                                   SET Logo = @Logo 
                                   WHERE Title = @Title";

                    MySqlParameter[] parameters =
                    [
                        new("Logo", attachmentId),
                        new("Title", fileName)
                    ];
                    var dbResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
                }
            }

            apiResponse.Succeeded = true;
            apiResponse.Message = "Files processed successfully";
            return Ok(apiResponse);
        }
        catch (Exception e)
        {
            apiResponse.Succeeded = false;
            apiResponse.Message = $"Error processing files: {e.Message}";
            return Ok(apiResponse);
        }
    }
}
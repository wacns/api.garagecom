#region

using System.Text.RegularExpressions;
using api.garagecom.Filters;
using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

#endregion

#region Models

public class User
{
    public int UserID { get; set; }
    public string UserName { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string ProfilePicture { get; set; }
    public string AttachmentName { get; set; }
}

#endregion


namespace api.garagecom.Controllers
{
    [ActionFilter]
    [Route("api/[controller]")]
    public class ProfileController : Controller
    {
        [HttpGet("GetUserDetails")]
        public ApiResponse GetUserDetails()
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                if (userId == -1)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "User not found";
                    return apiResponse;
                }

                var user = new User();
                var sql =
                    @"SELECT UserID, UserName, FirstName, LastName, Email, Mobile, GI.Avatar FROM Users GI WHERE UserID = @UserID";
                MySqlParameter[] parameters =
                [
                    new("UserID", userId)
                ];
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                        user = new User
                        {
                            UserID = Convert.ToInt32(reader["UserID"]),
                            UserName = (reader["UserName"] != DBNull.Value ? reader["UserName"].ToString() : "")!,
                            FirstName = (reader["FirstName"] != DBNull.Value ? reader["FirstName"].ToString() : "")!,
                            LastName = (reader["LastName"] != DBNull.Value ? reader["LastName"].ToString() : "")!,
                            Email = (reader["Email"] != DBNull.Value ? reader["Email"].ToString() : "")!,
                            PhoneNumber =
                                (reader["Mobile"] != DBNull.Value ? reader["Mobile"].ToString() : "")!,
                            AttachmentName = (reader["Avatar"] != DBNull.Value ? reader["Avatar"].ToString() : "")!
                        };
                }

                apiResponse.Succeeded = true;
                apiResponse.Parameters.Add("User", user);
            }
            catch (Exception e)
            {
                apiResponse.Message = e.Message;
                apiResponse.Succeeded = false;
            }

            return apiResponse;
        }

        [HttpPost("UpdateUserDetails")]
        public ApiResponse UpdateUserDetails(string firstName, string lastName, string phoneNumber)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                if (userId == -1)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "User not found";
                    return apiResponse;
                }

                firstName = firstName.SanitizeFileName();
                lastName = lastName.SanitizeFileName();
                phoneNumber = phoneNumber.SanitizeFileName();
                if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ||
                    string.IsNullOrEmpty(phoneNumber))
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "First name, last name or phone number is empty";
                    return apiResponse;
                }

                if (phoneNumber.Length != 8)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Phone number is too short";
                    return apiResponse;
                }

                var phoneNumberPattern = "^[1-9][0-9]{7}$";
                if (!Regex.IsMatch(phoneNumber, phoneNumberPattern))
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Phone number is invalid";
                    return apiResponse;
                }

                var sql =
                    @"SELECT UserID FROM Users WHERE UserID = @UserID";
                MySqlParameter[] parameters =
                [
                    new("UserID", userId)
                ];
                var user = int.Parse(DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString() ??
                                     string.Empty);
                if (user == userId)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "User not found";
                    return apiResponse;
                }

                sql =
                    @"UPDATE Users SET FirstName = @FirstName, LastName = @LastName, Mobile = @PhoneNumber WHERE UserID = @UserID";
                parameters =
                [
                    new MySqlParameter("FirstName", firstName),
                    new MySqlParameter("LastName", lastName),
                    new MySqlParameter("PhoneNumber", phoneNumber),
                    new MySqlParameter("UserID", userId)
                ];
                DatabaseHelper.ExecuteNonQuery(sql, parameters);
                apiResponse.Succeeded = true;
            }
            catch (Exception e)
            {
                apiResponse.Message = e.Message;
                apiResponse.Succeeded = false;
            }

            return apiResponse;
        }

        [HttpGet("GetAvatarAttachment")]
        public async Task<FileResult?> GetAvatarAttachment(string fileName)
        {
            // fileName = fileName.SanitizeFileName();
            if (string.IsNullOrEmpty(fileName))
                return null;
            var file = await S3Helper.DownloadAttachmentAsync(fileName, "Images/Avatars/");
            return File(file, "application/octet-stream", fileName);
        }

        [HttpPost("SetAvatarAttachment")]
        public ApiResponse SetAvatarAttachment(IFormFile file)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var attachmentName = $"{userId}_{Guid.NewGuid().ToString()}";
            if (file.Length == 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "File is empty"
                };
            if (file.Length > 1048576)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "File size is too large"
                };

            if (userId == -1)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "User not found"
                };
            attachmentName = attachmentName.SanitizeFileName();
            var task = new Task(async void () =>
            {
                var status = await S3Helper.UploadAttachmentAsync(file, attachmentName, "Images/Avatars/");
                if (!status) return;
                var sql = @"UPDATE Users
                            SET Avatar = @Attachment
                            WHERE UserID = @UserID";
                MySqlParameter[] parameters =
                [
                    new("Attachment", attachmentName),
                    new("UserID", userId)
                ];
                DatabaseHelper.ExecuteNonQuery(sql, parameters);
            });
            task.Start();
            return new ApiResponse
            {
                Succeeded = true,
                Parameters =
                {
                    ["AttachmentName"] = attachmentName
                }
            };
        }


        [HttpPost("DeleteAvatar")]
        public ApiResponse DeleteAvatar()
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                if (userId == -1)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "User not found";
                    return apiResponse;
                }

                var sql =
                    @"SELECT Avatar FROM Users WHERE UserID = @UserID";
                MySqlParameter[] parameters =
                [
                    new("UserID", userId)
                ];
                var attachmentName = DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString();
                if (string.IsNullOrEmpty(attachmentName))
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Avatar not found";
                    return apiResponse;
                }
                // var status = S3Helper.DeleteAttachment(attachmentName, "Images/Avatars/");

                sql =
                    @"UPDATE Users SET Avatar = null WHERE UserID = @UserID";
                parameters =
                [
                    new MySqlParameter("UserID", userId)
                ];
                DatabaseHelper.ExecuteNonQuery(sql, parameters);
                apiResponse.Succeeded = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return apiResponse;
        }


        [HttpPost("Logout")]
        public ApiResponse Logout()
        {
            var userId = HttpContext.Items["UserID"] as int? ?? -1;
            if (userId == -1)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "User not found"
                };
            var apiResponse = new ApiResponse();

            var sql =
                @"UPDATE Logins SET Logins.LastToken = null WHERE UserID = @UserID ORDER BY Logins.CreatedIn DESC LIMIT 1;";
            MySqlParameter[] parameters =
            [
                new("UserID", userId)
            ];
            apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
            if (!apiResponse.Succeeded)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = "Error Logging Out!";
                return apiResponse;
            }

            apiResponse.Succeeded = true;

            return apiResponse;
        }
    }
}
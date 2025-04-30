#region

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
                var user = new User();
                var sql =
                    @"SELECT UserID, UserName, FirstName, LastName, Email, Mobile, GI.Avatar FROM GeneralInformation GI WHERE UserID = @UserID";
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
                var sql =
                    @"UPDATE GeneralInformation SET FirstName = @FirstName, LastName = @LastName, Mobile = @PhoneNumber WHERE UserID = @UserID";
                MySqlParameter[] parameters =
                [
                    new("FirstName", firstName),
                    new("LastName", lastName),
                    new("PhoneNumber", phoneNumber),
                    new("UserID", userId)
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
        public async Task<FileResult> GetAvatarAttachment(string fileName)
        {
            var file = await S3Helper.DownloadAttachmentAsync(fileName, "Images/Avatars/");
            return File(file, "application/octet-stream", fileName);
        }

        [HttpPost("SetAvatarAttachment")]
        public ApiResponse SetAvatarAttachment(IFormFile file)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var attachmentName = $"{userId}_{Guid.NewGuid().ToString()}";
            var task = new Task(async void () =>
            {
                var status = await S3Helper.UploadAttachmentAsync(file, attachmentName, "Images/Avatars/");
                if (!status) return;
                var sql = @"UPDATE GeneralInformation
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

        [HttpPost("Logout")]
        public ApiResponse Logout()
        {
            var userId = HttpContext.Items["UserID"] as int? ?? -1;
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
using System.Net.Mail;
using System.Text.RegularExpressions;
using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace api.garagecom.Controllers
{
    [Route("api/[controller]")]
    public class RegistrationController : Controller
    {
        [HttpPost("register")]
        public async Task<ApiResponse> Register(string userName, string email,string password, string gender, string firstName, string lastName, string phoneNumber, IFormFile? avatar = null)
        {
            ApiResponse apiResponse = new ApiResponse();
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(gender) || string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || phoneNumber.Length != 8)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = "Please fill all fields";
                return apiResponse;
            }
            
            string userNamePattern = "[^a-zA-Z0-9_.]";
            string phoneNumberPattern = "^[0-9]{8}$";
            
            MatchCollection matches = Regex.Matches(userName, userNamePattern);
            
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
                MailAddress mail = new MailAddress(email);
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
            
            string sql = @"SELECT COUNT(*) > 0 AS Existing
                            FROM GeneralInformation
                            WHERE LOWER(Email) = LOWER(@Email)
                               OR LOWER(UserName) = LOWER(@UserName)";
            MySqlParameter[] parameters =
            [
                new("Email", email),
                new("UserName", userName)
            ];
            bool exists = DatabaseHelper.ExecuteScalar(sql,parameters).Parameters["Result"].ToString() == "1";
            
            if (exists)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = "User already exists";
                return apiResponse;
            }
            
            sql = @"INSERT INTO GeneralInformation (UserName, Email, Password, FirstName, LastName, CreatedIn, Gender, Mobile)
                    VALUES (@UserName, @Email, @Password, @FirstName, @LastName, NOW(), @Gender, @PhoneNumber);
                    SELECT LAST_INSERT_ID() Into @UserID;
                    ";
            parameters = [
                new MySqlParameter("Email", email),
                new MySqlParameter("UserName", userName),
                new MySqlParameter("Password", GeneralHelper.HashEncrypt(password)),
                new MySqlParameter("FirstName", firstName),
                new MySqlParameter("LastName", lastName),
                new MySqlParameter("Gender", gender),
                new MySqlParameter("PhoneNumber", phoneNumber)
            ];
            
            apiResponse = DatabaseHelper.ExecuteScalar(sql, parameters);
            
            var userId = Convert.ToInt32(apiResponse.Parameters["Result"].ToString());
            
            if (avatar != null)
            {
                var fileName = $"{userId}_{Guid.NewGuid().ToString()}";
                var succeeded = await S3Helper.UploadAttachmentAsync(avatar,fileName, "Images/Logos/");
                if (succeeded)
                {
                    sql = @"UPDATE GeneralInformation
                        SET Avatar = @Avatar
                        WHERE UserID = @UserID";
                    parameters = [
                        new MySqlParameter("Avatar", fileName),
                        new MySqlParameter("UserID", userId)
                    ];
                    apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
                }
            }
            
            string token = Authentication.GenerateJsonWebToken(userName.ToLower(), userId, email);
            
            apiResponse.Parameters["Token"] = token;
            apiResponse.Succeeded = true;
            
            return apiResponse;
        }
        
        [HttpPost("login")]
        public ApiResponse Login(string userName, string password)
        {
            ApiResponse apiResponse = new ApiResponse();
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = "Please fill all fields";
                return apiResponse;
            }
            
            string pattern = "[^a-zA-Z0-9_.]";
            
            MatchCollection matches = Regex.Matches(userName, pattern);
            
            if (matches.Count > 0)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = "Username can only contain letters, numbers, underscores and dots";
            }
            
            int userId;
            string email;
            
            string sql = @"SELECT UserID, Email
                            FROM GeneralInformation
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
            
            string token = Authentication.GenerateJsonWebToken(userName.ToLower(), userId, email);
            
            apiResponse.Parameters["Token"] = token;
            apiResponse.Succeeded = true;
            
            return apiResponse;
        }
    }
}

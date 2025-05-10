#region

using System.Text.RegularExpressions;
using api.garagecom.Filters;
using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

#endregion

public class Report
{
    public int ReportCount { get; set; }
    public int? CommentID { get; set; }
    public int? PostID { get; set; }
}

namespace api.garagecom.Controllers
{
    [Authorization(RoleName = "Admin")]
    [Route("api/[controller]")]
    public class AdministrationsController : Controller
    {
        #region Posts Modertation & Reporting

        [HttpGet("GetNewReport")]
        public ApiResponse GetNewReport()
        {
            var apiResponse = new ApiResponse();
            try
            {
                var report = new Report();
                var sql = @"SELECT 
  PostID,
  NULL      AS CommentID,
  COUNT(*)  AS ReportCount
FROM Reports
WHERE PostID IS NOT NULL AND ProcessedIn IS NULL
GROUP BY PostID

UNION ALL

SELECT
  NULL      AS PostID,
  CommentID,
  COUNT(*)  AS ReportCount
FROM Reports
WHERE CommentID IS NOT NULL AND ProcessedIn IS NULL
GROUP BY CommentID

ORDER BY ReportCount DESC
LIMIT 1;
";
                MySqlParameter[] parameters = [];
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    if (reader.Read())
                    {
                        report = new Report
                        {
                            ReportCount = reader["ReportCount"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["ReportCount"]),
                            CommentID = reader["CommentID"] == DBNull.Value
                                ? null
                                : Convert.ToInt32(reader["CommentID"]),
                            PostID = reader["PostID"] == DBNull.Value ? null : Convert.ToInt32(reader["PostID"])
                        };
                        apiResponse.Succeeded = true;
                        apiResponse.Parameters.Add("Report", report);
                    }
                }
            }
            catch (Exception e)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = e.Message;
            }

            return apiResponse;
        }

        [HttpPost("ProcessReport")]
        public async Task<ApiResponse> ProcessReport(string action, int? commentId = null, int? postId = null)
        {
            action = action.SanitizeFileName();
            if (string.IsNullOrEmpty(action))
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Please provide action"
                };
            if (action != "block" && action != "allow")
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Please provide valid action"
                };
            if (commentId == null && postId == null)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Please provide PostID or CommentID"
                };
            if (commentId != null && postId != null)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Please provide only PostID or CommentID"
                };
            var apiResponse = new ApiResponse();
            var actionUserId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            try
            {
                string sql;
                MySqlParameter[] parameters;
                if (commentId != null)
                {
                    sql =
                        "UPDATE Reports SET ProcessedIn = NOW() WHERE Reports.ProcessedIn IS NULL AND CommentID = @CommentID;";
                    parameters =
                    [
                        new MySqlParameter("PostID", postId),
                        new MySqlParameter("CommentID", commentId)
                    ];
                    apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
                }
                else if (postId != null)
                {
                    sql =
                        "UPDATE Reports SET ProcessedIn = NOW() WHERE Reports.ProcessedIn IS NULL AND PostID = @PostID;";
                    parameters =
                    [
                        new MySqlParameter("PostID", postId),
                        new MySqlParameter("CommentID", commentId)
                    ];
                    apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
                }
                else
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Please provide PostID or CommentID";
                    return apiResponse;
                }

                parameters =
                [
                    new MySqlParameter("PostID", postId),
                    new MySqlParameter("CommentID", commentId)
                ];
                sql =
                    "SELECT U.userId FROM (SELECT userId FROM Comments WHERE CommentID = @CommentID UNION SELECT userId FROM Posts WHERE PostID = @PostID) AS U WHERE U.userId IS NOT NULL;";
                var userScalar = DatabaseHelper.ExecuteScalar(sql, parameters);
                var reportedUserId = Convert.ToInt32(userScalar.Parameters["Result"].ToString());

                sql = @"INSERT INTO ReportActions (ReportedUserID, ActionUserID, Action, CreatedIn)
                          VALUES (@ReportedUserID, @ActionUserID, @Action, NOW());";
                MySqlParameter[] userParameters =
                [
                    new("ReportedUserID", reportedUserId),
                    new("ActionUserID", actionUserId),
                    new("Action", action.ToUpper())
                ];
                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, userParameters);

                if (action.Equals("block", StringComparison.CurrentCultureIgnoreCase))
                {
                    sql = @"UPDATE Posts P SET P.StatusID = 3 WHERE P.PostID = @PostID;
                            UPDATE Comments C SET C.StatusID = 3 WHERE C.CommentID = @CommentID;
                            ";
                    apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
                    sql =
                        @"SELECT COUNT(*) FROM ReportActions RA WHERE RA.ReportedUserID = @reportedUserId AND RA.Action = 'BLOCK';";
                    var blockCount =
                        int.Parse(DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"]?.ToString() ??
                                  string.Empty);
                    var userDeviceToken = GeneralHelper.GetDeviceTokenByUserId(reportedUserId);
                    if (!string.IsNullOrEmpty(userDeviceToken) && !string.IsNullOrWhiteSpace(userDeviceToken) &&
                        blockCount >= 3)
                        await NotificationHelper.SendNotification(new NotificationRequest
                        {
                            Body = "You have been blocked from using the Car Community!",
                            Title = "Blocked",
                            DeviceToken = userDeviceToken
                        });
                }
            }
            catch (Exception e)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = e.Message;
            }

            return apiResponse;
        }

        #endregion

        #region Cars & Parts

        [HttpPost("SetPart")]
        public ApiResponse SetPart(string partName)
        {
            partName = partName.SanitizeFileName();
            if (string.IsNullOrEmpty(partName))
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Please provide part name"
                };
            if (partName.Length < 3)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Part name must be at least 3 characters long"
                };
            if (partName.Length > 50)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Part name must be at most 50 characters long"
                };
            var pattern = "[^a-zA-Z0-9_.]";
            var matches = Regex.Matches(partName, pattern);
            if (matches.Count > 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Part name can only contain letters, numbers, underscores and dots"
                };
            if (partName.Contains("  "))
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Part name cannot contain multiple spaces"
                };
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"INSERT INTO Parts (PartName) VALUES (@PartName);";
                MySqlParameter[] parameters =
                [
                    new("PartName", partName.Trim())
                ];
                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception e)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = e.Message;
            }

            return apiResponse;
        }

        [HttpPost("SetBrand")]
        public ApiResponse SetBrand(string brandName)
        {
            brandName = brandName.SanitizeFileName();
            if (string.IsNullOrEmpty(brandName))
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Please provide brand name"
                };
            if (brandName.Length < 3)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Brand name must be at least 3 characters long"
                };
            if (brandName.Length > 50)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Brand name must be at most 50 characters long"
                };
            var pattern = "[^a-zA-Z0-9_.]";
            var matches = Regex.Matches(brandName, pattern);
            if (matches.Count > 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Brand name can only contain letters, numbers, underscores and dots"
                };
            if (brandName.Contains("  "))
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Brand name cannot contain multiple spaces"
                };
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"INSERT INTO Brands (BrandName) VALUES (@BrandName);";
                MySqlParameter[] parameters =
                [
                    new("BrandName", brandName.Trim())
                ];
                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception e)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = e.Message;
            }

            return apiResponse;
        }

        [HttpPost("SetModel")]
        public ApiResponse SetModel(string modelName, int brandId)
        {
            modelName = modelName.SanitizeFileName();
            if (string.IsNullOrEmpty(modelName))
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Please provide model name"
                };
            if (modelName.Length < 3)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Model name must be at least 3 characters long"
                };
            if (modelName.Length > 50)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Model name must be at most 50 characters long"
                };
            var pattern = "[^a-zA-Z0-9_.]";
            var matches = Regex.Matches(modelName, pattern);
            if (matches.Count > 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Model name can only contain letters, numbers, underscores and dots"
                };
            if (modelName.Contains("  "))
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Model name cannot contain multiple spaces"
                };
            if (brandId <= 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Please provide valid brand ID"
                };
            var sql = "SELECT COUNT(*) FROM Brands WHERE BrandID = @BrandID;";
            MySqlParameter[] parameters =
            [
                new("BrandID", brandId)
            ];
            var brandCount = DatabaseHelper.ExecuteScalar(sql, parameters);
            if (brandCount.Parameters["Result"].ToString() == "0")
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Brand ID does not exist"
                };
            var apiResponse = new ApiResponse();
            try
            {
                sql = @"INSERT INTO Models (ModelName, BrandID) VALUES (@ModelName, @BrandID);";
                parameters =
                [
                    new MySqlParameter("ModelName", modelName),
                    new MySqlParameter("BrandID", brandId)
                ];
                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception e)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = e.Message;
            }

            return apiResponse;
        }

        #endregion
    }
}
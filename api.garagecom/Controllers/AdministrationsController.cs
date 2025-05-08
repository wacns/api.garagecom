#region

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
        public ApiResponse ProcessReport(int? postId, int? commentId, string action)
        {
            var apiResponse = new ApiResponse();
            var actionUserId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            try
            {
                var sql =
                    "UPDATE Reports SET ProcessedIn = NOW() WHERE Reports.ProcessedIn IS NULL AND PostID = @PostID AND CommentID = @CommentID;";
                MySqlParameter[] parameters =
                [
                    new("PostID", postId),
                    new("CommentID", commentId)
                ];
                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
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
                    sql = @"DELETE FROM Posts P 
                            WHERE P.PostID = @PostID;
                            DELETE FROM Comments C 
                            WHERE C.CommentID = @CommentID;";
                    apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
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
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"INSERT INTO Parts (PartName) VALUES (@PartName);";
                MySqlParameter[] parameters =
                [
                    new("PartName", partName)
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
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"INSERT INTO Brands (BrandName) VALUES (@BrandName);";
                MySqlParameter[] parameters =
                [
                    new("BrandName", brandName)
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
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"INSERT INTO Models (ModelName, BrandID) VALUES (@ModelName, @BrandID);";
                MySqlParameter[] parameters =
                [
                    new("ModelName", modelName),
                    new("BrandID", brandId)
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
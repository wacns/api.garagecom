#region

using api.garagecom.Filters;
using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

#endregion

namespace api.garagecom.Controllers;

[ActionFilter]
[Route("api/[controller]")]
public class DashboardController : Controller
{
    [HttpGet("GetDashboardSignAttachment")]
    public async Task<ActionResult> GetDashboardSignAttachment(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || string.IsNullOrWhiteSpace(fileName))
            return NotFound();
        // fileName = fileName.Trim();
        // fileName = fileName.SanitizeFileName();

        var file = await S3Helper.DownloadAttachmentAsync(fileName, "Images/DashboardSigns/");
        return File(file, "application/octet-stream", fileName);
    }

    [HttpPost("GetDashboardSigns")]
    public async Task<ApiResponse> GetDashboardSigns([FromForm] IFormFile file)
    {
        var apiResponse = new ApiResponse();
        if (file.Length == 0)
        {
            apiResponse.Succeeded = false;
            apiResponse.Message = "File is empty";
            return apiResponse;
        }

        if (file.Length > 1048576)
        {
            apiResponse.Succeeded = false;
            apiResponse.Message = "File size is too large";
            return apiResponse;
        }

        var defects = await AiHelper.GetDashboardSigns(file);
        var defectsList = new List<Defects>();
        try
        {
            var sql =
                @"SELECT Logo, Title, Description, Solution FROM DashboardSigns DS WHERE LOWER(Title) = LOWER(@Defect)";
            foreach (var defect in defects)
            {
                MySqlParameter[] parameters =
                [
                    new("Defect", defect)
                ];
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                        defectsList.Add(new Defects
                        {
                            Logo = (reader["Logo"] == DBNull.Value ? "" : reader["Logo"].ToString()) ?? string.Empty,
                            Title = (reader["Title"] == DBNull.Value ? "" : reader["Title"].ToString()) ?? string.Empty,
                            Description =
                                (reader["Description"] == DBNull.Value ? "" : reader["Description"].ToString()) ??
                                string.Empty,
                            Solution = (reader["Solution"] == DBNull.Value ? "" : reader["Solution"].ToString()) ??
                                       string.Empty
                        });
                }
            }

            apiResponse.Succeeded = true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        apiResponse.Parameters.Add("defects", defectsList);
        return apiResponse;
    }
}

public class Defects
{
    public string Logo { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Solution { get; set; }
}
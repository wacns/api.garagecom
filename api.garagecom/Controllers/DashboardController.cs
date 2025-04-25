using api.garagecom.Filters;
using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace api.garagecom.Controllers;

[ActionFilter]
[Route("api/[controller]")]
public class DashboardController : Controller
{
    [HttpPost("GetDashboardSigns")]
    public async Task<ApiResponse> GetDashboardSigns(IFormFile file)
    {
        var apiResponse = new ApiResponse();
        var defects = await AiHelper.GetDashboardSigns(file);
        var defectsList = new List<Defects>();
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
                        Description = (reader["Description"] == DBNull.Value ? "" : reader["Description"].ToString()) ??
                                      string.Empty,
                        Solution = (reader["Solution"] == DBNull.Value ? "" : reader["Solution"].ToString()) ??
                                   string.Empty
                    });
            }
        }

        apiResponse.Parameters.Add("", defectsList);
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
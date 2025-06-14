﻿#region

using api.garagecom.Filters;
using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

#endregion

#region Models

public class Brand
{
    public int BrandID { get; set; }
    public string BrandName { get; set; }
}

public class Model
{
    public int ModelID { get; set; }
    public string ModelName { get; set; }
    public Brand Brand { get; set; }
}

public class Part
{
    public int PartID { get; set; }
    public string PartName { get; set; }
}

public class CarPart
{
    public int CarPartID { get; set; }
    public Part Part { get; set; }
    public int LifeTimeInterval { get; set; }
    public DateTime CreatedIn { get; set; }
    public DateTime NextDueDate { get; set; }
    public string Notes { get; set; }
}

public class Car
{
    public int CarID { get; set; } // maps to Cars.CarID
    public int Year { get; set; }
    public string? Nickname { get; set; }
    public int? Kilos { get; set; }
    public Model Model { get; set; }
    public List<CarPart> Parts { get; set; } = new();
}

#endregion

namespace api.garagecom.Controllers
{
    [ActionFilter]
    [Route("api/[controller]")]
    public class CarsController : Controller
    {
        #region ComboBox

        [HttpGet("GetBrands")]
        public ApiResponse GetBrands()
        {
            var apiResponse = new ApiResponse();
            try
            {
                var list = new List<Brand>();
                var sql = @"SELECT BrandID,
                            BrandName
                      FROM Brands";
                MySqlParameter[] parameters = [];
                using var reader = DatabaseHelper.ExecuteReader(sql, parameters);
                while (reader.Read())
                    list.Add(new Brand
                    {
                        BrandID = reader["BrandID"] != DBNull.Value ? Convert.ToInt32(reader["BrandID"]) : -1,
                        BrandName = reader["BrandName"] != DBNull.Value ? reader["BrandName"].ToString()! : string.Empty
                    });
                apiResponse.Parameters["Brands"] = list;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpGet("GetModels")]
        public ApiResponse GetModels()
        {
            var apiResponse = new ApiResponse();
            try
            {
                var list = new List<Model>();
                var sql = @"
SELECT cm.ModelID,
       cm.ModelName,
       comp.BrandID,
       comp.BrandName
  FROM Models cm
  INNER JOIN Garagecom.Brands comp ON cm.BrandID = comp.BrandID";
                MySqlParameter[] parameters = [];
                using var reader = DatabaseHelper.ExecuteReader(sql, parameters);
                while (reader.Read())
                    list.Add(new Model
                    {
                        ModelID = reader["ModelID"] != DBNull.Value ? Convert.ToInt32(reader["ModelID"]) : -1,
                        ModelName =
                            reader["ModelName"] != DBNull.Value ? reader["ModelName"].ToString()! : string.Empty,
                        Brand = new Brand
                        {
                            BrandID = reader["BrandID"] != DBNull.Value ? Convert.ToInt32(reader["BrandID"]) : -1,
                            BrandName = reader["BrandName"] != DBNull.Value
                                ? reader["BrandName"].ToString()!
                                : string.Empty
                        }
                    });

                apiResponse.Parameters["Models"] = list;
                apiResponse.Succeeded = true;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpGet("GetParts")]
        public ApiResponse GetParts()
        {
            var apiResponse = new ApiResponse();
            try
            {
                var list = new List<Part>();
                var sql = @"
SELECT PartID,
       PartName
  FROM Parts";
                MySqlParameter[] parameters = [];
                using var reader = DatabaseHelper.ExecuteReader(sql, parameters);
                while (reader.Read())
                    list.Add(new Part
                    {
                        PartID = reader["PartID"] != DBNull.Value ? Convert.ToInt32(reader["PartID"]) : -1,
                        PartName = reader["PartName"] != DBNull.Value ? reader["PartName"].ToString()! : string.Empty
                    });

                apiResponse.Parameters["Parts"] = list;
                apiResponse.Succeeded = true;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        #endregion

        #region Cars

        [HttpGet("GetUserCars")]
        public ApiResponse GetUserCars()
        {
            var r = new ApiResponse();
            try
            {
                var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
                var list = new List<Car>();

                var sql = @"
SELECT c.CarID,
       c.Year,
       cm.ModelID, cm.ModelName,
       comp.BrandID, comp.BrandName,
         c.Nickname, c.Kilos
  FROM Cars c
  JOIN Models cm ON c.ModelID = cm.ModelID
  JOIN Brands comp ON cm.BrandID = comp.BrandID
  JOIN Statuses s   ON c.StatusID    = s.StatusID
 WHERE s.Status = @st
   AND c.UserID = @uid";
                MySqlParameter[] ps =
                {
                    new("st", "Active"),
                    new("uid", userId)
                };

                using var reader = DatabaseHelper.ExecuteReader(sql, ps);
                while (reader.Read())
                    list.Add(new Car
                    {
                        CarID = reader["CarID"] != DBNull.Value ? Convert.ToInt32(reader["CarID"]) : -1,
                        Year = reader["Year"] != DBNull.Value ? Convert.ToInt32(reader["Year"]) : 0,
                        Model = new Model
                        {
                            ModelID = reader["ModelID"] != DBNull.Value
                                ? Convert.ToInt32(reader["ModelID"])
                                : -1,
                            ModelName = reader["ModelName"] != DBNull.Value
                                ? reader["ModelName"].ToString()!
                                : string.Empty,
                            Brand = new Brand
                            {
                                BrandID = reader["BrandID"] != DBNull.Value ? Convert.ToInt32(reader["BrandID"]) : -1,
                                BrandName = reader["BrandName"] != DBNull.Value
                                    ? reader["BrandName"].ToString()!
                                    : string.Empty
                            }
                        },
                        Nickname = reader["Nickname"] != DBNull.Value ? reader["Nickname"].ToString()! : string.Empty,
                        Kilos = reader["Kilos"] != DBNull.Value ? Convert.ToInt32(reader["Kilos"]) : null
                    });

                var partSql = @"
SELECT cp.CarPartID,
       cp.PartID,
       p.PartName,
       cp.LifeTimeInterval,
        COALESCE(
                (
                    SELECT MAX(r.CreatedIn)
                    FROM CarPartsRenewal r
                    WHERE r.CarPartID = cp.CarPartID
                    ORDER BY r.CreatedIn DESC
                    LIMIT 1
                ),
                cp.CreatedIn
        ) AS CreatedIn,
       -- cp.CreatedIn AS CreatedIn,
       DATE_ADD(
               COALESCE(
                       (
                           SELECT DATE_ADD(r.CreatedIn, INTERVAL 1 DAY)
                           FROM CarPartsRenewal r
                           WHERE r.CarPartID = cp.CarPartID
                           ORDER BY r.CreatedIn DESC
                           LIMIT 1
                       ),
                       cp.CreatedIn
               ),
               INTERVAL cp.LifeTimeInterval MONTH
       ) AS NextDueDate,
       cp.Notes
FROM CarParts cp
         JOIN Parts p ON cp.PartID = p.PartID
         JOIN Statuses s ON cp.StatusID = s.StatusID
WHERE cp.CarID = @cid AND s.Status = 'Active'";

                foreach (var car in list)
                {
                    using var reader2 = DatabaseHelper.ExecuteReader(partSql,
                        [new MySqlParameter("cid", car.CarID)]);
                    while (reader2.Read())
                        car.Parts.Add(new CarPart
                        {
                            CarPartID = reader2["CarPartID"] != DBNull.Value
                                ? Convert.ToInt32(reader2["CarPartID"])
                                : -1,
                            Part = new Part
                            {
                                PartID = reader2["PartID"] != DBNull.Value ? Convert.ToInt32(reader2["PartID"]) : -1,
                                PartName = reader2["PartName"] != DBNull.Value
                                    ? reader2["PartName"].ToString()!
                                    : string.Empty
                            },
                            LifeTimeInterval = reader2["LifeTimeInterval"] != DBNull.Value
                                ? Convert.ToInt32(reader2["LifeTimeInterval"])
                                : 0,
                            CreatedIn = reader2["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader2["CreatedIn"])
                                : DateTime.MinValue,
                            NextDueDate = reader2["NextDueDate"] != DBNull.Value
                                ? Convert.ToDateTime(reader2["NextDueDate"])
                                : DateTime.MinValue,
                            Notes = (reader2["Notes"] == DBNull.Value ? "" : reader2["Notes"].ToString()) ??
                                    string.Empty
                        });
                }

                r.Parameters["UserCars"] = list;
                r.Succeeded = true;
            }
            catch (Exception ex)
            {
                r.Succeeded = false;
                r.Message = ex.Message;
            }

            return r;
        }

        [HttpPost("UpdateUserCar")]
        public ApiResponse UpdateUserCar(int carId, string? nickname, int? kilos, int? year)
        {
            nickname = nickname?.SanitizeFileName();
            if (year == null || year < 1900 || year > DateTime.Now.Year)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Invalid year"
                };
            if (kilos != null && kilos < 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Invalid kilos"
                };
            if (string.IsNullOrEmpty(nickname) || nickname.Length == 0)
                nickname = string.Empty;
            if (nickname.Length > 50)
                nickname = nickname[..50];
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            if (userId == -1)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "User not found"
                };
            if (carId <= 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Invalid car ID"
                };
            var sql =
                @"SELECT UserID FROM Cars WHERE CarID = @cid AND UserID = @uid";
            MySqlParameter[] parameters =
            {
                new("cid", carId),
                new("uid", userId)
            };
            var userIdFromDb =
                int.Parse(DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString() ??
                          string.Empty);
            if (userIdFromDb != userId)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Car not found"
                };
            var apiResponse = new ApiResponse();
            try
            {
                sql =
                    @"UPDATE Cars SET Nickname = IFNULL(@nick, Nickname), Kilos = IFNULL(@kilos, Kilos), Year = IFNULL(@y, Year) WHERE CarID = @cid";
                parameters =
                [
                    new MySqlParameter("uid", userId),
                    new MySqlParameter("nick", nickname),
                    new MySqlParameter("kilos", kilos),
                    new MySqlParameter("y", year),
                    new MySqlParameter("cid", carId)
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

        [HttpPost("SetCar")]
        public ApiResponse SetCar(int modelId, string? nickname, int? kilos, int year)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                nickname = nickname?.SanitizeFileName();
                if (year < 1900 || year > DateTime.Now.Year)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "Invalid year"
                    };
                if (kilos != null && kilos < 0)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "Invalid kilos"
                    };
                if (string.IsNullOrEmpty(nickname) || nickname.Length == 0)
                    nickname = string.Empty;
                if (nickname.Length > 50)
                    nickname = nickname[..50];
                if (userId == -1)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "User not found"
                    };
                if (modelId <= 0)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "Invalid model ID"
                    };
                var sql = @"SELECT ModelID FROM Models WHERE ModelID = @mid";
                MySqlParameter[] parameters =
                {
                    new("mid", modelId)
                };
                var modelIdFromDb =
                    int.Parse(DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString() ??
                              string.Empty);
                if (modelIdFromDb != modelId)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "Model not found"
                    };
                sql =
                    @"SELECT StatusID INTO @StatusID
FROM Statuses WHERE Status = @StatusName ; INSERT INTO Cars (ModelID, UserID, Nickname, Kilos, Year, CreatedIn, StatusID) VALUES (@cmid, @uid, @nick, @kilos, @y, NOW(), @StatusID)";
                parameters =
                [
                    new MySqlParameter("cmid", modelId),
                    new MySqlParameter("uid", userId),
                    new MySqlParameter("nick", nickname),
                    new MySqlParameter("kilos", kilos),
                    new MySqlParameter("y", year),
                    new MySqlParameter("StatusName", "Active")
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

        [HttpPost("DeleteCar")]
        public ApiResponse DeleteCar(int carId)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                if (carId <= 0)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "Invalid car ID"
                    };
                var sql = @"SELECT UserID FROM Cars WHERE CarID = @cid AND UserID = @uid";
                MySqlParameter[] parameters =
                {
                    new("cid", carId),
                    new("uid", userId)
                };
                var userIdFromDb =
                    int.Parse(DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString() ??
                              string.Empty);
                if (userIdFromDb != userId)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "Car not found"
                    };
                sql =
                    @"UPDATE Cars SET StatusID = (SELECT StatusID FROM Statuses WHERE Status = @st) WHERE CarID = @cid";
                parameters =
                [
                    new MySqlParameter("st", "Inactive"),
                    new MySqlParameter("cid", carId)
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

        #region Parts

        [HttpPost("SetCarPart")]
        public ApiResponse SetCarPart(int carId, int partId, int lifeTimeInterval, DateOnly lastReplacementDate,
            string? notes)
        {
            notes = notes?.SanitizeFileName();
            if (carId <= 0 || partId <= 0 || lifeTimeInterval <= 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Invalid parameters"
                };
            if (lastReplacementDate == DateOnly.MinValue || lastReplacementDate == DateOnly.MaxValue ||
                lastReplacementDate == DateOnly.FromDateTime(DateTime.MinValue))
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Invalid last replacement date"
                };
            if (string.IsNullOrEmpty(notes) || notes.Length == 0)
                notes = string.Empty;
            if (notes.Length > 255)
                notes = notes[..255];
            var r = new ApiResponse();
            try
            {
                var sql =
                    @"SELECT StatusID INTO @StatusID
FROM Statuses WHERE Status = 'Active' ;
INSERT INTO CarParts (CarID, PartID, LifeTimeInterval, CreatedIn, Notes, StatusID) VALUES (@cid, @pid, @lti, @LastReplacementDate, @Notes, @StatusID)";
                MySqlParameter[] parameters =
                [
                    new("cid", carId),
                    new("pid", partId),
                    new("lti", lifeTimeInterval),
                    new("Notes", notes),
                    new("LastReplacementDate", lastReplacementDate.ToString("yyyy-MM-dd"))
                ];
                r = DatabaseHelper.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception ex)
            {
                r.Succeeded = false;
                r.Message = ex.Message;
            }

            return r;
        }

        [HttpPost("UpdateCarPart")]
        public ApiResponse UpdateCarPart(int carPartId, int lifeTimeInterval, DateOnly lastReplacementDate,
            string? notes)
        {
            notes = notes?.SanitizeFileName();
            if (lifeTimeInterval <= 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Invalid parameters"
                };
            if (lastReplacementDate == DateOnly.MinValue || lastReplacementDate == DateOnly.MaxValue ||
                lastReplacementDate == DateOnly.FromDateTime(DateTime.MinValue))
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Invalid last replacement date"
                };
            if (string.IsNullOrEmpty(notes) || notes.Length == 0)
                notes = string.Empty;
            if (notes.Length > 255)
                notes = notes[..255];

            var r = new ApiResponse();
            try
            {
                var sql =
                    @"DELETE FROM CarPartsRenewal WHERE CarPartID = @cpid AND @lrd != (SELECT CreatedIn FROM CarParts CP WHERE CP.CarPartID = @cpid);
                    UPDATE CarParts SET LifeTimeInterval = @lti, Notes = @Notes, CreatedIn = @lrd WHERE CarPartID = @cpid;";
                MySqlParameter[] parameters =
                {
                    new("cpid", carPartId),
                    new("lti", lifeTimeInterval),
                    new("Notes", notes),
                    new("lrd", lastReplacementDate.ToString("yyyy-MM-dd"))
                };
                r = DatabaseHelper.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception ex)
            {
                r.Succeeded = false;
                r.Message = ex.Message;
            }

            return r;
        }

        [HttpPost("DeleteCarPart")]
        public ApiResponse DeleteCarPart(int carPartId)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                if (carPartId <= 0)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "Invalid car part ID"
                    };
                var sql = @"SELECT CarID FROM CarParts WHERE CarPartID = @cpid";
                MySqlParameter[] parameters =
                {
                    new("cpid", carPartId)
                };
                var carIdFromDb =
                    int.Parse(DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString() ??
                              string.Empty);
                if (carIdFromDb == -1)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "Car part not found"
                    };
                sql = @"SELECT UserID FROM Cars WHERE CarID = @cid AND UserID = @uid";
                parameters =
                [
                    new MySqlParameter("cid", carIdFromDb),
                    new MySqlParameter("uid", userId)
                ];
                var userIdFromDb =
                    int.Parse(DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString() ??
                              string.Empty);
                if (userIdFromDb != userId)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "Car part not found"
                    };
                sql =
                    @"UPDATE CarParts SET StatusID = (SELECT StatusID FROM Statuses WHERE Status = @st) WHERE CarPartID = @cpid";
                parameters =
                [
                    new MySqlParameter("st", "InActive"),
                    new MySqlParameter("cpid", carPartId)
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

        [HttpPost("RenewCarPart")]
        public ApiResponse RenewCarPart(int carPartId)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var r = new ApiResponse();
            try
            {
                if (carPartId <= 0)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "Invalid car part ID"
                    };
                var sql = @"SELECT CarID FROM CarParts WHERE CarPartID = @cpid";
                MySqlParameter[] parameters =
                {
                    new("cpid", carPartId)
                };
                var carIdFromDb =
                    int.Parse(DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString() ??
                              string.Empty);
                if (carIdFromDb == -1)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "Car part not found"
                    };
                sql = @"SELECT UserID FROM Cars WHERE CarID = @cid AND UserID = @uid";
                parameters =
                [
                    new MySqlParameter("cid", carIdFromDb),
                    new MySqlParameter("uid", userId)
                ];
                var userIdFromDb =
                    int.Parse(DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString() ??
                              string.Empty);
                if (userIdFromDb != userId)
                    return new ApiResponse
                    {
                        Succeeded = false,
                        Message = "Car part not found"
                    };
                sql = @"INSERT INTO CarPartsRenewal (CarPartID, CreatedIn) VALUES (@cpid, NOW());
-- UPDATE CarParts SET CreatedIn = NOW() WHERE CarPartID = @cpid";
                var ps = new[] { new MySqlParameter("cpid", carPartId) };
                r = DatabaseHelper.ExecuteNonQuery(sql, ps);
            }
            catch (Exception ex)
            {
                r.Succeeded = false;
                r.Message = ex.Message;
            }

            return r;
        }

        #endregion
    }
}
#region

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

public class CarModel
{
    public int CarModelID { get; set; }
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
}

public class Car
{
    public int CarID { get; set; } // maps to Cars.CarID
    public int Year { get; set; }
    public string Nickname { get; set; }
    public double Kilos { get; set; }
    public CarModel CarModel { get; set; }
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

        [HttpGet("GetCarModels")]
        public ApiResponse GetCarModels()
        {
            var apiResponse = new ApiResponse();
            try
            {
                var list = new List<CarModel>();
                var sql = @"
SELECT cm.CarModelID,
       cm.ModelName,
       comp.BrandID,
       comp.BrandName
  FROM CarModels cm
  INNER JOIN Garagecom.Brands comp ON cm.BrandID = comp.BrandID";
                MySqlParameter[] parameters = [];
                using var reader = DatabaseHelper.ExecuteReader(sql, parameters);
                while (reader.Read())
                    list.Add(new CarModel
                    {
                        CarModelID = reader["CarModelID"] != DBNull.Value ? Convert.ToInt32(reader["CarModelID"]) : -1,
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

                apiResponse.Parameters["CarModels"] = list;
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
                var userId = HttpContext.Items["UserID"] as int? ?? -1;
                var list = new List<Car>();

                var sql = @"
SELECT c.CarID,
       c.Year,
       cm.CarModelID, cm.ModelName,
       comp.BrandID, comp.BrandName,
         c.Nickname, c.Kilos
  FROM Cars c
  JOIN CarModels cm ON c.CarModelID = cm.CarModelID
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
                        CarModel = new CarModel
                        {
                            CarModelID = reader["CarModelID"] != DBNull.Value
                                ? Convert.ToInt32(reader["CarModelID"])
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
                        Kilos = reader["Kilos"] != DBNull.Value ? Convert.ToDouble(reader["Kilos"]) : 0
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
       ) AS NextDueDate
  FROM CarParts cp
  JOIN Parts p ON cp.PartID = p.PartID
 WHERE cp.CarID = @cid";

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
                                : DateTime.MinValue
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

        [HttpPost("SetCar")]
        public ApiResponse SetCar(int carModelId, string nickname, double kilos, int year)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                var sql =
                    @"INSERT INTO Cars (CarModelID, UserID, Nickname, Kilos, Year, CreatedIn) VALUES (@cmid, @uid, @nick, @kilos, @y, NOW())";
                MySqlParameter[] parameters =
                {
                    new("cmid", carModelId),
                    new("uid", userId),
                    new("nick", nickname),
                    new("kilos", kilos),
                    new("y", year)
                };
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
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                var sql = @"UPDATE Cars SET StatusID = (SELECT StatusID FROM Statuses WHERE Status = @st) WHERE CarID = @cid";
                MySqlParameter[] parameters =
                {
                    new("st", "Inactive"),
                    new("cid", carId)
                };
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
        public ApiResponse SetCarPart(int carId, int partId, int lifeTimeInterval)
        {
            var r = new ApiResponse();
            try
            {
                var sql =
                    @"INSERT INTO CarParts (CarID, PartID, LifeTimeInterval, CreatedIn) VALUES (@cid, @pid, @lti, NOW())";
                MySqlParameter[] parameters =
                [
                    new("cid", carId),
                    new("pid", partId),
                    new("lti", lifeTimeInterval)
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

        [HttpPost("RenewCarPart")]
        public ApiResponse RenewCarPart(int carPartId)
        {
            var r = new ApiResponse();
            try
            {
                var sql = @"INSERT INTO CarPartsRenewal (CarPartID, CreatedIn) VALUES (@cpid, NOW())";
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
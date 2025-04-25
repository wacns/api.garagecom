#region

using api.garagecom.Filters;
using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

#endregion

public class CarModel
{
    public int CarModelID { get; set; }
    public string ModelName { get; set; }
}

public class CarType
{
    public int CarTypeID { get; set; }
    public string CarTypeName { get; set; }
}

public class Part
{
    public int PartID { get; set; }
    public string PartName { get; set; }
}

public class UserCar
{
    public int UserCarID { get; set; }
    public CarModel CarModel { get; set; }
    public CarType CarType { get; set; }
}

public class UserPart
{
    public int UserCarPartID { get; set; }
    public Part Part { get; set; }
    public int UserCarID { get; set; }
    public int LifeTimeInterval { get; set; } // stored in months
}

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
            var r = new ApiResponse();
            try
            {
                var list = new List<CarModel>();
                var sql = "SELECT CarModelID, ModelName FROM CarModels";
                using var rd = DatabaseHelper.ExecuteReader(sql, new MySqlParameter[] { });
                while (rd.Read())
                    list.Add(new CarModel
                    {
                        CarModelID = Convert.ToInt32(rd["CarModelID"]),
                        ModelName = rd["ModelName"].ToString()!
                    });

                r.Parameters["CarModels"] = list;
                r.Succeeded = true;
            }
            catch (Exception ex)
            {
                r.Succeeded = false;
                r.Message = ex.Message;
            }

            return r;
        }

        [HttpGet("GetCarTypes")]
        public ApiResponse GetCarTypes()
        {
            var r = new ApiResponse();
            try
            {
                var list = new List<CarType>();
                var sql = "SELECT CarTypeID, CarTypeName FROM CarTypes";
                using var rd = DatabaseHelper.ExecuteReader(sql, []);
                while (rd.Read())
                    list.Add(new CarType
                    {
                        CarTypeID = Convert.ToInt32(rd["CarTypeID"]),
                        CarTypeName = rd["CarTypeName"].ToString()!
                    });

                r.Parameters["CarTypes"] = list;
                r.Succeeded = true;
            }
            catch (Exception ex)
            {
                r.Succeeded = false;
                r.Message = ex.Message;
            }

            return r;
        }

        [HttpGet("GetParts")]
        public ApiResponse GetParts()
        {
            var r = new ApiResponse();
            try
            {
                var list = new List<Part>();
                var sql = "SELECT PartID, PartName FROM Parts";
                using var rd = DatabaseHelper.ExecuteReader(sql, new MySqlParameter[] { });
                while (rd.Read())
                    list.Add(new Part
                    {
                        PartID = Convert.ToInt32(rd["PartID"]),
                        PartName = rd["PartName"].ToString()!
                    });

                r.Parameters["Parts"] = list;
                r.Succeeded = true;
            }
            catch (Exception ex)
            {
                r.Succeeded = false;
                r.Message = ex.Message;
            }

            return r;
        }

        #endregion

        #region UserCars

        [HttpGet("GetUserCars")]
        public ApiResponse GetUserCars()
        {
            var r = new ApiResponse();
            try
            {
                var userId = HttpContext.Items["UserID"] as int? ?? -1;
                var list = new List<UserCar>();
                var sql = @"
SELECT uc.UserCarID,
       cm.CarModelID, cm.ModelName,
       ct.CarTypeID, ct.CarTypeName
  FROM UserCars uc
  JOIN Cars c       ON uc.CarID     = c.CarID
  JOIN CarModels cm ON c.CarModelID = cm.CarModelID
  JOIN CarTypes ct  ON c.CarTypeID  = ct.CarTypeID
  JOIN Statuses s   ON uc.StatusID  = s.StatusID
 WHERE s.Status = @st
   AND uc.UserID = @uid
ORDER BY uc.CreatedIn DESC";
                var ps = new[]
                {
                    new MySqlParameter("st", "Active"),
                    new MySqlParameter("uid", userId)
                };

                using var rd = DatabaseHelper.ExecuteReader(sql, ps);
                while (rd.Read())
                    list.Add(new UserCar
                    {
                        UserCarID = Convert.ToInt32(rd["UserCarID"]),
                        CarModel = new CarModel
                        {
                            CarModelID = Convert.ToInt32(rd["CarModelID"]),
                            ModelName = rd["ModelName"].ToString()!
                        },
                        CarType = new CarType
                        {
                            CarTypeID = Convert.ToInt32(rd["CarTypeID"]),
                            CarTypeName = rd["CarTypeName"].ToString()!
                        }
                    });

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

        [HttpGet("GetUserCar")]
        public ApiResponse GetUserCar(int userCarId)
        {
            var r = new ApiResponse();
            try
            {
                var userId = HttpContext.Items["UserID"] as int? ?? -1;
                var sql = @"
SELECT uc.UserCarID,
       cm.CarModelID, cm.ModelName,
       ct.CarTypeID, ct.CarTypeName
  FROM UserCars uc
  JOIN Cars c       ON uc.CarID     = c.CarID
  JOIN CarModels cm ON c.CarModelID = cm.CarModelID
  JOIN CarTypes ct  ON c.CarTypeID  = ct.CarTypeID
  JOIN Statuses s   ON uc.StatusID  = s.StatusID
 WHERE s.Status     = @st
   AND uc.UserID    = @uid
   AND uc.UserCarID = @ucid";
                var ps = new[]
                {
                    new MySqlParameter("st", "Active"),
                    new MySqlParameter("uid", userId),
                    new MySqlParameter("ucid", userCarId)
                };

                var car = new UserCar();
                using var rd = DatabaseHelper.ExecuteReader(sql, ps);
                if (rd.Read())
                    car = new UserCar
                    {
                        UserCarID = Convert.ToInt32(rd["UserCarID"]),
                        CarModel = new CarModel
                        {
                            CarModelID = Convert.ToInt32(rd["CarModelID"]),
                            ModelName = rd["ModelName"].ToString()!
                        },
                        CarType = new CarType
                        {
                            CarTypeID = Convert.ToInt32(rd["CarTypeID"]),
                            CarTypeName = rd["CarTypeName"].ToString()!
                        }
                    };

                r.Parameters["UserCar"] = car;
                r.Succeeded = true;
            }
            catch (Exception ex)
            {
                r.Succeeded = false;
                r.Message = ex.Message;
            }

            return r;
        }

        [HttpPost("SetUserCar")]
        public ApiResponse SetUserCar(int carModelId, int carTypeId, int year)
        {
            var r = new ApiResponse();
            try
            {
                var userId = HttpContext.Items["UserID"] as int? ?? -1;

                // 1) create Cars row
                var sql1 = @"
INSERT INTO Cars (CarModelID, CarTypeID, `Year`)
VALUES (@cm, @ct, @yr);
SELECT LAST_INSERT_ID();";
                var p1 = new[]
                {
                    new MySqlParameter("cm", carModelId),
                    new MySqlParameter("ct", carTypeId),
                    new MySqlParameter("yr", year)
                };
                var sc = DatabaseHelper.ExecuteScalar(sql1, p1);

                if (sc.Succeeded)
                {
                    var carId = Convert.ToInt32(sc.Parameters["Result"]);

                    // 2) link into UserCars
                    var sql2 = @"
SELECT Status INTO @sid
  FROM Statuses
 WHERE Status = @st;

INSERT INTO UserCars (UserID, CarID, CreatedIn, StatusID)
VALUES (@uid, @cid, NOW(), @sid);";
                    var p2 = new[]
                    {
                        new MySqlParameter("st", "Active"),
                        new MySqlParameter("uid", userId),
                        new MySqlParameter("cid", carId)
                    };
                    r = DatabaseHelper.ExecuteNonQuery(sql2, p2);
                }

                r.Succeeded = true;
            }
            catch (Exception ex)
            {
                r.Succeeded = false;
                r.Message = ex.Message;
            }

            return r;
        }

        [HttpDelete("DeleteUserCar")]
        public ApiResponse DeleteUserCar(int userCarId)
        {
            var r = new ApiResponse();
            try
            {
                var sql = @"
SELECT Status INTO @sid
  FROM Statuses
 WHERE Status = @st;
UPDATE UserCars
   SET StatusID   = @sid,
       ModifiedIn = NOW()
 WHERE UserCarID = @ucid";
                var ps = new[]
                {
                    new MySqlParameter("st", "InActive"),
                    new MySqlParameter("ucid", userCarId)
                };
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

        #region UserCarParts

        [HttpPost("SetUserCarPart")]
        public ApiResponse SetUserCarPart(int userCarId, int partId, int lifeTimeInterval)
        {
            var r = new ApiResponse();
            try
            {
                var sql = @"
INSERT INTO UserCarParts (PartID, UserCarID, LifeTimeInterval)
VALUES (@pid, @ucid, @lti)";
                var ps = new[]
                {
                    new MySqlParameter("pid", partId),
                    new MySqlParameter("ucid", userCarId),
                    new MySqlParameter("lti", lifeTimeInterval)
                };
                r = DatabaseHelper.ExecuteNonQuery(sql, ps);
            }
            catch (Exception ex)
            {
                r.Succeeded = false;
                r.Message = ex.Message;
            }

            return r;
        }

        [HttpGet("GetUserCarParts")]
        public ApiResponse GetUserCarParts(int userCarId)
        {
            var r = new ApiResponse();
            try
            {
                var list = new List<UserPart>();
                var sql = @"
SELECT ucp.UserCarPartID,
       ucp.PartID, p.PartName,
       ucp.UserCarID,
       ucp.LifeTimeInterval
  FROM UserCarParts ucp
  JOIN Parts p ON ucp.PartID = p.PartID
 WHERE ucp.UserCarID = @ucid";
                var ps = new[]
                {
                    new MySqlParameter("ucid", userCarId)
                };

                using var rd = DatabaseHelper.ExecuteReader(sql, ps);
                while (rd.Read())
                    list.Add(new UserPart
                    {
                        UserCarPartID = Convert.ToInt32(rd["UserCarPartID"]),
                        Part = new Part
                        {
                            PartID = Convert.ToInt32(rd["PartID"]),
                            PartName = rd["PartName"].ToString()!
                        },
                        UserCarID = Convert.ToInt32(rd["UserCarID"]),
                        LifeTimeInterval = Convert.ToInt32(rd["LifeTimeInterval"])
                    });

                r.Parameters["UserCarParts"] = list;
                r.Succeeded = true;
            }
            catch (Exception ex)
            {
                r.Succeeded = false;
                r.Message = ex.Message;
            }

            return r;
        }

        [HttpGet("GetUserCarPart")]
        public ApiResponse GetUserCarPart(int userCarPartId)
        {
            var r = new ApiResponse();
            try
            {
                var sql = @"
SELECT ucp.UserCarPartID,
       ucp.PartID, p.PartName,
       ucp.UserCarID,
       ucp.LifeTimeInterval
  FROM UserCarParts ucp
  JOIN Parts p ON ucp.PartID = p.PartID
 WHERE ucp.UserCarPartID = @ucpid";
                var ps = new[]
                {
                    new MySqlParameter("ucpid", userCarPartId)
                };

                var part = new UserPart();
                using var rd = DatabaseHelper.ExecuteReader(sql, ps);
                if (rd.Read())
                    part = new UserPart
                    {
                        UserCarPartID = Convert.ToInt32(rd["UserCarPartID"]),
                        Part = new Part
                        {
                            PartID = Convert.ToInt32(rd["PartID"]),
                            PartName = rd["PartName"].ToString()!
                        },
                        UserCarID = Convert.ToInt32(rd["UserCarID"]),
                        LifeTimeInterval = Convert.ToInt32(rd["LifeTimeInterval"])
                    };

                r.Parameters["UserCarPart"] = part;
                r.Succeeded = true;
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
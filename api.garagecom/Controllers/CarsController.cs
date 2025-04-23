using api.garagecom.Filters;
using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace api.garagecom.Controllers
{
    [ActionFilter]
    [Route("api/[controller]")]
    public class CarsController : Controller
    {
        // Get ComboBoxes
        // Add Car
        // Get Car
        // Delete Car
        // Add Part
        // Get Part
    }

    public class Part
    {
        public int PartID { get; set; }
        public string PartName { get; set; }
    }

    public class UserPart
    {
        public int UserPartID { get; set; }
        public Part Part { get; set; }
        public int UserCarID { get; set; }
        public string DueDate { get; set; }
        public string RepairDate { get; set; }
    }

    public class UserCar
    {
        public int UserCarID { get; set; }
        public int CarModelID { get; set; }
        public string CarModelName { get; set; }
        public int CarTypeID { get; set; }
        public string CarTypeName { get; set; }
    }
}


#region

using System.Security.Cryptography;
using System.Text;
using MySql.Data.MySqlClient;

#endregion

namespace api.garagecom.Utils;

public static class GeneralHelper
{
    [Obsolete("Obsolete")]
    public static string HashEncrypt(string? input)
    {
        input ??= "";

        var passwordByte = Encoding.ASCII.GetBytes(input);
        using var mD5CryptoServiceProvider = new MD5CryptoServiceProvider();
        var passwordHash = mD5CryptoServiceProvider.ComputeHash(passwordByte);

        return Convert.ToBase64String(passwordHash);
    }

    public static string? GetDeviceTokenByUserId(int userId)
    {
        var sql = "SELECT DeviceToken FROM Logins WHERE UserID = @UserID ORDER BY Logins.CreatedIn DESC LIMIT 1";
        MySqlParameter[] parameters =
        {
            new("UserID", userId)
        };
        var deviceToken = DatabaseHelper.ExecuteScalar(sql, parameters);
        return deviceToken?.ToString();
    }
}
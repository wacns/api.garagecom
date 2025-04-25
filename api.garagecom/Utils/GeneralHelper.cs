#region

using System.Security.Cryptography;
using System.Text;

#endregion

namespace api.garagecom.Utils;

public static class GeneralHelper
{
    public static string HashEncrypt(string? input)
    {
        input ??= "";

        var passwordByte = Encoding.ASCII.GetBytes(input);
        using (var mD5CryptoServiceProvider = new MD5CryptoServiceProvider())
        {
            var passwordHash = mD5CryptoServiceProvider.ComputeHash(passwordByte);

            return Convert.ToBase64String(passwordHash);
        }
    }
}
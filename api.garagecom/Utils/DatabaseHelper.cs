#region

using System.Data;
using System.Runtime.CompilerServices;
using System.Web;
using MySql.Data.MySqlClient;

#endregion

namespace api.garagecom.Utils;

public class DatabaseHelper
{
    public static ApiResponse ExecuteNonQuery(string query, MySqlParameter[] parameters)
    {
        var result = new ApiResponse();
        var con = new MySqlConnection(Globals.ConnString);
        MySqlTransaction myTrans = null!;

        try
        {
            con.Open();
            myTrans = con.BeginTransaction();

            using (var cmd = new MySqlCommand(query, con, myTrans))
            {
                if (parameters.Length > 0)
                    foreach (var p in parameters)
                        if (p.Value != null && p.Value != DBNull.Value)
                            cmd.Parameters.AddWithValue(p.ParameterName,
                                p.DbType == DbType.String
                                    ? HttpUtility.HtmlEncode(p.Value).Replace("&quot;", "\"").Replace("&amp;", "&")
                                        .Replace("&#39;", "'")
                                    : p.Value);

                cmd.CommandTimeout = 3600;
                result.Succeeded = cmd.ExecuteNonQuery() > 0;
                myTrans.Commit();
            }

            con.Close();
            con.ClearPoolAsync(con).GetAwaiter();
            con.Dispose();
        }
        catch (Exception ex)
        {
            myTrans?.Rollback();
            con.Close();
            con.ClearPoolAsync(con).GetAwaiter();
            con.Dispose();
            result.Succeeded = false;
            result.Message = ex.Message;
            throw;
        }

        return result;
    }

    public static ApiResponse ExecuteScalar(string query, MySqlParameter[] parameters,
        [CallerMemberName] string memberName = "")
    {
        var result = new ApiResponse();
        var con = new MySqlConnection(Globals.ConnString);
        MySqlTransaction myTrans = null!;

        try
        {
            con.Open();
            myTrans = con.BeginTransaction();

            using (var cmd = new MySqlCommand(query, con))
            {
                if (parameters.Length > 0)
                    foreach (var p in parameters)
                        if (p.Value != null && p.Value != DBNull.Value)
                            cmd.Parameters.AddWithValue(p.ParameterName,
                                p.DbType == DbType.String
                                    ? HttpUtility.HtmlEncode(p.Value).Replace("&quot;", "\"").Replace("&amp;", "&")
                                        .Replace("&#39;", "'")
                                    : p.Value);

                result.Parameters.Add("Result", cmd.ExecuteScalar()!);
                result.Succeeded = true;
                myTrans.Commit();
            }

            con.Close();
            con.ClearPoolAsync(con).GetAwaiter();
            con.Dispose();
        }
        catch (Exception ex)
        {
            myTrans?.Rollback();
            con.Close();
            con.ClearPoolAsync(con).GetAwaiter();
            con.Dispose();
            result.Parameters.Add("Result", "");
            result.Succeeded = false;
            result.Message = ex.Message;
            throw;
        }

        return result;
    }

    public static MySqlDataReader ExecuteReader(string query, MySqlParameter[] parameters,
        [CallerMemberName] string memberName = "")
    {
        var con = new MySqlConnection(Globals.ConnString);

        try
        {
            con.Open();

            var cmd = new MySqlCommand(query, con);

            if (parameters.Length > 0)
                foreach (var p in parameters)
                    if (p.Value != null && p.Value != DBNull.Value)
                        cmd.Parameters.AddWithValue(p.ParameterName,
                            p.DbType == DbType.String
                                ? HttpUtility.HtmlEncode(p.Value).Replace("&quot;", "\"").Replace("&amp;", "&")
                                    .Replace("&#39;", "'")
                                : p.Value);

            var dr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            return dr;
        }
        catch (MySqlException)
        {
            con.Close();
            con.ClearPoolAsync(con).GetAwaiter();
            con.Dispose();
            throw;
        }
    }
}
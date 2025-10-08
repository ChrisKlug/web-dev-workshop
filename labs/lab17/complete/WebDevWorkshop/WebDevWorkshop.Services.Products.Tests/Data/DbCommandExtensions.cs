using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace WebDevWorkshop.Services.Products.Tests.Data;

public static class DbCommandExtensions
{
    public static async Task<int> AddProduct(this DbCommand cmd,
                                        string name,
                                        string description,
                                        decimal price,
                                        bool isFeatured,
                                        string imageName)
    {
        cmd.CommandText = "INSERT INTO Products (Name, Description, Price, IsFeatured, ThumbnailUrl, ImageUrl) " +
                      "VALUES (@Name, @Description, @Price, @IsFeatured, @ThumbnailUrl, @ImageUrl); " +
                      "SELECT SCOPE_IDENTITY();";

        cmd.Parameters.Add(new SqlParameter("@Name", name));
        cmd.Parameters.Add(new SqlParameter("Description", description));
        cmd.Parameters.Add(new SqlParameter("Price", price));
        cmd.Parameters.Add(new SqlParameter("IsFeatured", isFeatured));
        cmd.Parameters.Add(new SqlParameter("ThumbnailUrl", $"{imageName}_thumbnail.jpg"));
        cmd.Parameters.Add(new SqlParameter("ImageUrl", $"{imageName}.jpg"));

        var ret = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        cmd.Parameters.Clear();

        return ret;
    }
}

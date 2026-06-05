using System.Collections.Generic;
using System.Threading.Tasks;
using MyMiniCar.Web.Models;

namespace MyMiniCar.Web.Services;

public interface IProductService
{
    Task<IEnumerable<Product>> GetProductsAsync();
    Task<IEnumerable<Product>> GetFeaturedProductsAsync();
    Task<Product?> GetProductByIdAsync(string id);
}

namespace MyMiniCar.Web.Models;

public class CartItem
{
    public Product Product { get; set; } = new();
    public string SelectedMaterial { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

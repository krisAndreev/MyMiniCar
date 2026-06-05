using System;
using System.Collections.Generic;
using System.Linq;
using MyMiniCar.Web.Models;

namespace MyMiniCar.Web.Services;

public class CartService
{
    private readonly List<CartItem> _items = new();
    private bool _isCartOpen;

    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();
    
    public bool IsCartOpen
    {
        get => _isCartOpen;
        set
        {
            if (_isCartOpen != value)
            {
                _isCartOpen = value;
                NotifyStateChanged();
            }
        }
    }

    public event Action? OnChange;

    public void AddItem(Product product, string material)
    {
        var existingItem = _items.FirstOrDefault(i => 
            i.Product.Id == product.Id && 
            i.SelectedMaterial.Equals(material, StringComparison.OrdinalIgnoreCase));

        if (existingItem != null)
        {
            existingItem.Quantity++;
        }
        else
        {
            _items.Add(new CartItem
            {
                Product = product,
                SelectedMaterial = material,
                Quantity = 1
            });
        }

        _isCartOpen = true; // Auto open cart when adding items
        NotifyStateChanged();
    }

    public void RemoveItem(string productId, string material)
    {
        var item = _items.FirstOrDefault(i => 
            i.Product.Id == productId && 
            i.SelectedMaterial.Equals(material, StringComparison.OrdinalIgnoreCase));

        if (item != null)
        {
            if (item.Quantity > 1)
            {
                item.Quantity--;
            }
            else
            {
                _items.Remove(item);
            }
            NotifyStateChanged();
        }
    }

    public void RemoveAllOfItem(string productId, string material)
    {
        var item = _items.FirstOrDefault(i => 
            i.Product.Id == productId && 
            i.SelectedMaterial.Equals(material, StringComparison.OrdinalIgnoreCase));

        if (item != null)
        {
            _items.Remove(item);
            NotifyStateChanged();
        }
    }

    public void ClearCart()
    {
        _items.Clear();
        NotifyStateChanged();
    }

    public decimal GetTotal()
    {
        return _items.Sum(i => i.Product.Price * i.Quantity);
    }

    public int GetCount()
    {
        return _items.Sum(i => i.Quantity);
    }

    public void ToggleCart()
    {
        IsCartOpen = !IsCartOpen;
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

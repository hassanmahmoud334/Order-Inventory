namespace Order_Service.Models
{
    public class Order
    {
        public Guid Id { get; set; }
        public string Sku { get; set; } = null!;
        public int Quantity { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

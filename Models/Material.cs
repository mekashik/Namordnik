namespace Namordnik.Models
{
    public class Material
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int CountInPack { get; set; }
        public string Unit { get; set; }
        public float? CountInStock { get; set; }
        public float MinCount { get; set; }
        public string Description { get; set; }
        public decimal Cost { get; set; }
        public string Image { get; set; }
        public int MaterialTypeId { get; set; }
    }
}
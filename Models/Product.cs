namespace Namordnik.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Article { get; set; }
        public string Name { get; set; }
        public int? TypeId { get; set; }
        public string ImagePath { get; set; }
        public int? PeopleCount { get; set; }
        public int? WorkshopNumber { get; set; }
        public decimal AgentPrice { get; set; }
        public string Description { get; set; }
    }
}
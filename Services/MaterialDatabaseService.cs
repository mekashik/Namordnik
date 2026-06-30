using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Namordnik.Models;
using Namordnik.Resources;

namespace Namordnik.Services
{
    //Сервис для работы с материалами в базе данных
    public class MaterialDatabaseService
    {
        private readonly string _connectionString;

        public MaterialDatabaseService(string connectionString = null)
        {
            _connectionString = connectionString ?? ConnectionStrings.Default;
        }

        public List<Material> GetAllMaterials()
        {
            var materials = new List<Material>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var query = @"
                        SELECT ID, Title, CountInPack, Unit, CountInStock, MinCount, 
                               Description, Cost, Image, MaterialTypeID
                        FROM Material 
                        ORDER BY Title";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            materials.Add(new Material
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                CountInPack = reader.GetInt32(2),
                                Unit = reader.GetString(3),
                                CountInStock = reader.IsDBNull(4) ? (float?)null : (float)reader.GetDouble(4),
                                MinCount = (float)reader.GetDouble(5),
                                Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                                Cost = reader.GetDecimal(7),
                                Image = reader.IsDBNull(8) ? null : reader.GetString(8),
                                MaterialTypeId = reader.GetInt32(9)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при загрузке материалов: {ex.Message}");
            }

            return materials;
        }

        //Получает материалы конкретного продукта
        public List<MaterialProductInfo> GetProductMaterials(int productId)
        {
            var materials = new List<MaterialProductInfo>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var query = @"
                        SELECT pm.MaterialID, pm.Count, m.Title, m.CountInPack, m.Unit, 
                               m.CountInStock, m.MinCount, m.Description, m.Cost, m.Image, m.MaterialTypeID
                        FROM ProductMaterial pm
                        JOIN Material m ON pm.MaterialID = m.ID
                        WHERE pm.ProductID = @productId
                        ORDER BY m.Title";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@productId", productId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var material = new Material
                                {
                                    Id = reader.GetInt32(0),
                                    Title = reader.GetString(2),
                                    CountInPack = reader.GetInt32(3),
                                    Unit = reader.GetString(4),
                                    CountInStock = reader.IsDBNull(5) ? (float?)null : (float)reader.GetDouble(5),
                                    MinCount = (float)reader.GetDouble(6),
                                    Description = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    Cost = reader.GetDecimal(8),
                                    Image = reader.IsDBNull(9) ? null : reader.GetString(9),
                                    MaterialTypeId = reader.GetInt32(10)
                                };

                                float quantity = (float)reader.GetDouble(1);
                                materials.Add(new MaterialProductInfo { Material = material, Quantity = quantity });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при загрузке материалов продукта: {ex.Message}");
            }

            return materials;
        }

        public void SaveProductMaterials(int productId, List<ProductMaterialData> materials)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    var deleteQuery = "DELETE FROM ProductMaterial WHERE ProductID = @productId";
                    using (var command = new SqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@productId", productId);
                        command.ExecuteNonQuery();
                    }

                    if (materials.Count > 0)
                    {
                        var insertQuery = @"
                            INSERT INTO ProductMaterial (ProductID, MaterialID, Count)
                            VALUES (@productId, @materialId, @count)";

                        foreach (var material in materials)
                        {
                            using (var command = new SqlCommand(insertQuery, connection))
                            {
                                command.Parameters.AddWithValue("@productId", productId);
                                command.Parameters.AddWithValue("@materialId", material.MaterialId);
                                command.Parameters.AddWithValue("@count", material.Quantity);
                                command.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при сохранении материалов: {ex.Message}");
            }
        }
    }

    //Информация о материале и его количестве в продукте>
    public class MaterialProductInfo
    {
        public Material Material { get; set; }
        public float Quantity { get; set; }
    }

    //Данные материала для сохранения
    public class ProductMaterialData
    {
        public int MaterialId { get; set; }
        public float Quantity { get; set; }
    }
}
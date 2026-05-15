using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace Namordnik.Services
{
    /// <summary>
    /// Сервис для работы с базой данных продуктов
    /// </summary>
    public class ProductDatabaseService
    {
        private readonly string _connectionString;

        public ProductDatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Получает ID продукта по артикулу (исключая текущий продукт при редактировании)
        /// </summary>
        public int? GetProductIdByArticle(string articleNumber, int? excludeProductId = null)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var query = "SELECT ID FROM Product WHERE ArticleNumber = @article";

                    if (excludeProductId.HasValue)
                        query += " AND ID != @excludeId";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@article", articleNumber ?? "");
                        if (excludeProductId.HasValue)
                            command.Parameters.AddWithValue("@excludeId", excludeProductId.Value);

                        var result = command.ExecuteScalar();
                        return result != null ? (int?)Convert.ToInt32(result) : null;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при проверке артикула: {ex.Message}");
            }
        }

        /// <summary>
        /// Создает новый продукт в базе
        /// </summary>
        public int CreateProduct(ProductDTO product)
        {
            ValidateProductData(product);

            // Проверка на дубликат артикула
            if (!string.IsNullOrWhiteSpace(product.Article))
            {
                if (GetProductIdByArticle(product.Article) != null)
                    throw new InvalidOperationException($"Продукт с артикулом '{product.Article}' уже существует!");
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    var query = @"
                    INSERT INTO Product 
                    (ArticleNumber, Title, ProductTypeID, MinCostForAgent, Description, 
                     ProductionPersonCount, ProductionWorkshopNumber, Image)
                    VALUES (@article, @title, @typeId, @price, @description, 
                            @peopleCount, @workshopNumber, @image);
                    SELECT SCOPE_IDENTITY();";

                    using (var command = new SqlCommand(query, connection))
                    {
                        AddProductParameters(command, product);
                        var result = command.ExecuteScalar();
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при создании продукта: {ex.Message}");
            }
        }

        /// <summary>
        /// Обновляет существующий продукт
        /// </summary>
        public void UpdateProduct(int productId, ProductDTO product)
        {
            ValidateProductData(product);

            // Проверка на дубликат артикула (исключая текущий продукт)
            if (!string.IsNullOrWhiteSpace(product.Article))
            {
                var duplicateId = GetProductIdByArticle(product.Article, productId);
                if (duplicateId.HasValue)
                    throw new InvalidOperationException($"Продукт с артикулом '{product.Article}' уже существует!");
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    var query = @"
                    UPDATE Product
                    SET ArticleNumber = @article,
                        Title = @title,
                        ProductTypeID = @typeId,
                        MinCostForAgent = @price,
                        Description = @description,
                        ProductionPersonCount = @peopleCount,
                        ProductionWorkshopNumber = @workshopNumber,
                        Image = @image
                    WHERE ID = @id";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@id", productId);
                        AddProductParameters(command, product);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при обновлении продукта: {ex.Message}");
            }
        }

        /// <summary>
        /// Удаляет продукт из базы (с проверкой наличия продаж)
        /// </summary>
        public void DeleteProduct(int productId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    // Проверка на наличие продаж
                    var checkQuery = "SELECT COUNT(*) FROM ProductSale WHERE ProductID = @id";
                    using (var command = new SqlCommand(checkQuery, connection))
                    {
                        command.Parameters.AddWithValue("@id", productId);
                        int saleCount = (int)command.ExecuteScalar();

                        if (saleCount > 0)
                            throw new InvalidOperationException("Нельзя удалить продукт — есть информация о его продажах");
                    }

                    // Удаление материалов
                    var deleteMatQuery = "DELETE FROM ProductMaterial WHERE ProductID = @id";
                    using (var command = new SqlCommand(deleteMatQuery, connection))
                    {
                        command.Parameters.AddWithValue("@id", productId);
                        command.ExecuteNonQuery();
                    }

                    // Удаление самого продукта
                    var deleteQuery = "DELETE FROM Product WHERE ID = @id";
                    using (var command = new SqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@id", productId);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при удалении продукта: {ex.Message}");
            }
        }

        /// <summary>
        /// Получает все материалы из базы
        /// </summary>
        public List<MaterialDTO> GetAllMaterials()
        {
            var materials = new List<MaterialDTO>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var query = "SELECT ID, Title FROM Material ORDER BY Title";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            materials.Add(new MaterialDTO
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.GetString(1)
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

        /// <summary>
        /// Получает материалы конкретного продукта
        /// </summary>
        public List<ProductMaterialDTO> GetProductMaterials(int productId)
        {
            var materials = new List<ProductMaterialDTO>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var query = @"
                        SELECT pm.MaterialID, m.Title, pm.Count
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
                                materials.Add(new ProductMaterialDTO
                                {
                                    MaterialId = reader.GetInt32(0),
                                    MaterialName = reader.GetString(1),
                                    Quantity = reader.GetDouble(2)
                                });
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

        /// <summary>
        /// Сохраняет материалы для продукта (удаляет старые и добавляет новые)
        /// </summary>
        public void SaveProductMaterials(int productId, List<ProductMaterialDTO> materials)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    // Удаляем старые материалы
                    var deleteQuery = "DELETE FROM ProductMaterial WHERE ProductID = @productId";
                    using (var command = new SqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@productId", productId);
                        command.ExecuteNonQuery();
                    }

                    // Добавляем новые материалы
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

        /// <summary>
        /// Получает все типы продуктов
        /// </summary>
        public List<ProductTypeDTO> GetAllProductTypes()
        {
            var types = new List<ProductTypeDTO>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var query = "SELECT ID, Title FROM ProductType ORDER BY Title";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            types.Add(new ProductTypeDTO
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при загрузке типов продуктов: {ex.Message}");
            }

            return types;
        }

        private void AddProductParameters(SqlCommand command, ProductDTO product)
        {
            command.Parameters.AddWithValue("@article", product.Article ?? "");
            command.Parameters.AddWithValue("@title", product.Name ?? "");

            // Исправление для C# 7.3 - используем if вместо условного выражения
            object typeIdValue;
            if (product.TypeId > 0)
                typeIdValue = product.TypeId;
            else
                typeIdValue = DBNull.Value;

            command.Parameters.AddWithValue("@typeId", typeIdValue);
            command.Parameters.AddWithValue("@price", product.Price);
            command.Parameters.AddWithValue("@description", product.Description ?? "");
            command.Parameters.AddWithValue("@peopleCount", product.PeopleCount > 0 ? product.PeopleCount : 0);
            command.Parameters.AddWithValue("@workshopNumber", product.WorkshopNumber > 0 ? product.WorkshopNumber : 0);
            command.Parameters.AddWithValue("@image", product.ImagePath ?? "");
        }

        private void ValidateProductData(ProductDTO product)
        {
            if (string.IsNullOrWhiteSpace(product.Name))
                throw new ArgumentException("Наименование продукта обязательно");

            if (product.Price < 0)
                throw new ArgumentException("Цена не может быть отрицательной");

            if (product.PeopleCount < 0)
                throw new ArgumentException("Количество людей не может быть отрицательным");

            if (product.WorkshopNumber < 0)
                throw new ArgumentException("Номер цеха не может быть отрицательным");
        }

    }

    /// <summary>
    /// DTO для передачи данных продукта
    /// </summary>
    public class ProductDTO
    {
        public string Article { get; set; }
        public string Name { get; set; }
        public int TypeId { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public int PeopleCount { get; set; }
        public int WorkshopNumber { get; set; }
        public string ImagePath { get; set; }
    }

    public class MaterialDTO
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }

    public class ProductMaterialDTO
    {
        public int MaterialId { get; set; }
        public string MaterialName { get; set; }
        public double Quantity { get; set; }
    }

    public class ProductTypeDTO
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }
}
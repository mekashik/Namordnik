using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Namordnik.Models;
using Namordnik.Resources;

namespace Namordnik.Services
{
    //Сервис для работы с продуктами в базе данных
    public class ProductDatabaseService
    {
        private readonly string _connectionString;

        public ProductDatabaseService(string connectionString = null)
        {
            _connectionString = connectionString ?? ConnectionStrings.Default;
        }

        //Получает ID продукта по артикулу (исключая текущий продукт при редактировании)
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

        //Получает все продукты из базы
        public List<Product> GetAllProducts()
        {
            var products = new List<Product>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    var query = @"
                        SELECT 
                            ID,
                            ArticleNumber,
                            Title,
                            ProductTypeID,
                            Image,
                            ProductionPersonCount,
                            ProductionWorkshopNumber,
                            MinCostForAgent,
                            Description
                        FROM Product
                        ORDER BY Title";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            products.Add(new Product
                            {
                                Id = reader.GetInt32(0),
                                Article = reader.IsDBNull(1) ? "Нет артикула" : reader.GetString(1),
                                Name = reader.GetString(2),
                                TypeId = reader.IsDBNull(3) ? null : (int?)reader.GetInt32(3),
                                ImagePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                                PeopleCount = reader.IsDBNull(5) ? null : (int?)reader.GetInt32(5),
                                WorkshopNumber = reader.IsDBNull(6) ? null : (int?)reader.GetInt32(6),
                                AgentPrice = reader.GetDecimal(7),
                                Description = reader.IsDBNull(8) ? "" : reader.GetString(8)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка загрузки продуктов: {ex.Message}");
            }

            return products;
        }

        /// Создает новый продукт в базе
        public int CreateProduct(Product product)
        {
            ValidateProductData(product);

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

        //Обновляет существующий продукт
        public void UpdateProduct(int productId, Product product)
        {
            ValidateProductData(product);

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

        /// Удаляет продукт из базы (с проверкой наличия продаж)
        public void DeleteProduct(int productId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    var checkQuery = "SELECT COUNT(*) FROM ProductSale WHERE ProductID = @id";
                    using (var command = new SqlCommand(checkQuery, connection))
                    {
                        command.Parameters.AddWithValue("@id", productId);
                        int saleCount = (int)command.ExecuteScalar();

                        if (saleCount > 0)
                            throw new InvalidOperationException("Нельзя удалить продукт — есть информация о его продажах");
                    }

                    var deleteHistoryQuery = "DELETE FROM ProductCostHistory WHERE ProductID = @id";
                    using (var command = new SqlCommand(deleteHistoryQuery, connection))
                    {
                        command.Parameters.AddWithValue("@id", productId);
                        command.ExecuteNonQuery();
                    }
                    
                    var deleteMatQuery = "DELETE FROM ProductMaterial WHERE ProductID = @id";
                    using (var command = new SqlCommand(deleteMatQuery, connection))
                    {
                        command.Parameters.AddWithValue("@id", productId);
                        command.ExecuteNonQuery();
                    }

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

        //Получает все типы продуктов
        public List<ProductType> GetAllProductTypes()
        {
            var types = new List<ProductType>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var query = "SELECT ID, Title, DefectedPercent FROM ProductType ORDER BY Title";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            types.Add(new ProductType
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                DefectedPercent = reader.IsDBNull(2) ? 0 : (float)reader.GetDouble(2)
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

        //Получает тип продукта по ID
        public ProductType GetProductTypeById(int typeId)
        {
            if (typeId <= 0) return null;

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var query = "SELECT ID, Title, DefectedPercent FROM ProductType WHERE ID = @id";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@id", typeId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new ProductType
                                {
                                    Id = reader.GetInt32(0),
                                    Title = reader.GetString(1),
                                    DefectedPercent = reader.IsDBNull(2) ? 0 : (float)reader.GetDouble(2)
                                };
                            }
                        }
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        //Проверяет был ли продукт продан в последний месяц
        public bool WasSoldLastMonth(int productId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    var query = @"
                SELECT COUNT(*)
                FROM ProductSale
                WHERE ProductID = @productId
                AND SaleDate >= DATEADD(MONTH, -1, GETDATE())";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@productId", productId);
                        int count = (int)command.ExecuteScalar();

                        return count > 0;
                    }
                }
            }
            catch
            {
                return true;
            }
        }

        //Обновляет цену продукта
        public void UpdateProductPrice(int productId, decimal newPrice)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    var query = @"
                    UPDATE Product
                    SET MinCostForAgent = @price
                    WHERE ID = @id";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@price", newPrice);
                        command.Parameters.AddWithValue("@id", productId);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка обновления цены: {ex.Message}");
            }
        }

        private void AddProductParameters(SqlCommand command, Product product)
        {
            command.Parameters.AddWithValue("@article", product.Article ?? "");
            command.Parameters.AddWithValue("@title", product.Name ?? "");

            object typeIdValue = product.TypeId.HasValue && product.TypeId > 0
                ? (object)product.TypeId.Value
                : DBNull.Value;

            command.Parameters.AddWithValue("@typeId", typeIdValue);
            command.Parameters.AddWithValue("@price", product.AgentPrice);
            command.Parameters.AddWithValue("@description", product.Description ?? "");
            command.Parameters.AddWithValue("@peopleCount", product.PeopleCount ?? 0);
            command.Parameters.AddWithValue("@workshopNumber", product.WorkshopNumber ?? 0);
            command.Parameters.AddWithValue("@image", product.ImagePath ?? "");
        }

        private void ValidateProductData(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.Name))
                throw new ArgumentException("Наименование продукта обязательно");

            if (product.AgentPrice < 0)
                throw new ArgumentException("Цена не может быть отрицательной");

            if (product.PeopleCount < 0)
                throw new ArgumentException("Количество людей не может быть отрицательным");

            if (product.WorkshopNumber < 0)
                throw new ArgumentException("Номер цеха не может быть отрицательным");
        }
    }
}
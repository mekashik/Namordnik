using System;
using System.Data.SqlClient;
using Namordnik.Resources;

namespace Namordnik.Services
{
    //Сервис для управления подключением к базе данных
    public class DatabaseConnectionService
    {
        private readonly string _connectionString;

        public DatabaseConnectionService(string connectionString = null)
        {
            _connectionString = connectionString ?? ConnectionStrings.Default;
        }

        //Проверяет подключение к БД
        public bool CheckConnection()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        //Получает строку подключения
        public string GetConnectionString()
        {
            return _connectionString;
        }
    }
}
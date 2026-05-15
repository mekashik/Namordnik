using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Namordnik
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string ConnectionString = @"Data Source=USER-PC;Initial Catalog=yp_04_Lobanova;Integrated Security=True";
        private int _currentPage = 1;
        private int _totalPages = 1;
        private const int PageSize = 20;
        private int _totalItems = 0;
        private List<ProductViewModel> _allProducts = new List<ProductViewModel>();
        private List<ProductViewModel> _originalProducts = new List<ProductViewModel>();
        private bool _isInitialized = false;
        private ProductEditWindow _editWindow;

        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<ProductViewModel> _products;
        public ObservableCollection<ProductViewModel> Products
        {
            get => _products;
            set
            {
                _products = value;
                OnPropertyChanged();
            }
        }

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            //проверяю открыто или нет окно редактирования
            if (_editWindow != null && _editWindow.IsVisible)
            {
                MessageBox.Show("Окно редактирования уже открыто. Закройте его и попробуйте снова.",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                _editWindow.Focus();
                return;
            }

            _editWindow = new ProductEditWindow();
            _editWindow.Owner = this;
            _editWindow.Closed += (s, args) => _editWindow = null;  //изменила e на args

            if (_editWindow.ShowDialog() == true)
            {
                LoadAllProducts();
            }
        }

        private void Product_Click(object sender, MouseButtonEventArgs e)
        {
            //тоже самое
            if (_editWindow != null && _editWindow.IsVisible)
            {
                MessageBox.Show("Окно редактирования уже открыто. Закройте его и попробуйте снова.",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                _editWindow.Focus();
                return;
            }

            if ((sender as Border)?.DataContext is ProductViewModel product)
            {
                _editWindow = new ProductEditWindow(product);
                _editWindow.Owner = this;
                _editWindow.Closed += (s, args) => _editWindow = null;  // изменила e на args

                if (_editWindow.ShowDialog() == true)
                {
                    LoadAllProducts();
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private bool WasSoldLastMonth(int productId)
        {
            try
            {
                using (var connection = new SqlConnection(ConnectionString))
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
                return true; //если ошибка лучше не подсвечивать
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //подключение к бд
                if (CheckDatabaseConnection())
                {
                    LoadAllProducts();
                    LoadProductTypes();
                    UpdatePaginationControls();
                    _isInitialized = true;
                }
                else
                {
                    MessageBox.Show("Не удалось подключиться к базе данных. Проверьте подключение.", "Ошибка подключения",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CheckDatabaseConnection()
        {
            try
            {
                using (var connection = new SqlConnection(ConnectionString))
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

        private void LoadAllProducts()
        {
            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();

                    var query = @"
                        SELECT 
                            p.ID,
                            p.ArticleNumber,
                            p.Title,
                            p.ProductTypeID,
                            p.Image,
                            p.ProductionPersonCount,
                            p.ProductionWorkshopNumber,
                            p.MinCostForAgent,
                            p.Description
                        FROM Product p
                        ORDER BY p.Title";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        _originalProducts.Clear();
                        while (reader.Read())
                        {
                            var product = new ProductViewModel
                            {
                                Id = reader.GetInt32(0),
                                Article = reader.IsDBNull(1) ? "Нет артикула" : reader.GetString(1),
                                Name = reader.GetString(2),
                                TypeId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                ImagePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                                PeopleCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                WorkshopNumber = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                                AgentPrice = reader.GetDecimal(7),
                                Description = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                IsSelected = false
                            };

                            //тип продукта
                            product.Type = LoadProductType(product.TypeId);

                            //материалы и стоимость
                            var materialInfo = LoadProductMaterials(product.Id);
                            product.Materials = materialInfo.Item1;
                            product.MaterialCost = materialInfo.Item2;
                            product.MaterialCount = materialInfo.Item3;

                            product.IsHighlighted = CheckIfProductShouldBeHighlighted(product);
                            product.HighlightReason = GetHighlightReason(product);
                            _originalProducts.Add(product);
                        }
                    }

                    _allProducts = new List<ProductViewModel>(_originalProducts);
                    _totalItems = _allProducts.Count;
                    _totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalItems / PageSize));

                    UpdateDisplayedProducts();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки продуктов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string LoadProductType(int typeId)
        {
            if (typeId <= 0) return "Не указан";

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    var query = "SELECT Title FROM ProductType WHERE ID = @id";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@id", typeId);
                        var result = command.ExecuteScalar();
                        return result?.ToString() ?? "Не указан";
                    }
                }
            }
            catch
            {
                return "Не указан";
            }
        }

        private Tuple<string, decimal, int> LoadProductMaterials(int productId)
        {
            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();

                    var query = @"
                        SELECT m.Title, pm.Count, m.Unit, m.Cost
                        FROM ProductMaterial pm
                        JOIN Material m ON pm.MaterialID = m.ID
                        WHERE pm.ProductID = @productId
                        ORDER BY m.Title";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@productId", productId);
                        using (var reader = command.ExecuteReader())
                        {
                            var materials = new List<string>();
                            decimal totalCost = 0;
                            int count = 0;

                            while (reader.Read())
                            {
                                count++;
                                var materialName = reader.GetString(0);
                                var materialCount = reader.GetDouble(1);
                                var unit = reader.GetString(2);
                                var cost = reader.GetDecimal(3);
                                var materialTotal = cost * (decimal)materialCount;
                                totalCost += materialTotal;

                                materials.Add($"{materialName} ({materialCount} {unit})");
                            }

                            string materialsText = materials.Count > 0
                                ? string.Join(", ", materials) + $"\nИтого: {totalCost:N2} ₽"
                                : "Нет материалов";

                            return new Tuple<string, decimal, int>(materialsText, totalCost, count);
                        }
                    }
                }
            }
            catch
            {
                return new Tuple<string, decimal, int>("Ошибка загрузки материалов", 0, 0);
            }
        }

        private bool CheckIfProductShouldBeHighlighted(ProductViewModel product)
        {
            return !WasSoldLastMonth(product.Id);
        }

        private string GetHighlightReason(ProductViewModel product)
        {
            return !WasSoldLastMonth(product.Id)
                ? "Не продавался последний месяц"
                : "";
        }

        private List<ProductViewModel> GetSelectedProducts()
        {
            return _allProducts.Where(p => p.IsSelected).ToList();
        }

        private void BtnChangePrice_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedProducts();

            if (selected.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы один продукт");
                return;
            }

            decimal average = selected.Average(p => p.AgentPrice);

            string input = Microsoft.VisualBasic.Interaction.InputBox(
                $"Введите значение увеличения (по умолчанию {average:N2}):",
                "Изменение стоимости",
                average.ToString());

            if (!decimal.TryParse(input, out decimal value))
            {
                MessageBox.Show("Некорректное число");
                return;
            }

            UpdatePrices(selected, value);
        }

        private void UpdatePrices(List<ProductViewModel> products, decimal delta)
        {
            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();

                    foreach (var product in products)
                    {
                        decimal newPrice = product.AgentPrice + delta;

                        var query = @"
                    UPDATE Product
                    SET MinCostForAgent = @price
                    WHERE ID = @id";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@price", newPrice);
                            command.Parameters.AddWithValue("@id", product.Id);
                            command.ExecuteNonQuery();
                        }

                        // обновляем в интерфейсе
                        product.AgentPrice = newPrice;
                    }
                }

                MessageBox.Show("Стоимость обновлена");
                ApplyFiltersAndSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления: {ex.Message}");
            }
        }

        private void LoadProductTypes()
        {
            try
            {
                if (cbFilter == null) return;

                cbFilter.Items.Clear();
                cbFilter.Items.Add(new ComboBoxItem { Content = "Все типы", Tag = "ALL" });

                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    var query = "SELECT ID, Title FROM ProductType ORDER BY Title";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var typeId = reader.GetInt32(0);
                            var typeName = reader.GetString(1);
                            cbFilter.Items.Add(new ComboBoxItem
                            {
                                Content = typeName,
                                Tag = typeId.ToString()
                            });
                        }
                    }
                }

                if (cbFilter.Items.Count == 0)
                {
                    //если нет типов в бд, используем типы из продуктов
                    var uniqueTypes = _originalProducts
                        .Select(p => p.Type)
                        .Where(t => !string.IsNullOrEmpty(t) && t != "Не указан")
                        .Distinct()
                        .OrderBy(t => t)
                        .ToList();

                    foreach (var type in uniqueTypes)
                    {
                        cbFilter.Items.Add(new ComboBoxItem { Content = type, Tag = type });
                    }
                }

                if (cbFilter.Items.Count > 0)
                    cbFilter.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки типов: {ex.Message}\nИспользуем типы из продуктов.", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);

                //типы из загруженных продуктов
                if (cbFilter != null)
                {
                    cbFilter.Items.Clear();
                    cbFilter.Items.Add(new ComboBoxItem { Content = "Все типы", Tag = "ALL" });

                    var uniqueTypes = _originalProducts
                        .Select(p => p.Type)
                        .Where(t => !string.IsNullOrEmpty(t) && t != "Не указан")
                        .Distinct()
                        .OrderBy(t => t)
                        .ToList();

                    foreach (var type in uniqueTypes)
                    {
                        cbFilter.Items.Add(new ComboBoxItem { Content = type, Tag = type });
                    }

                    if (cbFilter.Items.Count > 0)
                        cbFilter.SelectedIndex = 0;
                }
            }
        }

        private void UpdateDisplayedProducts()
        {
            if (_allProducts == null || _allProducts.Count == 0)
            {
                Products = new ObservableCollection<ProductViewModel>();
                return;
            }

            var startIndex = (_currentPage - 1) * PageSize;
            var pageProducts = _allProducts
                .Skip(startIndex)
                .Take(PageSize)
                .ToList();

            Products = new ObservableCollection<ProductViewModel>(pageProducts);
        }

        private void UpdatePaginationControls()
        {
            if (spPageNumbers == null) return;

            spPageNumbers.Children.Clear();

            if (_totalPages <= 1)
            {
                if (btnPrev != null) btnPrev.IsEnabled = false;
                if (btnNext != null) btnNext.IsEnabled = false;
                return;
            }

            //предыдущая
            if (btnPrev != null) btnPrev.IsEnabled = _currentPage > 1;

            //номеры страниц
            int startPage = Math.Max(1, _currentPage - 2);
            int endPage = Math.Min(_totalPages, _currentPage + 2);

            if (startPage > 1)
            {
                AddPageButton(1);
                if (startPage > 2)
                {
                    var dots = new TextBlock
                    {
                        Text = "...",
                        Margin = new Thickness(5, 0, 5, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    spPageNumbers.Children.Add(dots);
                }
            }

            for (int i = startPage; i <= endPage; i++)
            {
                AddPageButton(i);
            }

            if (endPage < _totalPages)
            {
                if (endPage < _totalPages - 1)
                {
                    var dots = new TextBlock
                    {
                        Text = "...",
                        Margin = new Thickness(5, 0, 5, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    spPageNumbers.Children.Add(dots);
                }
                AddPageButton(_totalPages);
            }

            //следующая
            if (btnNext != null) btnNext.IsEnabled = _currentPage < _totalPages;
        }

        private void AddPageButton(int pageNumber)
        {
            var button = new Button
            {
                Content = pageNumber.ToString(),
                Width = 30,
                Height = 30,
                Margin = new Thickness(2),
                Background = pageNumber == _currentPage ? Brushes.LightBlue : Brushes.Transparent,
                FontWeight = pageNumber == _currentPage ? FontWeights.Bold : FontWeights.Normal
            };

            button.Click += (s, e) =>
            {
                _currentPage = pageNumber;
                UpdateDisplayedProducts();
                UpdatePaginationControls();
            };

            spPageNumbers.Children.Add(button);
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdateDisplayedProducts();
                UpdatePaginationControls();
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                UpdateDisplayedProducts();
                UpdatePaginationControls();
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;
            ApplyFiltersAndSearch();
        }

        private void CbSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            ApplyFiltersAndSearch();
        }

        private void CbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            ApplyFiltersAndSearch();
        }

        private void ApplyFiltersAndSearch()
        {
            try
            {
                if (cbFilter == null || txtSearch == null || cbSort == null || !_isInitialized)
                    return;

                //фильтрация по типу
                var filteredProducts = _originalProducts.AsEnumerable();

                if (cbFilter.SelectedItem is ComboBoxItem filterItem && filterItem.Tag != null)
                {
                    var tag = filterItem.Tag.ToString();
                    if (tag != "ALL")
                    {
                        var selectedType = filterItem.Content.ToString();
                        filteredProducts = filteredProducts.Where(p => p.Type == selectedType);
                    }
                }

                //поиск
                var searchText = txtSearch.Text?.Trim()?.ToLower() ?? "";
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    filteredProducts = filteredProducts.Where(p =>
                        (p.Name?.ToLower()?.Contains(searchText) ?? false) ||
                        (p.Description?.ToLower()?.Contains(searchText) ?? false) ||
                        (p.Article?.ToLower()?.Contains(searchText) ?? false)
                    );
                }

                //сортировка
                if (cbSort.SelectedItem is ComboBoxItem sortItem && sortItem.Tag != null)
                {
                    var sortType = sortItem.Tag.ToString();

                    switch (sortType)
                    {
                        case "NameAsc":
                            filteredProducts = filteredProducts.OrderBy(p => p.Name);
                            break;
                        case "NameDesc":
                            filteredProducts = filteredProducts.OrderByDescending(p => p.Name);
                            break;
                        case "WorkshopAsc":
                            filteredProducts = filteredProducts.OrderBy(p => p.WorkshopNumber);
                            break;
                        case "WorkshopDesc":
                            filteredProducts = filteredProducts.OrderByDescending(p => p.WorkshopNumber);
                            break;
                        case "PriceAsc":
                            filteredProducts = filteredProducts.OrderBy(p => p.AgentPrice);
                            break;
                        case "PriceDesc":
                            filteredProducts = filteredProducts.OrderByDescending(p => p.AgentPrice);
                            break;
                        default:
                            filteredProducts = filteredProducts.OrderBy(p => p.Name);
                            break;
                    }
                }

                //обновление данных
                _allProducts = filteredProducts.ToList();
                _totalItems = _allProducts.Count;
                _totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalItems / PageSize));
                _currentPage = Math.Min(_currentPage, Math.Max(1, _totalPages));

                UpdateDisplayedProducts();
                UpdatePaginationControls();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка фильтрации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ProductViewModel : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Article { get; set; }
        public string Name { get; set; }
        public int TypeId { get; set; }
        public string Type { get; set; }
        public string ImagePath { get; set; }
        public int PeopleCount { get; set; }
        public int WorkshopNumber { get; set; }
        
        private decimal _agentPrice;
        public decimal AgentPrice
        {
            get => _agentPrice;
            set
            {
                _agentPrice = value;
                OnPropertyChanged();
            }
        }
        public string Description { get; set; }
        public decimal MaterialCost { get; set; }
        public int MaterialCount { get; set; }
        public string Materials { get; set; }
        public string HighlightReason { get; set; }

        private bool _isHighlighted;
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                OnPropertyChanged();
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public ImageSource ProductImage
        {
            get
            {
                try
                {
                    string basePath = @"C:\Users\Карина\Desktop\Намордник\Namordnik";

                    if (!string.IsNullOrWhiteSpace(ImagePath))
                    {
                        string fullPath = System.IO.Path.Combine(
                            basePath,
                            ImagePath.TrimStart('\\'));

                        if (System.IO.File.Exists(fullPath))
                            return new BitmapImage(new Uri(fullPath, UriKind.Absolute));
                    }

                    //заглушка
                    return new BitmapImage(
                        new Uri("pack://application:,,,/Resources/picture.png"));
                }
                catch
                {
                    return new BitmapImage(
                        new Uri("pack://application:,,,/Resources/picture.png"));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Namordnik.Models;
using Namordnik.Models.ViewModels;
using Namordnik.Resources;
using Namordnik.Services;

namespace Namordnik
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly ProductDatabaseService _productService;
        private readonly MaterialDatabaseService _materialService;

        private int _currentPage = 1;
        private int _totalPages = 1;
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

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            _productService = new ProductDatabaseService();
            _materialService = new MaterialDatabaseService();
        }

        // !!!ЗАДАНИЕ 4
        // Открытие формы добавления продукции
        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            // Запрет открытия нескольких окон редактирования
            if (_editWindow != null && _editWindow.IsVisible)
            {
                MessageBox.Show("Окно редактирования уже открыто. Закройте его и попробуйте снова.",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                _editWindow.Focus();
                return;
            }

            _editWindow = new ProductEditWindow();
            _editWindow.Owner = this;
            _editWindow.Closed += (s, args) => _editWindow = null;

            if (_editWindow.ShowDialog() == true)
            {
                LoadAllProducts();
            }
        }

        // !!!ЗАДАНИЕ 4
        // Открытие формы редактирования продукции
        private void Product_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Запрет открытия нескольких окон редактирования
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
                _editWindow.Closed += (s, args) => _editWindow = null;

                if (_editWindow.ShowDialog() == true)
                {
                    LoadAllProducts();
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var connectionService = new DatabaseConnectionService();
                if (connectionService.CheckConnection())
                {
                    LoadAllProducts();
                    LoadProductTypes();
                    UpdatePaginationControls();
                    _isInitialized = true;
                }
                else
                {
                    MessageBox.Show("Не удалось подключиться к базе данных. Проверьте подключение.",
                        "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // !!!ЗАДАНИЕ 2
        // Загрузка всей продукции из базы данных
        // Расчет стоимости материалов
        // Подготовка данных для отображения
        private void LoadAllProducts()
        {
            try
            {
                var products = _productService.GetAllProducts();
                _originalProducts.Clear();

                foreach (var product in products)
                {
                    var viewModel = new ProductViewModel
                    {
                        Id = product.Id,
                        Article = product.Article,
                        Name = product.Name,
                        TypeId = product.TypeId ?? 0,
                        ImagePath = product.ImagePath,
                        PeopleCount = product.PeopleCount ?? 0,
                        WorkshopNumber = product.WorkshopNumber ?? 0,
                        AgentPrice = product.AgentPrice,
                        Description = product.Description,
                        IsSelected = false
                    };

                    viewModel.PropertyChanged += Product_PropertyChanged;
                    var productType = _productService.GetProductTypeById(viewModel.TypeId);
                    viewModel.Type = productType?.Title ?? "Не указан";

                    var materialsInfo = _materialService.GetProductMaterials(product.Id);
                    var materialsList = new List<string>();
                    decimal totalCost = 0;

                    foreach (var materialInfo in materialsInfo)
                    {
                        var material = materialInfo.Material;
                        var quantity = materialInfo.Quantity;
                        var materialTotal = material.Cost * (decimal)quantity;
                        totalCost += materialTotal;
                        materialsList.Add($"{material.Title} ({quantity} {material.Unit})");
                    }

                    viewModel.Materials = materialsList.Count > 0
                        ? string.Join(", ", materialsList) + $"\nИтого: {totalCost:N2} ₽"
                        : "Нет материалов";
                    viewModel.MaterialCost = totalCost;
                    viewModel.MaterialCount = materialsInfo.Count;

                    viewModel.IsHighlighted = !_productService.WasSoldLastMonth(product.Id);
                    viewModel.HighlightReason = viewModel.IsHighlighted
                        ? "Не продавался последний месяц"
                        : "";

                    _originalProducts.Add(viewModel);
                }

                _allProducts = new List<ProductViewModel>(_originalProducts);
                _totalItems = _allProducts.Count;
                _totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalItems / Constants.PageSize));

                UpdateDisplayedProducts();
                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки продуктов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обновление количества выбранных продуктов
        private void UpdateSelectedCount()
        {
            if (tbSelectedCount != null)
            {
                int selected = _allProducts.Count(p => p.IsSelected);
                tbSelectedCount.Text = $"Выбрано: {selected}";
            }
        }

        // Отслеживание выбора продукции для массового изменения стоимости
        private void Product_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProductViewModel.IsSelected))
            {
                UpdateSelectedCount();
            }
        }

        // !!!ЗАДАНИЕ 3
        // Загрузка типов продукции для фильтрации
        private void LoadProductTypes()
        {
            try
            {
                if (cbFilter == null) return;

                cbFilter.Items.Clear();
                cbFilter.Items.Add(new ComboBoxItem { Content = "Все типы", Tag = "ALL" });

                var types = _productService.GetAllProductTypes();
                foreach (var type in types)
                {
                    cbFilter.Items.Add(new ComboBoxItem
                    {
                        Content = type.Title,
                        Tag = type.Id.ToString()
                    });
                }

                if (cbFilter.Items.Count > 0)
                    cbFilter.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки типов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // !!!ЗАДАНИЕ 2
        // Постраничный вывод продукции (по 20 элементов)
        private void UpdateDisplayedProducts()
        {
            if (_allProducts == null || _allProducts.Count == 0)
            {
                Products = new ObservableCollection<ProductViewModel>();
                return;
            }

            var startIndex = (_currentPage - 1) * Constants.PageSize;
            var pageProducts = _allProducts
                .Skip(startIndex)
                .Take(Constants.PageSize)
                .ToList();

            Products = new ObservableCollection<ProductViewModel>(pageProducts);
        }

        // !!1ЗАДАНИЕ 2
        // Формирование постраничной навигации. Кнопки страниц, переход вперед/назад
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

            if (btnPrev != null) btnPrev.IsEnabled = _currentPage > 1;

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
                        Margin = new System.Windows.Thickness(5, 0, 5, 0),
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
                        Margin = new System.Windows.Thickness(5, 0, 5, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    spPageNumbers.Children.Add(dots);
                }
                AddPageButton(_totalPages);
            }

            if (btnNext != null) btnNext.IsEnabled = _currentPage < _totalPages;
            tbPageInfo.Text = $"Страница {_currentPage} из {_totalPages}";
        }

        private void AddPageButton(int pageNumber)
        {
            var button = new Button
            {
                Content = pageNumber.ToString(),
                Width = 30,
                Height = 30,
                Margin = new System.Windows.Thickness(2),
                Background = pageNumber == _currentPage ? System.Windows.Media.Brushes.LightBlue : System.Windows.Media.Brushes.Transparent,
                FontWeight = pageNumber == _currentPage ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal
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

        // !!!ЗАДАНИЕ 3
        // Массовое изменение минимальной стоимости продукции через модальное окно
        private void BtnChangePrice_Click(object sender, RoutedEventArgs e)
        {
            var selected = _allProducts
                .Where(p => p.IsSelected)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(
                    "Выберите хотя бы один продукт",
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            decimal average = selected.Average(p => p.AgentPrice);

            var window = new ChangePriceWindow(average);

            window.Owner = this;

            if (window.ShowDialog() != true)
                return;

            try
            {
                foreach (var product in selected)
                {
                    decimal newPrice = window.ChangeValue;

                    _productService.UpdateProductPrice(
                        product.Id,
                        newPrice);

                    product.AgentPrice = newPrice;
                }

                MessageBox.Show(
                    "Стоимость успешно обновлена",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ApplyFiltersAndSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка обновления: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // !!!ЗАДАНИЕ 3
        // Поиск, фильтрация и сортировка продукции. Работа в режиме реального времени
        private void ApplyFiltersAndSearch()
        {
            try
            {
                if (cbFilter == null || txtSearch == null || cbSort == null || !_isInitialized)
                    return;

                var filteredProducts = _originalProducts.AsEnumerable();

                if (cbFilter.SelectedItem is ComboBoxItem filterItem && filterItem.Tag != null)
                {
                    var tag = filterItem.Tag.ToString();
                    if (tag != "ALL")
                    {
                        var selectedType = filterItem.Content.ToString();
                        // Фильтрация продукции по типу
                        filteredProducts = filteredProducts.Where(p => p.Type == selectedType);
                    }
                }

                // Поиск по названию и описанию продукции
                var searchText = txtSearch.Text?.Trim()?.ToLower() ?? "";
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    filteredProducts = filteredProducts.Where(p =>
                        (p.Name?.ToLower()?.Contains(searchText) ?? false) ||
                        (p.Description?.ToLower()?.Contains(searchText) ?? false)
                    );
                }

                if (cbSort.SelectedItem is ComboBoxItem sortItem && sortItem.Tag != null)
                {
                    var sortType = sortItem.Tag.ToString();

                    // Сортировка продукции по наименованию, по номеру цеха, по мин стоимости
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

                _allProducts = filteredProducts.ToList();
                _totalItems = _allProducts.Count;
                _totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalItems / Constants.PageSize));
                _currentPage = Math.Min(_currentPage, Math.Max(1, _totalPages));

                UpdateDisplayedProducts();
                UpdatePaginationControls();
                UpdateSelectedCount();
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
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Namordnik.Services;

namespace Namordnik
{
    public partial class ProductEditWindow : Window
    {
        private const string ConnectionString = @"Data Source=USER-PC;Initial Catalog=yp_04_Lobanova;Integrated Security=True";
        private readonly ProductDatabaseService _dbService;

        private int? _productId;
        private string _imagePath = "";
        private List<ProductMaterialDTO> _currentMaterials;
        private List<MaterialDTO> _availableMaterials;

        public ProductEditWindow(ProductViewModel product = null)
        {
            InitializeComponent();

            _dbService = new ProductDatabaseService(ConnectionString);
            _currentMaterials = new List<ProductMaterialDTO>();
            _availableMaterials = new List<MaterialDTO>();

            InitializeWindow(product);
        }

        private void InitializeWindow(ProductViewModel product)
        {
            try
            {
                LoadProductTypes();
                LoadAvailableMaterials();

                if (product != null)
                {
                    _productId = product.Id;
                    LoadProductData(product);
                    btnDelete.IsEnabled = true;
                }
                else
                {
                    btnDelete.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void LoadProductTypes()
        {
            try
            {
                cbType.Items.Clear();
                cbType.Items.Add(new { ID = 0, Title = "Не указан" });

                var types = _dbService.GetAllProductTypes();
                foreach (var type in types)
                {
                    cbType.Items.Add(new { ID = type.Id, Title = type.Title });
                }

                cbType.DisplayMemberPath = "Title";
                cbType.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки типов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAvailableMaterials()
        {
            try
            {
                cbMaterials.Items.Clear();
                _availableMaterials = _dbService.GetAllMaterials();

                foreach (var material in _availableMaterials)
                {
                    cbMaterials.Items.Add(material.Title);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки материалов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProductData(ProductViewModel product)
        {
            try
            {
                txtArticle.Text = product.Article;
                txtName.Text = product.Name;
                txtPrice.Text = product.AgentPrice.ToString("F2");
                txtDescription.Text = product.Description;
                txtWorkshop.Text = product.WorkshopNumber.ToString();
                txtPeople.Text = product.PeopleCount.ToString();

                _imagePath = product.ImagePath;
                if (!string.IsNullOrEmpty(_imagePath) && File.Exists(_imagePath))
                {
                    imgProduct.Source = new BitmapImage(new Uri(_imagePath));
                }

                //выбираем тип в ComboBox
                SelectProductType(product.TypeId);

                //загружаем материалы
                LoadProductMaterials(product.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных продукта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectProductType(int typeId)
        {
            foreach (var item in cbType.Items)
            {
                dynamic obj = item;
                if (obj.ID == typeId)
                {
                    cbType.SelectedItem = item;
                    return;
                }
            }
        }

        private void LoadProductMaterials(int productId)
        {
            try
            {
                lbMaterials.Items.Clear();
                _currentMaterials = _dbService.GetProductMaterials(productId);

                foreach (var material in _currentMaterials)
                {
                    lbMaterials.Items.Add($"{material.MaterialName} - {material.Quantity}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки материалов продукта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Изображения|*.png;*.jpg;*.jpeg|Все файлы|*.*";

            if (dlg.ShowDialog() == true)
            {
                _imagePath = dlg.FileName;
                imgProduct.Source = new BitmapImage(new Uri(_imagePath));
            }
        }

        private void AddMaterial_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cbMaterials.SelectedIndex < 0)
                {
                    MessageBox.Show("Выберите материал", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!double.TryParse(txtMatCount.Text, out double quantity) || quantity <= 0)
                {
                    MessageBox.Show("Введите корректное количество (больше 0)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string selectedMaterial = cbMaterials.SelectedItem.ToString();
                var material = _availableMaterials.Find(m => m.Title == selectedMaterial);

                if (material == null)
                {
                    MessageBox.Show("Материал не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                //проверяем нет ли материала
                if (_currentMaterials.Exists(m => m.MaterialId == material.Id))
                {
                    MessageBox.Show("Этот материал уже добавлен", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _currentMaterials.Add(new ProductMaterialDTO
                {
                    MaterialId = material.Id,
                    MaterialName = material.Title,
                    Quantity = quantity
                });

                lbMaterials.Items.Add($"{selectedMaterial} - {quantity}");
                cbMaterials.SelectedIndex = -1;
                txtMatCount.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления материала: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveMaterial_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int selectedIndex = lbMaterials.SelectedIndex;
                if (selectedIndex < 0)
                {
                    MessageBox.Show("Выберите материал для удаления", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _currentMaterials.RemoveAt(selectedIndex);
                lbMaterials.Items.RemoveAt(selectedIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления материала: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var productData = ValidateAndGetProductData();

                if (_productId == null)
                {
                    _productId = _dbService.CreateProduct(productData);
                }
                else
                {
                    _dbService.UpdateProduct(_productId.Value, productData);
                }

                _dbService.SaveProductMaterials(_productId.Value, _currentMaterials);

                MessageBox.Show("Продукт успешно сохранен", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ProductDTO ValidateAndGetProductData()
        {
            //название
            if (string.IsNullOrWhiteSpace(txtName.Text))
                throw new InvalidOperationException("Наименование продукта обязательно");

            //цена
            if (!decimal.TryParse(txtPrice.Text, out decimal price))
                throw new InvalidOperationException("Цена должна быть числом");

            if (price < 0)
                throw new InvalidOperationException("Цена не может быть отрицательной");

            //кол-во людей
            if (!int.TryParse(txtPeople.Text, out int peopleCount) || peopleCount < 0)
                throw new InvalidOperationException("Количество людей должно быть положительным числом");

            //номер цеха
            if (!int.TryParse(txtWorkshop.Text, out int workshopNumber) || workshopNumber < 0)
                throw new InvalidOperationException("Номер цеха должен быть положительным числом");

            int typeId = 0;
            dynamic selectedType = cbType.SelectedItem;
            if (selectedType != null)
                typeId = selectedType.ID;

            return new ProductDTO
            {
                Article = txtArticle.Text.Trim(),
                Name = txtName.Text.Trim(),
                TypeId = typeId,
                Price = price,
                Description = txtDescription.Text,
                PeopleCount = peopleCount,
                WorkshopNumber = workshopNumber,
                ImagePath = _imagePath
            };
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_productId == null)
                return;

            var result = MessageBox.Show(
                "Вы уверены, что хотите удалить этот продукт?\nВсе связанные материалы будут удалены.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                _dbService.DeleteProduct(_productId.Value);
                MessageBox.Show("Продукт успешно удален", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
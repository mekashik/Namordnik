using Microsoft.Win32;
using Namordnik.Models;
using Namordnik.Models.ViewModels;
using Namordnik.Resources;
using Namordnik.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Namordnik
{
    public partial class ProductEditWindow : Window
    {
        private readonly ProductDatabaseService _productService;
        private readonly MaterialDatabaseService _materialService;

        private int? _productId;
        private string _imagePath = "";
        private List<ProductMaterialData> _currentMaterials;
        private List<Material> _availableMaterials;

        public ProductEditWindow(ProductViewModel product = null)
        {
            InitializeComponent();

            _productService = new ProductDatabaseService();
            _materialService = new MaterialDatabaseService();
            _currentMaterials = new List<ProductMaterialData>();
            _availableMaterials = new List<Material>();

            InitializeWindow(product);
        }

        // !!!ЗАДАНИЕ 4
        // Инициализация формы добавления/редактирования продукции
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

        // Загрузка типов продукции в выпадающий список
        private void LoadProductTypes()
        {
            try
            {
                cbType.Items.Clear();
                cbType.Items.Add(new ComboBoxItem { Content = "Не указан" });

                var types = _productService.GetAllProductTypes();
                foreach (var type in types)
                {
                    cbType.Items.Add(new ComboBoxItem { Content = type.Title, Tag = type.Id });
                }

                cbType.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки типов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Загрузка материалов для выбора при производстве продукции
        private void LoadAvailableMaterials()
        {
            try
            {
                cbMaterials.Items.Clear();
                _availableMaterials = _materialService.GetAllMaterials();

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

        // Загрузка данных выбранной продукции в форму редактирования
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

                SelectProductType(product.TypeId);
                LoadProductMaterials(product.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных продукта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectProductType(int typeId)
        {
            foreach (ComboBoxItem item in cbType.Items)
            {
                if (item.Tag is int id && id == typeId)
                {
                    cbType.SelectedItem = item;
                    return;
                }
            }
        }

        // Загрузка списка материалов продукции
        private void LoadProductMaterials(int productId)
        {
            try
            {
                lbMaterials.Items.Clear();
                var materialsInfo = _materialService.GetProductMaterials(productId);
                _currentMaterials.Clear();

                foreach (var materialInfo in materialsInfo)
                {
                    _currentMaterials.Add(new ProductMaterialData
                    {
                        MaterialId = materialInfo.Material.Id,
                        Quantity = materialInfo.Quantity
                    });
                    lbMaterials.Items.Add($"{materialInfo.Material.Title} - {materialInfo.Quantity}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки материалов продукта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // !!!ЗАДАНИЕ 4
        // Добавление или замена изображения продукции
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

        // Добавление материала к продукции
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
                var material = _availableMaterials.FirstOrDefault(m => m.Title == selectedMaterial);

                if (material == null)
                {
                    MessageBox.Show("Материал не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_currentMaterials.Any(m => m.MaterialId == material.Id))
                {
                    MessageBox.Show("Этот материал уже добавлен", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _currentMaterials.Add(new ProductMaterialData
                {
                    MaterialId = material.Id,
                    Quantity = (float)quantity
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
                var product = ValidateAndGetProductData();

                if (_productId == null)
                {
                    _productId = _productService.CreateProduct(product);
                }
                else
                {
                    _productService.UpdateProduct(_productId.Value, product);
                }

                _materialService.SaveProductMaterials(_productId.Value, _currentMaterials);

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

        // !!!ЗАДАНИЕ 4
        // Проверка корректности введенных данных: обязательные поля, отрицательные значения, корректность числовых данных
        private Product ValidateAndGetProductData()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
                throw new InvalidOperationException("Наименование продукта обязательно");

            if (!decimal.TryParse(txtPrice.Text, out decimal price))
                throw new InvalidOperationException("Цена должна быть числом");

            if (price < 0)
                throw new InvalidOperationException("Цена не может быть отрицательной");

            if (!int.TryParse(txtPeople.Text, out int peopleCount) || peopleCount < 0)
                throw new InvalidOperationException("Количество людей должно быть положительным числом");

            if (!int.TryParse(txtWorkshop.Text, out int workshopNumber) || workshopNumber < 0)
                throw new InvalidOperationException("Номер цеха должен быть положительным числом");

            if (string.IsNullOrWhiteSpace(txtArticle.Text))
                throw new InvalidOperationException("Артикул обязателен");

            int typeId = 0;
            if (cbType.SelectedItem is ComboBoxItem item && item.Tag is int id)
            {
                typeId = id;
            }

            return new Product
            {
                Article = txtArticle.Text.Trim(),
                Name = txtName.Text.Trim(),
                TypeId = typeId > 0 ? (int?)typeId : null,
                AgentPrice = price,
                Description = txtDescription.Text,
                PeopleCount = peopleCount > 0 ? (int?)peopleCount : null,
                WorkshopNumber = workshopNumber > 0 ? (int?)workshopNumber : null,
                ImagePath = _imagePath
            };
        }

        // !!!ЗАДАНИЕ 4
        // Удаление продукции и связанных материалов и запрет удаления при наличии продаж
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
                _productService.DeleteProduct(_productId.Value);
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
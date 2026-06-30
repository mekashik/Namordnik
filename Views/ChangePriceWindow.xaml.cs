using System;
using System.Windows;

namespace Namordnik
{
    public partial class ChangePriceWindow : Window
    {
        public decimal ChangeValue { get; private set; }

        public ChangePriceWindow(decimal averageValue)
        {
            InitializeComponent();

            txtValue.Text = averageValue.ToString("F2");

            tbAverage.Text =
                $"Среднее значение цены: {averageValue:F2} ₽";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtValue.Text, out decimal value))
            {
                MessageBox.Show(
                    "Введите корректное число",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (value < 0)
            {
                MessageBox.Show(
                    "Значение не может быть отрицательным",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            ChangeValue = value;

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace Namordnik.Models.ViewModels
{
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
                string projectPath = Directory.GetCurrentDirectory();

                // Если есть путь к изображению из БД
                if (!string.IsNullOrWhiteSpace(ImagePath))
                {
                    string fullPath = Path.Combine(projectPath, ImagePath.TrimStart('\\', '/'));

                    if (File.Exists(fullPath))
                    {
                        return new BitmapImage(new Uri(fullPath));
                    }
                }

                // Заглушка
                string defaultImage = Path.Combine(projectPath, "products", "picture.png");
                return new BitmapImage(new Uri(defaultImage));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
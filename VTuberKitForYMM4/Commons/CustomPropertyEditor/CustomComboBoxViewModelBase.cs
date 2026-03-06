using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VTuberKitForYMM4.Commons.CustomPropertyEditor
{
    public class CustomComboBoxViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public ObservableCollection<CustomComboBoxValueBase> ItemsSource { get; set; }

        private CustomComboBoxValueBase _selectedValue;
        public CustomComboBoxValueBase SelectedValue
        {
            get => _selectedValue;
            set
            {
                _selectedValue = value;
                OnPropertyChanged(nameof(SelectedValue));
            }
        }

        private string _selectedDisplayMember = string.Empty;
        public string SelectedDisplayMember
        {
            get => _selectedDisplayMember;
            set
            {
                _selectedDisplayMember = value;
                OnPropertyChanged(nameof(SelectedDisplayMember));
            }
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        [JsonIgnore]
        public string SearchDisplayMember { get; set; }

        public CustomComboBoxViewModelBase(string searchDisplayMember)
        {
            ItemsSource = [];
            _selectedValue = new();
            _selectedDisplayMember = string.Empty;
            SearchDisplayMember = searchDisplayMember;
            _isEnabled = false;
        }
        public virtual void UpdateSelectedValue()
        {
            if (0 < ItemsSource.Count)
            {
                var items = ItemsSource.OrderByDescending(x => x.DisplayMember).ToList();

                if (SelectedValue == null)
                {
                    SelectedValue = items.FirstOrDefault(x => x.DisplayMember.Equals(SelectedDisplayMember))
                        ?? items.First();
                }
                else if (!string.IsNullOrEmpty(SelectedValue.DisplayMember))
                {
                    SelectedValue = items.FirstOrDefault(x => x.DisplayMember.Equals(SelectedValue.DisplayMember))
                        ?? items.First();
                }
                else
                {
                    SelectedValue = items.FirstOrDefault(x => x.DisplayMember.StartsWith(SearchDisplayMember))
                        ?? items.First();
                }

                SelectedDisplayMember = SelectedValue.DisplayMember;
            }
        }

        public virtual async Task PreUpdateItemsSource(bool update)
        {
            await Task.Run(() => { });
        }
        public virtual void UpdateItemsSource()
        {
        }
    }
}

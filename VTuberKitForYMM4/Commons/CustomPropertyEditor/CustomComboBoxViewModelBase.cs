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
        [JsonIgnore]
        public virtual CustomComboBoxValueBase SelectedValue
        {
            get => _selectedValue;
            set
            {
                _selectedValue = value;
                OnPropertyChanged(nameof(SelectedValue));
            }
        }

        private string _selectedDisplayMember = string.Empty;
        [JsonIgnore]
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
                SelectedValue = ItemsSource.FirstOrDefault(x => x.DisplayMember.StartsWith(SearchDisplayMember))
                    ?? ItemsSource.First();
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

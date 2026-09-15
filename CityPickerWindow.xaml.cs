using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AuroraClock.Services;

namespace AuroraClock
{
    public partial class CityPickerWindow : Window
    {
        public sealed class Row
        {
            public CityEntry Entry { get; init; } = new("", "", "");
            public string Name => Entry.Name;
            public string Offset { get; init; } = "";
        }

        private List<Row> _rows = new();

        public CityEntry? Selected { get; private set; }

        public CityPickerWindow()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                AcrylicHelper.Enable(this, System.Windows.Media.Color.FromRgb(9, 13, 20), 0.55);
                Refresh("");
                SearchBox.Focus();
            };
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) { DialogResult = false; Close(); }
                else if (e.Key == Key.Enter && List.SelectedItem is Row r) { Selected = r.Entry; DialogResult = true; Close(); }
            };
        }

        private void Refresh(string query)
        {
            _rows = TimeZoneCatalog.Search(query)
                .Select(c => new Row { Entry = c, Offset = c.Id.Length == 0 ? TimeZoneCatalog.OffsetText(null) : TimeZoneCatalog.OffsetText(c.Id) })
                .ToList();

            List.ItemsSource = _rows;
            CountText.Text = $"{_rows.Count} 个结果";
            if (_rows.Count > 0) { List.SelectedIndex = 0; }
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Placeholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            Refresh(SearchBox.Text);
        }

        private void List_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { }

        private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (List.SelectedItem is Row r) { Selected = r.Entry; DialogResult = true; Close(); }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (List.SelectedItem is Row r) { Selected = r.Entry; DialogResult = true; Close(); }
            else if (_rows.Count > 0) { Selected = _rows[0].Entry; DialogResult = true; Close(); }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }
    }
}

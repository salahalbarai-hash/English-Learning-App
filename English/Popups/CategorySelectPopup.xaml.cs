using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Maui.Views;

namespace English.Popups;

public class CategoryViewModel
{
    public string Name { get; set; } = string.Empty;
}

public partial class CategorySelectPopup : Popup
{
    public ObservableCollection<CategoryViewModel> Categories { get; set; } = new();

    public string? SelectedCategory { get; private set; }
    public string? SelectedEnglishWord { get; private set; }
    public string? SelectedArabicWord { get; private set; }

    // 🟢 قائمة الفئات المسموحة والمعتمدة داخل التطبيق
    private readonly List<string> _allowedCategories = new()
    {
        "صفات", "طعام", "افعال", "ادوات", "حيوانات", "مهن", "الاشكال"
    };

    private readonly List<string> _userUnlockedCategories;
    private readonly int _memorizedCount;

    public class CategorySelectionResult
    {
        public string Category { get; set; } = "";
        public string English { get; set; } = "";
        public string Arabic { get; set; } = "";
    }

    public CategorySelectPopup(List<string> userUnlockedCategories, int memorizedCount)
    {
        InitializeComponent();

        _userUnlockedCategories = userUnlockedCategories ?? new List<string>();
        _memorizedCount = memorizedCount;

        var cv = this.FindByName<CollectionView>("CategoriesCollectionView");
        if (cv != null)
            cv.ItemsSource = Categories;

        _ = LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("words.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            using var doc = JsonDocument.Parse(json);

            // 1. أخذ الكلمات المحفوظة فقط بناءً على عدد الحفظ
            var memorizedWords = doc.RootElement.EnumerateArray().Take(_memorizedCount);

            // 2. استخراج الفئات الموجودة ضمن الكلمات المحفوظة
            var availableInFile = memorizedWords
                .Where(el => el.TryGetProperty("Category", out var c) && !string.IsNullOrWhiteSpace(c.GetString()))
                .Select(el => el.GetProperty("Category").GetString()!)
                .Distinct();

            // 3. التصفية: إظهار الفئات الموجودة ضمن الكلمات المحفوظة والمفتوحة للمستخدم والموجودة في _allowedCategories فقط
            var filteredCategories = availableInFile
                .Where(cat => _allowedCategories.Contains(cat, StringComparer.OrdinalIgnoreCase) &&
                             _userUnlockedCategories.Contains(cat, StringComparer.OrdinalIgnoreCase))
                .Distinct()
                .ToList();

            Categories.Clear();
            foreach (var catName in filteredCategories)
            {
                Categories.Add(new CategoryViewModel { Name = catName });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading categories: {ex.Message}");
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ConfirmButton.IsEnabled = e.CurrentSelection.FirstOrDefault() != null;
        ConfirmButton.Opacity = ConfirmButton.IsEnabled ? 1.0 : 0.5;
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(null);
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        var cv = this.FindByName<CollectionView>("CategoriesCollectionView");
        if (cv != null && cv.SelectedItem is CategoryViewModel vm)
        {
            SelectedCategory = vm.Name;

            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("words.json");
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(json);

                var items = doc.RootElement.EnumerateArray()
                             .Take(_memorizedCount)
                             .Where(el => el.TryGetProperty("Category", out var c) && c.GetString() == SelectedCategory)
                             .ToArray();

                if (items.Length > 0)
                {
                    var rnd = new Random();
                    var chosen = items[rnd.Next(items.Length)];

                    if (chosen.TryGetProperty("EnglishWord", out var engProp))
                        SelectedEnglishWord = engProp.GetString();

                    if (chosen.TryGetProperty("ArabicWord", out var arProp))
                        SelectedArabicWord = arProp.GetString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error picking word: {ex.Message}");
            }

            var res = new CategorySelectionResult
            {
                Category = SelectedCategory ?? string.Empty,
                English = SelectedEnglishWord ?? string.Empty,
                Arabic = SelectedArabicWord ?? string.Empty
            };

            Close(res);
        }
        else
        {
            Close(null);
        }
    }
}
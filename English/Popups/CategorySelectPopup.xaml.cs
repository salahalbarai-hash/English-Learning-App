namespace English.Popups;

public class CategoryViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
}

public partial class CategorySelectPopup : Popup
{
    public ObservableCollection<CategoryViewModel> Categories { get; set; } = new();

    public string? SelectedCategory { get; private set; }
    public string? SelectedEnglishWord { get; private set; }
    public string? SelectedArabicWord { get; private set; }

    public class CategorySelectionResult
    {
        public string Category { get; set; } = "";
        public string English { get; set; } = "";
        public string Arabic { get; set; } = "";
    }

    public CategorySelectPopup()
    {
        InitializeComponent();

        var cv = this.FindByName<CollectionView>("CategoriesCollectionView");
        if (cv != null)
            cv.ItemsSource = Categories;

        // بدء تحميل الفئات دون إيقاف الواجهة
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

            var groups = new Dictionary<string, List<string>>();

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("Category", out var c) && el.TryGetProperty("EnglishWord", out var w))
                {
                    var cat = c.GetString() ?? "";
                    var word = w.GetString() ?? "";

                    if (!groups.ContainsKey(cat))
                        groups[cat] = new List<string>();

                    if (!string.IsNullOrWhiteSpace(word) && groups[cat].Count < 5) // preview up to 5
                        groups[cat].Add(word);
                }
            }

            // إضافة العناصر إلى ObservableCollection (تقوم بتحديث الواجهة تلقائياً)
            Categories.Clear();
            foreach (var kv in groups)
            {
                Categories.Add(new CategoryViewModel { Name = kv.Key, Preview = string.Join(", ", kv.Value) });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading categories: {ex.Message}");
        }
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(null);
    }

    // 🟢 تم تحويل الدالة هنا إلى async void لإصلاح مشكلة التجميد (Deadlock)
    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        var cv = this.FindByName<CollectionView>("CategoriesCollectionView");
        if (cv != null && cv.SelectedItem is CategoryViewModel vm)
        {
            SelectedCategory = vm.Name;

            try
            {
                // 🟢 استخدام await بدلاً من .Result و ReadToEndAsync بدلاً من ReadToEnd
                using var stream = await FileSystem.OpenAppPackageFileAsync("words.json");
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                using var doc = JsonDocument.Parse(json);
                var items = doc.RootElement.EnumerateArray()
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
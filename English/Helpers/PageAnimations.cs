using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace English.Helpers
{
    public static class PageAnimations
    {
        public static async void AnimatePageInAsync(this ContentPage page)
        {
            if (page.Content is Layout rootLayout)
            {
                // إخفاء جميع العناصر فوراً قبل أن يراها المستخدم
                var children = rootLayout.Children.OfType<VisualElement>().ToList();
                foreach (var child in children)
                {
                    child.Opacity = 0;
                    child.TranslationY = 40;
                }

                // انتظار قصير جداً لنسمح للشاشة بالاستقرار
                await Task.Delay(100);

                // حركة متتالية (Staggered Animation): كل عنصر يظهر بعد الآخر بجزء من الثانية
                int staggerDelay = 0;
                foreach (var child in children)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(staggerDelay);
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            child.FadeTo(1, 500, Easing.SpringOut);
                            child.TranslateTo(0, 0, 500, Easing.SpringOut);
                        });
                    });
                    staggerDelay += 80; // الفارق الزمني بين كل عنصر والآخر
                }
            }
            else if (page.Content != null) // في حال لم يكن الـ Content عبارة عن Layout
            {
                page.Content.Opacity = 0;
                page.Content.TranslationY = 40;
                await Task.Delay(100);
                await Task.WhenAll(
                    page.Content.FadeTo(1, 500, Easing.SpringOut),
                    page.Content.TranslateTo(0, 0, 500, Easing.SpringOut)
                );
            }
        }
    }
}

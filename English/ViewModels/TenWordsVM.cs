using CommunityToolkit.Mvvm.ComponentModel;
using English.Models;
using English.Services;
using System;
using System.Collections.ObjectModel;

namespace English.ViewModels
{
    public partial class TenWordsVM : ObservableObject
    {
        public ObservableCollection<WordModel> Titles { get; set; } = [];

        [ObservableProperty]
        private int memorizedWordsCount;

        [ObservableProperty]
        private string progressStatus;

        [ObservableProperty]
        private double progressWidth;

        private static readonly string[][] Phases =
        [
            ["شرارة البداية انطلقت ⚡", "أنت تدخل عالم اللغة الآن 🚪", "خطوتك الأولى صنعت الفرق 🔥", "عقلك بدأ يستوعب الجديد 🧠", "أنت تكسر الحاجز الأول 💥", "بداية تبشر بالقوة القادمة 🌱", "أنت تتحرك للأمام بثقة 👣", "كل كلمة تبني مستقبلك 🧱"],
            ["تقدمك أصبح واضحاً 🚀", "أنت تتشكل لغوياً الآن 🧭", "الثقة لديك ترتفع 💪", "تفكيرك بدأ يتغير فعلياً 🧠", "أنت تدخل مستوى أعلى 🎯", "الفهم لديك يتحسن بسرعة ⚡", "تبدأ برؤية الصورة الكاملة 👁️", "أنت في الطريق الصحيح ✔️"],
            ["أنت لاعب حقيقي الآن ⚔️", "تسيطر على الأساسيات 🔥", "قدرتك تضاعفت بشكل ملحوظ 📈", "اللغة لم تعد غريبة 👌", "أنت تبني قوة حقيقية 💎", "تفوقك بدأ يظهر 👑", "أنت تتقدم بثبات 🚀", "التحكم لديك يتحسن 🎮"],
            ["أنت ضمن النخبة الآن 👑", "تفكيرك أصبح أسرع ⚡", "أنت قريب من الطلاقة 💬", "مستواك يفرض نفسه 🔥", "أنت تتحكم باللغة 🧠", "الاحتراف يقترب منك 🎯", "أنت تبني مستقبلاً عالمياً 🌍", "أنت تتجاوز الحدود 🚀"],
            ["أنت في مستوى نادر 🚀", "القمة تقترب 👀", "أنت تتحكم بثقة 💪", "أنت على وشك الإنجاز 🏆", "لم يتبق إلا القليل 👌", "أنت تصنع أسطورة 👑", "أنت تتجاوز الجميع ⚔️", "الاحتراف أصبح واقعاً 🔥"]
        ];

        partial void OnMemorizedWordsCountChanged(int value)
        {
            LoadDailyWords();
            UpdateUI(value);
        }

        private void UpdateUI(int count)
        {
            double maxWidth = 220;

            double ratio = Math.Clamp(count / 1000.0, 0, 1);
            ProgressWidth = ratio * maxWidth;
            if (count >= 1000)
            {
                ProgressStatus = "أسطوري! فككت شفرة اللغة بنجاح! 👑";
                return;
            }

            int phaseIndex = Math.Min(count / 200, Phases.Length - 1);
            var phase = Phases[phaseIndex];

            int level = count / 10;
            int index = level % phase.Length;

            ProgressStatus = phase[index];
        }

        public void LoadDailyWords()
        {
            Titles.Clear();
            var words = TenWords.All();

            if (words != null)
            {
                foreach (var word in words)
                    Titles.Add(word);
            }
        }
    }
}
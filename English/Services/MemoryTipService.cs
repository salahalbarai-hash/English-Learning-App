using English.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace English.Services
{
    public class MemoryTip
    {
        public string Word { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
        public string Syllables { get; set; } = string.Empty;
        public string TipText { get; set; } = string.Empty;
        public string PatternBadge { get; set; } = "حيلة حفظ 💡";
        public string Category { get; set; } = string.Empty;
    }

    public static class MemoryTipService
    {
        // قاموس روابط ذهنية ذكية جاهزة للكلمات الأكثر تكراراً وأهمية
        private static readonly Dictionary<string, (string Tip, string Badge)> CuratedTips = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Hospital"] = ("قسّم الكلمة إلى مقطعين مألوفين: (Host = مضيف) + (p+ital = عناية حيوية). تذكر: المشفى يستضيف المرضى لرعايتهم.", "تقسيم مقاطع 🧩"),
            ["Breakfast"] = ("تتكون الكلمة من كلمتين: (Break = كسر) و (Fast = صيام)؛ أي الوجبة التي تكسر بها صيام الليل صباحاً!", "تركيب الكلمة 🍳"),
            ["Grandmother"] = ("كلمة مركبة: Grand (عظيم / كبير) + Mother (أم) = الأم الكبيرة (الجدة).", "تركيب الكلمة 👵"),
            ["Exercise"] = ("تنتهي بـ cise مثل قطْع الشك والتكاسل! ركّز على حرف الـ x الذي يربط بين البداية والنهاية بحيوية.", "نمط الحروف 🏃"),
            ["Coffee"] = ("تتميز بثنائيتين نادرتين متجاورتين: حرفان ff في الوسط وحرفان ee في النهاية (C-o-ff-ee).", "ثنائية الحروف ☕"),
            ["Kitchen"] = ("احذر نطق حرف t! المقطع tch ينطق (تش) مدمجاً كما في watch و match.", "صوتيات 🍳"),
            ["School"] = ("مقطع ch هنا ينطق بصوت الـ (ك) الصريح وليس (تش). تذكر: Sch = سكول.", "صوتيات 🏫"),
            ["Clean"] = ("احفظ قاعدة الصوت الطويل: حرفا ea ينطقان كياء ممدودة ناعمة (كلِينْ).", "مد صوتي ✨"),
            ["Doctor"] = ("تنتهي بـ tor وليس ter! تذكر أن معظم المهن الطبية والقانونية تنتهي بـ or مثل Actor و Professor.", "نهاية الكلمة 🩺"),
            ["Window"] = ("تبدأ وتنتهي بحرف W! وتتكون من مقطعين خفيفين: Win (فوز) + dow.", "تناظر الحروف 🪟"),
            ["Mountain"] = ("المقطع ain في النهاية ينطق سريعاً كـ (تِنْ) خفيفة (ماون-تِنْ) وليس تاين طويل.", "نطق سليم ⛰️"),
            ["Walk"] = ("حرف L صامت تماماً لا ينطق (وُوكْ)! مثل talk و chalk.", "حرف صامت 🚶"),
            ["Watch"] = ("تجمع بين معنيين في نفس الحروف: يشاهد كفعل، وساعة يد كاسم!", "دلالة مشتركة ⌚"),
            ["Drink"] = ("تتطابق صوتياً مع think و sink؛ فقط غيّر البداية إلى Dr لتنطلق للشرب.", "عائلة صوتية 🥤"),
            ["Help"] = ("كلمة طوارئ عالمية من 4 حروف! تذكر SOS دائماً تقترن بـ HELP.", "رابط ذهني 🆘"),
            ["Pray"] = ("تشبه كلمة Play مع استبدال L بحرف R (رابط الخشوع والسكينة).", "تشابه صوتي 🤲"),
            ["Visit"] = ("من الأصل اللاتيني Vis الذي يعني (يرى)؛ فأنت تزور شخصاً لـ(تراه) وتسعد بلقائه.", "أصل المعنى 🚪"),
            ["Movie"] = ("مشتقة تاريخياً من Moving picture (صورة متحركة)، فاختصرت إلى Movie!", "قصة الكلمة 🎬"),
            ["Room"] = ("صوت الـ oo الطويل مثل Boom و Zoom يعطيك إحساساً بالاتساع والمساحة.", "صوت الكلمة 🛋️")
        };

        /// <summary>
        /// يولد أو يستخرج نصيحة حفظ ذكية وموثوقة مبنية على الكلمة وتركيبتها
        /// </summary>
        public static MemoryTip GetTip(WordModel word)
        {
            if (word == null || string.IsNullOrWhiteSpace(word.EnglishWord))
            {
                return new MemoryTip
                {
                    Word = "",
                    Translation = "",
                    Syllables = "",
                    TipText = "انقر على أي كلمة لتظهر لك حيلة ذكية لتذكرها فوراً 💡",
                    PatternBadge = "نصيحة الحفظ 💡"
                };
            }

            string eng = word.EnglishWord.Trim();
            string ar = word.ArabicWord.Trim();
            string category = word.Category?.Trim() ?? "";

            // 1. فحص هل الكلمة مسجلة في القاموس المنسق الدقيق
            if (CuratedTips.TryGetValue(eng, out var curated))
            {
                return new MemoryTip
                {
                    Word = eng,
                    Translation = ar,
                    Syllables = DivideIntoSyllables(eng),
                    TipText = curated.Tip,
                    PatternBadge = curated.Badge,
                    Category = category
                };
            }

            // 2. التوليد الديناميكي المنطقي الموثوق بناءً على القواعد الصوتية والإملائية الحقيقية
            var (dynamicTip, badge) = GenerateDynamicTip(eng, ar);

            return new MemoryTip
            {
                Word = eng,
                Translation = ar,
                Syllables = DivideIntoSyllables(eng),
                TipText = dynamicTip,
                PatternBadge = badge,
                Category = category
            };
        }

        private static (string Tip, string Badge) GenerateDynamicTip(string eng, string ar)
        {
            string lower = eng.ToLowerInvariant();

            // فحص الأحرف المكررة (Double Letters)
            for (int i = 0; i < lower.Length - 1; i++)
            {
                if (char.IsLetter(lower[i]) && lower[i] == lower[i + 1])
                {
                    char d = lower[i];
                    return ($"انتبه لتكرار حرف '{d}{d}' المتتالي داخل الكلمة؛ تثبيت هذه الثنائية يضمن كتابتها بدقة تامة.", "ثنائية إملائية ✍️");
                }
            }

            // فحص المقاطع الصوتية المركبة الشائعة
            if (lower.EndsWith("tion"))
                return ("تنتهي بالمقطع الشهير (tion) الذي يُنطق كصوت (شَن)؛ تذكر استخدامه دائماً في تحويل الأفعال لأسماء.", "مقطع شن 🔔");

            if (lower.EndsWith("ing"))
                return ("تنتهي بلاحقة الاستمرارية (ing)؛ احذف اللاحقة لتصل إلى جذر الفعل الأصلي بسهولة.", "لاحقة فعلية ⚡");

            if (lower.EndsWith("ly"))
                return ("تنتهي باللاحقة (ly)؛ وهي العلامة المميزة للظروف التي تصف طريقة حدوث الفعل.", "لاحقة وصفية 🧭");

            if (lower.StartsWith("un") || lower.StartsWith("in") || lower.StartsWith("dis"))
                return ("تبدأ ببادئة نفي مشهورة؛ انتبه أن البادئة تعكس المعنى الإيجابي إلى نقيضه مباشرة.", "بادئة المعنى 🔄");

            if (lower.Contains("ph"))
                return ("تحتوي على التركيبة (ph) التي تنطق دائماً كحرف F الصريح؛ مثل Phone و Photo.", "قاعدة صوتية 🎙️");

            if (lower.Contains("ea") || lower.Contains("ee"))
                return ("تحتوي على صوت مد طويل (ee/ea)؛ يمنحك نطقاً سلساً ممتداً يسهل تمييزه سماعياً.", "مد صوتي 🎵");

            if (lower.StartsWith("kn") || lower.StartsWith("wr"))
                return ("الحرف الأول فيها صامت تماماً ولا ينطق؛ انتبه لكتابته وتجاهله أثناء النطق.", "حرف صامت 🤫");

            // تقسيم الكلمات الطويلة
            if (eng.Length >= 7)
            {
                string syl = DivideIntoSyllables(eng);
                return ($"كلمة طويلة؟ لا تحفظها دفعة واحدة بل كإيقاع صوتي مجزأ: [{syl}].", "تقسيم صوتي 🧩");
            }

            // نصيحة استرجاع ذهني قياسية
            return ($"اقرأ الكلمة [{eng}] ثم أغمض عينيك وتخيل حروفها المكتوبة بالترتيب، ثم اربطها بـ ({ar}).", "تصور ذهني 🧠");
        }

        /// <summary>
        /// تقسيم الكلمة إلى مقاطع صوتية متناسقة ومريحة للقراءة
        /// </summary>
        public static string DivideIntoSyllables(string word)
        {
            if (string.IsNullOrWhiteSpace(word) || word.Length <= 3)
                return word;

            // خوارزمية ذكية بسيطة لفصل حروف العلة والحروف الساكنة
            var vowels = "aeiouyAEIOUY";
            var sb = new StringBuilder();

            for (int i = 0; i < word.Length; i++)
            {
                sb.Append(word[i]);

                if (i < word.Length - 2)
                {
                    bool isCurrentVowel = vowels.Contains(word[i]);
                    bool isNextConsonant = !vowels.Contains(word[i + 1]) && char.IsLetter(word[i + 1]);
                    bool isAfterNextVowel = vowels.Contains(word[i + 2]);

                    // نمط V-CV (فصل بعد العلة إذا تلاها ساكن ثم علة)
                    if (isCurrentVowel && isNextConsonant && isAfterNextVowel && i > 0)
                    {
                        sb.Append('-');
                    }
                    // نمط VC-CV (فصل بين حرفين ساكنين محصورين بين علتين)
                    else if (i < word.Length - 3 && !vowels.Contains(word[i]) && !vowels.Contains(word[i + 1]) && vowels.Contains(word[i + 2]))
                    {
                        sb.Append('-');
                    }
                }
            }

            string result = sb.ToString().Trim('-');
            // حماية من التقسيم المفرط
            return result.Replace("--", "-");
        }
    }
}


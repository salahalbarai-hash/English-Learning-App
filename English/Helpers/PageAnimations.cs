namespace English.Helpers
{
    /// <summary>
    /// يحدد استراتيجية تحريك محتوى الصفحة عند ظهورها.
    /// </summary>
    public enum PageAnimationMode
    {
        /// <summary>
        /// اختيار ذكي: يحرك العناصر الفرعية بالتتابع إذا كانت الحاوية مناسبة وعدد العناصر معقولًا،
        /// ويتحول تلقائيًا إلى تحريك الحاوية ككتلة واحدة في التخطيطات المعقدة أو الكبيرة.
        /// </summary>
        Auto,

        /// <summary>
        /// تحريك الحاوية الجذرية للصفحة ككتلة واحدة (الأكثر استقرارًا والأخف أداءً).
        /// </summary>
        ContainerOnly,

        /// <summary>
        /// تحريك العناصر الفرعية المباشرة بالتتابع مع سقف زمني محدد.
        /// </summary>
        Staggered,

        /// <summary>
        /// تعطيل الحركة لهذه الصفحة.
        /// </summary>
        None
    }

    /// <summary>
    /// إعدادات قابلة للتخصيص لضبط سلوك وسرعة حركات ظهور الصفحات.
    /// </summary>
    public sealed class PageAnimationOptions
    {
        /// <summary>
        /// الإعدادات الافتراضية المتوازنة (هادئة وسريعة وآمنة).
        /// </summary>
        public static PageAnimationOptions Default { get; } = new();

        /// <summary>
        /// إعدادات لتحريك الحاوية الرئيسية فقط دون تجزئة العناصر الداخلية.
        /// </summary>
        public static PageAnimationOptions ContainerOnly { get; } = new()
        {
            Mode = PageAnimationMode.ContainerOnly,
            DurationMs = 240,
            InitialTranslationY = 14
        };

        /// <summary>
        /// نمط الحركة المطلوب. القيمة الافتراضية هي Auto.
        /// </summary>
        public PageAnimationMode Mode { get; init; } = PageAnimationMode.Auto;

        /// <summary>
        /// مدة حركة العنصر الواحد بالمللي ثانية (بين 180 و 300 مللي ثانية مثالية لتجربة سلسة).
        /// </summary>
        public uint DurationMs { get; init; } = 260;

        /// <summary>
        /// الفارق الزمني المبدئي بين كل عنصر والذي يليه في الحركة المتتابعة.
        /// </summary>
        public uint StaggerStepMs { get; init; } = 35;

        /// <summary>
        /// الحد الأقصى لإجمالي تأخير التتابع (يمنع تأخر ظهور العناصر السفلية في الصفحات الطويلة).
        /// </summary>
        public uint MaxTotalStaggerDelayMs { get; init; } = 180;

        /// <summary>
        /// الحد الأقصى لعدد العناصر التي يتم تحريكها منفردة قبل دمج البقية أو التحول لتحريك الحاوية.
        /// </summary>
        public int MaxStaggeredChildren { get; init; } = 8;

        /// <summary>
        /// مقدار الإزاحة الرأسية الابتدائية (بوحدات مستقلة عن الكثافة). قيمة صغيرة تمنع الاهتزاز.
        /// </summary>
        public double InitialTranslationY { get; init; } = 16.0;

        /// <summary>
        /// الشفافية الابتدائية قبل بدء التلاشي التدريجي.
        /// </summary>
        public double InitialOpacity { get; init; } = 0.0;

        /// <summary>
        /// منحنى التسارع والتباطؤ. CubicOut يمنح دخولًا سلسًا ومستقرًا دون ارتداد مزعج.
        /// </summary>
        public Easing EasingCurve { get; init; } = Easing.CubicOut;

        /// <summary>
        /// عند تفعيله (افتراضيًا true)، تعمل الحركة عند أول ظهور للصفحة فقط،
        /// ولا تتكرر بشكل مزعج عند إغلاق نافذة منبثقة أو العودة من صفحة فرعية.
        /// </summary>
        public bool AnimateOnlyOnFirstAppearance { get; init; } = true;

        /// <summary>
        /// عند تفعيله، إذا كان محتوى الصفحة ScrollView، يتم فحص الحاوية الداخلية لتحريك عناصرها
        /// مع الحفاظ الكامل على وظيفة التمرير.
        /// </summary>
        public bool UnwrapScrollViewContent { get; init; } = true;
    }

    /// <summary>
    /// نظام احترافي وآمن لإدارة حركات ظهور الصفحات في .NET MAUI.
    /// يضمن بقاء جميع العناصر مرئية وفي مواضعها الصحيحة حتى عند الإلغاء أو التنقل السريع.
    /// </summary>
    public static class PageAnimations
    {
        /// <summary>
        /// مفتاح عام لتمكين أو تعطيل حركات الصفحات على مستوى التطبيق بالكامل
        /// (مفيد لدعم خيارات تسهيل الاستخدام أو الأجهزة ضعيفة الموارد).
        /// </summary>
        public static bool IsAnimationsEnabled { get; set; } = true;

        // تخزين حالة كل صفحة دون الاحتفاظ بمرجع قوي يمنع جامع القمامة (GC) من تحرير الصفحة
        private static readonly ConditionalWeakTable<ContentPage, PageAnimationState> PageStates = new();

        private sealed class PageAnimationState
        {
            public CancellationTokenSource? ActiveCts { get; set; }
            public Task? ActiveTask { get; set; }
            public bool HasAnimatedOnce { get; set; }
            public List<ElementSnapshot>? ActiveSnapshots { get; set; }
        }

        private readonly struct ElementSnapshot
        {
            public VisualElement Element { get; }
            public double OriginalOpacity { get; }
            public double OriginalTranslationX { get; }
            public double OriginalTranslationY { get; }

            public ElementSnapshot(VisualElement element)
            {
                Element = element;
                OriginalOpacity = element.Opacity <= 0.01 ? 1.0 : element.Opacity;
                OriginalTranslationX = element.TranslationX;
                OriginalTranslationY = element.TranslationY;
            }

            public void Restore()
            {
                try
                {
                    Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(Element);
                    Element.Opacity = OriginalOpacity;
                    Element.TranslationX = OriginalTranslationX;
                    Element.TranslationY = OriginalTranslationY;
                }
                catch
                {
                    // تجاهل أي استثناء في حال تم التخلص من العنصر في المنصة الأصلية أثناء إغلاق الصفحة
                }
            }
        }

        /// <summary>
        /// ينفذ حركة ظهور الصفحة بشكل غير متزامن وآمن، مع ضمان استعادة حالة جميع العناصر عند الانتهاء أو الإلغاء.
        /// يمكن استدعاؤها مع await أو بدونه.
        /// </summary>
        public static Task AnimatePageInAsync(
            this ContentPage? page,
            PageAnimationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (page == null || !IsAnimationsEnabled)
                return Task.CompletedTask;

            options ??= PageAnimationOptions.Default;

            if (options.Mode == PageAnimationMode.None)
                return Task.CompletedTask;

            // ضمان التنفيذ دائمًا على خيط واجهة المستخدم (UI Thread)
            if (page.Dispatcher.IsDispatchRequired)
            {
                return page.Dispatcher.DispatchAsync(() => ExecuteAnimationCoreAsync(page, options, cancellationToken));
            }

            return ExecuteAnimationCoreAsync(page, options, cancellationToken);
        }

        /// <summary>
        /// يلغي أي حركة جارية على الصفحة فورًا ويعيد جميع العناصر المتأثرة إلى حالتها المرئية الطبيعية.
        /// </summary>
        public static void CancelAndRestorePageAnimations(this ContentPage? page)
        {
            if (page == null)
                return;

            if (PageStates.TryGetValue(page, out var state))
            {
                try
                {
                    state.ActiveCts?.Cancel();
                }
                catch
                {
                    // تجاهل إذا تم التخلص من CancellationTokenSource مسبقًا
                }

                if (state.ActiveSnapshots is { Count: > 0 } snapshots)
                {
                    foreach (var snapshot in snapshots)
                    {
                        snapshot.Restore();
                    }
                }
            }
        }

        /// <summary>
        /// يعيد ضبط سجل ظهور الصفحة بحيث تعمل الحركة مرة أخرى عند الظهور القادم
        /// حتى لو كان الخيار AnimateOnlyOnFirstAppearance مفعلًا.
        /// </summary>
        public static void ResetPageAnimationState(this ContentPage? page)
        {
            if (page == null)
                return;

            if (PageStates.TryGetValue(page, out var state))
            {
                state.HasAnimatedOnce = false;
            }
        }

        private static async Task ExecuteAnimationCoreAsync(
            ContentPage page,
            PageAnimationOptions options,
            CancellationToken externalToken)
        {
            if (page.Content == null || !page.Content.IsVisible)
                return;

            var state = PageStates.GetOrCreateValue(page);

            // إذا كانت هناك حركة قيد التشغيل حاليًا لنفس الصفحة، نعيد نفس المهمة لمنع التضارب أو الوميض
            if (state.ActiveTask is { IsCompleted: false } runningTask)
            {
                await runningTask;
                return;
            }

            // تجنب تكرار الحركة عند العودة للصفحة إذا كان الخيار مفعلًا
            if (options.AnimateOnlyOnFirstAppearance && state.HasAnimatedOnce)
                return;

            // تحديد العناصر المستهدفة بالحركة وحفظ لقطة لحالتها الأصلية قبل أي تعديل
            var targets = ResolveAnimationTargets(page.Content, options);
            if (targets.Count == 0)
                return;

            var snapshots = new List<ElementSnapshot>(targets.Count);
            foreach (var target in targets)
            {
                // إلغاء أي حركات سابقة عالقة على العنصر قبل قراءة قيمه الأصلية
                Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(target);
                snapshots.Add(new ElementSnapshot(target));
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            var token = linkedCts.Token;

            state.ActiveCts = linkedCts;
            state.ActiveSnapshots = snapshots;

            // إلغاء الحركة فورًا إذا غادر المستخدم الصفحة أثناء تشغيلها
            void OnPageDisappearing(object? sender, EventArgs e) => linkedCts.Cancel();
            void OnPageUnloaded(object? sender, EventArgs e) => linkedCts.Cancel();

            page.Disappearing += OnPageDisappearing;
            page.Unloaded += OnPageUnloaded;

            var animationTask = RunSnapshotsAnimationAsync(page, snapshots, options, token);
            state.ActiveTask = animationTask;

            try
            {
                await animationTask;
                if (!token.IsCancellationRequested)
                {
                    state.HasAnimatedOnce = true;
                }
            }
            catch (OperationCanceledException)
            {
                // الإلغاء متوقع عند مغادرة الصفحة السريعة، وستتكفل كتلة finally بالاستعادة الكاملة
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PageAnimations] Warning: Animation interrupted: {ex.Message}");
            }
            finally
            {
                page.Disappearing -= OnPageDisappearing;
                page.Unloaded -= OnPageUnloaded;

                // الضمانة الأساسية: إعادة جميع العناصر التي لمستها الحركة إلى حالتها الأصلية الدقيقة
                foreach (var snapshot in snapshots)
                {
                    snapshot.Restore();
                }

                state.ActiveSnapshots = null;
                state.ActiveCts = null;
                state.ActiveTask = null;
            }
        }

        private static async Task RunSnapshotsAnimationAsync(
            ContentPage page,
            IReadOnlyList<ElementSnapshot> snapshots,
            PageAnimationOptions options,
            CancellationToken token)
        {
            // 1. ضبط الحالة الابتدائية على خيط الواجهة مباشرة قبل بدء الحركة
            double startOpacity = Math.Clamp(options.InitialOpacity, 0.0, 1.0);
            foreach (var snapshot in snapshots)
            {
                // نحافظ على النسبة إذا كان العنصر ذا شفافية مخصصة أقل من 1
                snapshot.Element.Opacity = Math.Min(startOpacity, snapshot.OriginalOpacity);
                snapshot.Element.TranslationY = snapshot.OriginalTranslationY + options.InitialTranslationY;
            }

            // 2. انتظار دورة تخطيط واحدة (Frame Yield) بدل التأخير العشوائي الطويل (100ms)
            // هذا يمنع الوميض البصري ويسمح لـ MAUI بتثبيت أبعاد العناصر قبل تحريكها
            if (!page.IsLoaded)
            {
                await WaitForPageLoadedOrTimeoutAsync(page, maxWaitMs: 60, token);
            }
            else
            {
                await Task.Yield();
            }

            token.ThrowIfCancellationRequested();

            // 3. في حال كان الهدف عنصرًا واحدًا (مثل تحريك الحاوية الرئيسية)
            if (snapshots.Count == 1)
            {
                await AnimateSingleElementAsync(snapshots[0], delayMs: 0, options, token);
                return;
            }

            // 4. حساب الفارق الزمني التكيفي للحركة المتتابعة بحيث لا يتجاوز السقف الزمني المسموح
            int count = snapshots.Count;
            int calculatedStep = (int)options.StaggerStepMs;
            if ((count - 1) * calculatedStep > options.MaxTotalStaggerDelayMs)
            {
                calculatedStep = Math.Max(10, (int)options.MaxTotalStaggerDelayMs / (count - 1));
            }

            var tasks = new List<Task>(count);
            for (int i = 0; i < count; i++)
            {
                int itemDelay = i * calculatedStep;
                tasks.Add(AnimateSingleElementAsync(snapshots[i], itemDelay, options, token));
            }

            await Task.WhenAll(tasks);
        }

        private static async Task AnimateSingleElementAsync(
            ElementSnapshot snapshot,
            int delayMs,
            PageAnimationOptions options,
            CancellationToken token)
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, token);
            }

            token.ThrowIfCancellationRequested();

            var element = snapshot.Element;
            if (!element.IsVisible)
                return;

            // تشغيل التلاشي والإزاحة معًا على نفس العنصر دون استخدام Task.Run
            var fadeTask = element.FadeTo(snapshot.OriginalOpacity, options.DurationMs, options.EasingCurve);
            var translateTask = element.TranslateTo(
                snapshot.OriginalTranslationX,
                snapshot.OriginalTranslationY,
                options.DurationMs,
                options.EasingCurve);

            await Task.WhenAll(fadeTask, translateTask);
        }

        /// <summary>
        /// يحلل بنية محتوى الصفحة ويختار العناصر الأنسب للتحريك بأمان دون إفساد التخطيط أو التمرير.
        /// </summary>
        private static List<VisualElement> ResolveAnimationTargets(View rootContent, PageAnimationOptions options)
        {
            var result = new List<VisualElement>();

            if (!IsEligibleForAnimation(rootContent))
                return result;

            // إذا طلب المطور تحريك الحاوية فقط، أو كان المحتوى عنصر قائمة افتراضية مباشرة
            if (options.Mode == PageAnimationMode.ContainerOnly || IsVirtualizedOrHeavyControl(rootContent))
            {
                result.Add(rootContent);
                return result;
            }

            // استخراج الحاوية الفعلية في حال كان المحتوى مغلفًا بـ ScrollView أو Border أو ContentView
            Layout? targetLayout = ExtractTargetLayout(rootContent, options.UnwrapScrollViewContent);

            if (targetLayout == null)
            {
                result.Add(rootContent);
                return result;
            }

            // إذا كانت الحاوية Grid تُستخدم لتكديس عناصر فوق بعضها في نفس الخلية (Overlay Grid)،
            // فإن تحريك العناصر منفردة سيفصل الخلفية عن المحتوى، لذا نحرك الحاوية ككتلة واحدة
            if (options.Mode == PageAnimationMode.Auto && IsSingleCellOverlayGrid(targetLayout))
            {
                result.Add(rootContent);
                return result;
            }

            var eligibleChildren = targetLayout.Children
                .OfType<VisualElement>()
                .Where(IsEligibleForAnimation)
                .ToList();

            if (eligibleChildren.Count == 0)
            {
                result.Add(rootContent);
                return result;
            }

            // إذا كان عدد العناصر الفرعية كبيرًا جدًا في الوضع التلقائي، نحرك الحاوية ككتلة واحدة
            // للحفاظ على سرعة استجابة الصفحة وتجنب إنشاء عشرات المهام المتزامنة
            if (options.Mode == PageAnimationMode.Auto && eligibleChildren.Count > options.MaxStaggeredChildren)
            {
                result.Add(rootContent);
                return result;
            }

            // في الوضع Staggered الصريح، نلتزم بالحد الأقصى عبر الاكتفاء بأول مجموعة أو تحريك العناصر المرئية
            if (eligibleChildren.Count > options.MaxStaggeredChildren * 2)
            {
                result.Add(rootContent);
                return result;
            }

            return eligibleChildren;
        }

        private static Layout? ExtractTargetLayout(View rootContent, bool unwrapScrollView)
        {
            View? current = rootContent;

            // فك تغليف ScrollView إذا كان الخيار مفعلًا
            if (unwrapScrollView && current is ScrollView scrollView && scrollView.Content != null)
            {
                current = scrollView.Content;
            }

            // فك تغليف طبقة واحدة من Border أو ContentView إذا كانت تحوي Layout بداخلها
            if (current is Border border && border.Content is Layout borderLayout)
            {
                return borderLayout;
            }

            if (current is ContentView contentView && contentView.Content is Layout contentViewLayout)
            {
                return contentViewLayout;
            }

            // ملاحظة مهمة في .NET MAUI: الكلاس ScrollView يرث من Layout، لذلك نستثنيه هنا إذا لم يُفك تغليفه
            if (current is Layout layout && current is not ScrollView)
            {
                return layout;
            }

            return null;
        }

        private static bool IsEligibleForAnimation(VisualElement? element)
        {
            // لا نحرك العناصر المخفية صراحة عبر IsVisible
            return element is { IsVisible: true };
        }

        private static bool IsVirtualizedOrHeavyControl(VisualElement element)
        {
            return element is CollectionView
                or ListView
                or CarouselView
                or WebView;
        }

        private static bool IsSingleCellOverlayGrid(Layout layout)
        {
            if (layout is not Grid grid)
                return false;

            bool hasSingleRow = grid.RowDefinitions.Count <= 1;
            bool hasSingleCol = grid.ColumnDefinitions.Count <= 1;

            if (!hasSingleRow || !hasSingleCol)
                return false;

            var visibleChildren = grid.Children.OfType<VisualElement>().Where(c => c.IsVisible).ToList();
            if (visibleChildren.Count <= 1)
                return false;

            // إذا كان هناك أكثر من عنصر مرئي في Grid بلا صفوف أو أعمدة محددة، فهي طبقة متراكبة (Overlay)
            return true;
        }

        private static async Task WaitForPageLoadedOrTimeoutAsync(
            ContentPage page,
            int maxWaitMs,
            CancellationToken token)
        {
            if (page.IsLoaded)
                return;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnLoaded(object? sender, EventArgs e) => tcs.TrySetResult(true);

            page.Loaded += OnLoaded;
            try
            {
                var delayTask = Task.Delay(maxWaitMs, token);
                await Task.WhenAny(tcs.Task, delayTask);
            }
            finally
            {
                page.Loaded -= OnLoaded;
            }
        }
    }
}
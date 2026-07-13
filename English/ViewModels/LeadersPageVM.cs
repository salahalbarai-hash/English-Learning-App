using English.Models;
using English.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace English.ViewModels
{
    public class LeadersPageVM : INotifyPropertyChanged
    {
        // تهيئة القائمة فوراً تمنع مشاكل الربط (Binding)
        public ObservableCollection<Leader> TopTenLeaders { get; set; } = [];

        private bool isBusy;
        private bool isErrorVisible;
        private bool showOverlay;

        public bool IsBusy
        {
            get => isBusy;
            set { isBusy = value; OnPropertyChanged(); }
        }

        public bool IsErrorVisible
        {
            get => isErrorVisible;
            set { isErrorVisible = value; OnPropertyChanged(); }
        }

        public bool ShowOverlay
        {
            get => showOverlay;
            set { showOverlay = value; OnPropertyChanged(); }
        }

        public LeadersPageVM() { }

        public async Task LoadLeadersAsync()
        {
            if (IsBusy) return;

            // إعداد الحالة البصرية للتحميل
            ShowOverlay = true;
            IsBusy = true;
            IsErrorVisible = false;

            try
            {
                // فحص الإنترنت قبل الجلب
                if (await Service.HasActiveInternetAsync(4))
                {
                    var leaders = await Service.GetStudents();

                    if (leaders != null)
                    {
                        // تحديث القائمة على الخيط الرئيسي لضمان الظهور الفوري
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            TopTenLeaders.Clear();
                            int i = 1;
                            foreach (var leader in leaders)
                            {
                                leader.Rank = i++;
                                TopTenLeaders.Add(leader);
                            }
                            ShowOverlay = false; // إخفاء طبقة التحميل بنجاح
                        });
                    }
                }
                else
                {
                    IsErrorVisible = true;
                }
            }
            catch (Exception)
            {
                IsErrorVisible = true;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
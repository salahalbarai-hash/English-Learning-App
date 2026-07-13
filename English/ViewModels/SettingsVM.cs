using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using English.Models;
using English.Services;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Alerts; // مكتبة التوست
using CommunityToolkit.Maui.Core;   // إعدادات التوست
using System;
using System.Threading.Tasks;

namespace English.ViewModels
{
    public partial class SettingsVM : ObservableObject
    {
        private bool internetConnected = true;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string userName = Preferences.Get(nameof(UserName), "");

        [ObservableProperty]
        private string password = Preferences.Get(nameof(Password), "");

        [ObservableProperty]
        private string phoneNumber = Preferences.Get(nameof(PhoneNumber), "");

        public SettingsVM()
        {
            Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;
            internetConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }

        [RelayCommand]
        private async Task SaveCustomerInformationAsync()
        {
            if (IsBusy) return;

            if (!await CheckInputsAsync())
                return;

            try
            {
                IsBusy = true;
                string customerId = Preferences.Get("ID", "").Trim('"');

                var user = new User
                {
                    ID = string.IsNullOrEmpty(customerId) ? 0 : Convert.ToInt64(customerId),
                    UserName = this.UserName,
                    Password = this.Password,
                    PhoneNumber = this.PhoneNumber
                };

                string success = await Service.UpdateUser(user);

                if (string.IsNullOrEmpty(success))
                {
                    await ShowToast("حدث خطأ، يرجى المحاولة لاحقاً");
                }
                else
                {
                    Preferences.Set(nameof(UserName), UserName);
                    Preferences.Set(nameof(Password), Password);
                    Preferences.Set(nameof(PhoneNumber), PhoneNumber);

                    await ShowToast("تم حفظ البيانات بنجاح ✅");
                }
            }
            catch (Exception ex)
            {
                await ShowToast("خطأ في الاتصال بالسيرفر");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<bool> CheckInputsAsync()
        {
            if (string.IsNullOrWhiteSpace(UserName))
            {
                await ShowToast("يرجى إدخال اسم المستخدم");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                await ShowToast("يرجى إدخال كلمة المرور");
                return false;
            }

            if (!internetConnected)
            {
                await ShowToast("لا يوجد اتصال بالإنترنت 🌐");
                return false;
            }

            return true;
        }

        // دالة مساعدة لعرض التوست بشكل احترافي وموحد في كل الصفحة
        private async Task ShowToast(string message)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            var toast = Toast.Make(message, ToastDuration.Short, 14); // رسالة، مدة قصيرة، حجم خط 14
            await toast.Show(cancellationTokenSource.Token);
        }

        private void Connectivity_ConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            internetConnected = e.NetworkAccess == NetworkAccess.Internet;
        }
    }
}
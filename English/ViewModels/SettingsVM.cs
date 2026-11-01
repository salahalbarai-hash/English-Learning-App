using English.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using English.Models;
using CommunityToolkit.Maui.Core;   // إعدادات التوست

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
        private string password = string.Empty;

        [ObservableProperty]
        private string phoneNumber = Preferences.Get(nameof(PhoneNumber), "");

        public SettingsVM()
        {
            Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;
            internetConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            _ = LoadPasswordAsync();
        }

        private async Task LoadPasswordAsync()
        {
            try { Password = await SecureStorage.GetAsync("Password") ?? ""; } catch { }
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
                    await SecureStorage.SetAsync("Password", Password ?? "");
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


        [RelayCommand]
        private async Task DeleteAccountAsync()
        {
            if (IsBusy) return;

            if (!internetConnected)
            {
                await ShowToast("لا يوجد اتصال بالإنترنت");
                return;
            }

            Page? currentPage = Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0].Page : null;
            if (currentPage == null) return;

            bool confirm = await currentPage.DisplayAlert(
                "حذف الحساب",
                "هل أنت متأكد من رغبتك في حذف الحساب نهائياً؟ سيتم مسح جميع بياناتك ونقاطك وكلماتك ولا يمكن التراجع عن هذا الإجراء.",
                "نعم، احذف حسابي",
                "إلغاء");

            if (!confirm) return;

            try
            {
                IsBusy = true;
                string currentUserName = Preferences.Get(nameof(UserName), "");
                if (string.IsNullOrWhiteSpace(currentUserName))
                    currentUserName = UserName;

                bool isDeleted = await Service.DeleteUserAsync(currentUserName);
                if (isDeleted)
                {
                    if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is AppShell appShell)
                    {
                        try { await appShell.GameHub.DisconnectAsync(); } catch { }
                    }

                    Preferences.Clear();
                    SecureStorage.RemoveAll();
                    Preferences.Set("IsLogin", "0");

                    await ShowToast("تم حذف الحساب بنجاح");
                    await Shell.Current.GoToAsync("//LoginPage");
                }
                else
                {
                    await ShowToast("تعذر حذف الحساب، يرجى المحاولة لاحقاً");
                }
            }
            catch (Exception ex)
            {
                await ShowToast($"حدث خطأ: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Connectivity_ConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            internetConnected = e.NetworkAccess == NetworkAccess.Internet;
        }
    }
}
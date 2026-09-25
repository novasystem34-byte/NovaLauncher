using System;
using System.Windows;
using System.Windows.Threading;
using NovaLauncher.Core;

namespace NovaLauncher
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            CrashShieldLogger.LogInfo("تم بدء تشغيل Nova Launcher.");
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            CrashShieldLogger.LogError("خطأ غير متوقع في واجهة اللانشر (تم منع الكراش).", e.Exception);

            Exception rootCause = e.Exception;
            while (rootCause.InnerException != null)
            {
                rootCause = rootCause.InnerException;
            }

            MessageBox.Show(
                "حدث خطأ غير متوقع تم التعامل معه دون إغلاق اللانشر:\n\n" + rootCause.Message +
                "\n\n(تفاصيل كاملة محفوظة في ملف السجل: %AppData%\\NovaLauncher\\launcher.log)",
                "Nova Launcher - تنبيه",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                CrashShieldLogger.LogError("خطأ فادح على مستوى العملية بالكامل.", ex);
            }
        }
    }
}

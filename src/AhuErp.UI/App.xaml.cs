using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using AhuErp.Core.Services;
using AhuErp.UI.Infrastructure;
using AhuErp.UI.ViewModels;
using Serilog;
using Serilog.Events;

namespace AhuErp.UI
{
    /// <summary>
    /// Корневой App. Инициализирует DI-контейнер, показывает окно входа и,
    /// в случае успешной аутентификации, открывает <see cref="MainWindow"/>
    /// с уже разрешённой <see cref="MainViewModel"/> (зависимости приходят через DI).
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Phase 21 / Improvement #18 — структурированное логирование.
            // Инициализируем Serilog до создания DI-контейнера, чтобы ошибки
            // на этапе AppServices.Initialize() / EfDataSeeder тоже попали
            // в файл. Используем статический Log.Logger — ViewModel-ы и
            // фоновые таймеры пишут через него без отдельной DI-обёртки.
            ConfigureSerilog();
            Log.Information("AhuErp starting (v{Version})", typeof(App).Assembly.GetName().Version);

            // Любое необработанное исключение в WPF (в том числе при resolve
            // ViewModel-ов через DI и при запросах к EF6) иначе молча убивает
            // процесс. Показываем модалку с полным текстом, чтобы пользователь
            // мог переслать стек разработчику.
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            AppServices.Initialize();

            ShowLoginAndThenMain();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Flush буфера Serilog перед закрытием приложения, иначе
            // последние записи (в т.ч. сообщение о фатале) могут не успеть
            // долететь до файла.
            Log.Information("AhuErp exit (ExitCode={ExitCode})", e.ApplicationExitCode);
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        /// <summary>
        /// Конфигурирует <see cref="Log.Logger"/>: файл с ротацией по дню в
        /// <c>%LOCALAPPDATA%\AhuErp\logs\ahuerp-YYYYMMDD.log</c>, fallback на
        /// <c>%TEMP%\AhuErp\logs\</c>, плюс Debug-output (Visual Studio /
        /// DebugView). Минимальный уровень — <see cref="LogEventLevel.Information"/>;
        /// для EF6 / Microsoft / System источников поднят до Warning, чтобы
        /// не засорять журнал служебной болтовнёй.
        /// </summary>
        private static void ConfigureSerilog()
        {
            var logDir = ResolveLogDirectory();
            var template =
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] " +
                "{Message:lj}{NewLine}{Exception}";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Debug(outputTemplate: template)
                .WriteTo.File(
                    path: Path.Combine(logDir, "ahuerp-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    shared: true,
                    outputTemplate: template)
                .CreateLogger();
        }

        private static string ResolveLogDirectory()
        {
            string root;
            try
            {
                root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(root))
                {
                    root = Path.GetTempPath();
                }
            }
            catch
            {
                root = Path.GetTempPath();
            }

            var dir = Path.Combine(root, "AhuErp", "logs");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                // Если каталог недоступен (нет прав / read-only тома),
                // упадём только в Debug-sink — это не критично для UI.
            }
            return dir;
        }

        /// <summary>
        /// Цикл «логин → главное окно». Если пользователь нажал «Выйти» в
        /// шапке (MainViewModel.LogoutCommand ставит Tag="logout"), MainWindow
        /// закрывается и мы заново показываем LoginWindow вместо <see cref="Shutdown()"/>.
        /// </summary>
        private void ShowLoginAndThenMain()
        {
            var loginVm = AppServices.GetRequiredService<LoginViewModel>();
            var login = new LoginWindow(loginVm);

            // ShutdownMode=OnExplicitShutdown в App.xaml: WPF не закрывает приложение
            // сам, когда `Application.Windows` ненадолго становится пустым между
            // закрытием LoginWindow и показом MainWindow.
            if (login.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            var mainVm = AppServices.GetRequiredService<MainViewModel>();
            var main = new MainWindow { DataContext = mainVm };
            MainWindow = main;
            main.Closed += (_, __) =>
            {
                if (string.Equals(main.Tag as string, "logout", StringComparison.Ordinal))
                {
                    // Перезапускаем цикл входа, не выгружая приложение.
                    Dispatcher.BeginInvoke(new Action(ShowLoginAndThenMain));
                }
                else
                {
                    Shutdown();
                }
            };
            main.Show();

            // Phase 9 — раз в 60 секунд обходим активные задачи и создаём
            // напоминания TaskDeadlineSoon / TaskOverdue, плюс обновляем
            // счётчик непрочитанных в шапке. Таймер живёт пока живо MainWindow.
            var notifications = AppServices.GetRequiredService<INotificationService>();
            var reminderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60),
            };
            reminderTimer.Tick += (_, __) =>
            {
                try
                {
                    notifications.TickReminders(DateTime.Now);
                    mainVm.RefreshUnreadCount();
                }
                catch (Exception ex)
                {
                    // Сбой фонового таймера не должен ронять UI; пишем
                    // в Serilog, диагностика идёт через журнал аудита.
                    Log.Warning(ex, "Reminder timer tick failed");
                }
            };
            reminderTimer.Start();
            main.Closed += (_, __) => reminderTimer.Stop();

            // Phase 10 — фоновое доиндексирование вложений каждые 5 минут.
            // Запускаем именно через DispatcherTimer, чтобы EF6/AhuDbContext
            // оставался в одном UI-треде (контекст у нас Singleton).
            var indexTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            indexTimer.Tick += (_, __) =>
            {
                try
                {
                    var index = AppServices.GetRequiredService<ISearchIndexService>();
                    index.IndexOutdated();
                }
                catch (Exception ex)
                {
                    // Поиск — не критичный фоновый процесс; пишем warning
                    // и продолжаем работу.
                    Log.Warning(ex, "Background search index update failed");
                }
            };
            indexTimer.Start();
            main.Closed += (_, __) => indexTimer.Stop();
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "Unhandled exception on UI thread");
            ShowFatal(e.Exception, "UI-поток");

            // Если падение случилось ДО показа главного окна (например, ошибка
            // подключения к SQL Server в EfDataSeeder или ошибка резолва
            // ViewModel-ов через DI), оставлять процесс «живым» нельзя: при
            // ShutdownMode.OnLastWindowClose ни одного окна нет — получится
            // невидимый зомби. Поэтому после показа стека сразу глушим процесс.
            // Помечаем обработанным в обоих случаях — иначе исключение «всплывёт»
            // в AppDomain.UnhandledException и пользователь увидит вторую модалку
            // про то же самое падение.
            e.Handled = true;
            if (Current?.MainWindow == null)
            {
                Current?.Shutdown(1);
            }
        }

        private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Unhandled exception on AppDomain (IsTerminating={IsTerminating})", e.IsTerminating);
                ShowFatal(ex, "AppDomain");
            }
        }

        private static void ShowFatal(Exception ex, string source)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Источник: {source}");
            sb.AppendLine();
            for (var current = ex; current != null; current = current.InnerException)
            {
                sb.AppendLine($"{current.GetType().FullName}: {current.Message}");
                sb.AppendLine(current.StackTrace);
                sb.AppendLine();
            }
            MessageBox.Show(
                sb.ToString(),
                "Необработанная ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}

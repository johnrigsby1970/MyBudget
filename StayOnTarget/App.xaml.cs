using System.IO;
using System.Windows;
using Microsoft.Data.Sqlite;
using Serilog;
using StayOnTarget.Data;
using StayOnTarget.Services;
using StayOnTarget.ViewModels;
using Windows.Security.Credentials;
using Serilog.Events;
using StayOnTarget.Helpers;
using StayOnTarget.ViewModels.Wizard;
using StayOnTarget.Views.Wizard;

namespace StayOnTarget;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application {
    protected override async void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);
        string sentryDsn = 
#if DEBUG
        string.Empty;
#else
            "https://8bb5d363029e82d05ec88dc7ed3aebe6@o4511910567149568.ingest.us.sentry.io/4511960904957952";
#endif
        
        // 1. Initialize Sentry FIRST so it catches any startup failures
        SentrySdk.Init(o => {
            
            // Essential for WPF / desktop applications
            o.IsGlobalModeEnabled = true;

            o.SampleRate = 1.0f; // Capture 100% of crashes
            o.TracesSampleRate = 0.0; // Disable performance tracing (focused purely on crashes)

#if DEBUG
        // Blank out the DSN during local debugging so it never sends data to sentry.io
        o.Dsn = string.Empty;
        o.Debug = true;
        o.Environment = "development";
#else
            o.Dsn = sentryDsn;
            o.Debug = false;
            o.Environment = "production";
#endif
        });

        // Initialize logging first
        LogConfig.Initialize(sentryDsn);

        Log.Write(LogEventLevel.Debug, "Hello Sentry");

        SetupGlobalExceptionHandling();

        // Log session startup details
        Log.Information("{AppName} session started. OS: {OSVersion}, Version: {AppVersion}",
            Constants.AppName,
            Environment.OSVersion,
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);

        Log.Information("OnStartup started.");

        // Tell WPF not to shut down just because a window closes
        Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        string dbPath = StayOnTarget.Properties.Settings.Default.DatabasePath(); //DatabaseContext.GetDefaultDbPath();

        Log.Information("Database path: {DbPath}", dbPath);
        bool dbExists = File.Exists(dbPath);
        string? password = null;

        // Try Windows Hello first if database exists
        if (dbExists) {
            Log.Information("Database exists, attempting auto-unlock.");
            try {
                bool userWantsHello = StayOnTarget.Properties.Settings.Default.UseWindowsHello;
                if (userWantsHello) {
                    password = await HelperMethods.TryUnlockWithWindowsHello();
                }
            }
            catch (Exception ex) {
                Log.Error(ex, "Error during Windows Hello unlock attempt.");
            }

            if (password != null) {
                try {
                    // Verify the password from vault works
                    var dbContext = new DatabaseContext(dbPath, password);
                    using (var connection = dbContext.GetConnection()) {
                        connection.Open();
                    }

                    Log.Information("Auto-unlock successful.");
                    // Success! Launch MainWindow

                    dbContext.InitializeDatabase();

                    var budgetService = new BudgetService(dbContext, password);
                    //LaunchMainWindow(dbPath, password);
                    LaunchMainWindow(budgetService);
                    return;
                }
                catch (SqliteException ex) {
                    Log.Warning(ex, "Vault password invalid or database error during auto-unlock. Clearing vault.");
                    // Vault password invalid (e.g. database replaced), clear it
                    var vault = new PasswordVault();
                    try {
                        var credential = vault.Retrieve("StayOnTarget_DB_Vault", "MasterKey");
                        vault.Remove(credential);
                    }
                    catch (Exception vaultEx) {
                        Log.Error(vaultEx, "Failed to remove invalid credential from vault.");
                    }
                }
                catch (Exception ex) {
                    Log.Fatal(ex, "Unexpected error during auto-unlock.");
                    MessageBox.Show($"Unexpected error during auto-unlock: {ex.Message}", "Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            else {
                try {
                    Log.Information("Showing PasswordPromptWindow.");
                    var passwordWindow = new PasswordPromptWindow(!dbExists, dbPath);
                    if (passwordWindow.ShowDialog() == true) {
                        Log.Information("Password provided, launching MainWindow.");

                        // Verify the password from vault works
                        var dbContext = new DatabaseContext(dbPath, passwordWindow.Password);
                        using (var connection = dbContext.GetConnection()) {
                            connection.Open();
                        }

                        Log.Information("Auto-unlock successful.");
                        // Success! Launch MainWindow

                        dbContext.InitializeDatabase();

                        var budgetService = new BudgetService(dbContext, passwordWindow.Password);
                        //var budgetService = new BudgetService(dbPath, passwordWindow.Password);
                        //var budgetService = new BudgetService(dbPath, passwordWindow.Password);
                        LaunchMainWindow(budgetService);
                    }
                    else {
                        Log.Information("Password prompt cancelled. Shutting down.");
                        Shutdown();
                    }
                }
                catch (Exception ex) {
                    Log.Fatal(ex, "Error during password prompt or main window launch.");
                    MessageBox.Show($"Critical error during startup: {ex.Message}", "Critical Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                }
            }
        }
        else {
            Log.Information("Database does not exist. User will need to create one.");
        }

        if (!dbExists) {
            try {
                DatabaseInitializationContext DatabaseInitializationContext = new DatabaseInitializationContext();

                var wizardViewModel = new WizardViewModel(DatabaseInitializationContext);

                // Show the view/dialog
                var wizardWindow = new WizardWindow(wizardViewModel) {
                    //Owner = Application.Current.MainWindow // Keeps it modal to the main window
                };

                bool? result = wizardWindow.ShowDialog();

                if (result == true) {
                    Log.Information("Database exists, attempting auto-unlock.");
                    // try {
                    //     bool userWantsHello = StayOnTarget.Properties.Settings.Default.UseWindowsHello;
                    //     if (userWantsHello) {
                    //         password = await Helpers.TryUnlockWithWindowsHello();
                    //     }
                    // }
                    // catch (Exception ex) {
                    //     Log.Error(ex, "Error during Windows Hello unlock attempt.");
                    // }
                    //
                    // if (password != null) {
                    //     try {
                    //         // Verify the password from vault works
                    //         var dbContext = new DatabaseContext(dbPath, password);
                    //         using (var connection = dbContext.GetConnection()) {
                    //             connection.Open();
                    //         }
                    //
                    //         Log.Information("Auto-unlock successful.");
                    //         // Success! Launch MainWindow
                    //         LaunchMainWindow(dbPath, password);
                    //     }
                    //     catch (SqliteException ex) {
                    //         Log.Warning(ex, "Vault password invalid or database error during auto-unlock. Clearing vault.");
                    //         // Vault password invalid (e.g. database replaced), clear it
                    //         var vault = new PasswordVault();
                    //         try {
                    //             var credential = vault.Retrieve("StayOnTarget_DB_Vault", "MasterKey");
                    //             vault.Remove(credential);
                    //         }
                    //         catch (Exception vaultEx) {
                    //             Log.Error(vaultEx, "Failed to remove invalid credential from vault.");
                    //         }
                    //     }
                    //     catch (Exception ex) {
                    //         Log.Fatal(ex, "Unexpected error during auto-unlock.");
                    //         MessageBox.Show($"Unexpected error during auto-unlock: {ex.Message}", "Error",
                    //             MessageBoxButton.OK, MessageBoxImage.Error);
                    //     }
                    // }


                    // Since we already initialized the BudgetService in the wizard,
                    // we should be able to launch the MainWindow directly.
                    // However, App.xaml.cs expects to launch it with dbPath and password.

                    // Let's check what we have in DatabaseInitializationContext
                    if (DatabaseInitializationContext.BudgetService != null) {
                        Log.Information("Wizard completed successfully. Launching MainWindow.");

                        // We need to get the password from the wizard. 
                        // DatabaseInitializationContext doesn't have it, but DatabaseNameViewModel does.
                        var dbStep = wizardViewModel.Steps.OfType<DatabaseNameViewModel>().FirstOrDefault();
                        string passwordProvided = dbStep?.Password ?? "";
                        string currentDbPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            @"AppData\Local\StayOnTarget", dbStep?.DatabaseName ?? "budget.db");

                        // Verify the password from vault works
                        var dbContext = new DatabaseContext(currentDbPath, passwordProvided);
                        using (var connection = dbContext.GetConnection()) {
                            connection.Open();
                        }

                        Log.Information("Auto-unlock successful.");
                        // Success! Launch MainWindow

                        dbContext.InitializeDatabase();

                        var budgetService = new BudgetService(dbContext, passwordProvided);

                        LaunchMainWindow(budgetService);
                    }
                    else {
                        Log.Warning("Wizard result was true but BudgetService is null. Shutting down.");
                        Shutdown();
                    }
                }
                else {
                    Log.Information("Initialization wizard cancelled. Shutting down.");
                    Shutdown();
                }
            }
            catch (Exception ex) {
                Log.Fatal(ex, "Error during password prompt or main window launch.");
                MessageBox.Show($"Critical error during startup: {ex.Message}", "Critical Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }
    }

    /// <summary>
    /// Forcibly re-enables MainWindow at the Win32 level if a modal crash left it disabled.
    /// </summary>
    public static void ForceUnlockMainWindow() {
        try {
            if (Current.MainWindow != null) {
                var helper = new System.Windows.Interop.WindowInteropHelper(Current.MainWindow);
                if (helper.Handle != IntPtr.Zero) {
                    // 1. Re-enable Win32 mouse/keyboard input to MainWindow
                    NativeMethods.EnableWindow(helper.Handle, true);

                    // 2. Force MainWindow to the foreground
                    NativeMethods.SetForegroundWindow(helper.Handle);

                    // 3. Ensure Topmost status is reapplied if needed
                    Current.MainWindow.Topmost = true;
                }
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error in ForceUnlockMainWindow during emergency recovery.");
        }
    }

    private void SetupGlobalExceptionHandling() {
        // 1. Unhandled WPF UI Thread Exceptions (App stays alive)
        DispatcherUnhandledException += (s, e) => {
            // SPECIAL CASE: Check if the crash happened during modal/dialog teardown
            if (e.Exception is NullReferenceException && e.Exception.StackTrace?.Contains("DoDialogHide") == true) {
                Log.Error(e.Exception, "Caught modal DoDialogHide crash! Forcibly unlocking MainWindow.");
                SentrySdk.CaptureException(e.Exception);

                // Recover MainWindow input state so the app doesn't freeze in a beep loop
                ForceUnlockMainWindow();

                // Mark exception as handled silently without showing a MessageBox
                e.Handled = true;
                return;
            }

            // GENERAL CASE: Standard unhandled UI exceptions
            Log.Error(e.Exception, "Unhandled UI dispatcher exception.");

            // Explicitly push to Sentry since e.Handled = true prevents a hard crash crash-dump
            SentrySdk.CaptureException(e.Exception);

            // Do NOT call Log.CloseAndFlush() here because e.Handled = true keeps Serilog running!
            MessageBox.Show(
                $"An unexpected UI error occurred: {e.Exception?.Message ?? "Unknown error"}",
                "Unexpected Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            // Keep app alive for recoverable UI errors
            e.Handled = true;
        };

        // 2. Critical AppDomain / Non-UI Thread Crashes (App WILL terminate)
        AppDomain.CurrentDomain.UnhandledException += (s, e) => {
            var ex = e.ExceptionObject as Exception;
            string errorDetails = ex?.ToString() ?? e.ExceptionObject?.ToString() ?? "Unknown exception";

            Log.Fatal(ex, "Unhandled AppDomain exception. Terminating: {IsTerminating}. Details: {Details}",
                e.IsTerminating, errorDetails);
            if (ex != null) SentrySdk.CaptureException(ex);

            // Synchronously flush Serilog because process termination is imminent
            Log.CloseAndFlush();

            if (e.IsTerminating) {
                string userMessage =
                    $"A critical error occurred and the application must close:\n\n{ex?.Message ?? "Unknown error"}";

                // Safely show dialog on UI Thread if coming from a background thread
                if (Current != null && Current.Dispatcher.CheckAccess() == false) {
                    Current.Dispatcher.Invoke(() => {
                        MessageBox.Show(userMessage, "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                else {
                    MessageBox.Show(userMessage, "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        };

        // 3. Unobserved Async Task Exceptions
        TaskScheduler.UnobservedTaskException += (s, e) => {
            Log.Error(e.Exception, "Unobserved task exception caught.");

            // Prevent background task failures from tearing down process
            e.SetObserved();
        };
    }

    private void LaunchMainWindow(BudgetService budgetService) {
        //private void LaunchMainWindow(string dbPath, string password) {
        try {
            Log.Information("Initializing BudgetService and MainWindow.");
            //var budgetService = new BudgetService(dbPath, password);
            var reconciliationService = new ReconciliationService(budgetService);
            var viewModel = new MainViewModel(budgetService, reconciliationService);
            var mainWindow = new MainWindow(viewModel);

            // STEP 2: Make this the official main window
            Current.MainWindow = mainWindow;

            // STEP 3: Change the shutdown mode back so closing the main window exits the app
            Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

            mainWindow.Show();
            Log.Information("MainWindow shown.");
        }
        catch (Exception ex) {
            Log.Fatal(ex, "Failed to initialize database or main window.");
            MessageBox.Show($"Failed to initialize database: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e) {
        LogConfig.Shutdown();
        base.OnExit(e);
    }
}
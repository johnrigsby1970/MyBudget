using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.Sqlite;
using Serilog;
using StayOnTarget.Data;
using StayOnTarget.Services;
using StayOnTarget.ViewModels;
using Windows.Security.Credentials;
using StayOnTarget.ViewModels.Wizard;
using StayOnTarget.Views.Wizard;

namespace StayOnTarget;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application {
    protected override async void OnStartup(StartupEventArgs e) {
        LogConfig.Initialize();
        base.OnStartup(e);

        SetupGlobalExceptionHandling();

        Log.Information("OnStartup started.");

        // STEP 1: Tell WPF not to shut down just because a window closes
        Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        string dbPath = StayOnTarget.Properties.Settings.Default.DatabasePath();//DatabaseContext.GetDefaultDbPath();
        
        // try {
        //     string savedPath = StayOnTarget.Properties.Settings.Default.DatabasePath();
        //     if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath)) {
        //         dbPath = savedPath;
        //     }
        // }
        // catch (Exception ex) {
        //     Log.Error(ex, "Error during Windows Hello unlock attempt.");
        // }
        
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
                try
                {
                    Log.Information("Showing PasswordPromptWindow.");
                    var passwordWindow = new PasswordPromptWindow(!dbExists, dbPath);
                    if (passwordWindow.ShowDialog() == true)
                    {
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
                    else
                    {
                        Log.Information("Password prompt cancelled. Shutting down.");
                        Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Error during password prompt or main window launch.");
                    MessageBox.Show($"Critical error during startup: {ex.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void SetupGlobalExceptionHandling() {
        DispatcherUnhandledException += (s, e) => {
            Log.Fatal(e.Exception, "Unhandled UI dispatcher exception.");
            MessageBox.Show($"An unexpected UI error occurred: {e.Exception.Message}", "Unexpected Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) => {
            var ex = e.ExceptionObject as Exception;
            Log.Fatal(ex, "Unhandled AppDomain exception. Terminating: {IsTerminating}", e.IsTerminating);
            if (e.IsTerminating) {
                MessageBox.Show($"A critical error occurred and the application must close: {ex?.Message}",
                    "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        TaskScheduler.UnobservedTaskException += (s, e) => {
            Log.Error(e.Exception, "Unobserved task exception.");
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
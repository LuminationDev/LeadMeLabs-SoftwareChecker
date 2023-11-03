using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LeadMeLabs_SoftwareChecker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Hide the process and any windows associated with the application
            Visibility = Visibility.Hidden;
            
            // Get the directory where the current executable is located - reflection is required for task scheduler
            string? currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Console.WriteLine("Current directory: " + currentDirectory);
            
            // Track the software and the Launcher program
            bool softwareRunning = false;
            
            // Check if the NUC or Station software is running
            const string stationNameToCheck = "Station";
            const string nucToCheck = "NUC";
            const string launcherToCheck = "LeadMe";

            // Get a list of all running processes
            Process[] processes = Process.GetProcesses();

            // Loop through the list of processes and check if any of them match the desired process name
            foreach (Process process in processes)
            {
                // CHeck if the process is the Station or NUC software
                if (process.ProcessName.Equals(stationNameToCheck, StringComparison.OrdinalIgnoreCase) || process.ProcessName.Equals(nucToCheck, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Software found: " + process.ProcessName);
                    softwareRunning = true;
                }
            }
            
            // Kill (if not null) and then restart the Launcher if no software is running
            if (softwareRunning || CheckIfDownloading())
            {
                // Close the checker
                Application.Current.Shutdown();
                return;
            }
            
            // Loop through the process and kill all Launcher processes as Electron runs up to 5 process with sub-processes
            // for the application.
            foreach (Process process in processes)
            {
                // Check if the process is the LeadMe Launcher
                if (process.ProcessName.Equals(launcherToCheck, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill(true);
                }
            }
            
            // Wait for the process to end
            Task.Delay(4000).Wait();
            
            // Navigate one level up by using Path.GetDirectoryName to get to the Launcher's main folder
            string? localDirectory = Path.GetDirectoryName(currentDirectory);
            string launcherPath = Path.Join(localDirectory, $"{launcherToCheck}.exe");
            
            Console.WriteLine("Launcher path: " + launcherPath);
            StartProcess(launcherPath);
        }

        private void StartProcess(string filePath)
        {
            Console.WriteLine(filePath);
            if (!File.Exists(filePath))
            {
                // Close the checker
                Application.Current.Shutdown();
                return;
            }
            
            new Task(() =>
            {
                // Create a new ProcessStartInfo instance with the file path
                ProcessStartInfo startInfo = new ProcessStartInfo(filePath);

                // Start the process
                Process newProcess = new Process();
                newProcess.StartInfo = startInfo;
                startInfo.UseShellExecute = true;

                // Start the new process
                newProcess.Start();
            }).Start();
            
            // Make sure the process has started
            Task.Delay(4000).Wait();
            
            // Close the checker
            Application.Current.Shutdown();
        }

        private bool CheckIfDownloading()
        {
            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "LeadMeLauncher", PipeDirection.InOut))
            {
                Console.WriteLine("Connecting to Electron...");
                try
                {
                    pipeClient.Connect(5000);
                }
                catch (Exception e)
                {
                    return false;
                }
                
                string messageToSend = "checkIfDownloading";
                byte[] messageBytes = Encoding.UTF8.GetBytes(messageToSend);
                pipeClient.Write(messageBytes, 0, messageBytes.Length);

                // Read the response from the server
                byte[] responseBytes = new byte[1024];
                int bytesRead = pipeClient.Read(responseBytes, 0, responseBytes.Length);
                string responseMessage = Encoding.UTF8.GetString(responseBytes, 0, bytesRead);

                Console.WriteLine($"Received from server: {responseMessage}");
    
                return responseMessage != null && responseMessage.Equals("true");
            }
        }
    }
}

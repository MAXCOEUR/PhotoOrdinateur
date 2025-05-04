using PhotoOrdinateur;
using QRCoder;
using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoSyncServer
{
    public partial class MainWindow : Window
    {
        private const int Port = 8080;
        private readonly string baseFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Photos");
        private PhotoServer server;

        public MainWindow()
        {
            InitializeComponent();
            Firewall.AddFirewallRuleForApp(Port);

            server = new PhotoServer(Port, baseFolder, DisplayQrCode);
            server.Start();
        }

        private void DisplayQrCode(BitmapImage qrImage)
        {
            Dispatcher.Invoke(() => QrCodeImage.Source = qrImage);
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(baseFolder))
                System.Diagnostics.Process.Start("explorer.exe", baseFolder);
            else
                MessageBox.Show("Le dossier d'images n'existe pas encore.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Firewall.RemoveFirewallRuleForApp();
        }
    }

}

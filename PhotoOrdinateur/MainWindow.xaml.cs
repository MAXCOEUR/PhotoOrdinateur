using PhotoOrdinateur;
using QRCoder;
using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
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
        private readonly string baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        private PhotoServer server;

        public MainWindow()
        {
            InitializeComponent();

            Title += " " + GetAppVersion(); // Ajoute la version dans la barre de titre

            try
            {
                server = new PhotoServer(baseFolder, DisplayIpPort,DisplayQrCode, UpdateImageFileName);
                server.Start();
            }
            catch (Exception ex)
            {
                LogErreur.print("Erreur lors de l'initialisation du serveur", ex);
                MessageBox.Show("Une erreur est survenue lors du démarrage du serveur.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateImageFileName(string fileName)
        {
            Dispatcher.Invoke(() => ImageFileNameTextBox.Text = fileName);
        }

        private void DisplayQrCode(BitmapImage qrImage)
        {
            Dispatcher.Invoke(() => QrCodeImage.Source = qrImage);
        }
        private void DisplayIpPort((string, int) ipPort)
        {
            Dispatcher.Invoke(() =>
            {
                IpTextBox.Text = "Ip : " + ipPort.Item1;
                PortTextBox.Text = "Port : " + ipPort.Item2.ToString();
            });
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(baseFolder))
                    System.Diagnostics.Process.Start("explorer.exe", baseFolder);
                else
                    MessageBox.Show("Le dossier d'images n'existe pas encore.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogErreur.print("Erreur lors de l'ouverture du dossier", ex);
                MessageBox.Show("Impossible d'ouvrir le dossier.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                Firewall.RemoveFirewallRuleForApp();
            }
            catch (Exception ex)
            {
                LogErreur.print("Erreur lors de la suppression de la règle pare-feu", ex);
            }
        }

        private string GetAppVersion()
        {
            var version = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;
            return version != null ? $"v{version}" : "v1.0";
        }



    }

}

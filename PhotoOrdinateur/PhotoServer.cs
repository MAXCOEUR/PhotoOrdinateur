using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoOrdinateur
{
    public class PhotoServer
    {
        private int port = 8080;
        private readonly string baseFolder;
        private readonly Action<(string, int)> IpPortCallback;
        private readonly Action<BitmapImage> qrCodeCallback;
        private readonly Action<string> imageFileNameCallback;

        public PhotoServer(string baseFolder, Action<(string, int)> IpPortCallback, Action<BitmapImage> qrCodeCallback, Action<string> imageFileNameCallback)
        {
            this.baseFolder = baseFolder;
            this.IpPortCallback = IpPortCallback;
            this.qrCodeCallback = qrCodeCallback;
            this.imageFileNameCallback = imageFileNameCallback;
        }

        public async void Start()
        {
            try
            {
                while (IsPortInUse(port))
                {
                    port++;
                    if (port > 65535)
                    {
                        MessageBox.Show("Aucun port libre trouvé", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                Firewall.AddFirewallRuleForApp(port);
                string ip = GetLocalIPAddress();
                string url = $"http://{ip}:{port}/upload/";

                Task.Run(() => RunHttpListener(url));

                string qrContent = System.Text.Json.JsonSerializer.Serialize(new { ip, port });
                var image = QrCodeService.Generate(qrContent);
                qrCodeCallback?.Invoke(image);
                IpPortCallback?.Invoke((ip, port));
            }
            catch (Exception ex)
            {
                LogErreur.print("Erreur sur le serveur", ex);
            }
        }

        private async Task RunHttpListener(string prefix)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            Console.WriteLine($"Listening on {prefix}");

            while (true)
            {
                var context = await listener.GetContextAsync();
                if (context.Request.HttpMethod == "POST")
                {
                    await HandleUpload(context);
                }
                else if (context.Request.HttpMethod == "GET")
                {
                    context.Response.StatusCode = 200; // Method Not Allowed
                    context.Response.Close();
                }
                else
                {
                    context.Response.StatusCode = 405; // Method Not Allowed
                    context.Response.Close();
                }
            }
        }

        private async Task HandleUpload(HttpListenerContext context)
        {
            var contentType = context.Request.ContentType ?? "";

            string deviceName;
            DateTime fileDate;
            string fileName;
            byte[] fileContents;

            var query = context.Request.QueryString;
            deviceName = string.IsNullOrWhiteSpace(query["DeviceName"]) ? "UnknownDevice" : query["DeviceName"];

            if (!DateTime.TryParse(query["DateTime"], out fileDate))
            {
                context.Response.StatusCode = 400;
                await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Invalid or missing DateTime"));
                context.Response.Close();
                return;
            }

            if (contentType.StartsWith("multipart/form-data"))
            {
                // MULTIPART
                var boundary = contentType.Split("boundary=")[1];
                var parser = new MultipartParser(context.Request.InputStream, boundary);

                if (parser.FileContents == null || parser.FileName == null)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Invalid multipart data"));
                    context.Response.Close();
                    return;
                }

                // Extraire l'extension du fichier original
                string originalExtension = Path.GetExtension(parser.FileName);
                if (string.IsNullOrEmpty(originalExtension))
                    originalExtension = ".jpg"; // fallback si pas d'extension

                // Récupérer le titre original, si possible (par exemple depuis le nom du fichier ou les données envoyées)
                string originalTitle = Path.GetFileNameWithoutExtension(parser.FileName);

                // Si le titre est trop long, on peut le tronquer si nécessaire pour ne pas dépasser la longueur du nom de fichier
                if (originalTitle.Length > 50) // longueur limite arbitraire, ajuster selon les besoins
                    originalTitle = originalTitle.Substring(0, 50);

                // Générer le nom de fichier basé sur la date et ajouter le titre entre parenthèses
                fileName = $"{fileDate:yyyy-MM-dd HH_mm_ss} ({deviceName}-{originalTitle}){originalExtension}";

                imageFileNameCallback?.Invoke(fileName);

                fileContents = parser.FileContents;
            }
            else
            {
                context.Response.StatusCode = 400;
                await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Invalid or missing multipart/form-data"));
                context.Response.Close();
                return;
            }

            // Crée le chemin final
            var folder = Path.Combine(baseFolder, deviceName, fileDate.Year.ToString(), fileDate.Month.ToString("D2"));
            Directory.CreateDirectory(folder);

            string filePath = Path.Combine(folder, fileName);

            if (!File.Exists(filePath) || !FilesAreEqual(fileContents, filePath))
            {
                await File.WriteAllBytesAsync(filePath, fileContents);
                Console.WriteLine($"Saved {filePath}");
            }
            else
            {
                Console.WriteLine($"Skipped duplicate {filePath}");
            }

            context.Response.StatusCode = 200;
            context.Response.Close();
        }

        private bool FilesAreEqual(byte[] fileData, string path)
        {
            using var md5 = MD5.Create();
            byte[] existingHash;
            using (var fs = File.OpenRead(path))
                existingHash = md5.ComputeHash(fs);
            var incomingHash = md5.ComputeHash(fileData);
            return StructuralComparisons.StructuralEqualityComparer.Equals(existingHash, incomingHash);
        }

        private string GetLocalIPAddress()
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            return "127.0.0.1";
        }

        private bool IsPortInUse(int port)
        {
            try
            {
                // Préparer la commande netstat
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",  // Lancer cmd.exe
                    Arguments = $"/C netstat -ano | findstr :{port}",  // Exécuter netstat et rechercher le port
                    RedirectStandardOutput = true,  // Rediriger la sortie standard
                    RedirectStandardError = false,  // Pas besoin de rediriger les erreurs
                    UseShellExecute = false,  // Ne pas utiliser le shell
                    CreateNoWindow = true  // Pas d'affichage de la fenêtre de CMD
                };

                // Démarrer le processus
                using (var process = Process.Start(processStartInfo))
                {
                    // Lire la sortie
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();  // Attendre la fin du processus

                    // Si la sortie contient quelque chose, cela signifie que le port est utilisé
                    return !string.IsNullOrEmpty(output);
                }
            }
            catch (Exception ex)
            {
                // En cas d'erreur, loguer et supposer que le port est libre
                Console.WriteLine($"Erreur lors de la vérification du port : {ex.Message}");
                return false;
            }
        }
    }

}

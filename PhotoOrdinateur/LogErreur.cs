using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoOrdinateur
{
    public class LogErreur
    {
        public static void print(string message, Exception ex)
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            string fullMessage = $"{DateTime.Now}: {message}\n{ex}\n\n";

            File.AppendAllText(logPath, fullMessage);
        }
    }
}

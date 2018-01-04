using System;
using System.IO;
using System.IO.Compression;
using Lzma;
using Microsoft.Extensions.Configuration;

namespace LzmaConsole
{
    class Program
    {
        class Command
        {
            public CompressionMode Mode { get; set; }
            public string Input { get; set; }
            public string Output { get; set; }
        }

        static int Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            var command = new Command();
            configuration.Bind(command);

            if (!File.Exists(command.Input))
            {
                Console.Error.WriteLine("Error: input file not found.");
                return -1;
            }

            if (File.Exists(command.Output))
            {
                var originalBackgroundColor = Console.BackgroundColor;
                var originalForegroundColor = Console.ForegroundColor;

                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.Yellow;

                Console.WriteLine("Warning: overwriting existing output file.");

                Console.BackgroundColor = originalBackgroundColor;
                Console.ForegroundColor = originalForegroundColor;
            }

            try
            {
                using (var inputStream = File.OpenRead(command.Input))
                using (var lzmaStream = new LzmaStream(inputStream, command.Mode, true))
                using (var outputStream = File.OpenWrite(command.Output))
                {
                    lzmaStream.CopyTo(outputStream);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return -1;
            }

            return 0;
        }
    }
}

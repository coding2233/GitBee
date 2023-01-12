// See https://aka.ms/new-console-template for more information
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;

internal class Program
{
    static void Main(string[] args)
    {
        args = System.Environment.GetCommandLineArgs();

        string zipFilePath = null;
        string extractDir = null;
        string execPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("ZipFilePath="))
            {
                var splitArgs = args[i].Split('=');
                zipFilePath = splitArgs[1];
            }
            else if (args[i].StartsWith("ExtractDir="))
            {
                var splitArgs = args[i].Split('=');
                extractDir = splitArgs[1];
            }
            else if (args[i].StartsWith("ExecPath="))
            {
                var splitArgs = args[i].Split('=');
                execPath = splitArgs[1];
            }
        }

        //等待0.5s
        Thread.Sleep(500);

        //可运行
        if (!string.IsNullOrEmpty(zipFilePath) && !string.IsNullOrEmpty(extractDir) && !string.IsNullOrEmpty(execPath))
        {
            if (zipFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                FastZip fastZip = new FastZip();
                fastZip.ExtractZip(zipFilePath, extractDir, "");
            }
            else
            {
                ExtractTGZ(zipFilePath, extractDir);
            }
            Process.Start("dotnet", execPath);
        }
        //FastZip fastZip = new FastZip();
        //fastZip.CreateZip("test.zip", @"E:\source\temp\HoneyBee.Git.Gui\HoneyBee.Git.Gui\bin\x64\Debug\net6.0\", true, "");

        Console.WriteLine("Hello, World!");
    }



    public static void ExtractTGZ(String gzArchiveName, String destFolder)
    {
        Stream inStream = File.OpenRead(gzArchiveName);
        Stream gzipStream = new GZipInputStream(inStream);

        TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream,null);
        tarArchive.ExtractContents(destFolder);
        tarArchive.Close();

        gzipStream.Close();
        inStream.Close();
    }

}
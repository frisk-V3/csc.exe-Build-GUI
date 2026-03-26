using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace BuildApp
{
    class Program
    {
        // エントリポイント: async Task Main にすることで非同期処理を可能にする
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Build Runner Process Started ===");
            
            try 
            {
                // インスタンス化して実行
                var runner = new BuildRunner();
                await runner.SeriousBuildAndRunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
            }

            Console.WriteLine("=== Process Finished ===");
        }
    }

    public class BuildRunner
    {
        public async Task SeriousBuildAndRunAsync()
        {
            // --- ここからが「本気」のロジック ---
            
            // 1. ファイル選択 (GitHub Actions上ではGUIは出せないので、カレントディレクトリのcsを探す例)
            // ローカルで動かす場合は OpenFileDialog に戻してください
            string sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "Program.cs");
            
            if (!File.Exists(sourcePath))
            {
                Console.WriteLine("Source file not found.");
                return;
            }

            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            
            // ディレクトリがなければ作成
            if (!Directory.Exists(downloadsDir)) Directory.CreateDirectory(downloadsDir);
            
            string outputExe = Path.Combine(downloadsDir, $"{fileName}_compiled.exe");

            // 2. csc.dll を自動探索
            string sdkRoot = @"C:\Program Files\dotnet\sdk";
            if (!Directory.Exists(sdkRoot)) 
            {
                Console.WriteLine("SDK Root not found. Make sure .NET SDK is installed.");
                return;
            }

            var cscDll = Directory.EnumerateFiles(sdkRoot, "csc.dll", SearchOption.AllDirectories)
                                  .Where(path => path.Contains("Roslyn"))
                                  .OrderByDescending(File.GetCreationTime)
                                  .FirstOrDefault();

            if (cscDll == null) throw new FileNotFoundException("csc.dll が見つかりません。");

            // 3. 標準ライブラリの参照解決
            string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
            string[] coreRefs = { "System.Runtime.dll", "System.Console.dll", "mscorlib.dll", "System.Private.CoreLib.dll" };
            string referenceArgs = string.Join(" ", coreRefs.Select(dll => $"/r:\"{Path.Combine(runtimeDir, dll)}\""));

            // 4. 引数構築
            string arguments = $"\"{cscDll}\" /noconfig /target:exe /optimize+ {referenceArgs} /out:\"{outputExe}\" \"{sourcePath}\"";

            Console.WriteLine($"Building: {outputExe}...");

            // 5. プロセス実行
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return;

            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // 6. 結果
            if (process.ExitCode == 0 && File.Exists(outputExe))
            {
                Console.WriteLine("Build Success!");
                Console.WriteLine($"Output: {outputExe}");
            }
            else
            {
                Console.WriteLine("Build Failed.");
                Console.WriteLine(stderr);
            }
        }
    }
}

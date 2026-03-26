using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

public async Task SeriousBuildAndRunAsync()
{
    // 1. ファイル選択 (GUI)
    var ofd = new Microsoft.Win32.OpenFileDialog { 
        Filter = "C# Source File (*.cs)|*.cs",
        Title = "ビルド対象のソースを選択" 
    };
    if (ofd.ShowDialog() != true) return;

    string sourcePath = ofd.FileName;
    string fileName = Path.GetFileNameWithoutExtension(sourcePath);
    string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    string outputExe = Path.Combine(downloadsDir, $"{fileName}.exe");

    // 2. 最新の .NET SDK 内にある csc.dll を自動探索
    string sdkRoot = @"C:\Program Files\dotnet\sdk";
    var cscDll = Directory.EnumerateFiles(sdkRoot, "csc.dll", SearchOption.AllDirectories)
                          .Where(path => path.Contains("Roslyn"))
                          .OrderByDescending(File.GetCreationTime)
                          .FirstOrDefault() ?? throw new FileNotFoundException("csc.dll が見つかりません。.NET SDKをインストールしてください。");

    // 3. 【重要】標準ライブラリ (.NET Runtime) のパスを取得して参照に追加
    // これをしないと System.Console すら使えない
    string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
    string[] coreRefs = { "System.Runtime.dll", "System.Console.dll", "mscorlib.dll", "System.Private.CoreLib.dll" };
    string referenceArgs = string.Join(" ", coreRefs.Select(dll => $"/r:\"{Path.Combine(runtimeDir, dll)}\""));

    // 4. csc 実行引数の構築
    // /target:exe -> 実行形式
    // /optimize+ -> 最適化有効
    // /noconfig   -> 余計な設定を読み込まない
    string arguments = $"\"{cscDll}\" /noconfig /target:exe /optimize+ {referenceArgs} /out:\"{outputExe}\" \"{sourcePath}\"";

    // 5. バックグラウンドでプロセス実行
    var startInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = arguments,
        CreateNoWindow = true,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    using var process = new Process { StartInfo = startInfo };
    process.Start();

    // 出力とエラーを非同期でキャプチャ
    string stdout = await process.StandardOutput.ReadToEndAsync();
    string stderr = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    // 6. 結果判定と実行
    if (process.ExitCode == 0 && File.Exists(outputExe))
    {
        // 成功：ダウンロードフォルダのEXEを叩く
        Process.Start(new ProcessStartInfo(outputExe) { 
            UseShellExecute = true, 
            WorkingDirectory = downloadsDir 
        });
    }
    else
    {
        string logPath = Path.Combine(downloadsDir, "build_error.log");
        File.WriteAllText(logPath, $"STDOUT:\n{stdout}\n\nSTDERR:\n{stderr}");
        throw new Exception($"ビルド失敗。詳細はログを確認してください: {logPath}");
    }
}

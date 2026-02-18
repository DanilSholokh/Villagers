using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class ProjectScriptsDumper
{
    // ✅ Налаштування
    private const bool INCLUDE_PACKAGES = false;   // true якщо хочеш включати Packages/*.cs
    private const bool INCLUDE_META = false;       // true якщо хочеш ще й *.meta (зазвичай не треба)
    private const int MAX_FILE_SIZE_KB = 512;     // захист від випадкових гігантських файлів (можеш збільшити)

    [MenuItem("Tools/Dump Project Scripts")]
    public static void DumpAllScripts()
    {
        try
        {
            // Куди зберігаємо
            var outDir = Path.Combine(Application.dataPath, "../Dumps");
            Directory.CreateDirectory(outDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outPath = Path.Combine(outDir, $"project_scripts_dump_{timestamp}.txt");

            var sb = new StringBuilder(2_000_000);

            // Заголовок
            sb.AppendLine("=== UNITY PROJECT SCRIPTS DUMP ===");
            sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Unity: {Application.unityVersion}");
            sb.AppendLine($"ProjectPath: {Directory.GetParent(Application.dataPath)?.FullName}");
            sb.AppendLine($"IncludePackages: {INCLUDE_PACKAGES}");
            sb.AppendLine($"IncludeMeta: {INCLUDE_META}");
            sb.AppendLine();

            // Джерела
            var rootProject = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(rootProject))
            {
                Debug.LogError("[Dump] Cannot resolve project root.");
                return;
            }

            // Основний список файлів
            var assetRoot = Application.dataPath; // .../Project/Assets
            var files = Directory.GetFiles(assetRoot, "*.cs", SearchOption.AllDirectories).ToList();

            if (INCLUDE_META)
                files.AddRange(Directory.GetFiles(assetRoot, "*.meta", SearchOption.AllDirectories));

            if (INCLUDE_PACKAGES)
            {
                var packagesDir = Path.Combine(rootProject, "Packages");
                if (Directory.Exists(packagesDir))
                {
                    files.AddRange(Directory.GetFiles(packagesDir, "*.cs", SearchOption.AllDirectories));
                    if (INCLUDE_META)
                        files.AddRange(Directory.GetFiles(packagesDir, "*.meta", SearchOption.AllDirectories));
                }
            }

            // Сортування стабільне
            files = files
                .Distinct()
                .OrderBy(f => f.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
                .ToList();

            sb.AppendLine($"FilesCount: {files.Count}");
            sb.AppendLine();

            int written = 0;
            int skippedLarge = 0;
            int skippedReadErr = 0;

            foreach (var absPath in files)
            {
                // Захист від дуже великих файлів
                var fi = new FileInfo(absPath);
                if (fi.Exists && fi.Length > MAX_FILE_SIZE_KB * 1024L)
                {
                    skippedLarge++;
                    continue;
                }

                string content;
                try
                {
                    // UTF8 із нормальним читанням
                    content = File.ReadAllText(absPath, Encoding.UTF8);
                }
                catch
                {
                    skippedReadErr++;
                    continue;
                }

                var relPath = MakeRelative(absPath, rootProject);
                var hash = Sha256Hex(content);

                sb.AppendLine("----- FILE BEGIN -----");
                sb.AppendLine($"PATH: {relPath.Replace('\\', '/')}");
                sb.AppendLine($"SIZE: {fi.Length} bytes");
                sb.AppendLine($"SHA256: {hash}");
                sb.AppendLine("CONTENT:");
                sb.AppendLine(content);
                if (!content.EndsWith("\n")) sb.AppendLine();
                sb.AppendLine("----- FILE END -----");
                sb.AppendLine();

                written++;
            }

            sb.AppendLine("=== DUMP SUMMARY ===");
            sb.AppendLine($"Written: {written}");
            sb.AppendLine($"SkippedLarge(>{MAX_FILE_SIZE_KB}KB): {skippedLarge}");
            sb.AppendLine($"SkippedReadErrors: {skippedReadErr}");
            sb.AppendLine("====================");

            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);

            Debug.Log($"[Dump] Saved: {outPath}");
            EditorUtility.RevealInFinder(outPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Dump] Exception: {e}");
        }
    }

    private static string MakeRelative(string fullPath, string rootPath)
    {
        fullPath = Path.GetFullPath(fullPath);
        rootPath = Path.GetFullPath(rootPath);

        if (!rootPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            rootPath += Path.DirectorySeparatorChar;

        if (fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            return fullPath.Substring(rootPath.Length);

        return fullPath;
    }

    private static string Sha256Hex(string text)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = sha.ComputeHash(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        for (int i = 0; i < hash.Length; i++)
            sb.Append(hash[i].ToString("x2"));
        return sb.ToString();
    }
}


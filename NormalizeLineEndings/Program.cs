using System.Diagnostics;
using NormalizeLineEndings.Utilities;

namespace NormalizeLineEndings;

internal class Program
{
    static void Main()
    {
        if (Debugger.IsAttached)
        {
            SafeMain();
            return;
        }

        try
        {
            SafeMain();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    static void SafeMain()
    {
        Console.WriteLine("NormalizeLineEndings - Copyright (C) 2024-" + DateTime.Now.Year + " Simon Mourier. All rights reserved.");
        Console.WriteLine();

        if (CommandLine.Current.HelpRequested)
        {
            Help();
            return;
        }

        var cmd = CommandLine.Current.GetArgument<Command>(0);
        var inputPath = CommandLine.Current.GetNullifiedArgument(1);
        var texts = CommandLine.Current.GetNullifiedArgument(1);
        var write = CommandLine.Current.GetArgument<bool>("write");

        if (cmd == Command.Defs)
        {
            foreach (var ext in Perceived.PerceivedTypes.OrderBy(t => t.Key))
            {
                Console.WriteLine(ext);
            }
            return;
        }

        if (cmd == Command.Texts)
        {
            foreach (var ext in Perceived.PerceivedTypes.Where(p => p.Value.PerceivedType == PerceivedType.Text).OrderBy(t => t.Key))
            {
                Console.WriteLine(ext);
            }
            return;
        }

        if (inputPath == null)
        {
            Help();
            return;
        }

        inputPath = Path.GetFullPath(inputPath);
        var txtExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var listOfTexts = CommandLine.Current.GetNullifiedArgument("exts");
        if (listOfTexts == null)
        {
            foreach (var text in Perceived.PerceivedTypes.Where(p => p.Value.PerceivedType == PerceivedType.Text).OrderBy(t => t.Key))
            {
                txtExtensions.Add(text.Key);
            }

            listOfTexts = CommandLine.Current.GetNullifiedArgument("pexts");
            if (listOfTexts != null)
            {
                foreach (var text in listOfTexts.Split(','))
                {
                    var nullified = text.Nullify();
                    if (nullified != null)
                    {
                        txtExtensions.Add(nullified);
                    }
                }
            }
        }
        else
        {
            foreach (var text in listOfTexts.Split(','))
            {
                var nullified = text.Nullify();
                if (nullified != null)
                {
                    txtExtensions.Add(nullified);
                }
            }
        }

        Console.WriteLine("Input      : " + inputPath);
        Console.WriteLine("Command    : " + cmd);
        Console.WriteLine("Write      : " + write);
        Console.WriteLine("Extensions : " + string.Join(' ', txtExtensions.Order()));

        if (Directory.Exists(inputPath))
        {
            foreach (var file in Directory.EnumerateFiles(inputPath, "*.*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
            {
                Normalize(cmd, txtExtensions, write, file);
            }
        }
        else
        {
            Normalize(cmd, txtExtensions, write, inputPath);
        }
    }

    private static void Normalize(Command command, HashSet<string> txtExtensions, bool write, string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (!txtExtensions.Contains(ext))
            return;

        var txt = EncodingDetector.ReadAllText(filePath, out var encoding);
        CountNewLines(txt, out var lf, out var crlf);
        Console.WriteLine($"{filePath} Encoding:{encoding.WebName} NewLines:{lf + crlf} CrLf:{crlf} Lf:{lf}");

        string? newText = null;
        switch (command)
        {
            case Command.List:
                return;

            case Command.CrLf:
                if (lf > 0)
                {
                    newText = txt.ReplaceLineEndings("\r\n");
                }
                break;

            case Command.Lf:
                if (crlf > 0)
                {
                    newText = txt.ReplaceLineEndings("\n");
                }
                break;

            default:
                throw new NotSupportedException();
        }

        if (newText == null || newText == txt)
            return;

        Console.WriteLine($"{filePath} => {command}");
        if (write)
        {
            File.WriteAllText(filePath, newText, encoding);
        }
    }

    private static void CountNewLines(string text, out int lf, out int crlf)
    {
        lf = 0;
        crlf = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var n = text.Length > i + 1 ? text[i + 1] : '\0';
            if (c == '\r')
            {
                if (n == '\n')
                {
                    crlf++;
                    i++;
                }
                else
                {
                    lf++;
                }
            }
            else if (c == '\n')
            {
                lf++;
            }
        }
    }

    private enum Command
    {
        List,
        CrLf,
        Lf,
        Defs,
        Texts,
    }

    static void Help()
    {
        Console.WriteLine("Format:");
        Console.WriteLine();
        Console.WriteLine(Assembly.GetEntryAssembly()!.GetName().Name + " <input path> command [options]");
        Console.WriteLine();
        Console.WriteLine("Description:");
        Console.WriteLine("    This tool updates all text files line endings.");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("    List             List line endings.");
        Console.WriteLine("    CrLf             Set all line endings to CR (13) + LF (10), i.e: \\r\\n.");
        Console.WriteLine("    Lf               Set all line endings to LF (10), i.e: \\n.");
        Console.WriteLine("    Defs             Shows the default list of extensions mappings.");
        Console.WriteLine("    Texts            Shows the default list of extensions considered as text files.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("    /Write           By default the tool doesn't change any file. Set this flag to write changed files.");
        Console.WriteLine("    /Exts:<list>     A comma-separated list of of text files extensions (don't use the default one).");
        Console.WriteLine("    /PExts:<list>    A comma-separated list of of text files extensions (to add to the default one).");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine();
        Console.WriteLine("    " + Assembly.GetEntryAssembly()!.GetName().Name + " c:\\mypath CRLF");
        Console.WriteLine();
        Console.WriteLine("    Set all line endings of files in the c:\\mypath directory to CR+LF");
        Console.WriteLine();
    }
}

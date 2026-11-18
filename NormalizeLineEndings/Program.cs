using System.Diagnostics;
using NormalizeLineEndings.Properties;
using NormalizeLineEndings.Utilities;

namespace NormalizeLineEndings;

internal class Program
{
    private const string _lfArgument = "lf";

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
        Console.WriteLine(Resources.Copyright, DateTime.Now.Year);
        Console.WriteLine();

        if (CommandLine.Current.HelpRequested)
        {
            Help();
            return;
        }

        // the command is optional, a first argument that names none is the input path of the default Test command,
        // so nothing is written unless asked for.
        var first = CommandLine.Current.GetNullifiedArgument(0);
        var hasCommand = first != null && char.IsLetter(first[0]) && Enum.TryParse<Command>(first, true, out _);
        var cmd = hasCommand ? CommandLine.Current.GetArgument<Command>(0) : Command.Test;
        var inputPath = CommandLine.Current.GetNullifiedArgument(hasCommand ? 1 : 0);
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
        if (cmd is Command.Git or Command.Test)
        {
            var lfPatterns = (CommandLine.Current.GetNullifiedArgument(_lfArgument)?.Split(',') ?? []).Select(p => p.Nullify()).OfType<string>().ToList();
            GitRepositoryNormalizer.Run(inputPath, cmd == Command.Git, lfPatterns);
            return;
        }

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

        Console.WriteLine(Resources.RunInput, inputPath);
        Console.WriteLine(Resources.RunCommand, cmd);
        Console.WriteLine(Resources.RunWrite, write);
        Console.WriteLine(Resources.RunExtensions, string.Join(' ', txtExtensions.Order()));

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
        Console.WriteLine(Resources.FileLineEndings, filePath, encoding.WebName, lf + crlf, crlf, lf);

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

        Console.WriteLine(Resources.FileChanged, filePath, command);
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
        Git,
        Test,
    }

    static void Help()
    {
        var name = Assembly.GetEntryAssembly()!.GetName().Name;
        Console.WriteLine(Resources.HelpFormat);
        Console.WriteLine();
        Console.WriteLine(Resources.HelpFormatLine, name);
        Console.WriteLine();
        Console.WriteLine(Resources.HelpDescription);
        Console.WriteLine(Resources.HelpDescriptionText);
        Console.WriteLine();
        Console.WriteLine(Resources.HelpCommands);
        Console.WriteLine(Resources.HelpListCommand);
        Console.WriteLine(Resources.HelpCrLfCommand);
        Console.WriteLine(Resources.HelpLfCommand);
        Console.WriteLine(Resources.HelpDefsCommand);
        Console.WriteLine(Resources.HelpTextsCommand);
        Console.WriteLine(Resources.HelpGitCommand);
        Console.WriteLine(Resources.HelpTestCommand);
        Console.WriteLine();
        Console.WriteLine(Resources.HelpOptions);
        Console.WriteLine(Resources.HelpWriteOption);
        Console.WriteLine(Resources.HelpExtsOption);
        Console.WriteLine(Resources.HelpPExtsOption);
        Console.WriteLine(Resources.HelpLfOption);
        Console.WriteLine();
        Console.WriteLine(Resources.HelpExamples);
        Console.WriteLine();
        Console.WriteLine(Resources.HelpTestExampleLine, name);
        Console.WriteLine();
        Console.WriteLine(Resources.HelpTestExample);
        Console.WriteLine();
        Console.WriteLine(Resources.HelpGitExampleLine, name);
        Console.WriteLine();
        Console.WriteLine(Resources.HelpGitExample);
        Console.WriteLine();
        Console.WriteLine(Resources.HelpCrLfExampleLine, name);
        Console.WriteLine();
        Console.WriteLine(Resources.HelpCrLfExample);
        Console.WriteLine();
    }
}

using System.Diagnostics;
using System.IO.Enumeration;
using NormalizeLineEndings.Properties;
using NormalizeLineEndings.Utilities;

namespace NormalizeLineEndings;

// makes a git repository store every text file with LF and write it out with CRLF, whatever core.autocrlf says:
// * a .gitattributes is added when the repository has none, one it already has is used as is,
// * what git stores is renormalized and staged, except the files with unstaged changes, staged when their owner adds them,
// * the files on disk get the line endings their attributes ask for, converted as bytes so encodings and BOMs stay.
// Nothing is committed.
internal sealed class GitRepositoryNormalizer(string repositoryPath, bool write, IReadOnlyList<string> lfPatterns)
{
    private const string _gitExecutable = "git";
    private const string _gitDirectoryName = ".git";
    private const string _attributesFileName = ".gitattributes";
    private const string _indexLockFileName = "index.lock";
    private const string _textAttribute = "-text";
    private const string _noneInfo = "none";
    private const string _crlfInfo = "crlf";
    private const string _mixedInfo = "mixed";
    private const string _lfInfo = "lf";
    private const string _eolCrlfAttribute = "eol=crlf";
    private const string _eolLfAttribute = "eol=lf";
    private const string _attributePrefix = "attr/";
    private const string _indexPrefix = "i/";
    private const string _workingTreePrefix = "w/";
    private const string _defaultRule = "* text=auto eol=crlf";
    private const string _lfRuleFormat = "{0} text eol=lf";
    private const string _defaultAttributes = "text=auto eol=crlf";
    private const string _lfAttributes = "text eol=lf";
    private const string _textDeclared = "text";
    private const string _newLine = "\r\n";
    private const char _recordSeparator = '\0';
    private const char _pathSeparator = '\t';
    private const byte _cr = (byte)'\r';
    private const byte _lf = (byte)'\n';
    private const byte _nul = 0;

    // writes the repositories found at the given path, the path itself when it is one, else its direct subfolders that are.
    public static void Run(string path, bool write, IReadOnlyList<string> lfPatterns)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(lfPatterns);
        if (!write)
        {
            Console.WriteLine(Resources.TestMode);
        }

        var repositories = IsRepository(path) ? [path] : Directory.EnumerateDirectories(path).Where(IsRepository).Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (repositories.Count == 0)
        {
            Console.WriteLine(Resources.NoRepository, path);
            return;
        }

        foreach (var repository in repositories)
        {
            try
            {
                new GitRepositoryNormalizer(repository, write, lfPatterns).Normalize();
            }
            catch (Exception ex)
            {
                Console.WriteLine(Resources.RepositoryFailed, repository, ex.Message);
            }
        }
    }

    private static bool IsRepository(string path)
    {
        var git = Path.Combine(path, _gitDirectoryName);
        return Directory.Exists(git) || File.Exists(git);
    }

    private void Normalize()
    {
        Console.WriteLine();
        Console.WriteLine(Resources.RepositoryTitle, repositoryPath);
        if (File.Exists(Path.Combine(repositoryPath, _gitDirectoryName, _indexLockFileName)))
        {
            Console.WriteLine(Resources.RepositoryLocked);
            return;
        }

        var attributesFilePath = Path.Combine(repositoryPath, _attributesFileName);
        var addAttributes = !File.Exists(attributesFilePath);
        if (addAttributes)
        {
            Console.WriteLine(lfPatterns.Count == 0 ? Resources.AttributesAdded : Resources.AttributesAddedLf, _defaultRule, string.Join(' ', lfPatterns));
            if (write)
            {
                WriteAttributes(attributesFilePath);
            }
        }
        else
        {
            Console.WriteLine(Resources.AttributesKept);
        }

        // a dry run has no new .gitattributes on disk, so its rules are applied to the listing here instead.
        var files = ListFiles(addAttributes && !write);

        // a file that differs from git's copy by its line endings only is not someone's work in progress.
        var unstaged = Git(null, "diff", "--ignore-cr-at-eol", "--name-only", "-z").Split(_recordSeparator, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var toStore = files.Where(f => f.IsText && f.Index is _crlfInfo or _mixedInfo).ToList();
        var restaged = toStore.Count(f => !unstaged.Contains(f.Path));
        Console.WriteLine(Resources.StoredFiles, toStore.Count, restaged, toStore.Count - restaged);

        var converted = 0;
        foreach (var file in files)
        {
            if (file.Wanted is not { } wanted || file.WorkingTree == wanted || file.WorkingTree == _noneInfo)
                continue;

            Console.WriteLine(Resources.FileConverted, file.Path, file.WorkingTree, wanted);
            if (write && !ConvertFile(Path.Combine(repositoryPath, file.Path), wanted == _crlfInfo))
                continue;

            converted++;
        }

        Console.WriteLine(Resources.DiskFiles, converted);
        if (!write)
            return;

        if (addAttributes)
        {
            Git(null, "add", "--", _attributesFileName);
        }

        // every clean text file goes through, also those already stored with LF,
        // since adding them is what tells git a file converted on disk above still matches its copy.
        var toRenormalize = files.Where(f => f.IsText && !unstaged.Contains(f.Path)).Select(f => f.Path).ToList();
        if (toRenormalize.Count > 0)
        {
            Git(string.Join(_recordSeparator, toRenormalize), "add", "--renormalize", "--pathspec-from-file=-", "--pathspec-file-nul");
        }
    }

    private void WriteAttributes(string filePath)
    {
        var text = new StringBuilder();
        text.Append(Resources.AttributesComment).Append(_newLine);
        text.Append(_defaultRule).Append(_newLine);
        foreach (var pattern in lfPatterns)
        {
            text.AppendFormat(CultureInfo.InvariantCulture, _lfRuleFormat, pattern).Append(_newLine);
        }

        File.WriteAllText(filePath, text.ToString());
    }

    private List<GitFile> ListFiles(bool applyDefaultRules)
    {
        var files = new List<GitFile>();
        foreach (var record in Git(null, "ls-files", "--eol", "-z").Split(_recordSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = record.IndexOf(_pathSeparator);
            if (tab < 0)
                continue;

            var tokens = record[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
                continue;

            var attributes = string.Join(' ', tokens.Skip(2));
            if (attributes.StartsWith(_attributePrefix, StringComparison.Ordinal))
            {
                attributes = attributes[_attributePrefix.Length..];
            }

            var path = record[(tab + 1)..];
            if (applyDefaultRules && attributes.Length == 0)
            {
                attributes = IsLfPath(path) ? _lfAttributes : _defaultAttributes;
            }

            files.Add(new GitFile(path, strip(tokens[0], _indexPrefix), strip(tokens[1], _workingTreePrefix), attributes));
        }

        return files;

        static string strip(string token, string prefix) => token.StartsWith(prefix, StringComparison.Ordinal) ? token[prefix.Length..] : token;
    }

    private bool IsLfPath(string path)
    {
        var name = Path.GetFileName(path);
        return lfPatterns.Any(p => FileSystemName.MatchesSimpleExpression(p, name) || FileSystemName.MatchesSimpleExpression(p, path));
    }

    // false when the file is gone or holds a NUL, a binary git did not recognize.
    private static bool ConvertFile(string filePath, bool crlf)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine(Resources.FileFailed, filePath, ex.Message);
            return false;
        }

        if (Array.IndexOf(bytes, _nul) >= 0)
            return false;

        var output = new List<byte>(bytes.Length + bytes.Length / 32);
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            if (b == _lf)
            {
                var afterCr = output.Count > 0 && output[^1] == _cr;
                if (crlf && !afterCr)
                {
                    output.Add(_cr);
                }
                else if (!crlf && afterCr)
                {
                    output.RemoveAt(output.Count - 1);
                }
            }

            output.Add(b);
        }

        File.WriteAllBytes(filePath, [.. output]);
        return true;
    }

    private string Git(string? input, params string[] arguments)
    {
        var start = new ProcessStartInfo(_gitExecutable)
        {
            WorkingDirectory = repositoryPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input != null,
            StandardOutputEncoding = Encoding.UTF8,
        };

        // paths come back as they are, not quoted with octal escapes.
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("core.quotepath=off");
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException();
        var error = new StringBuilder();
        process.ErrorDataReceived += (s, e) => error.AppendLine(e.Data);
        process.BeginErrorReadLine();
        if (input != null)
        {
            process.StandardInput.Write(input);
            process.StandardInput.Close();
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.GitFailed, string.Join(' ', arguments), error.ToString().Trim()));

        return output;
    }

    private sealed record GitFile(string Path, string Index, string WorkingTree, string Attributes)
    {
        // text for git's content check and declared text by the attributes, which an existing .gitattributes may not do.
        public bool IsText => Index != _textAttribute && !Attributes.Contains(_textAttribute, StringComparison.Ordinal) &&
            Attributes.Contains(_textDeclared, StringComparison.Ordinal);

        // the line endings the attributes ask for on disk, null when they ask for none.
        public string? Wanted
        {
            get
            {
                if (!IsText)
                    return null;

                if (Attributes.Contains(_eolCrlfAttribute, StringComparison.Ordinal))
                    return _crlfInfo;

                if (Attributes.Contains(_eolLfAttribute, StringComparison.Ordinal))
                    return _lfInfo;

                return null;
            }
        }
    }
}

namespace NormalizeLineEndings.Utilities;

public sealed partial class Perceived
{
    public static IReadOnlyDictionary<string, Perceived> PerceivedTypes => _perceiveds.Value;
    private static readonly Lazy<ConcurrentDictionary<string, Perceived>> _perceiveds = new(LoadPerceiveds);

    private Perceived(string extension)
    {
        Extension = extension;
    }

    public string Extension { get; }
    public PerceivedType PerceivedType { get; private set; }
    public PerceivedTypeSource PerceivedTypeSource { get; set; }
    public override string ToString() => Extension + ":" + PerceivedType + " (" + PerceivedTypeSource + ")";

    public static Perceived SetPerceived(string extension, PerceivedType type)
    {
        ArgumentNullException.ThrowIfNull(extension);
        var perceived = new Perceived(extension)
        {
            PerceivedType = type,
            PerceivedTypeSource = PerceivedTypeSource.HardCoded
        };

        _perceiveds.Value[perceived.Extension] = perceived;
        return perceived;
    }

    [LibraryImport("shlwapi")]
    private static partial int AssocGetPerceivedType([MarshalAs(UnmanagedType.LPWStr)] string pszExt, ref PerceivedType ptype, ref PerceivedTypeSource pflag, ref nint ppszType);

    private static ConcurrentDictionary<string, Perceived> LoadPerceiveds()
    {
        var dic = new ConcurrentDictionary<string, Perceived>(StringComparer.OrdinalIgnoreCase);

        foreach (var extension in Registry.ClassesRoot.GetSubKeyNames())
        {
            if (!extension.StartsWith('.'))
                continue;

            using var key = Registry.ClassesRoot.OpenSubKey(extension);
            if (key == null)
                continue;

            var ptype = new Perceived(extension);
            var ct = Conversions.ChangeType<string>(key.GetValue("PerceivedType"));
            if (ct != null)
            {
                ptype.PerceivedType = Conversions.ChangeType(ct, PerceivedType.Custom);
                ptype.PerceivedTypeSource = PerceivedTypeSource.SoftCoded;
            }
            else
            {
                ct = Conversions.ChangeType<string>(key.GetValue("Content Type"));
                if (ct != null)
                {
                    var pos = ct.IndexOf('/');
                    if (pos > 0)
                    {
                        ptype.PerceivedType = Conversions.ChangeType(ct[..pos], PerceivedType.Custom);
                        ptype.PerceivedTypeSource = PerceivedTypeSource.Mime;
                    }
                }
            }

            if (ptype.PerceivedType == PerceivedType.Unknown)
            {
                var text = nint.Zero;
                var type = PerceivedType.Unknown;
                var source = PerceivedTypeSource.Undefined;
                var hr = AssocGetPerceivedType(extension, ref type, ref source, ref text);
                if (hr == 0)
                {
                    ptype.PerceivedType = type;
                    ptype.PerceivedTypeSource = source;
                }
            }

            if (ptype.PerceivedType != PerceivedType.Unknown)
            {
                if (ptype.PerceivedType == PerceivedType.Unspecified || ptype.PerceivedType == PerceivedType.Unknown)
                    continue;

                if (dic.ContainsKey(ptype.Extension))
                    continue;

                dic[ptype.Extension] = ptype;
            }
            key.Close();
        }

        LoadWellKnown(dic);
        return dic;
    }

    public static Perceived? GetPerceivedType(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var extension = Path.GetExtension(fileName) ?? throw new ArgumentException(null, nameof(fileName));
        _perceiveds.Value.TryGetValue(extension, out var ptype);
        return ptype;
    }

    private static void LoadWellKnown(ConcurrentDictionary<string, Perceived> dic)
    {
        void setPerceived(string extension, PerceivedType type)
        {
            var perceived = new Perceived(extension)
            {
                PerceivedType = type,
                PerceivedTypeSource = PerceivedTypeSource.HardCoded
            };

            dic[perceived.Extension] = perceived;
        }
        setPerceived(".appxmanifest", PerceivedType.Text);
        setPerceived(".asax", PerceivedType.Text);
        setPerceived(".ascx", PerceivedType.Text);
        setPerceived(".ashx", PerceivedType.Text);
        setPerceived(".asmx", PerceivedType.Text);
        setPerceived(".asp", PerceivedType.Text);
        setPerceived(".axml", PerceivedType.Text);
        setPerceived(".bas", PerceivedType.Text);
        setPerceived(".bat", PerceivedType.Text);
        setPerceived(".btproj", PerceivedType.Text);
        setPerceived(".cbl", PerceivedType.Text);
        setPerceived(".cfg", PerceivedType.Text);
        setPerceived(".class", PerceivedType.Text);
        setPerceived(".cmd", PerceivedType.Text);
        setPerceived(".cob", PerceivedType.Text);
        setPerceived(".c", PerceivedType.Text);
        setPerceived(".cpp", PerceivedType.Text);
        setPerceived(".cs", PerceivedType.Text);
        setPerceived(".cshtml", PerceivedType.Text);
        setPerceived(".css", PerceivedType.Text);
        setPerceived(".cxx", PerceivedType.Text);
        setPerceived(".config", PerceivedType.Text);
        setPerceived(".cbproj", PerceivedType.Text);
        setPerceived(".crproj", PerceivedType.Text);
        setPerceived(".csproj", PerceivedType.Text);
        setPerceived(".dproj", PerceivedType.Text);
        setPerceived(".dbproj", PerceivedType.Text);
        setPerceived(".dbschema", PerceivedType.Text);
        setPerceived(".def", PerceivedType.Text);
        setPerceived(".disco", PerceivedType.Text);
        setPerceived(".deploymanifest", PerceivedType.Text);
        setPerceived(".diagram", PerceivedType.Text);
        setPerceived(".dotsettings", PerceivedType.Text);
        setPerceived(".editorconfig", PerceivedType.Text);
        setPerceived(".edmx", PerceivedType.Text);
        setPerceived(".eml", PerceivedType.Text);
        setPerceived(".frm", PerceivedType.Text);
        setPerceived(".go", PerceivedType.Text);
        setPerceived(".h", PerceivedType.Text);
        setPerceived(".hpp", PerceivedType.Text);
        setPerceived(".hxx", PerceivedType.Text);
        setPerceived(".html", PerceivedType.Text);
        setPerceived(".iqy", PerceivedType.Text);
        setPerceived(".inf", PerceivedType.Text);
        setPerceived(".ini", PerceivedType.Text);
        setPerceived(".isl", PerceivedType.Text);
        setPerceived(".isproj", PerceivedType.Text);
        setPerceived(".java", PerceivedType.Text);
        setPerceived(".js", PerceivedType.Text);
        setPerceived(".json", PerceivedType.Text);
        setPerceived(".l4g", PerceivedType.Text);
        setPerceived(".log", PerceivedType.Text);
        setPerceived(".licx", PerceivedType.Text);
        setPerceived(".master", PerceivedType.Text);
        setPerceived(".manifest", PerceivedType.Text);
        setPerceived(".md", PerceivedType.Text);
        setPerceived(".nuspec", PerceivedType.Text);
        setPerceived(".pas", PerceivedType.Text);
        setPerceived(".package", PerceivedType.Text);
        setPerceived(".pbxproj", PerceivedType.Text);
        setPerceived(".plist", PerceivedType.Text);
        setPerceived(".props", PerceivedType.Text);
        setPerceived(".ps1", PerceivedType.Text);
        setPerceived(".psd1", PerceivedType.Text);
        setPerceived(".psm1", PerceivedType.Text);
        setPerceived(".rc", PerceivedType.Text);
        setPerceived(".rdl", PerceivedType.Text);
        setPerceived(".readme", PerceivedType.Text);
        setPerceived(".reg", PerceivedType.Text);
        setPerceived(".resx", PerceivedType.Text);
        setPerceived(".rs", PerceivedType.Text);
        setPerceived(".rtf", PerceivedType.Text);
        setPerceived(".rzt", PerceivedType.Text);
        setPerceived(".schemaview", PerceivedType.Text);
        setPerceived(".sh", PerceivedType.Text);
        setPerceived(".sitemap", PerceivedType.Text);
        setPerceived(".sln", PerceivedType.Text);
        setPerceived(".spdata", PerceivedType.Text);
        setPerceived(".sql", PerceivedType.Text);
        setPerceived(".sqlproj", PerceivedType.Text);
        setPerceived(".sqlcmdvars", PerceivedType.Text);
        setPerceived(".sqldeployment", PerceivedType.Text);
        setPerceived(".sqlsettings", PerceivedType.Text);
        setPerceived(".snippet", PerceivedType.Text);
        setPerceived(".storyboard", PerceivedType.Text);
        setPerceived(".svc", PerceivedType.Text);
        setPerceived(".svcinfo", PerceivedType.Text);
        setPerceived(".svcmap", PerceivedType.Text);
        setPerceived(".targets", PerceivedType.Text);
        setPerceived(".tcl", PerceivedType.Text);
        setPerceived(".tpl", PerceivedType.Text);
        setPerceived(".tplxaml", PerceivedType.Text);
        setPerceived(".txt", PerceivedType.Text);
        setPerceived(".vb", PerceivedType.Text);
        setPerceived(".vbhtml", PerceivedType.Text);
        setPerceived(".vbp", PerceivedType.Text);
        setPerceived(".vbproj", PerceivedType.Text);
        setPerceived(".vbs", PerceivedType.Text);
        setPerceived(".vcproj", PerceivedType.Text);
        setPerceived(".vcxproj", PerceivedType.Text);
        setPerceived(".vdproj", PerceivedType.Text);
        setPerceived(".webpart", PerceivedType.Text);
        setPerceived(".wsdl", PerceivedType.Text);
        setPerceived(".wxi", PerceivedType.Text);
        setPerceived(".wxl", PerceivedType.Text);
        setPerceived(".wxs", PerceivedType.Text);
        setPerceived(".wixlib", PerceivedType.Text);
        setPerceived(".vixproj", PerceivedType.Text);
        setPerceived(".xaml", PerceivedType.Text);
        setPerceived(".xsd", PerceivedType.Text);
        setPerceived(".xsl", PerceivedType.Text);
        setPerceived(".xslt", PerceivedType.Text);

        setPerceived(".dll", PerceivedType.Application);
        setPerceived(".exe", PerceivedType.Application);

        setPerceived(".pdb", PerceivedType.Custom);

        setPerceived(".dacpac", PerceivedType.Compressed);
        setPerceived(".nupkg", PerceivedType.Compressed);
        setPerceived(".rar", PerceivedType.Compressed);
        setPerceived(".tar", PerceivedType.Compressed);
        setPerceived(".7z", PerceivedType.Compressed);
        setPerceived(".docx", PerceivedType.Compressed);
        setPerceived(".xlsx", PerceivedType.Compressed);
        setPerceived(".pptx", PerceivedType.Compressed);
    }
}

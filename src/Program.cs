using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;

var licenses = await FileHandler.LoadLicensesAsync();
var prompt = new SelectionPrompt<License>()
    .Title("Select a License")
    .PageSize(10)
    .MoreChoicesText("[grey]Move up and down to reveal more options[/]")
    .UseConverter(x => $"{x.Name} [[{x.Spdx}]]")
    .AddChoices(licenses);

var license = AnsiConsole.Prompt(prompt);
var template = await FileHandler.LoadTemplateAsync(license);

if (license.HasAttribution)
{
    AnsiConsole.MarkupLine($"The [green]{license.Name}[/] requires a [blue]year[/] and [blue]author(s)[/].");

    var name = AnsiConsole.Ask<string>("Author(s): ");
    var year = AnsiConsole.Ask<int>("Year: ");

    template = Replacer.Replace(template, name, year);
}

var path = Path.Combine(Environment.CurrentDirectory, "LICENSE");
await File.WriteAllTextAsync(path, template);

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine($"Created LICENSE file with the [green]{license.Spdx}[/] license");

readonly record struct License(string Spdx, string Name, string? Version, bool HasAttribution, bool EachFile);

class FileHandler
{
    private static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();

    private static readonly JsonSerializerOptions _options = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async ValueTask<IList<License>> LoadLicensesAsync()
    {
        var json = await LoadEmbeddedFileAsync("licenses.json");
        var licenses = JsonSerializer.Deserialize<IEnumerable<License>>(json, _options) ?? Enumerable.Empty<License>();

        return licenses.ToList();
    }

    public static async ValueTask<string> LoadTemplateAsync(License license)
    {
        var filename = $"{license.Spdx.ToLowerInvariant()}.txt";
        return await LoadEmbeddedFileAsync(filename);
    }

    private static async ValueTask<string> LoadEmbeddedFileAsync(string filename)
    {
        var assemblyName = ExecutingAssembly.GetName().Name;
        using var stream = ExecutingAssembly.GetManifestResourceStream($"{assemblyName}.{filename}");
        if (stream is null)
        {
            throw new FileNotFoundException($"File {filename} not found", filename);
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        return content;
    }
}

static class Replacer
{
    private static readonly Regex _yearRegex = new(@"#{year}#", RegexOptions.Compiled);
    private static readonly Regex _nameRegex = new(@"#{name}#", RegexOptions.Compiled);

    public static string Replace(string template, string name, int year)
    {
        template = _yearRegex.Replace(template, year.ToString(CultureInfo.InvariantCulture));
        template = _nameRegex.Replace(template, name);

        return template;
    }
}
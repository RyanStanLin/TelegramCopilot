using System.Net.Http.Json;
using System.Text.Json;

if (args.Length == 0 || args[0] is "help" or "--help")
{
    Console.WriteLine("""
        TelegramCopilot CLI
        --url URL defaults to TELEGRAMCOPILOT_API_URL or http://127.0.0.1:5080
        list KIND [--search TEXT --page 1 --pageSize 100]
        get KIND ID
        create KIND --file settings.json
        update KIND ID --file settings.json
        delete KIND ID --revision N
        status ACCOUNT_ID
        login ACCOUNT_ID --type code|password|email|emailCode [--stdin]
        timeline TARGET_ID --from ISO --to ISO [--state Online --page 1 --pageSize 100]
        statistics TARGET_ID --from ISO --to ISO [--period day|week|month|year --timeZone Asia/Taipei]
        export TARGET_ID --from ISO --to ISO --output records.csv
        events --from ISO --to ISO [--targetId ID --type Offline --page 1]
        deliveries [--channel Telegram|Bark --resourceId ID --page 1]
        health
        request METHOD /api/path [--file body.json]

        KIND: accounts, targets, schedules, destinations, rules, settings
        Create/update body: {"name":"...","enabled":true,"settings":{...},"revision":1}
        Updates require current revision. Secret *** values preserve existing secrets.
        Login secrets are read interactively without echo, or from stdin with --stdin.
        """);
    return 0;
}
try
{
    var options = new Dictionary<string, string>();
    var positional = new List<string>();
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal)) { positional.Add(args[i]); continue; }
        var key = args[i][2..];
        options[key] = key == "stdin" ? "true" : i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {key}");
    }
    var url = options.GetValueOrDefault("url") ?? Environment.GetEnvironmentVariable("TELEGRAMCOPILOT_API_URL") ?? "http://127.0.0.1:5080";
    using var client = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(90) };
    var command = positional[0];
    string Arg(int index) => positional.Count > index ? positional[index] : throw new ArgumentException("Missing command argument. Run help.");
    var method = HttpMethod.Get;
    var path = command switch
    {
        "list" or "create" => $"/api/{Arg(1)}",
        "get" or "update" or "delete" => $"/api/{Arg(1)}/{Arg(2)}",
        "status" or "login" => $"/api/accounts/{Arg(1)}/{(command == "status" ? "status" : "login")}",
        "timeline" or "statistics" => $"/api/targets/{Arg(1)}/{command}",
        "export" => $"/api/targets/{Arg(1)}/export.csv",
        "events" or "deliveries" or "health" => $"/api/{command}",
        "request" => Arg(2),
        _ => throw new ArgumentException("Unknown command. Run help.")
    };
    object? body = null;
    if (command is "create" or "update" or "request")
    {
        method = command == "create" ? HttpMethod.Post : command == "update" ? HttpMethod.Put : new HttpMethod(Arg(1));
        if (options.TryGetValue("file", out var file)) body = JsonDocument.Parse(await File.ReadAllTextAsync(file)).RootElement.Clone();
        else if (command is "create" or "update") throw new ArgumentException("--file is required.");
    }
    if (command == "delete")
    {
        method = HttpMethod.Delete;
        if (!options.ContainsKey("revision")) throw new ArgumentException("--revision is required.");
    }
    if (command == "login")
    {
        method = HttpMethod.Post;
        var type = options.GetValueOrDefault("type") ?? throw new ArgumentException("--type is required.");
        string secret;
        if (options.ContainsKey("stdin")) secret = (await Console.In.ReadToEndAsync()).TrimEnd('\r', '\n');
        else
        {
            Console.Error.Write($"{type}: ");
            var chars = new List<char>();
            ConsoleKeyInfo key;
            while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
            {
                if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); }
                else if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
            }
            Console.Error.WriteLine(); secret = new string(chars.ToArray());
        }
        body = new { type, value = secret };
    }
    var query = options.Where(x => x.Key is not ("url" or "file" or "output" or "stdin") && !(command == "login" && x.Key == "type"));
    var queryText = string.Join('&', query.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
    if (queryText.Length > 0) path += (path.Contains('?') ? "&" : "?") + queryText;
    using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
    using var response = await client.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode) { Console.Error.WriteLine($"HTTP {(int)response.StatusCode}: {content}"); return 1; }
    if (command == "export")
    {
        var output = options.GetValueOrDefault("output") ?? throw new ArgumentException("--output is required.");
        await File.WriteAllBytesAsync(output, await response.Content.ReadAsByteArrayAsync());
        Console.WriteLine(Path.GetFullPath(output));
    }
    else if (content.Length > 0) Console.WriteLine(JsonSerializer.Serialize(JsonDocument.Parse(content), new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or HttpRequestException or JsonException or IOException or TaskCanceledException)
{
    Console.Error.WriteLine(ex.Message); return 1;
}

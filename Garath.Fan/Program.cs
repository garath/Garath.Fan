using System.Net;
using System.Security.Cryptography.X509Certificates;

Console.WriteLine("Hello, World!");

using CancellationTokenSource stoppingTokenSource = new();
CancellationToken stoppingToken = stoppingTokenSource.Token;

Console.CancelKeyPress += (_, args) => {
    Console.WriteLine("Cancelling...");
    stoppingTokenSource.Cancel();
    args.Cancel = true;
};

Uri[] targetUris = args
    .Select(arg => new Uri(arg))
    .ToArray();

Queue<Uri> redirectUris = new(5);
Dictionary<string, Dictionary<string, int>> uriStats = [];

using HttpClient httpClient = new(new SocketsHttpHandler() {
    AllowAutoRedirect = false,
    SslOptions = {
        CertificateRevocationCheckMode = X509RevocationMode.Offline
    }
});

IEnumerable<Uri> enumerateUris(CancellationToken cancellationToken) {
    int index = 0;

    do
    {
        if (redirectUris.Count != 0)
        {
            yield return redirectUris.Dequeue();
        }
        else
        {
            if (index >= targetUris.Length)
            {
                index = 0;
            }

            yield return targetUris[index++];
        }
    } while (cancellationToken.IsCancellationRequested == false);

    yield break;
}

foreach(Uri uri in enumerateUris(stoppingToken))
{
    try
    {
        await Task.Delay(1000, stoppingToken);
    }
    catch (TaskCanceledException)
    {
        break;
    }

    if (stoppingToken.IsCancellationRequested)
    {
        break;
    }

    if (uriStats.ContainsKey(uri.Host) == false)
    {
        uriStats[uri.Host] = [];
    }

    using HttpRequestMessage request = new(HttpMethod.Get, uri);

    try 
    {
        Console.WriteLine($"Requesting {uri}");
        using HttpResponseMessage response = await httpClient.SendAsync(request);

        string resultsKey = $"{((int)response.StatusCode).ToString()} {response.StatusCode}";
        if (uriStats[uri.Host].ContainsKey(resultsKey) == false)
        {
            uriStats[uri.Host][resultsKey] = 0;
        }

        uriStats[uri.Host][resultsKey] += 1;

        if (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther && response.Headers.Location is not null)
        {
            Console.WriteLine($"Response status code: {response.StatusCode}");
            Console.WriteLine($"Redirected to {response.Headers.Location}");
            redirectUris.Enqueue(response.Headers.Location);
            continue;
        }

        Console.WriteLine($"Response status code: {response.StatusCode}");
        await response.Content.ReadAsStream().CopyToAsync(Stream.Null);
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"Caught exception: {ex.Message}");
        Console.WriteLine($"Request error: {ex.HttpRequestError}");

        string resultsKey = $"{ex.GetType().Name}";
        if (uriStats[uri.Host].ContainsKey(resultsKey) == false)
        {
            uriStats[uri.Host][resultsKey] = 0;
        }

        uriStats[uri.Host][resultsKey] += 1;
    }
}

Console.WriteLine();
Console.WriteLine("================");
Console.WriteLine("Results:");
foreach (KeyValuePair<string, Dictionary<string, int>> uriStat in uriStats)
{
    Console.WriteLine($"  Host: {uriStat.Key}");
    foreach (KeyValuePair<string, int> stat in uriStat.Value)
    {
        Console.WriteLine($"    {stat.Key}: {stat.Value}");
    }
}

Console.WriteLine("Goodbye!");
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Forge;
using Prometheus;
using Robust.Shared.Configuration;

namespace Content.Server._Forge.TTS;

// ReSharper disable once InconsistentNaming
public sealed class TTSManager
{
    private static readonly Histogram RequestTimings = Metrics.CreateHistogram(
        "tts_req_timings",
        "Timings of TTS API requests",
        new HistogramConfiguration()
        {
            LabelNames = new[] { "type" },
            Buckets = Histogram.ExponentialBuckets(.1, 1.5, 10),
        });

    private static readonly Counter WantedCount = Metrics.CreateCounter(
        "tts_wanted_count",
        "Amount of wanted TTS audio.");

    private static readonly Counter ReusedCount = Metrics.CreateCounter(
        "tts_reused_count",
        "Amount of reused TTS audio from cache.");

    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly HttpClient _httpClient = new();

    private ISawmill _sawmill = default!;
    private readonly Dictionary<string, byte[]> _cache = new();
    private readonly List<string> _cacheKeysSeq = new();
    private readonly Dictionary<string, Task<byte[]?>> _pendingRequests = new();
    private readonly object _lock = new();
    private int _maxCachedCount = 200;
    private string _apiUrl = string.Empty;
    private string _apiToken = string.Empty;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");
        _cfg.OnValueChanged(ForgeVars.TTSMaxCache, val =>
        {
            _maxCachedCount = val;
            ResetCache();
        }, true);
        _cfg.OnValueChanged(ForgeVars.TTSApiUrl, v => _apiUrl = v, true);
        _cfg.OnValueChanged(ForgeVars.TTSApiToken, v => _apiToken = v, true);
    }

    /// <summary>
    /// Generates audio with passed text by API
    /// </summary>
    /// <param name="speaker">Identifier of speaker</param>
    /// <param name="text">SSML formatted text</param>
    /// <returns>OGG audio bytes or null if failed</returns>
    public Task<byte[]?> ConvertTextToSpeech(string speaker, string text)
    {
        WantedCount.Inc();
        var cacheKey = GenerateCacheKey(speaker, text);

        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var data))
            {
                ReusedCount.Inc();
                _sawmill.Verbose($"Use cached sound for '{text}' speech by '{speaker}' speaker");
                return Task.FromResult<byte[]?>(data);
            }

            if (_pendingRequests.TryGetValue(cacheKey, out var pending))
            {
                ReusedCount.Inc();
                _sawmill.Verbose($"Reuse in-flight request for '{text}' speech by '{speaker}' speaker");
                return pending;
            }

            var task = ConvertTextToSpeechInternal(speaker, text, cacheKey);
            _pendingRequests[cacheKey] = task;
            return task;
        }
    }

    private async Task<byte[]?> ConvertTextToSpeechInternal(string speaker, string text, string cacheKey)
    {
        _sawmill.Verbose($"Generate new audio for '{text}' speech by '{speaker}' speaker");

        var reqTime = DateTime.UtcNow;

        try
        {
            var timeout = _cfg.GetCVar(ForgeVars.TTSApiTimeout);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

            var url = $"{_apiUrl}?speaker={Uri.EscapeDataString(speaker)}" +
                $"&text={Uri.EscapeDataString(text)}" +
                $"&ext=ogg";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiToken);

            var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _sawmill.Warning("TTS request was rate limited");
                    return null;
                }

                _sawmill.Error($"TTS bad status code: {response.StatusCode}");
                return null;
            }

            var soundData = await response.Content.ReadAsByteArrayAsync(cts.Token);

            if (soundData.Length == 0)
            {
                _sawmill.Error($"TTS returned empty audio for '{text}'");
                return null;
            }

            lock (_lock)
            {
                _cache[cacheKey] = soundData;
                _cacheKeysSeq.Add(cacheKey);

                if (_cache.Count > _maxCachedCount)
                {
                    var firstKey = _cacheKeysSeq.First();
                    _cache.Remove(firstKey);
                    _cacheKeysSeq.Remove(firstKey);
                }
            }

            _sawmill.Debug($"Generated TTS '{text}' ({soundData.Length} bytes)");
            RequestTimings.WithLabels("Success").Observe((DateTime.UtcNow - reqTime).TotalSeconds);

            return soundData;
        }
        catch (TaskCanceledException)
        {
            RequestTimings.WithLabels("Timeout").Observe((DateTime.UtcNow - reqTime).TotalSeconds);
            _sawmill.Error($"Timeout TTS '{text}'");
            return null;
        }
        catch (Exception e)
        {
            RequestTimings.WithLabels("Error").Observe((DateTime.UtcNow - reqTime).TotalSeconds);
            _sawmill.Error($"TTS error '{text}'\n{e}");
            return null;
        }
        finally
        {
            lock (_lock)
            {
                _pendingRequests.Remove(cacheKey);
            }
        }
    }

    public void ResetCache()
    {
        lock (_lock)
        {
            _cache.Clear();
            _cacheKeysSeq.Clear();
        }
    }

    private string GenerateCacheKey(string speaker, string text)
    {
        var key = $"{speaker}/{text}";
        byte[] keyData = Encoding.UTF8.GetBytes(key);
        var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256.ComputeHash(keyData);
        return Convert.ToHexString(bytes);
    }
}

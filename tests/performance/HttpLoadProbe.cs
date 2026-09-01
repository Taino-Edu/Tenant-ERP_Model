using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Octus.Performance
{
    public sealed class HttpSample
    {
        public int Status;
        public long Bytes;
        public double Milliseconds;
        public string CacheControl;
        public string ContentType;
        public string Error;
    }

    public static class HttpLoadProbe
    {
        // Mede a requisição completa, incluindo leitura do corpo descomprimido.
        public static async Task<HttpSample> GetAsync(HttpClient client, string path)
        {
            var timer = Stopwatch.StartNew();
            var sample = new HttpSample();
            try
            {
                using (var response = await client.GetAsync(path).ConfigureAwait(false))
                {
                    sample.Status = (int)response.StatusCode;
                    sample.Bytes = (await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)).LongLength;
                    sample.CacheControl = response.Headers.CacheControl == null ? null : response.Headers.CacheControl.ToString();
                    sample.ContentType = response.Content.Headers.ContentType == null ? null : response.Content.Headers.ContentType.ToString();
                }
            }
            catch (Exception exception)
            {
                // Sem stack/URL/credenciais nos resultados compartilháveis.
                sample.Error = exception.GetType().Name;
            }
            sample.Milliseconds = timer.Elapsed.TotalMilliseconds;
            return sample;
        }
    }
}

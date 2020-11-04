// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace NLU.DevOps.Luis
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class LuisBatchTestClient : ILuisBatchTestClient
    {
        public const int BatchSize = 500;

        private static readonly TimeSpan DefaultTransientDelay = TimeSpan.FromSeconds(2);
        private static readonly Regex RetryAfterSecondsRegex = new Regex(@"^\d+$");

        public LuisBatchTestClient(ILuisConfiguration luisConfiguration)
        {
            this.LuisConfiguration = luisConfiguration;
        }

        private static ILogger Logger => LazyLogger.Value;

        private static Lazy<ILogger> LazyLogger { get; } = new Lazy<ILogger>(() => ApplicationLogger.LoggerFactory.CreateLogger<LuisBatchTestClient>());

        private ILuisConfiguration LuisConfiguration { get; }

        public async Task<string> CreateEvaluationsOperationAsync(JToken batchInput, CancellationToken cancellationToken)
        {
            var request = (HttpWebRequest)WebRequest.Create(new Uri(this.LuisConfiguration.GetBatchEvaluationEndpoint()));
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Headers.Add("Apim-Subscription-Key", this.LuisConfiguration.PredictionKey);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using (var requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                    using (var streamWriter = new StreamWriter(requestStream))
                    {
                        await streamWriter.WriteAsync(batchInput.ToString(Formatting.None)).ConfigureAwait(false);
                        using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                        using (var streamReader = new StreamReader(response.GetResponseStream()))
                        {
                            var responseText = await streamReader.ReadToEndAsync().ConfigureAwait(false);
                            return JToken.Parse(responseText).Value<string>("operationId");
                        }
                    }
                }
                catch (WebException ex)
                when (ex.Response is HttpWebResponse response && IsTransientStatusCode(response.StatusCode))
                {
                    Logger.LogTrace($"Received HTTP {(int)response.StatusCode} result from Cognitive Services. Retrying.");
                    await Task.Delay(GetRetryAfterDelay(response), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task<JToken> GetEvaluationsResultAsync(string operationId, CancellationToken cancellationToken)
        {
            var request = (HttpWebRequest)WebRequest.Create(new Uri(this.LuisConfiguration.GetBatchResultEndpoint(operationId)));
            request.Method = "GET";
            request.Accept = "application/json";
            request.Headers.Add("Apim-Subscription-Key", this.LuisConfiguration.PredictionKey);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                    using (var streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        var responseText = await streamReader.ReadToEndAsync().ConfigureAwait(false);
                        return JToken.Parse(responseText);
                    }
                }
                catch (WebException ex)
                when (ex.Response is HttpWebResponse response && IsTransientStatusCode(response.StatusCode))
                {
                    Logger.LogTrace($"Received HTTP {(int)response.StatusCode} result from Cognitive Services. Retrying.");
                    await Task.Delay(GetRetryAfterDelay(response), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task<string> GetEvaluationsStatusAsync(string operationId, CancellationToken cancellationToken)
        {
            var request = (HttpWebRequest)WebRequest.Create(new Uri(this.LuisConfiguration.GetBatchStatusEndpoint(operationId)));
            request.Method = "GET";
            request.Accept = "application/json";
            request.Headers.Add("Apim-Subscription-Key", this.LuisConfiguration.PredictionKey);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                    using (var streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        var responseText = await streamReader.ReadToEndAsync().ConfigureAwait(false);
                        return JToken.Parse(responseText).Value<string>("status");
                    }
                }
                catch (WebException ex)
                when (ex.Response is HttpWebResponse response && IsTransientStatusCode(response.StatusCode))
                {
                    Logger.LogTrace($"Received HTTP {(int)response.StatusCode} result from Cognitive Services. Retrying.");
                    await Task.Delay(GetRetryAfterDelay(response), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static bool IsTransientStatusCode(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.TooManyRequests
                || (statusCode >= HttpStatusCode.InternalServerError
                && statusCode != HttpStatusCode.HttpVersionNotSupported
                && statusCode != HttpStatusCode.NotImplemented);
        }

        private static TimeSpan GetRetryAfterDelay(HttpWebResponse response)
        {
            var retryAfter = response.Headers[HttpResponseHeader.RetryAfter];
            if (retryAfter == null)
            {
                return DefaultTransientDelay;
            }

            if (RetryAfterSecondsRegex.IsMatch(retryAfter))
            {
                return TimeSpan.FromSeconds(int.Parse(retryAfter, CultureInfo.InvariantCulture));
            }

            return DateTimeOffset.Parse(retryAfter, CultureInfo.InvariantCulture) - DateTimeOffset.Now;
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace NLU.DevOps.Luis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core;
    using Models;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Base class for using the LUIS batch testing API.
    /// </summary>
    public abstract class LuisNLUBatchTestClientBase : INLUTestClient, INLUBatchTestClient
    {
        private static readonly TimeSpan OperationStatusDelay = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Initializes a new instance of the <see cref="LuisNLUBatchTestClientBase"/> class.
        /// </summary>
        /// <param name="luisConfiguration">LUIS configuration.</param>
        /// <param name="luisBatchTestClient">LUIS batch test client.</param>
        public LuisNLUBatchTestClientBase(ILuisConfiguration luisConfiguration, ILuisBatchTestClient luisBatchTestClient)
        {
            this.IsBatchEnabled = luisConfiguration?.UseBatch ?? throw new ArgumentNullException(nameof(luisConfiguration));
            this.LuisBatchTestClient = luisBatchTestClient ?? throw new ArgumentNullException(nameof(luisBatchTestClient));
        }

        /// <inheritdoc />
        public bool IsBatchEnabled { get; }

        private ILuisBatchTestClient LuisBatchTestClient { get; }

        /// <inheritdoc />
        public abstract Task<ILabeledUtterance> TestAsync(JToken query, CancellationToken cancellationToken);

        /// <inheritdoc />
        public abstract Task<ILabeledUtterance> TestSpeechAsync(string speechFile, JToken query, CancellationToken cancellationToken);

        /// <inheritdoc />
        public async Task<IEnumerable<ILabeledUtterance>> TestAsync(IEnumerable<JToken> queries, CancellationToken cancellationToken)
        {
            if (queries == null)
            {
                throw new ArgumentNullException(nameof(queries));
            }

            var batchResults = await queries
                .Batch(Luis.LuisBatchTestClient.BatchSize)
                .Select(CreateBatchInput)
                .SelectAsync(batchInput => this.EvaluateAsync(batchInput, cancellationToken), 1)
                .ConfigureAwait(false);

            return batchResults.SelectMany(BatchResultsToLabeledUtterances);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the LUIS batch test client.
        /// </summary>
        /// <param name="disposing">
        /// <code>true</code> if disposing, otherwise <code>false</code>.
        /// </param>
        protected abstract void Dispose(bool disposing);

        private static JToken CreateBatchInput(IEnumerable<JToken> queries)
        {
            void ensureLuisEntitiy(string text, JToken json)
            {
                if (json is JObject jsonObject && jsonObject.ContainsKey("matchText") && !jsonObject.ContainsKey("startPos"))
                {
                    var matchText = jsonObject.Value<string>("matchText");
                    var matchIndex = jsonObject.Value<int>("matchIndex");
                    var count = 0;
                    var startPos = 0;
                    while (count++ <= matchIndex)
                    {
                        startPos = text.IndexOf(matchText, startPos + 1, StringComparison.Ordinal);
                    }

                    if (startPos == -1)
                    {
                        throw new InvalidOperationException($"Could not find '{matchText}' in '{text}'.");
                    }

                    jsonObject.Add("startPos", startPos);
                    jsonObject.Add("endPos", startPos + matchText.Length - 1);
                    jsonObject["entity"] = jsonObject["entity"] ?? jsonObject["entityType"];
                }
            }

            JToken toLuisBatchInput(JToken json)
            {
                if (json is JObject jsonObject && jsonObject.ContainsKey("entities"))
                {
                    var text = jsonObject.Value<string>("text");
                    foreach (var entity in jsonObject["entities"])
                    {
                        ensureLuisEntitiy(text, entity);
                    }
                }

                return json;
            }

            var utterances = new JArray(queries.Select(toLuisBatchInput));
            return new JObject
            {
                { "LabeledTestSetUtterances", utterances }
            };
        }

        private static IEnumerable<ILabeledUtterance> BatchResultsToLabeledUtterances(JToken batchResults)
        {
            throw new NotImplementedException();
        }

        private async Task<JToken> EvaluateAsync(JToken batchInput, CancellationToken cancellationToken)
        {
            var operationId = await this.LuisBatchTestClient.CreateEvaluationsOperationAsync(batchInput, cancellationToken).ConfigureAwait(false);
            var status = default(string);
            while (status != "succeeded")
            {
                status = await this.LuisBatchTestClient.GetEvaluationsStatusAsync(operationId, cancellationToken).ConfigureAwait(false);
                await Task.Delay(OperationStatusDelay, cancellationToken).ConfigureAwait(false);
            }

            return await this.LuisBatchTestClient.GetEvaluationsResultAsync(operationId, cancellationToken).ConfigureAwait(false);
        }
    }
}

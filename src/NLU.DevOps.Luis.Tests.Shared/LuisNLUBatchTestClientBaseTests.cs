// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace NLU.DevOps.Luis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using FluentAssertions.Json;
    using Moq;
    using Newtonsoft.Json.Linq;
    using NLU.DevOps.Models;
    using NUnit.Framework;

    [TestFixture]
    internal static class LuisNLUBatchTestClientBaseTests
    {
        [Test]
        public static void ThrowsArgumentNull()
        {
            var luisConfiguration = new Mock<ILuisConfiguration>().Object;
            var luisBatchTestClient = new Mock<ILuisBatchTestClient>().Object;
            Action nullLuisConfiguration = () => new LuisNLUBatchTestClient(null, luisBatchTestClient);
            Action nullLuisBatchTestClient = () => new LuisNLUBatchTestClient(luisConfiguration, null);
            nullLuisConfiguration.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("luisConfiguration");
            nullLuisBatchTestClient.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("luisBatchTestClient");

            var nluBatchTestClient = new LuisNLUBatchTestClient(luisConfiguration, luisBatchTestClient);
            Func<Task> nullQueries = () => nluBatchTestClient.TestAsync(default(IEnumerable<JToken>));
            nullQueries.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("queries");
        }

        [Test]
        public static void PullsIsBatchEnabledFromConfiguration()
        {
            var luisConfigurationMock = new Mock<ILuisConfiguration>();
            luisConfigurationMock
                .Setup(c => c.IsBatchEnabled)
                .Returns(true);

            var luisConfiguration = luisConfigurationMock.Object;
            var luisBatchTestClient = new Mock<ILuisBatchTestClient>().Object;
            var nluBatchTestClient = new LuisNLUBatchTestClient(luisConfiguration, luisBatchTestClient);
            nluBatchTestClient.IsBatchEnabled.Should().BeTrue();
        }

        [Test]
        public static async Task ParsesInputAndOutputCorrectly()
        {
            var operationId = Guid.NewGuid().ToString();
            var batchInputJson = default(JToken);
            var luisBatchTestClientMock = new Mock<ILuisBatchTestClient>();
            var outputJson = new JObject
            {
                {
                    "entityModelsStats",
                    new JArray(
                        new JObject
                        {
                            { "modelName", "greeting" },
                        })
                },
                {
                    "utterancesStats",
                    new JArray(
                        new JObject
                        {
                            { "text", "hello" },
                            { "predictedIntentName", "greeting" },
                            { "labeledIntentName", "greeting" },
                            { "falsePositiveEntities", new JArray() },
                            { "falseNegativeEntities", new JArray() },
                        })
                },
            };

            luisBatchTestClientMock.Setup(c => c.CreateEvaluationsOperationAsync(
                    It.IsAny<JToken>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(OperationResponse.Create(operationId)))
                .Callback((JToken json, CancellationToken cancellationToken) => batchInputJson = json);

            luisBatchTestClientMock.Setup(c => c.GetEvaluationsStatusAsync(
                    It.Is<string>(s => s == operationId),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(OperationResponse.Create(new LuisBatchStatusInfo("succeeded", null))));

            luisBatchTestClientMock.Setup(c => c.GetEvaluationsResultAsync(
                    It.Is<string>(s => s == operationId),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<JToken>(outputJson));

            var input = new JObject
            {
                { "text", "hello" },
                { "intent", "greeting" },
                {
                    "entities",
                    new JArray(
                        new JObject
                        {
                            { "entityType", "greeting" },
                            { "matchText", "hello" }
                        })
                },
            };

            var luisConfiguration = new Mock<ILuisConfiguration>().Object;
            var luisBatchTestClient = luisBatchTestClientMock.Object;
            var nluBatchTestClient = new LuisNLUBatchTestClient(luisConfiguration, luisBatchTestClient);
            var results = await nluBatchTestClient.TestAsync(new[] { input }).ConfigureAwait(false);

            batchInputJson.Should().BeEquivalentTo(new JObject
            {
                {
                    "LabeledTestSetUtterances",
                    new JArray(
                        new JObject
                        {
                            { "text", "hello" },
                            { "intent", "greeting" },
                            {
                                "entities",
                                new JArray(
                                    new JObject
                                    {
                                        { "startPos", 0 },
                                        { "endPos", 4 },
                                        { "entity", "greeting" }
                                    })
                            },
                        })
                },
            });

            results.Count().Should().Be(1);
            results.First().Text.Should().Be("hello");
            results.First().Intent.Should().Be("greeting");
            results.First().Entities.Count.Should().Be(1);
            results.First().Entities[0].EntityType.Should().Be("greeting");
            results.First().Entities[0].MatchText.Should().Be("hello");
            results.First().Entities[0].MatchIndex.Should().Be(0);
        }

        [Test]
        public static async Task ExcludesEntitiesWithoutCorrespondingModelStats()
        {
            var operationId = Guid.NewGuid().ToString();
            var batchInputJson = default(JToken);
            var luisBatchTestClientMock = new Mock<ILuisBatchTestClient>();
            var outputJson = new JObject
            {
                { "entityModelsStats", new JArray() },
                {
                    "utterancesStats",
                    new JArray(
                        new JObject
                        {
                            { "text", "hello" },
                            { "predictedIntentName", "greeting" },
                            { "labeledIntentName", "greeting" },
                            { "falsePositiveEntities", new JArray() },
                            { "falseNegativeEntities", new JArray() },
                        })
                },
            };

            luisBatchTestClientMock.Setup(c => c.CreateEvaluationsOperationAsync(
                    It.IsAny<JToken>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(OperationResponse.Create(operationId)))
                .Callback((JToken json, CancellationToken cancellationToken) => batchInputJson = json);

            luisBatchTestClientMock.Setup(c => c.GetEvaluationsStatusAsync(
                    It.Is<string>(s => s == operationId),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(OperationResponse.Create(new LuisBatchStatusInfo("succeeded", null))));

            luisBatchTestClientMock.Setup(c => c.GetEvaluationsResultAsync(
                    It.Is<string>(s => s == operationId),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<JToken>(outputJson));

            var input = new JObject
            {
                { "text", "hello" },
                { "intent", "greeting" },
                {
                    "entities",
                    new JArray(
                        new JObject
                        {
                            { "entityType", "greeting" },
                            { "matchText", "hello" }
                        })
                },
            };

            var luisConfiguration = new Mock<ILuisConfiguration>().Object;
            var luisBatchTestClient = luisBatchTestClientMock.Object;
            var nluBatchTestClient = new LuisNLUBatchTestClient(luisConfiguration, luisBatchTestClient);
            var results = await nluBatchTestClient.TestAsync(new[] { input }).ConfigureAwait(false);
            results.Count().Should().Be(1);
            results.First().Entities.Count.Should().Be(0);
        }

        [Test]
        public static async Task IncludesEntitiesInFalsePositiveResults()
        {
            var operationId = Guid.NewGuid().ToString();
            var batchInputJson = default(JToken);
            var luisBatchTestClientMock = new Mock<ILuisBatchTestClient>();
            var outputJson = new JObject
            {
                {
                    "entityModelsStats",
                    new JArray(
                        new JObject
                        {
                            { "modelName", "greeting" },
                        })
                },
                {
                    "utterancesStats",
                    new JArray(
                        new JObject
                        {
                            { "text", "hello" },
                            { "predictedIntentName", "greeting" },
                            { "labeledIntentName", "greeting" },
                            {
                                "falsePositiveEntities",
                                new JArray(
                                    new JObject
                                    {
                                        { "entityName", "greeting" },
                                        { "startCharIndex", 0 },
                                        { "endCharIndex", 4 },
                                    })
                            },
                            { "falseNegativeEntities", new JArray() },
                        })
                },
            };

            luisBatchTestClientMock.Setup(c => c.CreateEvaluationsOperationAsync(
                    It.IsAny<JToken>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(OperationResponse.Create(operationId)))
                .Callback((JToken json, CancellationToken cancellationToken) => batchInputJson = json);

            luisBatchTestClientMock.Setup(c => c.GetEvaluationsStatusAsync(
                    It.Is<string>(s => s == operationId),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(OperationResponse.Create(new LuisBatchStatusInfo("succeeded", null))));

            luisBatchTestClientMock.Setup(c => c.GetEvaluationsResultAsync(
                    It.Is<string>(s => s == operationId),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<JToken>(outputJson));

            var input = new JObject
            {
                { "text", "hello" },
                { "intent", "greeting" },
            };

            var luisConfiguration = new Mock<ILuisConfiguration>().Object;
            var luisBatchTestClient = luisBatchTestClientMock.Object;
            var nluBatchTestClient = new LuisNLUBatchTestClient(luisConfiguration, luisBatchTestClient);
            var results = await nluBatchTestClient.TestAsync(new[] { input }).ConfigureAwait(false);
            results.Count().Should().Be(1);
            results.First().Entities.Count.Should().Be(1);
            results.First().Entities[0].EntityType.Should().Be("greeting");
            results.First().Entities[0].MatchText.Should().Be("hello");
            results.First().Entities[0].MatchIndex.Should().Be(0);
        }

        [Test]
        public static async Task ExcludesEntitiesInFalseNegativeResults()
        {
            var operationId = Guid.NewGuid().ToString();
            var batchInputJson = default(JToken);
            var luisBatchTestClientMock = new Mock<ILuisBatchTestClient>();
            var outputJson = new JObject
            {
                { "entityModelsStats", new JArray() },
                {
                    "utterancesStats",
                    new JArray(
                        new JObject
                        {
                            { "text", "hello" },
                            { "predictedIntentName", "greeting" },
                            { "labeledIntentName", "greeting" },
                            { "falsePositiveEntities", new JArray() },
                            {
                                "falseNegativeEntities",
                                new JArray(
                                    new JObject
                                    {
                                        { "entityName", "greeting" },
                                        { "startCharIndex", 0 },
                                        { "endCharIndex", 4 },
                                    })
                            },
                        })
                },
            };

            luisBatchTestClientMock.Setup(c => c.CreateEvaluationsOperationAsync(
                    It.IsAny<JToken>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(OperationResponse.Create(operationId)))
                .Callback((JToken json, CancellationToken cancellationToken) => batchInputJson = json);

            luisBatchTestClientMock.Setup(c => c.GetEvaluationsStatusAsync(
                    It.Is<string>(s => s == operationId),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(OperationResponse.Create(new LuisBatchStatusInfo("succeeded", null))));

            luisBatchTestClientMock.Setup(c => c.GetEvaluationsResultAsync(
                    It.Is<string>(s => s == operationId),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<JToken>(outputJson));

            var input = new JObject
            {
                { "text", "hello" },
                { "intent", "greeting" },
                {
                    "entities",
                    new JArray(
                        new JObject
                        {
                            { "entityType", "greeting" },
                            { "matchText", "hello" }
                        })
                },
            };

            var luisConfiguration = new Mock<ILuisConfiguration>().Object;
            var luisBatchTestClient = luisBatchTestClientMock.Object;
            var nluBatchTestClient = new LuisNLUBatchTestClient(luisConfiguration, luisBatchTestClient);
            var results = await nluBatchTestClient.TestAsync(new[] { input }).ConfigureAwait(false);
            results.Count().Should().Be(1);
            results.First().Entities.Count.Should().Be(0);
        }

        private class LuisNLUBatchTestClient : LuisNLUBatchTestClientBase
        {
            public LuisNLUBatchTestClient(ILuisConfiguration luisConfiguration, ILuisBatchTestClient luisBatchTestClient)
                : base(luisConfiguration, luisBatchTestClient)
            {
            }

            public override Task<ILabeledUtterance> TestAsync(JToken query, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public override Task<ILabeledUtterance> TestSpeechAsync(string speechFile, JToken query, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            protected override void Dispose(bool disposing)
            {
            }
        }
    }
}

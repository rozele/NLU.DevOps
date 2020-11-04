﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace NLU.DevOps.Luis
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// LUIS configuration.
    /// </summary>
    public class LuisConfiguration : ILuisConfiguration
    {
        /// <summary>
        /// Configuration key for LUIS app ID.
        /// </summary>
        protected static readonly string LuisAppIdConfigurationKey = CamelCase(nameof(LuisNLUTrainClient.LuisAppId));

        private const string LuisAppNameConfigurationKey = "luisAppName";
        private const string LuisAppNamePrefixConfigurationKey = "luisAppNamePrefix";
        private const string LuisAuthoringKeyConfigurationKey = "luisAuthoringKey";
        private const string LuisEndpointKeyConfigurationKey = "luisEndpointKey";
        private const string LuisAuthoringRegionConfigurationKey = "luisAuthoringRegion";
        private const string LuisAuthoringResourceNameConfigurationKey = "luisAuthoringResourceName";
        private const string LuisEndpointRegionConfigurationKey = "luisEndpointRegion";
        private const string LuisPredictionResourceNameConfigurationKey = "luisPredictionResourceName";
        private const string LuisVersionIdConfigurationKey = "luisVersionId";
        private const string LuisVersionPrefixConfigurationKey = "luisVersionPrefix";
        private const string LuisIsStagingConfigurationKey = "luisIsStaging";
        private const string LuisUseBatchExperimentalConfigurationKey = "luisUseBatchExperimental";
        private const string SpeechKeyConfigurationKey = "speechKey";
        private const string SpeechRegionConfigurationKey = "speechRegion";
        private const string CustomSpeechAppIdConfigurationKey = "customSpeechAppId";
#if LUIS_V2
        private const string LuisUseSpeechEndpointConfigurationKey = "luisUseSpeechEndpoint";
#endif
#if LUIS_V3
        private const string LuisSlotNameConfigurationKey = "luisSlotName";
#endif
        private const string LuisDirectVersionPublishConfigurationKey = "luisDirectVersionPublish";
        private const string AzureSubscriptionIdConfigurationKey = "azureSubscriptionId";
        private const string AzureResourceGroupConfigurationKey = "azureResourceGroup";
        private const string AzureAppNameConfigurationKey = "azureLuisResourceName";
        private const string ArmTokenConfigurationKey = "ARM_TOKEN";
        private const string BuildIdConfigurationKey = "BUILD_BUILDID";

        private const string CustomSpeechEndpointTemplate = "https://{0}.stt.speech.microsoft.com/speech/recognition/interactive/cognitiveservices/v1?language={1}&cid={2}&format=detailed";
        private const string SpeechEndpointTemplate = "https://{0}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language={1}&format=detailed";
        private const string CognitiveServicesAzureTemplate = "https://{0}.cognitiveservices.azure.com";
        private const string ApiCognitiveMicrosoftTemplate = "https://{0}.api.cognitive.microsoft.com";

        private static readonly string LuisAppCreatedConfigurationKey = CamelCase(nameof(LuisNLUTrainClient.LuisAppCreated));

        /// <summary>
        /// Initializes a new instance of the <see cref="LuisConfiguration"/> class.
        /// </summary>
        /// <param name="configuration">Configuration.</param>
        public LuisConfiguration(IConfiguration configuration)
        {
            this.Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.LazyAppName = new Lazy<string>(this.GetOrCreateAppName);
        }

        /// <inheritdoc />
        public string AppName => this.LazyAppName.Value;

        /// <inheritdoc />
        public virtual string AppId => this.Configuration[LuisAppIdConfigurationKey];

        /// <inheritdoc />
        public string AuthoringKey => this.EnsureConfigurationString(LuisAuthoringKeyConfigurationKey);

        /// <inheritdoc />
        public string AuthoringEndpoint => this.GetEndpoint(
            new[] { LuisAuthoringResourceNameConfigurationKey },
            new[] { LuisAuthoringRegionConfigurationKey });

        /// <inheritdoc />
        public string PredictionKey => this.EnsureConfigurationString(
            LuisEndpointKeyConfigurationKey,
            LuisAuthoringKeyConfigurationKey);

        /// <inheritdoc />
        public string PredictionEndpoint => this.GetEndpoint(
            new[] { LuisPredictionResourceNameConfigurationKey, LuisAuthoringResourceNameConfigurationKey },
            new[] { LuisEndpointRegionConfigurationKey, LuisAuthoringRegionConfigurationKey });

        /// <inheritdoc />
        public string PredictionResourceName =>
            this.Configuration[LuisPredictionResourceNameConfigurationKey] ??
            this.Configuration[AzureAppNameConfigurationKey];

        /// <inheritdoc />
        public virtual string VersionId => this.GetVersionId();

        /// <inheritdoc />
        public bool IsStaging => this.GetConfigurationBoolean(LuisIsStagingConfigurationKey);

        /// <inheritdoc />
        public bool AppCreated => this.GetConfigurationBoolean(LuisAppCreatedConfigurationKey);

        /// <inheritdoc />
        public string SpeechKey => this.EnsureConfigurationString(
            SpeechKeyConfigurationKey,
            LuisEndpointKeyConfigurationKey);

        /// <inheritdoc />
        public string SpeechRegion => this.EnsureConfigurationString(
            SpeechRegionConfigurationKey,
            LuisEndpointRegionConfigurationKey);

        /// <inheritdoc />
        public Uri SpeechEndpoint => this.GetSpeechEndpoint();
#if LUIS_V2

        /// <inheritdoc />
        public bool UseSpeechEndpoint =>
            this.GetConfigurationBoolean(LuisUseSpeechEndpointConfigurationKey) ||
            this.CustomSpeechAppId != null;
#endif

        /// <inheritdoc />
        public string SlotName =>
#if LUIS_V3
            this.Configuration[LuisSlotNameConfigurationKey] ??
#endif
            this.DefaultSlotName;

        /// <inheritdoc />
        public bool DirectVersionPublish => this.GetConfigurationBoolean(LuisDirectVersionPublishConfigurationKey);

        /// <inheritdoc />
        public bool UseBatch => this.GetConfigurationBoolean(LuisUseBatchExperimentalConfigurationKey);

        /// <inheritdoc />
        public string AzureResourceGroup => this.Configuration[AzureResourceGroupConfigurationKey];

        /// <inheritdoc />
        public string AzureSubscriptionId => this.Configuration[AzureSubscriptionIdConfigurationKey];

        /// <inheritdoc />
        public string ArmToken => this.Configuration[ArmTokenConfigurationKey];

        private IConfiguration Configuration { get; }

        private Lazy<string> LazyAppName { get; }

        private string CustomSpeechAppId => this.Configuration[CustomSpeechAppIdConfigurationKey];

        private string DefaultSlotName => this.IsStaging ? "Staging" : "Production";

        /// <summary>
        /// Gets a non-null configuration value, or throws.
        /// </summary>
        /// <param name="key">Configuration key.</param>
        /// <returns>The configuration value.</returns>
        protected string EnsureConfigurationString(string key)
        {
            var value = this.Configuration[key];
            if (value == null)
            {
                throw new InvalidOperationException($"Configuration value for '{key}' must be supplied.");
            }

            return value;
        }

        private static string CamelCase(string s)
        {
            if (string.IsNullOrEmpty(s) || char.IsLower(s[0]))
            {
                return s;
            }

            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        private string GetOrCreateAppName()
        {
            var appName = this.Configuration[LuisAppNameConfigurationKey];
            if (appName != null)
            {
                return appName;
            }

            var prefix = this.Configuration[LuisAppNamePrefixConfigurationKey];
            var random = new Random();
            var randomString = new string(Enumerable.Repeat(0, 8)
                .Select(_ => (char)random.Next((int)'A', (int)'Z'))
                .ToArray());

            prefix = prefix != null ? $"{prefix}_" : prefix;
            return $"{prefix}{randomString}";
        }

        private string GetEndpoint(string[] resourceNameKeys, string[] regionKeys)
        {
            Debug.Assert(resourceNameKeys.Length >= 1 && regionKeys.Length >= 1, "Expected more than one configuration key.");

            var resourceName = resourceNameKeys.Select(key => this.Configuration[key]).FirstOrDefault(value => value != null);
            if (resourceName != null)
            {
                return string.Format(CultureInfo.InvariantCulture, CognitiveServicesAzureTemplate, resourceName);
            }

            var region = regionKeys.Select(key => this.Configuration[key]).FirstOrDefault(value => value != null);
            if (region != null)
            {
                return string.Format(CultureInfo.InvariantCulture, ApiCognitiveMicrosoftTemplate, region);
            }

            var keys = resourceNameKeys.Concat(regionKeys);
            var keysString = $"'{string.Join("', '", keys.SkipLast(1))}' or '{keys.TakeLast(1).Single()}'";
            throw new InvalidOperationException($"Configuration value for one of {keysString} must be supplied.");
        }

        private string GetVersionId()
        {
            var versionId = this.Configuration[LuisVersionIdConfigurationKey];
            if (versionId != null)
            {
                return versionId;
            }

            var versionIdPrefix = this.Configuration[LuisVersionPrefixConfigurationKey];
            var buildId = this.Configuration[BuildIdConfigurationKey];
            if (versionIdPrefix == null || buildId == null)
            {
                return "0.1.1";
            }

            return $"{versionIdPrefix}.{buildId}";
        }

        private Uri GetSpeechEndpoint()
        {
            return new Uri(this.CustomSpeechAppId != null
                ? string.Format(CultureInfo.InvariantCulture, CustomSpeechEndpointTemplate, this.SpeechRegion, "en-US", this.CustomSpeechAppId)
                : string.Format(CultureInfo.InvariantCulture, SpeechEndpointTemplate, this.SpeechRegion, "en-US"));
        }

        private string EnsureConfigurationString(params string[] keys)
        {
            Debug.Assert(keys.Length > 1, "Expected more than one configuration key.");

            foreach (var key in keys)
            {
                var value = this.Configuration[key];
                if (value != null)
                {
                    return value;
                }
            }

            var keysString = $"'{string.Join("', '", keys.SkipLast(1))}' or '{keys.TakeLast(1).Single()}'";
            throw new InvalidOperationException($"Configuration value for one of {keysString} must be supplied.");
        }

        private bool GetConfigurationBoolean(string key)
        {
            var value = this.Configuration[key];
            if (value == null)
            {
                return false;
            }

            if (bool.TryParse(value, out var result))
            {
                return result;
            }

            throw new InvalidOperationException($"Configuration value for '{key}' must be valid boolean.");
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace NLU.DevOps.Luis
{
    internal static class LuisConfigurationExtensions
    {
        private const string BatchEndpointBase = "https://dialogice4.azurewebsites.net/api/v3.0/apps/";

        public static string GetBatchEvaluationEndpoint(this ILuisConfiguration luisConfiguration)
        {
            if (luisConfiguration.DirectVersionPublish)
            {
                return $"{BatchEndpointBase}{luisConfiguration.AppId}/versions/{luisConfiguration.VersionId}/evaluations";
            }

            return $"{BatchEndpointBase}{luisConfiguration.AppId}/slots/{luisConfiguration.SlotName}/evaluations";
        }

        public static string GetBatchStatusEndpoint(this ILuisConfiguration luisConfiguration, string operationId)
        {
            return $"{luisConfiguration.GetBatchEvaluationEndpoint()}/{operationId}/status";
        }

        public static string GetBatchResultEndpoint(this ILuisConfiguration luisConfiguration, string operationId)
        {
            return $"{luisConfiguration.GetBatchEvaluationEndpoint()}/{operationId}/result";
        }
    }
}

﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace NLU.DevOps.Luis
{
    /// <summary>
    /// Information about the batch test evaluation operation status.
    /// </summary>
    /// <typeparam name="T">Type of response value.</typeparam>
    public class OperationResponse<T>
    {
        internal OperationResponse(T value, string retryAfter, string operationLocation)
        {
            this.Value = value;
            this.RetryAfter = retryAfter;
            this.OperationLocation = operationLocation;
        }

        /// <summary>
        /// Gets the response value.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Gets the HTTP 'Retry-After' header.
        /// </summary>
        public string RetryAfter { get; }

        /// <summary>
        /// Gets the HTTP 'Operation-Location' header.
        /// </summary>
        public string OperationLocation { get; }
    }
}

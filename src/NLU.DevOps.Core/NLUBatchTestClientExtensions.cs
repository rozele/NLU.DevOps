// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace NLU.DevOps.Core
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Models;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// NLU batch test client extensions.
    /// </summary>
    public static class NLUBatchTestClientExtensions
    {
        /// <summary>
        /// Tests the NLU model with a batch of queries.
        /// </summary>
        /// <param name="instance">NLU batch test client.</param>
        /// <param name="queries">Queries to test.</param>
        /// <returns>Task to await the resulting labeled utterances.</returns>
        public static Task<IEnumerable<ILabeledUtterance>> TestAsync(this INLUBatchTestClient instance, IEnumerable<JToken> queries)
        {
            return instance.TestAsync(queries, CancellationToken.None);
        }
    }
}

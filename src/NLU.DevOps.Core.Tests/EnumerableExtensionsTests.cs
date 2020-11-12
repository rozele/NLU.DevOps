// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace NLU.DevOps.Core.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using NUnit.Framework;

    [TestFixture]
    internal static class EnumerableExtensionsTests
    {
        [Test]
        public static void ThrowsArgumentExceptions()
        {
            Action nullBatchItems = () => EnumerableExtensions.Batch<int>(null, 1).ToList();
            Action invalidBatchSize = () => EnumerableExtensions.Batch(Array.Empty<int>(), 0).ToList();
            Func<Task> nullSelectAsyncItems = () => EnumerableExtensions.SelectAsync(null, (int x) => Task.FromResult(x), 1);
            Func<Task> nullSelectAsyncSelector = () => EnumerableExtensions.SelectAsync<int, int>(Array.Empty<int>(), null, 1);
            Func<Task> invalidDegreeOfParallelism = () => EnumerableExtensions.SelectAsync(Array.Empty<int>(), x => Task.FromResult(x), 0);

            nullBatchItems.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("items");
            invalidBatchSize.Should().Throw<ArgumentOutOfRangeException>().And.ParamName.Should().Be("batchSize");
            nullSelectAsyncItems.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("items");
            nullSelectAsyncSelector.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("selector");
            invalidDegreeOfParallelism.Should().Throw<ArgumentOutOfRangeException>().And.ParamName.Should().Be("degreeOfParallelism");
        }

        [Test]
        public static void CreatesBatches()
        {
            Enumerable.Repeat(0, 2).Batch(1).Count().Should().Be(2);
        }

        [Test]
        public static void CreatesBatchesWithRemainder()
        {
            var batches = Enumerable.Repeat(0, 3).Batch(2);
            batches.Count().Should().Be(2);
            batches.Select(b => b.Count()).Should().BeEquivalentTo(2, 1);
        }

        [Test]
        public static void DoesNotCreateBatches()
        {
            Array.Empty<int>().Batch(1).Any().Should().BeFalse();
        }

        [Test]
        public static async Task SelectAsyncLimitsParallelism()
        {
            var enter = new ManualResetEvent(false);
            var leave = new AutoResetEvent(false);
            Task<int> selector(int x)
            {
                enter.Set();
                leave.WaitOne();
                return Task.FromResult(x);
            }

            var task = Task.Run(() => Enumerable.Repeat(0, 2)
                .SelectAsync(selector, 1));

            enter.WaitOne().Should().BeTrue();
            leave.Set();
            task.IsCompleted.Should().BeFalse();
            leave.Set();
            await task.ConfigureAwait(false);
        }
    }
}

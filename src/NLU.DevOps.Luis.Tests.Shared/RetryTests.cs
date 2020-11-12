// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace NLU.DevOps.Luis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    internal static class RetryTests
    {
        [Test]
        public static void ParsesRetryAfterIntString()
        {
            var retryAfter = "42";
            Retry.GetRetryAfterDelay(retryAfter, TimeSpan.Zero).TotalSeconds.Should().BeApproximately(42, 10e-6);
        }

        [Test]
        public static void ParsesRetryAfterDateString()
        {
            var delta = 1;
            var retryAfter = DateTimeOffset.Now.AddSeconds(10).ToString("r", CultureInfo.InvariantCulture);
            var delay = Retry.GetRetryAfterDelay(retryAfter, TimeSpan.Zero);
            delay.TotalSeconds.Should().BeLessOrEqualTo(10);
            delay.TotalSeconds.Should().BeGreaterOrEqualTo(10 - delta);
        }

        [Test]
        [TestCase(HttpStatusCode.TooManyRequests, true)]
        [TestCase(HttpStatusCode.InternalServerError, true)]
        [TestCase((HttpStatusCode)502, true)]
        [TestCase(HttpStatusCode.NotImplemented, false)]
        [TestCase(HttpStatusCode.HttpVersionNotSupported, false)]
        public static void CorrectlyIdentifiesTransientError(HttpStatusCode statusCode, bool isTransient)
        {
            Retry.IsTransientStatusCode(statusCode).Should().Be(isTransient);
        }

        [Test]
        public static void OnTransientErrorAsyncThrowsCancellation()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Func<Task> throwsOperationCanceled = () => Retry.OnTransientErrorAsync(() => Task.FromResult(true), cts.Token);
                throwsOperationCanceled.Should().Throw<OperationCanceledException>().And.CancellationToken.Should().Be(cts.Token);
            }
        }

        [Test]
        public static void OnTransientErrorAsyncRetryMax()
        {
            var count = 0;
            Task<bool> doAsync()
            {
                count++;
                var responseMock = new Mock<HttpWebResponse>();
                responseMock.Setup(r => r.StatusCode).Returns(HttpStatusCode.InternalServerError);
                throw new WebException(string.Empty, null, WebExceptionStatus.UnknownError, responseMock.Object);
            }

            Func<Task> throwsOperationCanceled = () => Retry.OnTransientErrorAsync(doAsync, CancellationToken.None);
            throwsOperationCanceled.Should().Throw<WebException>();
            count.Should().Be(5);
        }

        [Test]
        public static async Task OnTransientErrorAsyncRetries()
        {
            var count = 0;
            Task<bool> doAsync()
            {
                if (count++ == 0)
                {
                    var responseMock = new Mock<HttpWebResponse>();
                    responseMock.Setup(r => r.StatusCode).Returns(HttpStatusCode.InternalServerError);
                    throw new WebException(string.Empty, null, WebExceptionStatus.UnknownError, responseMock.Object);
                }

                return Task.FromResult(true);
            }

            var result = await Retry.OnTransientErrorAsync(doAsync, CancellationToken.None).ConfigureAwait(false);
            result.Should().BeTrue();
        }

        [Test]
        public static void OnTransientErrorAsyncDoesNotCatch()
        {
            var exception = new InvalidOperationException(string.Empty);
            Task<bool> doAsync()
            {
                throw exception;
            }

            Func<Task> result = () => Retry.OnTransientErrorAsync(doAsync, CancellationToken.None);
            result.Should().Throw<InvalidOperationException>().And.Should().Be(exception);
        }

        [Test]
        public static async Task OnTransientErrorAsyncUsesRetryAfterHeader()
        {
            var stopwatch = Stopwatch.StartNew();
            var invokeTimes = new List<TimeSpan>();
            Task<bool> doAsync()
            {
                invokeTimes.Add(stopwatch.Elapsed);
                if (invokeTimes.Count == 1)
                {
                    var headers = new WebHeaderCollection();
                    headers[HttpResponseHeader.RetryAfter] = "1";
                    var responseMock = new Mock<HttpWebResponse>();
                    responseMock.Setup(r => r.StatusCode).Returns(HttpStatusCode.InternalServerError);
                    responseMock.Setup(r => r.Headers).Returns(headers);
                    throw new WebException(string.Empty, null, WebExceptionStatus.UnknownError, responseMock.Object);
                }

                return Task.FromResult(true);
            }

            var result = await Retry.OnTransientErrorAsync(doAsync, CancellationToken.None).ConfigureAwait(false);
            result.Should().BeTrue();
            invokeTimes.Count.Should().Be(2);
            (invokeTimes[1] - invokeTimes[0]).TotalSeconds.Should().BeApproximately(1, 0.1);
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace WebPush.Test
{
    [TestClass]
    public class WebPushClientTest
    {
        private const string TestPublicKey =
            @"BCvKwB2lbVUYMFAaBUygooKheqcEU-GDrVRnu8k33yJCZkNBNqjZj0VdxQ2QIZa4kV5kpX9aAqyBKZHURm6eG1A";

        private const string TestPrivateKey = @"on6X5KmLEFIVvPP3cNX9kE0OF6PV9TJQXVbnKU2xEHI";

        private const string TestGcmEndpoint = @"https://android.googleapis.com/gcm/send/";

        private const string TestFcmEndpoint =
            @"https://fcm.googleapis.com/fcm/send/efz_TLX_rLU:APA91bE6U0iybLYvv0F3mf6";

        public const string TestSubject = "mailto:example@example.com";

        private MockHttpMessageHandler httpMessageHandlerMock;
        private WebPushClient client;

        [TestInitialize]
        public void InitializeTest()
        {
            httpMessageHandlerMock = new MockHttpMessageHandler();
            client = new WebPushClient(httpMessageHandlerMock.ToHttpClient());
        }

        [TestMethod]
        public void TestGcmApiKeyInOptions()
        {
            var gcmAPIKey = @"teststring";
            var subscription = new PushSubscription(TestGcmEndpoint, TestPublicKey, TestPrivateKey);

            var options = new Dictionary<string, object>();
            options[@"gcmAPIKey"] = gcmAPIKey;
            var message = client.GenerateRequestDetails(subscription, @"test payload", options);
            var authorizationHeader = message.Headers.GetValues(@"Authorization").First();

            Assert.AreEqual("key=" + gcmAPIKey, authorizationHeader);

            // Test previous incorrect casing of gcmAPIKey
            var options2 = new Dictionary<string, object>();
            options2[@"gcmApiKey"] = gcmAPIKey;
            Assert.ThrowsException<ArgumentException>(delegate
            {
                client.GenerateRequestDetails(subscription, "test payload", options2);
            });
        }

        [TestMethod]
        public void TestSetGcmApiKey()
        {
            var gcmAPIKey = @"teststring";
            client.SetGcmApiKey(gcmAPIKey);
            var subscription = new PushSubscription(TestGcmEndpoint, TestPublicKey, TestPrivateKey);
            var message = client.GenerateRequestDetails(subscription, @"test payload");
            var authorizationHeader = message.Headers.GetValues(@"Authorization").First();

            Assert.AreEqual(@"key=" + gcmAPIKey, authorizationHeader);
        }

        [TestMethod]
        public void TestSetGCMAPIKeyEmptyString()
        {
            Assert.ThrowsException<ArgumentException>(delegate { client.SetGcmApiKey(""); });
        }

        [TestMethod]
        public void TestSetGcmApiKeyNonGcmPushService()
        {
            // Ensure that the API key doesn't get added on a service that doesn't accept it.

            var gcmAPIKey = @"teststring";
            client.SetGcmApiKey(gcmAPIKey);
            var subscription = new PushSubscription(TestFcmEndpoint, TestPublicKey, TestPrivateKey);
            var message = client.GenerateRequestDetails(subscription, @"test payload");

            IEnumerable<string> values;
            Assert.IsFalse(message.Headers.TryGetValues(@"Authorization", out values));
        }

        [TestMethod]
        public void TestSetGcmApiKeyNull()
        {
            client.SetGcmApiKey(@"somestring");
            client.SetGcmApiKey(null);

            var subscription = new PushSubscription(TestGcmEndpoint, TestPublicKey, TestPrivateKey);
            var message = client.GenerateRequestDetails(subscription, @"test payload");

            IEnumerable<string> values;
            Assert.IsFalse(message.Headers.TryGetValues("Authorization", out values));
        }

        [TestMethod]
        public void TestSetVapidDetails()
        {
            client.SetVapidDetails(TestSubject, TestPublicKey, TestPrivateKey);

            var subscription = new PushSubscription(TestFcmEndpoint, TestPublicKey, TestPrivateKey);
            var message = client.GenerateRequestDetails(subscription, @"test payload");
            var authorizationHeader = message.Headers.GetValues(@"Authorization").First();
            var cryptoHeader = message.Headers.GetValues(@"Crypto-Key").First();

            Assert.IsTrue(authorizationHeader.StartsWith(@"WebPush "));
            Assert.IsTrue(cryptoHeader.Contains(@"p256ecdsa"));
        }

        [TestMethod]
        [DataRow(HttpStatusCode.Created)]
        [DataRow(HttpStatusCode.Accepted)]
        public void TestHandlingSuccessHttpCodes(HttpStatusCode status)
        {
            TestSendNotification(status);
        }

        [TestMethod]
        [DataRow(HttpStatusCode.BadRequest, "Bad Request")]
        [DataRow(HttpStatusCode.RequestEntityTooLarge, "Payload too large")]
        [DataRow((HttpStatusCode)429, "Too many request.")]
        [DataRow(HttpStatusCode.NotFound, "Subscription no longer valid")]
        [DataRow(HttpStatusCode.Gone, "Subscription no longer valid")]
        [DataRow(HttpStatusCode.InternalServerError, "Received unexpected response code: 500")]
        public void TestHandlingFailureHttpCodes(HttpStatusCode status, string expectedMessage)
        {
            var actual = Assert.ThrowsException<WebPushException>(() => TestSendNotification(status));

            Assert.AreEqual(expectedMessage, actual.Message);
        }

        private void TestSendNotification(HttpStatusCode status)
        {
            var subscription = new PushSubscription(TestFcmEndpoint, TestPublicKey, TestPrivateKey); ;
            httpMessageHandlerMock.When(TestFcmEndpoint).Respond(status);

            client.SetVapidDetails(TestSubject, TestPublicKey, TestPrivateKey);

            client.SendNotification(subscription, "123");
        }
    }
}
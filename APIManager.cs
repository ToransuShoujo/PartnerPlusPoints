using Newtonsoft.Json.Linq;
using RestSharp;
using System.Reflection;
using TwitchLib.Api;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

namespace PartnerPlusPoints
{
    public class APIManager
    {
        const string twitchClientId = "kimne78kx3ncx6brgo4mv6wki5h1ko"; // Baked into every webpage.
        const string pppClientId = "e0arhpzrbpc636zprhn6sjxv2tnbog"; // The client ID of Partner Plus Points Counter, used for PubSub authorization.
        public static TwitchAPI API = new();
        private static TwitchPubSub pubSubClient = new();
        private static List<string> tierOneSubs = new();
        private static List<string> tierTwoSubs = new();
        private static List<string> tierThreeSubs = new();

        public static async Task<int> GetPartnerPlusPoints()
        {
            Console.Write("Getting Partner Plus Points... ");

            if (string.IsNullOrWhiteSpace(Program.UserSettings.ChannelID) || string.IsNullOrWhiteSpace(Program.TempGQLAuthToken)) { return -1; }

            string currentTime = DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");
            string queryString;

            if (Program.UserSettings.DisplayActualCalc)
            {
                var startTime = DateTime.UtcNow.AddMonths(-3).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'"); // Three months ago, which is what the GQL call on the PartnerPlus page uses. 
                queryString = "{\"query\":\"query {\\r\\n    creatorProgramInfo(channelID:\\\"" + Program.UserSettings.ChannelID + "\\\", endDate:\\\"" + currentTime + "\\\", startDate:\\\"" + startTime + "\\\") {\\r\\n        partnerPlusProgram {\\r\\n            subPoints {\\r\\n                count\\r\\n            }\\r\\n        }\\r\\n    }\\r\\n}\",\"variables\":{}}";
            }
            else
            {
                var startTime = DateTime.UtcNow.AddDays(-30).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'"); // One month ago, which is usually simply defined as 30 days in the Twitch dashboard.
                queryString = "{\"query\":\"query {\\r\\n    revenues(startAt: \\\"" + startTime + "\\\", endAt: \\\"" + currentTime + "\\\", timeZone: \\\"UTC\\\", channelID: \\\"" + Program.UserSettings.ChannelID + "\\\") {\\r\\n        revenuePanel {\\r\\n            paidSubscriptions {\\r\\n                tierOneSubs {\\r\\n                    subCount\\r\\n                }\\r\\n                tierTwoSubs {\\r\\n                    subCount\\r\\n                }\\r\\n                tierThreeSubs {\\r\\n                    subCount\\r\\n                }\\r\\n            }\\r\\n        }\\r\\n    }\\r\\n}\",\"variables\":{}}";
            }

            var options = new RestClientOptions("https://gql.twitch.tv") { MaxTimeout = -1 };
            var client = new RestClient(options);
            var request = new RestRequest("/gql", Method.Post);
            request.AddHeader("Client-ID", twitchClientId);
            request.AddHeader("Authorization", $"OAuth {Program.TempGQLAuthToken}");
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", queryString, ParameterType.RequestBody);

            var response = await client.ExecuteAsync(request);
            if (response.Content == null) { return -2; } // -2 error is something other than unauthenticated. -1 is unauthenticated. The response should always have content if things went correctly.

            var errorCheckValue = CheckError(response.Content);
            var responseObject = JObject.Parse(response.Content);

            if (Program.UserSettings.DisplayActualCalc)
            {
                JToken? subPoints = responseObject.SelectToken("data.creatorProgramInfo.partnerPlusProgram.subPoints");
                if (subPoints == null) { return errorCheckValue; }

                JToken? subPointCount = JArray.Parse(subPoints.ToString())[0]["count"];
                if (subPointCount == null) { return -2; }

                if (int.TryParse(subPointCount.ToString(), out int convertedCount)) { return convertedCount; }
                else { return -2; }
            }
            else
            {
                JToken? paidSubscriptions = responseObject.SelectToken("data.revenues.revenuePanel.paidSubscriptions");
                if (paidSubscriptions == null) { return errorCheckValue; }

                int? tierOneSubs = (int?)paidSubscriptions.SelectToken("tierOneSubs.subCount");
                int? tierTwoSubs = (int?)paidSubscriptions.SelectToken("tierTwoSubs.subCount");
                int? tierThreeSubs = (int?)paidSubscriptions.SelectToken("tierThreeSubs.subCount");

                if (tierOneSubs != null && tierTwoSubs != null && tierThreeSubs != null)
                {
                    int? totalCalculation = tierOneSubs + (tierTwoSubs * 2) + (tierThreeSubs * 6) - ((Program.UserSettings.LifetimeTierThrees + 1) * 6);
                    return (int)totalCalculation;
                }
                else { return -2; }
            }
        }

        private static int CheckError(string response)
        {
            if (response.Contains("unauthenticated") || response.Contains("Unauthorized")) { return -1; }
            else { return -2; }
        }

        public static async Task<bool> ValidateToken()
        {
            API.Settings.ClientId = pppClientId;
            API.Settings.AccessToken = Program.UserSettings.APIAuth;

            Console.Write("Validating Twitch API token... ");
            var validationResponse = await API.Auth.ValidateAccessTokenAsync();

            if (validationResponse == null)
            {
                ConsoleHelper.Failure();
                ConsoleHelper.Warn("Your Twitch API token is invalid. You must generate a new one.");
                return false;
            }
            else
            {
                ConsoleHelper.Success();
                if (string.IsNullOrWhiteSpace(Program.UserSettings.ChannelID)) { Program.UserSettings.ChannelID = validationResponse.UserId; }
                return true;
            }
        }

        public static async Task ListenForSubs()
        {
            (tierOneSubs, tierTwoSubs, tierThreeSubs) = await GetAllSubs();

            pubSubClient = new TwitchPubSub();
            pubSubClient.OnPubSubServiceConnected += OnPubSubServiceConnected;
            pubSubClient.OnListenResponse += OnListenResponse;
            pubSubClient.OnChannelSubscription += OnChannelSubscription;
            pubSubClient.ListenToSubscriptions(Program.UserSettings.ChannelID);

            pubSubClient.Connect();
        }

        private static void OnPubSubServiceConnected(object? sender, EventArgs e)
        {
            Console.WriteLine("Connected to PubSub to monitor subscription events.");
            pubSubClient.SendTopics(Program.UserSettings.APIAuth);
        }

        private static void OnListenResponse(object? sender, OnListenResponseArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Response.Error)) { ConsoleHelper.Warn($"Disconnected from PubSub with error \"{e.Response.Error}\"."); }
        }

        private static async void OnChannelSubscription(object? sender, OnChannelSubscriptionArgs e)
        {
            string user = e.Subscription.Username;
            int subTier = (int)e.Subscription.SubscriptionPlan - 1; // Not set = 0, Prime = 1, Tier One = 2, Tier Two = 4, and Tier Three = 5.
            if (subTier <= 0) { return; } // This will exclude Prime subs.

            switch (e.Subscription.Context)
            {
                case "sub":
                    if (subTier == 1)
                    {
                        await Program.UpdatePoints(1);
                        tierOneSubs.Add(user);
                    }
                    else if (subTier == 2)
                    {
                        await Program.UpdatePoints(2);
                        tierTwoSubs.Add(user);
                    }
                    else
                    {
                        await Program.UpdatePoints(subTier);
                        tierThreeSubs.Add(user);
                    }
                    break;
                case "resub":
                    var isInTierOne = tierOneSubs.Contains(user);
                    var isInTierTwo = tierTwoSubs.Contains(user);
                    var isInTierThree = tierThreeSubs.Contains(user);

                    switch (subTier)
                    {
                        case 1:
                            if (!isInTierOne)
                            {
                                await Program.UpdatePoints(1); // Resubbing after not being subbed to tier one. Does not handle Prime upgrades.
                                tierOneSubs.Add(user);
                            }
                            break;
                        case 2:
                            if (!isInTierTwo)
                            {
                                if (!isInTierOne) { await Program.UpdatePoints(2); } // Resubbing after not being subbed.
                                else { await Program.UpdatePoints(1); } // Upgrade from tier one to tier two.
                                tierTwoSubs.Add(user);
                            }
                            break;
                        case 3:
                            if (!isInTierThree)
                            {
                                if (isInTierOne) { await Program.UpdatePoints(5); } // Upgrade from tier one to tier three.
                                else if (isInTierTwo) { await Program.UpdatePoints(4); } // Upgrade from tier two to tier three.
                                else { await Program.UpdatePoints(6); } // Resubbing after not being subbed.
                                tierThreeSubs.Add(user);
                            }
                            break;
                    }
                    break;
            }
        }

        private static async Task<(List<string>, List<string>, List<string>)> GetAllSubs()
        {
            var tierOneSubs = new List<string>();
            var tierTwoSubs = new List<string>();
            var tierThreeSubs = new List<string>();
            var doneGettingSubs = false;

            Console.Write("Getting all subscribers... ");

            var getSubscribers = await API.Helix.Subscriptions.GetBroadcasterSubscriptionsAsync(Program.UserSettings.ChannelID, 100);
            while (!doneGettingSubs)
            {
                foreach (var subscriber in getSubscribers.Data)
                {
                    if (!subscriber.IsGift)
                    {
                        if (subscriber.Tier == "1000") { tierOneSubs.Add(subscriber.UserLogin); }
                        else if (subscriber.Tier == "2000") { tierTwoSubs.Add(subscriber.UserLogin); }
                        else if (subscriber.Tier == "3000") { tierThreeSubs.Add(subscriber.UserLogin); }
                    }
                }

                var pagnation = getSubscribers.Pagination.Cursor;
                if (!string.IsNullOrWhiteSpace(pagnation)) { getSubscribers = await API.Helix.Subscriptions.GetBroadcasterSubscriptionsAsync(Program.UserSettings.ChannelID, 100, pagnation); }
                else { doneGettingSubs = true; }
            }

            ConsoleHelper.Success();
            return (tierOneSubs, tierTwoSubs, tierThreeSubs);
        }
    }
}


#region

using FirebaseAdmin.Messaging;

#endregion

public class NotificationRequest
{
    public string DeviceToken { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public IReadOnlyDictionary<string, string> Data { get; set; }
}

namespace api.garagecom.Utils
{
    public class NotificationHelper
    {
        public static async Task<bool> SendNotification(NotificationRequest req)
        {
            if (string.IsNullOrEmpty(req.DeviceToken))
                return false;

            var message = new Message
            {
                Token = req.DeviceToken,
                Notification = new Notification
                {
                    Title = req.Title,
                    Body = req.Body
                },
                Data = req.Data
            };

            try
            {
                var messageId =
                    await FirebaseMessaging.DefaultInstance.SendAsync(message);
                return true;
            }
            catch (FirebaseMessagingException ex)
            {
                return false;
            }
        }
    }
}
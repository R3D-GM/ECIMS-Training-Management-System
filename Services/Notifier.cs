using TCS.Data;
using TCS.Models;

namespace TCS.Services;

public static class Notifier
{
    public static void NotifyUser(ApplicationDbContext db, string userId, string message, string? link = null)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Message = message,
            Link = link,
            CreatedDate = DateTime.Now
        });
    }

    public static void NotifyRole(ApplicationDbContext db, string role, string message, string? link = null)
    {
        db.Notifications.Add(new Notification
        {
            TargetRole = role,
            Message = message,
            Link = link,
            CreatedDate = DateTime.Now
        });
    }
}

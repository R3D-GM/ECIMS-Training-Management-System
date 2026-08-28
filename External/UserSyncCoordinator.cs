using Microsoft.AspNetCore.Identity;
using TCS.Models;

namespace TCS.External;

// "Every user must be saved on the User table, and every role must be
// found/saved on Consignee so they connect via the Role Mapper class."
//
// This is the one place that does that chain - Consignee first (using
// their username as the Code so they can be matched by username), then
// User (linked to that Consignee's Id), then UserRoleMapper (linked to
// that User's Id and their role). Called from account creation AND from
// login, so that even a user created before this integration existed
// still gets synced the next time they log in.
public static class UserSyncCoordinator
{
    public static async Task EnsureUserSyncedAsync(ApplicationUser user, string role, ExternalSyncClient sync, UserManager<ApplicationUser> userManager)
    {
        var changed = false;

        if (user.ExternalConsigneeId is null)
        {
            var consigneeId = await sync.SyncConsigneeAndGetIdAsync(ExternalMapper.ToConsignee(user));
            if (consigneeId is not null)
            {
                user.ExternalConsigneeId = consigneeId;
                changed = true;
            }
        }

        if (user.ExternalConsigneeId is not null && user.ExternalUserId is null)
        {
            var userId = await sync.SyncUserAndGetIdAsync(ExternalMapper.ToUser(user, user.ExternalConsigneeId.Value));
            if (userId is not null)
            {
                user.ExternalUserId = userId;
                changed = true;

                // Only send the role mapping the first time we create the
                // User record over there - no need to resend on every login.
                await sync.SyncUserRoleMapperAsync(ExternalMapper.ToUserRoleMapper(userId.Value, role));
            }
        }

        if (changed)
            await userManager.UpdateAsync(user);
    }
}

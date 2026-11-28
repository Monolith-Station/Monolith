cmd-deadchatshadowban-desc = Shadowbans a player from dead chat. Their dead chat messages are only shown to themselves. Persists across rounds.
cmd-deadchatshadowban-help = Usage: deadchatshadowban <username or user ID> [reason]
cmd-deadchatshadowban-existing = {$username} is already shadowbanned from dead chat.
cmd-deadchatshadowban-added = {$username} has been shadowbanned from dead chat.
cmd-deadchatshadowban-not-found = Unable to find '{$username}'
cmd-deadchatshadowban-arg-player = <username or user ID>
cmd-deadchatshadowban-arg-reason = [reason]

cmd-deadchatunshadowban-desc = Removes a player's dead chat shadowban.
cmd-deadchatunshadowban-help = Usage: deadchatunshadowban <username or user ID>
cmd-deadchatunshadowban-existing = {$username} is not shadowbanned from dead chat.
cmd-deadchatunshadowban-removed = {$username}'s dead chat shadowban has been removed.
cmd-deadchatunshadowban-not-found = Unable to find '{$username}'

cmd-deadchatshadowbans-desc = Lists every player shadowbanned from dead chat.
cmd-deadchatshadowbans-help = Usage: deadchatshadowbans
cmd-deadchatshadowbans-none = Nobody is shadowbanned from dead chat.
cmd-deadchatshadowbans-header = Dead chat shadowbans ({$count}):
cmd-deadchatshadowbans-entry = - {$username} ({$userId}) by {$admin} on {$date}: {$reason}
cmd-deadchatshadowbans-no-reason = no reason given

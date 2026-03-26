cmd-company-not-enough-permissions = You should be admin with WHITELIST flag or be marked as company owner

cmd-company-company-does-not-exist = Company {$company} does not exist.
cmd-company-player-not-found = Player {$player} not found.
cmd-company-hint-player = [player]
cmd-company-hint-company = [company]

command-description-company-addmember = Lets a player play a whitelisted company.
cmd-company-memberadd-already-whitelisted = {$player} is already whitelisted to play as {$companyId} .({$companyName}).
cmd-company-memberadd-added = Added {$player} to the {$companyId} ({$companyName}) whitelist.

command-description-company-playercompanies = Gets all the companies that a player has been whitelisted for.
cmd-company-playercompanies-whitelisted-none = Player {$player} is not whitelisted for any companies.
cmd-company-playercompanies-whitelisted-for = Player {$player} is whitelisted for:
{$companies}

command-description-company-members = Gets all the players that has been whitelisted for a company.
cmd-company-members-whitelisted-none = Company {$company} doesn't have any whitelisted players.
cmd-company-members-whitelisted-for = Company {$company} is whitelisted for:
    {$players}
    {$company} owners:
    {$owners}

cmd-company-setowner-success = Successfully changed owner status of {$player} to {$status}

command-description-company-removemember = Removes a player's ability to play a whitelisted company.
cmd-company-memberremove-was-not-whitelisted = {$player} was not whitelisted to play as {$companyId} ({$companyName}).
cmd-company-memberremove-removed = Removed {$player} from the whitelist for {$companyId} ({$companyName}).

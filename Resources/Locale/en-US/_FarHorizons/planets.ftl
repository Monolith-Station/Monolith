planet-descent-no-surface = Failed to locate safe landing position
planet-descent-surface-load-failed = The surface for this planet failed to load. [color=red]All online administrators have already been notified.[/color]
planet-surface-load-failed-admin = DEFCON 2 - Planet {$planet} has failed to load; landing has been disabled! Please contact the hosts and request the log to get this fixed.
planet-save-failed-admin = DEFCON 3 - Planet key {$planet} failed to save during the most recent planet save interval! Please contact the hosts and request the log to get this fixed.
planet-save-success-admin = Planet {$planet} saved. Next save: {$next}
planet-save-next-none = not scheduled
planet-save-next-days = { $count ->
    [one] {$count} day
   *[other] {$count} days
}
planet-save-next-hours = { $count ->
    [one] {$count} hour
   *[other] {$count} hours
}
planet-save-next-minutes = { $count ->
    [one] {$count} minute
   *[other] {$count} minutes
}
planet-save-command-ok = Planet {$planet} saved.
planet-save-command-not-loaded = No loaded planet named {$planet}.
planet-save-command-ambiguous = More than one loaded planet is named {$planet}.
planet-save-command-failed = Could not save {$planet}. Check that persistence is enabled, the round is active, and the planet has a loaded persistent surface.
planet-save-delete-not-found = No matching planet save found.
planet-save-delete-ambiguous = More than one save matches that round ID. Nothing was deleted.
planet-save-delete-current = Cannot delete this round's save before its final save.
planet-save-delete-confirm = This will permanently delete {$folder}. Repeat the command with true at the end to confirm.
planet-save-delete-ok = Deleted {$folder}.
planet-save-delete-failed = Could not delete the planet save. Check the persistence::planet log.
planet-descent-too-far = Move closer to descend
planet-descent-unavailable = Unable to locate area to land
planet-descent-respooling = ERROR: Thrusters not responding

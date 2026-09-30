# List of matchers that are always illegal to use. These always supercede CONDITIONALLY_ILLEGAL_MATCHES.
ILLEGAL_MATCHES = [
    "DO NOT MAP",
    "DoNotMap",
    "DEBUG",
    "Admeme",
    "EncryptionKeyCommand",
    "SurveillanceCameraWireless",
    "APCHighCapacity",
    "APCSuperCapacity",
    "APCHyperCapacity",
    "PDA",
    "SpawnPointPassenger",
    "SalvageShuttleMarker",
    "FTLPoint",
]
# List of specific legal entities that override the above.  Does not check suffixes.
LEGAL_OVERRIDES = [
    "ButtonFrameCautionSecurity", # red button
    "PosterLegitPDAAd",
    "ShowcaseRobot" # decoration
]
# List of matchers that are illegal to use, unless the map is a ship and the ship belongs to the keyed shipyard.
CONDITIONALLY_ILLEGAL_MATCHES = {
    "Shipyard": [
    ],
    "Scrap": [
    ],
    "Expedition": [
    ],
    "Custom": [
    ],
    "Security": [
    ],
    "Syndicate": [
    ],
    "BlackMarket": [
    ],
    "Sr": [
    ],
    "Medical": [
    ],
    "Ussp": [
    ],
    # It is assumed that mapped instances of plastitanium, security gear, etc. are deemed acceptable
    "PointOfInterest": [
        "WallPlastitaniumIndestructible",
        "WallPlastitaniumDiagonalIndestructible",
        "PlastititaniumWindowIndestructible",
        "PlastititaniumWindowDiagonalIndestructible",
        "ClosetMaintenanceFilledRandom",
        "ClosetWallMaintenanceFilledRandom",
    ]
}

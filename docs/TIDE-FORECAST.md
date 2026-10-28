# Harbor tide forecast

The existing exploration conditions panel in Mist Harbor shows seconds until the next real tide change. Low tide reports how long the shoals remain open; high tide reports the wait for ebb and retains the sluice hint. This does not add a combat HUD panel.

The forecast reads the original world clock: each 36-second cycle has 22 seconds of low tide. A live sluice keeps the shoals open until its existing deadline. If that deadline falls during natural low tide, the countdown continues until the next natural high tide instead of claiming the route closes at the gate deadline. Positive fractions round up, so an open crossing never reports zero seconds prematurely.

Queries and drawing do not advance time, open gates or spend materials. Menus, application pause and hit stop retain the original simulation clock. The original 90-second paid sluice, 60-second exploration power, two-crystal sluice cost, tide collisions and safe-foot recovery are unchanged. No save fields or resources are added; old saves reconstruct the forecast from their existing clocks.

Core checks compare predicted boundaries to the original `LowTide` state and exercise the original gate transactions. Runtime checks exercise production Update/OnGUI, pauses, the real sluice, legacy saves and bilingual normal/large layouts. These offline checks remain **NATIVE_UNVERIFIED**; native Unity rendering and gameplay acceptance have not been established.

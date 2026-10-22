# Traffic Stress Lab: 14 October 2026

User report: presets activate and the submitted-request counter grows, but too
little traffic appears. The supplied screenshot shows 25x and 110,496 submitted
requests. The running local mod log identifies the prior 1.5.5-beta.1 assembly.

## Root cause verified in installed Game.dll

`RandomTrafficDispatchSystem.ValidateTarget` requires a destination spawner's
`TrafficSpawner.m_TrafficRequest` to match the request being dispatched. If it
already owns a live random-traffic request, competing requests are rejected.
The old implementation cloned requests and replayed templates against that same
target, so its submitted counter included work that could not become traffic.

The installed `TrafficSpawnerAISystem` consumes `ServiceDispatch` entries and
spawns from their approved `PathInformation`/`PathElement` data. It already supports
multiple spawns per request during loading. `ServiceRequestSystem` aggregates
completion events by request before deleting the completed request.

## Candidate correction: 1.5.7-beta.1

Amplify eligible native road dispatch entries immediately before the native
spawner, with its exact update interval/offset. Keep the original request, target
ownership and path checks. Restrict amplification to car routes and valid road
spawners. Snapshot original buffer length and skip duplicate originals to avoid
recursive amplification. Use a bounded shared batch budget and rotate source
priority. Remove old cloned requests rather than continuing the rejected queue.

2x adds one departure per approved route; 5x adds four; 10x adds nine; 25x adds 24.
The configured cap can reduce this effective rate. Gentle reaches the cap over
eight spawn batches, Fast over three, Extreme immediately. Stop returns to vanilla
departures; spawned vehicles remain under native lifecycle control. A rapid
Stop/Start still resets the ramp. No building, citizen-trip or service-trip edits.

The panel reports extra departures queued, observed road vehicle entities, ready
routes and cap pressure. The queued count is not a confirmed spawned-car count.
Live preset switches are available. The split running-heading text shown in the
screenshot has also been simplified. Visual appearance remains unverified.

## Evidence

- Current game log: 1.6.2f1 (767.21d1) [6300.26419], Windows Steamworks.
- Official toolchain Debug and Release: zero warnings/errors.
- Webpack/UI builds: successful, no warnings.
- `dotnet run --project scripts/TrafficStressTests/TrafficStressTests.csproj`:
  56 offline assertions covering preset outputs, caps, ramps, Stop/Reset,
  rapid restart and city transition state. These do not execute native ECS jobs.
- Diff checked; installed native dispatch/spawn/completion implementation inspected.
- Release installed in the existing local TrafficStressTester mod directory;
  DLL/UI/manifest SHA-256 values match staging. Assembly version is 1.5.7.0.
  Previous installation backed up locally. Same Cities2 process remained running;
  the current session retains the previous loaded assembly until restart.
- No PDX publication and no runtime density or compatibility pass claimed.

The user explicitly requested keeping the current game session running. No
Computer Use, restart or city modification occurred. Remaining runtime gate:
compare vanilla/2x/5x/10x/25x on the same connected disposable city, observe native
departures and vehicle count over several batches, inspect logs, verify Stop drains
normally, and check roads not on random through-traffic routes are not misrepresented.

## Rebuild check: 22 October 2026

- Re-ran the Traffic Stress Lab offline suite: 56 assertions passed.
- Rebuilt Debug and Release: both completed with zero warnings and zero errors.
- Release DLL source and deployed DLL SHA-256 match:
  `87808038A447E96C17CDF477D4F16683790D23AF7C85C07FE5EC85374363EABD`.
- UI bundle source and deployed UI bundle SHA-256 match:
  `548F4DA2CD0634ECCCA28ED0D630079A51FB9EC703D260AA3DB5DDB781115A70`.
- The local candidate is current and deployed. The game was not restarted or
  interacted with during this check, so runtime traffic volume and preset
  effectiveness remain **not tested in this pass**. No PDX update was made.

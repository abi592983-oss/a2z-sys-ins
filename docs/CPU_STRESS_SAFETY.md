# CPU Stress-Test Safety Supervisor

## Purpose

The CPU stress test is technician opt-in only. Its safety supervisor is deliberately separate from the general diagnostic scoring model.

The supervisor samples CPU telemetry at approximately 200 ms and uses sustained evidence rather than a single clock sample for frequency-anomaly decisions.

## Preflight refusal conditions

The stress test is refused when:

- CPU temperature telemetry is unavailable or invalid.
- CPU clock telemetry is unavailable.
- CPU load telemetry is unavailable.
- Preflight temperature is 80 °C or higher.
- Preflight clock telemetry is unstable by more than 50% across the short baseline window.

Idle clock changes are not automatically treated as faults. Modern CPUs legitimately change frequency because of power management and boost behavior.

## Immediate abort conditions

The stress load is stopped immediately when:

- CPU temperature reaches 90 °C.
- CPU temperature reaches the 87 °C early-abort threshold.
- CPU temperature telemetry is lost or becomes invalid.
- CPU clock telemetry is lost during stress.
- CPU load telemetry is lost during stress.
- Clock telemetry becomes physically implausible.
- Sustained clock collapse is detected under high load.
- Sustained abnormal clock surge is detected under high load.
- Telemetry appears stale while the CPU is under active load.

## Anti-noise behavior

A single implausible-looking clock sample does not cause an immediate frequency abort. The supervisor keeps a short rolling history and requires sustained evidence before declaring clock collapse or surge.

The stress clock baseline is established from the first few samples after load begins. This prevents a normal idle-to-boost transition such as 800 MHz -> 3500 MHz from being incorrectly classified as an overclock event.

## Evidence retained

Every accepted stress sample contains:

- capture timestamp
- elapsed milliseconds
- target load stage
- CPU temperature
- observed CPU load
- average and maximum core clock
- fan speed when available
- safety validity/assessment

No historical samples are invented or interpolated.

## Important limitation

No software-only monitor can guarantee physical safety. This design is intentionally conservative and designed to fail closed when required telemetry becomes unavailable. A completed 60-second test is evidence about that recorded workload and is not a guarantee of long-term stability.
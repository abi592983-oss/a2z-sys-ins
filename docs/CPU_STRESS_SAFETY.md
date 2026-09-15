# CPU Stress-Test Safety Supervisor

## Purpose

The CPU stress test is technician opt-in only. Its safety supervisor is deliberately separate from the general diagnostic scoring model.

Safety telemetry is polled at up to 50 ms (20 samples/second). The design separates **polling frequency**, **sensor freshness**, **decision frequency**, and **graph refresh frequency**. CPU safety remains on the fast 50 ms ceiling; slower sensors are tracked independently so repeated values are not mistaken for fresh sensor changes.

## Adaptive sensor cadence

Each telemetry channel learns its observed update cadence from timestamped value changes:

- CPU load
- CPU clock
- CPU temperature
- memory load
- GPU load
- GPU temperature

A sensor value can be read repeatedly without claiming that the sensor produced a new measurement. The learned cadence is retained in the sample's telemetry-freshness text. During active stress the safety poll never slows below the 50 ms target, because polling faster than a hardware/provider update rate cannot manufacture faster physical telemetry.

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
- Memory or GPU telemetry, when present, becomes physically implausible.
- Sustained clock collapse is detected under high load.
- Sustained abnormal clock surge is detected under high load.

Unchanged telemetry alone is **not** treated as a failure. This avoids aborting a safe test simply because a temperature provider updates more slowly than the 50 ms polling loop.

## Anti-noise behavior

A single implausible-looking clock sample does not cause an immediate frequency abort. The supervisor keeps a short rolling history and requires sustained evidence before declaring clock collapse or surge.

The stress clock baseline is established from the first few samples after load begins. This prevents a normal idle-to-boost transition such as 800 MHz -> 3500 MHz from being incorrectly classified as an overclock event.

## Live graphs and report evidence

The report workspace displays live, Task-Manager-style telemetry while the technician runs the stress test:

- CPU utilization
- memory utilization
- GPU utilization when the machine/provider exposes it
- CPU temperature
- CPU average clock

The same timestamped samples are retained in `CpuStressResult.Samples`. The final report preview uses those captured samples; it does not generate or interpolate historical values. If GPU telemetry is unavailable, the graph leaves GPU history absent rather than inventing a zero or estimated series.

## Evidence retained

Every accepted stress sample contains:

- capture timestamp
- elapsed milliseconds
- target load stage
- CPU temperature
- observed CPU load
- average and maximum core clock
- fan speed when available
- memory utilization when available
- GPU load/temperature when available
- observed telemetry polling interval
- sensor cadence/freshness description
- safety validity/assessment

## Important limitation

No software-only monitor can guarantee physical safety. This design is intentionally conservative and designed to fail closed when required CPU telemetry becomes unavailable. A completed 60-second test is evidence about that recorded workload and is not a guarantee of long-term stability.
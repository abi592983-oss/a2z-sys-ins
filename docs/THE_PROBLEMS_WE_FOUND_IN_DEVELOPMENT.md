

### DEV-014 — SSD life attribute used vendor raw counter instead of normalized percentage
- **Problem:** On the HP HS-SSD-WAVE(S) 256G, the E7 SSD Life Left SMART row had normalized value 64 but raw value 36. The interpretation code used the raw value and reported 0% remaining / 100% used instead of 64% remaining.
- **Date identified:** 2026-09-15
- **Area:** Storage / SMART interpretation / endurance
- **Impact:** A valid SSD endurance indicator was misreported on a real device.
- **Status:** Fixed in code; repeat physical verification pending
- **Date fixed:** 2026-09-15
- **Ever fixed:** Yes
- **Fix / evidence:** Life attributes now prefer the normalized SMART value when available because the raw field is vendor-specific. Raw evidence remains preserved. A regression fixture was added for the real E7 representation.
- **Validation:** Synthetic regression test added. Same HP machine must be rerun before Pass 10 closes.

### DEV-015 — Storage selection UI and legacy-looking controls conflict with technician workflow
- **Problem:** The storage selector used a bright/default selection treatment and the controls had an old WPF/1980s-1990s appearance.
- **Date identified:** 2026-09-15
- **Area:** UI / usability / visual design
- **Impact:** Selected storage text could become unreadable against the dark/green theme, and technicians had to perform unnecessary manual storage interaction.
- **Status:** Fixed in UI branch
- **Date fixed:** 2026-09-15
- **Ever fixed:** Yes
- **Fix / evidence:** Removed the manual storage selector/window and launch button. The main console now uses a modern dark UI, Segoe UI typography, rounded controls, dark selection states and green accents. CPU staged testing remains technician-controlled and optional.
- **Validation:** XAML/code change complete; CI/runtime validation pending.

# Third-party notices

This application package includes `smartctl` 7.5 from the smartmontools project.

- Project and licence information: https://www.smartmontools.org/
- Official release: https://sourceforge.net/projects/smartmontools/files/smartmontools/7.5/
- Licence: GNU General Public License, version 2 or later
- Corresponding source: `tools/smartmontools-7.5-source.tar.gz` in the packaged build

LibreHardwareMonitorLib 0.9.6 and Newtonsoft.Json are also distributed in the application package. Their licence files and project metadata are available through their NuGet packages and upstream projects.

The portable package also carries the official PawnIO 2.2.0 installer at `tools/pawnio/2.2.0/PawnIO_setup.exe`. PawnIO provides the privileged hardware-access path used by current LibreHardwareMonitor releases. Inspector does not silently downgrade to older vulnerable low-level drivers when this path is unavailable; failure is recorded and safer/documented fallbacks should be used instead.

- PawnIO official release repository: https://github.com/namazso/PawnIO.Setup
- Bundled version: 2.2.0
- Bundled installer SHA-256: `1f519a22e47187f70a1379a48ca604981c4fcf694f4e65b734aaa74a9fba3032`

## CrystalDiskInfo SMART interpretation reference

A2Z System Inspector's storage interpretation work was informed by the public CrystalDiskInfo source repository:

- Project: https://github.com/hiyohiyo/CrystalDiskInfo
- Licence: MIT License
- Use in A2Z: reference/interpretation design; no CrystalDiskInfo executable is bundled.

The Inspector does not assume that a SMART attribute ID has a universal meaning. In particular, attribute C5/197 is only treated as a pending-sector count when the structured SMART evidence supplies a matching validated attribute name. Vendor-specific/unknown attributes remain raw evidence and are excluded from failure scoring until their semantics are validated.

# Study Workflow — Operator Guide

Step-by-step instructions for setting up and running a data collection session. Follow the steps in order. Each section flags what to do if something goes wrong.

---

## Prerequisites

### One-time setup (do once per machine)

1. **Install Python dependencies**
   ```
   pip install pyserial pupil-labs-realtime-api
   ```
   `beamngpy` should already be installed at `C:\SimulatorPlatform\anaconda3`.

2. **Install TanvasTouch Engine**
   The Tanvas Engine runs as a Windows service. Confirm it is installed and set to start automatically. Without it, the WPF app will exit on launch.

3. **Flash Arduino firmware**
   Open `arduino/knob.ino` in the Arduino IDE and upload to the board. Verify the encoder sends `{"delta": 1}` / `{"delta": -1}` in the Serial Monitor before proceeding.

4. **Verify `Python/config.py`**
   Confirm these values match your machine:
   | Setting | Default | Notes |
   |---|---|---|
   | `BEAMNG_HOME` | `C:/SimulatorPlatform/BeamNG.tech.v0.38.3.0` | Path to BeamNG installation |
   | `BEAMNG_USER` | `C:/Users/bwander/AppData/Local/BeamNG/BeamNG.tech/current` | BeamNG user folder |
   | `ARDUINO_PORT` | `COM7` | Check Device Manager if different |
   | `NEON_HOST` | `None` (auto-discover) | Set to IP string if mDNS fails |

---

## Session Setup (before each participant)

### Step 1 — Hardware

- [ ] Plug in the **Arduino knob** via USB. Confirm the correct COM port in Device Manager if needed; update `ARDUINO_PORT` in `config.py`.
- [ ] Place the **TanvasTouch tablet** on the mount. Confirm the TanvasTouch Engine service is running (check Windows Services or the Tanvas system tray icon).
- [ ] Power on the **Neon Pupil** eye tracker and open the **Neon Companion app** on its paired device. Confirm it is on the same Wi-Fi network as the study PC. Note the device IP if mDNS auto-discovery has been unreliable.
- [ ] Confirm the **steering wheel / input device** is connected if used.

### Step 2 — Launch BeamNG

1. Launch **BeamNG.tech** manually from `C:\SimulatorPlatform\BeamNG.tech.v0.38.3.0\BeamNG.drive.exe`.
2. Wait for the main menu to fully load. The Python script will connect to the already-running instance.
   > The script can also launch BeamNG itself via `bng.open()`, but launching manually first gives you a chance to verify the game is working before the session starts.

### Step 3 — Launch TanvasTouch WPF App

1. Open `tanvas_knob/TanvasTouchHapticKnob.sln` in Visual Studio and run (F5), **or** run the pre-built executable at:
   ```
   tanvas_knob\bin\Debug\netcoreapp3.1\TanvasTouchHapticKnob.exe
   ```
2. The app window opens on the TanvasTouch display. The cursor is hidden. Confirm the knob renders and haptic feedback is active by touching and rotating the nub.
   > If the app exits immediately, the TanvasTouch Engine is not running.

### Step 4 — Fit Eye Tracker

1. Fit the Neon eye tracker on the participant following the Pupil Labs calibration procedure.
2. Confirm a live gaze stream is visible in the Neon Companion app before proceeding.

---

## Running a Session

### Step 5 — Start the Python script

From the repo root:
```
cd C:\SimulatorPlatform\DrivingStudy-TanvasTouch
python Python\study.py
```

You will be prompted for:
```
Participant ID: 01
Starting condition [['tanvas', 'physical']]: tanvas
```

The script will then:
1. Connect to BeamNG, load the `west_coast_usa` scenario, and spawn the vehicle
2. Connect to the Arduino on COM7 (retries silently if not found)
3. Search for the Neon device on the local network (up to 10 s)
4. Print the session data path and drop into the researcher CLI

Expected output when all components connect:
```
Session data: data\P01_2026-05-12_143022
Connecting to BeamNG...
[BeamNG] Started — scenario loaded, polling at 20 Hz
Connecting to Arduino...
[Arduino] Connected on COM7
Connecting to eye tracker...
[EyeTracker] Searching for Neon device on local network (up to 10 s)...
[EyeTracker] Connected to Neon device

Commands: start | stop | condition <name> | note <text> | status | quit

>
```

> If a component fails to connect, it logs a warning and continues — the session proceeds without that stream. Run `status` to confirm which components are live before starting trials.

### Step 6 — Run trials

Use the researcher CLI to control trial flow:

| Command | When to use |
|---|---|
| `start` | Participant is ready and driving — begins a trial |
| `stop` | Trial task is complete or interrupted |
| `condition tanvas` | Switch to the TanvasTouch haptic condition |
| `condition physical` | Switch to the physical knob condition |
| `note <text>` | Log any observation (e.g., `note participant asked question`) |
| `status` | Check component health and current trial number at any time |
| `quit` | End the session after all trials are complete |

**Typical trial sequence:**
```
> condition tanvas         # confirm starting condition
> start                    # participant begins trial
  ... participant drives and interacts with the knob ...
> stop                     # trial complete
> note short break         # optional
> condition physical       # switch condition if counterbalancing
> start                    # next trial
> stop
> quit                     # end session
```

### Step 7 — End the session

Type `quit` at the prompt. The script will:
1. Stop all data streams
2. Write a `session_end` marker to `events.csv`
3. Flush and close all CSV files
4. Disconnect from BeamNG
5. Print the path to the session data folder

```
> quit
Shutting down...
[BeamNG] Disconnected
Data saved to: data\P01_2026-05-12_143022
```

---

## Data Output

Session data is saved to `data/P{id}_{datetime}/` relative to the repo root:

```
data/
└── P01_2026-05-12_143022/
    ├── beamng_vehicle.csv    # vehicle telemetry + lane position at ~20 Hz
    ├── arduino_knob.csv      # encoder events (delta + accumulated position)
    ├── eyetracker_gaze.csv   # gaze x/y + confidence
    └── events.csv            # trial markers, condition changes, notes
```

All timestamps use `time.monotonic()` (seconds since script start) as the shared clock across all four files — use `events.csv` to segment the other streams into trials.

`beamng_vehicle.csv` columns relevant to SDLP analysis:

| Column | Source | Description |
|---|---|---|
| `lane_offset_m` | `RoadsSensor.dist2CL` | Signed lateral distance from road centreline in metres. Positive = right of centre, negative = left. This is the raw value for computing SDLP. |
| `heading_angle_rad` | `RoadsSensor.headingAngle` | Angle between vehicle heading and road tangent in radians. Use with `lane_offset_m` for heading-corrected lane position analysis. |

**SDLP calculation (post-processing):**
```python
import pandas as pd

df = pd.read_csv('data/P01_.../beamng_vehicle.csv')
# Segment by trial using events.csv, then per trial:
sdlp = df['lane_offset_m'].std()
```
`lane_offset_m` will be `None` if the vehicle is off any mapped road. Filter these rows before computing SDLP.

---

## Troubleshooting

### BeamNG fails to connect
- Confirm BeamNG is running and at the main menu (not mid-load).
- Check that no firewall is blocking `localhost:25252`.
- The script will raise an exception and print the error — fix the issue and restart.

### Arduino not detected
- Check Device Manager for the correct COM port and update `ARDUINO_PORT` in `config.py`.
- The reader thread will retry every 5 s — you can plug in the Arduino after the script starts and it will connect automatically.

### Eye tracker not found
- Confirm the Neon Companion app is open and the device is on the same network.
- If mDNS auto-discovery fails repeatedly, find the device IP in the Neon Companion app and set `NEON_HOST = '192.168.x.x'` in `config.py`.
- The session continues without gaze data if the tracker is unavailable.

### TanvasTouch app exits immediately
- The TanvasTouch Engine service is not running. Start it from Windows Services (`services.msc`) or the Tanvas system tray utility.

### TanvasTouch app has no haptic feedback
- Confirm the tablet is connected via USB (not just power). The haptic engine communicates over USB.
- Restart the TanvasTouch Engine service, then relaunch the app.

### Script crashes mid-session
- All CSV files are line-buffered — each row is written to disk immediately. Data up to the crash point is intact.
- Restart the script with the same participant ID and a new trial number. Use `note` to log the interruption.

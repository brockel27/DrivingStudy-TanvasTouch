# DrivingStudy-TanvasTouch

A driving simulator study investigating whether haptic feedback on touchscreens reduces visual distraction compared to a physical rotary knob. Participants drive in a simulated environment (BeamNG) while completing a secondary-task knob interaction, with gaze behavior measured via eye tracking.

---

## Project Overview

The study uses an experimental design with two interface conditions:

- **TanvasTouch (haptic)** — a 10-section rotary knob rendered on a Mimo Vue HD TanvasTouch tablet. Ultrasonic haptic feedback provides tactile cues for the knob's position and detent, allowing the driver to interact without looking.
- **Physical knob (baseline)** — a physical rotary encoder (Arduino) providing mechanical tactile feedback as a comparison condition.

The central research question is whether haptic touchscreen feedback can substitute for the physical affordances of a hardware knob, reducing the need for visual attention during secondary-task interaction while driving.

---

## Repository Structure

```
DrivingStudy-TanvasTouch/
├── arduino/          # Arduino firmware for the physical rotary encoder knob
├── Python/           # Python study orchestration scripts
└── tanvas_knob/      # C# WPF application for the TanvasTouch haptic knob interface
```

---

## Components

### `tanvas_knob/` — TanvasTouch Haptic Knob (C# / WPF)

A .NET Core 3.1 WPF application that runs on the Mimo Vue HD TanvasTouch tablet and renders a 10-section rotary knob with multimodal feedback:

- **Touch input** — detects finger acquisition of the knob nub and tracks rotation
- **Haptic feedback** — ultrasonic haptic textures via the Tanvas SDK (v5.0.5); rotation texture while held, nub texture while idle
- **Visual feedback** — animated section markers indicate the current and target sections
- **Audio feedback** — plays a cue when the participant reaches the target section

Open `tanvas_knob/TanvasTouchHapticKnob.sln` in Visual Studio to build. Requires the TanvasTouch Engine service to be running on the host machine.

**Key files:**
| File | Purpose |
|---|---|
| `HapticKnob.cs` | Core interaction state machine (touch → rotation → haptics → snap) |
| `AutoLerp.cs` | Smooth snap-to-section animation (300 ms ease-out) |
| `MainWindow.xaml` | UI layout (400×400 canvas, 10 section markers) |
| `assets/` | Knob graphics (PNGs at 1× and 1.25× DPI) and target audio cue |

---

### `arduino/` — Physical Rotary Encoder Knob (Arduino / C++)

Arduino firmware for a rotary encoder with button, used as the physical knob baseline condition. Sends encoder events over serial as line-delimited JSON.

**Serial protocol:** 9600 baud, sends `{"delta": 1}` or `{"delta": -1}` per detent.

**Key files:**
| File | Purpose |
|---|---|
| `knob.ino` | Encoder read loop using ClickEncoder + Timer1 interrupt |

---

### `Python/` — Study Orchestration (Python)

Python scripts that connect all study components, manage trial flow, and log synchronized data from every source to CSV.

**Run a session:**
```
pip install pyserial pupil-labs-realtime-api
# beamngpy must already be installed
cd DrivingStudy-TanvasTouch
python Python/study.py
```

**Researcher CLI commands (during a session):**
| Command | Action |
|---|---|
| `start` | Begin a trial (auto-increments trial counter) |
| `stop` | End the current trial |
| `condition tanvas` / `condition physical` | Switch active condition |
| `note <text>` | Log a free-text event marker |
| `status` | Print component health and current trial state |
| `quit` | End the session and close all files |

**Data output** — written to `data/P{id}_{datetime}/`:
| File | Contents |
|---|---|
| `beamng_vehicle.csv` | Position, speed, steering, throttle, brake, gear at ~20 Hz |
| `arduino_knob.csv` | Encoder delta events and accumulated knob position |
| `eyetracker_gaze.csv` | Gaze x/y and confidence from Neon Pupil |
| `events.csv` | Trial start/stop, condition changes, notes, session markers |

**Key files:**
| File | Purpose |
|---|---|
| `study.py` | Entry point and researcher CLI |
| `config.py` | All configurable parameters (ports, paths, conditions) |
| `beamng_manager.py` | BeamNG connection, scenario setup, telemetry polling thread |
| `arduino_manager.py` | Serial reader thread with auto-reconnect |
| `eyetracker_manager.py` | Neon Pupil gaze streaming thread |
| `data_logger.py` | Thread-safe CSV writer for all four data streams |
| `knob_test.py` | Standalone Arduino serial test script |

---

## Hardware Requirements

| Component | Details |
|---|---|
| Driving simulator PC | Runs BeamNG.tech, Python scripts, and TanvasTouch WPF app |
| Mimo Vue HD TanvasTouch | 10" capacitive + ultrasonic haptic display; requires TanvasTouch Engine |
| Arduino (Uno or compatible) | Rotary encoder wired to A0 (CLK), A1 (DT), A2 (SW) |
| Pupil Labs Neon | Eye tracker; Neon Companion app must be on the same local network |

---

## AI Usage Disclaimer

Portions of this codebase were developed with assistance from [Claude](https://claude.ai) (Anthropic), an AI assistant. AI-generated code has been reviewed and integrated by the project team. All study design decisions, experimental parameters, and research interpretations remain the responsibility of the human researchers.

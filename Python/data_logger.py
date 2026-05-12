import csv
from pathlib import Path


class DataLogger:
    def __init__(self, session_dir: Path):
        self._dir = session_dir
        self._files = {}
        self._writers = {}

    def open(self):
        self._dir.mkdir(parents=True, exist_ok=True)
        schemas = {
            'beamng_vehicle':  ['timestamp_mono', 'pos_x', 'pos_y', 'pos_z',
                                 'speed_ms', 'steering', 'throttle', 'brake', 'gear',
                                 'lane_offset_m', 'heading_angle_rad'],
            'arduino_knob':    ['timestamp_mono', 'delta', 'position'],
            'eyetracker_gaze': ['timestamp_mono', 'gaze_x', 'gaze_y', 'confidence'],
            'events':          ['timestamp_mono', 'event', 'value'],
        }
        for name, headers in schemas.items():
            f = open(self._dir / f'{name}.csv', 'w', newline='', buffering=1)
            writer = csv.writer(f)
            writer.writerow(headers)
            self._files[name] = f
            self._writers[name] = writer

    def close(self):
        for f in self._files.values():
            f.flush()
            f.close()
        self._files.clear()
        self._writers.clear()

    def log_beamng(self, t, pos_x, pos_y, pos_z, speed_ms, steering, throttle, brake, gear,
                   lane_offset_m=None, heading_angle_rad=None):
        self._writers['beamng_vehicle'].writerow(
            [t, pos_x, pos_y, pos_z, speed_ms, steering, throttle, brake, gear,
             lane_offset_m, heading_angle_rad])

    def log_arduino(self, t, delta, position):
        self._writers['arduino_knob'].writerow([t, delta, position])

    def log_gaze(self, t, gaze_x, gaze_y, confidence):
        self._writers['eyetracker_gaze'].writerow([t, gaze_x, gaze_y, confidence])

    def log_event(self, t, event, value=''):
        self._writers['events'].writerow([t, event, value])

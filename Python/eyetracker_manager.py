import time
import threading

import config
from data_logger import DataLogger


class EyeTrackerManager:
    def __init__(self, logger: DataLogger, shutdown: threading.Event):
        self._logger = logger
        self._shutdown = shutdown
        self._device = None
        self._thread = None
        self.connected = False

    def start(self):
        try:
            from pupil_labs.realtime_api.simple import discover_one_device
        except ImportError:
            print('[EyeTracker] pupil-labs-realtime-api not installed — skipping. '
                  'Run: pip install pupil-labs-realtime-api')
            return

        try:
            if config.NEON_HOST:
                from pupil_labs.realtime_api.simple import Device
                self._device = Device(config.NEON_HOST, config.NEON_PORT)
            else:
                print('[EyeTracker] Searching for Neon device on local network '
                      '(up to 10 s)...')
                self._device = discover_one_device(max_search_duration_s=10)
            self.connected = True
            print(f'[EyeTracker] Connected to Neon device')
        except Exception as e:
            self._logger.log_event(time.monotonic(), 'hardware_disconnect', 'eyetracker')
            print(f'[EyeTracker] Could not connect: {e} — eye tracking disabled')
            return

        self._thread = threading.Thread(target=self._stream_loop, name='eyetracker-stream',
                                        daemon=True)
        self._thread.start()

    def stop(self):
        if self._thread:
            self._thread.join(timeout=3)
        if self._device:
            try:
                self._device.close()
            except Exception:
                pass
        self.connected = False

    def _stream_loop(self):
        try:
            for gaze in self._device.receive_gaze_datum():
                if self._shutdown.is_set():
                    break
                t = time.monotonic()
                confidence = 1.0 if gaze.worn else 0.0
                self._logger.log_gaze(t, gaze.x, gaze.y, confidence)
        except Exception as e:
            self._logger.log_event(time.monotonic(), 'hardware_disconnect', 'eyetracker')
            self.connected = False
            print(f'[EyeTracker] Stream error: {e}')

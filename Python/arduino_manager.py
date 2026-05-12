import json
import time
import threading

import serial
from serial import SerialException

import config
from data_logger import DataLogger


class ArduinoManager:
    def __init__(self, logger: DataLogger, shutdown: threading.Event):
        self._logger = logger
        self._shutdown = shutdown
        self._ser = None
        self._position = 0
        self._thread = None
        self.connected = False

    def start(self):
        try:
            self._ser = serial.Serial(config.ARDUINO_PORT, config.ARDUINO_BAUD,
                                      timeout=config.ARDUINO_TIMEOUT)
            time.sleep(2)  # wait for Arduino reset after serial connect
            self.connected = True
            print(f'[Arduino] Connected on {config.ARDUINO_PORT}')
        except SerialException as e:
            print(f'[Arduino] Could not open {config.ARDUINO_PORT}: {e} — will retry')

        self._thread = threading.Thread(target=self._reader_loop, name='arduino-reader',
                                        daemon=True)
        self._thread.start()

    def stop(self):
        if self._thread:
            self._thread.join(timeout=3)
        if self._ser and self._ser.is_open:
            self._ser.close()
        self.connected = False

    @property
    def position(self) -> int:
        return self._position

    def _reader_loop(self):
        while not self._shutdown.is_set():
            if not self._ser or not self._ser.is_open:
                try:
                    self._ser = serial.Serial(config.ARDUINO_PORT, config.ARDUINO_BAUD,
                                              timeout=config.ARDUINO_TIMEOUT)
                    time.sleep(2)
                    self.connected = True
                    print(f'[Arduino] Reconnected on {config.ARDUINO_PORT}')
                except SerialException:
                    time.sleep(config.ARDUINO_RETRY_S)
                    continue

            try:
                line = self._ser.readline().decode('utf-8').strip()
                if line:
                    data = json.loads(line)
                    self._position += data['delta']
                    self._logger.log_arduino(time.monotonic(), data['delta'], self._position)
            except (SerialException, OSError):
                self._logger.log_event(time.monotonic(), 'hardware_disconnect', 'arduino')
                self.connected = False
                print('[Arduino] Disconnected — will retry')
                if self._ser:
                    try:
                        self._ser.close()
                    except Exception:
                        pass
                self._ser = None
            except (json.JSONDecodeError, KeyError):
                pass  # malformed line — skip silently

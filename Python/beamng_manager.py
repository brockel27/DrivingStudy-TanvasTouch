import math
import time
import threading

from beamngpy import BeamNGpy, Scenario, Vehicle
from beamngpy.sensors import Electrics

import config
from data_logger import DataLogger


class BeamNGManager:
    def __init__(self, logger: DataLogger, shutdown: threading.Event):
        self._logger = logger
        self._shutdown = shutdown
        self._bng = None
        self._ego = None
        self._thread = None
        self.connected = False

    def start(self):
        self._bng = BeamNGpy(
            config.BEAMNG_HOST, config.BEAMNG_PORT,
            home=config.BEAMNG_HOME, user=config.BEAMNG_USER,
        )
        self._bng.open()

        scenario = Scenario(config.BEAMNG_MAP, config.BEAMNG_SCENARIO_NAME)
        self._ego = Vehicle(config.VEHICLE_ID, model=config.VEHICLE_MODEL,
                            license=config.VEHICLE_LICENSE)

        game_state = self._bng.get_gamestate()
        if game_state.get('state') != 'scenario':
            scenario.add_vehicle(self._ego, pos=config.VEHICLE_POS,
                                 rot_quat=config.VEHICLE_ROT_QUAT)
            scenario.make(self._bng)

        self._bng.scenario.load(scenario)
        self._bng.scenario.start()

        # State sensor is auto-attached; attach Electrics explicitly
        self._ego.sensors.attach('electrics', Electrics())

        self.connected = True
        self._thread = threading.Thread(target=self._poll_loop, name='beamng-poll', daemon=True)
        self._thread.start()
        print('[BeamNG] Started — scenario loaded, polling at '
              f'{config.BEAMNG_POLL_HZ} Hz')

    def stop(self):
        if self._thread:
            self._thread.join(timeout=3)
        if self._bng:
            self._bng.disconnect()
        self.connected = False
        print('[BeamNG] Disconnected')

    def _poll_loop(self):
        interval = 1.0 / config.BEAMNG_POLL_HZ
        while not self._shutdown.is_set():
            try:
                t = time.monotonic()
                self._ego.sensors.poll()
                state = self._ego.sensors['state']
                elec = self._ego.sensors['electrics']
                pos = state['pos']
                vel = state['vel']
                speed = math.sqrt(sum(v ** 2 for v in vel))
                self._logger.log_beamng(
                    t,
                    pos[0], pos[1], pos[2],
                    speed,
                    elec.get('steering', 0),
                    elec.get('throttle', 0),
                    elec.get('brake', 0),
                    elec.get('gear_index', 0),
                )
            except Exception as e:
                print(f'[BeamNG] Poll error: {e}')
            time.sleep(interval)

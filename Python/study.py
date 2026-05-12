import sys
import time
import threading
from datetime import datetime
from pathlib import Path

import config
from data_logger import DataLogger
from beamng_manager import BeamNGManager
from arduino_manager import ArduinoManager
from eyetracker_manager import EyeTrackerManager


def _build_session_dir(participant_id: str) -> Path:
    timestamp = datetime.now().strftime('%Y-%m-%d_%H%M%S')
    data_root = Path(__file__).parent.parent / config.DATA_ROOT
    return data_root / f'P{participant_id}_{timestamp}'


def _run_cli(logger: DataLogger, beamng: BeamNGManager, arduino: ArduinoManager,
             eye: EyeTrackerManager, shutdown: threading.Event,
             participant_id: str, condition: list):
    trial_num = 0
    trial_active = False

    print('\nCommands: start | stop | condition <name> | note <text> | status | quit\n')

    while not shutdown.is_set():
        try:
            raw = input('> ').strip()
        except EOFError:
            break
        if not raw:
            continue

        parts = raw.split(None, 1)
        cmd = parts[0].lower()
        arg = parts[1] if len(parts) > 1 else ''

        if cmd == 'start':
            if trial_active:
                print('Trial already active — type "stop" first')
                continue
            trial_num += 1
            trial_active = True
            logger.log_event(time.monotonic(), 'trial_start', str(trial_num))
            print(f'Trial {trial_num} started  [condition: {condition[0]}]')

        elif cmd == 'stop':
            if not trial_active:
                print('No trial is active')
                continue
            trial_active = False
            logger.log_event(time.monotonic(), 'trial_stop', str(trial_num))
            print(f'Trial {trial_num} stopped')

        elif cmd == 'condition':
            if not arg:
                print(f'Usage: condition <{"|".join(config.CONDITIONS)}>')
                continue
            if arg not in config.CONDITIONS:
                print(f'Unknown condition "{arg}". Valid: {config.CONDITIONS}')
                continue
            condition[0] = arg
            logger.log_event(time.monotonic(), 'condition_set', arg)
            print(f'Condition set to: {arg}')

        elif cmd == 'note':
            if not arg:
                print('Usage: note <text>')
                continue
            logger.log_event(time.monotonic(), 'note', arg)
            print(f'Note logged: {arg}')

        elif cmd == 'status':
            print(f'  Participant : {participant_id}')
            print(f'  Condition   : {condition[0]}')
            print(f'  Trial       : {trial_num} ({"active" if trial_active else "idle"})')
            print(f'  BeamNG      : {"connected" if beamng.connected else "disconnected"}')
            print(f'  Arduino     : {"connected" if arduino.connected else "disconnected"} '
                  f'(position={arduino.position})')
            print(f'  Eye tracker : {"connected" if eye.connected else "disconnected"}')

        elif cmd == 'quit':
            if trial_active:
                logger.log_event(time.monotonic(), 'trial_stop', str(trial_num))
                print(f'Trial {trial_num} auto-stopped on quit')
            break

        else:
            print(f'Unknown command: {cmd}')


def main():
    participant_id = input('Participant ID: ').strip()
    if not participant_id:
        print('Participant ID cannot be empty')
        sys.exit(1)

    print(f'Conditions: {config.CONDITIONS}')
    initial_condition = input('Starting condition: ').strip()
    if initial_condition not in config.CONDITIONS:
        print(f'Invalid condition. Choose from: {config.CONDITIONS}')
        sys.exit(1)

    session_dir = _build_session_dir(participant_id)
    print(f'Session data: {session_dir}')

    logger = DataLogger(session_dir)
    logger.open()

    shutdown = threading.Event()
    condition = [initial_condition]  # mutable so _run_cli can update it

    beamng = BeamNGManager(logger, shutdown)
    arduino = ArduinoManager(logger, shutdown)
    eye = EyeTrackerManager(logger, shutdown)

    try:
        logger.log_event(time.monotonic(), 'session_start', participant_id)
        logger.log_event(time.monotonic(), 'condition_set', initial_condition)

        print('\nConnecting to BeamNG...')
        beamng.start()

        print('Connecting to Arduino...')
        arduino.start()

        print('Connecting to eye tracker...')
        eye.start()

        _run_cli(logger, beamng, arduino, eye, shutdown, participant_id, condition)

    except KeyboardInterrupt:
        print('\nInterrupted')
    finally:
        print('\nShutting down...')
        shutdown.set()
        beamng.stop()
        arduino.stop()
        eye.stop()
        logger.log_event(time.monotonic(), 'session_end', participant_id)
        logger.close()
        print(f'Data saved to: {session_dir}')


if __name__ == '__main__':
    main()

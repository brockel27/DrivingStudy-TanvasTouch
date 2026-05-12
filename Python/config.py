# BeamNG
BEAMNG_HOST = 'localhost'
BEAMNG_PORT = 25252
BEAMNG_HOME = 'C:/SimulatorPlatform/BeamNG.tech.v0.38.3.0'
BEAMNG_USER = 'C:/Users/bwander/AppData/Local/BeamNG/BeamNG.tech/current'
BEAMNG_MAP = 'west_coast_usa'
BEAMNG_SCENARIO_NAME = 'driving_study'
VEHICLE_ID = 'ego_vehicle1'
VEHICLE_MODEL = 'etk800'
VEHICLE_LICENSE = 'STUDY'
VEHICLE_POS = (900, -475, 162.5)
VEHICLE_ROT_QUAT = (0, 0, 0.3826834, 0.9238795)
BEAMNG_POLL_HZ = 20

# Arduino
ARDUINO_PORT = 'COM7'
ARDUINO_BAUD = 9600
ARDUINO_TIMEOUT = 1.0
ARDUINO_RETRY_S = 5

# Neon Pupil eye tracker
NEON_HOST = None   # None = mDNS auto-discover; set to IP string if known e.g. '192.168.1.42'
NEON_PORT = 8080

# Study
CONDITIONS = ['tanvas', 'physical']
DATA_ROOT = 'data'   # resolved relative to repo root in study.py

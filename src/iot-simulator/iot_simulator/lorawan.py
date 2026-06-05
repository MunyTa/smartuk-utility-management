from __future__ import annotations

import base64
import hashlib
import struct
from dataclasses import dataclass
from datetime import UTC, datetime
from typing import Iterable

from iot_simulator.payloads import MeterDefinition


UNIT_CODES = {
    "m3": 1,
    "kWh": 2,
    "Gcal": 3,
}

CODE_UNITS = {value: key for key, value in UNIT_CODES.items()}
PAYLOAD_VERSION = 1
PAYLOAD_FORMAT = ">BqIBH"
PAYLOAD_SIZE = struct.calcsize(PAYLOAD_FORMAT)


@dataclass(frozen=True)
class LoRaWanUplinkFrame:
    dev_eui: str
    gateway_id: str
    f_port: int
    frame_counter: int
    payload_base64: str
    received_at: str
    rssi: int
    snr: float


class LoRaWanMockBridge:
    """Emulates a LoRaWAN gateway and application payload decoder."""

    def __init__(
        self,
        meters: Iterable[MeterDefinition],
        gateway_id: str = "smartuk-lorawan-gw-1",
    ) -> None:
        self.gateway_id = gateway_id
        self._device_index: dict[str, MeterDefinition] = {}
        self._frame_counters: dict[str, int] = {}
        self.update_meters(meters)

    def update_meters(self, meters: Iterable[MeterDefinition]) -> None:
        self._device_index = {
            dev_eui_for_device(meter.device_id): meter
            for meter in meters
        }
        self._frame_counters = {
            dev_eui: self._frame_counters.get(dev_eui, 0)
            for dev_eui in self._device_index
        }

    def to_uplink(self, reading: dict[str, object]) -> LoRaWanUplinkFrame:
        device_id = str(reading["deviceId"])
        dev_eui = dev_eui_for_device(device_id)
        if dev_eui not in self._device_index:
            raise ValueError(f"Unknown LoRaWAN device {device_id}")

        self._frame_counters[dev_eui] = self._frame_counters.get(dev_eui, 0) + 1
        measured_at = _parse_datetime(str(reading["measuredAt"]))
        payload = encode_lorawan_payload(
            value=float(reading["value"]),
            unit=str(reading["unit"]),
            measured_at=measured_at,
            battery_voltage=float(reading["batteryVoltage"]),
        )

        return LoRaWanUplinkFrame(
            dev_eui=dev_eui,
            gateway_id=self.gateway_id,
            f_port=10,
            frame_counter=self._frame_counters[dev_eui],
            payload_base64=base64.b64encode(payload).decode("ascii"),
            received_at=datetime.now(UTC).isoformat(),
            rssi=int(reading["signalRssi"]),
            snr=round(6.0 + (abs(int(reading["signalRssi"])) % 30) / 10, 1),
        )

    def decode(self, frame: LoRaWanUplinkFrame) -> dict[str, object]:
        meter = self._device_index.get(frame.dev_eui)
        if meter is None:
            raise ValueError(f"Unknown LoRaWAN devEUI {frame.dev_eui}")

        payload = base64.b64decode(frame.payload_base64)
        value, unit, measured_at, battery_voltage = decode_lorawan_payload(payload)

        return {
            "deviceId": meter.device_id,
            "value": value,
            "unit": unit,
            "measuredAt": measured_at.isoformat(),
            "signalRssi": frame.rssi,
            "batteryVoltage": battery_voltage,
            "gatewayId": frame.gateway_id,
            "devEui": frame.dev_eui,
            "frameCounter": frame.frame_counter,
        }


def dev_eui_for_device(device_id: str) -> str:
    digest = hashlib.sha256(device_id.encode("utf-8")).hexdigest()
    return digest[:16].upper()


def encode_lorawan_payload(
    value: float,
    unit: str,
    measured_at: datetime,
    battery_voltage: float,
) -> bytes:
    unit_code = UNIT_CODES.get(unit)
    if unit_code is None:
        raise ValueError(f"Unsupported LoRaWAN unit {unit}")

    measured_at = measured_at.astimezone(UTC)
    return struct.pack(
        PAYLOAD_FORMAT,
        PAYLOAD_VERSION,
        int(round(value * 1000)),
        int(measured_at.timestamp()),
        unit_code,
        int(round(battery_voltage * 100)),
    )


def decode_lorawan_payload(payload: bytes) -> tuple[float, str, datetime, float]:
    if len(payload) != PAYLOAD_SIZE:
        raise ValueError("Invalid LoRaWAN payload size")

    version, value_milli, timestamp, unit_code, battery_centivolts = struct.unpack(
        PAYLOAD_FORMAT,
        payload,
    )
    if version != PAYLOAD_VERSION:
        raise ValueError(f"Unsupported LoRaWAN payload version {version}")

    unit = CODE_UNITS.get(unit_code)
    if unit is None:
        raise ValueError(f"Unsupported LoRaWAN unit code {unit_code}")

    return (
        round(value_milli / 1000, 3),
        unit,
        datetime.fromtimestamp(timestamp, UTC),
        round(battery_centivolts / 100, 2),
    )


def _parse_datetime(value: str) -> datetime:
    normalized = value.replace("Z", "+00:00")
    parsed = datetime.fromisoformat(normalized)
    if parsed.tzinfo is None:
        return parsed.replace(tzinfo=UTC)
    return parsed.astimezone(UTC)

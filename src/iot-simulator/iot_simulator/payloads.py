from __future__ import annotations

from dataclasses import dataclass
from datetime import UTC, datetime, timedelta, timezone
from random import Random
from typing import Callable, Iterable


@dataclass(frozen=True)
class MeterDefinition:
    device_id: str
    unit: str
    meter_type: str
    base_value: float
    min_increment: float
    max_increment: float


DEFAULT_METERS: tuple[MeterDefinition, ...] = (
    MeterDefinition("meter-101-cold-water", "m3", "ColdWater", 184.0, 0.0, 0.0),
    MeterDefinition("meter-101-electricity", "kWh", "Electricity", 2840.0, 0.0, 0.0),
    MeterDefinition("meter-42-hot-water", "m3", "HotWater", 126.0, 0.0, 0.0),
    MeterDefinition("meter-42-heating", "Gcal", "Heating", 42.0, 0.0, 0.0),
)


class ReadingGenerator:
    def __init__(
        self,
        meters: Iterable[MeterDefinition] = DEFAULT_METERS,
        seed: int = 16,
        now_provider: Callable[[], datetime] | None = None,
    ) -> None:
        self._meters = {meter.device_id: meter for meter in meters}
        self._values = {
            device_id: meter.base_value
            for device_id, meter in self._meters.items()
        }
        self._random = Random(seed)
        self._counter = 0
        self._now_provider = now_provider or (lambda: datetime.now(UTC))

    def update_meters(self, meters: Iterable[MeterDefinition]) -> None:
        updated = {meter.device_id: meter for meter in meters}

        for device_id, meter in updated.items():
            current_value = self._values.get(device_id, meter.base_value)
            self._values[device_id] = max(current_value, meter.base_value)

        self._values = {
            device_id: value
            for device_id, value in self._values.items()
            if device_id in updated
        }
        self._meters = updated

    def next_payloads(self) -> list[dict[str, object]]:
        self._counter += 1
        payloads: list[dict[str, object]] = []
        measured_at = self._now_provider()
        if measured_at.tzinfo is None:
            measured_at = measured_at.replace(tzinfo=UTC)
        local_time = measured_at.astimezone(timezone(timedelta(hours=3)))

        for meter in self._meters.values():
            increment = self._profiled_increment(meter, local_time)

            self._values[meter.device_id] += increment
            payloads.append(
                {
                    "deviceId": meter.device_id,
                    "value": round(self._values[meter.device_id], 3),
                    "unit": meter.unit,
                    "measuredAt": measured_at.isoformat(),
                    "signalRssi": self._random.randint(-92, -51),
                    "batteryVoltage": round(self._random.uniform(3.15, 3.65), 2),
                }
            )

        return payloads

    def _profiled_increment(self, meter: MeterDefinition, local_time: datetime) -> float:
        meter_type = meter.meter_type
        unit = meter.unit
        hour = local_time.hour

        if meter_type == "Gas":
            return self._gas_increment(hour)

        if meter_type == "Electricity" or unit == "kWh":
            return self._electricity_increment(hour)

        if meter_type == "Heating" or unit == "Gcal":
            return self._heating_increment(local_time)

        if meter_type in {"ColdWater", "HotWater"} or unit == "m3":
            return self._water_increment(meter_type, hour)

        return 0.0

    def _water_increment(self, meter_type: str, hour: int) -> float:
        if 6 <= hour <= 9:
            probability, minimum, maximum = 0.55, 0.003, 0.028
        elif 18 <= hour <= 23:
            probability, minimum, maximum = 0.62, 0.004, 0.040
        elif 10 <= hour <= 17:
            probability, minimum, maximum = 0.16, 0.002, 0.014
        else:
            probability, minimum, maximum = 0.03, 0.001, 0.006

        if meter_type == "HotWater":
            probability *= 0.75
            maximum *= 0.8

        if self._random.random() > probability:
            return 0.0

        return self._random.uniform(minimum, maximum)

    def _electricity_increment(self, hour: int) -> float:
        if 0 <= hour <= 5:
            minimum, maximum = 0.001, 0.008
        elif 6 <= hour <= 9:
            minimum, maximum = 0.010, 0.055
        elif 10 <= hour <= 17:
            minimum, maximum = 0.006, 0.035
        else:
            minimum, maximum = 0.020, 0.085

        increment = self._random.uniform(minimum, maximum)
        if 7 <= hour <= 23 and self._random.random() < 0.08:
            increment += self._random.uniform(0.08, 0.35)

        return increment

    def _heating_increment(self, local_time: datetime) -> float:
        if local_time.month in {5, 6, 7, 8, 9}:
            return self._random.uniform(0.0, 0.0005)

        if 0 <= local_time.hour <= 5:
            return self._random.uniform(0.001, 0.006)

        return self._random.uniform(0.002, 0.012)

    def _gas_increment(self, hour: int) -> float:
        if 6 <= hour <= 9 or 18 <= hour <= 22:
            probability, minimum, maximum = 0.35, 0.002, 0.020
        else:
            probability, minimum, maximum = 0.06, 0.001, 0.006

        if self._random.random() > probability:
            return 0.0

        return self._random.uniform(minimum, maximum)


def meter_from_catalog(item: dict[str, object]) -> MeterDefinition | None:
    device_id = str(item.get("deviceId") or "").strip()
    unit = str(item.get("unit") or "").strip()
    meter_type = str(item.get("meterType") or "").strip()
    if not device_id or not unit:
        return None

    base_value = _to_float(item.get("lastValue"))
    if base_value is None:
        base_value = _default_base_value(meter_type, unit)

    min_increment, max_increment = _increments_for(meter_type, unit)
    return MeterDefinition(
        device_id=device_id,
        unit=unit,
        meter_type=meter_type,
        base_value=base_value,
        min_increment=min_increment,
        max_increment=max_increment,
    )


def _to_float(value: object) -> float | None:
    if value is None:
        return None
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def _default_base_value(meter_type: str, unit: str) -> float:
    if meter_type == "Electricity" or unit == "kWh":
        return 1000.0
    if meter_type == "Heating" or unit == "Gcal":
        return 20.0
    if meter_type == "Gas":
        return 50.0
    return 100.0


def _increments_for(meter_type: str, unit: str) -> tuple[float, float]:
    return 0.0, 0.0

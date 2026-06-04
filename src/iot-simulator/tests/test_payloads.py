from datetime import UTC, datetime

from iot_simulator.payloads import ReadingGenerator, meter_from_catalog


def test_generator_produces_required_payload_fields() -> None:
    generator = ReadingGenerator(
        seed=16,
        now_provider=lambda: datetime(2026, 1, 1, 18, 0, tzinfo=UTC),
    )

    payload = generator.next_payloads()[0]

    assert payload["deviceId"] == "meter-101-cold-water"
    assert payload["unit"] == "m3"
    assert payload["value"] >= 184
    assert "measuredAt" in payload
    assert "signalRssi" in payload
    assert "batteryVoltage" in payload


def test_generator_keeps_values_monotonic() -> None:
    generator = ReadingGenerator(
        seed=16,
        now_provider=lambda: datetime(2026, 1, 1, 18, 0, tzinfo=UTC),
    )

    first = generator.next_payloads()[1]["value"]
    second = generator.next_payloads()[1]["value"]

    assert second > first


def test_generator_accepts_new_meter_from_catalog() -> None:
    generator = ReadingGenerator(
        seed=16,
        now_provider=lambda: datetime(2026, 1, 1, 18, 0, tzinfo=UTC),
    )
    meter = meter_from_catalog(
        {
            "deviceId": "meter-205-electricity",
            "unit": "kWh",
            "meterType": "Electricity",
            "lastValue": 500,
        }
    )

    assert meter is not None
    generator.update_meters([meter])
    payload = generator.next_payloads()[0]

    assert payload["deviceId"] == "meter-205-electricity"
    assert payload["value"] > 500

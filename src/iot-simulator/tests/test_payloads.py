from datetime import UTC, datetime

import pytest

from iot_simulator.lorawan import LoRaWanMockBridge, LoRaWanUplinkFrame, dev_eui_for_device
from iot_simulator.payloads import DEFAULT_METERS, ReadingGenerator, meter_from_catalog


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


def test_lorawan_bridge_decodes_uplink_to_mqtt_payload() -> None:
    generator = ReadingGenerator(
        seed=16,
        now_provider=lambda: datetime(2026, 1, 1, 18, 0, tzinfo=UTC),
    )
    reading = generator.next_payloads()[0]
    bridge = LoRaWanMockBridge(DEFAULT_METERS)

    uplink = bridge.to_uplink(reading)
    decoded = bridge.decode(uplink)

    assert uplink.dev_eui == dev_eui_for_device("meter-101-cold-water")
    assert uplink.f_port == 10
    assert decoded["deviceId"] == reading["deviceId"]
    assert decoded["unit"] == reading["unit"]
    assert decoded["value"] == reading["value"]
    assert decoded["signalRssi"] == reading["signalRssi"]
    assert decoded["batteryVoltage"] == reading["batteryVoltage"]
    assert decoded["frameCounter"] == 1


def test_lorawan_bridge_rejects_unknown_device() -> None:
    bridge = LoRaWanMockBridge([])
    frame = LoRaWanUplinkFrame(
        dev_eui="0011223344556677",
        gateway_id="smartuk-lorawan-gw-1",
        f_port=10,
        frame_counter=1,
        payload_base64="AQAAAAAAAAAAAAAAAQDi",
        received_at=datetime(2026, 1, 1, tzinfo=UTC).isoformat(),
        rssi=-70,
        snr=7.5,
    )

    with pytest.raises(ValueError, match="Unknown LoRaWAN devEUI"):
        bridge.decode(frame)

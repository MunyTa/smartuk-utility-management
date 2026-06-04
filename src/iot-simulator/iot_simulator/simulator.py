from __future__ import annotations

import json
import time
from urllib.error import URLError
from urllib.request import urlopen

from paho.mqtt import client as mqtt

from iot_simulator.config import SimulatorConfig
from iot_simulator.payloads import MeterDefinition, ReadingGenerator, meter_from_catalog


def build_client() -> mqtt.Client:
    return mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id="smartuk-iot-simulator")


def main() -> None:
    config = SimulatorConfig.from_env()
    generator = ReadingGenerator([]) if config.meter_catalog_url else ReadingGenerator()
    client = build_client()
    last_catalog_refresh = 0.0

    print(f"Connecting to MQTT broker {config.mqtt_host}:{config.mqtt_port}")
    client.connect(config.mqtt_host, config.mqtt_port, keepalive=60)
    client.loop_start()

    try:
        while True:
            now = time.monotonic()
            if config.meter_catalog_url and now - last_catalog_refresh >= config.meter_catalog_refresh_seconds:
                last_catalog_refresh = now
                meters = load_meter_catalog(config.meter_catalog_url)
                if meters:
                    generator.update_meters(meters)
                    print(f"loaded {len(meters)} meters from catalog")

            for payload in generator.next_payloads():
                message = json.dumps(payload, ensure_ascii=False)
                publish = client.publish(config.mqtt_topic, message, qos=1)
                publish.wait_for_publish()
                print(f"published {message}")
            time.sleep(config.publish_interval_seconds)
    finally:
        client.loop_stop()
        client.disconnect()


def load_meter_catalog(url: str) -> list[MeterDefinition]:
    try:
        with urlopen(url, timeout=5) as response:
            data = json.loads(response.read().decode("utf-8"))
    except (OSError, URLError, json.JSONDecodeError) as exc:
        print(f"meter catalog unavailable: {exc}")
        return []

    if not isinstance(data, list):
        return []

    meters: list[MeterDefinition] = []
    for item in data:
        if isinstance(item, dict):
            meter = meter_from_catalog(item)
            if meter is not None:
                meters.append(meter)
    return meters


if __name__ == "__main__":
    main()

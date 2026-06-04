from __future__ import annotations

import os
from dataclasses import dataclass


@dataclass(frozen=True)
class SimulatorConfig:
    mqtt_host: str
    mqtt_port: int
    mqtt_topic: str
    publish_interval_seconds: float
    meter_catalog_url: str | None
    meter_catalog_refresh_seconds: float

    @classmethod
    def from_env(cls) -> "SimulatorConfig":
        return cls(
            mqtt_host=os.getenv("MQTT_HOST", "localhost"),
            mqtt_port=int(os.getenv("MQTT_PORT", "1883")),
            mqtt_topic=os.getenv("MQTT_TOPIC", "uk/meters/readings"),
            publish_interval_seconds=float(os.getenv("PUBLISH_INTERVAL_SECONDS", "5")),
            meter_catalog_url=os.getenv("METER_CATALOG_URL"),
            meter_catalog_refresh_seconds=float(os.getenv("METER_CATALOG_REFRESH_SECONDS", "10")),
        )

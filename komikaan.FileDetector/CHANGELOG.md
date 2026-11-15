# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.6.0] - 15-11-2026
### Changed
- Delayed imports now use RabbitMQ 4.4 native delayed delivery: the publisher sets the `x-opt-delivery-time` header (delivery time = request time + configured delay) on the quorum queue, so the harvester no longer needs to hold and requeue messages in a loop
- Published messages are now marked persistent

## [0.5.0] - 14-11-2026
### Changed
- Update from .NET 8 to .NET 10 (target framework and Docker base images)
- Bump all NuGet dependencies to their latest versions
- Remove unused EntityFrameworkCore.SqlServer.NetTopologySuite and NetTopologySuite.Core dependencies
- RabbitMQ topology is now declared via the RabbitMQ operator; the app only passive-declares the exchange and queue

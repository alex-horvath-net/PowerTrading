# PowerTrading

## Overview

Power traders need a report generated during the day (intra-day) that shows how much electricity they will have bought or sold for each hour of the next day (the “day-ahead power position”). This helps them plan and manage their trading positions in advance.
This solution implements a **reliable Windows Service** for generating intra-day power position reports.

# Modules and Responsibilities

## PowerTrading.WindowsService
- Responsible solely for scheduling the report generation as a Windows Service. Does not contain actual business logic.
- This section in `appsettings.json` contains the scheduling configuration for the Windows Service worker.
  **`ExtractIntervalMinutes`** specifies how often (in minutes) the worker triggers the report generation.
```json
  "Worker": {
    "ExtractIntervalMinutes": 15
  }
```
- Once the execution is scheduled, a RunId and a RunTime (London local time) help the traceability.
- Implements a queue-based scheduling system to guarantee no scheduled run is missed.
- Uses async semaphores to prevent overlapping executions.
- Handles cancellation tokens for graceful shutdown.

## PowerTrading.Reporting
- Responsible for orchestrating the business process framework, specifically a simple Extract, Transform, Load (ETL) pipeline. Detailed explanation below.  
- Provides a clean, independent interface for generating reports, decoupled from the Windows Service scheduling concerns.

## PowerTrading.Infrastructure
- Responsible for defining the infrastructure-related work steps.  
- **PowerServiceClient** encapsulates the consumption logic of `PowerService.dll` and maps the native DLL types to domain types.  
  The method `IPowerService.GetTradesAsync` receives the parameter `runTime`, which comes from the Windows Service.  
- **CsvExporter** encapsulates the actual report generation. It transforms a `List<PowerTrading.Domain.PowerPosition>` into CSV format.  
  This section in `appsettings.json` contains the CSV configuration:  

  ```json
  "CsvExporter": {
    "OutputFolder": "C:\\Data",
    "Separator": ";",
    "DecimalPlaces": 3
  }
  ```
- **LondonTime** is responsible for calculating the exact London local time


## PowerTrading.Domain 
- Encapsulate domain entities, business rules.
- PowerPeriod, PowerTrade, PowerPosition, PowerPositionAggregator

## Key Design Decisions

- **Concurrency control:** Leveraging async semaphores ensures single processing execution at a time.
- **Queue-based scheduling:** All scheduled runs are enqueued to prevent missing any runs.
- **Graceful cancellation:** Cancellation tokens are respected throughout to allow prompt service shutdown.
- **Centralised error handling:** Exceptions in report generation are logged without stopping the service.
- **Configuration-driven:** Scheduling intervals and output paths are configurable for flexible deployment.
- **Separation of concerns:** Clear layering between service orchestration, business logic, and infrastructure.
- **Retry mechanisim** via Polly


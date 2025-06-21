# PowerTrading

## Overview

Power traders need a report generated during the day (intra-day) that shows how much electricity they will have bought or sold for each hour of the next day (the “day-ahead power position”). This helps them plan and manage their trading positions in advance.
This solution implements a **reliable Windows Service** for generating intra-day power position reports.

# Requirements

## Explicitly Referenced in the Assignment (primary goal)
- Must be implemented as a Windows service using .NET 6.0 C#
- All trade positions must be aggregated per hour (local/wall clock time), starting at 23:00 the previous day (London local time)
- CSV output must have two columns, Local Time (24hr HH:MM) and Volume, with the first row as a header
- CSV filename must be in the format PowerPosition_YYYYMMDD_HHMM.csv, reflecting local extract time
- CSV file location must be configurable and read from the application configuration file
- Extraction must run at a scheduled interval (every X minutes), configurable via the application configuration file
- It is acceptable for the extract to run within +/- 1 minute of the configured interval
- It is not acceptable to miss a scheduled extract
- An extract must run immediately on service start and then continue at the configured interval
- The service only needs to read the configuration at startup; config changes require service restart
- The service must provide adequate logging for production support and diagnostics

## Implicitly Referenced or Inferred from the Assignment (secondary goal)
- Overlapping execution should be handled.
- Retry logic for transient failures (data fetching, file writing)
- Graceful cancellation and shutdown support
- Proper logging for events, errors, and retries
- Configuration driven (intervals, paths)
- Clean architecture with separation of concerns
- Use of asynchronous programming with cancellation tokens
- Robust error handling and resilience patterns (e.g., Polly retries)
- Readability — Clear, consistent, and understandable code
- Modularity — Logical separation of concerns and components
- Traceability — Meaningful logging, error messages, and auditability
- Scalability — Ability to handle growing data or load gracefully
- Security — Secure handling of data and failures (e.g., no sensitive info leakage)
- Error prompt — User or operator-friendly error reporting
- Testable — Well-structured for unit and integration testing


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
- Once the execution receives a run signal, a RunId and a RunTime (in London local time) will be generated. This supports structured logging and traceability.
- Implements a queue-based scheduling system to guarantee no scheduled run is missed.
- Uses async semaphores to prevent overlapping executions.
- Handles cancellation tokens for graceful shutdown.

## PowerTrading.Reporting
- Responsible for orchestrating the business process as Extract, Transform, Load (ETL) pipeline.   
- Provides a clean, independent interface for generating reports, decoupled from the Windows Service scheduling concerns.

## PowerTrading.Infrastructure
- Responsible for defining the infrastructure-related work steps. Each of them is self-defined and confined.
- **LondonTime** is responsible for calculating the exact London local time
- **PowerServiceClient** encapsulates the consumption logic of `PowerService.dll` and maps the native DLL types to domain types.  
  The method `PowerService / GetTradesAsync` receives the parameter `runTime`, which comes from the Windows Service and provides native PowerTrade or PowerServiceException.
  So the client ensures cancellation, a retry mechanism and transforms the native raw data to domain.   
- **CsvExporter** encapsulates the actual report generation. It transforms a `List<PowerTrading.Domain.PowerPosition>` into CSV format.  
  This section in `appsettings.json` contains the CSV configuration:  

  ```json
  "CsvExporter": {
    "OutputFolder": "C:\\Data",
    "Separator": ";",
    "DecimalPlaces": 3
  }
  ```



## PowerTrading.Domain 
- Encapsulate domain entities, business rules.
- PowerPeriod, PowerTrade, PowerPosition, PowerPositionAggregator
- PowerPositionAggregator transforms PowerTrades and PowerPositions. 

## Key Design Decisions

- **Concurrency control:** Leveraging async semaphores ensures single processing execution at a time.
- **Queue-based scheduling:** All scheduled runs are enqueued to prevent missing any runs.
- **Graceful cancellation:** Cancellation tokens are respected throughout to allow prompt service shutdown.
- **Centralised error handling:** Exceptions in report generation are logged without stopping the service.
- **Configuration-driven:** Scheduling intervals and output paths are configurable for flexible deployment.
- **Separation of concerns:** Clear layering between service orchestration, business logic, and infrastructure.
- **Retry mechanisim** via Polly


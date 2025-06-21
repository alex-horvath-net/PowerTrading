# PowerTrading

## Overview

Power traders need a report generated during the day (intra-day) that shows how much electricity they will have bought or sold for each hour of the next day (the “day-ahead power position”). This helps them plan and manage their trading positions in advance.
This solution implements a **reliable Windows Service** for generating intra-day power position reports.

# Requirements

## Explicitly Referenced in the Assignment
- Scheduled extraction runs at configured intervals
- Immediate initial run on service start
- Aggregation of power trade volumes per hour (starting from 23:00 the previous day)
- Output CSV file with correct formatting, naming, and storage location
- Retry logic for transient failures (data fetching, file writing)
- Graceful cancellation and shutdown support
- Ensuring no scheduled extract run is missed (reliability)

## Implicitly Referenced or Inferred from the Assignment (nice to have)
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


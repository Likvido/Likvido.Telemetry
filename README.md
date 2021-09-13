[![GitHub Workflow Status](https://img.shields.io/github/workflow/status/likvido/Likvido.ApplicationInsights.Telemetry/Publish%20to%20nuget)](https://github.com/Likvido/Likvido.ApplicationInsights.Telemetry/actions?query=workflow%3A%22Publish+to+nuget%22)
[![Nuget](https://img.shields.io/nuget/v/Likvido.ApplicationInsights.Telemetry)](https://www.nuget.org/packages/Likvido.ApplicationInsights.Telemetry/)
# Likvido.ApplicationInsights.Telemetry
Small util library to work with app insights library
# Utils
## ServiceNameInitializer
Adjust all telemetry with role name. Helps to search in metrics

![image](https://user-images.githubusercontent.com/3293183/100420543-3919b080-30b9-11eb-8b4d-eadeaaa55a1b.png)

To register it just add the following code when you are building your service provider
```
services.AddSingleton<ITelemetryInitializer>(new ServiceNameInitializer(<RoleName>));
```

## AvoidSamplingTelemetryInitializer
Prevents metrics, which match the specified condition, to be sample on the client-side
To register it just add the following code when you are building your service provider
```
services.AddSingleton<ITelemetryInitializer>(
    new AvoidSamplingTelemetryInitializer(
        t => t is RequestTelemetry telemetry && telemetry.Name == operationName))
```

## AvoidRequestSamplingTelemetryInitializer
Just a small convenient wrapper around `AvoidSamplingTelemetryInitializer`

```
services.AddSingleton<ITelemetryInitializer>(
    new AvoidRequestSamplingTelemetryInitializer(operationName))
```

## TelemetryClientExtensions.ExecuteAsRequest/TelemetryClientExtensions.ExecuteAsRequestAsync
Executes operations as `RequestTelemetry`
Usage
```
await telemetryClient.ExecuteAsRequestAsync(new ExecuteAsRequestAsyncOptions(operationName, func));
```
### Optional fields
* `Action<IOperationHolder<RequestTelemetry>>? Configure` - if more telemetry details need to be added. Is called right before operations itself
* `int? FlushWait` - time for telemetry to be pushed. Usefull for console apps. In secondss - default 15
* `Action? PostExecute` - is called after an operation just before operation.Dispose(). That means logs will still be attached to the RequestTelemetry and method execution time will be included

## HttpResponseTelemetryInitializer
Enrich telemetry with response content to dependency http requests.
```
services.AddSingleton<ITelemetryInitializer>(
    new HttpResponseTelemetryInitializer(
        c => c.StatusCode >= HttpStatusCode.BadRequest))
```

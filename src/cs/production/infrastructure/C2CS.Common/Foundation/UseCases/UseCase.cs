// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

using System;
using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace C2CS;

[PublicAPI]
public abstract class UseCase<TRequest, TInput, TOutput>
    where TRequest : UseCaseRequest
    where TOutput : UseCaseOutput<TInput>, new()
{
    public readonly ILogger Logger;
    public readonly IServiceProvider Services;

    private IDisposable? _loggerScope;
    private IDisposable? _loggerScopeStep;
    private readonly string _name;
    private readonly Stopwatch _stopwatch;
    private readonly Stopwatch _stepStopwatch;
    private readonly UseCaseValidator<TRequest, TInput> _validator;

    public abstract string Name { get; }

    protected UseCase(ILogger logger, IServiceProvider services, UseCaseValidator<TRequest, TInput> validator)
    {
        Logger = logger;
        Services = services;

        // ReSharper disable once VirtualMemberCallInConstructor
        _name = Name;

        _stopwatch = new Stopwatch();
        _stepStopwatch = new Stopwatch();
        _validator = validator;
    }

    protected DiagnosticsSink Diagnostics { get; } = new();

    [DebuggerHidden]
    public TOutput Execute(TRequest? request)
    {
        var output = new TOutput();
        if (request == null)
        {
            return output;
        }

        var previousCurrentDirectory = Environment.CurrentDirectory;
        Environment.CurrentDirectory = request.WorkingDirectory ?? Environment.CurrentDirectory;

        Begin();
        try
        {
            output.Input = _validator.Validate(request);
            Execute(output.Input, output);
        }
        catch (Exception e)
        {
            if (Debugger.IsAttached)
            {
                throw;
            }
            else
            {
                Panic(e);
            }
        }
        finally
        {
            Environment.CurrentDirectory = previousCurrentDirectory;
        }

        End(output);
        return output;
    }

    protected abstract void Execute(TInput input, TOutput output);

    private void Begin()
    {
        _loggerScope = Logger.BeginScope(_name);
        _stopwatch.Reset();
        _stepStopwatch.Reset();
        GarbageCollect();
        Logger.UseCaseStarted();
        _stopwatch.Start();
    }

    private void End(TOutput response)
    {
        _stopwatch.Stop();
        var timeSpan = _stopwatch.Elapsed;

        response.Complete(Diagnostics.GetAll());

        if (response.IsSuccessful)
        {
            Logger.UseCaseSucceeded(timeSpan);
        }
        else
        {
            Logger.UseCaseFailed(timeSpan);
        }

        foreach (var diagnostic in response.Diagnostics)
        {
            diagnostic.Log(Logger);
        }

        _loggerScope?.Dispose();
        _loggerScope = null;
        GarbageCollect();
    }

    private void Panic(Exception e)
    {
        var diagnostic = new DiagnosticPanic(e);
        Diagnostics.Add(diagnostic);
    }

    protected void BeginStep(string stepName)
    {
        _stepStopwatch.Reset();
        _loggerScopeStep = Logger.BeginScope(stepName);
        GarbageCollect();
        Logger.UseCaseStepStarted();
        _stepStopwatch.Start();
    }

    protected void EndStep()
    {
        _stepStopwatch.Stop();
        var timeSpan = _stepStopwatch.Elapsed;
        Logger.UseCaseStepFinished(timeSpan);
        _loggerScopeStep?.Dispose();
        _loggerScopeStep = null;
        GarbageCollect();

        if (!Diagnostics.HasError)
        {
            return;
        }

        var diagnostics = Diagnostics.GetAll();
        throw new UseCaseException(diagnostics);
    }

    private static void GarbageCollect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}

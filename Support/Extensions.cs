using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ConsoleBattery;

public static class Extensions
{
    public static string FormatMilliwatts(int? milliwatts)
    {
        if (milliwatts == null)
            return "0 W";

        if (milliwatts < 0)
            milliwatts = Math.Abs((int)milliwatts);

        if (milliwatts >= 1000000)
            return $"{milliwatts / 1000000.0:F2} KW";
        else if (milliwatts >= 1000)
            return $"{milliwatts / 1000.0:F2} W";
        else
            return $"{milliwatts:F2} mW";
    }

    public static string MilliwattHoursToMinutes(int? milliwattHours, int? powerConsumptionInMilliwatts)
    {
        if (milliwattHours == null || powerConsumptionInMilliwatts == null || powerConsumptionInMilliwatts == 0)
            return "Invalid power consumption value.";
        else if (powerConsumptionInMilliwatts < 0)
            powerConsumptionInMilliwatts = Math.Abs((int)powerConsumptionInMilliwatts);

        double totalMinutes = ((int)milliwattHours * 60.0) / (int)powerConsumptionInMilliwatts;
        int minutesRemaining = (int)Math.Floor(totalMinutes);
        int hoursRemaining = minutesRemaining / 60;
        minutesRemaining %= 60;

        return $"{hoursRemaining} hours & {minutesRemaining} minutes ";
    }

    #region [Reflection Helpers]
    /// <summary>
    /// Returns the declaring type's namespace.
    /// </summary>
    public static string? GetCurrentNamespace() => System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace;

    /// <summary>
    /// Returns the declaring type's full name.
    /// </summary>
    public static string? GetCurrentFullName() => System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Assembly.FullName;

    /// <summary>
    /// Returns the declaring type's assembly name.
    /// </summary>
    public static string? GetCurrentAssemblyName() => System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;

    /// <summary>
    /// Returns the AssemblyVersion, not the FileVersion.
    /// </summary>
    public static Version GetCurrentAssemblyVersion() => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
    #endregion

    public static async Task<DateTimeOffset> GetItemDate(this IStorageItem file) => (await file.GetBasicPropertiesAsync()).ItemDate;
    public static async Task<DateTimeOffset> GetModifiedDate(this IStorageItem file) => (await file.GetBasicPropertiesAsync()).DateModified;
    public static async Task<ulong> GetSize(this IStorageItem file) => (await file.GetBasicPropertiesAsync()).Size;

    /// <summary>
    /// An updated string truncation helper.
    /// </summary>
    /// <remarks>
    /// This can be helpful when the CharacterEllipsis TextTrimming Property is not available.
    /// </remarks>
    public static string Truncate(this string text, int maxLength, string mesial = "…")
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (maxLength > 0 && text.Length > maxLength)
        {
            var limit = maxLength / 2;
            if (limit > 1)
            {
                return String.Format("{0}{1}{2}", text.Substring(0, limit).Trim(), mesial, text.Substring(text.Length - limit).Trim());
            }
            else
            {
                var tmp = text.Length <= maxLength ? text : text.Substring(0, maxLength).Trim();
                return String.Format("{0}{1}", tmp, mesial);
            }
        }
        return text;
    }

    /// <summary>
    /// Converts long file size into typical browser file size.
    /// </summary>
    public static string ToFileSize(this ulong size)
    {
        if (size < 1024) { return (size).ToString("F0") + " Bytes"; }
        if (size < Math.Pow(1024, 2)) { return (size / 1024).ToString("F0") + "KB"; }
        if (size < Math.Pow(1024, 3)) { return (size / Math.Pow(1024, 2)).ToString("F0") + "MB"; }
        if (size < Math.Pow(1024, 4)) { return (size / Math.Pow(1024, 3)).ToString("F0") + "GB"; }
        if (size < Math.Pow(1024, 5)) { return (size / Math.Pow(1024, 4)).ToString("F0") + "TB"; }
        if (size < Math.Pow(1024, 6)) { return (size / Math.Pow(1024, 5)).ToString("F0") + "PB"; }
        return (size / Math.Pow(1024, 6)).ToString("F0") + "EB";
    }

    /// <summary>
    /// uint max = 4,294,967,295 (4.29 Gbps)
    /// </summary>
    /// <returns>formatted bit-rate string</returns>
    public static string FormatBitrate(this uint amount)
    {
        var sizes = new string[]
        {
            "bps",
            "Kbps", // kilo
            "Mbps", // mega
            "Gbps", // giga
            "Tbps", // tera
        };
        var order = amount.OrderOfMagnitude();
        var speed = amount / Math.Pow(1000, order);
        return $"{speed:0.##} {sizes[order]}";
    }

    /// <summary>
    /// ulong max = 18,446,744,073,709,551,615 (18.45 Ebps)
    /// </summary>
    /// <returns>formatted bit-rate string</returns>
    public static string FormatBitrate(this ulong amount)
    {
        var sizes = new string[]
        {
            "bps",
            "Kbps", // kilo
            "Mbps", // mega
            "Gbps", // giga
            "Tbps", // tera
            "Pbps", // peta
            "Ebps", // exa
            "Zbps", // zetta
            "Ybps"  // yotta
        };
        var order = amount.OrderOfMagnitude();
        var speed = amount / Math.Pow(1000, order);
        return $"{speed:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Returns the order of magnitude (10^3)
    /// </summary>
    public static int OrderOfMagnitude(this ulong amount) => (int)Math.Floor(Math.Log(amount, 1000));

    /// <summary>
    /// Returns the order of magnitude (10^3)
    /// </summary>
    public static int OrderOfMagnitude(this uint amount) => (int)Math.Floor(Math.Log(amount, 1000));

    /// <summary>
    /// Gets a stream to a specified file from the application local folder.
    /// </summary>
    /// <param name="fileName">Relative name of the file to open. Can contains subfolders.</param>
    /// <param name="accessMode">File access mode. Default is read.</param>
    /// <returns>The file stream</returns>
    public static Task<IRandomAccessStream> GetLocalFileStreamAsync(this string fileName, FileAccessMode accessMode = FileAccessMode.Read)
    {
        var workingFolder = StorageFolder.GetFolderFromPathAsync(AppContext.BaseDirectory).AsTask<StorageFolder>();
        return GetFileStreamAsync(fileName, accessMode, workingFolder.Result);
    }
    static async Task<IRandomAccessStream> GetFileStreamAsync(string fullFileName, FileAccessMode accessMode, StorageFolder workingFolder)
    {
        var fileName = Path.GetFileName(fullFileName);
        workingFolder = await GetSubFolderAsync(fullFileName, workingFolder);
        var file = await workingFolder.GetFileAsync(fileName);
        return await file.OpenAsync(accessMode);
    }
    static async Task<StorageFolder> GetSubFolderAsync(string fullFileName, StorageFolder workingFolder)
    {
        var folderName = Path.GetDirectoryName(fullFileName);
        if (!string.IsNullOrEmpty(folderName) && folderName != @"\")
        {
            return await workingFolder.GetFolderAsync(folderName);
        }
        return workingFolder;
    }

    public static void CopyTo(this Stream from, Stream to, long Length = -1, CancellationToken CancelToken = default)
    {
        if (from == null)
            throw new ArgumentNullException(nameof(from), "Argument cannot be null");
        if (to == null)
            throw new ArgumentNullException(nameof(to), "Argument cannot be null");

        try
        {
            int BytesRead = 0;
            int ProgressValue = 0;
            long TotalBytesRead = 0;
            long TotalBytesLength = Length > 0 ? Length : from.Length;
            byte[] DataBuffer = new byte[4096];
            while ((BytesRead = from.Read(DataBuffer, 0, DataBuffer.Length)) > 0)
            {
                to.Write(DataBuffer, 0, BytesRead);
                TotalBytesRead += BytesRead;
                if (TotalBytesLength > 1024 * 1024)
                {
                    int LatestValue = Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(TotalBytesRead * 100d / TotalBytesLength))));
                    if (LatestValue > ProgressValue)
                    {
                        ProgressValue = LatestValue;
                        Debug.WriteLine($"Copied {LatestValue}");
                    }
                }
                CancelToken.ThrowIfCancellationRequested();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            from.CopyTo(to);
        }
        finally
        {
            to.Flush();
        }
    }

    #region [Task Helpers]

    /// <summary>
    /// Chainable task helper.
    /// var result = Task.Run(() => SomeAsyncMethodWithReturnValue()).AsCancellable(Cancellation.Token).Result;
    /// </summary>
    public static async Task<T> AsCancellable<T>(this Task<T> Instance, CancellationToken token)
    {
        if (!token.CanBeCanceled)
            return await Instance;

        TaskCompletionSource<T> TCS = new TaskCompletionSource<T>();

        using (CancellationTokenRegistration CancelRegistration = token.Register(() => TCS.TrySetCanceled(token), false))
        {
            _ = Instance.ContinueWith((PreviousTask) =>
            {
                CancelRegistration.Dispose();

                if (Instance.IsCanceled)
                    TCS.TrySetCanceled();
                else if (Instance.IsFaulted)
                    TCS.TrySetException(PreviousTask.Exception ?? new AggregateException("aggregate exception was empty"));
                else
                    TCS.TrySetResult(PreviousTask.Result);

            }, TaskContinuationOptions.ExecuteSynchronously);

            return await TCS.Task;
        }
    }

    /// <summary>
    /// Chainable task helper.
    /// Task.Run(() => SomeAsyncMethodWithNoReturnValue()).AsCancellable(Cancellation.Token);
    /// </summary>
    public static Task AsCancellable(this Task Instance, CancellationToken token)
    {
        TaskCompletionSource TCS = new TaskCompletionSource();

        if (!token.CanBeCanceled)
            return TCS.Task;

        using (CancellationTokenRegistration CancelRegistration = token.Register(() => TCS.TrySetCanceled(token), false))
        {
            _ = Instance.ContinueWith((PreviousTask) =>
            {
                CancelRegistration.Dispose();

                if (Instance.IsCanceled)
                    TCS.TrySetCanceled();
                else if (Instance.IsFaulted)
                    TCS.TrySetException(PreviousTask.Exception ?? new AggregateException("aggregate exception was empty"));
                else
                    TCS.TrySetResult();

            }, TaskContinuationOptions.ExecuteSynchronously);

            return TCS.Task;
        }
    }

    public static async Task WithTimeoutAsync(this Task task, TimeSpan timeout)
    {
        if (task == await Task.WhenAny(task, Task.Delay(timeout))) { await task; }
    }

    public static async Task<T?> WithTimeoutAsync<T>(this Task<T> task, TimeSpan timeout, T? defaultValue = default)
    {
        if (task == await Task.WhenAny(task, Task.Delay(timeout)))
            return await task;

        return defaultValue;
    }

    public static async Task<TOut> AndThen<TIn, TOut>(this Task<TIn> inputTask, Func<TIn, Task<TOut>> mapping)
    {
        var input = await inputTask;
        return (await mapping(input));
    }

    public static async Task<TOut?> AndThen<TIn, TOut>(this Task<TIn> inputTask, Func<TIn, Task<TOut>> mapping, Func<Exception, TOut>? errorHandler = null)
    {
        try
        {
            var input = await inputTask;
            return (await mapping(input));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] AndThen: {ex.Message}");
            if (errorHandler != null)
                return errorHandler(ex);

            throw; // Re-throw if no handler is provided.
        }
    }

    /// <summary>
    /// Runs the specified asynchronous method with return type.
    /// NOTE: Will not catch exceptions generated by the task.
    /// </summary>
    /// <param name="asyncMethod">The asynchronous method to execute.</param>
    public static T RunSynchronously<T>(this Func<Task<T>> asyncMethod)
    {
        if (asyncMethod == null)
            throw new ArgumentNullException($"{nameof(asyncMethod)} cannot be null");

        var prevCtx = SynchronizationContext.Current;
        try
        {   // Invoke the function and alert the context when it completes.
            var t = asyncMethod();
            if (t == null)
                throw new InvalidOperationException("No task provided.");

            return t.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(prevCtx); }
    }

    /// <summary>
    /// Runs the specified asynchronous method without return type.
    /// NOTE: Will not catch exceptions generated by the task.
    /// </summary>
    /// <param name="asyncMethod">The asynchronous method to execute.</param>
    public static void RunSynchronously(this Func<Task> asyncMethod)
    {
        if (asyncMethod == null)
            throw new ArgumentNullException($"{nameof(asyncMethod)}");

        var prevCtx = SynchronizationContext.Current;
        try
        {   // Invoke the function and alert the context when it completes
            var t = asyncMethod();
            if (t == null)
                throw new InvalidOperationException("No task provided.");

            t.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(prevCtx); }
    }

    /// <summary>
    /// Chainable task helper.
    /// var result = await SomeLongAsyncFunction().WithTimeout(TimeSpan.FromSeconds(2));
    /// </summary>
    /// <typeparam name="TResult">the type of task result</typeparam>
    /// <returns><see cref="Task"/>TResult</returns>
    public async static Task<TResult> WithTimeout<TResult>(this Task<TResult> task, TimeSpan timeout)
    {
        Task winner = await (Task.WhenAny(task, Task.Delay(timeout)));

        if (winner != task)
            throw new TimeoutException();

        return await task;   // Unwrap result/re-throw
    }

    /// <summary>
    /// Task extension to add a timeout.
    /// </summary>
    /// <returns>The task with timeout.</returns>
    /// <param name="task">Task.</param>
    /// <param name="timeoutInMilliseconds">Timeout duration in Milliseconds.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public async static Task<T> WithTimeout<T>(this Task<T> task, int timeoutInMilliseconds)
    {
        var retTask = await Task.WhenAny(task, Task.Delay(timeoutInMilliseconds))
            .ConfigureAwait(false);

#pragma warning disable CS8603 // Possible null reference return.
        return retTask is Task<T> ? task.Result : default;
#pragma warning restore CS8603 // Possible null reference return.
    }

    /// <summary>
    /// Chainable task helper.
    /// var result = await SomeLongAsyncFunction().WithCancellation(cts.Token);
    /// </summary>
    /// <typeparam name="TResult">the type of task result</typeparam>
    /// <returns><see cref="Task"/>TResult</returns>
    public static Task<TResult> WithCancellation<TResult>(this Task<TResult> task, CancellationToken cancelToken)
    {
        var tcs = new TaskCompletionSource<TResult>();
        var reg = cancelToken.Register(() => tcs.TrySetCanceled());
        task.ContinueWith(ant =>
        {
            reg.Dispose();
            if (ant.IsCanceled)
                tcs.TrySetCanceled();
            else if (ant.IsFaulted)
                tcs.TrySetException(ant.Exception?.InnerException ?? new Exception("Antecedent faulted."));
            else
                tcs.TrySetResult(ant.Result);
        });
        return tcs.Task;  // Return the TaskCompletionSource result
    }

    public static Task<T> WithAllExceptions<T>(this Task<T> task)
    {
        TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();

        task.ContinueWith(ignored =>
        {
            switch (task.Status)
            {
                case TaskStatus.Canceled:
                    Debug.WriteLine($"[TaskStatus.Canceled]");
                    tcs.SetCanceled();
                    break;
                case TaskStatus.RanToCompletion:
                    tcs.SetResult(task.Result);
                    //Debug.WriteLine($"[TaskStatus.RanToCompletion({task.Result})]");
                    break;
                case TaskStatus.Faulted:
                    // SetException will automatically wrap the original AggregateException
                    // in another one. The new wrapper will be removed in TaskAwaiter, leaving
                    // the original intact.
                    Debug.WriteLine($"[TaskStatus.Faulted: {task.Exception?.Message}]");
                    tcs.SetException(task.Exception ?? new Exception("Task faulted."));
                    break;
                default:
                    Debug.WriteLine($"[TaskStatus: Continuation called illegally.]");
                    tcs.SetException(new InvalidOperationException("Continuation called illegally."));
                    break;
            }
        });

        return tcs.Task;
    }

#pragma warning disable RECS0165 // Asynchronous methods should return a Task instead of void
    /// <summary>
    /// Attempts to await on the task and catches exception
    /// </summary>
    /// <param name="task">Task to execute</param>
    /// <param name="onException">What to do when method has an exception</param>
    /// <param name="continueOnCapturedContext">If the context should be captured.</param>
    public static async void SafeFireAndForget(this Task task, Action<Exception>? onException = null, bool continueOnCapturedContext = false)
#pragma warning restore RECS0165 // Asynchronous methods should return a Task instead of void
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (Exception ex) when (onException != null)
        {
            onException.Invoke(ex);
        }
        catch (Exception ex) when (onException == null)
        {
            Debug.WriteLine($"SafeFireAndForget: {ex.Message}");
        }
    }

    /// <summary>
    /// Task.Factory.StartNew (() => { throw null; }).IgnoreExceptions();
    /// </summary>
    public static void IgnoreExceptions(this Task task)
    {
        task.ContinueWith(t =>
        {
            var ignore = t.Exception;
            var inners = ignore?.Flatten()?.InnerExceptions;
            if (inners != null)
            {
                foreach (Exception ex in inners)
                    Debug.WriteLine($"[{ex.GetType()}]: {ex.Message}");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public static bool IgnoreExceptions(Action action, Type? exceptionToIgnore = null, [CallerMemberName] string? caller = null)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            if (exceptionToIgnore is null || exceptionToIgnore.IsAssignableFrom(ex.GetType()))
            {
                Console.WriteLine($"{caller ?? "N/A"}: {ex.Message}");
                return false;
            }
            else
                throw;
        }
    }


    /// <summary>
    /// Gets the result of a <see cref="Task"/> if available, or <see langword="null"/> otherwise.
    /// </summary>
    /// <param name="task">The input <see cref="Task"/> instance to get the result for.</param>
    /// <returns>The result of <paramref name="task"/> if completed successfully, or <see langword="default"/> otherwise.</returns>
    /// <remarks>
    /// This method does not block if <paramref name="task"/> has not completed yet. Furthermore, it is not generic
    /// and uses reflection to access the <see cref="Task{TResult}.Result"/> property and boxes the result if it's
    /// a value type, which adds overhead. It should only be used when using generics is not possible.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? GetResultOrDefault(this Task task)
    {
        // Check if the instance is a completed Task
        if (
#if NETSTANDARD2_1
            task.IsCompletedSuccessfully
#else
            task.Status == TaskStatus.RanToCompletion
#endif
        )
        {
            // We need an explicit check to ensure the input task is not the cached
            // Task.CompletedTask instance, because that can internally be stored as
            // a Task<T> for some given T (e.g. on dotNET 5 it's VoidTaskResult), which
            // would cause the following code to return that result instead of null.
            if (task != Task.CompletedTask)
            {
                // Try to get the Task<T>.Result property. This method would've
                // been called anyway after the type checks, but using that to
                // validate the input type saves some additional reflection calls.
                // Furthermore, doing this also makes the method flexible enough to
                // cases whether the input Task<T> is actually an instance of some
                // runtime-specific type that inherits from Task<T>.
                PropertyInfo? propertyInfo =
#if NETSTANDARD1_4
                    task.GetType().GetRuntimeProperty(nameof(Task<object>.Result));
#else
                    task.GetType().GetProperty(nameof(Task<object>.Result));
#endif

                // Return the result, if possible
                return propertyInfo?.GetValue(task);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the result of a <see cref="Task{TResult}"/> if available, or <see langword="default"/> otherwise.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="Task{TResult}"/> to get the result for.</typeparam>
    /// <param name="task">The input <see cref="Task{TResult}"/> instance to get the result for.</param>
    /// <returns>The result of <paramref name="task"/> if completed successfully, or <see langword="default"/> otherwise.</returns>
    /// <remarks>This method does not block if <paramref name="task"/> has not completed yet.</remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? GetResultOrDefault<T>(this Task<T?> task)
    {
#if NETSTANDARD2_1
        return task.IsCompletedSuccessfully ? task.Result : default;
#else
        return task.Status == TaskStatus.RanToCompletion ? task.Result : default;
#endif
    }
    #endregion

    #region [TaskCompletionSource via Thread encapsulation]
    public static Task StartSTATask(Func<Task> func)
    {
        var tcs = new TaskCompletionSource();
        Thread t = new Thread(async () => {
            try
            {
                await func();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                //tcs.SetException(ex);
                tcs.TrySetResult();
                Console.WriteLine($"StartSTATask(Func<Task>): {ex.Message}");
            }
        })
        { IsBackground = true, Priority = ThreadPriority.Lowest };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        return tcs.Task;
    }

    public static Task StartSTATask(Action action)
    {
        var tcs = new TaskCompletionSource();
        Thread t = new Thread(() => {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                //tcs.SetException(ex);
                tcs.TrySetResult();
                Console.WriteLine($"StartSTATask(Action): {ex.Message}");
            }
        })
        { IsBackground = true, Priority = ThreadPriority.Lowest };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        return tcs.Task;
    }

    public static Task<T?> StartSTATask<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T?>();
        Thread t = new Thread(() => {
            try
            {
                tcs.TrySetResult(func());
            }
            catch (Exception ex)
            {
                //tcs.SetException(ex);
                tcs.TrySetResult(default);
                Console.WriteLine($"StartSTATask(Func<T>): {ex.Message}");
            }
        })
        { IsBackground = true, Priority = ThreadPriority.Lowest };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        return tcs.Task;
    }

    public static Task<T?> StartSTATask<T>(Func<Task<T>> func)
    {
        var tcs = new TaskCompletionSource<T?>();
        Thread t = new Thread(async () => {
            try
            {
                tcs.TrySetResult(await func());
            }
            catch (Exception ex)
            {
                //tcs.SetException(ex);
                tcs.SetResult(default);
                Console.WriteLine($"StartSTATask(Func<Task<T>>): {ex.Message}");
            }
        })
        { IsBackground = true, Priority = ThreadPriority.Lowest };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        return tcs.Task;
    }
    #endregion
}

/// <summary>
///   A memory efficient version of the <see cref="System.Diagnostics.Stopwatch"/>.
///   Because this timer's function is passive, there's no need/way for a
///   stop method. A reset method would be equivalent to calling StartNew().
/// </summary>
/// <remarks>
///   Structs are value types. This means they directly hold their data, 
///   unlike reference types (e.g. classes) that hold references to objects.
///   Value types cannot be null, they'll always have a value, even if it's 
///   the default value for their member data type(s). While you can't assign 
///   null directly to a struct, you can have struct members that are reference 
///   types (e.g. String), and those members can be null.
/// </remarks>
internal struct ValueStopwatch
{
    long _startTimestamp;
    // Set the ratio of timespan ticks to stopwatch ticks.
    static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)System.Diagnostics.Stopwatch.Frequency;
    public bool IsActive => _startTimestamp != 0;
    private ValueStopwatch(long startTimestamp) => _startTimestamp = startTimestamp;
    public static ValueStopwatch StartNew() => new ValueStopwatch(System.Diagnostics.Stopwatch.GetTimestamp());
    public TimeSpan GetElapsedTime()
    {
        // _startTimestamp cannot be zero for an initialized ValueStopwatch.
        if (!IsActive)
            throw new InvalidOperationException($"ValueStopwatch is uninitialized. Initialize the ValueStopwatch before using.");

        long end = System.Diagnostics.Stopwatch.GetTimestamp();
        long timestampDelta = end - _startTimestamp;
        long ticks = (long)(TimestampToTicks * timestampDelta);
        return new TimeSpan(ticks);
    }

    public string GetElapsedFriendly()
    {
        return ToHumanFriendly(GetElapsedTime());
    }

    #region [Helpers]
    string ToHumanFriendly(TimeSpan timeSpan)
    {
        if (timeSpan == TimeSpan.Zero)
            return "0 seconds";

        bool isNegative = false;
        List<string> parts = new();

        // Check for negative TimeSpan.
        if (timeSpan < TimeSpan.Zero)
        {
            isNegative = true;
            timeSpan = timeSpan.Negate(); // Make it positive for the calculations.
        }

        if (timeSpan.Days > 0)
            parts.Add($"{timeSpan.Days} day{(timeSpan.Days > 1 ? "s" : "")}");
        if (timeSpan.Hours > 0)
            parts.Add($"{timeSpan.Hours} hour{(timeSpan.Hours > 1 ? "s" : "")}");
        if (timeSpan.Minutes > 0)
            parts.Add($"{timeSpan.Minutes} minute{(timeSpan.Minutes > 1 ? "s" : "")}");
        if (timeSpan.Seconds > 0)
            parts.Add($"{timeSpan.Seconds} second{(timeSpan.Seconds > 1 ? "s" : "")}");

        // If no large amounts so far, try milliseconds.
        if (parts.Count == 0 && timeSpan.Milliseconds > 0)
            parts.Add($"{timeSpan.Milliseconds} millisecond{(timeSpan.Milliseconds > 1 ? "s" : "")}");

        // If no milliseconds, use ticks (nanoseconds).
        if (parts.Count == 0 && timeSpan.Ticks > 0)
        {
            // A tick is equal to 100 nanoseconds. While this maps well into units of time
            // such as hours and days, any periods longer than that aren't representable in
            // a succinct fashion, e.g. a month can be between 28 and 31 days, while a year
            // can contain 365 or 366 days. A decade can have between 1 and 3 leap-years,
            // depending on when you map the TimeSpan into the calendar. This is why TimeSpan
            // does not provide a "Years" property or a "Months" property.
            // Internally TimeSpan uses long (Int64) for its values, so:
            //  - TimeSpan.MaxValue = long.MaxValue
            //  - TimeSpan.MinValue = long.MinValue
            parts.Add($"{(timeSpan.Ticks * TimeSpan.TicksPerMicrosecond)} microsecond{((timeSpan.Ticks * TimeSpan.TicksPerMicrosecond) > 1 ? "s" : "")}");
        }

        // Join the sections with commas and "and" for the last one.
        if (parts.Count == 1)
            return isNegative ? $"Negative {parts[0]}" : parts[0];
        else if (parts.Count == 2)
            return isNegative ? $"Negative {string.Join(" and ", parts)}" : string.Join(" and ", parts);
        else
        {
            string lastPart = parts[parts.Count - 1];
            parts.RemoveAt(parts.Count - 1);
            return isNegative ? $"Negative " + string.Join(", ", parts) + " and " + lastPart : string.Join(", ", parts) + " and " + lastPart;
        }
    }
    #endregion
}


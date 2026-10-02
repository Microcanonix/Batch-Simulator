using System.Globalization;

namespace Batch_Simulator;

internal sealed record Batch(
    int Id,
    TimeSpan ArrivalTime,
    int MessageCount,
    TimeSpan ProcessingTime);

internal sealed record BatchResult(
    int Id,
    TimeSpan ArrivalTime,
    int MessageCount,
    TimeSpan ProcessingTime,
    TimeSpan CompletionTime)
{
    public TimeSpan Duration => CompletionTime - ArrivalTime;
}

internal static class QueueSimulator
{
    public static IReadOnlyList<BatchResult> Simulate(
        int workerCount,
        TimeSpan startupTime,
        IEnumerable<Batch> batches)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            workerCount,
            0);

        if (startupTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startupTime));
        }

        var orderedBatches = batches
            .OrderBy(batch => batch.ArrivalTime)
            .ThenBy(batch => batch.Id)
            .ToList();

        var workers =
            new PriorityQueue<int, (TimeSpan AvailableAt, int WorkerId)>();

        // Workers are ready for the first message at time zero.
        for (var workerId = 1; workerId <= workerCount; workerId++)
        {
            workers.Enqueue(
                workerId,
                (TimeSpan.Zero, workerId));
        }

        var results = new List<BatchResult>(
            orderedBatches.Count);

        foreach (var batch in orderedBatches)
        {
            var completionTime = batch.ArrivalTime;

            for (var message = 0;
                 message < batch.MessageCount;
                 message++)
            {
                workers.TryDequeue(
                    out var workerId,
                    out var priority);

                var startTime = Max(
                    batch.ArrivalTime,
                    priority.AvailableAt);

                var finishTime =
                    startTime + batch.ProcessingTime;

                // The worker restarts before taking another message.
                workers.Enqueue(
                    workerId,
                    (finishTime + startupTime, workerId));

                // Completion excludes the restart after the final message.
                completionTime = Max(
                    completionTime,
                    finishTime);
            }

            results.Add(
                new BatchResult(
                    batch.Id,
                    batch.ArrivalTime,
                    batch.MessageCount,
                    batch.ProcessingTime,
                    completionTime));
        }

        return results;
    }

    private static TimeSpan Max(
        TimeSpan first,
        TimeSpan second)
    {
        return first >= second ? first : second;
    }
}

internal static class Program
{
    private static void Main()
    {
        Console.WriteLine("Batch Simulator");
        Console.WriteLine(
            "Shared FIFO queue with workers taking the next available message.\n");

        var workerCount = ReadInt(
            "Number of workers: ",
            1);

        var startupTime = ReadMinutes(
            "Worker startup time (minutes): ",
            true);

        Console.WriteLine(
            "\nEnter batches in arrival order. " +
            "Leave arrival time blank when finished.");

        var batches = new List<Batch>();

        while (true)
        {
            var arrivalInput = ReadOptional(
                "Arrival time (minutes): ");

            if (string.IsNullOrWhiteSpace(arrivalInput))
            {
                break;
            }

            var arrivalMinutes = ParseNonNegative(
                arrivalInput,
                "arrival time");

            var messageCount = ReadInt(
                "Batch size (1-96): ",
                1,
                96);

            var processingTime = ReadMinutes(
                "Execution time per message in this batch (minutes): ",
                false);

            batches.Add(
                new Batch(
                    batches.Count + 1,
                    TimeSpan.FromMinutes(arrivalMinutes),
                    messageCount,
                    TimeSpan.FromMinutes(processingTime)));

            Console.WriteLine();
        }

        if (batches.Count == 0)
        {
            Console.WriteLine("No batches were entered.");
            return;
        }

        var results = QueueSimulator.Simulate(
            workerCount,
            TimeSpan.FromMinutes(startupTime),
            batches);

        Console.WriteLine("\nResults\n-------");

        foreach (var result in results)
        {
            Console.WriteLine(
                $"Batch {result.Id}: " +
                $"{result.MessageCount} messages, " +
                $"{Format(result.ProcessingTime)} per message, " +
                $"arrives at {Format(result.ArrivalTime)}, " +
                $"completes at {Format(result.CompletionTime)}, " +
                $"duration {Format(result.Duration)}.");
        }
    }

    private static int ReadInt(
        string prompt,
        int minimum,
        int? maximum = null)
    {
        while (true)
        {
            var input = ReadRequired(prompt);

            if (int.TryParse(
                    input,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var value)
                && value >= minimum
                && (!maximum.HasValue
                    || value <= maximum.Value))
            {
                return value;
            }

            var range = maximum.HasValue
                ? $" between {minimum} and {maximum}"
                : $" greater than or equal to {minimum}";

            Console.WriteLine(
                $"Please enter a whole number{range}.");
        }
    }

    private static double ReadMinutes(
        string prompt,
        bool allowZero)
    {
        while (true)
        {
            var input = ReadRequired(prompt);

            if (double.TryParse(
                    input,
                    NumberStyles.Float,
                    CultureInfo.CurrentCulture,
                    out var value)
                && !double.IsNaN(value)
                && !double.IsInfinity(value)
                && (allowZero
                    ? value >= 0
                    : value > 0))
            {
                return value;
            }

            Console.WriteLine(
                allowZero
                    ? "Please enter a non-negative number of minutes."
                    : "Please enter a positive number of minutes.");
        }
    }

    private static double ParseNonNegative(
        string input,
        string fieldName)
    {
        if (double.TryParse(
                input,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out var value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value)
            && value >= 0)
        {
            return value;
        }

        throw new FormatException(
            $"Invalid {fieldName}: '{input}'.");
    }

    private static string ReadRequired(
        string prompt)
    {
        while (true)
        {
            Console.Write(prompt);

            var input = Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(input))
            {
                return input.Trim();
            }

            Console.WriteLine("A value is required.");
        }
    }

    private static string? ReadOptional(
        string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim();
    }

    private static string Format(
        TimeSpan value)
    {
        return value.ToString(
            @"d\.hh\:mm\:ss\.fff",
            CultureInfo.InvariantCulture);
    }
}

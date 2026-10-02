"""Interactive shared-FIFO batch simulator; uses only the Python standard library."""

from __future__ import annotations

from dataclasses import dataclass
import heapq


@dataclass(frozen=True)
class Batch:
    batch_id: int
    arrival_minutes: float
    message_count: int
    processing_minutes: float


@dataclass(frozen=True)
class BatchResult:
    batch: Batch
    completion_minutes: float

    @property
    def duration_minutes(self) -> float:
        return self.completion_minutes - self.batch.arrival_minutes


def simulate(
    worker_count: int,
    startup_minutes: float,
    batches: list[Batch],
) -> list[BatchResult]:
    """Simulate messages in FIFO order, assigning each to the next free worker."""
    if worker_count < 1:
        raise ValueError("Worker count must be at least 1.")
    if startup_minutes < 0:
        raise ValueError("Startup time cannot be negative.")

    # Heap entries are (next available time, worker ID), giving stable tie breaks.
    workers = [(0.0, worker_id) for worker_id in range(1, worker_count + 1)]
    heapq.heapify(workers)
    results: list[BatchResult] = []

    # Sorting by arrival preserves FIFO; batch ID preserves entry order for ties.
    for batch in sorted(batches, key=lambda item: (item.arrival_minutes, item.batch_id)):
        completion_minutes = batch.arrival_minutes

        for _ in range(batch.message_count):
            available_minutes, worker_id = heapq.heappop(workers)
            start_minutes = max(batch.arrival_minutes, available_minutes)
            finish_minutes = start_minutes + batch.processing_minutes

            # The worker waits through startup before taking its next message.
            heapq.heappush(
                workers,
                (finish_minutes + startup_minutes, worker_id),
            )
            completion_minutes = max(completion_minutes, finish_minutes)

        results.append(BatchResult(batch, completion_minutes))

    return results


def read_integer(prompt: str, minimum: int, maximum: int | None = None) -> int:
    while True:
        raw_value = input(prompt).strip()
        try:
            value = int(raw_value)
        except ValueError:
            value = None

        if value is not None and value >= minimum and (maximum is None or value <= maximum):
            return value

        if maximum is None:
            print(f"Enter a whole number greater than or equal to {minimum}.")
        else:
            print(f"Enter a whole number between {minimum} and {maximum}.")


def read_minutes(prompt: str, *, allow_zero: bool) -> float:
    while True:
        raw_value = input(prompt).strip()
        try:
            value = float(raw_value)
        except ValueError:
            value = float("nan")

        if value == value and abs(value) != float("inf") and (value >= 0 if allow_zero else value > 0):
            return value

        if allow_zero:
            print("Enter a non-negative number of minutes.")
        else:
            print("Enter a positive number of minutes.")


def format_minutes(minutes: float) -> str:
    """Format elapsed minutes as days.hh:mm:ss.mmm."""
    total_milliseconds = round(minutes * 60_000)
    milliseconds, total_seconds = total_milliseconds % 1000, total_milliseconds // 1000
    seconds, total_minutes = total_seconds % 60, total_seconds // 60
    minute, total_hours = total_minutes % 60, total_minutes // 60
    hour, days = total_hours % 24, total_hours // 24
    return f"{days}.{hour:02}:{minute:02}:{seconds:02}.{milliseconds:03}"


def main() -> None:
    print("Batch Simulator (Python)")
    print("Shared FIFO queue; workers take the next message when available.\n")

    worker_count = read_integer("Number of workers: ", 1)
    startup_minutes = read_minutes(
        "Worker startup time before taking another message (minutes): ",
        allow_zero=True,
    )

    print("\nEnter batches by arrival time. Leave arrival time blank when finished.")
    batches: list[Batch] = []
    while True:
        arrival_text = input("Arrival time (minutes): ").strip()
        if not arrival_text:
            break

        try:
            arrival_minutes = float(arrival_text)
        except ValueError:
            arrival_minutes = float("nan")

        if arrival_minutes != arrival_minutes or abs(arrival_minutes) == float("inf") or arrival_minutes < 0:
            print("Enter a non-negative arrival time in minutes.")
            continue

        message_count = read_integer("Batch size (1-96): ", 1, 96)
        processing_minutes = read_minutes(
            "Execution time per message in this batch (minutes): ",
            allow_zero=False,
        )
        batches.append(
            Batch(
                batch_id=len(batches) + 1,
                arrival_minutes=arrival_minutes,
                message_count=message_count,
                processing_minutes=processing_minutes,
            )
        )
        print()

    if not batches:
        print("No batches were entered.")
        return

    print("\nResults\n-------")
    for result in simulate(worker_count, startup_minutes, batches):
        batch = result.batch
        print(
            f"Batch {batch.batch_id}: {batch.message_count} messages, "
            f"{format_minutes(batch.processing_minutes)} per message, "
            f"arrives at {format_minutes(batch.arrival_minutes)}, "
            f"completes at {format_minutes(result.completion_minutes)}, "
            f"duration {format_minutes(result.duration_minutes)}."
        )


if __name__ == "__main__":
    main()

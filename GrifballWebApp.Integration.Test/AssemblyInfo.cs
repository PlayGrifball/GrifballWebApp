using NUnit.Framework;

// Enable parallel test execution at the assembly level
// Tests will run in parallel across different fixtures (test classes)
[assembly: Parallelizable(ParallelScope.Fixtures)]

// Set the maximum number of parallel workers
// This value balances CPU utilization with database container resource constraints
[assembly: LevelOfParallelism(4)]

using Microsoft.Extensions.Logging;

namespace WealthOps.TestFramework.Logging;

public sealed record XUnitLoggerCategoryMinValue(string CategoryPrefix, LogLevel MinLevel);

[test-icon]:            https://github.com/ASolomatin/T-Digest.NET/actions/workflows/tests.yml/badge.svg?branch=main
[test-url]:             https://github.com/ASolomatin/T-Digest.NET/actions/workflows/tests.yml

[packaging-icon]:       https://github.com/ASolomatin/T-Digest.NET/actions/workflows/publish.yml/badge.svg
[packaging-url]:        https://github.com/ASolomatin/T-Digest.NET/actions/workflows/publish.yml

[license-icon]:         https://img.shields.io/github/license/ASolomatin/T-Digest.NET
[license-url]:          https://github.com/ASolomatin/T-Digest.NET/blob/master/LICENSE

[nuget-icon]:           https://img.shields.io/nuget/v/T-Digest.NET.svg
[nuget-downloads-icon]: https://img.shields.io/nuget/dt/T-Digest.NET.svg
[nuget-url]:            https://www.nuget.org/packages/T-Digest.NET

# T-Digest.NET

[![NuGet][nuget-icon]][nuget-url]
[![NuGet downloads][nuget-downloads-icon]][nuget-url]
[![Tests][test-icon]][test-url]
[![Publish][packaging-icon]][packaging-url]
[![GitHub][license-icon]][license-url]

----------------------------------------

.NET Implementation of the relatively new T-Digest quantile estimation algorithm. Useful for calculating highly accurate Quantiles or Percentiles from on-line streaming data, or data-sets that are too large to store in memory and sort, which is required to calculate the true quantile.

Fully refactored fork of [quantumtunneling/T-Digest.NET](https://github.com/quantumtunneling/T-Digest.NET) with next changes:
 - Modern .NET frameworks support
 - Highly improved performance
 - A bit better accuracy

The Nuget package for this Implementation can be found [here](https://www.nuget.org/packages/T-Digest.NET)

The T-Digest white paper can be found [here](https://github.com/tdunning/t-digest/blob/master/docs/t-digest-paper/histo.pdf)

----------------------------------------

Example Code:
```csharp
    using TDigestNet;

    ...

    Random random = new();
    TDigest digest = new();

    for (int i = 0; i < 1000000; i++)
    {
        var n = random.NextDouble() * 100;
        digest.Add(n);
    }

    Console.WriteLine($"Average: {digest.Average}");
    Console.WriteLine($"Percentile 10: {digest.Quantile(10 / 100d)}");
    Console.WriteLine($"Percentile 50: {digest.Quantile(50 / 100d)}");
    Console.WriteLine($"Percentile 80: {digest.Quantile(80 / 100d)}");
    Console.WriteLine($"Percentile 99: {digest.Quantile(99 / 100d)}");
```

----------------------------------------

## License

**[MIT][license-url]**

Copyright (C) 2023 Aleksej Solomatin

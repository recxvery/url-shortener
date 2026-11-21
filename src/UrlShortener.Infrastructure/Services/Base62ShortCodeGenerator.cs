using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Options;

namespace UrlShortener.Infrastructure.Services;

public sealed class Base62ShortCodeGenerator : IShortCodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private readonly ShortLinkOptions _options;

    public Base62ShortCodeGenerator(IOptions<ShortLinkOptions> options)
    {
        _options = options.Value;
    }

    public string Generate()
    {
        var length = Math.Clamp(_options.CodeLength, 4, 16);
        Span<char> buffer = stackalloc char[length];

        for (var i = 0; i < length; i++)
        {
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(buffer);
    }
}

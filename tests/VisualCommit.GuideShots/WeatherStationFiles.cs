using System.Text;
using VisualCommit.Testing;

namespace VisualCommit.GuideShots;

/// <summary>
/// The weather station's files as its history changes them (<see cref="GuideScenario"/>): each
/// step of the history turns a part of a file on, and a file's text is made from the parts that
/// are on. Made-up code of a made-up project.
/// </summary>
internal sealed class WeatherStationFiles
{
    private readonly HashSet<string> _steps = new(StringComparer.Ordinal);
    private string _version = "0.1";

    /// <summary>The files of the first commits.</summary>
    public static WeatherStationFiles Initial() => new();

    /// <summary>The text the working tree's change gives <c>station/units.py</c>: one hunk at the top and one at the end.</summary>
    public static string UnitsInProgress(string committed) =>
        committed
            .Replace(UnitsDocstring, UnitsDocstring + "\n\nEvery function takes and returns plain numbers, never strings.", StringComparison.Ordinal)
            + "\n\ndef kilometres_per_hour_to_knots(speed):\n    return speed / 1.852\n";

    /// <summary>What the working tree's change adds to the readme.</summary>
    public const string ReadmeAddition = "\n## Calibration\n\nCompare the temperature with a reference thermometer once a season.\n";

    private const string UnitsDocstring = "\"\"\"Conversions between the units the station reads and the units it shows.";

    /// <summary>The station's icon, 64 × 64: a sun on a blue sky, with a cloud over it in the working tree.</summary>
    public static byte[] Icon(bool cloud) =>
        TestImages.Png(64, 64, (x, y) =>
        {
            var sun = (x - 24) * (x - 24) + (y - 24) * (y - 24) <= 14 * 14;
            var inCloud = cloud && ((x - 38) * (x - 38) / 2.2 + (y - 40) * (y - 40) <= 11 * 11);
            if (inCloud)
            {
                return new TestImages.Rgb(0xF4, 0xF6, 0xFA);
            }

            return sun ? new TestImages.Rgb(0xF5, 0xB8, 0x2E) : new TestImages.Rgb(0x4A, 0x90, 0xD9);
        });

    public bool Has(string path) => Text(path, throwIfMissing: false) is not null;

    public string Text(string path) => Text(path, throwIfMissing: true)!;

    public WeatherStationFiles Copy()
    {
        var copy = new WeatherStationFiles { _version = _version };
        copy._steps.UnionWith(_steps);
        return copy;
    }

    public void ReadHumidity() => _steps.Add(nameof(ReadHumidity));

    public void RoundReadings() => _steps.Add(nameof(RoundReadings));

    public void Version(string version) => _version = version;

    public void SupportKelvin() => _steps.Add(nameof(SupportKelvin));

    public void LogSensorErrors() => _steps.Add(nameof(LogSensorErrors));

    public void ConvertWindSpeeds() => _steps.Add(nameof(ConvertWindSpeeds));

    public void RefreshEveryMinute() => _steps.Add(nameof(RefreshEveryMinute));

    public void FixHumidityRange() => _steps.Add(nameof(FixHumidityRange));

    public void SketchForecast() => _steps.Add(nameof(SketchForecast));

    public void ShowPressureTrend() => _steps.Add(nameof(ShowPressureTrend));

    public void PredictRain() => _steps.Add(nameof(PredictRain));

    public void UpdateReadme() => _steps.Add(nameof(UpdateReadme));

    public void RoundHalfUp() => _steps.Add(nameof(RoundHalfUp));

    public void CacheReadings() => _steps.Add(nameof(CacheReadings));

    public void TidyDisplay() => _steps.Add(nameof(TidyDisplay));

    public void RoundToConfiguredDigits() => _steps.Add(nameof(RoundToConfiguredDigits));

    public void CheckWiring() => _steps.Add(nameof(CheckWiring));

    public void ShowBatteryLevel() => _steps.Add(nameof(ShowBatteryLevel));

    public WeatherStationFiles WithFrenchReadme()
    {
        _steps.Add(nameof(WithFrenchReadme));
        return this;
    }

    private bool On(string step) => _steps.Contains(step);

    private string? Text(string path, bool throwIfMissing)
    {
        var text = path switch
        {
            "README.md" => Readme(),
            "README.fr.md" => On(nameof(WithFrenchReadme)) ? FrenchReadme : null,
            ".gitignore" => "__pycache__/\n*.pyc\n.venv/\nconfig.toml\n",
            "config.example.toml" => "# Copy to config.toml and change what your station needs.\n[station]\nname = \"Garden\"\nunits = \"metric\"\nrefresh_seconds = 60\n",
            "docs/setup.md" => SetUp,
            "station/__init__.py" => $"\"\"\"The weather station.\"\"\"\n\n__version__ = \"{_version}\"\n",
            "station/sensors.py" => Sensors(),
            GuideScenario.UnitsFile => Units(),
            "station/display.py" => Display(),
            "station/forecast.py" => On(nameof(SketchForecast)) ? Forecast() : null,
            "tests/test_units.py" => TestUnits(),
            "tests/test_forecast.py" => On(nameof(SketchForecast)) ? TestForecast : null,
            _ => null,
        };
        return text is null && throwIfMissing ? throw new ArgumentException($"The weather station has no file {path}.", nameof(path)) : text;
    }

    private string Readme()
    {
        var text = new StringBuilder("# Weather station\n\nReads the garden's sensors and shows the weather on a small display.\n");
        if (On(nameof(UpdateReadme)))
        {
            text.Append("\n## Running it\n\n1. Copy `config.example.toml` to `config.toml`.\n2. Run `python -m station`.\n\nThe display refreshes every minute.\n");
        }

        return text.ToString();
    }

    private const string FrenchReadme = "# Station météo\n\nLit les capteurs du jardin et affiche la météo sur un petit écran.\n";

    private const string SetUp = "# Setting up the station\n\n1. Wire the sensor to the board's I2C pins.\n2. Install Python 3.12.\n3. Copy `config.example.toml` to `config.toml`.\n";

    private string Sensors()
    {
        var text = new StringBuilder();
        text.Append("\"\"\"Reads the station's sensors.\"\"\"\n\n");
        if (On(nameof(LogSensorErrors)))
        {
            text.Append("import logging\n\n");
        }

        if (On(nameof(CacheReadings)))
        {
            text.Append("from functools import lru_cache\n\n");
        }

        text.Append("SENSOR_ADDRESS = 0x76\n");
        if (On(nameof(CheckWiring)))
        {
            text.Append("\n\ndef check_wiring(bus):\n    \"\"\"Fails early when the sensor does not answer at its address.\"\"\"\n    if bus.read_byte(SENSOR_ADDRESS) is None:\n        raise RuntimeError(\"No sensor at 0x76: check the wiring.\")\n");
        }

        if (On(nameof(CacheReadings)))
        {
            text.Append("\n\n@lru_cache(maxsize=1)\n");
        }
        else
        {
            text.Append("\n\n");
        }

        text.Append("def read_temperature(bus):\n");
        if (On(nameof(LogSensorErrors)))
        {
            text.Append("    try:\n        return bus.read_word(SENSOR_ADDRESS, 0xFA) / 100\n    except OSError:\n        logging.exception(\"The temperature sensor did not answer.\")\n        return None\n");
        }
        else
        {
            text.Append("    return bus.read_word(SENSOR_ADDRESS, 0xFA) / 100\n");
        }

        if (On(nameof(ReadHumidity)))
        {
            var limit = On(nameof(FixHumidityRange)) ? "min(max(value, 0), 100)" : "value";
            text.Append($"\n\ndef read_humidity(bus):\n    value = bus.read_word(SENSOR_ADDRESS, 0xFD) / 1024\n    return {limit}\n");
        }

        return text.ToString();
    }

    private string Units()
    {
        var text = new StringBuilder();
        text.Append(UnitsDocstring).Append("\"\"\"\n");
        if (On(nameof(RoundHalfUp)))
        {
            text.Append("\nfrom decimal import ROUND_HALF_UP, Decimal\n");
        }

        text.Append("\n\ndef celsius_to_fahrenheit(celsius):\n    return celsius * 9 / 5 + 32\n");
        text.Append("\n\ndef fahrenheit_to_celsius(fahrenheit):\n    return (fahrenheit - 32) * 5 / 9\n");
        if (On(nameof(SupportKelvin)))
        {
            text.Append("\n\ndef celsius_to_kelvin(celsius):\n    return celsius + 273.15\n");
        }

        text.Append("\n\ndef hectopascals_to_inches(hectopascals):\n    return hectopascals * 0.02953\n");
        if (On(nameof(ConvertWindSpeeds)))
        {
            text.Append("\n\ndef metres_per_second_to_kilometres_per_hour(speed):\n    return speed * 3.6\n");
            text.Append("\n\ndef metres_per_second_to_miles_per_hour(speed):\n    return speed * 2.23694\n");
        }

        if (On(nameof(RoundReadings)))
        {
            if (On(nameof(RoundToConfiguredDigits)))
            {
                text.Append("\n\ndef round_reading(value, digits=1):\n    return round(value, digits)\n");
            }
            else if (On(nameof(RoundHalfUp)))
            {
                text.Append("\n\ndef round_reading(value):\n    return float(Decimal(str(value)).quantize(Decimal(\"0.1\"), rounding=ROUND_HALF_UP))\n");
            }
            else
            {
                text.Append("\n\ndef round_reading(value):\n    return round(value, 1)\n");
            }
        }

        return text.ToString();
    }

    private string Display()
    {
        var text = new StringBuilder();
        text.Append("\"\"\"Shows the readings on the station's display.\"\"\"\n\n");
        text.Append("FONT_SIZE = 14\n");
        if (On(nameof(RefreshEveryMinute)))
        {
            text.Append("REFRESH_SECONDS = 60\n");
        }

        if (On(nameof(TidyDisplay)))
        {
            text.Append("\n\ndef _line(label, value, unit):\n    return f\"{label:<12}{value:>6} {unit}\"\n");
            text.Append("\n\ndef show(screen, reading):\n    screen.clear()\n    screen.write(_line(\"Temperature\", reading.temperature, \"°C\"), size=FONT_SIZE)\n    screen.write(_line(\"Humidity\", reading.humidity, \"%\"), size=FONT_SIZE)\n");
        }
        else
        {
            text.Append("\n\ndef show(screen, reading):\n    screen.clear()\n    screen.write(f\"Temperature {reading.temperature} °C\", size=FONT_SIZE)\n    screen.write(f\"Humidity {reading.humidity} %\", size=FONT_SIZE)\n");
        }

        if (On(nameof(ShowPressureTrend)))
        {
            text.Append("    screen.write(\"Pressure \" + (\"rising\" if reading.pressure_trend > 0 else \"falling\"), size=FONT_SIZE)\n");
        }

        if (On(nameof(ShowBatteryLevel)))
        {
            text.Append("    screen.write(f\"Battery {reading.battery} %\", size=FONT_SIZE - 2)\n");
        }

        return text.ToString();
    }

    private string Forecast()
    {
        var text = new StringBuilder("\"\"\"A simple forecast from the station's own readings.\"\"\"\n\n\ndef forecast(readings):\n");
        if (On(nameof(PredictRain)))
        {
            text.Append("    \"\"\"Rain is likely when the pressure fell by more than 3 hPa in three hours.\"\"\"\n    drop = readings[0].pressure - readings[-1].pressure\n    return \"rain likely\" if drop > 3 else \"settled\"\n");
        }
        else
        {
            text.Append("    return \"settled\"\n");
        }

        return text.ToString();
    }

    private string TestUnits()
    {
        var text = new StringBuilder("from station import units\n\n\ndef test_freezing_point():\n    assert units.celsius_to_fahrenheit(0) == 32\n");
        if (On(nameof(ConvertWindSpeeds)))
        {
            text.Append("\n\ndef test_wind_speed():\n    assert units.metres_per_second_to_kilometres_per_hour(10) == 36\n");
        }

        return text.ToString();
    }

    private const string TestForecast = "from types import SimpleNamespace\n\nfrom station.forecast import forecast\n\n\ndef test_falling_pressure_means_rain():\n    readings = [SimpleNamespace(pressure=1012), SimpleNamespace(pressure=1007)]\n    assert forecast(readings) == \"rain likely\"\n";
}

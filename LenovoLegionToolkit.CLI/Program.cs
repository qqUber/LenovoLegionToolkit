using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Reflection;
using System.Threading.Tasks;
using LenovoLegionToolkit.CLI.Lib;
using Spectre.Console;

namespace LenovoLegionToolkit.CLI;

public class Program
{
    private const string AppName = "LOQ Toolkit CLI";
    
    public static Task<int> Main(string[] args) => BuildCommandLine().InvokeAsync(args);

    private static Parser BuildCommandLine()
    {
        var root = new RootCommand(GetDescription());

        var builder = new CommandLineBuilder(root)
            .UseDefaults()
            .UseExceptionHandler(OnException);

        // Add all commands
        root.AddCommand(BuildQuickActionsCommand());
        root.AddCommand(BuildFeatureCommand());
        root.AddCommand(BuildSpectrumCommand());
        root.AddCommand(BuildRGBCommand());
        root.AddCommand(BuildStatusCommand());
        root.AddCommand(BuildInfoCommand());

        return builder.Build();
    }

    private static string GetDescription()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.4.0";
        return $"""
            {AppName} v{version}
            
            Control your Lenovo Legion/LOQ laptop from the command line.
            
            REQUIREMENTS:
              • LOQ Toolkit must be running in the background
              • CLI setting must be enabled in Settings
            
            EXAMPLES:
              llt feature get powerMode           Get current power mode
              llt feature set powerMode Balance   Set power mode to Balance
              llt qa "My Action"                  Run a quick action
              llt status                          Show system status
              llt spectrum brightness set 80      Set keyboard brightness
            """;
    }

    #region Status Command (NEW)
    
    private static Command BuildStatusCommand()
    {
        var cmd = new Command("status", "Show current system status overview");
        cmd.AddAlias("st");
        cmd.SetHandler(async () =>
        {
            await ShowStatusAsync();
        });
        return cmd;
    }

    private static async Task ShowStatusAsync()
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.Title = new TableTitle($"[bold blue]{AppName}[/] - System Status");
        table.AddColumn(new TableColumn("[bold]Setting[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold]Value[/]").RightAligned());

        try
        {
            // Get power mode
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Fetching status...", async ctx =>
                {
                    try
                    {
                        var powerMode = await IpcClient.GetFeatureValueAsync("powerModeState");
                        table.AddRow("Power Mode", GetColoredValue(powerMode));
                    }
                    catch { table.AddRow("Power Mode", "[dim]unavailable[/]"); }

                    try
                    {
                        var brightness = await IpcClient.GetSpectrumBrightnessAsync();
                        table.AddRow("Keyboard Brightness", $"[cyan]{brightness}%[/]");
                    }
                    catch { table.AddRow("Keyboard Brightness", "[dim]unavailable[/]"); }

                    try
                    {
                        var profile = await IpcClient.GetSpectrumProfileAsync();
                        table.AddRow("Keyboard Profile", $"[magenta]{profile}[/]");
                    }
                    catch { table.AddRow("Keyboard Profile", "[dim]unavailable[/]"); }
                });

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Run 'llt feature -l' to see all available features[/]");
        }
        catch (IpcConnectException)
        {
            ShowConnectionError();
        }
    }

    private static string GetColoredValue(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "quiet" => "[blue]Quiet[/] 🔇",
            "balance" or "balanced" => "[green]Balanced[/] ⚖️",
            "performance" => "[yellow]Performance[/] 🚀",
            "godmode" or "custom" => "[red]Custom[/] 🔥",
            "on" or "true" or "enabled" => "[green]✓ Enabled[/]",
            "off" or "false" or "disabled" => "[red]✗ Disabled[/]",
            _ => $"[white]{value}[/]"
        };
    }

    #endregion

    #region Info Command (NEW)
    
    private static Command BuildInfoCommand()
    {
        var cmd = new Command("info", "Show CLI and app information");
        cmd.AddAlias("i");
        cmd.SetHandler(() =>
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.4.0";
            
            var panel = new Panel(new Markup($"""
                [bold]Version:[/] {version}
                [bold]Runtime:[/] .NET {Environment.Version}
                [bold]OS:[/] {Environment.OSVersion}
                [bold]Machine:[/] {Environment.MachineName}
                
                [dim]For full documentation, visit:[/]
                [link]https://github.com/varun875/Varun-LLT[/]
                """))
            {
                Header = new PanelHeader($"[bold blue]{AppName}[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(2, 1)
            };
            
            AnsiConsole.Write(panel);
        });
        return cmd;
    }

    #endregion

    #region Quick Actions

    private static Command BuildQuickActionsCommand()
    {
        var nameArgument = new Argument<string>("name", "Name of the Quick Action") { Arity = ArgumentArity.ZeroOrOne };

        var listOption = new Option<bool>("--list", "List available Quick Actions") { Arity = ArgumentArity.ZeroOrOne };
        listOption.AddAlias("-l");

        var cmd = new Command("quickAction", "Run Quick Action");
        cmd.AddAlias("qa");
        cmd.AddArgument(nameArgument);
        cmd.AddOption(listOption);
        cmd.SetHandler(async (name, list) =>
        {
            if (list)
            {
                var result = await IpcClient.ListQuickActionsAsync();
                PrintList("Available Quick Actions", result);
                return;
            }

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Running '{name}'...", async ctx =>
                {
                    await IpcClient.RunQuickActionAsync(name);
                });
            
            AnsiConsole.MarkupLine($"[green]✓[/] Quick action '[bold]{name}[/]' executed successfully");
        }, nameArgument, listOption);
        
        cmd.AddValidator(result =>
        {
            if (result.FindResultFor(nameArgument) is not null)
                return;

            if (result.FindResultFor(listOption) is not null)
                return;

            result.ErrorMessage = $"{nameArgument.Name} or --{listOption.Name} should be specified";
        });

        return cmd;
    }

    #endregion

    #region Features

    private static Command BuildFeatureCommand()
    {
        var getCmd = BuildGetFeatureCommand();
        var setCmd = BuildSetFeatureCommand();

        var listOption = new Option<bool?>("--list", "List available features") { Arity = ArgumentArity.ZeroOrOne };
        listOption.AddAlias("-l");

        var cmd = new Command("feature", "Control features");
        cmd.AddAlias("f");
        cmd.AddCommand(getCmd);
        cmd.AddCommand(setCmd);
        cmd.AddOption(listOption);
        cmd.SetHandler(async list =>
        {
            if (!list.HasValue || !list.Value)
                return;

            var value = await IpcClient.ListFeaturesAsync();
            PrintList("Available Features", value);
        }, listOption);
        
        cmd.AddValidator(result =>
        {
            if (result.FindResultFor(getCmd) is not null)
                return;

            if (result.FindResultFor(setCmd) is not null)
                return;

            if (result.FindResultFor(listOption) is not null)
                return;

            result.ErrorMessage = $"{getCmd.Name}, {setCmd.Name} or --{listOption.Name} should be specified";
        });

        return cmd;
    }

    private static Command BuildGetFeatureCommand()
    {
        var nameArgument = new Argument<string>("name", "Name of the feature") { Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("get", "Get value of a feature");
        cmd.AddAlias("g");
        cmd.AddArgument(nameArgument);
        cmd.SetHandler(async name =>
        {
            var result = await IpcClient.GetFeatureValueAsync(name);
            AnsiConsole.MarkupLine($"[bold]{name}[/]: {GetColoredValue(result)}");
        }, nameArgument);

        return cmd;
    }

    private static Command BuildSetFeatureCommand()
    {
        var nameArgument = new Argument<string>("name", "Name of the feature") { Arity = ArgumentArity.ExactlyOne };
        var valueArgument = new Argument<string>("value", "Value of the feature") { Arity = ArgumentArity.ZeroOrOne };

        var listOption = new Option<bool>("--list", "List available feature values") { Arity = ArgumentArity.ZeroOrOne };
        listOption.AddAlias("-l");

        var cmd = new Command("set", "Set value of a feature");
        cmd.AddAlias("s");
        cmd.AddArgument(nameArgument);
        cmd.AddArgument(valueArgument);
        cmd.AddOption(listOption);
        cmd.SetHandler(async (name, value, list) =>
        {
            if (list)
            {
                var result = await IpcClient.ListFeatureValuesAsync(name);
                PrintList($"Available values for '{name}'", result);
                return;
            }

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Setting {name}...", async ctx =>
                {
                    await IpcClient.SetFeatureValueAsync(name, value);
                });
            
            AnsiConsole.MarkupLine($"[green]✓[/] [bold]{name}[/] set to [cyan]{value}[/]");
        }, nameArgument, valueArgument, listOption);
        
        cmd.AddValidator(result =>
        {
            if (result.FindResultFor(nameArgument) is not null)
                return;

            if (result.FindResultFor(listOption) is not null)
                return;

            result.ErrorMessage = $"{nameArgument.Name} or --{listOption.Name} should be specified";
        });

        return cmd;
    }

    #endregion

    #region Spectrum

    private static Command BuildSpectrumCommand()
    {
        var profileCommand = BuildSpectrumProfileCommand();
        var brightnessCommand = BuildSpectrumBrightnessCommand();

        var cmd = new Command("spectrum", "Control Spectrum backlight");
        cmd.AddAlias("s");
        cmd.AddCommand(profileCommand);
        cmd.AddCommand(brightnessCommand);
        return cmd;
    }

    private static Command BuildSpectrumProfileCommand()
    {
        var getCmd = BuildGetSpectrumProfileCommand();
        var setCmd = BuildSetSpectrumProfileCommand();

        var cmd = new Command("profile", "Control Spectrum backlight profile");
        cmd.AddAlias("p");
        cmd.AddCommand(getCmd);
        cmd.AddCommand(setCmd);

        return cmd;
    }

    private static Command BuildGetSpectrumProfileCommand()
    {
        var cmd = new Command("get", "Get current Spectrum profile");
        cmd.AddAlias("g");
        cmd.SetHandler(async () =>
        {
            var result = await IpcClient.GetSpectrumProfileAsync();
            AnsiConsole.MarkupLine($"[bold]Spectrum Profile[/]: [magenta]{result}[/]");
        });

        return cmd;
    }

    private static Command BuildSetSpectrumProfileCommand()
    {
        var valueArgument = new Argument<int>("profile", "Profile to set (1-6)") { Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("set", "Set current Spectrum profile");
        cmd.AddAlias("s");
        cmd.AddArgument(valueArgument);
        cmd.SetHandler(async value =>
        {
            await IpcClient.SetSpectrumProfileAsync($"{value}");
            AnsiConsole.MarkupLine($"[green]✓[/] Spectrum profile set to [magenta]{value}[/]");
        }, valueArgument);

        return cmd;
    }

    private static Command BuildSpectrumBrightnessCommand()
    {
        var getCmd = BuildGetSpectrumBrightnessCommand();
        var setCmd = BuildSetSpectrumBrightnessCommand();

        var cmd = new Command("brightness", "Control Spectrum brightness");
        cmd.AddAlias("b");
        cmd.AddCommand(getCmd);
        cmd.AddCommand(setCmd);

        return cmd;
    }

    private static Command BuildGetSpectrumBrightnessCommand()
    {
        var cmd = new Command("get", "Get current Spectrum brightness");
        cmd.AddAlias("g");
        cmd.SetHandler(async () =>
        {
            var result = await IpcClient.GetSpectrumBrightnessAsync();
            
            // Show a nice progress bar representation
            if (int.TryParse(result, out var brightness))
            {
                AnsiConsole.Write(new BarChart()
                    .Width(50)
                    .Label("[bold]Keyboard Brightness[/]")
                    .AddItem("Level", brightness, Color.Cyan1));
            }
            else
            {
                AnsiConsole.MarkupLine($"[bold]Brightness[/]: [cyan]{result}[/]");
            }
        });

        return cmd;
    }

    private static Command BuildSetSpectrumBrightnessCommand()
    {
        var valueArgument = new Argument<int>("brightness", "Brightness to set (0-100)") { Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("set", "Set current Spectrum brightness");
        cmd.AddAlias("s");
        cmd.AddArgument(valueArgument);
        cmd.SetHandler(async value =>
        {
            // Validate range
            if (value < 0 || value > 100)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Brightness must be between 0 and 100");
                return;
            }
            
            await IpcClient.SetSpectrumBrightnessAsync($"{value}");
            AnsiConsole.MarkupLine($"[green]✓[/] Brightness set to [cyan]{value}%[/]");
        }, valueArgument);

        return cmd;
    }

    #endregion

    #region RGB

    private static Command BuildRGBCommand()
    {
        var getCmd = BuildGetRGBCommand();
        var setCmd = BuildSetRGBCommand();

        var cmd = new Command("rgb", "Control RGB backlight preset");
        cmd.AddAlias("r");
        cmd.AddCommand(getCmd);
        cmd.AddCommand(setCmd);

        return cmd;
    }

    private static Command BuildGetRGBCommand()
    {
        var cmd = new Command("get", "Get current RGB preset");
        cmd.AddAlias("g");
        cmd.SetHandler(async () =>
        {
            var result = await IpcClient.GetRGBPresetAsync();
            AnsiConsole.MarkupLine($"[bold]RGB Preset[/]: [magenta]{result}[/]");
        });

        return cmd;
    }

    private static Command BuildSetRGBCommand()
    {
        var valueArgument = new Argument<int>("preset", "Preset to set") { Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("set", "Set current RGB preset");
        cmd.AddAlias("s");
        cmd.AddArgument(valueArgument);
        cmd.SetHandler(async value =>
        {
            await IpcClient.SetRGBPresetAsync($"{value}");
            AnsiConsole.MarkupLine($"[green]✓[/] RGB preset set to [magenta]{value}[/]");
        }, valueArgument);

        return cmd;
    }

    #endregion

    #region Helpers

    private static void PrintList(string title, string items)
    {
        var lines = items.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        AnsiConsole.MarkupLine($"[bold blue]{title}[/]");
        AnsiConsole.WriteLine();
        
        foreach (var line in lines)
        {
            AnsiConsole.MarkupLine($"  [dim]•[/] {line.Trim()}");
        }
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Total: {lines.Length} items[/]");
    }

    private static void ShowConnectionError()
    {
        var panel = new Panel(new Markup("""
            [red bold]Connection Failed[/]
            
            Could not connect to LOQ Toolkit.
            
            [bold]Please ensure:[/]
              1. LOQ Toolkit is running in the background
              2. CLI is enabled in Settings → Advanced → CLI
            
            [dim]Try running the main app first, then retry this command.[/]
            """))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Red),
            Padding = new Padding(2, 1)
        };
        
        AnsiConsole.Write(panel);
    }

    private static void OnException(Exception ex, InvocationContext context)
    {
        var message = ex switch
        {
            IpcConnectException => null, // Handle specially
            IpcException => ex.Message,
            _ => ex.ToString()
        };

        if (ex is IpcConnectException)
        {
            ShowConnectionError();
            context.ExitCode = -1;
            return;
        }

        var exitCode = ex switch
        {
            IpcConnectException => -1,
            IpcException => -2,
            _ => -99
        };

        AnsiConsole.MarkupLine($"[red bold]Error:[/] {Markup.Escape(message ?? "Unknown error")}");
        context.ExitCode = exitCode;
    }

    #endregion
}

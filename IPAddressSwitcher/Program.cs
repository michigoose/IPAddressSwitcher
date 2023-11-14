using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Diagnostics;

class Program
{
    static void Main()
    {
        Console.Write("*******************************************\r\n");
        Console.Write("*         Fast IP Address Switcher        *\r\n");
        Console.Write("*  Please note that this program requires *\r\n");
        Console.Write("*    Windows PowerShell to be installed.  *\r\n");
        Console.Write("*                                         *\r\n");
        Console.Write("*           Happy IP Changing!            *\r\n");
        Console.Write("*******************************************\r\n\r\n");
        
        NetworkInterface selectedAdapter = DisplayAvailableAdapters();
        if (selectedAdapter == null)
        {
            Console.WriteLine("Invalid adapter index. Exiting program.");
            WaitForExit();
            return;
        }

        Console.Write("Choose IP Configuration (1 for Static, 2 for DHCP): ");
        if (!int.TryParse(Console.ReadLine(), out int configurationChoice) || (configurationChoice != 1 && configurationChoice != 2))
        {
            Console.WriteLine("Invalid input. Exiting program.");
            WaitForExit();
            return;
        }

        if (configurationChoice == 1)
        {
            ConfigureStaticIP(selectedAdapter);
        }
        else
        {
            ConfigureDHCP(selectedAdapter);
        }

        // Read back adapter settings
        DisplayAdapterSettings(selectedAdapter);

        WaitForExit();
    }

    static NetworkInterface DisplayAvailableAdapters()
    {
        Console.WriteLine("Choose an adapter to change or exit:");

        NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

        for (int i = 0; i < adapters.Length; i++)
        {
            Console.WriteLine($"{i + 1}. {adapters[i].Name} ({adapters[i].Description})");
        }

        Console.WriteLine($"{adapters.Length + 1}. Exit");

        Console.Write("Choose an option: ");
        int choice;

        while (!int.TryParse(Console.ReadLine(), out choice) || choice < 1 || choice > adapters.Length + 1)
        {
            Console.WriteLine("Invalid choice. Please enter a valid number.");
            Console.Write("Choose an option: ");
        }

        // Check if the user chose the "Exit" option
        if (choice == adapters.Length + 1)
        {
            Environment.Exit(0); // Gracefully exit the program
        }

        return adapters[choice - 1];
    }


    static void ConfigureStaticIP(NetworkInterface adapter)
    {
        Console.Write("Enter IP Address: ");
        string ipAddress = Console.ReadLine();

        Console.Write("Enter Subnet Mask (press Enter for default 255.255.255.0): ");
        string subnetMask = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(subnetMask))
        {
            subnetMask = "255.255.255.0";
        }

        // Generate default gateway based on the entered IP address
        string[] ipParts = ipAddress.Split('.');
        if (ipParts.Length == 4 && int.TryParse(ipParts[0], out int firstOctet) && int.TryParse(ipParts[1], out int secondOctet) && int.TryParse(ipParts[2], out int thirdOctet))
        {
            string defaultGateway = $"{firstOctet}.{secondOctet}.{thirdOctet}.1";
            Console.Write($"Enter Gateway (press Enter for default {defaultGateway}): ");
            string customGateway = Console.ReadLine();
            string gateway = string.IsNullOrWhiteSpace(customGateway) ? defaultGateway : customGateway;

            SetStaticIPConfiguration(adapter, ipAddress, subnetMask, gateway);
        }
        else
        {
            Console.WriteLine("Invalid IP address format. Exiting program.");
        }
    }

    static void SetStaticIPConfiguration(NetworkInterface adapter, string ipAddress, string subnetMask, string gateway)
    {
        UnicastIPAddressInformationCollection unicastIPs = adapter.GetIPProperties().UnicastAddresses;

        foreach (UnicastIPAddressInformation unicastIP in unicastIPs)
        {
            if (unicastIP.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                Console.WriteLine($"Configuring {adapter.Name} with static IP: {ipAddress}, Subnet Mask: {subnetMask}, Gateway: {gateway}");
                Console.WriteLine("Please note that administrative privileges might be required for this operation.");
                adapter.EnableStaticIP(ipAddress, subnetMask, gateway);
                return;
            }
        }

        Console.WriteLine($"Error: {adapter.Name} does not support IPv4 or does not have an existing IPv4 configuration.");
    }

    static void ConfigureDHCP(NetworkInterface adapter)
    {
        Console.WriteLine($"Configuring {adapter.Name} to use DHCP...");
        Console.WriteLine("Please note that administrative privileges might be required for this operation.");
        adapter.EnableDHCP();
    }
    static void DisplayAdapterSettings(NetworkInterface adapter)
    {
        Console.WriteLine($"Current settings for {adapter.Description} ({adapter.Name}):");

        string script = $"Get-WmiObject Win32_NetworkAdapterConfiguration | Where-Object {{ $_.Description -eq '{adapter.Description}' }}";
        string output = RunPowerShellScript(script);

        Console.WriteLine(output);
    }
    private static string RunPowerShellScript(string script)
    {
        using (Process PowerShellProcess = new Process())
        {
            PowerShellProcess.StartInfo.FileName = "powershell.exe";
            PowerShellProcess.StartInfo.Arguments = $"-NoProfile -ExecutionPolicy unrestricted -Command {script}";
            PowerShellProcess.StartInfo.UseShellExecute = false;
            PowerShellProcess.StartInfo.RedirectStandardOutput = true;
            PowerShellProcess.StartInfo.CreateNoWindow = true;

            PowerShellProcess.Start();

            string output = PowerShellProcess.StandardOutput.ReadToEnd();

            PowerShellProcess.WaitForExit();

            return output;
        }
    }
    static string GetIPAddress(NetworkInterface adapter)
    {
        foreach (UnicastIPAddressInformation unicastIP in adapter.GetIPProperties().UnicastAddresses)
        {
            if (unicastIP.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                return unicastIP.Address.ToString();
            }
        }

        return "Not available";
    }

    static string GetSubnetMask(NetworkInterface adapter)
    {
        foreach (UnicastIPAddressInformation unicastIP in adapter.GetIPProperties().UnicastAddresses)
        {
            if (unicastIP.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                return unicastIP.IPv4Mask.ToString();
            }
        }

        return "Not available";
    }

    static string GetGateway(NetworkInterface adapter)
    {
        foreach (GatewayIPAddressInformation gatewayIP in adapter.GetIPProperties().GatewayAddresses)
        {
            if (gatewayIP.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                return gatewayIP.Address.ToString();
            }
        }

        return "Not available";
    }
    static void WaitForExit()
    {
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}


public static class NetworkInterfaceExtensions
{
    public static void EnableStaticIP(this NetworkInterface adapter, string ipAddress, string subnetMask, string gateway)
    {
        try
        {

            // Disable DHCP first
            DisableDHCP(adapter);

            // Set static IP configuration
            ProcessRunner.RunCommand($"netsh interface ip set address \"{adapter.Name}\" static {ipAddress} {subnetMask} {gateway}");

            // Set DNS settings to 0.0.0.0
            SetDNS(adapter, "0.0.0.0", "0.0.0.0");

            Console.WriteLine("Static IP configuration applied successfully.");
        } catch (Exception ex)
        {
            Console.WriteLine($"Error configuring Static IP: {ex.Message}");
        }
    }

    private static void DisableDHCP(NetworkInterface adapter)
    {
        ProcessRunner.RunCommand($"netsh interface ip set address \"{adapter.Name}\" dhcp");
    }

    private static void SetDNS(NetworkInterface adapter, string preferredDNS, string alternateDNS)
    {
        ProcessRunner.RunCommand($"netsh interface ip set dns \"{adapter.Name}\" static {preferredDNS} primary validate=no");
        ProcessRunner.RunCommand($"netsh interface ip add dns \"{adapter.Name}\" {alternateDNS} index=2 validate=no");
    }

    public static void EnableDHCP(this NetworkInterface adapter)
    {
        try
        {
            // Enable DHCP for both IP and DNS settings
            ProcessRunner.RunCommand($"netsh interface ip set address \"{adapter.Name}\" dhcp");
            ProcessRunner.RunCommand($"netsh interface ip set dns \"{adapter.Name}\" dhcp");

            Console.WriteLine("DHCP configuration applied successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error configuring DHCP: {ex.Message}");
        }
    }
}

public static class ProcessRunner
{
    public static void RunCommand(string command)
    {
        System.Diagnostics.Process process = new System.Diagnostics.Process();
        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
        {
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            FileName = "cmd.exe",
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.StartInfo = startInfo;
        process.Start();

        using (System.IO.StreamWriter sw = process.StandardInput)
        {
            if (sw.BaseStream.CanWrite)
            {
                sw.WriteLine(command);
            }
        }

        process.WaitForExit();
        process.Close();
    }
}

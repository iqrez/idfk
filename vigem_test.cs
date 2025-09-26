using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;

class ViGEmTest
{
    static void Main()
    {
        try
        {
            Console.WriteLine("Testing ViGEm Client...");

            using (var client = new ViGEmClient())
            {
                Console.WriteLine("ViGEm Client created successfully");

                var controller = client.CreateXbox360Controller();
                Console.WriteLine("Xbox 360 controller target created successfully");

                controller.Connect();
                Console.WriteLine("Controller connected successfully");

                // Test basic functionality
                controller.SetButtonState(Xbox360Button.A, true);
                Console.WriteLine("Button A pressed");

                controller.SetButtonState(Xbox360Button.A, false);
                Console.WriteLine("Button A released");

                controller.Disconnect();
                Console.WriteLine("Controller disconnected successfully");
            }

            Console.WriteLine("ViGEm smoke test PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ViGEm smoke test FAILED: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
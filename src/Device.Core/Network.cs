using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Net.NetworkInformation;
using System.Threading;

namespace Device.Core
{
	public class Network
	{
		public static void InitDhcpNetwork()
		{
			// write your code here
			NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

			foreach (NetworkInterface networkInterface in networkInterfaces)
			{
				if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
				{
					if (!networkInterface.IsDhcpEnabled)
					{
						// Switch to DHCP ...
						networkInterface.EnableDhcp();
						networkInterface.RenewDhcpLease();
						Thread.Sleep(10000);
					}

					Debug.Print("IP Address: " + networkInterface.IPAddress);
					Debug.Print("Subnet mask " + networkInterface.SubnetMask);
				}
			}
		}
		public static void InitStaticNetwork(string ipAddress,string subnetMask,string gatway)
		{
			// write your code here
			NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

			foreach (NetworkInterface networkInterface in networkInterfaces)
			{
				if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
				{
					if (networkInterface.IPAddress != ipAddress || networkInterface.IsDhcpEnabled || networkInterface.SubnetMask != subnetMask || networkInterface.GatewayAddress != gatway)
					{
						// Switch to Static ...
						networkInterface.EnableStaticIP(ipAddress, subnetMask, gatway);
					}
					Debug.Print("IP Address: " + networkInterface.IPAddress);
					Debug.Print("Subnet mask " + networkInterface.SubnetMask);
					Debug.Print("Gateway " + networkInterface.GatewayAddress);
					Debug.Print("DynamicDns " + networkInterface.IsDynamicDnsEnabled);
					//Debug.Print("Physical " + networkInterface.PhysicalAddress);
					for (int i = 0; i < networkInterface.DnsAddresses.Length; i++)
					{
						Debug.Print("Dns " + networkInterface.DnsAddresses[i]);
					}
					break;
				}
			}
		}
	}
}
